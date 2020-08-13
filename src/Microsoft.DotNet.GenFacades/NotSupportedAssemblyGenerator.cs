// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.DotNet.Build.Tasks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.DotNet.GenFacades
{
    /// <summary>
    /// The class generates an NotSupportedAssembly from the reference sources.
    /// </summary>
    public class NotSupportedAssemblyGenerator : BuildTask
    {
        [Required]
        public ITaskItem[] SourceFiles { get; set; }

        [Required]
        public string Message { get; set; }

        public string ApiExclusionListPath { get; set; }

        public override bool Execute()
        {
            if (SourceFiles == null || SourceFiles.Length == 0)
            {
                Log.LogError("There are no ref source files.");
            }
            else
            {
                GenerateNotSupportedAssemblyFiles(SourceFiles);
            }
            return !Log.HasLoggedErrors;
        }

        private void GenerateNotSupportedAssemblyFiles(IEnumerable<ITaskItem> sourceFiles)
        {
            foreach (ITaskItem item in sourceFiles)
            {
                string sourceFile = item.ItemSpec;
                if (string.IsNullOrEmpty(sourceFile))
                {
                    continue;
                }
                if (!File.Exists(sourceFile))
                {
                    Log.LogError($"File {sourceFile} was not found.");
                    continue;
                }
                string text = GenerateNotSupportedAssemblyForSourceFile(sourceFile);

                if(text != null)
                    File.WriteAllText(item.GetMetadata("NotSupportedPath"), text);
            }
        }

        private string GenerateNotSupportedAssemblyForSourceFile(string sourceFile)
        {
            string[] apiExclusions;
            SyntaxTree syntaxTree;
            try
            {
                syntaxTree = CSharpSyntaxTree.ParseText(File.ReadAllText(sourceFile));
            }
            catch(Exception ex)
            {
                Log.LogError(ex.Message);
                return null;
            }

            if (string.IsNullOrEmpty(ApiExclusionListPath) || !File.Exists(ApiExclusionListPath))
            {
                apiExclusions = null;
            }
            else
            {
                apiExclusions = File.ReadAllLines(ApiExclusionListPath);
            }
            var rewriter = new NotSupportedAssemblyRewriter(Message, apiExclusions);
            SyntaxNode root = rewriter.Visit(syntaxTree.GetRoot());
            return root.GetText().ToString();
        }
    }

    internal class NotSupportedAssemblyRewriter : CSharpSyntaxRewriter
    {
        private string _message;
        private string[] _exclusionApis;

        public NotSupportedAssemblyRewriter(string message, string[] exclusionApis)
        {
            _message = message;
            _exclusionApis = exclusionApis;
        }

        public override SyntaxNode VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            if (node.Body == null)
                return node;

            if (_exclusionApis != null && _exclusionApis.Contains(GetMethodDefinition(node)))
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
            if (node.Body == null)
                return node;

            string message = "{ throw new System.PlatformNotSupportedException(" + $"{ _message }); "+ " } ";       
            BlockSyntax block = (BlockSyntax)SyntaxFactory.ParseStatement(message);

            return node.WithBody(block);
        }

        private string GetFullyQualifiedName(TypeDeclarationSyntax node)
        {
            string parent;
            if (node.Parent is NamespaceDeclarationSyntax parentNamespace)
            {
                parent = GetFullyQualifiedName(parentNamespace);
            }
            else
            {
                parent = GetFullyQualifiedName((TypeDeclarationSyntax)node.Parent);
            }

            return parent + "." + node.Identifier.ValueText.Trim();
        }

        private string GetFullyQualifiedName(NamespaceDeclarationSyntax node) => node.Name.ToFullString().Trim();

        private string GetMethodDefinition(MethodDeclarationSyntax node) => GetFullyQualifiedName((TypeDeclarationSyntax)node.Parent) + "." + node.Identifier.ValueText;
    }
}
