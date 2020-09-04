// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NuGet.Packaging.Core;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace Microsoft.DotNet.SourceBuild.Tasks.UsageReport
{
    public class ValidateUsageAgainstBaseline : Task
    {
        [Required]
        public string DataFile { get; set; }

        /// <summary>
        /// The prebuilt baseline: an XML file that lists allowed prebuilt usage.
        /// </summary>
        public string BaselineDataFile { get; set; }

        /// <summary>
        /// A hint path used in error messages to tell the user where to update the prebuilt
        /// baseline data if a baseline validation error occurs. If there is no baseline data file
        /// at all yet, the error indicates that one should be created at this location if the
        /// prebuilt usage should be permitted.
        /// </summary>
        [Required]
        public string BaselineDataUpdateHintFile { get; set; }

        [Required]
        public string OutputBaselineFile { get; set; }

        [Required]
        public string OutputReportFile { get; set; }

        public bool AllowTestProjectUsage { get; set; }

        public override bool Execute()
        {
            var used = UsageData.Parse(XElement.Parse(File.ReadAllText(DataFile)));

            string baselineText = string.IsNullOrEmpty(BaselineDataFile)
                ? "<UsageData />"
                : File.ReadAllText(BaselineDataFile);

            var baseline = UsageData.Parse(XElement.Parse(baselineText));

            Comparison<PackageIdentity> diff = Compare(
                used.Usages.Select(u => u.GetIdentityWithoutRid()).Distinct(),
                baseline.Usages.Select(u => u.GetIdentityWithoutRid()).Distinct());

            var report = new XElement("BaselineComparison");

            bool tellUserToUpdateBaseline = false;

            if (diff.Added.Any())
            {
                tellUserToUpdateBaseline = true;
                Log.LogError(
                    $"{diff.Added.Length} new packages used not in baseline! See report " +
                    $"at {OutputReportFile} for more information. Package IDs are:\n" +
                    string.Join("\n", diff.Added.Select(u => u.ToString())));

                // In the report, list full usage info, not only identity.
                report.Add(
                    new XElement(
                        "New",
                        used.Usages
                            .Where(u => diff.Added.Contains(u.GetIdentityWithoutRid()))
                            .Select(u => u.ToXml())));
            }
            if (diff.Removed.Any())
            {
                tellUserToUpdateBaseline = true;
                Log.LogMessage(
                    MessageImportance.High,
                    $"{diff.Removed.Length} packages in baseline weren't used!");

                report.Add(new XElement("Removed", diff.Removed.Select(id => id.ToXElement())));
            }
            if (diff.Unchanged.Any())
            {
                Log.LogMessage(
                    MessageImportance.High,
                    $"{diff.Unchanged.Length} packages used as expected in the baseline.");
            }

            if (!AllowTestProjectUsage)
            {
                Usage[] testProjectUsages = used.Usages
                    .Where(WriteUsageReports.IsTestUsageByHeuristic)
                    .ToArray();

                if (testProjectUsages.Any())
                {
                    string[] projects = testProjectUsages
                        .Select(u => u.AssetsFile)
                        .Distinct()
                        .ToArray();

                    Log.LogError(
                        $"{testProjectUsages.Length} forbidden test usages found in " +
                        $"{projects.Length} projects:\n" +
                        string.Join("\n", projects));
                }
            }

            // Simplify the used data to what is necessary for a baseline, to reduce file size.
            foreach (var usage in used.Usages)
            {
                usage.AssetsFile = null;
            }
            used.Usages = used.Usages.Distinct().ToArray();

            Directory.CreateDirectory(Path.GetDirectoryName(OutputBaselineFile));
            File.WriteAllText(OutputBaselineFile, used.ToXml().ToString());

            Directory.CreateDirectory(Path.GetDirectoryName(OutputReportFile));
            File.WriteAllText(OutputReportFile, report.ToString());

            if (tellUserToUpdateBaseline)
            {
                string baselineNotFoundWarning = "";
                if (string.IsNullOrEmpty(BaselineDataFile))
                {
                    baselineNotFoundWarning =
                        $"not expected, because no baseline file exists at '{BaselineDataUpdateHintFile}'";
                }
                else
                {
                    baselineNotFoundWarning =
                        $"different from the baseline found at '{BaselineDataFile}'";
                }

                Log.LogWarning(
                    $"Prebuilt usages are {baselineNotFoundWarning}. If it's acceptable to " +
                    "update the baseline, copy the contents of the automatically generated " +
                    $"baseline '{OutputBaselineFile}'.");
            }

            return !Log.HasLoggedErrors;
        }

        private static Comparison<T> Compare<T>(IEnumerable<T> actual, IEnumerable<T> baseline)
        {
            return new Comparison<T>(actual.ToArray(), baseline.ToArray());
        }

        private class Comparison<T>
        {
            public T[] Added { get; }
            public T[] Removed { get; }
            public T[] Unchanged { get; }

            public Comparison(
                IEnumerable<T> actual,
                IEnumerable<T> baseline)
            {
                Added = actual.Except(baseline).ToArray();
                Removed = baseline.Except(actual).ToArray();
                Unchanged = actual.Intersect(baseline).ToArray();
            }
        }
    }
}
