using System.Collections.Generic;

namespace Microsoft.DotNet.DarcLib
{
    public class BuildData
    {
        public string Repository { get; set; }

        public string Branch { get; set; }

        public string Commit { get; set; }

        public string BuildNumber { get; set; }

        public List<AssetData> Assets { get; set; }

        public List<int> Dependencies { get; set; }
    }
}
