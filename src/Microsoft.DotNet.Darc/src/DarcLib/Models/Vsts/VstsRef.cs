namespace Microsoft.DotNet.Darc
{
    public class VstsRef
    {
        public VstsRef(string name, string sha, string oldObjectId = null)
        {
            Name = name;
            NewObjectId = sha;

            if (!string.IsNullOrEmpty(oldObjectId))
            {
                OldObjectId = oldObjectId;
            }
        }

        public string Name { get; set; }

        public string NewObjectId { get; set; }

        public string OldObjectId { get; set; } = "0000000000000000000000000000000000000000";
    }
}
