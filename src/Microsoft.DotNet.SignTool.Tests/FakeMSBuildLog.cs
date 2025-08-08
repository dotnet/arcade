// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.Collections.Generic;

namespace Microsoft.DotNet.SignTool.Tests
{
    /// <summary>
    /// A fake MSBuild logging helper for unit tests.
    /// </summary>
    internal class FakeMSBuildLog : TaskLoggingHelper
    {
        public FakeMSBuildLog() : base(new FakeBuildEngine(), "FakeTask")
        {
        }
    }
}
