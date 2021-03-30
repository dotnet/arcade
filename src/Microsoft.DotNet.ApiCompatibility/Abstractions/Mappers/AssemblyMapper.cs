using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.DotNet.ApiCompatibility.Abstractions
{
    public class AssemblyMapper : ElementMapper<IAssemblySymbol>
    {
        private Dictionary<string, NamespaceMapper> _namespaces;

        public AssemblyMapper(DiffingSettings settings) : base(settings) { }

        public IEnumerable<NamespaceMapper> GetNamespaces()
        {
            if (_namespaces == null)
            {
                _namespaces = new Dictionary<string, NamespaceMapper>();
                Dictionary<string, List<INamedTypeSymbol>> typeForwards;
                if (Left != null)
                {
                    typeForwards = ResolveTypeForwards(Left);
                    AddOrCreateMappers(Left.GlobalNamespace, 0);
                }

                if (Right != null)
                {
                    typeForwards = ResolveTypeForwards(Right);
                    AddOrCreateMappers(Right.GlobalNamespace, 1);
                }

                void AddOrCreateMappers(INamespaceSymbol ns, int index)
                {
                    Stack<INamespaceSymbol> stack = new();
                    stack.Push(ns);
                    while (stack.Count > 0)
                    {
                        INamespaceSymbol symbol = stack.Pop();
                        string name = symbol.ToDisplayString();
                        if (typeForwards.TryGetValue(name, out List<INamedTypeSymbol> forwardedTypes) || symbol.GetTypeMembers().Length > 0)
                        {
                            if (!_namespaces.TryGetValue(name, out NamespaceMapper mapper))
                            {
                                mapper = new NamespaceMapper(Settings);
                                _namespaces.Add(name, mapper);
                            }

                            mapper.AddElement(symbol, index);
                            mapper.AddForwardedTypes(forwardedTypes ?? new List<INamedTypeSymbol>(), index);
                        }

                        foreach (INamespaceSymbol child in symbol.GetNamespaceMembers())
                            stack.Push(child);
                    }
                }

                static Dictionary<string, List<INamedTypeSymbol>> ResolveTypeForwards(IAssemblySymbol assembly)
                {
                    Dictionary<string, List<INamedTypeSymbol>> typeForwards = new();
                    foreach (INamedTypeSymbol symbol in assembly.GetForwardedTypes())
                    {
                        if (symbol.TypeKind != TypeKind.Error)
                        {
                            string containingNamespace = symbol.ContainingNamespace.ToDisplayString();
                            if (!typeForwards.TryGetValue(containingNamespace, out List<INamedTypeSymbol> types))
                            {
                                types = new List<INamedTypeSymbol>();
                                typeForwards.Add(containingNamespace, types);
                            }

                            types.Add(symbol);
                        }
                        else
                        {
                            // TODO: Log Warning;
                        }
                    }

                    return typeForwards;
                }
            }

            return _namespaces.Values;
        }
    }
}
