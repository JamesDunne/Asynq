using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AsynqTest.Models
{
    public struct ProgramEnrollmentID : IModelIdentifier
    {
        private int _Value;
        public int Value { get { return _Value; } }
        public ProgramEnrollmentID(int value) { _Value = value; }
    }

    public sealed class ProgramEnrollment
    {
    }
}
