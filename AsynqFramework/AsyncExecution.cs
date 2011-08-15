using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;

using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Data.SqlServerCe;
using System.Data.Common;
using System.Reactive.Disposables;
using AsynqFramework.CodeWriter;
using System.Collections;
using System.Diagnostics;
using System.Reflection;
using AsynqFramework.Materialization;
using System.Threading;
using System.Data;

namespace AsynqFramework
{
    public static class Asynq
    {
        // NOTE: EF currently does not support async IO. Terrible. Back to LINQ-to-SQL.
        // LINQ-to-SQL is a bit better in this area, but not by much. We have a problem
        // in that we cannot select anonymous / complex mapped types via LINQ queries.
        // The Translate() method supports flat models only with unique column name
        // mappings, which might be a blessing in disguise.

        /// <summary>
        /// MS SQL-CE "fake async" query executor; uses a separate thread with synchronous I/O since
        /// SqlCeCommand does not expose the async I/O pattern.
        /// </summary>
        /// <typeparam name="Tcontext"></typeparam>
        /// <typeparam name="Tparameters"></typeparam>
        /// <typeparam name="Tresult"></typeparam>
        private class AsyncSqlCeExecutor<Tcontext, Tparameters, Tresult> : IObservable<List<Tresult>>
            where Tcontext : System.Data.Linq.DataContext
            where Tparameters : struct
            where Tresult : class
        {
            private Tcontext Context;
            private SqlCeCommand Command;
            private ConstructedQuery<Tcontext, Tparameters, Tresult> Constructed;
            private int ExpectedCount;

            internal AsyncSqlCeExecutor(Tcontext context, SqlCeCommand cmd, ConstructedQuery<Tcontext, Tparameters, Tresult> constructed, int expectedCount = 10)
            {
                this.Context = context;
                this.Command = cmd;
                this.Constructed = constructed;
                Debug.Assert(expectedCount >= 0);
                this.ExpectedCount = expectedCount;
            }

            #region IObservable<Tresult> Members

            public IDisposable Subscribe(IObserver<List<Tresult>> observer)
            {
                return Observable.Create<List<Tresult>>((ob) =>
                {
                    try
                    {
                        Debug.Assert(Command.Connection != null);
                        Debug.Assert(Command.Connection.State == System.Data.ConnectionState.Open);
                        //Command.Connection.Open();

                        using (var dr = Command.ExecuteReader())
                        {
                            var materializer = new DataRecordObjectMaterializer();
                            var mapping = materializer.BuildMaterializationMapping(Constructed.Query.Expression, Constructed.Query.ElementType, dr);

                            // Build a List so we can get out of here as soon as possible:
                            List<Tresult> items = new List<Tresult>(ExpectedCount);
                            while (dr.Read())
                            {
                                object row = materializer.Materialize(mapping, dr);

                                Tresult tmp = Constructed.RowProjection(row);
                                items.Add(tmp);
                            }

                            // Cave Johnson. We're done here.
                            ob.OnNext(items);
                            ob.OnCompleted();
                        }

                        return Disposable.Empty;
                    }
                    finally
                    {
                        Command.Connection.Close();
                        Context.Dispose();
                    }
                }).Subscribe(observer);
            }

            #endregion
        }

        /// <summary>
        /// SQL Server 200[058] async I/O query executor using true async I/O pattern via SqlCommand.
        /// </summary>
        /// <typeparam name="Tcontext"></typeparam>
        /// <typeparam name="Tparameters"></typeparam>
        /// <typeparam name="Tresult"></typeparam>
        private class AsyncSqlState<Tcontext, Tparameters, Tresult>
            where Tcontext : System.Data.Linq.DataContext
            where Tparameters : struct
            where Tresult : class
        {
            internal Tcontext Context;
            internal SqlCommand Command;
            internal ConstructedQuery<Tcontext, Tparameters, Tresult> Constructed;
            internal int ExpectedCount;

            internal AsyncSqlState(Tcontext context, SqlCommand cmd, ConstructedQuery<Tcontext, Tparameters, Tresult> constructed, int expectedCount = 10)
            {
                Context = context;
                Command = cmd;
                Constructed = constructed;
                Debug.Assert(expectedCount >= 0);
                ExpectedCount = expectedCount;
            }
        }

