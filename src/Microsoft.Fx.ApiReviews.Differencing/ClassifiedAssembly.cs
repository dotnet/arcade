// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Cci;

namespace Microsoft.Fx.ApiReviews.Differencing
{
    public struct ClassifiedAssembly
    {
        private readonly IAssemblyReference _reference;
        private readonly AssemblyClassification _classification;

        public ClassifiedAssembly(IAssemblyReference reference, AssemblyClassification classification)
        {
            _reference = reference;
            _classification = classification;
        }

        public IAssemblyReference Reference
        {
            get { return _reference; }
        }

        public AssemblyClassification Classification
        {
            get { return _classification; }
        }
    }
}
