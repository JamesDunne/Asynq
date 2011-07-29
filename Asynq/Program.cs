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
                var sampleQuery = sampleDescriptor.Construct(null, NoParameters.Default);

                // Lets display that IQueryable's Expression:
                Console.WriteLine(sampleQuery.Query.Expression.ToString());

                // Enumerate the query, projecting each row to its final type:
                foreach (object row in sampleQuery.Query)
                {
                    var projected = sampleQuery.RowProjection(row);

                    Console.WriteLine("  {0}", projected);
                }
            }

            // Example 2:
            {
                var sampleDescriptor = SampleQueryDescriptors.Default.GetSampleByID;
                // Instruct the descriptor to build its IQueryable:
                var sampleQuery = sampleDescriptor.Construct(null, new OneIDParameter<SampleID>(new SampleID { Value = 15 }));

                // Lets display that IQueryable's Expression:
                Console.WriteLine(sampleQuery.Query.Expression.ToString());

                // Enumerate the query, projecting each row to its final type:
                foreach (object row in sampleQuery.Query)
                {
                    var projected = sampleQuery.RowProjection(row);

                    Console.WriteLine("  {0}", projected.ID.Value);
                }
            }

            {
                using (var db = new Tmp(@"tmp.sdf"))
                {
                    var obsQuery = db.AsyncExecuteQuery(
                        SampleQueryDescriptors.Default.GetClassByID
                       ,new OneIDParameter<SampleID>(new SampleID { Value = 1 })
                    );

                    obsQuery.First();
                }
            }
        }
    }
}
