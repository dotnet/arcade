using System.Collections.Generic;

namespace Microsoft.DotNet.Darc
{
    public class VstsCommit
    {
        public VstsCommit(List<VstsChange> changes, string commitComment)
        {
            Changes = changes;
            Comment = commitComment;
        }
        public List<VstsChange> Changes { get; set; }

        public string Comment { get; set; }
    }
}
