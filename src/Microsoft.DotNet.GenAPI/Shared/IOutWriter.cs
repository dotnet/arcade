// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.DotNet.GenAPI.Shared;

/// <summary>
/// Interface responsible for writing output to a file, console, stream etc.
/// </summary>
public interface IOutWriter : IDisposable
{
    void WriteSymbol(string str);
    void OpenBrace();
    void CloseBrace();
    void WriteSpace();
    void WriteLine();
}
