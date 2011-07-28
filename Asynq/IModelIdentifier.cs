using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Asynq
{
    public interface IModelIdentifier
    {
        int Value { get; }
    }
}
