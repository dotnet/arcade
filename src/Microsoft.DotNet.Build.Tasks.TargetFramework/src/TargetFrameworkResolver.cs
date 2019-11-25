// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using NuGet.Client;
using NuGet.ContentModel;
using NuGet.Frameworks;
using NuGet.RuntimeModel;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.DotNet.Build.Tasks.TargetFramework
{
    public class TargetFrameworkResolver
    {
        private ManagedCodeConventions _conventions;
        private PatternSet _configStringPattern;

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
            contentCollection.Load(supportedTargetFrameworks.Select(t => t + '/'));

            string targetFramework = buildTargetFramework;
            string osGroup = string.Empty;
            if (buildTargetFramework.Contains("-"))
            {
                string[] splitStrings = buildTargetFramework.Split('-');
                targetFramework = splitStrings[0];
                osGroup = splitStrings[1];
            }

            SelectionCriteria criteria = _conventions.Criteria.ForFrameworkAndRuntime(NuGetFramework.Parse(targetFramework), osGroup);
            string bestTargetFrameworkString = contentCollection.FindBestItemGroup(criteria, _configStringPattern)?.Items[0].Path;
            return bestTargetFrameworkString == null ? null : bestTargetFrameworkString.Remove(bestTargetFrameworkString.Length - 1);
        }
    }
}
