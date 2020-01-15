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
    /// <summary>
    /// This class uses NuGet's asset selection logic to choose the best TargetFramework given the list of supported TargetFrameworks.
    /// This behaves in a same way as NuGet selects lib files from a nuget package for a particular TargetFramework.
    /// </summary>
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
                    // In order to use Nuget's asset allocation, the input needs to file paths and should contain a trailing slash.
                    new PatternDefinition("{tfm}/"),
                    new PatternDefinition("{tfm}-{rid}/")
                },
                pathPatterns: new PatternDefinition[]
                {
                    new PatternDefinition("{tfm}/"),
                    new PatternDefinition("{tfm}-{rid}/")
                });
        }

        public string GetBestSupportedTargetFramework(IEnumerable<string> supportedTargetFrameworks, string targetFramework)
        {
            List<string> exactConfigs = supportedTargetFrameworks.Where(t => !t.StartsWith("_")).ToList();
            IEnumerable<string> placeHolderConfigs = supportedTargetFrameworks.Where(t => t.StartsWith("_")).Select(t => t.Substring(1));
            
            if (placeHolderConfigs.Any())
                exactConfigs.AddRange(placeHolderConfigs);
            
            var contentCollection = new ContentItemCollection();
            contentCollection.Load(exactConfigs.Select(t => t + '/').ToArray());

            string[] splitStrings = targetFramework.Split('-');
            string targetFrameworkWithoutSuffix = splitStrings[0];
            string targetFrameworkSuffix = splitStrings.Length > 1 ? splitStrings[1] : string.Empty;

            SelectionCriteria criteria = _conventions.Criteria.ForFrameworkAndRuntime(NuGetFramework.Parse(targetFrameworkWithoutSuffix), targetFrameworkSuffix);
            string bestTargetFrameworkString = contentCollection.FindBestItemGroup(criteria, _configStringPattern)?.Items[0].Path;                  
            string bestTargetFrameworkStringWithoutSlash = bestTargetFrameworkString?.Remove(bestTargetFrameworkString.Length - 1);
            return placeHolderConfigs.Any() && placeHolderConfigs.Contains(bestTargetFrameworkStringWithoutSlash) ? null : bestTargetFrameworkStringWithoutSlash;
        }
    }
}
