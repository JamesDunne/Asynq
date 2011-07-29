using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;

using System.Reactive.Linq;
using System.Reactive.Subjects;

namespace Asynq
{
    public static class AsyncExecution
    {
        // NOTE: EF currently does not support async IO. Terrible. Back to LINQ-to-SQL.
        // LINQ-to-SQL is a bit better in this area, but not by much. We have a problem
        // in that we cannot select anonymous / complex mapped types via LINQ queries.
        // The Translate() method supports flat models only with unique column name
        // mappings, which might be a blessing in disguise.

        private struct AsyncExecState<Tparameters, Tcontext, Tresult>
            where Tparameters : struct
            where Tcontext : System.Data.Linq.DataContext
            where Tresult : class
        {
            public SqlCommand Command;
            public IQueryable Query;
            public QueryDescriptor<Tparameters, Tcontext, Tresult> QueryDescriptor;

            public AsyncExecState(SqlCommand cmd, IQueryable query, QueryDescriptor<Tparameters, Tcontext, Tresult> queryDescriptor)
            {
                Command = cmd;
                Query = query;
                QueryDescriptor = queryDescriptor;
            }
        }

        // Attempt async execution:
        public static IObservable<Tresult> AsyncExecuteQuery<Tparameters, Tcontext, Tresult>(this Tcontext db, QueryDescriptor<Tparameters, Tcontext, Tresult> descriptor, Tparameters parameters)
            where Tparameters : struct
            where Tcontext : System.Data.Linq.DataContext
            where Tresult : class
        {
            // Instruct the descriptor to build its IQueryable:
            var query = descriptor.BuildQuery(parameters, db);

            AsyncSubject<Tresult> subj = new AsyncSubject<Tresult>();

            // Assume the IQueryable was constructed via LINQ-to-SQL:
            SqlCommand cmd = (SqlCommand)db.GetCommand(query);

            // Create the async completion callback:
            AsyncCallback callback = delegate(IAsyncResult iar)
            {
                var st = (AsyncExecState<Tparameters, Tcontext, Tresult>)iar.AsyncState;

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

                    // Materialize the DbDataReader rows into objects of the proper element type per the IQueryable:
                    var results = db.Translate(st.Query.ElementType, dr);
                    foreach (object r in results)
                    {
                        subj.OnNext(st.QueryDescriptor.RowProjection(r));
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
            cmd.BeginExecuteReader(
                callback
               ,new AsyncExecState<Tparameters, Tcontext, Tresult>(cmd, query, descriptor)
               ,System.Data.CommandBehavior.CloseConnection
            );

            return subj.AsObservable();
        }
    }
}
