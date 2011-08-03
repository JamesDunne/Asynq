using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using AsynqFramework.ParameterContainers;

namespace AsynqFramework.Queries
{
    public struct CourseID : IModelIdentifier { public int Value { get; set; } }

    public sealed partial class CourseQueryDescriptors
    {
        public static readonly CourseQueryDescriptors Default = new CourseQueryDescriptors();

        /// <summary>
        /// Describes a query to get a Course by its CourseID.
        /// </summary>
        public QueryDescriptor<ExampleDataContext, OneIDParameter<CourseID>, Course>
            GetCourseByID = Query.Describe(
                (ExampleDataContext db, OneIDParameter<CourseID> p) =>

                    from cr in db.Course
                    where p.ID.Value == cr.ID
                    select cr
            );
    }
}
