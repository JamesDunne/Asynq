using System;
using System.Linq;

namespace Asynq
{
    /// <summary>
    /// Contains the metadata necessary for describing a query.
    /// </summary>
    /// <typeparam name="Tresult"></typeparam>
    public sealed class QueryDescriptor<Tparameters, Tcontext, Tresult>
        where Tparameters : struct
        where Tcontext : System.Data.Linq.DataContext
        where Tresult : class
    {
        public Func<Tparameters, Tcontext, IQueryable> BuildQuery { get; private set; }
        public Converter<object, Tresult> RowProjection { get; private set; }

        internal QueryDescriptor(
            Func<Tparameters, Tcontext, IQueryable> buildQuery
           ,Converter<object, Tresult> converter
        )
        {
            this.BuildQuery = buildQuery;
            this.RowProjection = converter;
        }
    }

    public static class Query
    {
        // Dirty hack to support anonymous types for `Ttmp`.
        public static QueryDescriptor<Tparameters, Tcontext, Tresult>
            Describe<Ttmp, Tparameters, Tcontext, Tresult>(
                Func<Tparameters, Tcontext, IQueryable<Ttmp>> buildQuery
               ,Converter<Ttmp, Tresult> converter
            )
            where Tparameters : struct
            where Tcontext : System.Data.Linq.DataContext
            where Tresult : class
        {
            return new QueryDescriptor<Tparameters, Tcontext, Tresult>(buildQuery, row => converter((Ttmp)row));
        }

        public static QueryDescriptor<Tparameters, Tcontext, Tresult>
            Describe<Tparameters, Tcontext, Tresult>(
                Func<Tparameters, Tcontext, IQueryable<Tresult>> buildQuery
            )
            where Tparameters : struct
            where Tcontext : System.Data.Linq.DataContext
            where Tresult : class
        {
            // FIXME: unnecessary dangerous-looking but safe type cast in order to satisfy T -> object -> T.
            return new QueryDescriptor<Tparameters, Tcontext, Tresult>(buildQuery, row => (Tresult)row);
        }
    }
}
