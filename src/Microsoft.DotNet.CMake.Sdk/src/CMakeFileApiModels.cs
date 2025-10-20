// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Microsoft.DotNet.CMake.Sdk
{
    internal class CMakeFileApiIndex
    {
        public CMakeFileApiIndexReply Reply { get; set; }
    }

    internal class CMakeFileApiIndexReply
    {
        [JsonPropertyName("client-Microsoft.DotNet.CMake.Sdk")]
        public CMakeFileApiClientReply ClientReply { get; set; }
    }

    internal class CMakeFileApiClientReply
    {
        [JsonPropertyName("codemodel-v2")]
        public CMakeFileApiIndexCodeModel CodemodelV2 { get; set; }
    }

    internal class CMakeFileApiIndexCodeModel
    {
        public string JsonFile { get; set; }
    }

    internal class CMakeCodeModel
    {
        public CMakeCodeModelPaths Paths { get; set; }
        public List<CMakeConfiguration> Configurations { get; set; }
    }

    internal class CMakeCodeModelPaths
    {
        public string Source { get; set; }
        public string Build { get; set; }
    }

    internal class CMakeConfiguration
    {
        public string Name { get; set; }
        public List<CMakeDirectory> Directories { get; set; }
        public List<CMakeTarget> Targets { get; set; }
    }

    internal class CMakeDirectory
    {
        public string Source { get; set; }
        public string Build { get; set; }
        public List<int> TargetIndexes { get; set; }
    }

    internal class CMakeTarget
    {
        public string Name { get; set; }
        public string JsonFile { get; set; }
    }

    internal class CMakeTargetDetails
    {
        public List<CMakeArtifact> Artifacts { get; set; }
    }

    internal class CMakeArtifact
    {
        public string Path { get; set; }
    }
}
