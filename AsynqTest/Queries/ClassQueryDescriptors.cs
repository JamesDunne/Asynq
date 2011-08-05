using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using AsynqFramework;
using AsynqTest.ParameterContainers;

namespace AsynqTest.Queries
{
    public sealed partial class ClassQueryDescriptors
    {
        public static readonly ClassQueryDescriptors Default = new ClassQueryDescriptors();

        /// <summary>
        /// Describes a query to get a Class (and its related Course) by its ClassID.
        /// </summary>
        public QueryDescriptor<Data.ExampleDataContext, OneIDParameter<Models.ClassID>, Tuple<Models.Class, Models.Course>>
            GetClassByID = Query.Describe(
                (Data.ExampleDataContext db, OneIDParameter<Models.ClassID> p) =>

                    from cl in db.Class
                    join cr in db.Course on cl.CourseID equals cr.CourseID
                    join crBad in db.Course on cl.ClassID equals crBad.CourseID into crBadLJ
                    from crBad in crBadLJ.DefaultIfEmpty()
                    where p.ID.Value == cl.ClassID
                    select new { cl, cr, courseCount = db.Course.Count(), crBad /*, d2 = crBad *//*, c = true, d = 1, e = 2, f = cr.ID*/ }

               ,row => new Tuple<Models.Class, Models.Course>(ModelMapping.mapClass(row.cl), ModelMapping.mapCourse(row.cr))
            );
    }
}
