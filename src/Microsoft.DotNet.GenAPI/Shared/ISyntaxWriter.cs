// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.CodeAnalysis.CSharp;

namespace Microsoft.DotNet.GenAPI.Shared;

/// <summary>
/// Interface responsible for writing C# code in various formats: code file, xml, etc.
/// </summary>
public interface ISyntaxWriter : IDisposable
{
    void WriteSymbol(string str);
    void WriteKeyword(SyntaxKind sk);
    void WriteLine();
}
