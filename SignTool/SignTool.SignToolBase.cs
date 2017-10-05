// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using static SignTool.PathUtil;

namespace SignTool
{
    internal static partial class SignToolFactory
    {
        private abstract class SignToolBase : ISignTool
        {
            internal const string MicroBuildVersion = "0.2.0";

            private readonly SignToolArgs _args;

            internal string MSBuildPath => _args.MSBuildPath;
            internal string OutputPath => _args.OutputPath;
            internal string IntermediateOutputPath => _args.IntermediateOutputPath;
            internal string NuGetPackagesPath => _args.NuGetPackagesPath;
            internal string AppPath => _args.AppPath;

            internal SignToolBase(SignToolArgs args)
            {
                _args = args;
            }

            public abstract void RemovePublicSign(string assemblyPath);

            public abstract bool VerifySignedAssembly(Stream assemblyStream);

            protected abstract int RunMSBuild(ProcessStartInfo startInfo);

            public void Sign(IEnumerable<FileSignInfo> filesToSign)
            {
                var buildFilePath = Path.Combine(AppPath, "build.proj");
                
                var content = GenerateBuildFileContent(filesToSign);
                File.WriteAllText(buildFilePath, content);
                Console.WriteLine("Generated project file");
                Console.WriteLine(content);

                string fileName = MSBuildPath;
                var commandLine = $@"/v:m ""{buildFilePath}""";

                if (!_args.Test)
                {
                    Console.WriteLine($"{fileName} {commandLine}");
                }

                var startInfo = new ProcessStartInfo()
                {
                    FileName = fileName,
                    Arguments = commandLine,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    WorkingDirectory = AppPath,
                };

                var exitCode = RunMSBuild(startInfo);

                File.Delete(buildFilePath);

                if (exitCode != 0)
                {
                    Console.WriteLine("MSBuild failed!!!");
                    throw new Exception("Sign failed");
                }
            }

            private string GenerateBuildFileContent(IEnumerable<FileSignInfo> filesToSign)
            {
                var builder = new StringBuilder();
                AppendLine(builder, depth: 0, text: @"<?xml version=""1.0"" encoding=""utf-8""?>");
                AppendLine(builder, depth: 0, text: @"<Project DefaultTargets=""AfterBuild"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">");

                // Setup the code to get the NuGet package root.
                var signKind = _args.TestSign ? "test" : "real";
                AppendLine(builder, depth: 1, text: @"<PropertyGroup>");
                AppendLine(builder, depth: 2, text: $@"<NuGetPackageRoot>{NuGetPackagesPath}</NuGetPackageRoot>");
                AppendLine(builder, depth: 2, text: $@"<OutDir>{OutputPath}</OutDir>");
                AppendLine(builder, depth: 2, text: $@"<IntermediateOutputPath>{IntermediateOutputPath}</IntermediateOutputPath>");
                AppendLine(builder, depth: 2, text: $@"<SignType>{signKind}</SignType>");
                AppendLine(builder, depth: 1, text: @"</PropertyGroup>");

                AppendLine(builder, depth: 1, text: $@"<Import Project=""$(NuGetPackageRoot)\MicroBuild.Core\{MicroBuildVersion}\build\MicroBuild.Core.props"" />");

                AppendLine(builder, depth: 1, text: $@"<ItemGroup>");

                foreach (var fileToSign in filesToSign)
                {
                    AppendLine(builder, depth: 2, text: $@"<FilesToSign Include=""{fileToSign.FileName.FullPath}"">");
                    AppendLine(builder, depth: 3, text: $@"<Authenticode>{fileToSign.Certificate}</Authenticode>");
                    if (fileToSign.StrongName != null)
                    {
                        AppendLine(builder, depth: 3, text: $@"<StrongName>{fileToSign.StrongName}</StrongName>");
                    }
                    AppendLine(builder, depth: 2, text: @"</FilesToSign>");
                }

                AppendLine(builder, depth: 1, text: $@"</ItemGroup>");

                // The MicroBuild targets hook AfterBuild to do the signing hence we just make it our no-op default target
                AppendLine(builder, depth: 1, text: @"<Target Name=""AfterBuild"">");
                AppendLine(builder, depth: 2, text: @"<Message Text=""Running actual signing process"" />");
                AppendLine(builder, depth: 1, text: @"</Target>");

                AppendLine(builder, depth: 1, text: $@"<Import Project=""$(NuGetPackageRoot)\MicroBuild.Core\{MicroBuildVersion}\build\MicroBuild.Core.targets"" />");
                AppendLine(builder, depth: 0, text: @"</Project>");

                return builder.ToString();
            }

            private static void AppendLine(StringBuilder builder, int depth, string text)
            {
                for (int i = 0; i < depth; i++)
                {
                    builder.Append("    ");
                }

                builder.AppendLine(text);
            }
        }
    }
}
