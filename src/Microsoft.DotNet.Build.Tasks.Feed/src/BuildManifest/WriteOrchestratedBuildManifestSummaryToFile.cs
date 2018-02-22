// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.DotNet.VersionTools.BuildManifest.Model;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace Microsoft.DotNet.Build.Tasks.Feed.BuildManifest
{
    public class WriteOrchestratedBuildManifestSummaryToFile : Task
    {
        [Required]
        public string File { get; set; }

        [Required]
        public string ManifestFile { get; set; }

        public string SdkTableTemplateFile { get; set; }

        public string DotNetRuntimeTableTemplateFile { get; set; }

        public string AspNetCoreRuntimeTableTemplateFile { get; set; }

        public override bool Execute()
        {
            string contents = System.IO.File.ReadAllText(ManifestFile);
            OrchestratedBuildModel model = OrchestratedBuildModel.Parse(XElement.Parse(contents));
            EndpointModel blobFeed = model.Endpoints.First(e => e.IsOrchestratedBlobFeed);

            string feedAssetsRoot = blobFeed.Url.Replace("/index.json", "/assets");

            string sdkProductVersion = model.Builds
                .FirstOrDefault(b => b.Name == "cli")
                ?.BuildId;

            string runtimeProductVersion = blobFeed.Artifacts.Blobs
                .FirstOrDefault(b =>
                    b.Id.StartsWith("Runtime/") &&
                    b.Id.EndsWith("/Microsoft.NET.CoreRuntime.2.1.appx"))
                ?.Id.Split('/')[1];

            string aspnetProductVersion = blobFeed.Artifacts.Blobs
                .FirstOrDefault(b =>
                    b.Id.StartsWith("Runtime/") &&
                    b.Id.EndsWith("/aspnetcore_base_runtime.version"))
                ?.Id.Split('/')[1];

            var builder = new StringBuilder();

            builder.Append("## Product build: ");
            builder.AppendLine(model.Identity.ToString());

            if (!string.IsNullOrEmpty(SdkTableTemplateFile) && sdkProductVersion != null)
            {
                builder.AppendLine();
                builder.AppendLine(FillTemplate(
                    SdkTableTemplateFile,
                    feedAssetsRoot,
                    sdkProductVersion));
            }

            if (!string.IsNullOrEmpty(DotNetRuntimeTableTemplateFile) && runtimeProductVersion != null)
            {
                builder.AppendLine();
                builder.AppendLine(FillTemplate(
                    DotNetRuntimeTableTemplateFile,
                    feedAssetsRoot,
                    runtimeProductVersion));
            }

            if (!string.IsNullOrEmpty(AspNetCoreRuntimeTableTemplateFile) && aspnetProductVersion != null)
            {
                builder.AppendLine();
                builder.AppendLine(FillTemplate(
                    AspNetCoreRuntimeTableTemplateFile,
                    feedAssetsRoot,
                    aspnetProductVersion));
            }

            builder.AppendLine();
            builder.AppendLine("### Built Repositories");

            foreach (BuildIdentity build in model.Builds
                .Where(b => b.Name != "anonymous")
                .OrderBy(b => b.Name))
            {
                builder.Append(" * ");
                builder.AppendLine(build.ToString());
            }

            System.IO.File.WriteAllText(File, builder.ToString());

            return !Log.HasLoggedErrors;
        }

        private static string FillTemplate(string templateFile, string feedAssetsRoot, string productVersion)
        {
            return System.IO.File.ReadAllText(templateFile)
                .Replace("{{FeedAssetsRoot}}", feedAssetsRoot)
                .Replace("{{ProductVersion}}", productVersion);
        }
    }
}
