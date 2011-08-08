using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using AsynqFramework;
using AsynqTest.ParameterContainers;

namespace AsynqTest.Queries
{
    public sealed partial class ClassEnrollmentQueryDescriptors
    {
        public static readonly ClassEnrollmentQueryDescriptors Default = new ClassEnrollmentQueryDescriptors();

        public QueryDescriptor<Data.ExampleDataContext, OneIDParameter<Models.ProgramEnrollmentID>, Tuple<Models.ClassEnrollment, Models.Course, Models.Class, Models.Term, Models.Course, Models.Staff>>
            GetClassEnrollmentDetailsByProgramEnrollmentID = Query.Describe(
                (Data.ExampleDataContext db, OneIDParameter<Models.ProgramEnrollmentID> p) =>

                    from ce in db.ClassEnrollment
                    join rcr in db.Course on ce.CourseID equals rcr.CourseID into joinedRCRs
                    from rcr in joinedRCRs.DefaultIfEmpty()
                    join cl in db.Class on ce.ClassID equals cl.ClassID into joinedCLs
                    from cl in joinedCLs.DefaultIfEmpty()
                    join tm in db.Term on ce.TermID equals tm.TermID into joinedTMs
                    from tm in joinedTMs.DefaultIfEmpty()
                    join cr in db.Course on cl.CourseID equals cr.CourseID into joinedCRs
                    from cr in joinedCRs.DefaultIfEmpty()
                    join ins in db.Staff on cl.InstructorStaffID equals ins.StaffID into joinedINSs
                    from ins in joinedINSs.DefaultIfEmpty()
                    where ce.ProgramEnrollmentID == p.ID.Value
                    select new { ce, rcr, cl, tm, cr, ins }

               , j => new Tuple<Models.ClassEnrollment, Models.Course, Models.Class, Models.Term, Models.Course, Models.Staff>(
                    ModelMapping.mapClassEnrollment(j.ce),
                    ModelMapping.mapCourse(j.rcr),
                    ModelMapping.mapClass(j.cl),
                    ModelMapping.mapTerm(j.tm),
                    ModelMapping.mapCourse(j.cr),
                    ModelMapping.mapStaff(j.ins)
                )
            );
    }
}
