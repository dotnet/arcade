// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Microsoft.DotNet.GenAPI.Shared;


using IFilter = IAssemblySymbolFilter;

public class IntersectionFilter : IFilter
{
    private readonly List<IFilter> _innerFilters = new List<IFilter>();

    /// <inheritdoc />
    public override bool Include(INamespaceSymbol ns) => _innerFilters.All(f => f.Include(ns));

    /// <inheritdoc />
    public override bool Include(AttributeData at) => _innerFilters.All(f => f.Include(at));

    /// <inheritdoc />
    public override bool Include(ITypeSymbol ts) => _innerFilters.All(f => f.Include(ts));

    /// <inheritdoc />
    public override bool Include(ISymbol member) => _innerFilters.All(f => f.Include(member));

    public IntersectionFilter Add<T>() where T : IFilter, new()
    {
        _innerFilters.Add(new T());
        return this;
    }

    public IntersectionFilter Add<T>(T obj) where T : IFilter
    {
        _innerFilters.Add(obj);
        return this;
    }
}
