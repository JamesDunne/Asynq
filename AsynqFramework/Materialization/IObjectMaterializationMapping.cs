using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;

namespace AsynqFramework.Materialization
{
    public interface IObjectMaterializationMapping
    {
        IDataRecord DataRecord { get; }
    }
}
