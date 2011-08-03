using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Asynq.Materialization
{
    public interface IObjectMaterializationMapping<TdataSource>
    {
        TdataSource DataSource { get; }
    }
}
