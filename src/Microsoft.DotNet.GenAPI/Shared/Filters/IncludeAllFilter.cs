// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;

namespace Microsoft.DotNet.GenAPI.Shared;

public class IncludeAllFilter : IAssemblySymbolFilter
{
    /// <inheritdoc />
    public virtual bool Include(INamespaceSymbol ns) => true;

    /// <inheritdoc />
    public virtual bool Include(AttributeData at) => true;

    /// <inheritdoc />
    public virtual bool Include(ITypeSymbol ts) => true;

    /// <inheritdoc />
    public virtual bool Include(ISymbol member) => true;
}
