// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Runtime.Serialization;

namespace Microsoft.DotNet.GenPartialFacades
{
    [Serializable]
    internal class FacadeGenerationException : Exception
    {
        public FacadeGenerationException()
        {
        }

        public FacadeGenerationException(string message) : base(message)
        {
        }

        public FacadeGenerationException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected FacadeGenerationException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}