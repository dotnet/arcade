// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using NuGet.Client;
using NuGet.ContentModel;
using NuGet.Frameworks;
using NuGet.RuntimeModel;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.DotNet.Build.Tasks.TargetFramework.Sdk
{
    public class TargetFrameworkResolver
    {
        private readonly ManagedCodeConventions _conventions;
        private readonly PatternSet _configStringPattern;

        public TargetFrameworkResolver(string runtimeGraph)
        {
            _conventions = new ManagedCodeConventions(JsonRuntimeFormat.ReadRuntimeGraph(runtimeGraph));
            _configStringPattern = new PatternSet(
                _conventions.Properties,
                groupPatterns: new PatternDefinition[]
                {
                    new PatternDefinition("{tfm}/"),
                    new PatternDefinition("{tfm}-{rid}/")
                },
                pathPatterns: new PatternDefinition[]
                {
                    new PatternDefinition("{tfm}/"),
                    new PatternDefinition("{tfm}-{rid}/")
                });
        }

        public string GetBestSupportedTargetFramework(IEnumerable<string> supportedTargetFrameworks, string buildTargetFramework)
        {
            var contentCollection = new ContentItemCollection();
            contentCollection.Load(supportedTargetFrameworks.Select(t => t + '/').ToArray());

            string[] splitStrings = buildTargetFramework.Split('-');
            string targetFramework = splitStrings[0];
            string osGroup = splitStrings.Length > 1 ? splitStrings[1] : string.Empty;

            SelectionCriteria criteria = _conventions.Criteria.ForFrameworkAndRuntime(NuGetFramework.Parse(targetFramework), osGroup);
            string bestTargetFrameworkString = contentCollection.FindBestItemGroup(criteria, _configStringPattern)?.Items[0].Path;

            // This is a temporary fix and we will lowercase all the occurences in runtime in the future.
            if (bestTargetFrameworkString == null && (osGroup == "FreeBSD" || osGroup == "OSX" || osGroup == "NetBSD" || osGroup == "Linux"))
            {
                criteria = _conventions.Criteria.ForFrameworkAndRuntime(NuGetFramework.Parse(targetFramework), "Unix");
                bestTargetFrameworkString = contentCollection.FindBestItemGroup(criteria, _configStringPattern)?.Items[0].Path;
            }

            return bestTargetFrameworkString?.Remove(bestTargetFrameworkString.Length - 1);
        }
    }
}
