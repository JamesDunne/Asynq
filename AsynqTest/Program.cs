using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Threading;
using AsynqFramework;
using AsynqTest.ParameterContainers;
using AsynqTest.Queries;
using System.Diagnostics;

namespace AsynqTest
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
                Console.WriteLine("Using '{0}'...", connString);

                var descriptors = ClassQueryDescriptors.Default;

                var queries = new IObservable<List<Tuple<Class, Course>>>[2500];

                Stopwatch swTimer = Stopwatch.StartNew();
                for (int i = 0; i < queries.Length; ++i)
                {
                    queries[i] = Asynq.ExecuteQuery(
                        // Pass a lambda used to instantiate a new data context per each query:
                        createContext:  () => new ExampleDataContext(connString)
                        // Give the query descriptor:
                       ,descriptor:     descriptors.GetClassByID
                        // Give the parameter container struct:
                       ,parameters:     new OneIDParameter<ClassID>(new ClassID { Value = (i % 11) + 1 })
                        // Optional argument used to give an initial expected capacity of the result's List<T>:
                       ,expectedCount:  1
                    );
                }

                Console.WriteLine("Awaiting on Thread ID #{0}...", Thread.CurrentThread.ManagedThreadId);

                // Loop through the queries and pull back each one's results:
                for (int i = 0; i < queries.Length; ++i)
                {
                    // First() is blocking here, but the query should most likely already be complete:
                    List<Tuple<Class, Course>> rows = queries[i].First();

#if TEST
                    Console.WriteLine("#{0,3}) {1} items.", i + 1, rows.Count);
                    foreach (var row in rows)
                    {
                        Console.WriteLine(
                            "      {1}|{2}|{3}|{4}|{5}|{6}|{7}"
                           ,(i + 1)
                           ,row.Item1.ID, row.Item1.Code, row.Item1.Section, row.Item1.CourseID
                           ,row.Item2.ID, row.Item2.Code, row.Item2.Name
                        );
                    }
#endif
                }

                swTimer.Stop();

                Console.WriteLine("Completed {0} queries in {1} ms, average {2} ms/query, {3} queries/sec"
                    ,queries.Length
                    ,swTimer.ElapsedMilliseconds
                    ,swTimer.ElapsedMilliseconds / (double)queries.Length
                    ,(queries.Length / (double)swTimer.ElapsedMilliseconds) * 1000d);
            }

            Console.WriteLine("Press a key to end.");
            Console.ReadKey();
        }
    }
}
