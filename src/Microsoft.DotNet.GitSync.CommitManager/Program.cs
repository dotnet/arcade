// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using CommandLine;
using Microsoft.WindowsAzure.Storage.Table;

namespace Microsoft.DotNet.GitSync.CommitManager
{
    public class Program
    {
        private const string _cloudTableName = "CommitHistory";
        private static Table s_table { get; set; }
        private static Dictionary<string, List<string>> s_repos { get; set; } = new Dictionary<string, List<string>>();

        public static void Main(string[] args)
        {
            var myOptions = new CommandLineOptions();
            Parser.Default.ParseArguments<CommandLineOptions>(args).WithParsed(opts => myOptions = opts);
            
            if (!IsMirrorCommit(myOptions.Message, myOptions.Author))
            {
                s_table = new Table(myOptions.Username, myOptions.Key, _cloudTableName);
                Setup();
                InsertCommits(myOptions.Repository, myOptions.Commit, myOptions.Branch);
            }
        }

        private static void Setup()
        {
            s_repos.Add("corefx", new List<string> { "coreclr", "corefx" });
            s_repos.Add("coreclr", new List<string> { "corefx", "corert" });
            s_repos.Add("corert", new List<string> { "coreclr", "corert" });

            s_table.CTable.CreateIfNotExistsAsync().Wait();
        }

        private static bool IsMirrorCommit(string message, string author) => message.Contains($"Signed-off-by: {author} <{author}@microsoft.com>");

        private static void InsertCommits(string sourceRepo, string commitId, string branch)
        {
            foreach (string repo in s_repos[sourceRepo])
            {
                CommitEntity entry = new CommitEntity(sourceRepo, repo, commitId, branch);
                TableOperation insertOperation = TableOperation.Insert(entry);
                s_table.CTable.ExecuteAsync(insertOperation).Wait();
                Console.WriteLine($"Commit {commitId} added to table to get mirrored from {sourceRepo} to {repo}");
            }
        }
    }
}
