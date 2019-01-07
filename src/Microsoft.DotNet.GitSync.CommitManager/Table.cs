// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;

namespace Microsoft.DotNet.GitSync.CommitManager
{
    internal class Table
    {
        public Table(string accountName, string accountKey, string tableName, string repoTableName)
        {
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse("DefaultEndpointsProtocol=https;AccountName=" + accountName + ";AccountKey=" + accountKey + ";TableEndpoint=https://" + accountName + ".table.cosmosdb.azure.com:443/;");
            CloudTableClient tableClient = storageAccount.CreateCloudTableClient();
            CommitTable = tableClient.GetTableReference(tableName);
            RepoTable = tableClient.GetTableReference(repoTableName);
        }

        public CloudTable CommitTable { get; set; }

        public CloudTable RepoTable { get; set; }
    }
}
