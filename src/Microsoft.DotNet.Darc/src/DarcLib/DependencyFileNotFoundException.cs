using System;
using System.Runtime.Serialization;

namespace Microsoft.DotNet.DarcLib
{
    [Serializable]
    public class DependencyFileNotFoundException : DarcException
    {
        public DependencyFileNotFoundException(string filePath, string repository, string branch, Exception innerException)
            : base($"Required dependency file '{filePath}' in repository '{repository}' branch '{branch}' was not found.", innerException)
        {
        }

        protected DependencyFileNotFoundException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }

        public DependencyFileNotFoundException() : base()
        {
        }

        public DependencyFileNotFoundException(string message) : base(message)
        {
        }

        public DependencyFileNotFoundException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}