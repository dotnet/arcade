// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.Helix.Client.Models;

namespace Microsoft.DotNet.HelixPoolProvider.Models
{
    public class AgentDefinitionItem
    {
        public AgentDefinitionItem(QueueInfo info, string agentDefinitionUrl)
        {
            identifier = info.QueueId;
            url = agentDefinitionUrl;
            metadataDocumentUrl = null;
            workspacePath = info.WorkspacePath;
            isInternal = info.IsInternalOnly;
            available = info.IsAvailable;
        }

        // Required data
        public string identifier { get; set; }
        public string url { get; set; }
        public string metadataDocumentUrl { get; set; }

        // Optional data
        public string workspacePath { get; set; }
        public bool isInternal { get; set; }
        public bool available { get; set; }
    }
}
