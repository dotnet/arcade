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
using Microsoft.Build.Utilities;

namespace Microsoft.DotNet.SignTool
{
    internal static class Configuration
    {
        internal static BatchSignInput ReadConfigFile(string outputPath, string configFile, TaskLoggingHelper log)
        {
            using (var reader = File.OpenText(configFile))
            {
                return TryReadConfigFile(log, reader, outputPath);
            }
        }

        internal static BatchSignInput TryReadConfigFile(TaskLoggingHelper log, TextReader configReader, string outputPath)
        {
            var serializer = new JsonSerializer();
            var fileJson = (FileJson)serializer.Deserialize(configReader, typeof(FileJson));
            var map = new Dictionary<string, SignInfo>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in fileJson.SignList)
            {
                var data = new SignInfo(certificate: item.Certificate, strongName: item.StrongName);
                foreach (var relativeFileName in ExpandFileList(log, outputPath, item.FileList))
                {
                    if (map.ContainsKey(relativeFileName))
                    {
                        log.LogError($"Duplicate file entry: {relativeFileName}");
                    }
                    else
                    {
                        map.Add(relativeFileName, data);
                    }
                }
            }

            if (log.HasLoggedErrors)
            {
                return null;
            }

            return new BatchSignInput(outputPath, map, fileJson.ExcludeList ?? Array.Empty<string>(), fileJson.PublishUrl ?? "unset");
        }

        /// <summary>
        /// The 'files to sign' section supports globbing. The only caveat is that globs must expand to match at least a 
        /// single file else an error occurs. This function will expand those globs as necessary.
        /// </summary>
        private static List<string> ExpandFileList(TaskLoggingHelper log, string outputPath, IEnumerable<string> relativeFileNames)
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
                        log.LogError($"The glob {relativeFileName} expanded to 0 entries");
                        continue;
                    }

                    list.AddRange(result.Files.Select(x => PathUtil.NormalizeSeparators(x.Path)));
                }
                catch (Exception ex)
                {
                    log.LogError($"Error expanding glob {relativeFileName}: {ex.Message}");
                }
            }

            return list;
        }
    }
}
