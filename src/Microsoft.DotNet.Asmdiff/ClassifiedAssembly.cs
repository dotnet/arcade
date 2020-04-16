// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Cci;

namespace Microsoft.DotNet.AsmDiff
{
    public struct ClassifiedAssembly
    {
        public ClassifiedAssembly(IAssemblyReference reference, AssemblyClassification classification)
        {
            Reference = reference;
            Classification = classification;
        }

        public IAssemblyReference Reference { get; }

        public AssemblyClassification Classification { get; }
    }
}
