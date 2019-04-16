// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using MSBuild = Microsoft.Build.Utilities;

namespace Microsoft.DotNet.SDLAutomator
{
    /// <summary>
    /// This task should only ever be used in internal builds
    /// </summary>
    public class InitSDL : MSBuild.Task
    {
        /// <summary>
        /// Name of GitHub repository, e.g. "dotnet/arcade"
        /// </summary>
        [Required]
        public string Repository { get; set; }

        /// <summary>
        /// The root of the sources directory; should be set to $(Build.SourcesDirectory)
        /// </summary>
        [Required]
        public string SourcesDirectory { get; set; }

        /// <summary>
        /// PAT for accessing Azure DevOps API
        /// </summary>
        [Required]
        public string DncengPat { get; set; }

        public override bool Execute()
        {
            Log.LogMessage(MessageImportance.Low, "Inside InitSDL");
            try
            {
                CloneGdn().GetAwaiter().GetResult();
            }
            catch (Exception e)
            {
                Log.LogErrorFromException(e, true);
            }
            return !Log.HasLoggedErrors;
        }

        public async Task CloneGdn()
        {
            Log.LogMessage(MessageImportance.Low, $"Inside CloneGdn");
            try
            {
                // Download the repo's .gdn folder to the sources directory
                using (HttpClient client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/zip"));
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($":{DncengPat}")));
                    var uriBuilder = new UriBuilder("https", "dev.azure.com")
                    {
                        Path = "dnceng/internal/_apis/git/repositories/sdl-tool-cfg/Items",
                        Query = $"path={Uri.EscapeUriString($"/{Repository}/.gdn")}&versionDescriptor[versionOptions]=0&$format=zip&api-version=5.0-preview.1",
                    };

                    using (HttpResponseMessage response = await client.GetAsync(uriBuilder.Uri))
                    {
                        response.EnsureSuccessStatusCode();
                        HttpContent content = response.Content;
                        var stream = await content.ReadAsStreamAsync();
                        FileStream fileStream = new FileStream(Path.Combine(SourcesDirectory, ".gdn.zip"), FileMode.Create);
                        Log.LogMessage(MessageImportance.Low, $"Path: {Path.Combine(SourcesDirectory, ".gdn.zip")}");
                        await content.CopyToAsync(fileStream).ContinueWith(complete => { fileStream.Close(); stream.Close(); });
                    }
                }

                // Unzip the .gdn archive to the sources directory
                ZipFile.ExtractToDirectory(Path.Combine(SourcesDirectory, ".gdn.zip"), SourcesDirectory);
            }
            catch (Exception e)
            {
                Log.LogErrorFromException(e, true);
            }
        }
    }
}
