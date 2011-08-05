using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Linq.Expressions;
using System.Data;

namespace AsynqFramework.Materialization
{
    public interface IObjectMaterializer
    {
        IObjectMaterializationMapping BuildMaterializationMapping(Expression query, Type destinationType, IDataRecord dataSource);

        object Materialize(IObjectMaterializationMapping mapping, IDataRecord dataSource);
    }
}
