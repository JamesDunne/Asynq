using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Asynq.ParameterContainers
{
    /// <summary>
    /// A parameter container which contains a single identifier type.
    /// </summary>
    /// <typeparam name="Tid"></typeparam>
    public struct SingleIdentifierParameters<Tid>
        where Tid : IModelIdentifier
    {
        public readonly Tid ID;

        public SingleIdentifierParameters(Tid id)
        {
            ID = id;
        }
    }
}
