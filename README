Asynq - Asynchronous LINQ-to-SQL query execution framework

This is very much a WORK IN PROGRESS. Do not use in ANY production code WHATSOEVER until this notice is removed.

Requires System.Reactive v1.1 (experimental) from http://msdn.microsoft.com/en-us/data/gg577609

I started this project to do asynchronous LINQ-to-SQL query execution.

I started this simple project to do asynchronous LINQ-to-SQL query execution. The idea is quite simple albeit "brittle" at this stage (as of 8/16/2011):

1. Let LINQ-to-SQL do the "heavy" work of translating your `IQueryable` into a `DbCommand` via the `DataContext.GetCommand()`.
2. For SQL 200[058], cast up from the abstract `DbCommand` instance you got from `GetCommand()` to get a `SqlCommand`. If you're using SQL CE you're out of luck since `SqlCeCommand` does not expose the async pattern for `BeginExecuteReader` and `EndExecuteReader`.
3. Use `BeginExecuteReader` and `EndExecuteReader` off the `SqlCommand` using the standard .NET framework asynchronous I/O pattern to get yourself a `DbDataReader` in the completion callback delegate that you pass to the `BeginExecuteReader` method.
4. Now we have a `DbDataReader` which we have no idea what columns it contains nor how to map those values back up to the `IQueryable`'s `ElementType` (most likely to be an anonymous type in the case of joins). Sure, at this point you could hand-write your own column mapper that materializes its results back into your anonymous type or whatever. You'd have to write a new one per each query result type, depending on how LINQ-to-SQL treats your IQueryable and what SQL code it generates. This is a pretty nasty option and I don't recommend it since it's not maintainable nor would it be always correct. LINQ-to-SQL can change your query form depending on the parameter values you pass in, for example `query.Take(10).Skip(0)` produces different SQL than `query.Take(10).Skip(10)`, and perhaps a different resultset schema. Your best bet is to handle this materialization problem programmatically:
5. "Re-implement" a simplistic runtime object materializer that pulls columns off the `DbDataReader` in a defined order according to the LINQ-to-SQL mapping attributes of the `ElementType` Type for the `IQueryable`. Implementing this correctly is probably the most challenging part of this solution.

As others have discovered, the `DataContext.Translate()` method does not handle anonymous types and can only map a `DbDataReader` directly to a properly attributed LINQ-to-SQL proxy object. Since most queries worth writing in LINQ are going to involve complex joins which inevitably end up requiring anonymous types for the final select clause, it's pretty pointless to use this provided watered-down `DataContext.Translate()` method anyway.

There are a few minor drawbacks to this solution when leveraging the existing mature LINQ-to-SQL IQueryable provider:

1. You cannot map a single object instance to multiple anonymous type properties in the final select clause of your `IQueryable`, e.g. `from x in db.Table1 select new { a = x, b = x }`. LINQ-to-SQL internally keeps track of which column ordinals map to which properties; it does not expose this information to the end user so you have no idea which columns in the `DbDataReader` are reused and which are "distinct".
2. You cannot include constant values in your final select clause - these do not get translated into SQL and will be absent from the `DbDataReader` so you'd have to build custom logic to pull these constant values up from the `IQueryable`'s `Expression` tree, which would be quite a hassle and is simply not justifiable.

I'm sure there are other query patterns that might break but these are the two biggest I could think of that could cause problems in an existing LINQ-to-SQL data access layer.

These problems are easy to defeat - simply don't do them in your queries since neither pattern provides any benefit to the end result of the query. Hopefully this advice applies to all query patterns that would potentially cause object materialization problems :-P. It's a hard problem to solve not having access to LINQ-to-SQL's column mapping information.

A more "complete" approach to solving the problem would be to effectively re-implement nearly all of LINQ-to-SQL, which is a bit more time-consuming :-P. Starting from a quality, open-source LINQ-to-SQL provider implementation would be a good way to go here. The reason you'd need to reimplement it is so that you'd have access to all of the column mapping information used to materialize the `DbDataReader` results back up to an object instance without any loss of information.
