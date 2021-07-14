// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Deployment.WindowsInstaller;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Microsoft.DotNet.Build.Tasks.Workloads
{
    // Represents a single row from the MSI Upgrade table.
    public class RelatedProduct
    {
        public string UpgradeCode
        {
            get;
            set;
        }

        [JsonConverter(typeof(VersionConverter))]
        public Version VersionMin
        {
            get;
            set;
        }

        [JsonConverter(typeof(VersionConverter))]
        public Version VersionMax
        {
            get;
            set;
        }

        public string Language
        {
            get;
            set;
        }

        public int Attributes
        {
            get; 
            set;
        }

        public static RelatedProduct Create(Record record)
        {
            string versionMin = (string)record["VersionMin"];
            string versionMax = (string)record["VersionMax"];

            return new RelatedProduct
            {
                UpgradeCode = (string)record["UpgradeCode"],
                VersionMin = string.IsNullOrWhiteSpace(versionMin) ? null : Version.Parse(versionMin),
                VersionMax = string.IsNullOrWhiteSpace(versionMax) ? null : Version.Parse(versionMax),
                Language = (string)record["Language"],
                Attributes = (int)record["Attributes"]
            };
        }
    }
}
