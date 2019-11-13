// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using MSBuild = Microsoft.Build.Utilities;

namespace Microsoft.DotNet.Build.Tasks.Feed
{
    public class CreateAzureDevOpsFeed : MSBuild.Task
    {
        [Output]
        public string TargetFeedURL { get; set; }

        [Output]
        public string TargetFeedName { get; set; }

        [Required]
        public bool IsInternal { get; set; }

        [Required]
        public string RepositoryName { get; set; }

        [Required]
        public string CommitSha { get; set; }

        /// <summary>
        /// Additional info to include in the feed name (for example "sym")
        /// </summary>
        public string ContentIdentifier { get; set; }

        [Required]
        public string AzureDevOpsPersonalAccessToken { get; set; }

        public string AzureDevOpsFeedsApiVersion { get; set; } = "5.0-preview.1";

        public static string AzureDevOpsOrg { get; set; } = "dnceng";

        private readonly string AzureDevOpsFeedsBaseUrl = $"https://feeds.dev.azure.com/{AzureDevOpsOrg}/";

        /// <summary>
        /// Number of characters from the commit SHA prefix that should be included in the feed name.
        /// </summary>
        private readonly int ShaUsableLength = 8;

        /// <summary>
        /// Maximum allowed length for AzDO feed names.
        /// </summary>
        private readonly int MaxLengthForAzDoFeedNames = 64;

        public override bool Execute()
        {
            return ExecuteAsync().GetAwaiter().GetResult();
        }

        private async Task<bool> ExecuteAsync()
        {
            try
            {
                if (CommitSha.Length < ShaUsableLength)
                {
                    Log.LogError($"The CommitSHA should be at least {ShaUsableLength} characters long: CommitSha is '{CommitSha}'. Aborting feed creation.");
                    return false;
                }

                JsonSerializerSettings _serializerSettings = new JsonSerializerSettings
                {
                    ContractResolver = new CamelCasePropertyNamesContractResolver(),
                    NullValueHandling = NullValueHandling.Ignore
                };

                // GitHub repos may appear in the repository name with an 'org/repo' form.
                // When creating a repo, Github aslready replaces all of the characters invalid in AzDO feed names (see below)
                // with '-' in the repo name. We just need to replace '/' with '-' to deal with the org/repo input.
                // From the AzDO docs:
                // The feed name can't contain spaces, start with a '.' or '_', end with a '.',
                // or contain any of these: @ ~ ; { } ' + = , < > | / \ ? : & $ * " # [ ] %
                string feedCompatibleRepositoryName = RepositoryName.Replace('/', '-');

                string accessType = IsInternal ? "internal" : "public";
                string publicSegment = IsInternal ? string.Empty : "public/";
                string accessId = IsInternal ? "int" : "pub";
                string extraContentInfo = !string.IsNullOrEmpty(ContentIdentifier) ? $"-{ContentIdentifier}" : "";
                string baseFeedName = $"darc-{accessId}{extraContentInfo}-{feedCompatibleRepositoryName}-{CommitSha.Substring(0, ShaUsableLength)}";
                string versionedFeedName = baseFeedName;
                bool needsUniqueName = false;
                int subVersion = 0;

                Log.LogMessage(MessageImportance.High, $"Creating the new {accessType} Azure DevOps artifacts feed '{baseFeedName}'...");

                if (baseFeedName.Length > MaxLengthForAzDoFeedNames)
                {
                    Log.LogError($"The name of the new feed ({baseFeedName}) exceeds the maximum feed name size of 64 chars. Aborting feed creation.");
                    return false;
                }

                do
                {
                    using (HttpClient client = new HttpClient(new HttpClientHandler { CheckCertificateRevocationList = true })
                    {
                        BaseAddress = new Uri(AzureDevOpsFeedsBaseUrl)
                    })
                    {
                        client.DefaultRequestHeaders.Add(
                            "Accept",
                            $"application/json;api-version={AzureDevOpsFeedsApiVersion}");
                        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                            "Basic",
                            Convert.ToBase64String(Encoding.ASCII.GetBytes(string.Format("{0}:{1}", "", AzureDevOpsPersonalAccessToken))));

                        AzureDevOpsArtifactFeed newFeed = new AzureDevOpsArtifactFeed(versionedFeedName);

                        string body = JsonConvert.SerializeObject(newFeed, _serializerSettings);

                        HttpRequestMessage postMessage = new HttpRequestMessage(HttpMethod.Post, $"{publicSegment}_apis/packaging/feeds");
                        postMessage.Content = new StringContent(body, Encoding.UTF8, "application/json");

                        HttpResponseMessage response = await client.SendAsync(postMessage);

                        if (response.StatusCode == HttpStatusCode.Created)
                        {
                            needsUniqueName = false;
                            baseFeedName = versionedFeedName;
                        }
                        else if (response.StatusCode == HttpStatusCode.Conflict)
                        {
                            versionedFeedName = $"{baseFeedName}-{++subVersion}";
                            needsUniqueName = true;

                            if (versionedFeedName.Length > MaxLengthForAzDoFeedNames)
                            {
                                Log.LogError($"The name of the new feed ({baseFeedName}) exceeds the maximum feed name size of 64 chars. Aborting feed creation.");
                                return false;
                            }
                        }
                        else
                        {
                            throw new Exception($"Feed '{baseFeedName}' was not created. Request failed with status code {response.StatusCode}. Exception: {await response.Content.ReadAsStringAsync()}");
                        }
                    }
                } while (needsUniqueName);

                TargetFeedURL = $"https://pkgs.dev.azure.com/{AzureDevOpsOrg}/{publicSegment}_packaging/{baseFeedName}/nuget/v3/index.json";
                TargetFeedName = baseFeedName;

                Log.LogMessage(MessageImportance.High, $"Feed '{TargetFeedURL}' created successfully!");
            }
            catch (Exception e)
            {
                Log.LogErrorFromException(e, true);
            }

            return !Log.HasLoggedErrors;
        }
    }

    public class Permission
    {
        public Permission(string identityDescriptor, int role)
        {
            IdentityDescriptor = identityDescriptor;
            Role = role;
        }

        public string IdentityDescriptor { get; set; }

        public int Role { get; set; }
    }

    public class AzureDevOpsArtifactFeed
    {
        public AzureDevOpsArtifactFeed(string name)
        {
            Name = name;
        }

        public string Name { get; set; }

        public readonly List<Permission> Permissions = new List<Permission>
        {
            // Mimic the permissions added to a feed when created in the browser
            new Permission("Microsoft.TeamFoundation.ServiceIdentity;116cce53-b859-4624-9a95-934af41eccef:Build:b55de4ed-4b5a-4215-a8e4-0a0a5f71e7d8", 3),                      // Project Collection Build Service
            new Permission("Microsoft.TeamFoundation.ServiceIdentity;116cce53-b859-4624-9a95-934af41eccef:Build:7ea9116e-9fac-403d-b258-b31fcf1bb293", 3),                      // internal Build Service
            new Permission("Microsoft.TeamFoundation.Identity;S-1-9-1551374245-1349140002-2196814402-2899064621-3782482097-0-0-0-0-1", 4),                                      // Feed administrators
            new Permission("Microsoft.TeamFoundation.Identity;S-1-9-1551374245-1846651262-2896117056-2992157471-3474698899-1-2052915359-1158038602-2757432096-2854636005", 4)   // Feed administrators and contributors
        };
    }
}
