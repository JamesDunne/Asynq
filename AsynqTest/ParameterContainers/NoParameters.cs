using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AsynqTest.ParameterContainers
{
    /// <summary>
    /// Nonce parameter container.
    /// </summary>
    public struct NoParameters
    {
        /// <summary>
        /// Get the default instance.
        /// </summary>
        public static readonly NoParameters Default = new NoParameters();
    }
}
