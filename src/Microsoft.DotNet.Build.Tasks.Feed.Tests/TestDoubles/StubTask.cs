// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;

namespace Microsoft.DotNet.Build.Tasks.Feed.Tests.TestDoubles
{
    public class StubTask : ITask
    {
        public IBuildEngine BuildEngine { get; set; } = new MockBuildEngine();
        public ITaskHost HostObject { get; set; }

        public bool Execute()
        {
            return true;
        }
    }
}
