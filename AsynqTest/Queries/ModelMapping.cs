using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AsynqTest.Queries
{
    internal static class ModelMapping
    {
        internal static Models.Class mapClass(Data.Class mdl, Models.Class onto = null)
        {
            if (mdl == null) return onto;
            if (onto == null) onto = new Models.Class();

            onto.ID = new Models.ClassID(mdl.ClassID);
            onto.CourseID = new Models.CourseID(mdl.CourseID);
            onto.Code = mdl.Code;
            onto.Section = mdl.Section;

            return onto;
        }

        internal static Models.Course mapCourse(Data.Course mdl, Models.Course onto = null)
        {
            if (mdl == null) return onto;
            if (onto == null) onto = new Models.Course();

            onto.ID = new Models.CourseID(mdl.CourseID);
            onto.Code = mdl.Code;
            onto.Name = mdl.Name;

            return onto;
        }
    }
}
