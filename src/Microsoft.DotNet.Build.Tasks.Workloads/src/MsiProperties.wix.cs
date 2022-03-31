// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Microsoft.DotNet.Build.Tasks.Workloads
{
    public class MsiProperties
    {
        public long InstallSize
        {
            get;
            set;
        }

        public int Language
        {
            get;
            set;
        }

        public string Payload
        {
            get;
            set;
        }

        public string ProductCode
        {
            get;
            set;
        }

        public string ProductVersion
        {
            get;
            set;
        }

        public string ProviderKeyName
        {
            get;
            set;
        }

        public string UpgradeCode
        {
            get;
            set;
        }

        public IEnumerable<RelatedProduct> RelatedProducts
        {
            get;
            set;
        }
    }
}
