// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Microsoft.DotNet.GenFacades.ILRewriter
{
    internal sealed class FacadeGenerationException : Exception
    {
        public FacadeGenerationException(string message) : base(message)
        {
        }
    }
}
