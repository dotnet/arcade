// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Reflection;

namespace Microsoft.DotNet.XHarness.TestRunners.Common;

public class TestAssemblyInfo
{
    public Assembly Assembly { get; }
    public string FullPath { get; }

    public TestAssemblyInfo(Assembly assembly, string fullPath)
    {
        Assembly = assembly ?? throw new ArgumentNullException(nameof(assembly));
        FullPath = fullPath ?? string.Empty;
    }
}
