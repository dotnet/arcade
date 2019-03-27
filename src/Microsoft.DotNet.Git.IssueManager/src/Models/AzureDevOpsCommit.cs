// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;

namespace Microsoft.DotNet.Git.IssueManager
{
    public class AzureDevOpsCommit
    {
        public List<Value> Value { get; set; }
    }

    public class Value
    {
        public Author Author { get; set; }
    }

    public class Author
    {
        public string Name { get; set; }

        public string Email { get; set; }

        public DateTime Date { get; set; }
    }
}
