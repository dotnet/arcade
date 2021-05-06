using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using Microsoft.Arcade.Common;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

#nullable enable
namespace Microsoft.DotNet.Helix.Sdk
{
    public enum ApplePlatform
    {
        iOS,
        tvOS,
    }

    public interface IProvisioningProfileProvider
    {
        void AddProfilesToBundles(ITaskItem[] appBundles);
    }

    public class ProvisioningProfileProvider : IProvisioningProfileProvider
    {
        private static readonly IReadOnlyDictionary<ApplePlatform, string> s_targetNames = new Dictionary<ApplePlatform, string>()
        {
            { ApplePlatform.iOS, "ios-device" },
            { ApplePlatform.tvOS, "tvos-device" },
        };

        private readonly TaskLoggingHelper _log;
        private readonly IHelpers _helpers;
        private readonly HttpClient _httpClient;
        private readonly string? _profileUrlTemplate;
        private readonly string? _tmpDir;

        public ProvisioningProfileProvider(TaskLoggingHelper log, IHelpers helpers, HttpClient httpClient, string? profileUrlTemplate, string? tmpDir)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _helpers = helpers ?? throw new ArgumentNullException(nameof(helpers));
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _profileUrlTemplate = profileUrlTemplate;
            _tmpDir = tmpDir;
        }

        public void AddProfilesToBundles(ITaskItem[] appBundles)
        {
            var profileLocations = new Dictionary<ApplePlatform, string>();

            foreach (var appBundle in appBundles)
            {
                if (!appBundle.TryGetMetadata(CreateXHarnessAppleWorkItems.TargetPropName, out string bundleTargets))
                {
                    _log.LogError("'Targets' metadata must be specified - " +
                        "expecting list of target device/simulator platforms to execute tests on (e.g. ios-simulator-64)");
                    continue;
                }

                foreach (var pair in s_targetNames)
                {
                    var platform = pair.Key;
                    var targetName = pair.Value;

                    if (!bundleTargets.Contains(targetName))
                    {
                        continue;
                    }

                    // App comes with a profile already
                    var provisioningProfileDestPath = Path.Combine(appBundle.ItemSpec, "embedded.mobileprovision");
                    if (File.Exists(provisioningProfileDestPath))
                    {
                        _log.LogMessage($"Bundle already contains a provisioning profile at `{provisioningProfileDestPath}`");
                        continue;
                    }

                    // This makes sure we download the profile the first time we see an app that needs it
                    if (!profileLocations.TryGetValue(platform, out string profilePath))
                    {
                        if (string.IsNullOrEmpty(_tmpDir))
                        {
                            _log.LogError("TmpDir parameter not set but required for real device targets!");
                            return;
                        }

                        if (string.IsNullOrEmpty(_profileUrlTemplate))
                        {
                            _log.LogError("ProvisioningProfileUrl parameter not set but required for real device targets!");
                            return;
                        }

                        profilePath = DownloadProvisioningProfile(platform);
                        profileLocations.Add(platform, DownloadProvisioningProfile(platform));
                    }

                    // Copy the profile into the folder
                    _log.LogMessage($"Adding provisioning profile `{profilePath}` into the app bundle at `{provisioningProfileDestPath}`");
                    File.Copy(profilePath, provisioningProfileDestPath);
                }
            }
        }

        private string DownloadProvisioningProfile(ApplePlatform platform)
        {
            var targetFile = Path.Combine(_tmpDir, GetProvisioningProfileFileName(platform));

            _helpers.DirectoryMutexExec(async () =>
            {
                if (File.Exists(targetFile))
                {
                    _log.LogMessage($"Provisioning profile is already downloaded");
                    return;
                }

                _log.LogMessage($"Downloading {platform} provisioning profile to {targetFile}");

                var uri = new Uri(GetProvisioningProfileUrl(platform));
                using var response = await _httpClient.GetAsync(uri);
                response.EnsureSuccessStatusCode();

                using var fileStream = new FileStream(targetFile, FileMode.Create, FileAccess.Write);
                await response.Content.CopyToAsync(fileStream);
            }, _tmpDir);

            return targetFile;
        }

        private string GetProvisioningProfileFileName(ApplePlatform platform) => Path.GetFileName(GetProvisioningProfileUrl(platform));

        private string GetProvisioningProfileUrl(ApplePlatform platform) => _profileUrlTemplate!.Replace("{PLATFORM}", platform.ToString());

    }
}
