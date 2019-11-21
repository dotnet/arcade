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
    public class BestTfmResolver
    {
        private ManagedCodeConventions _conventions;
        private PatternSet _configStringPattern;

        public BestTfmResolver(string runtimeGraph, PatternSet PatternSet)
        {
            _conventions = new ManagedCodeConventions(JsonRuntimeFormat.ReadRuntimeGraph(runtimeGraph));

            if (PatternSet == null)
            {
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
        }

        public string GetBestSupportedTfm(IEnumerable<string> supportedTfms, string buildTfm)
        {
            var contentCollection = new ContentItemCollection();
            contentCollection.Load(supportedTfms.Select(t => t + '/'));

            string tfm = buildTfm;
            string osGroup = string.Empty;
            if (buildTfm.Contains("-"))
            {
                string[] splitStrings = buildTfm.Split('-');
                tfm = splitStrings[0];
                osGroup = splitStrings[1];
            }

            SelectionCriteria criteria = _conventions.Criteria.ForFrameworkAndRuntime(NuGetFramework.Parse(tfm), osGroup);
            string bestTfmString = contentCollection.FindBestItemGroup(criteria, _configStringPattern)?.Items[0].Path;
            return bestTfmString == null ? null : bestTfmString.Remove(bestTfmString.Length - 1);
        }
    }
}
