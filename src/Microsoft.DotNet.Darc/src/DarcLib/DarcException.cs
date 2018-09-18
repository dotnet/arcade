using System;
using System.Runtime.Serialization;

namespace Microsoft.DotNet.DarcLib
{
    [Serializable]
    public class DarcException : Exception
    {
        public DarcException() : base()
        {
        }

        protected DarcException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }

        public DarcException(string message) : base(message)
        {
        }

        public DarcException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}