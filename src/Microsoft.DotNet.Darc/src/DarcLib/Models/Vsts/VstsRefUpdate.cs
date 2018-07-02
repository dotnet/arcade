namespace Microsoft.DotNet.Darc
{
    public class VstsRefUpdate
    {
        public VstsRefUpdate(string branch, string currentSha)
        {
            Name = branch;
            OldObjectId = currentSha;
        }

        public string Name { get; set; }

        public string OldObjectId { get; set; }
    }
}