        public static IObservable<List<Tresult>> ExecuteQuery<Tcontext, Tparameters, Tresult>(Func<Tcontext> createContext, QueryDescriptor<Tcontext, Tparameters, Tresult> descriptor, Tparameters parameters, int expectedCount = 10)
            where Tcontext : System.Data.Linq.DataContext
            where Tparameters : struct
            where Tresult : class
        {
            var db = createContext();

            // Construct an IQueryable given the parameters and datacontext:
            var query = descriptor.Construct(db, parameters);

            // Get the DbCommand used to execute the query:
            DbCommand cmd = db.GetCommand(query.Query);

#if DEBUG
            // Dump the linq query to the console, colored:
            Console.WriteLine();
            var cw = new ConsoleColoredCodeWriter();
            new AsynqFramework.QueryProjector.LinqSyntaxExpressionFormatter(cw).WriteFormat(query.Query.Expression, Console.Out);
            Console.WriteLine();

            // Dump the SQL query:
            Console.WriteLine();
            foreach (DbParameter prm in cmd.Parameters)
            {
                Console.WriteLine("SET {0} = {1};", prm.ParameterName, prm.Value);
            }
            Console.WriteLine();
            Console.WriteLine(cmd.CommandText);
            Console.WriteLine();
#endif

            // Connection must be unique per DataContext:
            cmd.Connection.Open();

            if (cmd is SqlCeCommand)
            {
                SqlCeCommand sqlcmd = (SqlCeCommand)cmd;

                return new AsyncSqlCeExecutor<Tcontext, Tparameters, Tresult>(db, sqlcmd, query, expectedCount);
            }
            else if (cmd is SqlCommand)
            {
                SqlCommand sqlcmd = (SqlCommand)cmd;

                AsyncSubject<List<Tresult>> subj = new AsyncSubject<List<Tresult>>();

                // Create the async completion callback:
                AsyncCallback callback = delegate(IAsyncResult iar)
                {
                    Debug.WriteLine("Ending async on Thread ID #{0}...", Thread.CurrentThread.ManagedThreadId);

                    var st = (AsyncSqlState<Tcontext, Tparameters, Tresult>)iar.AsyncState;

                    SqlDataReader dr = null;

                    try
                    {
                        // Get the data reader:
                        dr = st.Command.EndExecuteReader(iar);

                        var materializer = new DataRecordObjectMaterializer();
                        var mapping = materializer.GetCachedMaterializationMapping(st.Constructed.Query.Expression, st.Constructed.Query.ElementType, dr);

                        // Build a List so we can get out of here as soon as possible:
                        List<Tresult> items = new List<Tresult>(st.ExpectedCount);
                        while (dr.Read())
                        {
                            object row = materializer.Materialize(mapping, dr);

                            Tresult tmp = st.Constructed.RowProjection(row);
                            items.Add(tmp);
                        }

                        // Notify the subject of the result List:
                        subj.OnNext(items);
                    }
                    catch (Exception ex)
                    {
                        // TODO: log the exception here for best exception locality and stack-trace preservation.

                        // Push the exception to the subject:
                        subj.OnError(ex);
                        return;
                    }
                    finally
                    {
                        // TODO: more robust clean-up code
                        if (dr != null) dr.Dispose();
                        st.Context.Dispose();
                    }

                    subj.OnCompleted();
                };

                Debug.Assert(sqlcmd.Connection != null);
                Debug.Assert(sqlcmd.Connection.State == System.Data.ConnectionState.Open);
                //sqlcmd.Connection.Open();

                // Start the async IO to the database:
                sqlcmd.BeginExecuteReader(
                    callback
                   ,new AsyncSqlState<Tcontext, Tparameters, Tresult>(db, sqlcmd, query, expectedCount)
                   ,System.Data.CommandBehavior.CloseConnection
                );

                return subj.AsObservable();
            }
            else
            {
                throw new NotSupportedException();
            }
        }

        public static List<Tresult> ExecuteQuerySync<Tcontext, Tparameters, Tresult>(Func<Tcontext> createContext, QueryDescriptor<Tcontext, Tparameters, Tresult> descriptor, Tparameters parameters, int expectedCount = 10)
            where Tcontext : System.Data.Linq.DataContext
            where Tparameters : struct
            where Tresult : class
        {
            using (var db = createContext())
            {
                // Construct an IQueryable given the parameters and datacontext:
                var query = descriptor.Construct(db, parameters);

                // Get the DbCommand used to execute the query:
                DbCommand cmd = db.GetCommand(query.Query);

#if DEBUG
                // Dump the linq query to the console, colored:
                Console.WriteLine();
                var cw = new ConsoleColoredCodeWriter();
                new AsynqFramework.QueryProjector.LinqSyntaxExpressionFormatter(cw).WriteFormat(query.Query.Expression, Console.Out);
                Console.WriteLine();

                // Dump the SQL query:
                Console.WriteLine();
                foreach (DbParameter prm in cmd.Parameters)
                {
                    Console.WriteLine("SET {0} = {1};", prm.ParameterName, prm.Value);
                }
                Console.WriteLine();
                Console.WriteLine(cmd.CommandText);
                Console.WriteLine();
#endif

                // Connection must be unique per DataContext:
                cmd.Connection.Open();

                SqlCeCommand sqlcmdCE;
                SqlCommand sqlcmd;
                IDataReader dr;

                if ((sqlcmdCE = cmd as SqlCeCommand) != null)
                {
                    Debug.Assert(sqlcmdCE.Connection != null);
                    Debug.Assert(sqlcmdCE.Connection.State == System.Data.ConnectionState.Open);

                    dr = sqlcmdCE.ExecuteReader(CommandBehavior.CloseConnection);
                }
                else if ((sqlcmd = cmd as SqlCommand) != null)
                {
                    Debug.Assert(sqlcmd.Connection != null);
                    Debug.Assert(sqlcmd.Connection.State == System.Data.ConnectionState.Open);

                    // Start the async IO to the database:
                    dr = sqlcmd.ExecuteReader(System.Data.CommandBehavior.CloseConnection);
                }
                else
                {
                    throw new NotSupportedException();
                }

                var materializer = new DataRecordObjectMaterializer();
                var mapping = materializer.GetCachedMaterializationMapping(query.Query.Expression, query.Query.ElementType, dr);

                // Build a List so we can get out of here as soon as possible:
                List<Tresult> items = new List<Tresult>(expectedCount);
                while (dr.Read())
                {
                    object row = materializer.Materialize(mapping, dr);

                    Tresult tmp = query.RowProjection(row);
                    items.Add(tmp);
                }

                return items;
            }
        }
    }
}
