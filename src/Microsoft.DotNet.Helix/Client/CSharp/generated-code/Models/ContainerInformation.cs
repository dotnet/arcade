using System;
using System.Collections.Immutable;
using Newtonsoft.Json;

namespace Microsoft.DotNet.Helix.Client.Models
{
    public partial class ContainerInformation
    {
        public ContainerInformation(DateTimeOffset created, DateTimeOffset expiration, string creator, string containerName, string storageAccountName)
        {
            Created = created;
            Expiration = expiration;
            Creator = creator;
            ContainerName = containerName;
            StorageAccountName = storageAccountName;
        }

        [JsonProperty("Created")]
        public DateTimeOffset Created { get; set; }

        [JsonProperty("Expiration")]
        public DateTimeOffset Expiration { get; set; }

        [JsonProperty("ReadToken")]
        public string ReadToken { get; set; }

        [JsonProperty("WriteToken")]
        public string WriteToken { get; set; }

        [JsonProperty("Creator")]
        public string Creator { get; set; }

        [JsonProperty("ContainerName")]
        public string ContainerName { get; set; }

        [JsonProperty("StorageAccountName")]
        public string StorageAccountName { get; set; }

        [JsonIgnore]
        public bool IsValid
        {
            get
            {
                if (string.IsNullOrEmpty(Creator))
                {
                    return false;
                }
                if (string.IsNullOrEmpty(ContainerName))
                {
                    return false;
                }
                if (string.IsNullOrEmpty(StorageAccountName))
                {
                    return false;
                }
                return true;
            }
        }
    }
}
