using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.ApiUsageAnalyzer.Core;
using Newtonsoft.Json;

namespace Microsoft.ApiUsageAnalyzer
{
    internal class JsonOutputFormat : IOutputFormat
    {
        public string Id => "json";
        public string Description => "Output in JSON format";

        public void FormatOutputFile(string destinationPath, IReadOnlyList<AppAnalysis> analyses)
        {
            var dict = analyses.ToDictionary(a => a.Name);
            File.WriteAllText(destinationPath, JsonConvert.SerializeObject(dict, Formatting.Indented));
        }

        public void FormatOutputDirectory(string destinationPath, IReadOnlyList<AppAnalysis> analyses)
        {
            Directory.CreateDirectory(destinationPath);
            foreach (var analysis in analyses)
            {
                File.WriteAllText(Path.Combine(destinationPath, $"{analysis.Name}.json"),
                    JsonConvert.SerializeObject(analysis, Formatting.Indented));
            }
        }
    }
}