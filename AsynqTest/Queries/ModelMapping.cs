using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AsynqTest.Queries
{
    internal static class ModelMapping
    {
        internal static Models.Class mapClass(Data.Class ent, Models.Class mdl = null)
        {
            if (ent == null) return mdl;
            if (mdl == null) mdl = new Models.Class();

            mdl.ID = new Models.ClassID(ent.ClassID);
            mdl.CourseID = new Models.CourseID(ent.CourseID);
            mdl.Code = ent.Code;
            mdl.Section = ent.Section;

            return mdl;
        }

        internal static Models.Course mapCourse(Data.Course ent, Models.Course mdl = null)
        {
            if (ent == null) return mdl;
            if (mdl == null) mdl = new Models.Course();

            mdl.ID = new Models.CourseID(ent.CourseID);
            mdl.Code = ent.Code;
            mdl.Name = ent.Name;

            return mdl;
        }

        internal static Models.ClassEnrollment mapClassEnrollment(Data.ClassEnrollment ent, Models.ClassEnrollment mdl = null)
        {
            if (ent == null) return mdl;
            if (mdl == null) mdl = new Models.ClassEnrollment();

            mdl.ID = new Models.ClassEnrollmentID(ent.ClassEnrollmentID);
            mdl.ProgramEnrollmentID = new Models.ProgramEnrollmentID(ent.ProgramEnrollmentID);
            mdl.ClassID = ent.ClassID.HasValue ? (Models.ClassID?) new Models.ClassID(ent.ClassID.Value) : null;
            mdl.CourseID = new Models.CourseID(ent.CourseID);
            mdl.TermID = ent.TermID.HasValue ? (Models.TermID?) new Models.TermID(ent.TermID.Value) : null;

            return mdl;
        }

        internal static Models.Term mapTerm(Data.Term ent, Models.Term mdl = null)
        {
            if (ent == null) return mdl;
            if (mdl == null) mdl = new Models.Term();

            mdl.ID = new Models.TermID(ent.TermID);
            mdl.Code = ent.Code;
            mdl.Name = ent.Name;
            mdl.StartDate = ent.StartDate;
            mdl.EndDate = ent.EndDate;

            return mdl;
        }

        internal static Models.Staff mapStaff(Data.Staff ent, Models.Staff mdl = null)
        {
            if (ent == null) return mdl;
            if (mdl == null) mdl = new Models.Staff();

            mdl.ID = new Models.StaffID(ent.StaffID);
            mdl.FirstName = ent.FirstName;
            mdl.LastName = ent.LastName;

            return mdl;
        }
    }
}
