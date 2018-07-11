// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using LibGit2Sharp;
using log4net;
using log4net.Config;
using Octokit;
using Commit = LibGit2Sharp.Commit;
using Credentials = Octokit.Credentials;
using Repository = LibGit2Sharp.Repository;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;

namespace Microsoft.DotNet.GitSync
{
    internal class Program
    {
        private const string TableName = "CommitHistory";
        private static CloudTable s_table;
        private static Dictionary<string, List<string>> s_repos { get; set; } = new Dictionary<string, List<string>>();
        private ConfigFile ConfigFile { get; }
        private static Lazy<GitHubClient> _lazyClient;
        private static EmailManager s_emailManager;
        private static GitHubClient Client => _lazyClient.Value;
        private static string s_mirrorSignatureUserName;
        private static readonly ILog s_logger = LogManager.GetLogger(typeof(Program));

        private Program(string[] args)
        {
            var dbFile = args.Length >= 1 ? args[0] : "settings.json";
            dbFile = Path.GetFullPath(dbFile);
            ConfigFile = new ConfigFile(dbFile, s_logger);
            XmlConfigurator.Configure();
        }

        private void Run()
        {
            var config = ConfigFile.Get();
            if (config == null)
            {
                s_logger.Error("Config File does not exist, Configuring.");
                Configure(ConfigFile);
                config = ConfigFile.Get();
            }
            Setup(config.ConnectionString, config.Server, config.Destinations);

            s_mirrorSignatureUserName = config.MirrorSignatureUser;
            _lazyClient =
                new Lazy<GitHubClient>(
                    () => new GitHubClient(new ProductHeaderValue("DotNetGitHubMirrorService", "1.0"))
                    {
                        Credentials = new Credentials(config.UserName, config.Password)
                    });

            EnsureRepository(config.Repos);

            var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (s, e) => cts.Cancel();

            while (true)
            {
                try
                {
                    Step(cts.Token);
                    s_logger.Info("Waiting");
                    Task.Delay(new TimeSpan(0, 5, 0), cts.Token).Wait();
                }

                catch (RateLimitExceededException ex)
                {
                    s_logger.Error(ex.Message);
                    s_logger.Info("Restarting Mirror after 30 minutes");
                    s_emailManager.Email("RateLimitExceeded Exception", string.Empty);
                    Task.Delay(new TimeSpan(0, 30, 0), cts.Token).Wait();
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Error($"fatal: {ex}");
                }
            }
        }

        private static void Main(string[] args)
        {
            new Program(args).Run();
        }

        private void Step(CancellationToken token)
        {
            Task.Run(() => ProcessAsync(token)).GetAwaiter().GetResult();
        }

        private async Task ProcessAsync(CancellationToken token)
        {
            var config = ConfigFile.Get();

            if (token.IsCancellationRequested)
            {
                Environment.Exit(0);
            }

            if (token.IsCancellationRequested)
            {
                Environment.Exit(0);
            }

            foreach (string prBranch in config.Branches)
            {
                UpdateRepository(config.Repos, prBranch);

                foreach (RepositoryInfo repo in config.Repos)
                {
                    SanityCheck(repo, prBranch);
                    if (!await WaitForPendingPRAsync(repo, prBranch))
                    {
                        continue;
                    }

                    if (token.IsCancellationRequested)
                    {
                        Environment.Exit(0);
                    }

                    var newChanges = GetChangesToApply(repo, prBranch);
                    if (newChanges != null)
                    {
                        var newBranch = CreateBranchForNewChanges(newChanges, prBranch);
                        if (newBranch != null)
                        {
                            await SubmitPRForNewChangesAsync(newChanges, newBranch, prBranch);
                        }
                        else
                        {
                            UpdateEntities(GetCommitsToMirror(repo, prBranch), "Commits are already Mirrored");
                            s_logger.Info($"Commit Entries modififed to show mirrored in the azure table");
                        }
                    }

                    if (token.IsCancellationRequested)
                    {
                        Environment.Exit(0);
                    }
                }
            }
        }

