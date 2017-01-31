using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Microsoft.ApiUsageAnalyzer.Cci.TestLibrary;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.DataCollection;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.ApiUsageAnalyzer.Cci.Tests
{
    public class CciApiUsageCollectorTest
    {
        private ITestOutputHelper Output { get; }

        public CciApiUsageCollectorTest(ITestOutputHelper output)
        {
            Output = output;
        }

        public static readonly string TestLibraryLocation = typeof(Inheritor).GetTypeInfo().Assembly.Location;

        public static IImmutableSet<string> GetApisFromTestLibrary()
        {
            var collector = new CciApiUsageCollector();

            return collector.GetApiUsage(TestLibraryLocation).Select(t => t.api).ToImmutableSortedSet();
        }

        [Fact]
        public void ListApis()
        {
            var apis = GetApisFromTestLibrary();
            foreach (var api in apis.OrderBy(api => api))
            {
                Output.WriteLine(api);
            }
        }

        public static IEnumerable<object[]> RequiredApis => new[]
        {
            new[] {"T:System.NotImplementedException"},
            new[] {"T:System.InvalidOperationException"},
            new[] {"T:System.IO.Stream"},
            new[] {"T:System.IO.MemoryStream"},
            new[] {"T:System.IO.SeekOrigin"},
            new[] {"T:System.Text.RegularExpressions.Regex"},
            new[] {"T:System.Collections.Generic.List`1"},

            new[] {"M:System.IO.Stream.#ctor"},
            new[] {"M:System.IO.Stream.set_Position(System.Int64)"},
            new[] {"M:System.IO.MemoryStream.#ctor"},
            new[] {"M:System.NotImplementedException.#ctor"},
            new[] {"M:System.InvalidOperationException.#ctor"},

            new[] {"F:System.Text.RegularExpressions.Regex.capsize"},
        };

        [Theory]
        [MemberData(nameof(RequiredApis))]
        public void ShouldRequireApi(string requiredApi)
        {
            var apis = GetApisFromTestLibrary();
            Assert.Contains(requiredApi, apis);
        }
    }
}
