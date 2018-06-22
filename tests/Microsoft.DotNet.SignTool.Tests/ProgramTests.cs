using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace SignTool.UnitTests
{
    public class ProgramTests
    {
        public class ReadConfigFileTests : ProgramTests
        {
            private BatchSignInput Load(string json)
            {
                using (var reader = new StringReader(json))
                using (var writer = new StringWriter())
                {
                    BatchSignInput data;
                    Assert.True(Program.TryReadConfigFile(writer, reader, @"q:\outputPath", out data));
                    Assert.True(string.IsNullOrEmpty(writer.ToString()));
                    return data;
                }
            }

            [Fact]
            public void MissingExcludeSection()
            {
                var json = @"
{
    ""sign"": []
}";
                var data = Load(json);
                Assert.Empty(data.FileNames);
                Assert.Empty(data.ExternalFileNames);
            }

            [Fact]
            public void MsiContent()
            {
                var json = @"
{
  ""sign"": [
  {
    ""certificate"": ""Microsoft402"",
    ""strongName"": null,
    ""values"": [
      ""test.msi""
    ]
  }]
}";

                var data = Load(json);
                Assert.Equal(new[] { "test.msi" }, data.FileNames.Select(x => x.Name));
                Assert.Empty(data.ExternalFileNames);
            }
        }

        public class CommandLineParsingTests : ProgramTests
        {
            internal const string DefaultProgramFiles = @"Q:\Program Files (x86)";
            internal const string DefaultUserProfile = @"C:\users\johndoe";

            private Mock<IHost> CreateDefaultHost()
            {
                var host = new Mock<IHost>(MockBehavior.Default);
                host.Setup(x => x.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)).Returns(DefaultProgramFiles);
                host.Setup(x => x.GetFolderPath(Environment.SpecialFolder.UserProfile)).Returns(DefaultUserProfile);
                return host;
            }

            private SignToolArgs Parse(params string[] args)
            {
                var host = CreateDefaultHost();
                return Parse(host.Object, args);
            }

            private SignToolArgs Parse(IHost host, params string[] args)
            {
                host = host ?? new Mock<IHost>(MockBehavior.Default).Object;
                SignToolArgs signToolArgs;
                Assert.True(Program.ParseCommandLineArguments(host, args, out signToolArgs));
                return signToolArgs;
            }

            [Fact]
            public void OutputPath()
            {
                var args = Parse("-test", @"e:\temp");
                Assert.Equal(@"e:\temp", args.OutputPath);
                Assert.Equal(@"e:\Obj", args.IntermediateOutputPath);
            }

            [Fact]
            public void NuGetPackagesPathDefault()
            {
                var args = Parse("-test", @"e:\temp\Debug");
                Assert.Equal(Path.Combine(DefaultUserProfile, @".nuget\packages"), args.NuGetPackagesPath);
            }

            [Fact]
            public void NuGetPackagesPathEnvironment()
            {
                var host = CreateDefaultHost();
                host.Setup(x => x.GetEnvironmentVariable(@"NUGET_PACKAGES")).Returns(@"e:\temp\.nuget");
                var args = Parse(host.Object, "-test", @"e:\temp\Debug");
                Assert.Equal(@"e:\temp\.nuget", args.NuGetPackagesPath);
            }

            [Fact]
            public void NuGetPackagesPathExplicit()
            {
                var args = Parse("-test", "-nugetPackagesPath", @"e:\temp\.nuget", @"e:\temp\Debug");
                Assert.Equal(@"e:\temp\.nuget", args.NuGetPackagesPath);
                Assert.Equal(@"e:\temp\Debug", args.OutputPath);
            }
        }
    }
}
