// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.DotNet.PackageValidation.Tests
{
    internal class Helpers
    {
        internal static IEnumerable<string> allTargetFrameworks => new string[]
        {
            "netcoreapp2.0",
            "netcoreapp2.1",
            "netcoreapp3.0",
            "netcoreapp3.1",
            "net5.0",
            "netstandard1.0",
            "netstandard1.1",
            "netstandard1.2",
            "netstandard1.3",
            "netstandard1.4",
            "netstandard1.5",
            "netstandard1.6",
            "netstandard2.0",
            "netstandard2.1",
            "net45",
            "net451",
            "net452",
            "net461",
            "net462",
            "net463",
            "uap10"
        };
    }
}