        private string CreateBranchForNewChanges(NewChanges newChanges, string prBranch)
        {
            string OriginalSha;
            var targetRepo = newChanges.TargetRepository;
            var branchName =
                $"mirror-merge-{(long)DateTime.Now.Subtract(new DateTime(2000, 1, 1, 0, 0, 0)).TotalMinutes}";
            s_logger.Info($"Creating branch {prBranch} in {targetRepo} to merge changes into {prBranch}");
            using (var repo = new Repository(targetRepo.Path))
            {
                var branch = repo.CreateBranch(branchName);
                s_logger.Info("Checking out PR branch");
                Commands.Checkout(repo, branch);
                OriginalSha = branch.Tip.ToString();
            }

            foreach (var source in newChanges.changes.Keys)
            {
                var sourceRepository = targetRepo.Configuration.Repos.Where(t => t.Name == source).First();
                using (var repo = new Repository(sourceRepository.Path))
                {
                    foreach (var change in newChanges.changes[sourceRepository.Name])
                    {
                        var commit = repo.Lookup<Commit>(change);
                        if (!IsMirrorCommit(commit.Message, targetRepo.Configuration.MirrorSignatureUser)
                            && commit.Parents.Count() == 1
                            )
                        {
                            s_logger.Info($"Applying {change}");
                            var patch = FormatPatch(sourceRepository, change);
                            if (string.IsNullOrWhiteSpace(patch))
                            {
                                continue;
                            }
                            s_logger.Debug($"Patch:\n{patch}");
                            ApplyPatch(sourceRepository, newChanges.TargetRepository, patch, commit);
                        }
                    }
                }
            }
            using (var repo = new Repository(targetRepo.Path))
            {
                if (repo.Head.Tip.ToString() == OriginalSha)
                {
                    s_logger.Info($"No new commits To add into this branch");
                    return null;
                }
            }

            return branchName;
        }

        private static bool IsMirrorCommit(string message, string author) => message.Contains($"Signed-off-by: {author} <{author}@microsoft.com>");
        private static string FormatPatch(RepositoryInfo sourceRepository, string sha)
        {
            var result = Runner.RunCommand("git",
                $"-C \"{sourceRepository.Path}\" show -p -m --first-parent --format=email {sha} -- \"{sourceRepository.SharedPath}\"", s_logger);
            return string.Join("\n", result.Output.Split(new[] { '\r', '\n' }, StringSplitOptions.None).Select(l => FixupPRReference(sourceRepository, l)));
        }

        private static string FixupPRReference(RepositoryInfo sourceRepository, string line)
        {
            var match = Regex.Match(line, @"Subject: \[PATCH\].*\(#[0-9]+\)$");
            if (match.Success)
            {
                return Regex.Replace(line, @"\(#([0-9]+)\)$", $"({sourceRepository.Configuration.UpstreamOwner}/{sourceRepository.Name}#$1)");
            }
            match = Regex.Match(line, @"Subject: \[PATCH\] Merge pull request #[0-9]+ from");
            if (match.Success)
            {
                return Regex.Replace(line, "#([0-9]+)", $"{sourceRepository.Configuration.UpstreamOwner}/{sourceRepository.Name}#$1");
            }
            return line;
        }

        private static void ApplyPatch(RepositoryInfo sourceRepository, RepositoryInfo targetRepository, string patch, Commit commit)
        {
            var sourceSlashIgnore = 1 + sourceRepository.SharedPath.Count(c => c == '\\') + 1;
            var result = Runner.RunCommand("git",
                $"-c \"user.name={s_mirrorSignatureUserName}\" -C \"{targetRepository.Path}\" am --signoff -p{sourceSlashIgnore} --directory=\"{targetRepository.SharedPath.Replace('\\', '/')}\"",
                s_logger, patch);
            s_logger.Debug(result.Output);
            if (result.ExitCode != 0)
            {
                s_logger.Error($"The commit being applied is ${commit.Sha} ${commit.MessageShort} {commit.Author}");
                s_logger.Error(
                    $"patching failed, please open '{targetRepository.Path}' and resolve the conflicts then press any key");
                s_emailManager.Email("Merge Conflicts", $"Merge Conflicts in {targetRepository.Name} while applying commit {commit} from repo {sourceRepository.Name}");
                Console.ReadKey();
            }
        }

