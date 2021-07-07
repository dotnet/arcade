using Microsoft.DotNet.RuntimeBuildMeasurement.Inspectors;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using FluentAssertions;
using Xunit;

namespace Microsoft.DotNet.RuntimeBuildMeasurement.Test
{
    public class NinjaLogInspectorTest
    {
        [Fact]
        public void TestNinjaFileCanBeParsed()
        {
            var inspector = new NinjaLogInspector();

            IEnumerable<NamedDuration> durations = inspector.Inspect(new FileInfo("Inspectors/.ninja_log"));

            durations.Should().HaveCount(301);
        }

        [Fact]
        public void AllSubstringsTest()
        {
            var inspector = new NinjaLogInspector();
            var path = "Native.Windows/System.Globalization.Native/CMakeFiles/System.Globalization.Native-Static.dir/pal_timeZoneInfo.c.obj";

            List<string> dirs = inspector.GetAllDirectories(path).ToList();

            dirs.Should().HaveCount(4);

            dirs[0].Should().Be("Native.Windows");
            dirs[1].Should().Be("Native.Windows/System.Globalization.Native");
            dirs[2].Should().Be("Native.Windows/System.Globalization.Native/CMakeFiles");
            dirs[3].Should().Be("Native.Windows/System.Globalization.Native/CMakeFiles/System.Globalization.Native-Static.dir");
        }
    }
}
