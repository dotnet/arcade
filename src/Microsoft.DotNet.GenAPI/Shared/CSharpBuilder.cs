// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Microsoft.DotNet.GenAPI.Shared;

/// <summary>
/// Processes assemly symbols to build correspoding structures in C# language.
/// Depend on ISyntaxWriter impelemtenation, the result could be C# source file, xml etc.
/// </summary>
public class CSharpBuilder : AssemblySymbolTraverser, IAssemblySymbolWriter, IDisposable
{
    private readonly ISyntaxWriter _syntaxWriter;

    public CSharpBuilder(IAssemblySymbolOrderProvider orderProvider,
        IAssemblySymbolFilter filter, ISyntaxWriter syntaxWriter)
        : base(orderProvider, filter) => _syntaxWriter = syntaxWriter;

    public void WriteAssembly(IAssemblySymbol assembly) => Visit(assembly);

    protected override IDisposable ProcessBlock(INamespaceSymbol namespaceSymbol)
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

    protected override IDisposable ProcessBlock(INamedTypeSymbol namedType)
    {
        var typeName = namedType.ToDisplayString(AssemblySymbolDisplayFormats.NamedTypeDisplayFormat);

        var accessibility = BuildAccessibility(namedType as ISymbol);
        var keywords = GetKeywords((ITypeSymbol)namedType);

        var baseTypeNames = BuildBaseTypes(namedType);
        var constraints = BuildConstraints(namedType);

        return _syntaxWriter.WriteTypeDefinition(accessibility, keywords, typeName, baseTypeNames, constraints);
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

    #region Private methods

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
        _syntaxWriter.WriteMethod(ms.ToDisplayString(AssemblySymbolDisplayFormats.MemberDisplayFormat),
            hasImplementation: !ms.IsAbstract);
    }

    private IEnumerable<SyntaxKind> BuildAccessibility(ISymbol symbol)
    {
        return symbol.DeclaredAccessibility switch
        {
            Accessibility.Private => new SyntaxKind[] { SyntaxKind.PrivateKeyword },
            Accessibility.Internal => new SyntaxKind[] { SyntaxKind.InternalKeyword },
            Accessibility.ProtectedAndInternal => new SyntaxKind[] { SyntaxKind.PrivateKeyword, SyntaxKind.ProtectedKeyword },
            Accessibility.Protected => new SyntaxKind[] { SyntaxKind.ProtectedKeyword },
            Accessibility.ProtectedOrInternal => new SyntaxKind[] { SyntaxKind.ProtectedKeyword, SyntaxKind.InternalKeyword },
            Accessibility.Public => new SyntaxKind[] { SyntaxKind.PublicKeyword },
            _ => throw new Exception(string.Format("Unexpected accessibility modifier found {0}",
                    SyntaxFacts.GetText(symbol.DeclaredAccessibility)))
        };
    }

    private IEnumerable<string> BuildBaseTypes(INamedTypeSymbol namedType)
    {
        var baseTypeNames = new List<string>();

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

        return baseTypeNames;
    }

    private IEnumerable<IEnumerable<SymbolDisplayPart>> BuildConstraints(INamedTypeSymbol namedType)
    {
        bool whereKeywordFound = false;
        var currConstraint = new List<SymbolDisplayPart>();
        var constraints = new List<List<SymbolDisplayPart>>();

        foreach (var part in namedType.ToDisplayParts(AssemblySymbolDisplayFormats.BaseTypeDisplayFormat))
        {
            if (part.Kind == SymbolDisplayPartKind.Keyword &&
                part.ToString() == SyntaxFacts.GetText(SyntaxKind.WhereKeyword))
            {
                if (whereKeywordFound)
                {
                    constraints.Add(currConstraint);
                    currConstraint.Clear();
                }

                currConstraint.Add(part);
                whereKeywordFound = true;
            }
            else if (whereKeywordFound)
            {
                currConstraint.Add(part);
            }
        }

        if (currConstraint.Any())
        {
            constraints.Add(currConstraint);
        }

        return constraints;
    }

    private IEnumerable<SyntaxKind> GetKeywords(ITypeSymbol namedType)
    {
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
                {
                if (namedType.IsReadOnly)
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

        return keywords;
    }

    #endregion
}

