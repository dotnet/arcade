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
            if (SourceFiles == null || SourceFiles.Length == 0)
                Log.LogError("There are no ref source files.");

            GenerateNotSupportedAssemblyFiles(SourceFiles?.Select(item => item.ItemSpec).ToList(), IntermediateOutputPath, Message, ApiExclusionListPath);
            return !Log.HasLoggedErrors;
        }

        public void GenerateNotSupportedAssemblyFiles(IEnumerable<string> sourceFiles, string intermediatePath, string message, string apiExclusionListPath)
        {
            foreach (string sourceFile in sourceFiles)
            {
                if (string.IsNullOrEmpty(sourceFile))
                {
                    continue;
                }
                if (!File.Exists(sourceFile))
                {
                    Log.LogError($"File {sourceFile} was not found.");
                }
                string text = GenerateNotSupportedAssemblyForSourceFile(sourceFile, message, apiExclusionListPath);
                string path = Path.Combine(intermediatePath, Path.GetFileNameWithoutExtension(sourceFile) + ".notSupported.cs");
                File.WriteAllText(path, text);
            }
        }

        public static string GenerateNotSupportedAssemblyForSourceFile(string sourceFile, string Message, string apiExclusionListPath)
        {
            string[] apiExclusions;
            SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(File.ReadAllText(sourceFile));
            if (string.IsNullOrEmpty(apiExclusionListPath) || !File.Exists(apiExclusionListPath))
            {
                apiExclusions = null;
            }
            else
            {
                apiExclusions = File.ReadAllLines(apiExclusionListPath);
            }
            var rewriter = new NotSupportedAssemblyRewriter(Message, apiExclusions);
            SyntaxNode root = rewriter.Visit(syntaxTree.GetRoot());
            return root.GetText().ToString();
        }
    }

    internal class NotSupportedAssemblyRewriter : CSharpSyntaxRewriter
    {
        private string _message;
        private List<string> _exclusionApis;

        public NotSupportedAssemblyRewriter(string Message, string[] ExclusionApis)
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

            return node.WithBody(block);
        }

        public override SyntaxNode VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
        {
            string message = "{ throw new System.PlatformNotSupportedException(" + $"{ _message }); "+ " }\n";        
            BlockSyntax block = (BlockSyntax)SyntaxFactory.ParseStatement(message);

            return node.WithBody(block);
        }

        public override SyntaxNode VisitAccessorDeclaration(AccessorDeclarationSyntax node)
        {
            if (node.Keyword.Text == "set" || node.Body == null)
                return node;

            string message = "{ throw new System.PlatformNotSupportedException(" + $"{ _message }); "+ " } ";       
            BlockSyntax block = (BlockSyntax)SyntaxFactory.ParseStatement(message);

            return node.WithBody(block);
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
