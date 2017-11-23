// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using Newtonsoft.Json;

namespace SignTool.Json
{
    internal sealed class FileJson
    {
        [JsonProperty(PropertyName = "kind")]
        public string Kind { get; set; }

        [JsonProperty(PropertyName = "publishUrl")]
        public string PublishUrl { get; set; }

        [JsonProperty(PropertyName = "sign")]
        public FileSignData[] SignList { get; set; }

        [JsonProperty(PropertyName = "exclude")]
        public string[] ExcludeList { get; set; }

        public FileJson()
        {

        }
    }

    internal sealed class OrchestratedFileJson
    {
        [JsonProperty(PropertyName = "kind")]
        public string Kind { get; set; }

        [JsonProperty(PropertyName = "sign")]
        public OrchestratedFileSignData[] SignList { get; set; }

        [JsonProperty(PropertyName = "exclude")]
        public string[] ExcludeList { get; set; }

        public OrchestratedFileJson()
        {
        }
    }

    internal class FileSignDataBase
    {
        [JsonProperty(PropertyName = "certificate", Order = 1)]
        public string Certificate { get; set; }

        [JsonProperty(PropertyName = "strongName", Order = 2)]
        public string StrongName { get; set; }
    }


    internal sealed class FileSignData : FileSignDataBase
    {
        [JsonProperty(PropertyName = "values", Order = 3)]
        public string[] FileList { get; set; }

        public FileSignData()
        {
        }
    }

    internal sealed class OrchestratedFileSignData : FileSignDataBase
    {
        [JsonProperty(PropertyName = "values", Order = 3)]
        public FileSignDataEntry[] FileList { get; set; }

        public OrchestratedFileSignData()
        {
        }
    }

    internal sealed class FileSignDataEntry
    {
        [JsonProperty(PropertyName = "filePath")]
        public string FilePath { get; set; }

        [JsonProperty(PropertyName = "sha256Hash")]
        public string SHA256Hash { get; set; }

        [JsonProperty(PropertyName = "publishtofeedurl")]
        public string PublishToFeedUrl { get; set; }

        public FileSignDataEntry()
        {
        }
    }
}
