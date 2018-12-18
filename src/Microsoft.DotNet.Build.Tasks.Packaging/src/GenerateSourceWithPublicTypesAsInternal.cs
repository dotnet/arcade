// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.DotNet.Build.Tasks.Packaging
{
    public class GenerateSourceWithPublicTypesAsInternal : BuildTask
    {
        [Required]
        public ITaskItem[] Files
        {
            get;
            set;
        }

        public override bool Execute()
        {
            Dictionary<string, string> sourceFiles = new Dictionary<string, string>(StringComparer.InvariantCulture);
            if (Files.Count() == 0)
            {
                Log.LogError("The Files Item cannot be empty.");
                return false;
            }
            foreach (ITaskItem file in Files)
            {
                string sourcePath = file.GetMetadata("SourcePath");
                string destinationPath = file.GetMetadata("DestinationPath");
                if (string.IsNullOrEmpty(sourcePath) || string.IsNullOrEmpty(destinationPath))
                {
                    Log.LogError($"Item {file.GetMetadata("Identity")} doesn't define SourcePath or DestinationPath metadata.");
                    return false;
                }
                sourceFiles.Add(sourcePath, destinationPath);
            }

            IEnumerable<SyntaxTree> sourceTrees = GetSourceTrees(sourceFiles);
            foreach (SyntaxTree sourceTree in sourceTrees)
            {
                PublicTypesToInternalRewriter rewriter = new PublicTypesToInternalRewriter();
                SyntaxNode newRootNode = rewriter.Visit(sourceTree.GetRoot());
                if (!Directory.Exists(Path.GetDirectoryName(sourceTree.FilePath)))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(sourceTree.FilePath));
                }
                File.WriteAllText(sourceTree.FilePath, newRootNode.ToFullString());
            }

            return !Log.HasLoggedErrors;
        }

        private IEnumerable<SyntaxTree> GetSourceTrees(Dictionary<string, string> sourceFiles)
        {
            List<SyntaxTree> result = new List<SyntaxTree>();
            foreach (KeyValuePair<string, string> sourceFile in sourceFiles)
            {
                string rawText = File.ReadAllText(sourceFile.Key);
                SyntaxTree tree = CSharpSyntaxTree.ParseText(rawText).WithFilePath(sourceFile.Value);
                result.Add(tree);
            }
            return result;
        }
    }
}
