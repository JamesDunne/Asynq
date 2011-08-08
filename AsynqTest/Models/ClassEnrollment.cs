using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AsynqTest.Models
{
    public struct ClassEnrollmentID : IModelIdentifier
    {
        private int _Value;
        public int Value { get { return _Value; } }
        public ClassEnrollmentID(int value) { _Value = value; }
    }

    public sealed class ClassEnrollment
    {
        public ClassEnrollmentID ID { get; set; }
        public ClassID? ClassID { get; set; }
        public ProgramEnrollmentID ProgramEnrollmentID { get; set; }
        public CourseID CourseID { get; set; }
        public TermID? TermID { get; set; }
    }
}
