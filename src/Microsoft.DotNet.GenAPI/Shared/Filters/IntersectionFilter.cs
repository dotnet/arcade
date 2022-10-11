// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace Microsoft.DotNet.GenAPI.Shared;


using IFilter = IAssemblySymbolFilter;

public class IntersectionFilter : IFilter
{
    private readonly List<IFilter> _innerFilters = new List<IFilter>();

    /// <inheritdoc />
    public bool Include(INamespaceSymbol ns)
    {
        foreach (var innerFilter in _innerFilters)
        {
            if (!innerFilter.Include(ns)) return false;
        }
        return true;
    }

    /// <inheritdoc />
    public bool Include(AttributeData at)
    {
        foreach (var innerFilter in _innerFilters)
        {
            if (!innerFilter.Include(at)) return false;
        }
        return true;
    }

    /// <inheritdoc />
    public bool Include(ITypeSymbol ts)
    {
        foreach (var innerFilter in _innerFilters)
        {
            if (!innerFilter.Include(ts)) return false;
        }
        return true;
    }

    /// <inheritdoc />
    public bool Include(ISymbol member)
    {
        foreach (var innerFilter in _innerFilters)
        {
            if (!innerFilter.Include(member)) return false;
        }
        return true;
    }

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
