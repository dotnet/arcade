namespace Microsoft.DotNet.DarcLib
{
    public class DependencyDetail
    {
        public string Branch { get; set; }

        public string Name { get; set; }

        public string Version { get; set; }

        public string RepoUri { get; set; }

        public string Commit { get; set; }

        public DependencyType Type { get; set; }
    }
}
