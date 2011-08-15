#define NonAsyncCompare
using System;
using System.Collections.Generic;
using System.Linq;
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
            const int queryCount = 250;

            //const string connString = @"tmp.sdf";
            const string connString = @"Data Source=.\SQLEXPRESS;Initial Catalog=Asynq;Integrated Security=SSPI;Asynchronous Processing=true";
            Console.WriteLine("Using '{0}'...", connString);

            var query = ClassEnrollmentQueryDescriptors.Default.GetClassEnrollmentDetailsByProgramEnrollmentID;

            var resultsSync = new List<Tuple<Models.ClassEnrollment, Models.Course, Models.Class, Models.Term, Models.Course, Models.Staff>>[queryCount];
            var resultsAsynq = new List<Tuple<Models.ClassEnrollment, Models.Course, Models.Class, Models.Term, Models.Course, Models.Staff>>[queryCount];

            {
                var queries = new IObservable<List<Tuple<Models.ClassEnrollment, Models.Course, Models.Class, Models.Term, Models.Course, Models.Staff>>>[queryCount];

                Console.WriteLine("Beginning asynchronous Asynq LINQ-to-SQL querying...");

                Stopwatch swTimer = Stopwatch.StartNew();
                for (int i = 0; i < queryCount; ++i)
                {
                    queries[i] = Asynq.ExecuteQuery(
                        // Pass a lambda used to instantiate a new data context per each query:
                        createContext:  () => new Data.ExampleDataContext(connString)
                        // Give the query descriptor:
                       ,descriptor:     query
                        // Give the parameter container struct:
                       ,parameters:     new OneIDParameter<Models.ProgramEnrollmentID>(new Models.ProgramEnrollmentID((i % 11) + 1))
                        // Optional argument used to give an initial expected capacity of the result's List<T>:
                       ,expectedCount:  1
                    );
                }

                Console.WriteLine("Awaiting on Thread ID #{0}...", Thread.CurrentThread.ManagedThreadId);

                // Loop through the queries and pull back each one's results:
                for (int i = 0; i < queryCount; ++i)
                {
                    // First() is blocking here, but the query should most likely already be complete:
                    var rows = queries[i].First();
                    resultsAsynq[i] = rows;

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

                Console.WriteLine("Asynchronous Asynq completed {0} queries in {1} ms, average {2} ms/query, {3} queries/sec"
                    , queryCount
                    , swTimer.ElapsedMilliseconds
                    , swTimer.ElapsedMilliseconds / (double)queryCount
                    , (queryCount / (double)swTimer.ElapsedMilliseconds) * 1000d);
            }

            // Synchronous Asynq testing:
            {
                Console.WriteLine("Beginning synchronous Asynq LINQ-to-SQL querying...");

                Stopwatch swTimer = Stopwatch.StartNew();
                for (int i = 0; i < queryCount; ++i)
                {
                    Asynq.ExecuteQuerySync(
                        // Pass a lambda used to instantiate a new data context per each query:
                        createContext:  () => new Data.ExampleDataContext(connString)
                        // Give the query descriptor:
                       ,descriptor:     query
                        // Give the parameter container struct:
                       ,parameters:     new OneIDParameter<Models.ProgramEnrollmentID>(new Models.ProgramEnrollmentID((i % 11) + 1))
                        // Optional argument used to give an initial expected capacity of the result's List<T>:
                       ,expectedCount:  1
                    );
                }
                swTimer.Stop();

                Console.WriteLine("Synchronous Asynq completed {0} queries in {1} ms, average {2} ms/query, {3} queries/sec"
                    , queryCount
                    , swTimer.ElapsedMilliseconds
                    , swTimer.ElapsedMilliseconds / (double)queryCount
                    , (queryCount / (double)swTimer.ElapsedMilliseconds) * 1000d);
            }

#if NonAsyncCompare

            using (var db = new Data.ExampleDataContext(connString))
            {
                Console.WriteLine("Beginning synchronous standard LINQ-to-SQL querying...");

                Stopwatch swTimer = Stopwatch.StartNew();
                for (int i = 0; i < queryCount; ++i)
                {
                    var constructed = query.Construct(db, new OneIDParameter<Models.ProgramEnrollmentID>(new Models.ProgramEnrollmentID((i % 11) + 1)));
                    var rows = constructed.Query.Cast<object>().Select(constructed.RowProjection).ToList();
                    resultsSync[i] = rows;
                }

                swTimer.Stop();

                Console.WriteLine("Sync LINQ-to-SQL completed {0} queries in {1} ms, average {2} ms/query, {3} queries/sec"
                    , queryCount
                    , swTimer.ElapsedMilliseconds
                    , swTimer.ElapsedMilliseconds / (double)queryCount
                    , (queryCount / (double)swTimer.ElapsedMilliseconds) * 1000d
                );
            }
#endif

            // Compare results:
            for (int i = 0; i < queryCount; ++i)
            {
                if (resultsSync[i].Count != resultsAsynq[i].Count) throw new InvalidProgramException();
                
                foreach (var x in resultsSync[i].Zip(resultsAsynq[i], (a, b) => new { a, b }))
                {
                    if (x.a.Item1.ID.Value != x.b.Item1.ID.Value) throw new InvalidProgramException();
                    if (x.a.Item2.ID.Value != x.b.Item2.ID.Value) throw new InvalidProgramException();
                }
            }

            Console.WriteLine("Press a key to end.");
            Console.ReadKey();
        }
    }
}
