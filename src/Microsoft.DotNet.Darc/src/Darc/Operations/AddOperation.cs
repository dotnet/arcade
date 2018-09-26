// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.Darc.Options;
using System;

namespace Microsoft.DotNet.Darc.Operations
{
    internal class AddOperation : Operation
    {
        AddCommandLineOptions _options;
        public AddOperation(AddCommandLineOptions options)
            : base(options)
        {
            _options = options;
        }

        public override int Execute()
        {
            throw new NotImplementedException("Add operation not yet implemented");
        }
    }
}
