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

        [Required]
        public bool IsInternal { get; set; }

        [Required]
        public string RepositoryName { get; set; }

        [Required]
        public string CommitSha { get; set; }

        [Required]
        public string PersonalAccessToken { get; set; }

        public string FeedsApiVersion { get; set; } = "5.0-preview.1";

        public static string AzureDevOpsOrg { get; set; } = "dnceng";

        private readonly string FeedsBaseUrl = $"https://feeds.dev.azure.com/{AzureDevOpsOrg}/";

        public override bool Execute()
        {
            return ExecuteAsync().GetAwaiter().GetResult();
        }

        private async Task<bool> ExecuteAsync()
        {
            try
            {
                JsonSerializerSettings _serializerSettings = new JsonSerializerSettings
                {
                    ContractResolver = new CamelCasePropertyNamesContractResolver(),
                    NullValueHandling = NullValueHandling.Ignore
                };

                string accessType = IsInternal ? "internal" : "public";
                string publicSegment = IsInternal ? string.Empty : "public/";
                string feedName = await CalculateUniqueFeedName();

                Log.LogMessage(MessageImportance.High, $"Creating the new {accessType} Azure DevOps artifacts feed '{feedName}'...");

                using (HttpClient client = new HttpClient(new HttpClientHandler { CheckCertificateRevocationList = true })
                {
                    BaseAddress = new Uri(FeedsBaseUrl)
                })
                {
                    client.DefaultRequestHeaders.Add(
                        "Accept",
                        $"application/json;api-version={FeedsApiVersion}");
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                        "Basic",
                        Convert.ToBase64String(Encoding.ASCII.GetBytes(string.Format("{0}:{1}", "", PersonalAccessToken))));

                    AzureDevOpsArtifactFeed newFeed = new AzureDevOpsArtifactFeed(feedName);

                    // Mimic the permissions added to a feed when created in the browser
                    newFeed.Permissions.Add(new Permission("Microsoft.TeamFoundation.ServiceIdentity;116cce53-b859-4624-9a95-934af41eccef:Build:b55de4ed-4b5a-4215-a8e4-0a0a5f71e7d8", 3));
                    newFeed.Permissions.Add(new Permission("Microsoft.TeamFoundation.ServiceIdentity;116cce53-b859-4624-9a95-934af41eccef:Build:7ea9116e-9fac-403d-b258-b31fcf1bb293", 3));
                    newFeed.Permissions.Add(new Permission("Microsoft.TeamFoundation.Identity;S-1-9-1551374245-1349140002-2196814402-2899064621-3782482097-0-0-0-0-1", 4));
                    newFeed.Permissions.Add(new Permission("Microsoft.TeamFoundation.Identity;S-1-9-1551374245-1846651262-2896117056-2992157471-3474698899-1-2052915359-1158038602-2757432096-2854636005", 4));

                    string body = JsonConvert.SerializeObject(newFeed, _serializerSettings);

                    HttpRequestMessage postMessage = new HttpRequestMessage(HttpMethod.Post, $"{publicSegment}_apis/packaging/feeds");
                    postMessage.Content = new StringContent(body, Encoding.UTF8, "application/json");

                    HttpResponseMessage response = await client.SendAsync(postMessage);

                    if (response.StatusCode != HttpStatusCode.Created)
                    {
                        throw new Exception($"Feed '{feedName}' was not created. Request failed with status code {response.StatusCode}.");
                    }

                    TargetFeedURL = $"https://{AzureDevOpsOrg}.pkgs.visualstudio.com/{publicSegment}_packaging/{feedName}";
                }

            }
            catch (Exception e)
            {
                Log.LogErrorFromException(e, true);
            }

            return !Log.HasLoggedErrors;
        }

        private async Task<string> CalculateUniqueFeedName()
        {
            string accessId = IsInternal ? "int" : "pub";
            string feedName = $"darc-{accessId}-{RepositoryName}-{CommitSha}";
            string publicSegment = IsInternal ? string.Empty : "public/";
            int subVersion = 0;
            bool feedExists = true;

            do
            {
                using (HttpClient client = new HttpClient(new HttpClientHandler { CheckCertificateRevocationList = true })
                {
                    BaseAddress = new Uri(FeedsBaseUrl)
                })
                {
                    client.DefaultRequestHeaders.Add(
                        "Accept",
                        $"application/json;api-version={FeedsApiVersion}");
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                        "Basic",
                        Convert.ToBase64String(Encoding.ASCII.GetBytes(string.Format("{0}:{1}", "", PersonalAccessToken))));

                    HttpRequestMessage getMessage = new HttpRequestMessage(HttpMethod.Get, $"{publicSegment}_apis/packaging/feeds/{feedName}/");
                    HttpResponseMessage response = await client.SendAsync(getMessage);

                    if (response.StatusCode == HttpStatusCode.NotFound)
                    {
                        feedExists = false;
                    }
                    else if (response.StatusCode == HttpStatusCode.OK)
                    {
                        // In case the feed name already exist, meaning we have the same repo name + commitSha, we append an increasing counter at the end i.e. darc-int-arcade-123456-1
                        if (subVersion > 0)
                        {
                            int index = feedName.LastIndexOf('-');
                            feedName = $"{feedName.Substring(0, index + 1)}{++subVersion}";
                        }
                        else
                        {
                            feedName = $"{feedName}-{++subVersion}";
                        }
                    }
                    else
                    {
                        throw new Exception($"Something failed while tring to check if feed '{feedName}' exists. Status code {response.StatusCode}.");
                    }
                }
  
            }
            while (feedExists);

            return feedName;
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
            Permissions = new List<Permission>();
        }

        public string Name { get; set; }

        public List<Permission> Permissions { get; set; }
    }
}
