// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Microsoft.DotNet.GenAPI.Shared;

public abstract class AssemblySymbolTraverser
{
    private readonly IAssemblySymbolOrderProvider _orderProvider;
    private readonly IAssemblySymbolFilter _filter;

    protected IAssemblySymbolFilter Filter { get { return _filter; } }

    public AssemblySymbolTraverser(IAssemblySymbolOrderProvider orderProvider, IAssemblySymbolFilter filter)
    {
        _orderProvider = orderProvider;
        _filter = filter;
    }

    public void Visit(IAssemblySymbol assembly)
    {
        var namespaces = EnumerateNamespaces(assembly).Where(_filter.Include);

        foreach (var namespaceSymbol in _orderProvider.Order(namespaces))
        {
            using var rs = ProcessBlock(namespaceSymbol);
            Visit(namespaceSymbol);
        }
    }

    public void Visit(INamespaceSymbol namespaceSymbol)
    {
        var typeMembers = namespaceSymbol.GetTypeMembers().Where(_filter.Include);

        foreach (var typeMember in _orderProvider.Order(typeMembers))
        {
            foreach (var attribute in typeMember.GetAttributes().Where(_filter.Include))
            {
                Process(attribute);
            }

            using var rs = ProcessBlock(typeMember);
            Visit(typeMember);
        }
    }

    public void Visit(INamedTypeSymbol namedType)
    {
        VisitInnerNamedTypes(namedType);

        var members = namedType.GetMembers().Where(_filter.Include);

        foreach (var member in _orderProvider.Order(members))
        {
            foreach (var attribute in member.GetAttributes().Where(_filter.Include))
            {
                Process(attribute);
            }

            Process(member);
        }
    }

    protected abstract IDisposable ProcessBlock(INamespaceSymbol namespaceSymbol);
    protected abstract IDisposable ProcessBlock(INamedTypeSymbol namedType);
    protected abstract void Process(ISymbol member);
    protected abstract void Process(AttributeData data);

    private void VisitInnerNamedTypes(INamedTypeSymbol namedType)
    {
        var innerNamedTypes = namedType.GetTypeMembers().Where(_filter.Include);

        foreach (var innerNamedType in _orderProvider.Order(innerNamedTypes))
        {
            using var rs = ProcessBlock(innerNamedType);
            Visit(innerNamedType);
        }
    }

    private IEnumerable<INamespaceSymbol> EnumerateNamespaces(IAssemblySymbol assembly)
    {
        var stack = new Stack<INamespaceSymbol>();
        stack.Push(assembly.GlobalNamespace);

        while (stack.TryPop(out var current))
        {
            yield return current;

            foreach (var subNamespace in current.GetNamespaceMembers())
            {
                stack.Push(subNamespace);
            }
        }
    }
}
