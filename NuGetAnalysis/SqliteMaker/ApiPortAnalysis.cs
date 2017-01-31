using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SqliteMaker
{
    public class ApiPortAnalysis
    {
        public IReadOnlyList<Dependency> MissingDependencies { get; set; }
        [JsonExtensionData]
        public IDictionary<string, JToken> Data { get; set; }

        public ApiPortAnalysis(IReadOnlyList<Dependency> missingDependencies)
        {
            MissingDependencies = missingDependencies;
        }

        public static ApiPortAnalysis operator +(ApiPortAnalysis left, ApiPortAnalysis right)
        {
            return new ApiPortAnalysis(left.MissingDependencies.Concat(right.MissingDependencies).ToList())
            {
                Data = left.Data
            };
        }

        public class Dependency
        {
            [JsonProperty("DefinedInAssemblyIdentity")]
            public string AssemblyIdentity { get; }
            [JsonIgnore]
            public string AssemblyName => AssemblyIdentity?.Split(',')[0] ?? "";
            public string MemberDocId { get; }
            [JsonExtensionData]
            public IDictionary<string, JToken> Data { get; set; }
            public Dependency(string assemblyIdentity, string memberDocId)
            {
                AssemblyIdentity = assemblyIdentity;
                MemberDocId = memberDocId;
            }
        }
    }
}