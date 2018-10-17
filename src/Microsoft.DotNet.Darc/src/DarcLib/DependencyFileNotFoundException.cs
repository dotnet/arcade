// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.Serialization;

namespace Microsoft.DotNet.DarcLib
{
    [Serializable]
    public class DependencyFileNotFoundException : DarcException
    {
        public DependencyFileNotFoundException(
            string filePath,
            string repository,
            string branch,
            Exception innerException) : base(
            $"Required dependency file '{filePath}' in repository '{repository}' branch '{branch}' was not found.",
            innerException)
        {
        }

        protected DependencyFileNotFoundException(SerializationInfo info, StreamingContext context) : base(
            info,
            context)
        {
        }

        public DependencyFileNotFoundException()
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
