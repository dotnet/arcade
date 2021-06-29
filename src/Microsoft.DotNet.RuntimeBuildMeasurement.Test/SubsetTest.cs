using FluentAssertions;
using Xunit;

namespace Microsoft.DotNet.RuntimeBuildMeasurement.Test
{
    public class SubsetTest
    {
        [Theory]
        [InlineData("libs")]
        [InlineData("Clr.Runtime")]
        [InlineData(" libs  ")]
        public void ParseNameWithoutDependencies(string name)
        {
            name.Trim().Should().Be(Subset.Parse(name).Name);
            Subset.Parse(name).Dependencies.Should().BeEmpty();
        }

        [Theory]
        [InlineData("libs.pretest(clr)", "libs.pretest", "clr")]
        [InlineData("libs.pretest(clr, mono)", "libs.pretest", "clr", "mono")]
        public void ParseDependencies(string description, string expectedName, params string[] expectedDeps)
        {
            Subset parsedSubset = Subset.Parse(description);

            expectedName.Should().Be(parsedSubset.Name);
            expectedDeps.Should().BeEquivalentTo(parsedSubset.Dependencies);
        }
    }
}
