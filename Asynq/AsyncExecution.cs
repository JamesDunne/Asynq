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
using Asynq.CodeWriter;
using System.Collections;
using System.Diagnostics;
using System.Reflection;
using Asynq.Materialization;
using System.Threading;

namespace Asynq
{
    public static class AsyncExecution
    {
        // NOTE: EF currently does not support async IO. Terrible. Back to LINQ-to-SQL.
        // LINQ-to-SQL is a bit better in this area, but not by much. We have a problem
        // in that we cannot select anonymous / complex mapped types via LINQ queries.
        // The Translate() method supports flat models only with unique column name
        // mappings, which might be a blessing in disguise.

        private struct AsyncSqlExecState<Tparameters, Tcontext, Tresult>
            where Tparameters : struct
            where Tcontext : System.Data.Linq.DataContext
            where Tresult : class
        {
            internal Tcontext Context;
            internal SqlCommand Command;
            internal ConstructedQuery<Tparameters, Tcontext, Tresult> Query;

            public AsyncSqlExecState(Tcontext context, SqlCommand cmd, ConstructedQuery<Tparameters, Tcontext, Tresult> query)
            {
                Context = context;
                Command = cmd;
                Query = query;
            }
        }

        private class AsyncSqlCeExecutor<Tparameters, Tcontext, Tresult> : IObservable<List<Tresult>>
            where Tparameters : struct
            where Tcontext : System.Data.Linq.DataContext
            where Tresult : class
        {
            private Tcontext Context;
            private SqlCeCommand Command;
            private ConstructedQuery<Tparameters, Tcontext, Tresult> Query;

            public AsyncSqlCeExecutor(Tcontext context, SqlCeCommand cmd, ConstructedQuery<Tparameters, Tcontext, Tresult> query)
            {
                Context = context;
                Command = cmd;
                Query = query;
            }

            #region IObservable<Tresult> Members

            public IDisposable Subscribe(IObserver<List<Tresult>> observer)
            {
                try
                {
                    Debug.Assert(Command.Connection != null);
                    Debug.Assert(Command.Connection.State == System.Data.ConnectionState.Open);
                    //Command.Connection.Open();

                    using (var dr = Command.ExecuteReader())
                    {
                        var materializer = new DbDataReaderObjectMaterializer();
                        var mapping = materializer.BuildMaterializationMapping(Query.Query.ElementType, dr);

                        // Build a List so we can get out of here as soon as possible:
                        List<Tresult> items = new List<Tresult>();
                        while (dr.Read())
                        {
                            object row = materializer.Materialize(mapping);

                            Tresult tmp = Query.RowProjection(row);
                            items.Add(tmp);
                        }

                        // Cave Johnson. We're done here.
                        observer.OnNext(items);
                        observer.OnCompleted();
                    }

                    return Disposable.Empty;
                }
                finally
                {
                    Command.Connection.Close();
                    Context.Dispose();
                }
            }

            #endregion
        }

        // Attempt async execution:
        public static IObservable<List<Tresult>> AsyncExecuteQuery<Tparameters, Tcontext, Tresult>(this Func<Tcontext> createContext, QueryDescriptor<Tparameters, Tcontext, Tresult> descriptor, Tparameters parameters)
            where Tparameters : struct
            where Tcontext : System.Data.Linq.DataContext
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
            new Asynq.QueryProjector.LinqSyntaxExpressionFormatter(cw).WriteFormat(query.Query.Expression, Console.Out);
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

            // Connection must be unique per DataContext instead:
            cmd.Connection.Open();

            if (cmd is SqlCeCommand)
            {
                SqlCeCommand sqlcmd = (SqlCeCommand)cmd;
                
                return new AsyncSqlCeExecutor<Tparameters, Tcontext, Tresult>(db, sqlcmd, query);
            }
            else if (cmd is SqlCommand)
            {
                SqlCommand sqlcmd = (SqlCommand)cmd;

                AsyncSubject<List<Tresult>> subj = new AsyncSubject<List<Tresult>>();

                // Create the async completion callback:
                AsyncCallback callback = delegate(IAsyncResult iar)
                {
                    Console.WriteLine("Ending async on Thread ID #{0}...", Thread.CurrentThread.ManagedThreadId);

                    var st = (AsyncSqlExecState<Tparameters, Tcontext, Tresult>)iar.AsyncState;

                    SqlDataReader dr = null;

                    try
                    {
                        // Get the data reader:
                        dr = st.Command.EndExecuteReader(iar);

                        var materializer = new DbDataReaderObjectMaterializer();
                        var mapping = materializer.BuildMaterializationMapping(st.Query.Query.ElementType, dr);

                        // Build a List so we can get out of here as soon as possible:
                        List<Tresult> items = new List<Tresult>();
                        while (dr.Read())
                        {
                            object row = materializer.Materialize(mapping);

                            Tresult tmp = st.Query.RowProjection(row);
                            items.Add(tmp);
                        }

                        subj.OnNext(items);
                    }
                    catch (Exception ex)
                    {
                        subj.OnError(ex);
                        return;
                    }
                    finally
                    {
                        if (dr != null) dr.Dispose();
                        st.Context.Dispose();
                    }

                    subj.OnCompleted();
                };

                Debug.Assert(sqlcmd.Connection != null);
                Debug.Assert(sqlcmd.Connection.State == System.Data.ConnectionState.Open);
                //sqlcmd.Connection.Open();

                // Start the async IO to the database:
                Console.WriteLine("Beginning async on Thread ID #{0}...", Thread.CurrentThread.ManagedThreadId);
                sqlcmd.BeginExecuteReader(
                    callback
                   ,new AsyncSqlExecState<Tparameters, Tcontext, Tresult>(db, sqlcmd, query)
                   ,System.Data.CommandBehavior.CloseConnection
                );

                return subj.AsObservable();
            }
            else
            {
                throw new NotSupportedException();
            }
        }
    }
}