        private async Task SubmitPRForNewChangesAsync(NewChanges newChanges, string branch, string prBranch)
        {
            using (var repository = new Repository(newChanges.TargetRepository.Path))
            {
                s_logger.Debug($"Pushing {branch} to {newChanges.TargetRepository} to update {prBranch}");
                var origin = repository.Network.Remotes["origin"];
                repository.Network.Push(origin, new[] { "refs/heads/" + branch }, new PushOptions
                {
                    CredentialsProvider = (url, usernameFromUrl, types) =>
                        new UsernamePasswordCredentials
                        {
                            Username = newChanges.TargetRepository.Configuration.UserName,
                            Password = newChanges.TargetRepository.Configuration.Password,
                        }
                });
            }
            var targetRepo = newChanges.TargetRepository;
            var newPr = new NewPullRequest($"Mirror changes from { targetRepo.Configuration.UpstreamOwner }/{string.Join(",", newChanges.changes.Keys)}", $"{ targetRepo.Owner}:{branch}", prBranch)
            {
                Body = $"This PR contains mirrored changes from { targetRepo.Configuration.UpstreamOwner }/{string.Join(",", newChanges.changes.Keys)}\n\n\n**Please REBASE this PR when merging**"
            };
            s_logger.Debug($"Creating pull request in {newChanges.TargetRepository.Configuration.UpstreamOwner}");
            var pr = await Client.PullRequest.Create(targetRepo.Configuration.UpstreamOwner, targetRepo.Name, newPr);
            s_logger.Debug($"Adding the commits");
            var commits = await Client.Repository.PullRequest.Commits(targetRepo.Configuration.UpstreamOwner, targetRepo.Name, pr.Number);
            s_logger.Debug($"Getting Assignees");
            var additionalAssignees = await Task.WhenAll(commits.Select(c => GetAuthorAsync(targetRepo, c.Sha)));
            try
            {
                var update = new PullRequestUpdate() { Body = pr.Body + "\n\n cc " + string.Join(" ", additionalAssignees.Select(a => "@" + a)) };
                await Client.PullRequest.Update(targetRepo.Configuration.UpstreamOwner, targetRepo.Name, pr.Number, update);
            }
            catch (Exception)
            {
            }
            targetRepo.PendingPRs[prBranch] = new PullRequestInfo
            {
                Number = pr.Number,
            };
            s_logger.Info($"Pull request #{pr.Number} created for {prBranch}");
            UpdateEntities(GetCommitsToMirror(targetRepo, prBranch), pr.Url.ToString());
            s_logger.Info($"Commit Entries modififed to show mirrored in the azure table");
            ConfigFile.Save(targetRepo.Configuration);
        }

        public static void UpdateEntities(IEnumerable<DynamicTableEntity> commits, string pr)
        {
            foreach (var c in commits)
            {
                c.Properties["Mirrored"].BooleanValue = true;
                c.Properties["PR"].StringValue = pr;
                TableOperation insertOrReplaceOperation = TableOperation.InsertOrReplace(c);
                s_table.Execute(insertOrReplaceOperation);
            }
        }

        private NewChanges GetChangesToApply(RepositoryInfo targetRepo, string branch)
        {
            var listCommits = GetCommitsToMirror(targetRepo, branch);
            if (listCommits.Count() != 0)
            {
                s_logger.Info($"Commits to mirror for {targetRepo}/{branch}");
                var result = new NewChanges(targetRepo);
                foreach (var commit in listCommits)
                {
                    string key = commit.Properties["SourceRepo"].StringValue;
                    if (result.changes.ContainsKey(key))
                        result.changes[key].Add(commit.RowKey);
                    else
                        result.changes[key] = new List<string>() { commit.RowKey };
                }
                return result;
            }
            s_logger.Info($"No new commits to mirror for {targetRepo}/{branch}");
            return null;
        }

