using System;
using System.Collections.Generic;
using System.IO;

namespace Microsoft.SignCheck
{
    public class Exclusion
    {
        private string[] _exclusionParts;
        private const int FilenameIndex = 0;
        private const int ParentFileIndex = 1;
        private const int CommentIndex = 2;
        
        public Exclusion(string exclusion)
        {
            if (!String.IsNullOrEmpty(exclusion))
            {
                _exclusionParts = exclusion.Split(';');
            }
        }

        public string Comment
        {
            get
            {
                return GetExclusionPart(CommentIndex, defaultValue: SignCheckResources.NA);
            }
        }

        public string Filename
        {
            get
            {
                return GetExclusionPart(FilenameIndex);
            }
        }

        public string ParentFile
        {
            get
            {
                return GetExclusionPart(ParentFileIndex);
            }
        }

        /// <summary>
        /// Check if a file is excluded from verification.
        /// </summary>
        /// <param name="filename">The filename to check.</param>
        /// <param name="exclusions">A set of exclusions to check.</param>
        /// <param name="parent">The parent associated with the exclusion.</param>
        /// <param name="comment">Returns.</param>
        /// <returns>Returns true if the filename or filename and parent is found in the set of exclusions, otherwise returns false.</returns>
        public static bool IsExcluded(string filename, Dictionary<string, Exclusion> exclusions, string parent, out string comment)
        {
            comment = SignCheckResources.NA;

            if (exclusions.ContainsKey(filename))
            {
                var parentFile = exclusions[filename].ParentFile;

                if ((String.IsNullOrEmpty(parentFile)) || (String.Equals(parentFile, parent, StringComparison.OrdinalIgnoreCase)))
                {
                    comment = exclusions[filename].Comment;
                    return true;
                }                
            }

            return false;
        }

        /// <summary>
        /// Creates a dictionary of exclusions using the entries in a given file.
        /// </summary>
        /// <param name="path">Path to the file containing the exclusions.</param>
        /// <returns>A dictionary of exclusions. The keys contain the filenames. Returns null if the exclusion file does not exist.</returns>
        public static Dictionary<string, Exclusion> GetExclusionsFromFile(string path)
        {
            var exclusions = new Dictionary<string, Exclusion>();

            if (!File.Exists(path))
            {
                return exclusions;
            }

            using (var sr = File.OpenText(path))
            {
                var line = sr.ReadLine();

                while (line != null)
                {
                    if (!String.IsNullOrEmpty(line))
                    {
                        var exclusion = new Exclusion(line);
                        exclusions.Add(exclusion.Filename, exclusion);
                    }
                    line = sr.ReadLine();
                }
            }

            return exclusions;
        }

        private string GetExclusionPart(int index)
        {
            return GetExclusionPart(index, defaultValue: String.Empty);
        }

        private string GetExclusionPart(int index, string defaultValue)
        {
            if ((_exclusionParts != null) && (index >= 0) && (index < _exclusionParts.Length))
            {
                return _exclusionParts[index];
            }

            return defaultValue;
        }
    }
}
