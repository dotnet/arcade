// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Runtime.Versioning;

namespace Microsoft.DotNet.SignTool.Tests;

internal static class Config
{
    const string ConfigSwitchPrefix = "Microsoft.DotNet.SignTool.Tests.";

    public static string DotNetPath => (string)AppContext.GetData(ConfigSwitchPrefix + nameof(DotNetPath))! ?? throw new InvalidOperationException("DotNetPath must be specified");
    public static string TarToolPath => Path.Combine(Path.GetDirectoryName(typeof(SignToolTests).Assembly.Location), "tools", "tar", "Microsoft.Dotnet.Tar.dll");
    public static string PkgToolPath => Path.Combine(Path.GetDirectoryName(typeof(SignToolTests).Assembly.Location), "tools", "pkg", "Microsoft.Dotnet.MacOsPkg.dll");
    public static string SNPath => Path.Combine(Path.GetDirectoryName(typeof(SignToolTests).Assembly.Location), "tools", "sn", "sn.exe");
}
