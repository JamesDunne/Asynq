using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AsynqTest.Models
{
    public struct CourseID : IModelIdentifier {
        private int _Value;
        public int Value { get { return _Value; } }
        public CourseID(int value) { _Value = value; }
    }

    public sealed class Course
    {
        public CourseID ID { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
    }
}
