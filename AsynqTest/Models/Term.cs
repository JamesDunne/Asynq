using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AsynqTest.Models
{
    public struct TermID : IModelIdentifier
    {
        private int _Value;
        public int Value { get { return _Value; } }
        public TermID(int value) { _Value = value; }
    }

    public sealed class Term
    {
        public TermID ID { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
    }
}
