using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AsynqFramework.ParameterContainers
{
    /// <summary>
    /// A parameter container which contains a single identifier type.
    /// </summary>
    /// <typeparam name="T1"></typeparam>
    public struct OneIDParameter<T1>
        where T1 : IModelIdentifier
    {
        public readonly T1 ID;

        public OneIDParameter(T1 id)
        {
            ID = id;
        }
    }
}
