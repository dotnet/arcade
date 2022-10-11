// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.DotNet.GenAPI.Shared;

public class AccessibilityFilter : IAssemblySymbolFilter
{
    private readonly HashSet<Accessibility> _allowedAccessibilities;
    
    public AccessibilityFilter(IEnumerable<Accessibility> allowedAccessibilities)
    {
        _allowedAccessibilities = new HashSet<Accessibility>(allowedAccessibilities);
    }

    /// <inheritdoc />
    public bool Include(INamespaceSymbol ns)
    {
        return ns.GetTypeMembers().Any(t =>
        {
            return _allowedAccessibilities.Contains(t.DeclaredAccessibility);
        });
    }

    /// <inheritdoc />
    public bool Include(ISymbol member)
    {
        return _allowedAccessibilities.Contains(member.DeclaredAccessibility);
    }
}
