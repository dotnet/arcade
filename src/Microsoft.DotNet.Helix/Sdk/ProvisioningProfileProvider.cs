// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using Microsoft.Arcade.Common;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
        private readonly IFileSystem _fileSystem;
        private readonly IZipArchiveManager _zipArchiveManager;
        private readonly HttpClient _httpClient;
        private readonly string? _profileUrlTemplate;
        private readonly string? _tmpDir;
        private readonly Dictionary<ApplePlatform, string> _downloadedProfiles = new();

        public ProvisioningProfileProvider(
            TaskLoggingHelper log,
            IHelpers helpers,
            IFileSystem fileSystem,
            IZipArchiveManager zipArchiveManager,
            HttpClient httpClient,
            string? profileUrlTemplate,
            string? tmpDir)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _helpers = helpers ?? throw new ArgumentNullException(nameof(helpers));
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            _zipArchiveManager = zipArchiveManager ?? throw new ArgumentNullException(nameof(zipArchiveManager));
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _profileUrlTemplate = profileUrlTemplate;
            _tmpDir = tmpDir;
        }

        public void AddProfilesToBundles(ITaskItem[] appBundles)
        {
            foreach (var appBundle in appBundles)
            {
                var (workItemName, appBundlePath) = XHarnessTaskBase.GetNameAndPath(appBundle, CreateXHarnessAppleWorkItems.MetadataNames.AppBundlePath, _fileSystem);

                if (!appBundle.TryGetMetadata(CreateXHarnessAppleWorkItems.MetadataNames.Target, out string testTarget))
                {
                    _log.LogError($"'{CreateXHarnessAppleWorkItems.MetadataNames.Target}' metadata must be specified - " +
                        "expecting list of target device/simulator platforms to execute tests on (e.g. ios-simulator-64)");
                    continue;
                }

                foreach (var pair in s_targetNames)
                {
                    var platform = pair.Key;
                    var targetName = pair.Value;

                    if (!testTarget.Contains(targetName))
                    {
                        continue;
                    }

                    if (appBundlePath.EndsWith(".zip"))
                    {
                        // TODO: We need to be able to add provisioning profiles into a zipped payload too
                    }
                    else
                    {
                        AddProfileToBundle(platform, appBundlePath);
                    }
                }
            }
        }

        private void AddProfileToBundle(ApplePlatform platform, string appBundlePath)
        {
            // App comes with a profile already
            var provisioningProfileDestPath = _fileSystem.PathCombine(appBundlePath, "embedded.mobileprovision");
            if (_fileSystem.FileExists(provisioningProfileDestPath))
            {
                _log.LogMessage($"Bundle already contains a provisioning profile at `{provisioningProfileDestPath}`");
                return;
            }

            // This makes sure we download the profile the first time we see an app that needs it
            if (!_downloadedProfiles.TryGetValue(platform, out string? profilePath))
            {
                if (string.IsNullOrEmpty(_tmpDir))
                {
                    _log.LogError($"{nameof(CreateXHarnessAppleWorkItems.TmpDir)} parameter not set but required for real device targets!");
                    return;
                }

                if (string.IsNullOrEmpty(_profileUrlTemplate))
                {
                    _log.LogError($"{nameof(CreateXHarnessAppleWorkItems.ProvisioningProfileUrl)} parameter not set but required for real device targets!");
                    return;
                }

                profilePath = DownloadProvisioningProfile(platform);
                _downloadedProfiles.Add(platform, profilePath);
            }

            // Copy the profile into the folder
            _log.LogMessage($"Adding provisioning profile `{profilePath}` into the app bundle at `{provisioningProfileDestPath}`");
            _fileSystem.FileCopy(profilePath, provisioningProfileDestPath);
        }

        private string DownloadProvisioningProfile(ApplePlatform platform)
        {
            var targetFile = _fileSystem.PathCombine(_tmpDir!, GetProvisioningProfileFileName(platform));

            _helpers.DirectoryMutexExec(async () =>
            {
                if (_fileSystem.FileExists(targetFile))
                {
                    _log.LogMessage($"Provisioning profile is already downloaded");
                    return;
                }

                _log.LogMessage($"Downloading {platform} provisioning profile to {targetFile}");

                var uri = new Uri(GetProvisioningProfileUrl(platform));
                using var response = await _httpClient.GetAsync(uri);
                response.EnsureSuccessStatusCode();

                using var fileStream = _fileSystem.GetFileStream(targetFile, FileMode.Create, FileAccess.Write);
                await response.Content.CopyToAsync(fileStream);
            }, _tmpDir);

            return targetFile;
        }

        private string GetProvisioningProfileFileName(ApplePlatform platform)
            => _fileSystem.GetFileName(GetProvisioningProfileUrl(platform))
                ?? throw new InvalidOperationException("Failed to get provision profile file name");

        private string GetProvisioningProfileUrl(ApplePlatform platform)
            => _profileUrlTemplate!.Replace("{PLATFORM}", platform.ToString());
    }

    public static class ProvisioningProfileProviderRegistration
    {
        public static void TryAddProvisioningProfileProvider(this IServiceCollection collection, string provisioningProfileUrlTemplate, string tmpDir)
        {
            collection.TryAddTransient<IHelpers, Arcade.Common.Helpers>();
            collection.TryAddTransient<IFileSystem, FileSystem>();
            collection.TryAddTransient<IZipArchiveManager, ZipArchiveManager>();
            collection.TryAddSingleton(_ => new HttpClient(new HttpClientHandler { CheckCertificateRevocationList = true }));
            collection.TryAddSingleton<IProvisioningProfileProvider>(serviceProvider =>
            {
                return new ProvisioningProfileProvider(
                    serviceProvider.GetRequiredService<TaskLoggingHelper>(),
                    serviceProvider.GetRequiredService<IHelpers>(),
                    serviceProvider.GetRequiredService<IFileSystem>(),
                    serviceProvider.GetRequiredService<IZipArchiveManager>(),
                    serviceProvider.GetRequiredService<HttpClient>(),
                    provisioningProfileUrlTemplate,
                    tmpDir);
            });
        }
    }
}
