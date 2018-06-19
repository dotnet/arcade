// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Text.RegularExpressions;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.DotNet.Build.Tasks.Versioning
{
    public class GenerateVersion : Task
    {
        /// <summary>
        /// We use these fields as a form of caching for the generated
        /// output fields. That's why they are static properties.
        /// </summary>
        private static string s_shortSha = String.Empty;
        private static string s_shortDate = String.Empty;
        private static string s_revision = String.Empty;

        /// <summary>
        /// The size of the padding for the GeneratedShortDate and GeneratedRevision fields.
        /// </summary>
        private readonly int _datePadding = 5;
        private readonly int _revisionPadding = 2;

        /// <summary>
        ///  Optional parameter. When informed must be on the format yyyymmdd[-.]dd.
        ///  For instance, 20180618-01, 20180618-13, etc.
        /// </summary>
        public string OfficialBuildId { get; set; } = String.Empty;

        /// <summary>
        /// The GeneratedShortDate includes the number of months between current 
        /// date and a base date. BaselineDate is the base date to compute that.
        /// </summary>
        public DateTime BaselineDate { get; set; } = new DateTime(1996, 4, 1, 0, 0, 0, DateTimeKind.Utc);

        /// <summary>
        /// Specify whether to include padding on GenerateShortDate and GeneratedRevision. By default
        /// no padding is used. Currently padding size is fixed on 5 and 2, respectively.
        /// </summary>
        public bool IncludePadding { get; set; } = false;

        [Output]
        public string GeneratedShortDate { get; set; }

        [Output]
        public string GeneratedRevision { get; set; }

        [Output]
        public string GeneratedShortSha { get; set; }

        public override bool Execute()
        {
            bool emptySha = String.IsNullOrEmpty(s_shortSha);
            bool emptyDate = String.IsNullOrEmpty(s_shortDate);
            bool emptyRevision = String.IsNullOrEmpty(s_revision);

            if (emptySha || emptyDate || emptyRevision)
            {
                GitInfo gitInfo = new GitInfo();
                gitInfo.BuildEngine = this.BuildEngine;

                try
                {
                    gitInfo.Execute();
                }
                catch (Exception ex)
                {
                    Log.LogError($"Problems retrieving branch information from Git: {ex.Message}");
                    return false;
                }

                var buildIdParsedCorrectly = GetDateAndRevisionFromBuildId(OfficialBuildId, out var tempDate, out var tempRevision);

                if (emptySha)
                {
                    s_shortSha = !String.IsNullOrEmpty(gitInfo.HeadCommitSHA) ? gitInfo.HeadCommitSHA : "NOSHA";
                }

                if (emptyDate)
                {
                    if (buildIdParsedCorrectly)
                    {
                        s_shortDate = tempDate;
                    }
                    else
                    {
                        s_shortDate = CreateShortDate(gitInfo.HeadCommitDate);
                    }
                }

                if (emptyRevision)
                {
                    s_revision = buildIdParsedCorrectly ? tempRevision : "0";
                }
            }

            GeneratedShortDate = AdjustPadding(s_shortDate, _datePadding);
            GeneratedShortSha = s_shortSha;
            GeneratedRevision = AdjustPadding(s_revision, _revisionPadding);

            return true;
        }

        private bool GetDateAndRevisionFromBuildId(string buildId, out string date, out string revision)
        {
            date = String.Empty;
            revision = String.Empty;

            if (String.IsNullOrEmpty(buildId)) return false;

            // We might need to loosen the "8" at some point to allow for other formats.
            Regex regex = new Regex(@"(\d{8})[\-\.](\d+)$");
            Match match = regex.Match(buildId);

            if (match.Success)
            {
                if (DateTime.TryParseExact(match.Groups[1].Value, "yyyyMMdd", new CultureInfo("en-US"), DateTimeStyles.AdjustToUniversal, out DateTime dateFromBuildId))
                {
                    date = CreateShortDate(dateFromBuildId);
                    revision = match.Groups[2].Value;

                    return true;
                }
            }

            Log.LogError("Error: Invalid OfficialBuildId was passed: '{0}'. Expected format is '{1}'.", buildId, regex.ToString());

            return false;
        }

        private string CreateShortDate(DateTime buildDate)
        {
            if (buildDate != default(DateTime))
            {
                buildDate = DateTime.UtcNow;
            }

            int months = (buildDate.Year - BaselineDate.Year) * 12 + buildDate.Month - BaselineDate.Month;

            if (months > 0)
            {
                return string.Format("{0}{1}", months, buildDate.Day.ToString("D2"));
            }
            else
            {
                Log.LogError("Error: Comparison date is in the same month as the seed date");
                return string.Empty;
            }
        }

        private string AdjustPadding(string input, int padding)
        {
            var trimmed = input.TrimStart(new Char[] { '0' });

            if (IncludePadding)
            {
                return input.PadLeft(padding, '0');
            }
            else
            {
                return trimmed.Equals("") ? "0" : trimmed;
            }
        }
    }
}
