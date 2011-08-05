using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using AsynqFramework;
using AsynqTest.ParameterContainers;

namespace AsynqTest.Queries
{
    public sealed partial class CourseQueryDescriptors
    {
        public static readonly CourseQueryDescriptors Default = new CourseQueryDescriptors();

        /// <summary>
        /// Describes a query to get a Course by its CourseID.
        /// </summary>
        public QueryDescriptor<Data.ExampleDataContext, OneIDParameter<Models.CourseID>, Models.Course>
            GetCourseByID = Query.Describe(
                (Data.ExampleDataContext db, OneIDParameter<Models.CourseID> p) =>

                    from cr in db.Course
                    where p.ID.Value == cr.CourseID
                    select cr

               ,row => ModelMapping.mapCourse(row)
            );
    }
}
