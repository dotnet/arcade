// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.DotNet.Build.Tasks;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.DotNet.GenFacades
{
    public class NotSupportedAssemblyGenerator : BuildTask
    {
        [Required]
        public ITaskItem[] SourceFiles { get; set; }

        [Required]
        public string IntermediateOutputPath { get; set; }

        [Required]
        public string Message { get; set; }

        public string ApiExclusionListPath { get; set; }

        public override bool Execute()
        {
           GenerateNotSupportedAssemblyFiles(SourceFiles?.Select(item => item.ItemSpec).ToList(), IntermediateOutputPath, Message, ApiExclusionListPath);
           return !Log.HasLoggedErrors;
        }

        public static void GenerateNotSupportedAssemblyFiles(IEnumerable<string> sourceFiles, string intermediatePath, string message, string apiExclusionListPath)
        {
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
                string text = GenerateNotSupportedAssemblyForSourceFile(sourceFile, message, apiExclusionListPath);
                string path = intermediatePath + Path.GetFileNameWithoutExtension(sourceFile) + ".notSupported.cs";
                File.WriteAllText(path, text);
            }
        }

        public static string GenerateNotSupportedAssemblyForSourceFile(string sourceFile, string Message, string apiExclusionListPath)
        {
            string[] apiExclusions;
            SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(File.ReadAllText(sourceFile));
            if (apiExclusionListPath == null)
            {
                apiExclusions = null;
            }
            else
            {
                apiExclusions = File.ReadAllLines(Path.GetFullPath(apiExclusionListPath));
            }
            var rewriter = new NotSupportedAssmblyRewriter(Message, apiExclusions);
            SyntaxNode root = rewriter.Visit(syntaxTree.GetRoot());
            return root.GetText().ToString();
        }
    }

    internal class NotSupportedAssmblyRewriter : CSharpSyntaxRewriter
    {
        private string _message;
        private List<string> _exclusionApis;

        public NotSupportedAssmblyRewriter(string Message, string[] ExclusionApis)
        {
            _message = Message;
            _exclusionApis = ExclusionApis?.ToList();
        }

        public override SyntaxNode VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            if (node.Body == null)
                return node;

            if (_exclusionApis != null && _exclusionApis.Contains(GetMethodDefination(node)))
                return null;

            string message = "{ throw new System.PlatformNotSupportedException(" + $"{ _message }); "+ " }\n";  
            BlockSyntax block = (BlockSyntax)SyntaxFactory.ParseStatement(message);

            return node.Update(node.AttributeLists,
                node.Modifiers,
                node.ReturnType,
                node.ExplicitInterfaceSpecifier,
                node.Identifier,
                node.TypeParameterList,
                node.ParameterList,
                node.ConstraintClauses,
                block,
                node.SemicolonToken);
        }

        public override SyntaxNode VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
        {
            string message = "{ throw new System.PlatformNotSupportedException(" + $"{ _message }); "+ " }\n";        
            BlockSyntax block = (BlockSyntax)SyntaxFactory.ParseStatement(message);

            return node.Update(node.AttributeLists,
                node.Modifiers,
                node.Identifier,
                node.ParameterList,
                node.Initializer,
                block,
                node.SemicolonToken);
        }

        public override SyntaxNode VisitAccessorDeclaration(AccessorDeclarationSyntax node)
        {
            if (node.Keyword.Text == "set" || node.Body == null)
                return node;

            string message = "{ throw new System.PlatformNotSupportedException(" + $"{ _message }); "+ " } ";       
            BlockSyntax block = (BlockSyntax)SyntaxFactory.ParseStatement(message);

            return node.Update(node.AttributeLists,
                node.Modifiers,
                node.Keyword,
                block,
                node.SemicolonToken);
        }

        private string GetFullyQualifiedName(TypeDeclarationSyntax node)
        {
            string parent;
            if (node.Parent is NamespaceDeclarationSyntax)
            {
                parent = GetFullyQualifiedName((NamespaceDeclarationSyntax)node.Parent);
            }
            else
            {
                parent = GetFullyQualifiedName((TypeDeclarationSyntax)node.Parent);
            }

            return parent + "." + node.Identifier.ValueText.Trim();
        }

        private string GetFullyQualifiedName(NamespaceDeclarationSyntax node) => node.Name.ToFullString().Trim();

        private string GetMethodDefination(MethodDeclarationSyntax node) => GetFullyQualifiedName((TypeDeclarationSyntax)node.Parent) + "." + node.Identifier.ValueText;
    }
}
