// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using CommandLine;
using log4net;
using log4net.Config;
using Microsoft.WindowsAzure.Storage.Table;

namespace Microsoft.DotNet.GitSync.CommitManager
{
    public class Program
    {
        private const string _cloudTableName = "CommitHistory";
        private const string _repoTableName = "MirrorBranchRepos";
        private static Table s_table { get; set; }
        private static Dictionary<(string, string), List<string>> s_repos { get; set; } = new Dictionary<(string, string), List<string>>();
        private static ILog s_logger = LogManager.GetLogger(typeof(Program));

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
            var logRepository = LogManager.GetRepository(Assembly.GetEntryAssembly());
            XmlConfigurator.Configure(logRepository, new FileInfo("log4net.config"));

            s_table = new Table(username, key, _cloudTableName, _repoTableName);
            await s_table.CommitTable.CreateIfNotExistsAsync();
            await s_table.RepoTable.CreateIfNotExistsAsync();

            TableQuery getAllMirrorPairs = new TableQuery()
                .Where(TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.NotEqual, null));

            TableContinuationToken token = null;
            do
            {
                var segmentedResults = await s_table.RepoTable.ExecuteQuerySegmentedAsync(getAllMirrorPairs, token);
                token = segmentedResults.ContinuationToken;
                var tableRows = segmentedResults.Results;

                foreach (var item in tableRows)
                {
                    s_repos.Add((item.PartitionKey, item.Properties["Branch"].StringValue), item.Properties["ReposToMirrorInto"].StringValue.Split(';').ToList());
                    s_logger.Info($"The commits in  {item.PartitionKey} repo will be mirrored into {item.Properties["ReposToMirrorInto"].StringValue} Repos");
                }
            } while (token != null);
        }

        private static async Task InsertCommitsAsync(string sourceRepoFullname, string commitList, string branch)
        {
            string sourceRepo = sourceRepoFullname.Split("/")[1];
            foreach (string repo in s_repos[(sourceRepo, branch)])
            {
                foreach (var commitId in commitList.Split(";"))
                {
                    CommitEntity entry = new CommitEntity(sourceRepo, repo, commitId, branch);
                    TableOperation insertOperation = TableOperation.Insert(entry);

                    try
                    {
                        await s_table.CommitTable.ExecuteAsync(insertOperation);
                        s_logger.Info($"Commit {commitId} added to table to get mirrored from {sourceRepo} to {repo}");
                    }
                    catch (WindowsAzure.Storage.StorageException)
                    {
                        s_logger.Warn($"The commit {commitId} already exists in {repo}");
                    }
                    catch (Exception ex)
                    {
                        s_logger.Warn($"Insert Operation for commit {commitId} for {repo}\n" + ex.Message);
                    }
                }
            }
        }
    }
}
