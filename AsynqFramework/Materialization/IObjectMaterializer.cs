using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AsynqFramework.Materialization
{
    public interface IObjectMaterializer<TdataSource>
    {
        IObjectMaterializationMapping<TdataSource> BuildMaterializationMapping(Type destinationType, TdataSource dataSource);

        object Materialize(IObjectMaterializationMapping<TdataSource> mapping);
    }
}
