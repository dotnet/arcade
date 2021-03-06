using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Microsoft.DotNet.ApiCompat.Tests
{
    public static class Helpers
    {
        public static string RunApiCompat(string left, string rightDirs) => RunApiCompat(left, rightDirs, null, null);

        public static string RunApiCompat(string left, string rightDirs, string leftName, string rightName) => RunApiCompat(left, new string[] { rightDirs }, Enumerable.Empty<string>(), leftName, rightName);

        public static string RunApiCompat(string left, IEnumerable<string> rightDirs, IEnumerable<string> excludeAttributesFile, string leftName, string rightName)
        {
            using var writer = new StringWriter();

            var args = GetApiCompatArgs(left, rightDirs, excludeAttributesFile, leftName, rightName);
            new ApiCompatRunner(writer).Run(args);

            return writer.ToString();
        }

        private static string[] GetApiCompatArgs(string left, IEnumerable<string> rightDirs, IEnumerable<string> excludeAttributesFiles, string leftName, string rightName)
        {
            string frameworkRuntimePath = Path.GetDirectoryName(typeof(object).Assembly.Location);
            List<string> args = new List<string>()
            {
                left,
                "-i",
                $"{string.Join(";", rightDirs.ToArray())};{frameworkRuntimePath}",
                "--contract-depends",
                frameworkRuntimePath
            };

            if (!string.IsNullOrEmpty(leftName))
            {
                args.Add("-l");
                args.Add(leftName);
            }

            if (!string.IsNullOrEmpty(rightName))
            {
                args.Add("-r");
                args.Add(rightName);
            }

            if (excludeAttributesFiles.Count() > 0)
            {
                args.Add("--exclude-attributes");
                args.Add(string.Join(";", excludeAttributesFiles));
            }

            return args.ToArray();
        }
    }
}
