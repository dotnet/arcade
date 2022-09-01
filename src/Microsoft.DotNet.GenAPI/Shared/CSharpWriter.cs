// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.CodeAnalysis;

namespace Microsoft.DotNet.GenAPI.Shared;

public abstract class CSharpWriter : AssemblySymbolTraverser, IWriter, IDisposable
{
    private readonly ISyntaxWriter _syntaxWriter;

    public CSharpWriter(IAssemblySymbolOrderProvider orderProvider,
        IAssemblySymbolFilter filter, ISyntaxWriter syntaxWriter)
        : base(orderProvider, filter)
    {
        _syntaxWriter = syntaxWriter;
    }

    public void WriteAssemblies(IAssemblySymbol assembly)
    {
        Visit(assembly);
    }

    public void Dispose()
    {
        _syntaxWriter.Dispose();
    }
}
