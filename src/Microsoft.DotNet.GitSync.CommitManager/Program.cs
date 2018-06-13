using System;
using System.Collections.Generic;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;

namespace Microsoft.DotNet.GitSync.CommitManager
{
    class Program
    {
        private const string _cloudTableName = "CommitHistory";
        private static CloudStorageAccount _storageAccount;
        private static CloudTableClient _tableClient;
        private static CloudTable _table;
        private static string _accountName;
        private static string _accountKey;

        private static CloudTable Table
        {
            get
            {
                if (_table == null)
                {
                    _storageAccount = CloudStorageAccount.Parse("DefaultEndpointsProtocol=https;AccountName=" + _accountName + ";AccountKey=" + _accountKey + ";TableEndpoint=https://" + _accountName  + ".table.cosmosdb.azure.com:443/;");
                    _tableClient = _storageAccount.CreateCloudTableClient();
                    _table = _tableClient.GetTableReference(_cloudTableName);
                }
                return _table;
            }
        }

        private static Dictionary<string, List<string>> Repos { get; set; } = new Dictionary<string, List<string>>();

        public static void Main(string[] args)
        {
            if (args.Length == 6)
            {
                
                if (!IsMyCommit(args[5]))
                {
                    Setup(args[0], args[1]);
                    GetTableClient(args[2], args[4], args[3]);
                }
            }
            else
            {
                throw new ArgumentException("Too Few Arguments");
            }
        }

        private static void Setup(string AccountName, string AccountKey)
        {
            _accountName = AccountName;
            _accountKey = AccountKey;

            Repos.Add("corefx", new List<string> { "coreclr", "corefx" });
            Repos.Add("coreclr", new List<string> { "corefx", "corert" });
            Repos.Add("corert", new List<string> { "coreclr", "corert" });

            Table.CreateIfNotExistsAsync().Wait();
        }

        private static bool IsMyCommit(string message) => message.Contains("Signed-off-by: dotnet-bot <dotnet-bot@microsoft.com>");

        private static void GetTableClient(string sourceRepo, string commitId, string branch)
        {
            foreach (string repo in Repos[sourceRepo])
            {
                CommitEntity entry = new CommitEntity(sourceRepo, repo, commitId, branch);
                TableOperation insertOperation = TableOperation.Insert(entry);
                Table.ExecuteAsync(insertOperation).Wait();
                Console.WriteLine($"Commit {commitId} added to table to get mirrored from {sourceRepo} to {repo}");
            }
        }
    }
}