        private static async Task<string> GetAuthorAsync(RepositoryInfo repoInfo, string commitSha)
        {
            var commit = await Client.Repository.Commit.Get(repoInfo.Owner, repoInfo.Name, commitSha);

            if (commit.Author != null)
                return commit.Author.Login;

            var userSearchRequest = new SearchUsersRequest(commit.Commit.Author.Email)
            {
                In = new[] { UserInQualifier.Email }
            };

            var result = await Client.Search.SearchUsers(userSearchRequest);
            return result.Items.Count != 0 ? result.Items[0].Login : null;
        }

        private async Task<bool> WaitForPendingPRAsync(RepositoryInfo repo, string branch)
        {
            if (repo.PendingPRs[branch] == null) return true;

            var pendingPr = repo.PendingPRs[branch];
            var prNum = pendingPr.Number;

            var client = Client;
            var pr = await client.PullRequest.Get(repo.Configuration.UpstreamOwner, repo.Name, prNum);
            if (pr.State == ItemState.Open)
            {
                s_logger.Info($"{repo}/{branch} has pending pull request {prNum}");
                return false;
            }
            if (pr.State == ItemState.Closed && pr.Merged)
            {
                s_logger.Info($"{repo}/{branch} has merged pull request {prNum}");
                repo.PendingPRs[branch] = null;
                ConfigFile.Save(repo.Configuration);
                return true;
            }
            repo.PendingPRs[branch] = null;
            ConfigFile.Save(repo.Configuration);
            return true;
        }

        private void UpdateRepository(List<RepositoryInfo> repos, string branch)
        {
            foreach (var repo in repos)
            {
                s_logger.Debug($"Updating {repo}\\{branch} to latest version.");
                using (var repository = new Repository(repo.Path))
                {
                    s_logger.Info($"Fetching new changes for {repo}\\{branch} from upstream");
                    Commands.Fetch(repository, "upstream", new[] { $"{branch}:{branch}" }, new FetchOptions(), $"fetch {branch}");
                    s_logger.Info($"Checking out upstream  {repo}\\{branch}");
                    Commands.Checkout(repository, $"upstream/{branch}");
                    s_logger.Info($"Hard Reset to Head");
                    repository.Reset(ResetMode.Hard, "HEAD");
                }
            }
        }

        /// <summary>
        /// Ensure that the repository exists on disk and its origin remote points to the correct url
        /// </summary>
        /// <param name="repo"></param>
        private void EnsureRepository(List<RepositoryInfo> repos)
        {
            foreach (var repo in repos)
            {
                var repoPath = repo.Path;
                s_logger.Info($"Verifying repository {repo} at {repo.Path}");
                if (!Directory.Exists(repoPath) || !Repository.IsValid(repoPath))
                {
                    if (Directory.Exists(repoPath))
                    {
                        Directory.Delete(repoPath, true);
                    }

                    s_logger.Info($"Cloning the repo from {repo.CloneUrl}");
                    Repository.Clone(repo.CloneUrl, repoPath);
                }
                using (var repository = new Repository(repoPath))
                {
                    var remote = repository.Network.Remotes["origin"];
                    if (remote == null)
                    {
                        repository.Network.Remotes.Add("origin", repo.CloneUrl);
                    }
                    else if (remote.Url != repo.CloneUrl)
                    {
                        repository.Network.Remotes.Update("origin", u => u.Url = repo.CloneUrl);
                    }
                    var master = repository.Branches["master"] ?? repository.CreateBranch("master");
                    repository.Branches.Update(master, b => b.Remote = "origin", b => b.UpstreamBranch = "refs/heads/master");

                    remote = repository.Network.Remotes["upstream"];
                    if (remote == null)
                    {
                        repository.Network.Remotes.Add("upstream", @"https://github.com/" + repo.Configuration.UpstreamOwner + @"/" + repo.Name + ".git");
                    }
                }
            }
        }

