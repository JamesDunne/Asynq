using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Linq;
using System.Linq;
using System.Text;

namespace Asynq.Queries
{
    /// <summary>
    /// Contains the metadata necessary for describing a query.
    /// </summary>
    /// <typeparam name="Tresult"></typeparam>
    public sealed class QueryDescriptor<Tresult>
    {
        public Func<IQueryable> BuildQuery { get; private set; }
        public Converter<object, Tresult> RowProjection { get; private set; }

        internal QueryDescriptor(Func<IQueryable> buildQuery, Converter<object, Tresult> converter)
        {
            this.BuildQuery = buildQuery;
            this.RowProjection = converter;
        }
    }

    public static class Query
    {
        // Dirty hack to support anonymous types for `T`.
        public static QueryDescriptor<Tresult> Describe<Ttmp, Tresult>(Func<IQueryable<Ttmp>> buildQuery, Converter<Ttmp, Tresult> converter)
        {
            return new QueryDescriptor<Tresult>(buildQuery, row => converter((Ttmp)row));
        }
    }

    public sealed partial class Samples
    {
        public QueryDescriptor<string> getSample = Query.Describe(
            // Describe the query:
            () =>   from i in Enumerable.Range(0, 100).AsQueryable()
                    select new { i }
            // Define the row-converter function applied to each row object after the query is executed:
           ,row => row.i.ToString()
        );
    }
}
