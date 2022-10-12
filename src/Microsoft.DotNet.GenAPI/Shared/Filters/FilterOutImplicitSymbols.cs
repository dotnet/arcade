// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;

namespace Microsoft.DotNet.GenAPI.Shared;

public class FilterOutImplicitSymbols : IAssemblySymbolFilter
{
    /// <inheritdoc />
    public override bool Include(INamespaceSymbol ns) => !ns.IsImplicitlyDeclared;

    /// <inheritdoc />
    public override bool Include(ITypeSymbol ts) => !ts.IsImplicitlyDeclared;

    /// <inheritdoc />
    public override bool Include(ISymbol member)
    {
        if (member.Kind == SymbolKind.NamedType || member.IsImplicitlyDeclared)
        {
            return false;
        }

        if (member is IMethodSymbol method)
        {
            if (method.MethodKind == MethodKind.PropertyGet ||
                method.MethodKind == MethodKind.PropertySet ||
                method.MethodKind == MethodKind.EventAdd ||
                method.MethodKind == MethodKind.EventRemove ||
                method.MethodKind == MethodKind.EventRaise ||
                method.MethodKind == MethodKind.DelegateInvoke)
            {
                return false;
            }
        }
        return true;
    }
}
