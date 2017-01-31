using System;
using System.IO;
using Microsoft.ApiUsageAnalyzer.Core;

namespace Microsoft.ApiUsageAnalyzer
{
    public class DllInputFormat : IInputFormat
    {
        private readonly Func<DllAnalyzer> _factory;
        public string Id => "raw";
        public string Description => "Reads a single dll file";


        public DllInputFormat(Func<DllAnalyzer> factory)
        {
            _factory = factory;
        }

        public IAppAnalyzer CreateAnalyzer()
        {
            return _factory();
        }

        public bool CanProcess(string input)
            => !string.IsNullOrEmpty(input) && input.EndsWith(".dll") && File.Exists(input);
    }

    public class DllAnalyzer : IAppAnalyzer
    {
        private IApiUsageCollector Collector { get; }

        public DllAnalyzer(IApiUsageCollector collector)
        {
            Collector = collector;
        }

        public bool TryAnalyzeApp(string path, out AppAnalysis analysis)
        {
            if (string.IsNullOrEmpty(path) || !path.EndsWith(".dll") || !File.Exists(path))
            {
                return false;
            }

            var apis = Collector.GetApiUsage(path);
            var name = Path.GetFileNameWithoutExtension(path);
            analysis = new AppAnalysis(name, new[] {new LibraryAnalysis(name, "any", apis)});
            return true;
        }
    }
}