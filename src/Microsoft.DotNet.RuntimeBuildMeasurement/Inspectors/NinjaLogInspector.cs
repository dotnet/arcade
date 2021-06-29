using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Microsoft.DotNet.RuntimeBuildMeasurement.Inspectors
{
    public class NinjaLogInspector : IInspector
    {
        public IEnumerable<NamedDuration> Inspect(FileInfo file)
        {
            using var sr = new StreamReader(file.FullName);
            var results = new Dictionary<string, long>();

            var line = Array.Empty<string>();
            while ((line = sr.ReadLine()?.Split('\t')) != null)
            {
                if (line.Length != 5)
                {
                    // comment or something
                    continue;
                }

                int duration = Convert.ToInt32(line[1], CultureInfo.InvariantCulture) - Convert.ToInt32(line[0], CultureInfo.InvariantCulture);
                string target = line[3];

                foreach (string directory in GetAllDirectories(target))
                {
                    if (results.ContainsKey(directory))
                    {
                        results[directory] += duration;
                    }
                    else
                    {
                        results[directory] = duration;
                    }
                }
            }

            return results.Select(r => new NamedDuration(r.Key, TimeSpan.FromMilliseconds(r.Value)));
        }

        public IEnumerable<string> GetAllDirectories(string path)
        {
            int i = 0;
            while ((i = path.IndexOf('/', i) + 1) != 0)
            {
                yield return path.Substring(0, i - 1);
            }
        }
    }
}
