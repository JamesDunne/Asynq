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
            internal SqlCommand Command;
            internal ConstructedQuery<Tparameters, Tcontext, Tresult> Query;

            public AsyncSqlExecState(SqlCommand cmd, ConstructedQuery<Tparameters, Tcontext, Tresult> query)
            {
                Command = cmd;
                Query = query;
            }
        }

        private class AsyncSqlCeExecutor<Tparameters, Tcontext, Tresult> : IObservable<Tresult>
            where Tparameters : struct
            where Tcontext : System.Data.Linq.DataContext
            where Tresult : class
        {
            private SqlCeCommand Command;
            private ConstructedQuery<Tparameters, Tcontext, Tresult> Query;

            public AsyncSqlCeExecutor(ConstructedQuery<Tparameters, Tcontext, Tresult> query, SqlCeCommand cmd)
            {
                Command = cmd;
                Query = query;
            }

            #region IObservable<Tresult> Members

            public IDisposable Subscribe(IObserver<Tresult> observer)
            {
                try
                {
                    Command.Connection.Open();

                    using (var dr = Command.ExecuteReader())
                    {
                        // TODO: reverse-engineer LINQ-to-SQL's exact column mapping policy and use that to translate
                        // columns from the DbDataReader to the ElementType. `db.Translate()` does not use the same
                        // column mapping policy and is incompatible with anonymous types.

                        while (dr.Read())
                        {
                            int endOrdinal;
                            object row = materializeType(Query.Query.ElementType, dr, 0, out endOrdinal);

                            Tresult tmp = Query.RowProjection(row);
                            observer.OnNext(tmp);
                        }

#if false
                        // Materialize the DbDataReader rows into objects of the proper element type per the IQueryable:
                        IEnumerable rows = Query.Context.Translate(Query.Query.ElementType, dr);

                        foreach (object row in rows)
                        {
                            Tresult tmp = Query.RowProjection(row);
                            observer.OnNext(tmp);
                        }
#endif
                        observer.OnCompleted();
                    }

                    return Disposable.Empty;
                }
                finally
                {
                    Command.Connection.Close();
                }
            }

            private object materializeType(Type ty, DbDataReader dr, int startOrdinal, out int endOrdinal)
            {
                if (ty.Name.StartsWith("<>f__AnonymousType"))
                {
                    return materializeAnonymousType(ty, dr, startOrdinal, out endOrdinal);
                }
                else
                {
                    // Assume concrete type with writable properties:
                    throw new NotImplementedException();
                }
            }

            private object materializeAnonymousType(Type ty, DbDataReader dr, int startOrdinal, out int endOrdinal)
            {
                Debug.Assert(ty.IsGenericType);
                
                ConstructorInfo[] ctors = ty.GetConstructors();
                Debug.Assert(ctors.Length == 1);

                ParameterInfo[] prms = ctors[0].GetParameters();
                object[] objs = new object[prms.Length];
                
                int currOrdinal = startOrdinal;
                for (int i = 0; i < prms.Length; ++i)
                {
                    int lastOrdinal;
                    // TODO: anonymous type with columns?
                    objs[i] = materializeType(prms[i].ParameterType, dr, currOrdinal, out lastOrdinal);
                    currOrdinal = lastOrdinal;
                }
                endOrdinal = currOrdinal;

                return Activator.CreateInstance(ty, objs);
            }

            #endregion
        }

        // Attempt async execution:
        public static IObservable<Tresult> AsyncExecuteQuery<Tparameters, Tcontext, Tresult>(this Tcontext db, QueryDescriptor<Tparameters, Tcontext, Tresult> descriptor, Tparameters parameters)
            where Tparameters : struct
            where Tcontext : System.Data.Linq.DataContext
            where Tresult : class
        {
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
            Console.WriteLine(cmd.CommandText);
            Console.WriteLine();
#endif

            if (cmd is SqlCeCommand)
            {
                SqlCeCommand sqlcmd = (SqlCeCommand)cmd;
                return new AsyncSqlCeExecutor<Tparameters, Tcontext, Tresult>(query, sqlcmd);
            }
            else if (cmd is SqlCommand)
            {
                SqlCommand sqlcmd = (SqlCommand)cmd;

                AsyncSubject<Tresult> subj = new AsyncSubject<Tresult>();

                // Create the async completion callback:
                AsyncCallback callback = delegate(IAsyncResult iar)
                {
                    var st = (AsyncSqlExecState<Tparameters, Tcontext, Tresult>)iar.AsyncState;

                    SqlDataReader dr = null;

                    try
                    {
                        // Get the data reader:
                        dr = st.Command.EndExecuteReader(iar);

                        // TODO: reverse-engineer LINQ-to-SQL's exact column mapping policy and use that to translate
                        // columns from the DbDataReader to the ElementType. `db.Translate()` does not use the same
                        // column mapping policy and is incompatible with anonymous types.

                        // Materialize the DbDataReader rows into objects of the proper element type per the IQueryable:
                        IEnumerable rows = st.Query.Context.Translate(st.Query.Query.ElementType, dr);

                        foreach (object row in rows)
                        {
                            Tresult tmp = st.Query.RowProjection(row);
                            subj.OnNext(tmp);
                        }
                    }
                    catch (Exception ex)
                    {
                        subj.OnError(ex);
                        return;
                    }
                    finally
                    {
                        if (dr != null) dr.Dispose();
                    }

                    subj.OnCompleted();
                };

                // Start the async IO to the database:
                sqlcmd.BeginExecuteReader(
                    callback
                   ,new AsyncSqlExecState<Tparameters, Tcontext, Tresult>(sqlcmd, query)
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
