// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace SignTool
{
    internal static class Program
    {
        internal static void Main(string[] args)
        {
            SignToolArgs signToolArgs;
            if (!ParseCommandLineArguments(StandardHost.Instance, args, out signToolArgs))
            {
                PrintUsage();
                Environment.Exit(1);
            }

            if (!signToolArgs.Test && !File.Exists(signToolArgs.MSBuildPath))
            {
                Console.WriteLine($"Unable to locate MSBuild at the path '{signToolArgs.MSBuildPath}'.");
                Environment.Exit(1);
            }

            var signTool = SignToolFactory.Create(signToolArgs);
            var batchData = ReadConfigFile(signToolArgs.OutputPath, signToolArgs.ConfigFile);
            var util = new BatchSignUtil(signTool, batchData);
            util.Go();
        }

        internal static BatchSignInput ReadConfigFile(string outputPath, string configFile)
        {
            using (var file = File.OpenText(configFile))
            {
                BatchSignInput batchData;
                if (!TryReadConfigFile(Console.Out, file, outputPath, out batchData))
                {
                    Environment.Exit(1);
                }

                return batchData;
            }
        }

        internal static bool TryReadConfigFile(TextWriter output, TextReader configReader, string outputPath, out BatchSignInput batchData)
        {
            var serializer = new JsonSerializer();
            var fileJson = (Json.FileJson)serializer.Deserialize(configReader, typeof(Json.FileJson));
            var map = new Dictionary<string, SignInfo>(StringComparer.OrdinalIgnoreCase);
            var allGood = true;
            foreach (var item in fileJson.SignList)
            {
                var data = new SignInfo(certificate: item.Certificate, strongName: item.StrongName);
                foreach (var relativeFileName in ExpandFileList(outputPath, item.FileList, ref allGood))
                {
                    if (map.ContainsKey(relativeFileName))
                    {
                        Console.WriteLine($"Duplicate file entry: {relativeFileName}");
                        allGood = false;
                    }
                    else
                    {
                        map.Add(relativeFileName, data);
                    }
                }
            }

            if (!allGood)
            {
                batchData = null;
                return false;
            }

            batchData = new BatchSignInput(outputPath, map, fileJson.ExcludeList ?? Array.Empty<string>());
            return true;
        }

        /// <summary>
        /// The files to sign section supports globbing. The only caveat is that globs must expand to match at least a 
        /// single file else an error occurs. This function will expand those globas as necessary.
        /// </summary>
        private static List<string> ExpandFileList(string outputPath, IEnumerable<string> relativeFileNames, ref bool allGood)
        {
            var list = new List<string>();
            foreach (var relativeFileName in relativeFileNames)
            {
                if (!relativeFileName.Contains('*'))
                {
                    list.Add(relativeFileName);
                    continue;
                }

                var fullName = Path.Combine(outputPath, relativeFileName);
                try
                {
                    var expandedNameList = PathUtil.ExpandDirectoryGlob(fullName);
                    if (expandedNameList.Count == 0)
                    {
                        Console.WriteLine($"The glob {relativeFileName} expanded to 0 entries");
                        allGood = false;
                        continue;
                    }

                    list.AddRange(expandedNameList);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error expanding glob {relativeFileName}: {ex.Message}");
                    allGood = false;
                }
            }

            return list;
        }

        internal static void PrintUsage()
        {
            var usage =
@"SignTool.exe [-test] [-testSign] [-intermediateOutputPath <path>] [-msbuildPath <path>] [-nugetPackagesPath <path>] [-config <path>] outputPath

test: Run tool without actually modifying any state.
testSign: The binaries will be test signed. The default is to real sign.
outputPath: Directory containing the binaries.
intermediateOutputPath: Directory containing intermediate output.  Default is (outputpath\..\Obj).
nugetPackagesPath: Path containing downloaded NuGet packages.
msbuildPath: Path to MSBuild.exe to use as signing mechanism.
config: Path to SignToolData.json. Default build\config\SignToolData.json.
";
            Console.WriteLine(usage);
        }

        internal static bool ParseCommandLineArguments(
            IHost host,
            string[] args,
            out SignToolArgs signToolArgs)
        {
            signToolArgs = default(SignToolArgs);

            string intermediateOutputPath = null;
            string outputPath = null;
            string msbuildPath = null;
            string nugetPackagesPath = null;
            string configFile = null;
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
                    default:
                        Console.WriteLine($"Unrecognized option {current}");
                        return false;
                }
            }

            if (i + 1 != args.Length)
            {
                Console.WriteLine("Need a value for outputPath");
                return false;
            }

            outputPath = args[i];

            // Get defaults for all of the optional values that weren't specified
            if (msbuildPath == null && !test)
            {
                // installed VS versions could be "15.0" or "15.3", so "15." is a good proxy
                var vsInstallDir = LocateVS.Instance.GetInstallPath("15.", new[] { "Microsoft.Component.MSBuild" });
                msbuildPath = Path.Combine(vsInstallDir, "MSBuild", "15.0", "Bin", "msbuild.exe");
            }

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
                intermediateOutputPath: intermediateOutputPath,
                nugetPackagesPath: nugetPackagesPath,
                appPath: AppContext.BaseDirectory,
                configFile: configFile,
                test: test,
                testSign: testSign);
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
