// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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

        /// <summary>
        /// Organization that the feed should be created in
        /// </summary>
        [Required]
        public string AzureDevOpsOrg { get; set; }

        /// <summary>
        /// Project that that feed should be created in. The public/internal visibility
        /// of this project will determine whether the feed is public.
        /// </summary>
        [Required]
        public string AzureDevOpsProject { get; set; }

        /// <summary>
        /// Personal access token used to authorize to the API and create the feed
        /// </summary>
        [Required]
        public string AzureDevOpsPersonalAccessToken { get; set; }

        public string RepositoryName { get; set; }

        public string CommitSha { get; set; }

        /// <summary>
        /// In case we want to use a defined feed name instead of calculate one dynamically
        /// </summary>
        public string FeedName { get; set; }

        /// <summary>
        /// Additional info to include in the feed name (for example "sym")
        /// </summary>
        public string ContentIdentifier { get; set; }

        public string AzureDevOpsFeedsApiVersion { get; set; } = "5.1-preview.1";

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
                if (CommitSha?.Length < ShaUsableLength)
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
                string feedCompatibleRepositoryName = RepositoryName?.Replace('/', '-');

                // For clarity, and compatibility with existing infrastructure, we include the feed visibility tag.
                // This serves two purposes:
                // 1. In nuget.config files (and elsewhere), the name at a glance can identify its visibility
                // 2. Existing automation has knowledge of "darc-int" and "darc-pub" for purposes of injecting authentication for internal builds
                //    and managing the isolated feeds within the NuGet.config files.
                string accessTag = GetFeedVisibilityTag(AzureDevOpsOrg, AzureDevOpsProject);
                string extraContentInfo = !string.IsNullOrEmpty(ContentIdentifier) ? $"-{ContentIdentifier}" : "";
                string baseFeedName = FeedName ?? $"darc-{accessTag}{extraContentInfo}-{feedCompatibleRepositoryName}-{CommitSha.Substring(0, ShaUsableLength)}";
                string versionedFeedName = baseFeedName;
                bool needsUniqueName = false;
                int subVersion = 0;

                Log.LogMessage(MessageImportance.High, $"Creating the new Azure DevOps artifacts feed '{baseFeedName}'...");

                if (baseFeedName.Length > MaxLengthForAzDoFeedNames)
                {
                    Log.LogError($"The name of the new feed ({baseFeedName}) exceeds the maximum feed name size of 64 chars. Aborting feed creation.");
                    return false;
                }

                string azureDevOpsFeedsBaseUrl = $"https://feeds.dev.azure.com/{AzureDevOpsOrg}/";
                do
                {
                    using (HttpClient client = new HttpClient(new HttpClientHandler { CheckCertificateRevocationList = true })
                    {
                        BaseAddress = new Uri(azureDevOpsFeedsBaseUrl)
                    })
                    {
                        client.DefaultRequestHeaders.Add(
                            "Accept",
                            $"application/json;api-version={AzureDevOpsFeedsApiVersion}");
                        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                            "Basic",
                            Convert.ToBase64String(Encoding.ASCII.GetBytes(string.Format("{0}:{1}", "", AzureDevOpsPersonalAccessToken))));

                        AzureDevOpsArtifactFeed newFeed = new AzureDevOpsArtifactFeed(versionedFeedName, AzureDevOpsOrg, AzureDevOpsProject);

                        string createBody = JsonConvert.SerializeObject(newFeed, _serializerSettings);

                        using HttpRequestMessage createFeedMessage = new HttpRequestMessage(HttpMethod.Post, $"{AzureDevOpsProject}/_apis/packaging/feeds");
                        createFeedMessage.Content = new StringContent(createBody, Encoding.UTF8, "application/json");
                        using HttpResponseMessage createFeedResponse = await client.SendAsync(createFeedMessage);

                        if (createFeedResponse.StatusCode == HttpStatusCode.Created)
                        {
                            needsUniqueName = false;
                            baseFeedName = versionedFeedName;

                            // Now update the 'Local' feed view with aad tenant visibility.
                            var feedViewVisibilityPatch = new FeedView()
                            {
                                Visibility = "collection"
                            };

                            string patchBody = JsonConvert.SerializeObject(feedViewVisibilityPatch, _serializerSettings);

                            // Note that Framework doesn't natively have Patch
#if NETFRAMEWORK
                            HttpMethod patchMethod = new HttpMethod("PATCH");
#else
                            HttpMethod patchMethod = HttpMethod.Patch;
#endif
                            using HttpRequestMessage patchFeedViewMessage = new HttpRequestMessage(patchMethod, $"{AzureDevOpsProject}/_apis/packaging/feeds/{baseFeedName}/views/Local");
                            patchFeedViewMessage.Content = new StringContent(patchBody, Encoding.UTF8, "application/json");
                            using HttpResponseMessage patchFeedViewResponse = await client.SendAsync(patchFeedViewMessage);

                            if (patchFeedViewResponse.StatusCode != HttpStatusCode.OK)
                            {
                                throw new Exception($"Feed view 'Local' for '{baseFeedName}' could not be updated to have aadTenant visibility. Exception: {await patchFeedViewResponse.Content.ReadAsStringAsync()}");
                            }
                        }
                        else if (createFeedResponse.StatusCode == HttpStatusCode.Conflict)
                        {
                            versionedFeedName = $"{baseFeedName}-{++subVersion}";
                            needsUniqueName = true;

                            if (versionedFeedName.Length > MaxLengthForAzDoFeedNames)
                            {
                                Console.WriteLine($"The name of the new feed ({baseFeedName}) exceeds the maximum feed name size of 64 chars. Aborting feed creation.");
                                return false;
                            }
                        }
                        else
                        {
                            throw new Exception($"Feed '{baseFeedName}' was not created. Request failed with status code {createFeedResponse.StatusCode}. Exception: {await createFeedResponse.Content.ReadAsStringAsync()}");
                        }
                    }
                } while (needsUniqueName);

                TargetFeedURL = $"https://pkgs.dev.azure.com/{AzureDevOpsOrg}/{AzureDevOpsProject}/_packaging/{baseFeedName}/nuget/v3/index.json";
                TargetFeedName = baseFeedName;

                Log.LogMessage(MessageImportance.High, $"Feed '{TargetFeedURL}' created successfully!");
            }
            catch (Exception e)
            {
                Log.LogErrorFromException(e, true);
            }

            return !Log.HasLoggedErrors;
        }

        /// <summary>
        /// Returns a tag for feed visibility that will be added to the feed name
        /// </summary>
        /// <param name="organization">Organization containing the feed</param>
        /// <param name="project">Project within <paramref name="organization"/> containing the feed</param>
        /// <returns>Feed tag</returns>
        /// <exception cref="NotImplementedException"></exception>
        private string GetFeedVisibilityTag(string organization, string project)
        {
            switch (organization)
            {
                case "dnceng":
                    switch (project)
                    {
                        case "internal":
                            return "int";
                        case "public":
                            return "pub";
                        default:
                            throw new NotImplementedException($"Project '{project}' within organization '{organization}' has no visibility mapping.");
                    }
                default:
                    throw new NotImplementedException($"Organization '{organization}' has no visibility mapping.");
            }
        }
    }

    public class Permission
    {
        public Permission(string identityDescriptor, string role)
        {
            IdentityDescriptor = identityDescriptor;
            Role = role;
        }

        public string IdentityDescriptor { get; set; }

        public string Role { get; set; }
    }

    /// <summary>
    /// Represents a feed view
    /// </summary>
    public class FeedView
    {
        public string Visibility { get; set; }
    }

    /// <summary>
    /// Represents the body of a request sent when creating a new feed.
    /// </summary>
    /// <remarks>>
    /// When creating a new feed, we want to set up permissions based on the org and project.
    /// Right now, only dnceng's public and internal projects are supported.
    /// New feeds automatically get the feed administrators and project collection administrators as owners,
    /// but we want to automatically add some additional permissions so that the build services can push to them,
    /// and aadTenant users can read from them.
    /// </remarks>
    public class AzureDevOpsArtifactFeed
    {
        public AzureDevOpsArtifactFeed(string name, string organization, string project)
        {
            Name = name;
            switch (organization)
            {
                case "dnceng":
                    switch (project)
                    {
                        case "public":
                        case "internal":
                            Permissions = new List<Permission>
                            {
                                // Project Collection Build Service
                                new Permission("Microsoft.TeamFoundation.ServiceIdentity;116cce53-b859-4624-9a95-934af41eccef:Build:7ea9116e-9fac-403d-b258-b31fcf1bb293", "contributor"),
                                // internal Build Service
                                new Permission("Microsoft.TeamFoundation.ServiceIdentity;116cce53-b859-4624-9a95-934af41eccef:Build:b55de4ed-4b5a-4215-a8e4-0a0a5f71e7d8", "contributor"),
                                // Project administrators
                                new Permission("Microsoft.TeamFoundation.Identity;S-1-9-1551374245-1349140002-2196814402-2899064621-3782482097-0-0-0-0-1", "administrator"),
                            };
                            break;
                        default:
                            throw new NotImplementedException($"Project '{project}' within organization '{organization}' contains no feed permissions information.");
                    }
                    break;
                default:
                    throw new NotImplementedException($"Organization '{organization}' contains no feed permissions information.");

            }
        }

        public string Name { get; set; }

        public List<Permission> Permissions { get; private set; }
    }
}
