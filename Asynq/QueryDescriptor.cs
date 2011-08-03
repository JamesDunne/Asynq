using System;
using System.Linq;

namespace AsynqFramework
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
        private Func<Tparameters, Tcontext, IQueryable> buildQuery;
        private Converter<object, Tresult> rowProjection;

        internal QueryDescriptor(
            Func<Tparameters, Tcontext, IQueryable> buildQuery
           ,Converter<object, Tresult> converter
        )
        {
            this.buildQuery = buildQuery;
            this.rowProjection = converter;
        }

        public ConstructedQuery<Tparameters, Tcontext, Tresult> Construct(Tcontext context, Tparameters parameters)
        {
            return new ConstructedQuery<Tparameters, Tcontext, Tresult>(this, context, buildQuery(parameters, context), parameters, rowProjection);
        }
    }

    public sealed class ConstructedQuery<Tparameters, Tcontext, Tresult>
        where Tparameters : struct
        where Tcontext : System.Data.Linq.DataContext
        where Tresult : class
    {
        public QueryDescriptor<Tparameters, Tcontext, Tresult> Descriptor { get; private set; }
        public Tcontext Context { get; private set; }
        public IQueryable Query { get; private set; }
        public Tparameters Parameters { get; private set; }
        public Converter<object, Tresult> RowProjection { get; private set; }

        internal ConstructedQuery(QueryDescriptor<Tparameters, Tcontext, Tresult> descriptor, Tcontext context, IQueryable query, Tparameters parameters, Converter<object, Tresult> rowProjection)
        {
            this.Descriptor = descriptor;
            this.Context = context;
            this.Query = query;
            this.Parameters = parameters;
            this.RowProjection = rowProjection;
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
