// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.Asmdiff
{
    public sealed class DiffNewAssemblyCsvColumn : DiffAssemblyCsvColumn
    {
        public DiffNewAssemblyCsvColumn(DiffConfiguration diffConfiguration)
            : base(diffConfiguration, 1)
        {
        }
    }
}
