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
using System.Diagnostics;
using System.Threading;

namespace Asynq
{
    class Program
    {
        static void Main(string[] args)
        {
#if false
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
#endif

            {
                Console.WriteLine("Opening tmp.sdf and querying...");
                using (var db = new Tmp(@"tmp.sdf"))
                {
                    var obsQuery = db.AsyncExecuteQuery(
                        SampleQueryDescriptors.Default.GetClassByID
                       ,new OneIDParameter<SampleID>(new SampleID { Value = 1 })
                    );

                    Console.WriteLine("Awaiting...");
                    obsQuery.ForEachAsync(cl =>
                        {
                            Console.WriteLine(
                                "{0}\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}"
                                , cl.Item1.ID, cl.Item1.Code, cl.Item1.Section, cl.Item1.CourseID
                                , cl.Item2.ID, cl.Item2.Code, cl.Item2.Name
                            );
                        }
                    ).Wait();

                    Console.WriteLine("Completed");
                }
            }
        }
    }
}
