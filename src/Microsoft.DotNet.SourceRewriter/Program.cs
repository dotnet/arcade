// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using McMaster.Extensions.CommandLineUtils;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.DotNet.SourceRewriter.Rewriters;
using System.Collections.Generic;
using System.IO;

namespace Microsoft.DotNet.SourceRewriter
{
    public class Program
    {
        public static int Main(string[] args)
        {
            var app = new CommandLineApplication
            {
                Name = "SourceRewriter",
                FullName = "A command line tool that will take in a list of sources and apply some modifications to them.",
                ResponseFileHandling = ResponseFileHandling.ParseArgsAsSpaceSeparated
            };

            app.HelpOption("-?|-h|--help");

            CommandArgument sourceFiles = app.Argument("sourceFiles", "Semi-colon separated paths of all the sourceFiles to be rewritten.");
            sourceFiles.IsRequired();
            CommandArgument outputFolder = app.Argument("outputFolder", "Path to the output folder where the rewritten sources will be saved.");
            outputFolder.IsRequired();

            app.OnExecute(() =>
            {
                RewriteFiles(sourceFiles.Value.Split(';'), outputFolder.Value.TrimEnd('/', '\\'));
            });

            return app.Execute(args);
        }

        private static void RewriteFiles(IEnumerable<string> sourceFiles, string outputFolder)
        {
            if (!Directory.Exists(outputFolder))
            {
                Directory.CreateDirectory(outputFolder);
            }
            IEnumerable<SyntaxTree> sourceTrees = GetSourceTrees(sourceFiles, outputFolder);
            foreach (SyntaxTree sourceTree in sourceTrees)
            {
                PublicTypesToInternalRewriter rewriter = new PublicTypesToInternalRewriter();
                SyntaxNode newRootNode = rewriter.Visit(sourceTree.GetRoot());
                File.WriteAllText(sourceTree.FilePath, newRootNode.ToFullString());
            }
        }

        private static IEnumerable<SyntaxTree> GetSourceTrees(IEnumerable<string> sourceFiles, string outputFolder)
        {
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
                SyntaxTree tree = CSharpSyntaxTree.ParseText(rawText).WithFilePath(Path.Combine(outputFolder, Path.GetFileName(sourceFile)));
                result.Add(tree);
            }
            return result;
        }
    }
}
