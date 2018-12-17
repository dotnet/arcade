// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.DotNet.Build.Tasks.Packaging
{
    public class GenerateSourceWithPublicTypesAsInternal : BuildTask
    {
        /// <summary>
        /// Collection that will hold the map of the paths to the original files, and the path to the generated ones which will be rewritten.
        /// </summary>
        private Dictionary<string, string> _sourceFiles;

        [Required]
        public ITaskItem[] Files
        {
            get;
            set;
        }

        public override bool Execute()
        {
            _sourceFiles = new Dictionary<string, string>();
            foreach (ITaskItem file in Files)
            {
                string sourcePath = file.GetMetadata("SourcePath");
                string destinationPath = file.GetMetadata("DestinationPath");
                if (string.IsNullOrEmpty(sourcePath) || string.IsNullOrEmpty(destinationPath))
                {
                    Log.LogError($"Item {file.GetMetadata("Identity")} doesn't define SourcePath or DestinationPath metdata.");
                }
                _sourceFiles.Add(sourcePath, destinationPath);
            }

            var sourceTrees = GetSourceTrees();
            foreach (SyntaxTree sourceTree in sourceTrees)
            {
                PublicTypesToInternalRewriter rewriter = new PublicTypesToInternalRewriter();
                SyntaxNode newRootNode = rewriter.Visit(sourceTree.GetRoot());

                File.WriteAllText(sourceTree.FilePath, newRootNode.ToFullString());
            }

            return !Log.HasLoggedErrors;
        }

        private IEnumerable<SyntaxTree> GetSourceTrees()
        {
            List<SyntaxTree> result = new List<SyntaxTree>();
            foreach (var sourceFile in _sourceFiles)
            {
                string rawText = File.ReadAllText(sourceFile.Key);
                SyntaxTree tree = CSharpSyntaxTree.ParseText(rawText).WithFilePath(sourceFile.Value);
                result.Add(tree);
            }
            return result;
        }
    }
}
