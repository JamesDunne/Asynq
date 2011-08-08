using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AsynqTest.Models
{
    public struct ClassID : IModelIdentifier
    {
        private int _Value;
        public int Value { get { return _Value; } }
        public ClassID(int value) { _Value = value; }
    }

    public sealed class Class
    {
        public ClassID ID { get; set; }
        public CourseID CourseID { get; set; }
        public string Code { get; set; }
        public string Section { get; set; }
    }
}
