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
                        foreach (object row in Query.Context.Translate(Query.Query.ElementType, dr))
                        {
                            Tresult tmp = Query.RowProjection(row);
                            observer.OnNext(tmp);
                        }
                        observer.OnCompleted();
                    }

                    return Disposable.Empty;
                }
                finally
                {
                    Command.Connection.Close();
                }
            }

            #endregion
        }

        // Attempt async execution:
        public static IObservable<Tresult> AsyncExecuteQuery<Tparameters, Tcontext, Tresult>(this Tcontext db, QueryDescriptor<Tparameters, Tcontext, Tresult> descriptor, Tparameters parameters)
            where Tparameters : struct
            where Tcontext : System.Data.Linq.DataContext
            where Tresult : class
        {
            var query = descriptor.Construct(db, parameters);

            // Assume the IQueryable was constructed via LINQ-to-SQL:
            DbCommand cmd = db.GetCommand(query.Query);

            if (cmd is SqlCeCommand)
            {
                SqlCeCommand sqlcmd = (SqlCeCommand)cmd;

                Console.WriteLine(sqlcmd.CommandText);

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

                        // FIXME: serious problem here trying to map up complex element types.
                        // How to get the appropriate column mapping? Possibly flatten out the
                        // anonymous types to simple types with unique property names.

                        // Probably will end up having to disallow anonymous types altogether and
                        // code-gen a flattened class containing all properties from grouped models
                        // that can then be expanded out to individual models, or just drop the
                        // requirement to deal with fully-populated models and use the flattened
                        // class as-is. Benefit of this is querying only what you need to use but we
                        // lose the client-side implementation flexibility.

                        // Use System.Reflection.Emit.AssemblyBuilder to generate a deflated type to
                        // hold mapping information to/from the ElementType's nested properties.
                        // Generate a method within to inflate to the exact ElementType.

                        // Materialize the DbDataReader rows into objects of the proper element type per the IQueryable:
                        var results = db.Translate(st.Query.Query.ElementType, dr);
                        foreach (object row in results)
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
