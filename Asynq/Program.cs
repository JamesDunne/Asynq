using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Data.Linq;
using System.Data.SqlClient;

using Asynq.Queries;
using Asynq.ParameterContainers;

namespace Asynq
{
    class Program
    {
        static void Main(string[] args)
        {
            // Example 1:
            {
                // Get a sample QueryDescriptor to play with:
                var sampleDescriptor = SampleQueryDescriptors.Default.GetIntsFrom0to99;
                // Instruct the descriptor to build its IQueryable:
                var sampleQuery = sampleDescriptor.BuildQuery(NoParameters.Default, null);

                // Lets display that IQueryable's Expression:
                Console.WriteLine(sampleQuery.Expression.ToString());

                // Enumerate the query, projecting each row to its final type:
                foreach (object row in sampleQuery)
                {
                    var projected = sampleDescriptor.RowProjection(row);

                    Console.WriteLine("  {0}", projected);
                }
            }

            // Example 2:
            {
                var sampleDescriptor = SampleQueryDescriptors.Default.GetSampleByID;
                // Instruct the descriptor to build its IQueryable:
                var sampleQuery = sampleDescriptor.BuildQuery(new SingleIdentifierParameters<SampleID>(new SampleID { Value = 15 }), null);

                // Lets display that IQueryable's Expression:
                Console.WriteLine(sampleQuery.Expression.ToString());

                // Enumerate the query, projecting each row to its final type:
                foreach (object row in sampleQuery)
                {
                    var projected = sampleDescriptor.RowProjection(row);

                    Console.WriteLine("  {0}", projected.ID.Value);
                }
            }
        }

        // NOTE: EF currently does not support async IO. Terrible. Back to LINQ-to-SQL.
        // LINQ-to-SQL is a hait better in this area, but not by much. We have a problem
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
        private IObservable<Sample> AsyncGetSampleByID(SingleIdentifierParameters<SampleID> parameters)
        {
            var db = new DataContext("");

            var sampleDescriptor = SampleQueryDescriptors.Default.GetSampleByID;

            // Instruct the descriptor to build its IQueryable:
            var sampleQuery = sampleDescriptor.BuildQuery(new SingleIdentifierParameters<SampleID>(new SampleID { Value = 15 }), null);

            AsyncSubject<Sample> subj = new AsyncSubject<Sample>();

            // Assume the IQueryable was constructed via LINQ-to-SQL:
            SqlCommand cmd = (SqlCommand)db.GetCommand(sampleQuery);

            // Create the async completion callback:
            AsyncCallback callback = delegate(IAsyncResult iar)
            {
                var st = (AsyncExecState<SingleIdentifierParameters<SampleID>, DataContext, Sample>)iar.AsyncState;

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
                    var results = db.Translate(sampleQuery.ElementType, dr);
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
            cmd.BeginExecuteReader(callback, new AsyncExecState<SingleIdentifierParameters<SampleID>, DataContext, Sample>(cmd, sampleQuery, sampleDescriptor), System.Data.CommandBehavior.CloseConnection);

            return subj.AsObservable();
        }
    }
}
