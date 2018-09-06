// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.DotNet.DarcLib
{
    public class GitHubCommit
    {
        public string Message { get; set; }

        public string Tree { get; set; }

        public List<string> Parents { get; set; }
    }
}
