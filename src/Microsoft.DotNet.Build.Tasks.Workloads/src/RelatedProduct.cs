// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Deployment.WindowsInstaller;

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
       
        public string VersionMin
        {
            get;
            set;
        }

        public string VersionMax
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
                VersionMin = string.IsNullOrWhiteSpace(versionMin) ? null : versionMin,
                VersionMax = string.IsNullOrWhiteSpace(versionMax) ? null : versionMax,
                Language = (string)record["Language"],
                Attributes = (int)record["Attributes"]
            };
        }
    }
}
