// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.DarcLib
{
    public class GitCommit
    {
        public GitCommit(string content)
        {
            Content = content;
        }

        public GitCommit(string message, string content, string branch)
        {
            Message = message;
            Content = content;
            Branch = branch;
        }

        public string Message { get; set; }

        public string Content { get; set; }

        public string Branch { get; set; }

        public string Sha { get; set; }
    }
}
