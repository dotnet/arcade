// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.Build.Framework;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Microsoft.DotNet.Build.Tasks.Versioning
{
    public class GenerateVersioningDate : BuildTask
    {
        /// <summary>
        /// The passed in date that will be used to generate a version. (yyyy-MM-dd format)
        /// </summary>
        [Required]
        public string SeedDate { get; set; }

        /// <summary>
        /// Optional parameter containing the Official Build Id. We'll use this to get the revision number out and use it as BuildNumberMinor.
        /// </summary>
        public string OfficialBuildId { get; set; }

        /// <summary>
        /// 'true' for Semantic Versioning 1.0 format. Leading zeros are allowed on fields.
        /// 'false' that means that Semantic Versioning 2.0 will be used. No leading zeros on any field.
        /// </summary>
        public bool IncludePadding { get; set; }

        /// <summary>
        /// Padding for the version number. Must be 5 or bigger.
        /// </summary>
        public int Padding { get; set; }

        /// <summary>
        /// If basing off of internal builds version format is not required, this optional parameter lets you pass in a comparison date.
        /// </summary>
        public string ComparisonDate { get; set; }

        /// <summary>
        /// The Major Version that will be produced given a SeedDate.
        /// </summary>
        [Output]
        public string GeneratedShortDate { get; set; }

        /// <summary>
        /// The Revision number that will be produced from the BuildNumber.
        /// </summary>
        [Output]
        public string GeneratedRevision { get; set; }


        private const string DateFormat = "yyyy-MM-dd";
        private const string LastModifiedTimeDateFormat = "yyyy-MM-dd HH:mm:ss.FFFFFFF";
        private CultureInfo enUS = new CultureInfo("en-US");

        public override bool Execute()
        {
            // Leading zeros are only allowed in SemVer 1.0
            if (IncludePadding)
            {
                if (Padding == 0)
                {
                    Padding = 5;
                }
                else if (Padding < 5)
                {
                    Log.LogWarning($"The specified Padding '{Padding}' has to be equal to or greater than 5. Using 5 as a default now.");
                    Padding = 5;
                }
            }

            // If OfficialBuildId is passed in, then use that to calculate the version and revision.
            if (string.IsNullOrEmpty(OfficialBuildId))
            {
                GeneratedRevision = "0";
            }
            else
            {
                return SetVersionAndRevisionFromBuildId(OfficialBuildId);
            }

            DateTime date;
            GeneratedShortDate = string.Empty;
            if (!DateTime.TryParseExact(SeedDate, DateFormat, enUS, DateTimeStyles.AssumeLocal, out date))
            {
                // Check if the timestamp matches the LastModifiedTimeDateFormat
                if (!DateTime.TryParseExact(SeedDate, LastModifiedTimeDateFormat, enUS, DateTimeStyles.AssumeLocal, out date))
                {
                    Log.LogError("The seed date '{0}' is not valid. Please specify a date in the short format.({1})", SeedDate, DateFormat);
                    return false;
                }
            }
            //Convert Date to UTC to converge
            date = date.ToUniversalTime();
            GeneratedShortDate = CreateShortDate(date, ComparisonDate);
            if (string.IsNullOrEmpty(GeneratedShortDate))
            {
                Log.LogError("The date '{0}' is not valid. Please pass in a date after {1}.", SeedDate, ComparisonDate);
                return false;
            }

            return true;
        }

        private bool SetVersionAndRevisionFromBuildId(string buildId)
        {
            Regex regex = new Regex(@"(\d{8})[\-\.](\d+)$");
            Match match = regex.Match(buildId);

            if (match.Success && match.Groups.Count > 2)
            {
                string dateFormat = "yyyyMMdd";
                DateTime dateFromBuildId;

                if (!DateTime.TryParseExact(match.Groups[1].Value, dateFormat, enUS, DateTimeStyles.AssumeLocal, out dateFromBuildId))
                {
                    Log.LogError("The OfficialBuildId doesn't follow the expected({0}.rr) format: '{1}'", dateFormat, match.Groups[1].Value);
                    return false;
                }

                dateFromBuildId = dateFromBuildId.ToUniversalTime();
                GeneratedShortDate = CreateShortDate(dateFromBuildId, ComparisonDate);
                GeneratedRevision = match.Groups[2].Value;

                if (!IncludePadding)
                {
                    GeneratedRevision = GeneratedRevision.TrimStart(new Char[] { '0' });

                    if (GeneratedRevision.Equals(""))
                    {
                        GeneratedRevision = "0";
                    }
                }

                return true;
            }

            Log.LogError("Error: Invalid OfficialBuildId was passed: '{0}'", buildId);
            return false;
        }

        private string CreateShortDate(DateTime seedDate, string comparisonDate)
        {
            DateTime compareDate;

            if (string.IsNullOrEmpty(comparisonDate))
            {
                /*
                 * We need to ensure that our build numbers are higher that what we used to ship internal builds so this date
                 * will make that possible.
                 */
                compareDate = new DateTime(1996, 4, 1, 0, 0, 0, DateTimeKind.Utc);
            }
            else
            {
                if (!DateTime.TryParseExact(comparisonDate, DateFormat, enUS, DateTimeStyles.AssumeLocal, out compareDate))
                {
                    Log.LogError("The comparison date '{0}' is not valid. Please specify a date in the short format.({1})", comparisonDate, DateFormat);
                }

                //Convert to UTC to converge
                compareDate = compareDate.ToUniversalTime();
            }

            int months = (seedDate.Year - compareDate.Year) * 12 + seedDate.Month - compareDate.Month;

            if (months > 0) //only allow dates after comparedate
            {
                if (IncludePadding)
                {
                    return string.Format("{0}{1}", months.ToString("D" + (Padding - 2)), seedDate.Day.ToString("D2"));
                }
                else
                {
                    return string.Format("{0}{1}", months.ToString("D"), seedDate.Day.ToString("D2"));
                }
            }
            else
            {
                Log.LogError("Error: Comparison date is in the same month as the seed date");
                return string.Empty;
            }
        }
    }
}
