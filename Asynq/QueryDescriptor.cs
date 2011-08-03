using System;
using System.Linq;

namespace AsynqFramework
{
    /// <summary>
    /// Contains the metadata necessary for describing a query.
    /// </summary>
    /// <typeparam name="Tresult"></typeparam>
    public sealed class QueryDescriptor<Tcontext, Tparameters, Tresult>
        where Tcontext : System.Data.Linq.DataContext
        where Tparameters : struct
        where Tresult : class
    {
        private Func<Tcontext, Tparameters, IQueryable> buildQuery;
        private Converter<object, Tresult> rowProjection;

        internal QueryDescriptor(
            Func<Tcontext, Tparameters, IQueryable> buildQuery
           ,Converter<object, Tresult> converter
        )
        {
            this.buildQuery = buildQuery;
            this.rowProjection = converter;
        }

        public ConstructedQuery<Tcontext, Tparameters, Tresult> Construct(Tcontext context, Tparameters parameters)
        {
            return new ConstructedQuery<Tcontext, Tparameters, Tresult>(this, context, buildQuery(context, parameters), parameters, rowProjection);
        }
    }

    public sealed class ConstructedQuery<Tcontext, Tparameters, Tresult>
        where Tcontext : System.Data.Linq.DataContext
        where Tparameters : struct
        where Tresult : class
    {
        public QueryDescriptor<Tcontext, Tparameters, Tresult> Descriptor { get; private set; }
        public Tcontext Context { get; private set; }
        public IQueryable Query { get; private set; }
        public Tparameters Parameters { get; private set; }
        public Converter<object, Tresult> RowProjection { get; private set; }

        internal ConstructedQuery(QueryDescriptor<Tcontext, Tparameters, Tresult> descriptor, Tcontext context, IQueryable query, Tparameters parameters, Converter<object, Tresult> rowProjection)
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
        public static QueryDescriptor<Tcontext, Tparameters, Tresult>
            Describe<Ttmp, Tcontext, Tparameters, Tresult>(
                Func<Tcontext, Tparameters, IQueryable<Ttmp>> buildQuery
               ,Converter<Ttmp, Tresult> converter
            )
            where Tcontext : System.Data.Linq.DataContext
            where Tparameters : struct
            where Tresult : class
        {
            return new QueryDescriptor<Tcontext, Tparameters, Tresult>(buildQuery, row => converter((Ttmp)row));
        }

        public static QueryDescriptor<Tcontext, Tparameters, Tresult>
            Describe<Tcontext, Tparameters, Tresult>(
                Func<Tcontext, Tparameters, IQueryable<Tresult>> buildQuery
            )
            where Tcontext : System.Data.Linq.DataContext
            where Tparameters : struct
            where Tresult : class
        {
            // FIXME: unnecessary dangerous-looking but safe type cast in order to satisfy T -> object -> T.
            return new QueryDescriptor<Tcontext, Tparameters, Tresult>(buildQuery, row => (Tresult)row);
        }
    }
}
