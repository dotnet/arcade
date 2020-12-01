using System;
using System.Net;
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
        ///   'sdk', 'runtime' or 'aspnetcore-runtime'
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

            string downloadUrl = GetDownloadUrl();

            Log.LogMessage($"Retrieved dotnet cli {PackageType} version {Version} package uri {downloadUrl}, testing...");

            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Head, downloadUrl);
                using HttpResponseMessage res = await _client.SendAsync(req);

                if (res.StatusCode == HttpStatusCode.NotFound)
                {
                    // 404 means that we successfully hit the server, and it returned 404. This cannot be a network hiccup
                    Log.LogError(FailureCategory.Build, $"Unable to find dotnet cli {PackageType} version {Version}, tried {downloadUrl}");
                }
                else
                {
                    res.EnsureSuccessStatusCode();
                }
            }
            catch (Exception ex)
            {
                Log.LogError(FailureCategory.Build, $"Unable to access dotnet cli {PackageType} version {Version} at {downloadUrl}, {ex.Message}");
            }

            if (!Log.HasLoggedErrors)
            {
                Log.LogMessage($"Url {downloadUrl} is valid.");
                PackageUri = downloadUrl;
            }
        }

        private string GetDownloadUrl()
        {
            string extension = Runtime.StartsWith("win") ? "zip" : "tar.gz";
            return PackageType switch
            {
                "sdk"                => $"{DotNetCliAzureFeed}/Sdk/{Version}/dotnet-sdk-{Version}-{Runtime}.{extension}",
                "aspnetcore-runtime" => $"{DotNetCliAzureFeed}/aspnetcore/Runtime/{Version}/aspnetcore-runtime-{Version}-{Runtime}.{extension}",
                _                    => $"{DotNetCliAzureFeed}/Runtime/{Version}/dotnet-runtime-{Version}-{Runtime}.{extension}"
            };
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
            else if (string.Equals(PackageType, "aspnetcore-runtime", StringComparison.OrdinalIgnoreCase))
            {
                PackageType = "aspnetcore-runtime";
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
                string latestVersionUrl = PackageType switch
                {
                    "sdk"                => $"{DotNetCliAzureFeed}/Sdk/{Channel}/latest.version",
                    "aspnetcore-runtime" => $"{DotNetCliAzureFeed}/aspnetcore/Runtime/{Channel}/latest.version",
                    _                    => $"{DotNetCliAzureFeed}/Runtime/{Channel}/latest.version"
                };

                Log.LogMessage(MessageImportance.Low, $"Resolving latest version from url {latestVersionUrl}");
                string latestVersionContent = await _client.GetStringAsync(latestVersionUrl);
                string[] versionData = latestVersionContent.Split(Array.Empty<char>(), StringSplitOptions.RemoveEmptyEntries);
                Version = versionData[1];
                Log.LogMessage(MessageImportance.Low, $"Got latest dotnet cli version {Version}");
            }
        }
    }
}
