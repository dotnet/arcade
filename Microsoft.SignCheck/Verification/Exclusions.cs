using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Microsoft.SignCheck.Verification
{
    public class Exclusions
    {
        private static readonly char[] _wildCards = new char[] { '*', '?' };
        private List<Exclusion> _exclusions = new List<Exclusion>();

        public int Count
        {
            get
            {
                return _exclusions.Count;
            }
        }

        public Exclusions()
        {

        }

        /// <summary>
        /// Creates a collection of <see cref="Exclusion"/>s from a text file. 
        /// </summary>
        /// <param name="path">Path to a file that contains exclusion entries.</param>
        public Exclusions(string path)
        {
            if (File.Exists(path))
            {
                using (StreamReader fileReader = File.OpenText(path))
                {
                    string line = fileReader.ReadLine();

                    while (line != null)
                    {
                        if (!String.IsNullOrEmpty(line))
                        {
                            Add(new Exclusion(line));
                        }
                        line = fileReader.ReadLine();
                    }
                }
            }
        }

        public void Add(Exclusion exclusion)
        {
            if (!_exclusions.Contains(exclusion))
            {
                _exclusions.Add(exclusion);
            }
        }

        public void Clear()
        {
            _exclusions.Clear();
        }

        public bool Contains(Exclusion exclusion)
        {
            return _exclusions.Contains(exclusion);
        }

        /// <summary>
        /// Returns true if any <see cref="Exclusion.FilePatterns"/> matches the value of <paramref name="path"/>.
        /// </summary>
        /// <param name="path">The value to match against any <see cref="Exclusion.FilePatterns"/>.</param>
        /// <returns></returns>
        public bool IsFileExcluded(string path)
        {
            return _exclusions.Any(e => IsMatch(e.FilePatterns, path));
        }

        /// <summary>
        /// Returns true if any <see cref="Exclusion.ParentFiles"/> matches the value of <paramref name="parent"/>.
        /// </summary>
        /// <param name="parent">The value to match against any <see cref="Exclusion.ParentFiles"/>.</param>
        /// <returns></returns>
        public bool IsParentExcluded(string parent)
        {
            return _exclusions.Any(e => IsMatch(e.ParentFiles, parent));
        }

        private bool IsMatch(string[] patterns, string value)
        {
            return patterns.Any(p => IsMatch(p, value));
        }

        private bool IsMatch(string pattern, string value)
        {
            if (pattern == null || value == null)
            {
                return false;
            }
            if (pattern.IndexOfAny(_wildCards) > -1)
            {
                var regexPattern = Utils.ConvertToRegexPattern(pattern);
                return Regex.IsMatch(value, regexPattern, RegexOptions.IgnoreCase);
            }
            else
            {
                return String.Equals(pattern, value, StringComparison.OrdinalIgnoreCase);
            }
        }

        public bool Remove(Exclusion exclusion)
        {
            return false;
        }
    }
}
