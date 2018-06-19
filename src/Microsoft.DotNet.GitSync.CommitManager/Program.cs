// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CommandLine;
using Microsoft.WindowsAzure.Storage.Table;
using NLog;
using NLog.Config;
using NLog.Targets;

namespace Microsoft.DotNet.GitSync.CommitManager
{
    public class Program
    {
        private const string _cloudTableName = "CommitHistory";
        private static Table s_table { get; set; }
        private static Dictionary<string, List<string>> s_repos { get; set; } = new Dictionary<string, List<string>>();
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public static async Task Main(string[] args)
        {
            CommandLineOptions myOptions = null;
            Parser.Default.ParseArguments<CommandLineOptions>(args).WithParsed(opts => myOptions = opts);

            if (myOptions != null)
            {
                await SetupAsync(myOptions.Username, myOptions.Key);
                await InsertCommitsAsync(myOptions.Repository, myOptions.Commit, myOptions.Branch);
            }
        }

        private static async Task SetupAsync(string username, string key)
        {
            LoggingConfiguration config = new LoggingConfiguration();
            ConsoleTarget consoleTarget = new ConsoleTarget();
            consoleTarget.Layout = @"${date:format=HH\:mm\:ss}  ${level:uppercase=true}  ${message} ${exception:format=tostring}";
            config.AddRule(LogLevel.Info, LogLevel.Fatal, consoleTarget);
            LogManager.Configuration = config;

            s_repos.Add("corefx", new List<string> { "coreclr", "corert" });
            s_repos.Add("coreclr", new List<string> { "corefx", "corert" });
            s_repos.Add("corert", new List<string> { "coreclr", "corefx" });

            s_table = new Table(username, key, _cloudTableName);
            await s_table.CommitTable.CreateIfNotExistsAsync();
        }

        private static async Task InsertCommitsAsync(string sourceRepoFullname, string commitList, string branch)
        {
            string sourceRepo = sourceRepoFullname.Split("/")[1];
            foreach (string repo in s_repos[sourceRepo])
            {
                foreach (var commitId in commitList.Split(";"))
                {
                    CommitEntity entry = new CommitEntity(sourceRepo, repo, commitId, branch);
                    TableOperation insertOperation = TableOperation.Insert(entry);

                    try
                    {
                        await s_table.CommitTable.ExecuteAsync(insertOperation);
                        logger.Info($"Commit {commitId} added to table to get mirrored from {sourceRepo} to {repo}");
                    }
                    catch (WindowsAzure.Storage.StorageException)
                    {
                        logger.Warn($"The commit {commitId} already exists in {repo}");
                    }
                    catch (Exception ex)
                    {
                        logger.Warn($"Insert Operation for commit {commitId} for {repo}\n" + ex.Message);
                    }
                }
            }
        }
    }
}
