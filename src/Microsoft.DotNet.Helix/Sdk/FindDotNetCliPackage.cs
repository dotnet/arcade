using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Build.Framework;

namespace Microsoft.DotNet.Helix.Sdk
{
    public class FindDotNetCliPackage : BaseTask
    {
        private static readonly HttpClient _client = new HttpClient(new HttpClientHandler { CheckCertificateRevocationList = true });
        private const string DotNetCliAzureFeed = "https://dotnetcli.azureedge.net/dotnet";

        /// <summary>
        ///   'LTS' or 'Current'
        /// </summary>
        [Required]
        public string Channel { get; set; }

        /// <summary>
        ///   'latest' or specific version
        /// </summary>
        [Required]
        public string Version { get; set; }

        /// <summary>
        ///   RID of dotnet cli to get
        /// </summary>
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
            NormalizeParameters();
            await ResolveVersionAsync();

            var downloadUrl = GetDownloadUrl();

            Log.LogMessage($"Retrieved dotnet cli {PackageType} version {Version} package uri {downloadUrl}, testing...");

            using (var req = new HttpRequestMessage(HttpMethod.Head, downloadUrl))
            {
                var res = await _client.SendAsync(req);
                res.EnsureSuccessStatusCode();
            }

            Log.LogMessage($"Url {downloadUrl} is valid.");

            PackageUri = downloadUrl;
        }

        private string GetDownloadUrl()
        {
            var extension = Runtime.StartsWith("win") ? "zip" : "tar.gz";
            if (PackageType == "sdk")
            {
                return $"{DotNetCliAzureFeed}/Sdk/{Version}/dotnet-sdk-{Version}-{Runtime}.{extension}";
            }
            else // PackageType == "runtime"
            {
                return $"{DotNetCliAzureFeed}/Runtime/{Version}/dotnet-runtime-{Version}-{Runtime}.{extension}";
            }
        }

        private void NormalizeParameters()
        {
            if (string.Equals(Channel, "lts", StringComparison.OrdinalIgnoreCase))
            {
                Channel = "LTS";
            }
            else if (string.Equals(Channel, "current", StringComparison.OrdinalIgnoreCase))
            {
                Channel = "Current";
            }
            else
            {
                throw new ArgumentException($"Invalid value '{Channel}' for parameter {nameof(Channel)}");
            }

            if (string.Equals(Version, "latest", StringComparison.OrdinalIgnoreCase))
            {
                Version = "latest";
            }

            if (string.Equals(PackageType, "sdk", StringComparison.OrdinalIgnoreCase))
            {
                PackageType = "sdk";
            }
            else if (string.Equals(PackageType, "runtime", StringComparison.OrdinalIgnoreCase))
            {
                PackageType = "runtime";
            }
            else
            {
                throw new ArgumentException($"Invalid value '{PackageType}' for parameter {nameof(PackageType)}");
            }
        }

        private async Task ResolveVersionAsync()
        {
            if (Version == "latest")
            {
                Log.LogMessage(MessageImportance.Low, "Resolving latest dotnet cli version.");
                string latestVersionUrl;
                if (PackageType == "sdk")
                {
                    latestVersionUrl = $"{DotNetCliAzureFeed}/Sdk/{Channel}/latest.version";
                }
                else // PackageType == "runtime"
                {
                    latestVersionUrl = $"{DotNetCliAzureFeed}/Runtime/{Channel}/latest.version";
                }

                Log.LogMessage(MessageImportance.Low, $"Resolving latest version from url {latestVersionUrl}");
                var latestVersionContent = await _client.GetStringAsync(latestVersionUrl);
                var versionData = latestVersionContent.Split(Array.Empty<char>(), StringSplitOptions.RemoveEmptyEntries);
                Version = versionData[1];
                Log.LogMessage(MessageImportance.Low, $"Got latest dotnet cli version {Version}");
            }
        }
    }
}
