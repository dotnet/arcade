using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.ApiUsageAnalyzer.Core;

namespace Microsoft.ApiUsageAnalyzer
{
    internal class Application
    {
        private IEnumerable<IOutputFormat> OutputFormats { get; }
        private IEnumerable<IInputFormat> InputFormats { get; }

        public Application(IEnumerable<IOutputFormat> outputFormats, IEnumerable<IInputFormat> inputFormats)
        {
            OutputFormats = outputFormats;
            InputFormats = inputFormats;
        }

        public void ListOutputFormats()
        {
            Console.WriteLine("Avaliable Output Formats:");
            foreach (var format in OutputFormats)
            {
                Console.WriteLine($"\t{format.Id}\t{format.Description}");
            }
        }

        public void ListInputFormats()
        {
            Console.WriteLine("Avaliable Input Formats:");
            foreach (var format in InputFormats)
            {
                Console.WriteLine($"\t{format.Id}\t{format.Description}");
            }
        }

        private AppAnalysis Analyze(string input)
        {
            foreach (var format in InputFormats)
            {
                if (format.CanProcess(input) && format.CreateAnalyzer().TryAnalyzeApp(input, out var analysis))
                {
                    return analysis;
                }
            }
            throw new ArgumentException($"Unable to process input '{input}'");
        }

        public void AnalyzeToFile(IReadOnlyList<string> inputs, string output, string outputFormat)
        {
            IOutputFormat fmt = GetOutputFormat(outputFormat);
            var results = inputs.Select(Analyze).ToList();
            fmt.FormatOutputFile(output, results);
        }

        public void AnalyzeToDirectory(IReadOnlyList<string> inputs, string outputDirectory, string outputFormat)
        {
            IOutputFormat fmt = GetOutputFormat(outputFormat);
            var results = inputs.Select(Analyze).ToList();
            fmt.FormatOutputDirectory(outputDirectory, results);
        }

        private IOutputFormat GetOutputFormat(string outputFormat)
        {
            if (string.IsNullOrEmpty(outputFormat))
            {
                return OutputFormats.First();
            }
            var fmt =
                OutputFormats.FirstOrDefault(f => string.Equals(f.Id, outputFormat, StringComparison.OrdinalIgnoreCase));
            if (fmt == null)
            {
                throw new ArgumentException($"Unknown output format '{outputFormat}'");
            }
            return fmt;
        }
    }
}