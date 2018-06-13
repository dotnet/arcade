using Microsoft.WindowsAzure.Storage.Table;

namespace Microsoft.DotNet.GitSync.CommitManager
{
    class CommitEntity : TableEntity
    {
        public CommitEntity(string sourceRepo, string targetRepo, string commitId, string branch)
        {
            this.SourceRepo = sourceRepo;
            this.PartitionKey = targetRepo;
            this.RowKey = commitId;
            this.Branch = branch;
            this.Mirrored = false;
            this.PR = null;
        }

        public CommitEntity() { }
        public string SourceRepo { get; set; }
        public string Branch { get; set; }
        public bool Mirrored { get; set; }
        public string PR { get; set; }
    }
}
