using System;
using System.Data.Linq;
using System.Linq;

using Asynq.ParameterContainers;

namespace Asynq.Queries
{
    #region Dummy supporting types for sample code

    public struct SampleID : IModelIdentifier { public int Value { get; set; } }
    public sealed class Sample { public SampleID ID { get; set; } }
    
    #endregion

    public sealed partial class SampleQueryDescriptors
    {
        public static readonly SampleQueryDescriptors Default = new SampleQueryDescriptors();

        // NOTE: Ideally separate out queries into one class per each Tresult type.

        // NOTE: Obviously we would never use ObjectContext itself, but rather a code-generated derived class of it
        // that describes our entity model.

        public QueryDescriptor<NoParameters, DataContext, string>
            GetIntsFrom0to99 = Query.Describe(
                // Describe the query as a function based on the input parameters and an ObjectContext-deriving class:
                (NoParameters p, DataContext db) =>

                    from i in Enumerable.Range(0, 100).AsQueryable()
                    select new { i }

                // Define a function to convert each row object yielded by the query:
                // Here would be an excellent place to convert a temporary anonymous type into a concrete datacontract,
                // complete with mapping logic that is guaranteed to run locally and not be hoisted up into the IQueryable.
               ,row => row.i.ToString()
            );

        public QueryDescriptor<SingleIdentifierParameters<SampleID>, DataContext, Sample>
            GetSampleByID = Query.Describe(
                (SingleIdentifierParameters<SampleID> p, DataContext db) =>

                    from i in Enumerable.Range(0, 100).AsQueryable()
                    where i == p.ID.Value
                    select new Sample { ID = p.ID }

                // NOTE that the second parameter here is omitted for identity conversions, i.e. the raw IQueryable returns
                // the type requested without any required conversions or mappings.
            );
    }
}
