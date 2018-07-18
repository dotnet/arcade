// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.IO;

namespace Microsoft.DotNet.SignTool
{
    sealed class ParametersParsing
    {
        internal static readonly string UsageMessage = 
@"SignTool.exe [-test] [-testSign] [-intermediateOutputPath <path>] [-msbuildPath <path>] [-nugetPackagesPath <path>] [-config <path>] outputPath

test: Run tool without actually modifying any state.
testSign: The binaries will be test signed. The default is to real sign.
outputPath: Directory containing the binaries.
intermediateOutputPath: Directory containing intermediate output.  Default is (outputpath\..\Obj).
nugetPackagesPath: Path containing downloaded NuGet packages.
msbuildPath: Path to MSBuild.exe to use as signing mechanism.
msbuildBinaryLog: Path to the binary log to generate when invoking signing.
config: Path to SignToolData.json. Default: build\config\SignToolData.json.
outputConfig: Run tool to produce an orchestration json file with specified name.  This will contain SHA256 hashes of files for verification to consume later.
";

        internal static bool ParseCommandLineArguments(IHost host, string[] args, out SignToolArgs signToolArgs)
        {
            signToolArgs = default;

            string intermediateOutputPath = null;
            string outputPath = null;
            string msbuildPath = null;
            string nugetPackagesPath = null;
            string configFile = null;
            string outputConfigFile = null;
            string msbuildBinaryLogFilePath = null;
            var test = false;
            var testSign = false;

            var i = 0;

            while (i + 1 < args.Length)
            {
                var current = args[i].ToLower();
                switch (current)
                {
                    case "-test":
                        test = true;
                        i++;
                        break;
                    case "-testsign":
                        testSign = true;
                        i++;
                        break;
                    case "-intermediateoutputpath":
                        if (!ParsePathOption(args, ref i, current, out intermediateOutputPath))
                        {
                            return false;
                        }
                        break;
                    case "-msbuildpath":
                        if (!ParsePathOption(args, ref i, current, out msbuildPath))
                        {
                            return false;
                        }
                        break;
                    case "-msbuildbinarylog":
                        if (!ParsePathOption(args, ref i, current, out msbuildBinaryLogFilePath))
                        {
                            return false;
                        }
                        break;
                    case "-nugetpackagespath":
                        if (!ParsePathOption(args, ref i, current, out nugetPackagesPath))
                        {
                            return false;
                        }
                        break;
                    case "-config":
                        if (!ParsePathOption(args, ref i, current, out configFile))
                        {
                            return false;
                        }
                        break;
                    case "-outputconfig":
                        if (!ParsePathOption(args, ref i, current, out outputConfigFile))
                        {
                            return false;
                        }
                        outputConfigFile = outputConfigFile.TrimEnd('\"').TrimStart('\"');
                        break;
                    default:
                        Console.Error.WriteLine($"Unrecognized option {current}");
                        return false;
                }
            }

            if (i + 1 != args.Length)
            {
                Console.Error.WriteLine("Need a value for outputPath");
                return false;
            }

            outputPath = args[i];

            intermediateOutputPath = intermediateOutputPath ?? Path.Combine(Path.GetDirectoryName(outputPath), "Obj");

            if (string.IsNullOrWhiteSpace(nugetPackagesPath))
            {
                nugetPackagesPath = host.GetEnvironmentVariable("NUGET_PACKAGES");
                if (string.IsNullOrWhiteSpace(nugetPackagesPath))
                {
                    nugetPackagesPath = Path.Combine(
                        host.GetFolderPath(Environment.SpecialFolder.UserProfile),
                        @".nuget\packages");
                }
            }

            if (configFile == null)
            {
                var sourcesPath = GetSourcesPath(host, outputPath);
                if (sourcesPath != null)
                {
                    configFile = Path.Combine(sourcesPath, @"build\config\SignToolData.json");
                }
            }

            signToolArgs = new SignToolArgs(
                outputPath: outputPath,
                msbuildPath: msbuildPath,
                msbuildBinaryLogFilePath: msbuildBinaryLogFilePath,
                intermediateOutputPath: intermediateOutputPath,
                nugetPackagesPath: nugetPackagesPath,
                appPath: AppContext.BaseDirectory,
                configFile: configFile,
                test: test,
                testSign: testSign,
                orchestrationManifestPath: outputConfigFile);
            return true;
        }

        private static bool ParsePathOption(string[] args, ref int i, string optionName, out string optionValue)
        {
            if (i + 1 >= args.Length)
            {
                Console.WriteLine($"{optionName} needs an argument");
                optionValue = null;
                return false;
            }

            optionValue = args[i + 1];
            i += 2;
            return true;
        }

        private static string GetSourcesPath(IHost host, string outputPath)
        {
            var current = Path.GetDirectoryName(outputPath);
            while (!string.IsNullOrEmpty(current))
            {
                var gitDir = Path.Combine(current, ".git");
                if (host.DirectoryExists(gitDir))
                {
                    return current;
                }

                current = Path.GetDirectoryName(current);
            }
            return null;
        }
    }
}
