// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Net.Http;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Newtonsoft.Json;

namespace Microsoft.DotNet.Build.Tasks.Links
{
    /// <summary>
    /// Computes the checksum for a single file.
    /// </summary>
    public class CreateAkaMSLink : AkaMSLinkBase
    {
        [Required]
        public string Owners { get; set; }
        [Required]
        public string CreatedBy { get; set; }
        [Required]
        public string TargetUrl { get; set; }
        [Required]
        public string ShortUrl { get; set; }
        public string Description { get; set; } = "";
        public string GroupOwner { get; set; }
        // If the link already exists, update it.  If false and the link already exists, task will fail.
        public bool Overwrite { get; set; } = true;

        public override bool Execute()
        {
            try
            {
                using (HttpClient client = GetClient())
                {
                    // Check whether the link exists if overwrite is used
                    bool exists = false;
                    if (Overwrite)
                    {
                        var existsCheck = client.GetAsync($"{this.apiTargetUrl}/{ShortUrl}").Result;
                        if (existsCheck.StatusCode != System.Net.HttpStatusCode.NotFound)
                        {
                            if (!existsCheck.IsSuccessStatusCode)
                            {
                                Log.LogError($"aka.ms GET api returned unexpected result: {existsCheck.Content.ReadAsStringAsync().Result}");
                                return false;
                            }

                            var existingLink = Newtonsoft.Json.Linq.JObject.Parse(existsCheck.Content.ReadAsStringAsync().Result);
                            if ((string)existingLink["targetUrl"] == TargetUrl)
                            {
                                Log.LogMessage(MessageImportance.Low, $"aka.ms/{ShortUrl} already targets {TargetUrl}, skipping update.");
                                return true;
                            }
                            else
                            {
                                Log.LogMessage(MessageImportance.Low, $"aka.ms/{ShortUrl} exists but doesn't target {TargetUrl}, skipping update.");
                                exists = true;
                            }
                        }
                    }

                    if (!exists)
                    {
                        var newLink = new
                        {
                            isVanity = !string.IsNullOrEmpty(ShortUrl),
                            shortUrl = ShortUrl,
                            owners = Owners,
                            targetUrl = TargetUrl,
                            createdBy = CreatedBy,
                            lastModifiedBy = CreatedBy,
                            description = Description,
                            groupOwner = GroupOwner
                        };

                        var response = client.PostAsync(this.apiTargetUrl,
                            new StringContent(JsonConvert.SerializeObject(newLink), Encoding.UTF8, "application/json")).Result;
                        if (response.StatusCode != System.Net.HttpStatusCode.Created)
                        {
                            Log.LogError($"Error creating aka.ms/{ShortUrl}->{TargetUrl} link: {response.Content.ReadAsStringAsync().Result}");
                            return false;
                        }
                        else
                        {
                            Log.LogMessage(MessageImportance.Normal, $"Created aka.ms/{ShortUrl}->{TargetUrl} link.");
                            return true;
                        }
                    }
                    else
                    {
                        // Create the POST body
                        var updateLink = new
                        {
                            targetUrl = TargetUrl,
                            owners = Owners,
                            lastModifiedBy = CreatedBy
                        };

                        var response = client.PutAsync($"{this.apiTargetUrl}/{ShortUrl}",
                            new StringContent(JsonConvert.SerializeObject(updateLink), Encoding.UTF8, "application/json")).Result;
                        // Supposedly 404 is a successful status code for an update (link not found), but that seems really
                        // odd so it is excluded from the valid status codes.
                        if (response.StatusCode != System.Net.HttpStatusCode.Accepted &&
                            response.StatusCode != System.Net.HttpStatusCode.NoContent)
                        {
                            Log.LogError($"Error updating aka.ms/{ShortUrl}->{TargetUrl} link: {response.Content.ReadAsStringAsync().Result}");
                            return false;
                        }
                        else
                        {
                            Log.LogMessage(MessageImportance.Normal, $"aka.ms/{ShortUrl} was updated to target {TargetUrl}.");
                            return true;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log.LogError($"Error creating/updating aka.ms/{ShortUrl}->{TargetUrl} link: {e.ToString()}");
                return false;
            }
        }
    }
}
