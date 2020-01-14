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
            List<string> exactConfigs = supportedTargetFrameworks.Where(t => !t.StartsWith("_")).ToList();
            IEnumerable<string> placeHolderConfigs = supportedTargetFrameworks.Where(t => t.StartsWith("_"))?.Select(t => t.Substring(1));
            
            if (placeHolderConfigs != null)
                exactConfigs.AddRange(placeHolderConfigs);
            
            var contentCollection = new ContentItemCollection();
            contentCollection.Load(exactConfigs.Select(t => t + '/').ToArray());

            string[] splitStrings = buildTargetFramework.Split('-');
            string targetFramework = splitStrings[0];
            string osGroup = splitStrings.Length > 1 ? splitStrings[1] : string.Empty;

            SelectionCriteria criteria = _conventions.Criteria.ForFrameworkAndRuntime(NuGetFramework.Parse(targetFramework), osGroup);
            string bestTargetFrameworkString = contentCollection.FindBestItemGroup(criteria, _configStringPattern)?.Items[0].Path;                  
            string bestTargetFrameworkStringWithoutSlash = bestTargetFrameworkString?.Remove(bestTargetFrameworkString.Length - 1);
            return placeHolderConfigs != null && placeHolderConfigs.Contains(bestTargetFrameworkStringWithoutSlash) ? null : bestTargetFrameworkStringWithoutSlash;
        }
    }
}
