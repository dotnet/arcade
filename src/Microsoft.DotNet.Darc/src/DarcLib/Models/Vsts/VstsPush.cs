using System.Collections.Generic;

namespace Microsoft.DotNet.Darc
{
    public class VstsPush
    {
        public VstsPush(VstsRefUpdate refUpdate, VstsCommit vstsCommit)
        {
            RefUpdates = new List<VstsRefUpdate> { refUpdate };
            Commits = new List<VstsCommit> { vstsCommit };
        }
        public List<VstsRefUpdate> RefUpdates { get; set; }

        public List<VstsCommit> Commits { get; set; }
    }
}
