// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.AspNetCore.ApiVersioning.Analyzers
{
    internal struct AttributeUsageInfo
    {
        public AttributeUsageInfo(ITypeSymbol attributeType, IImmutableList<AttributeParameterInfo> parameters)
        {
            AttributeType = attributeType;
            Parameters = parameters;
        }

        public ITypeSymbol AttributeType { get; }
        public IImmutableList<AttributeParameterInfo> Parameters { get; }

        public void Deconstruct(out ITypeSymbol attributeType, out IImmutableList<AttributeParameterInfo> parameters)
        {
            attributeType = AttributeType;
            parameters = Parameters;
        }
    }

    internal struct AttributeParameterInfo
    {
        public AttributeParameterInfo(string name, ExpressionSyntax expression)
        {
            Name = name;
            Expression = expression;
        }

        public string Name { get; }
        public ExpressionSyntax Expression { get; }
    }

    internal static class Helpers
    {
        public static IImmutableList<ITypeSymbol> GetAllBaseTypeSymbols(
            this SemanticModel model,
            ClassDeclarationSyntax node)
        {
            var type = (ITypeSymbol) model.GetDeclaredSymbol(node);
            ImmutableList<ITypeSymbol>.Builder baseTypes = ImmutableList.CreateBuilder<ITypeSymbol>();
            while (type.BaseType != null)
            {
                baseTypes.Add(type.BaseType);
                type = type.BaseType;
            }

            return baseTypes.ToImmutable();
        }

        public static ITypeSymbol GetTypeSymbol(this SemanticModel model, TypeSyntax type)
        {
            SymbolInfo info = model.GetSymbolInfo(type);
            if (info.Symbol is ITypeSymbol typeSymbol)
            {
                return typeSymbol;
            }

            throw new InvalidOperationException($"Type {type} is somehow not a ITypeSymbol");
        }

        public static ITypeSymbol GetTypeSymbol(this SemanticModel model, ExpressionSyntax expression)
        {
            return model.GetTypeInfo(expression).Type;
        }

        public static bool IsAssignableTo(this SemanticModel model, ITypeSymbol source, ITypeSymbol destination)
        {
            Conversion conversion = model.Compilation.ClassifyConversion(source, destination);
            return conversion.Exists;
        }

        public static IImmutableList<AttributeUsageInfo> GetAttributeInfo(
            this SemanticModel model,
            SyntaxList<AttributeListSyntax> attributeLists)
        {
            AttributeParameterInfo GetArgumentParameter(AttributeArgumentSyntax arg, int idx)
            {
                string name = idx.ToString();
                if (arg.NameColon != null)
                {
                    name = arg.NameColon.Name.Identifier.Text;
                }
                else if (arg.NameEquals != null)
                {
                    name = arg.NameEquals.Name.Identifier.Text;
                }

                return new AttributeParameterInfo(name, arg.Expression);
            }


            IImmutableList<AttributeParameterInfo> GetParameters(AttributeSyntax attribute)
            {
                if (attribute.ArgumentList == null)
                {
                    return ImmutableList.Create<AttributeParameterInfo>();
                }

                return attribute.ArgumentList.Arguments.Select(GetArgumentParameter).ToImmutableList();
            }

            AttributeUsageInfo GetSingleAttributeInfo(AttributeSyntax attribute)
            {
                ISymbol method = model.GetSymbolInfo(attribute).Symbol;
                INamedTypeSymbol type = method.ContainingType;
                return new AttributeUsageInfo(type, GetParameters(attribute));
            }

            return attributeLists.SelectMany(l => l.Attributes).Select(GetSingleAttributeInfo).ToImmutableList();
        }

        /// <summary>
        ///     Gets all used types, including nested generic types, from the input type syntaxes
        /// </summary>
        /// <param name="model"></param>
        /// <param name="types"></param>
        /// <example>
        ///     List&lt;int&gt; produces the types: <see cref="List{T}" /> and <see cref="int" /> along with the source locations
        ///     for the usage of <see cref="List{T}" /> and <see cref="int" />
        /// </example>
        /// <returns></returns>
        public static IEnumerable<(ITypeSymbol symbol, Location location)> GetTypeUsages(
            this SemanticModel model,
            IEnumerable<(ITypeSymbol symbol, TypeSyntax syntax)> types)
        {
            foreach ((ITypeSymbol symbol, TypeSyntax syntax) type in types)
            {
                if (type.syntax is GenericNameSyntax genericNameSyntax)
                {
                    yield return (type.symbol, genericNameSyntax.Identifier.GetLocation());
                    foreach ((ITypeSymbol symbol, Location location) tuple in model.GetTypeUsages(
                        genericNameSyntax.TypeArgumentList.Arguments.Select(
                            ts => (symbol: model.GetTypeSymbol(ts), syntax: ts))))
                    {
                        yield return tuple;
                    }
                }
                else if (type.syntax is NullableTypeSyntax nullableTypeSyntax)
                {
                    yield return (type.symbol, nullableTypeSyntax.QuestionToken.GetLocation());
                    foreach ((ITypeSymbol symbol, Location location) tuple in model.GetTypeUsages(
                        new[]
                        {
                            (symbol: model.GetTypeSymbol(nullableTypeSyntax.ElementType),
                                syntax: nullableTypeSyntax.ElementType)
                        }))
                    {
                        yield return tuple;
                    }
                }
                else
                {
                    yield return (type.symbol, type.syntax.GetLocation());
                }
            }
        }
    }
}
