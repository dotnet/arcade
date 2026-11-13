// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using Microsoft.Arcade.Test.Common;
using Microsoft.DotNet.Internal.SymbolHelper;
using Xunit;

namespace Microsoft.DotNet.Build.Tasks.Feed.Tests;

public class SymbolUploadHelperTests
{
    private static readonly string s_extractDir = Path.Combine(Path.GetTempPath(), "symbol-extract");

    [Theory]
    [InlineData("lib/net8.0/a.dll")]
    [InlineData("runtimes/win-x64/native/a.pdb")]
    [InlineData("lib/../lib/a.dll")]
    [InlineData("a.dll")]
    public void TryGetSafeExtractionPath_AllowsEntriesInsideDirectory(string entryName)
    {
        Assert.True(SymbolUploadHelper.TryGetSafeExtractionPath(s_extractDir, entryName, out string entryPath));
        Assert.StartsWith(Path.GetFullPath(s_extractDir) + Path.DirectorySeparatorChar, entryPath, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("../a.dll")]
    [InlineData("../../a.dll")]
    [InlineData("lib/../../a.dll")]
    [InlineData("../symbol-extract-sibling/a.dll")]
    [InlineData("")]
    [InlineData("/a.dll")]
    public void TryGetSafeExtractionPath_RejectsEscapingEntries(string entryName)
    {
        Assert.False(SymbolUploadHelper.TryGetSafeExtractionPath(s_extractDir, entryName, out string entryPath));
        Assert.Null(entryPath);
    }

    [WindowsOnlyTheory]
    [InlineData("..\\a.dll")]
    [InlineData("lib\\..\\..\\a.dll")]
    [InlineData("\\a.dll")]
    [InlineData("C:\\a.dll")]
    [InlineData("C:a.dll")]
    [InlineData("\\\\server\\share\\a.dll")]
    public void TryGetSafeExtractionPath_RejectsEscapingWindowsEntries(string entryName)
    {
        Assert.False(SymbolUploadHelper.TryGetSafeExtractionPath(s_extractDir, entryName, out string entryPath));
        Assert.Null(entryPath);
    }
}
