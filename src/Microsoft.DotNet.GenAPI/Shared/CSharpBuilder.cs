// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Microsoft.DotNet.GenAPI.Shared;

public class CSharpBuilder : AssemblySymbolTraverser, IAssemblySymbolWriter, IDisposable
{
    private readonly ISyntaxWriter _syntaxWriter;

    public CSharpBuilder(IAssemblySymbolOrderProvider orderProvider,
        IAssemblySymbolFilter filter, ISyntaxWriter syntaxWriter)
        : base(orderProvider, filter) => _syntaxWriter = syntaxWriter;

    public void WriteAssembly(IAssemblySymbol assembly) => Visit(assembly);

    protected override IDisposable Process(INamespaceSymbol namespaceSymbol)
    {
        var namespacePath = new List<string>();

        foreach (var part in namespaceSymbol.ToDisplayParts(
            AssemblySymbolDisplayFormats.NamespaceDisplayFormat))
        {
            if (part.Kind == SymbolDisplayPartKind.NamespaceName)
            {
                namespacePath.Add(part.ToString());
            }
        }
        return _syntaxWriter.WriteNamespace(namespacePath);
    }

    protected override IDisposable Process(INamedTypeSymbol namedType)
    {
        var accessibility = BuildAccessibility(namedType);

        var keywords = new List<SyntaxKind>();

        switch (namedType.TypeKind)
        {
            case TypeKind.Class:
                if (namedType.IsAbstract)
                {
                    keywords.Add(SyntaxKind.AbstractKeyword);
                }
                if (namedType.IsStatic)
                {
                    keywords.Add(SyntaxKind.StaticKeyword);
                }
                if (namedType.IsSealed)
                {
                    keywords.Add(SyntaxKind.SealedKeyword);
                }

                keywords.Add(SyntaxKind.PartialKeyword);
                keywords.Add(SyntaxKind.ClassKeyword);
                break;
            case TypeKind.Delegate:
                keywords.Add(SyntaxKind.DelegateKeyword);
                break;
            case TypeKind.Enum:
                keywords.Add(SyntaxKind.EnumKeyword);
                break;
            case TypeKind.Interface:
                keywords.Add(SyntaxKind.InterfaceKeyword);
                break;
            case TypeKind.Struct:
                if (namedType.IsReadOnly)
                {
                    keywords.Add(SyntaxKind.ReadOnlyKeyword);
                }
                if (namedType.IsRefLikeType)
                {
                    keywords.Add(SyntaxKind.RefKeyword);
                }
                keywords.Add(SyntaxKind.PartialKeyword);
                keywords.Add(SyntaxKind.StructKeyword);
                break;
        }

        var typeName = namedType.ToDisplayString(AssemblySymbolDisplayFormats.NamedTypeDisplayFormat);

        var baseTypeNames = new List<string>();
        var constrains = new List<List<SymbolDisplayPart>>();

        BuildBaseTypes(namedType, ref baseTypeNames);
        BuildConstrains(namedType, ref constrains);

        return _syntaxWriter.WriteTypeDefinition(accessibility, keywords, typeName, baseTypeNames, constrains);
    }

    protected override void Process(ISymbol member)
    {
        switch (member.Kind)
        {
            case SymbolKind.Property:
                Process((IPropertySymbol)member);
                break;

            case SymbolKind.Event:
                Process((IEventSymbol)member);
                break;

            case SymbolKind.Method:
                Process((IMethodSymbol)member);
                break;

            default:
                break;
        }
    }

    protected override void Process(AttributeData data)
    {
        var attribute = data.ToString();
        if (attribute != null)
        {
            _syntaxWriter.WriteAttribute(attribute);
        }
    }

    public void Dispose() => _syntaxWriter.Dispose();

    /// ------------------------------------------------------

    private void Process(IPropertySymbol ps)
    {
        _syntaxWriter.WriteProperty(ps.ToDisplayString(AssemblySymbolDisplayFormats.MemberDisplayFormat),
            hasImplementation: !ps.IsAbstract, ps.GetMethod != null, ps.SetMethod != null);
    }

    private void Process(IEventSymbol es)
    {
        _syntaxWriter.WriteEvent(es.ToDisplayString(AssemblySymbolDisplayFormats.MemberDisplayFormat),
            es.AddMethod != null, es.RemoveMethod != null);
    }

    private void Process(IMethodSymbol ms)
    {
        _syntaxWriter.Writemethod(ms.ToDisplayString(AssemblySymbolDisplayFormats.MemberDisplayFormat),
            hasImplementation: !ms.IsAbstract);
    }

    private IEnumerable<SyntaxKind> BuildAccessibility(ISymbol symbol)
    {
        switch (symbol.DeclaredAccessibility)
        {
            case Accessibility.Private:
                return new SyntaxKind[] { SyntaxKind.PrivateKeyword };
            case Accessibility.Internal:
                return new SyntaxKind[] { SyntaxKind.InternalKeyword };
            case Accessibility.ProtectedAndInternal:
                return new SyntaxKind[] { SyntaxKind.PrivateKeyword, SyntaxKind.ProtectedKeyword };
            case Accessibility.Protected:
                return new SyntaxKind[] { SyntaxKind.ProtectedKeyword };
            case Accessibility.ProtectedOrInternal:
                return new SyntaxKind[] { SyntaxKind.ProtectedKeyword, SyntaxKind.InternalKeyword };
            case Accessibility.Public:
                return new SyntaxKind[] { SyntaxKind.PublicKeyword };
            default:
                throw new Exception(String.Format("Unexpected accesibility found {0}",
                    SyntaxFacts.GetText(symbol.DeclaredAccessibility)));
        }
    }

    private void BuildBaseTypes(INamedTypeSymbol namedType, ref List<string> baseTypeNames)
    {
        if (namedType.BaseType != null && namedType.BaseType.SpecialType == SpecialType.None &&
            Filter.Include(namedType.BaseType))
        {
            baseTypeNames.Add(namedType.BaseType.ToDisplayString());
        }

        foreach (var interfaceSymbol in namedType.Interfaces)
        {
            if (!Filter.Include(interfaceSymbol)) continue;

            baseTypeNames.Add(interfaceSymbol.ToDisplayString());
        }
    }

    private void BuildConstrains(INamedTypeSymbol namedType, ref List<List<SymbolDisplayPart>> constrains)
    {
        bool whereKeywordFound = false;
        var currConstrain = new List<SymbolDisplayPart>();

        foreach (var part in namedType.ToDisplayParts(AssemblySymbolDisplayFormats.BaseTypeDisplayFormat))
        {
            if (part.Kind == SymbolDisplayPartKind.Keyword &&
                part.ToString() == SyntaxFacts.GetText(SyntaxKind.WhereKeyword))
            {
                if (whereKeywordFound)
                {
                    constrains.Add(currConstrain);
                    currConstrain = new List<SymbolDisplayPart>();
                }

                currConstrain.Add(part);
                whereKeywordFound = true;
            }
            else if (whereKeywordFound)
            {
                currConstrain.Add(part);
            }
        }

        if (currConstrain.Count() > 0)
        {
            constrains.Add(currConstrain);
        }
    }
}

