// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;

namespace Microsoft.DotNet.GenAPI.Shared;

public class IncludeAllFilter : IAssemblySymbolFilter
{
    /// <inheritdoc />
    public bool Include(INamespaceSymbol ns)
    {
        return true;
    }

    /// <inheritdoc />
    public bool Include(AttributeData at)
    {
        return true;
    }

    /// <inheritdoc />
    public bool Include(ITypeSymbol ts)
    {
        return true;
    }

    /// <inheritdoc />
    public bool Include(ISymbol member)
    {
        return true;
    }
}
