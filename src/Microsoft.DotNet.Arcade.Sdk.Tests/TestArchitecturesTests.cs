// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace Microsoft.DotNet.Arcade.Sdk.Tests
{
    public class TestArchitecturesTests
    {
        private static readonly string s_toolsDirectory = Path.Combine(
            FindRepoRoot(),
            "src",
            "Microsoft.DotNet.Arcade.Sdk",
            "tools");

        [Fact]
        public void VSTestPassesRequestedArchitectureToDotNetTest()
        {
            XDocument targets = LoadTargets("VSTest.targets");

            string command = targets
                .Descendants("_TestRunnerCommand")
                .Select(element => element.Value)
                .Single(value => value.Contains("$(DotNetTool)", StringComparison.Ordinal));

            Assert.Contains("--arch %(TestToRun.Architecture)", command);
        }

        [Fact]
        public void XUnitCoreRequiresAnExactArchitectureHost()
        {
            XDocument targets = LoadTargets("XUnit", "XUnit.Runner.targets");
            XElement runTests = GetTarget(targets, "RunTests");

            Assert.Contains(
                runTests.Descendants("_TestRunner"),
                element => element.Attribute("Condition")?.Value.Contains(
                    "'$(_TestArchitecture)' == '$(_DotNetToolArchitecture)'",
                    StringComparison.Ordinal) == true);
            Assert.Contains(
                runTests.Descendants("_TestRunner"),
                element => element.Attribute("Condition")?.Value.Contains(
                    "Exists('$(_ArchitectureSpecificDotNetTool)')",
                    StringComparison.Ordinal) == true);
            Assert.Contains(
                runTests.Descendants("Error"),
                element => element.Attribute("Text")?.Value.Contains(
                    "requires a matching dotnet host",
                    StringComparison.Ordinal) == true);
        }

        [Theory]
        [InlineData("XUnitV3", "XUnitV3.Runner.targets")]
        [InlineData("Microsoft.Testing.Platform", "Microsoft.Testing.Platform.targets")]
        public void FixedHostRunnersOptIntoArchitectureValidation(string directory, string fileName)
        {
            XDocument targets = LoadTargets(directory, fileName);

            Assert.Contains(
                targets.Descendants("_TestRunnerRequiresMatchingArchitecture"),
                element => element.Value == "true");
        }

        [Fact]
        public void TestsValidateRequestedArchitectureAgainstFixedHost()
        {
            XDocument targets = LoadTargets("Tests.targets");
            XElement initialize = GetTarget(targets, "_InitializeTestArchitectures");
            XElement validate = GetTarget(targets, "_ValidateTestArchitectures");

            Assert.Contains(initialize.Descendants("_CurrentProcessArchitecture"), element =>
                element.Value.Contains("RuntimeInformation]::ProcessArchitecture", StringComparison.Ordinal));
            Assert.Contains(initialize.Descendants("_BuiltTestArchitecture"), element =>
                element.Value.Contains("DefaultAppHostRuntimeIdentifier", StringComparison.Ordinal) &&
                element.Attribute("Condition")?.Value.Contains("'$(UseAppHost)' == 'true'", StringComparison.Ordinal) == true);
            Assert.Contains(initialize.Descendants("_BuiltTestArchitecture"), element =>
                element.Value == "$(_DotNetToolArchitecture)" &&
                element.Attribute("Condition")?.Value.Contains("'$(UseAppHost)' != 'true'", StringComparison.Ordinal) == true);
            Assert.Contains(initialize.Descendants("_BuiltTestArchitecture"), element =>
                element.Value == "x86" &&
                element.Attribute("Condition")?.Value.Contains("'$(Prefer32Bit)' == 'true'", StringComparison.Ordinal) == true);
            Assert.Contains(initialize.Descendants("_BuiltTestArchitecture"), element =>
                element.Value == "$(_CurrentOSArchitecture)");
            Assert.Contains(initialize.Descendants("TestArchitectures"), element =>
                element.Value == "x64" &&
                element.Attribute("Condition")?.Value.Contains("'$(TestRunnerName)' == 'XUnit'", StringComparison.Ordinal) == true &&
                element.Attribute("Condition")?.Value.Contains("'$(OS)' == 'Windows_NT'", StringComparison.Ordinal) == true);
            Assert.Contains(validate.Descendants("Error"), element =>
                element.Attribute("Text")?.Value.Contains(
                    "Build a separate RID-specific test output",
                    StringComparison.Ordinal) == true);

            XElement initializationDependencies = targets
                .Descendants("_InitializeTestArchitecturesDependsOn")
                .Single();
            Assert.DoesNotContain("TestArchitectures", initializationDependencies.Attribute("Condition")?.Value);

            XElement testTarget = GetTarget(targets, "Test");
            Assert.Contains("_ValidateTestArchitectures", testTarget.Attribute("DependsOnTargets")?.Value);
        }

        private static XElement GetTarget(XDocument document, string name) =>
            document
                .Descendants("Target")
                .Single(element => element.Attribute("Name")?.Value == name);

        private static XDocument LoadTargets(params string[] path) =>
            XDocument.Load(Path.Combine(new[] { s_toolsDirectory }.Concat(path).ToArray()));

        private static string FindRepoRoot()
        {
            for (string directory = AppContext.BaseDirectory;
                 directory != null;
                 directory = Path.GetDirectoryName(directory))
            {
                if (Directory.Exists(Path.Combine(directory, "src", "Microsoft.DotNet.Arcade.Sdk")))
                {
                    return directory;
                }
            }

            for (string directory = Directory.GetCurrentDirectory();
                 directory != null;
                 directory = Path.GetDirectoryName(directory))
            {
                if (Directory.Exists(Path.Combine(directory, "src", "Microsoft.DotNet.Arcade.Sdk")))
                {
                    return directory;
                }
            }

            throw new InvalidOperationException("Unable to locate the Arcade repository root.");
        }
    }
}
