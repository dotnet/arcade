// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.Build.Framework;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Build.Utilities;

namespace Microsoft.DotNet.Build.Tasks.Versioning
{
    public class GenerateVersion : BuildTask
    {
        public static string SHA = String.Empty;
        public static string Date = String.Empty;
        public static string Revision = String.Empty;

        public string OfficialBuildId { get; set; } = String.Empty;
        public DateTime BaselineDate { get; set; } = new DateTime(1996, 4, 1, 0, 0, 0, DateTimeKind.Utc);
        public int Padding { get; set; } = 5;
        public bool IncludePadding { get; set; } = false;


        [Output]
        public string GeneratedShortDate { get; set; }

        [Output]
        public string GeneratedRevision { get; set; }

        [Output]
        public string GeneratedShortSha { get; set; }


        public override bool Execute()
        {
            bool emptySha = String.IsNullOrEmpty(SHA);
            bool emptyDate = String.IsNullOrEmpty(Date);
            bool emptyRevision = String.IsNullOrEmpty(Revision);

            if (emptySha || emptyDate || emptyRevision)
            {
                GitInfo gitInfo = new GitInfo();
                gitInfo.BuildEngine = this.BuildEngine;
                gitInfo.Execute();

                var BuildIdParsedStatus = GetDateAndRevisionFromBuildId(OfficialBuildId, out var TempDate, out var TempRevision);

                if (emptySha)
                {
                    SHA = !String.IsNullOrEmpty(gitInfo.HeadCommitSHA) ? gitInfo.HeadCommitSHA : "NOSHA";
                }

                if (emptyDate)
                {
                    if (BuildIdParsedStatus) Date = TempDate;
                    else if (gitInfo.HeadCommitDate != default(DateTime)) Date = CreateShortDate(gitInfo.HeadCommitDate);
                    else Date = CreateShortDate(DateTime.UtcNow);
                }

                if (emptyRevision)
                {
                    if (BuildIdParsedStatus) Revision = TempRevision;
                    else Revision = "00";
                }
            }

            GeneratedShortDate = Date;
            GeneratedShortSha = SHA;
            GeneratedRevision = Revision;

            return true;
        }

        private bool GetDateAndRevisionFromBuildId(string buildId, out string Date, out string Revision)
        {
            Regex regex = new Regex(@"(\d{8})[\-\.](\d+)$");
            Match match = regex.Match(buildId);

            Date = String.Empty;
            Revision = String.Empty;

            if (String.IsNullOrEmpty(buildId)) return false;

            if (match.Success && match.Groups.Count > 2)
            {
                if (DateTime.TryParseExact(match.Groups[1].Value, "yyyyMMdd", new CultureInfo("en-US"), DateTimeStyles.AdjustToUniversal, out DateTime dateFromBuildId))
                {
                    Date = CreateShortDate(dateFromBuildId);
                    Revision = match.Groups[2].Value;

                    return true;
                }
            }

            Log.LogError("Error: Invalid OfficialBuildId was passed: '{0}'. Expected format is '{1}'.", buildId, regex.ToString());

            return false;
        }

        private string CreateShortDate(DateTime BuildDate)
        {
            int months = (BuildDate.Year - BaselineDate.Year) * 12 + BuildDate.Month - BaselineDate.Month;

            if (months > 0)
            {
                return IncludePadding
                    ? string.Format("{0}{1}", months.ToString("D" + (Padding - 2)), BuildDate.Day.ToString("D2"))
                    : string.Format("{0}{1}", months.ToString("D"), BuildDate.Day.ToString("D2"));
            }
            else
            {
                Log.LogError("Error: Comparison date is in the same month as the seed date");
                return string.Empty;
            }
        }
    }
}
