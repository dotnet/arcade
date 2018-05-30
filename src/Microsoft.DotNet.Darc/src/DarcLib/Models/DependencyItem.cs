using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Microsoft.DotNet.Darc
{
    public class DependencyItem
    {
        public string Branch { get; set; }

        public string Name { get; set; }

        public string RepoUri { get; set; }

        public string Sha { get; set; }

        public string Version { get; set; }

        [JsonConverter(typeof(StringEnumConverter))]
        public DependencyType Type { get; set; }
    }
}
