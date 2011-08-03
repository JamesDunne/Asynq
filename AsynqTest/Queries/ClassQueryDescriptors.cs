using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using AsynqFramework;
using AsynqTest.ParameterContainers;

namespace AsynqTest.Queries
{
    public struct ClassID : IModelIdentifier { public int Value { get; set; } }

    public sealed partial class ClassQueryDescriptors
    {
        public static readonly ClassQueryDescriptors Default = new ClassQueryDescriptors();

        /// <summary>
        /// Describes a query to get a Class (and its related Course) by its ClassID.
        /// </summary>
        public QueryDescriptor<ExampleDataContext, OneIDParameter<ClassID>, Tuple<Class, Course>>
            GetClassByID = Query.Describe(
                (ExampleDataContext db, OneIDParameter<ClassID> p) =>

                    from cl in db.Class
                    join cr in db.Course on cl.CourseID equals cr.ID
                    where p.ID.Value == cl.ID
                    select new { cl, cr /*, c = true, d = 1, e = 2, f = cr.ID*/ }

               ,row => new Tuple<Class, Course>(row.cl, row.cr)
            );
    }
}
