// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;

namespace Microsoft.DotNet.GitSync.CommitManager
{
    internal class Table
    {
        public Table(string accountName, string accountKey, string tableName)
        {
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse("DefaultEndpointsProtocol=https;AccountName=" + accountName + ";AccountKey=" + accountKey + ";TableEndpoint=https://" + accountName + ".table.cosmosdb.azure.com:443/;");
            CloudTableClient tableClient = storageAccount.CreateCloudTableClient();
            CTable = tableClient.GetTableReference(tableName);
        }

        public CloudTable CTable { get; set; }
    }
}
