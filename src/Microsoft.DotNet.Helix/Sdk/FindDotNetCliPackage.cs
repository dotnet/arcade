using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.DotNet.Helix.Sdk
{
    public class FindDotNetCliPackage : Microsoft.Build.Utilities.Task
    {
        private static readonly HttpClient _client = new HttpClient();
        private const string DotNetCoreReleasesUrl = "https://raw.githubusercontent.com/dotnet/core/master/release-notes/releases.json";

        [Required]
        public string Version { get; set; }

        [Required]
        public string Runtime { get; set; }

        /// <summary>
        ///   'sdk' or 'runtime'
        /// </summary>
        [Required]
        public string PackageType { get; set; }

        [Output]
        public string PackageUri { get; set; }

        public override bool Execute()
        {
            ExecuteAsync().GetAwaiter().GetResult();
            return !Log.HasLoggedErrors;
        }

        private async Task ExecuteAsync()
        {
            var releases = await GetDotNetCliReleasesAsync();
            var type = PackageType;

            if (!releases.TryGetValue((type, Version), out var release))
            {
                Log.LogError($"Unable to find dotnet cli {type} version {Version}");
                return;
            }

            var uriId = type + "-" + Runtime;
            if (!release.TryGetValue(uriId, out var uri))
            {
                Log.LogError($"Dotnet cli {type} version {Version} does not contain a package for {uriId}");
                return;
            }

            Log.LogMessage($"Retrieved dotnet cli {type} version {Version} package uri {uri}");
            PackageUri = uri;
        }

        private async Task<Dictionary<(string type, string version), Dictionary<string, string>>> GetDotNetCliReleasesAsync()
        {
            var result = new Dictionary<(string type, string version), Dictionary<string, string>>();
            using (var stream = await _client.GetStreamAsync(DotNetCoreReleasesUrl))
            using (var tReader = new StreamReader(stream))
            using (var reader = new JsonTextReader(tReader))
            {
                var releases = JArray.Load(reader);
                Log.LogMessage(releases.ToString());
                foreach (var release in releases.OfType<JObject>())
                {
                    var data = release.Properties()
                        .Where(p => p.Name.StartsWith("runtime") || p.Name.StartsWith("sdk"))
                        .ToDictionary(p => p.Name, p => p.Value.ToObject<string>());
                    if (release.TryGetValue("version-sdk", out JToken sdkVersion))
                    {
                        result[("sdk", sdkVersion.ToString())] = data;
                    }

                    if (release.TryGetValue("version-runtime", out JToken runtimeVersion))
                    {
                        result[("runtime", runtimeVersion.ToString())] = data;
                    }
                }
            }

            Log.LogMessage(JObject.FromObject(result).ToString());
            return result;
        }
    }
}
