using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AsynqTest.Models
{
    public struct StaffID : IModelIdentifier
    {
        private int _Value;
        public int Value { get { return _Value; } }
        public StaffID(int value) { _Value = value; }
    }

    public sealed class Staff
    {
        public StaffID ID { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
    }
}
