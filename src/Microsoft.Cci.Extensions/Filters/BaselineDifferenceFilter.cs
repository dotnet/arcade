// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.IO;
using Microsoft.Cci.Differs;

namespace Microsoft.Cci.Filters
{
    public class BaselineDifferenceFilter : IDifferenceFilter
    {
        private readonly HashSet<string> _ignoreDifferences;
        private readonly IDifferenceFilter _filter;

        public BaselineDifferenceFilter(IDifferenceFilter filter, string baselineFile)
        {
            _filter = filter;
            _ignoreDifferences = ReadBaselineFile(baselineFile);
        }

        private HashSet<string> ReadBaselineFile(string baselineFile)
        {
            HashSet<string> ignoreList = new HashSet<string>();

            if (!File.Exists(baselineFile))
                return ignoreList;

            foreach (var line in File.ReadAllLines(baselineFile))
            {
                string filteredLine = line;
                int index = filteredLine.IndexOf('#');

                if (index >= 0)
                    filteredLine = filteredLine.Substring(0, index);

                filteredLine = filteredLine.Trim();
                if (string.IsNullOrWhiteSpace(filteredLine))
                    continue;

                ignoreList.Add(filteredLine);
            }

            return ignoreList;
        }

        public bool Include(Difference difference)
        {
            // Is the entire rule ignored?
            if (_ignoreDifferences.Contains(difference.Id))
                return false;

            // Is the specific violation of the rule ignored?
            if (_ignoreDifferences.Contains(difference.ToString()))
                return false;

            return _filter.Include(difference);
        }
    }
}