        private static string Prompt(string prompt)
        {
            Console.Write(prompt);
            return Console.ReadLine();
        }

        internal static string GetPassword(string prompt)
        {
            Console.Write(prompt);
            var sb = new StringBuilder();
            while (true)
            {
                var key = Console.ReadKey(true);
                if (key.Key == ConsoleKey.Enter)
                {
                    Console.WriteLine();
                    return sb.ToString();
                }
                if (key.Key == ConsoleKey.Backspace)
                {
                    sb.Length = sb.Length > 0 ? sb.Length - 1 : 0;
                    continue;
                }
                sb.Append(key.KeyChar);
            }
        }

        private static string Inquire(string question, params string[] options)
        {
            string answer;
            do
            {
                Console.Write($"{question} ({string.Join("/", options)})");
                answer = Console.ReadLine()?.Trim();
            } while (!options.Contains(answer));
            return answer;
        }

        private void Configure(ConfigFile configFile)
        {
            string userName = Prompt("Enter github username:");
            string password = GetPassword("Enter password:");
            string firstRepoOwner = Prompt("Enter the first repository owner:");
            string firstRepoName = Prompt("Enter the first repository name:");
            string firstRepoSharedPath = Prompt("Enter shared path:");
            string secondRepoOwner = Prompt("Enter the second repository owner:");
            string secondRepoName = Prompt("Enter the second repository name:");
            string secondRepoSharedPath = Prompt("Enter shared path:");
            string thirdRepoOwner = Prompt("Enter the third repository owner:");
            string thirdRepoName = Prompt("Enter the third repository name:");
            string thirdRepoSharedPath = Prompt("Enter shared path:");
            string repositoryBasePath = Prompt("Enter repository local storage base path:");
            string mirrorSignatureUser = Prompt("Enter a username to sign every commit:");
            if (string.IsNullOrWhiteSpace(repositoryBasePath))
            {
                Error("fatal: local storage path required");
            }
            repositoryBasePath = Path.GetFullPath(repositoryBasePath);
            if (File.Exists(repositoryBasePath))
            {
                Error($"fatal: '{repositoryBasePath}' is a file");
            }

            if (!Directory.Exists(repositoryBasePath))
            {
                switch (Inquire($"The directory '{repositoryBasePath}' does not exist, create it?", "y", "n"))
                {
                    case "y":
                        Directory.CreateDirectory(repositoryBasePath);
                        break;
                    case "n":
                        Environment.Exit(-2);
                        break;
                    default:
                        throw new InvalidOperationException("Unexpected");
                }
            }
            var config = new Configuration
            {
                Repos = new List<RepositoryInfo>(),
                UserName = userName,
                Password = password,
                MirrorSignatureUser = mirrorSignatureUser
            };

            config.Repos.Add(new RepositoryInfo { Owner = firstRepoOwner, Name = firstRepoName });
            config.Repos.Add(new RepositoryInfo { Owner = secondRepoOwner, Name = secondRepoName });
            config.Repos.Add(new RepositoryInfo { Owner = thirdRepoOwner, Name = thirdRepoName });

            foreach (var repo in config.Repos)
            {
                repo.Configuration = config;
            }
            EnsureRepository(config.Repos);

            configFile.Save(config);
        }

        private static void Setup(string connectionString, string server, string destinations)
        {
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(connectionString);
            s_table = storageAccount.CreateCloudTableClient().GetTableReference(TableName);
            s_table.CreateIfNotExists();
            s_logger.Info("Connected with azure table Successfully");
            s_repos.Add("corefx", new List<string> { "coreclr", "corert" });
            s_repos.Add("coreclr", new List<string> { "corefx", "corert" });
            s_repos.Add("corert", new List<string> { "coreclr", "corefx" });
            s_emailManager = new EmailManager(server, destinations, s_logger);
            s_logger.Info("Setup Completed");
        }

        private static IEnumerable<DynamicTableEntity> GetCommitsToMirror(RepositoryInfo targetRepo, string branch)
        {
            TableQuery rangeQuery = new TableQuery().Where(TableQuery.CombineFilters(
                TableQuery.GenerateFilterConditionForBool("Mirrored", QueryComparisons.Equal, false),
                TableOperators.And,
                TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, targetRepo.Name)));

