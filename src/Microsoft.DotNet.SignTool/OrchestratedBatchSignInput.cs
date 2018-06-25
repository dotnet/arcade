// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using SignTool.Json;

namespace SignTool
{
    /// <summary>
    /// Represents a preprocessed version of all of the input to the batch signing process.
    /// </summary>
    internal sealed class OrchestratedBatchSignInput
    {
        /// <summary>
        /// The path where the binaries are built to: e:\path\to\source\Binaries\Debug
        /// </summary>
        internal string OutputPath { get; }

        /// <summary>
        /// The path where the binaries are built to: e:\path\to\source\Binaries\Debug
        /// </summary>
        internal string PublishUri { get; }

        /// <summary>
        /// The ordered names of the files to be signed. These are all relative paths off of the <see cref="OutputPath"/>
        /// property.
        /// </summary>
        internal ImmutableArray<FileName> FileNames { get; }

        /// <summary>
        /// These are binaries which are included in our zip containers but are already signed. This list is used for 
        /// validation purposes. These are all flat names and cannot be relative paths.
        /// </summary>
        internal ImmutableArray<string> ExternalFileNames { get;}

        /// <summary>
        /// Names of assemblies that need to be signed. This is a subset of <see cref="FileNames"/>
        /// </summary>
        internal ImmutableArray<FileName> AssemblyNames { get; }

        /// <summary>
        /// Names of zip containers that need to be examined for signing. This is a subset of <see cref="FileNames"/>
        /// </summary>
        internal ImmutableArray<FileName> ZipContainerNames { get; }

        /// <summary>
        /// Names of other file types which aren't specifically handled by the tool. This is a subset of <see cref="FileNames"/>
        /// </summary>
        internal ImmutableArray<FileName> OtherNames { get; }

        /// <summary>
        /// A map from all of the binaries that need to be signed to the actual signing data.
        /// </summary>
        internal ImmutableDictionary<FileName, FileSignInfo> FileSignInfoMap { get; }

        internal OrchestratedBatchSignInput(string outputPath, Dictionary<string, SignInfo> fileSignDataMap, IEnumerable<string> externalFileNames, string publishUri)
        {
            OutputPath = outputPath;
            PublishUri = publishUri;
            // Use order by to make the output of this tool as predictable as possible.
            var fileNames = fileSignDataMap.Keys;
            FileNames = fileNames.OrderBy(x => x).Select(x => new FileName(outputPath, x)).ToImmutableArray();
            ExternalFileNames = externalFileNames.OrderBy(x => x).ToImmutableArray();

            AssemblyNames = FileNames.Where(x => x.IsAssembly).ToImmutableArray();
            ZipContainerNames = FileNames.Where(x => x.IsZipContainer).ToImmutableArray();
            OtherNames = FileNames.Where(x => !x.IsAssembly && !x.IsZipContainer).ToImmutableArray();

            var builder = ImmutableDictionary.CreateBuilder<FileName, FileSignInfo>();
            foreach (var name in FileNames)
            {
                var data = fileSignDataMap[name.RelativePath];
                builder.Add(name, new FileSignInfo(name, data));
            }
            FileSignInfoMap = builder.ToImmutable();
        }
    }
}

