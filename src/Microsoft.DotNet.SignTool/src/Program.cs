// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Microsoft.DotNet.SignTool.Json;

namespace Microsoft.DotNet.SignTool
{
    internal static class Program
    {
        internal const int ExitSuccess = 0;
        internal const int ExitFailure = 1;

        internal static int Main(string[] args)
        {
            try
            {
                if (!ParametersParsing.ParseCommandLineArguments(StandardHost.Instance, args, out SignToolArgs signToolArgs))
                {
                    Console.WriteLine(ParametersParsing.UsageMessage);
                    return ExitFailure;
                }

                if (!signToolArgs.Test && !File.Exists(signToolArgs.MSBuildPath))
                {
                    Console.WriteLine($"Unable to locate MSBuild at the path '{signToolArgs.MSBuildPath}'.");
                    return ExitFailure;
                }

                BatchSignInput batchData;
                var signTool = SignToolFactory.Create(signToolArgs);
                string configFileKind = GetConfigFileKind(signToolArgs.ConfigFile);

                switch (configFileKind.ToLower())
                {
                    case "default":
                        batchData = ReadConfigFile(signToolArgs.OutputPath, signToolArgs.ConfigFile);
                        break;
                    case "orchestration":
                        batchData = ReadOrchestrationConfigFile(signToolArgs.OutputPath, signToolArgs.ConfigFile);
                        break;
                    default:
                        Console.WriteLine($"signtool : error : Don't know how to deal with manifest kind '{configFileKind}'");
                        return ExitFailure;
                }

                var util = new BatchSignUtil(signTool, batchData, signToolArgs.OrchestrationManifestPath);

                return util.Go(Console.Out) ? ExitSuccess : ExitFailure;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unexpected exception: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                return ExitFailure;
            }
        }

        internal static BatchSignInput ReadConfigFile(string outputPath, string configFile)
        {
            using (var file = File.OpenText(configFile))
            {
                if (!TryReadConfigFile(Console.Out, file, outputPath, out BatchSignInput batchData))
                {
                    Environment.Exit(ExitFailure);
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
                        Console.WriteLine($"signtool : error : Duplicate file entry: {relativeFileName}");
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

            batchData = new BatchSignInput(outputPath, map, fileJson.ExcludeList ?? Array.Empty<string>(), fileJson.PublishUrl ?? "unset");
            return true;
        }

        internal static BatchSignInput ReadOrchestrationConfigFile(string outputPath, string configFile)
        {
            using (var file = File.OpenText(configFile))
            {
                if (!TryReadOrchestrationConfigFile(Console.Out, file, outputPath, out BatchSignInput batchData))
                {
                    Environment.Exit(ExitFailure);
                }
                return batchData;
            }
        }

        internal static bool TryReadOrchestrationConfigFile(TextWriter output, TextReader configReader, string outputPath, out BatchSignInput batchData)
        {
            var serializer = new JsonSerializer();
            var fileJson = (Json.OrchestratedFileJson)serializer.Deserialize(configReader, typeof(Json.OrchestratedFileJson));
            var map = new Dictionary<FileSignDataEntry, SignInfo>();
            // For now, a given json file will be assumed to serialize to one place and we'll throw otherwise
            string publishUrl = (from OrchestratedFileSignData entry in fileJson.SignList
                                 from FileSignDataEntry fileToSign in entry.FileList
                                 select fileToSign.PublishToFeedUrl).Distinct().Single();
            var allGood = true;
            foreach (var item in fileJson.SignList)
            {
                var data = new SignInfo(certificate: item.Certificate, strongName: item.StrongName);
                
                foreach (FileSignDataEntry entry in item.FileList)
                {
                    if (map.ContainsKey(entry))
                    {
                        Console.WriteLine($"signtool : error : Duplicate signing info entry for: {entry.FilePath}");
                        allGood = false;
                    }
                    else
                    {
                        map.Add(entry, data);
                    }
                }
            }

            if (!allGood)
            {
                batchData = null;
                return false;
            }

            batchData = new BatchSignInput(outputPath, map, fileJson.ExcludeList ?? Array.Empty<string>(), publishUrl );
            return true;
        }

        /// <summary>
        /// The 'files to sign' section supports globbing. The only caveat is that globs must expand to match at least a 
        /// single file else an error occurs. This function will expand those globs as necessary.
        /// </summary>
        private static List<string> ExpandFileList(string outputPath, IEnumerable<string> relativeFileNames, ref bool allGood)
        {
            var directoryInfo = new DirectoryInfo(outputPath);
            var matchDir = new DirectoryInfoWrapper(directoryInfo);

            var list = new List<string>();
            foreach (var relativeFileName in relativeFileNames)
            {
                if (!relativeFileName.Contains('*'))
                {
                    list.Add(relativeFileName);
                    continue;
                }

                try
                {
                    var matcher = new Matcher(StringComparison.OrdinalIgnoreCase);
                    matcher.AddInclude(relativeFileName);
                    var result = matcher.Execute(matchDir);
                    if (!result.HasMatches)
                    {
                        Console.WriteLine($"The glob {relativeFileName} expanded to 0 entries");
                        continue;
                    }

                    list.AddRange(result.Files.Select(x => PathUtil.NormalizeSeparators(x.Path)));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"signtool : error : Error expanding glob {relativeFileName}: {ex.Message}");
                    allGood = false;
                }
            }

            return list;
        }

        internal static string GetConfigFileKind(string path)
        {
            JObject configFile = JObject.Parse(File.ReadAllText(path));
            var kind = configFile["kind"]?.Value<string>();
            return string.IsNullOrEmpty(kind) ? "default" : kind;
        }
    }
}