            var commits = s_table.ExecuteQuery(rangeQuery);
            commits = commits.Where(t => t.Properties["Branch"].StringValue == branch);

            return commits;
        }

        private void RetrieveOrInsert(string SourceRepo, string branch, string sha, string TargetRepo)
        {
            TableQuery rangeQuery = new TableQuery().Where(TableQuery.CombineFilters(
            TableQuery.GenerateFilterCondition("RowKey", QueryComparisons.Equal, sha),
            TableOperators.And,
            TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, TargetRepo)));

            var commits = s_table.ExecuteQuery(rangeQuery);
            if (commits.Count() == 0)
            {
                DynamicTableEntity entity = new DynamicTableEntity(TargetRepo, sha);
                entity.Properties.Add("Branch", EntityProperty.GeneratePropertyForString(branch));
                entity.Properties.Add("PR", EntityProperty.GeneratePropertyForString(string.Empty));
                entity.Properties.Add("SourceRepo", EntityProperty.GeneratePropertyForString(SourceRepo));
                entity.Properties.Add("Mirrored", EntityProperty.GeneratePropertyForBool(false));

                TableOperation insertOperation = TableOperation.Insert(entity);
                s_table.Execute(insertOperation);
            }
        }

        private void SanityCheck(RepositoryInfo repository, string branch)
        {
            using (var repo = new Repository(repository.Path))
            {
                s_logger.Info($"Running sanity check for {repository.Name}/{branch}");
                var lastLookedAtCommit = repo.Lookup<Commit>(repository.LastSynchronizedCommits[branch]);
                var remoteBranch = repo.Refs[$"refs/heads/{branch}"];

                var commitFilter = new CommitFilter
                {
                    IncludeReachableFrom = remoteBranch,
                    ExcludeReachableFrom = lastLookedAtCommit,
                    SortBy = CommitSortStrategies.Topological | CommitSortStrategies.Time | CommitSortStrategies.Reverse,
                };
                var commitList = repo.Commits.QueryBy(commitFilter).ToList();

                if (commitList.Count == 0)
                    return;
                foreach (var commit in commitList)
                {
                    if (IsMirrorCommit(commit.Message, repository.Configuration.MirrorSignatureUser))
                        continue;

                    var changedFiles = GetChangedFiles(repo, commit);
                    var sharedDirectory = repository.SharedPath;
                    foreach (var changedFile in changedFiles)
                    {
                        if (changedFile.Contains(sharedDirectory))
                        {
                            foreach (string targetRepo in s_repos[repository.Name])
                            {
                                RetrieveOrInsert(repository.Name, branch, commit.Sha, targetRepo);
                            }
                        }
                    }
                }
                UpdateLastSynchronizedCommit(repository, commitList.Last().Sha, branch);
                s_logger.Info($"sanity check Completed for {repository.Name}/{branch}");
            }
        }

        private void UpdateLastSynchronizedCommit(RepositoryInfo repo, string sha, string branch)
        {
            var oldCommit = repo.LastSynchronizedCommits[branch];
            repo.LastSynchronizedCommits[branch] = sha;
            s_logger.Info($"{repo.Owner}/{repo.Name}/{branch} updated {oldCommit} -> {repo.LastSynchronizedCommits[branch]}");
            ConfigFile.Save(repo.Configuration);
        }

        private static IList<string> GetChangedFiles(Repository repo, Commit commit)
        {
            var files = new HashSet<string>();
            var parent = commit.Parents.First();
            foreach (var change in repo.Diff.Compare<TreeChanges>(parent.Tree, commit.Tree))
            {
                files.Add(Path.GetFullPath(change.Path));
                files.Add(Path.GetFullPath(change.OldPath));
            }
            return files.ToList();
        }

        private static void Error(string message)
        {
            Console.Error.WriteLine(message);
            Environment.Exit(-1);
        }
    }
}