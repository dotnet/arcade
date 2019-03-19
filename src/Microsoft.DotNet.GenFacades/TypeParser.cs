// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Microsoft.DotNet.GenFacades
{
    internal class TypeParser
    {
        public static List<string> GetAllTypes(IEnumerable<string> files, IEnumerable<string> constants)
        {
            List<string> types = new List<string>();
            var syntaxTreeCollection = GetSourceTrees(files, constants);

            foreach (var tree in syntaxTreeCollection)
            {
                AddTypesFromTypeForwards(tree, types);
                AddBaseTypes(tree, types);
                AddTypesFromDelegates(tree, types);
            }
            return types;
        }

        private static void AddTypesFromTypeForwards(SyntaxTree tree, List<string> types)
        {
            CompilationUnitSyntax root = tree.GetCompilationUnitRoot();

            foreach (var attribute in root.AttributeLists)
            {
                foreach (var item in attribute.Attributes)
                {
                    string attributeString = item.ToFullString();
                    if (attributeString.Contains("TypeForwardedTo"))
                    {
                        var typeNameExpression = (TypeOfExpressionSyntax)item.ArgumentList.Arguments[0].Expression;
                        string typeName = typeNameExpression.Type.ToFullString();
                        types.Add(typeName);
                    }
                }
            }
        }

        private static void AddBaseTypes(SyntaxTree tree, List<string> types)
        {
            CompilationUnitSyntax root = tree.GetCompilationUnitRoot();
            var allPublicTypes = root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>()
                .Where(t => HasPublicModifier(t));

            foreach (var item in allPublicTypes)
            {
                string fullyQualifiedName = GetFullyQualifiedName(item);
                if (!types.Contains(fullyQualifiedName))
                    types.Add(fullyQualifiedName);
            }
        }

        private static void AddTypesFromDelegates(SyntaxTree tree, List<string> types)
        {
            CompilationUnitSyntax root = tree.GetCompilationUnitRoot();
            var allPublicTypes = root.DescendantNodes().OfType<DelegateDeclarationSyntax>()
                .Where(t => HasPublicModifier(t));

            foreach (var item in allPublicTypes)
            {
                string fullyQualifiedName;
                if (item.Parent is NamespaceDeclarationSyntax)
                {
                    fullyQualifiedName = ((NamespaceDeclarationSyntax)item.Parent).Name.ToFullString().Trim() + "." + GetDelegateTypeName(item);
                }
                else
                {
                    fullyQualifiedName = GetFullyQualifiedName((BaseTypeDeclarationSyntax)item.Parent, item.Identifier.ValueText.Trim());
                }

                if (!types.Contains(fullyQualifiedName))
                    types.Add(fullyQualifiedName);
            }
        }

        private static string GetFullyQualifiedName(BaseTypeDeclarationSyntax node, string nested = "")
        {
            string typeName = GetBaseTypeName(node);
            if (node.Parent is NamespaceDeclarationSyntax)
            {
                string namespaceName = GetNamespaceName((NamespaceDeclarationSyntax)node.Parent);
                string withoutNested = namespaceName + "." + typeName;
                return string.IsNullOrEmpty(nested) ? withoutNested : withoutNested + "." + nested;
            }

            return GetFullyQualifiedName((BaseTypeDeclarationSyntax)node.Parent, string.IsNullOrEmpty(nested) ? typeName : typeName + "." + nested);
        }

        private static string GetBaseTypeName(BaseTypeDeclarationSyntax type)
        {
            string typeName = type.Identifier.ValueText;

            if (type is TypeDeclarationSyntax)
            {
                var actualType = (TypeDeclarationSyntax)type;
                return GetTypeNameWithTypeParameter(actualType.TypeParameterList, typeName);
            }
            return typeName;
        }

        private static string GetDelegateTypeName(DelegateDeclarationSyntax type)
        {
            string typeName = type.Identifier.ValueText;
            return GetTypeNameWithTypeParameter(type.TypeParameterList, typeName);
        }

        private static string GetTypeNameWithTypeParameter(TypeParameterListSyntax typeParameterList, string identifier)
        {
            return typeParameterList != null ? identifier + "`" + typeParameterList.Parameters.Count : identifier;
        }

        private static string GetNamespaceName(NamespaceDeclarationSyntax namespaceSyntax)
        {
            return namespaceSyntax.Name.ToFullString().Trim();
        }

        private static IEnumerable<SyntaxTree> GetSourceTrees(IEnumerable<string> sourceFiles, IEnumerable<string> constants)
        {
            CSharpParseOptions options = new CSharpParseOptions().WithPreprocessorSymbols(constants);
            List<SyntaxTree> result = new List<SyntaxTree>();
            foreach (string sourceFile in sourceFiles)
            {
                if (string.IsNullOrEmpty(sourceFile))
                {
                    continue;
                }
                if (!File.Exists(sourceFile))
                {
                    throw new FileNotFoundException($"File {sourceFile} was not found.");
                }
                string rawText = File.ReadAllText(sourceFile);
                SyntaxTree tree = CSharpSyntaxTree.ParseText(rawText, options);
                result.Add(tree);
            }
            return result;
        }

        private static bool HasPublicModifier(BaseTypeDeclarationSyntax token)
        {
            if (token.Parent == null || token.Parent.GetType() == typeof(NamespaceDeclarationSyntax))
                return HasPublicModifier(token.Modifiers);

            return HasPublicModifier(token.Modifiers) && HasPublicModifier((BaseTypeDeclarationSyntax)(token.Parent));
        }

        private static bool HasPublicModifier(DelegateDeclarationSyntax token)
        {
            if (token.Parent == null || token.Parent.GetType() == typeof(NamespaceDeclarationSyntax))
                return HasPublicModifier(token.Modifiers);

            return HasPublicModifier(token.Modifiers) && HasPublicModifier((BaseTypeDeclarationSyntax)(token.Parent));
        }

        private static bool HasPublicModifier(SyntaxTokenList modifiers)
        {
            foreach (SyntaxToken modifier in modifiers)
            {
                if (modifier.Text == "public")
                {
                    return true;
                }
            }
            return false;
        }

        public static string AddTypeForwardToStringBuilder(string typeName, string alias = "")
        {
            if (!string.IsNullOrEmpty(alias))
                alias += "::";

            return "[assembly: System.Runtime.CompilerServices.TypeForwardedTo(typeof(" + alias + TransformGenericTypes(typeName.Substring(2)) + "))]\n";
        }

        private static string TransformGenericTypes(string typeName)
        {
            if (!typeName.Contains('`'))
                return typeName;

            StringBuilder sb = new StringBuilder();
            string[] stringParts = typeName.Split('`');
            sb.Append(stringParts[0]);

            for (int i = 0; i < stringParts.Length - 1; i++)
            {
                if (i != 0)
                {
                    sb.Append(stringParts[i].Substring(1));
                }

                int numberOfGenericParameters = int.Parse(stringParts[i + 1][0].ToString());

                sb.Append("<");
                sb.Append(',', numberOfGenericParameters - 1);
                sb.Append('>');
            }

            sb.Append(stringParts[stringParts.Length - 1].Substring(1));
            return sb.ToString();
        }
    }
}
