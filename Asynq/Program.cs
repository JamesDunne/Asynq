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
            Console.WriteLine("Press a key to begin.");
            Console.ReadKey();
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
                //const string connString = @"tmp.sdf";
                const string connString = @"Data Source=.\SQLEXPRESS;Initial Catalog=Asynq;Integrated Security=SSPI;Asynchronous Processing=true";

                Console.WriteLine("Opening '{0}' and querying...", connString);

                Func<Tmp> createContext = () => new Tmp(connString);
                
                {
                    var obsQuery = createContext.AsyncExecuteQuery(
                        SampleQueryDescriptors.Default.GetClassByID
                       ,new OneIDParameter<SampleID>(new SampleID { Value = 1 })
                    );

                    var obsQuery2 = createContext.AsyncExecuteQuery(
                        SampleQueryDescriptors.Default.GetClassByID
                       ,new OneIDParameter<SampleID>(new SampleID { Value = 2 })
                    );

                    var obsQuery3 = createContext.AsyncExecuteQuery(
                        SampleQueryDescriptors.Default.GetClassByID
                       ,new OneIDParameter<SampleID>(new SampleID { Value = 3 })
                    );

                    Console.WriteLine("Awaiting on Thread ID #{0}...", Thread.CurrentThread.ManagedThreadId);
                    obsQuery.ForEachAsync(rows =>
                        {
                            foreach (var row in rows)
                            {
                                Console.WriteLine(
                                    "{0}\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}"
                                    , row.Item1.ID, row.Item1.Code, row.Item1.Section, row.Item1.CourseID
                                    , row.Item2.ID, row.Item2.Code, row.Item2.Name
                                );
                            }
                        }
                    ).Wait();

                    Console.WriteLine("Completed");
                }
            }

            Console.WriteLine("Press a key to end.");
            Console.ReadKey();
        }
    }
}
