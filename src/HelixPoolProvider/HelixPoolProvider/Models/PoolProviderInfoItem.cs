// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.HelixPoolProvider.Models
{
    public class PoolProviderInfoItem
    {
        public string poolProviderProtocolVersion { get; set; }
        public string poolProviderVersion { get; set; }
        public string acquireAgentUrl { get; set; }
        public string releaseAgentUrl { get; set; }
        public string getAgentDefinitionsUrl { get; set; }
        public string getAgentRequestStatusUrl { get; set; }
        public string getAccountParallelismUrl { get; set; }
    }
}
