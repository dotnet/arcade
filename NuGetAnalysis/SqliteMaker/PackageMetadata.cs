using System;
using System.Collections.Generic;
using NuGet.Frameworks;
using NuGet.Packaging.Core;

namespace SqliteMaker
{

    public class PackageMetadata
    {
        public string Authors { get; set; }
        public int DownloadCount { get; set; }
        public Dictionary<string, List<(string package, string versionRange)>> Dependencies { get; set; }
        public DateTimeOffset LastUpdated { get; set; }
    }
}