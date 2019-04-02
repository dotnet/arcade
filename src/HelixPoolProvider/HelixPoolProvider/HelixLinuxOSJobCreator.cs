// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.AspNetCore.Hosting;
using Microsoft.DotNet.Helix.Client;
using Microsoft.DotNet.Helix.Client.Models;
using Microsoft.DotNet.HelixPoolProvider.Models;
using Microsoft.Extensions.Logging;
using System;

namespace Microsoft.DotNet.HelixPoolProvider
{
    public class HelixLinuxOSJobCreator : HelixJobCreator
    {
        public HelixLinuxOSJobCreator(AgentAcquireItem agentRequestItem, QueueInfo queueInfo, IHelixApi api,
            ILoggerFactory loggerFactory, IHostingEnvironment hostingEnvironment,
            Config configuration, string orchestrationId, string jobName)
            : base(agentRequestItem, queueInfo, api, loggerFactory, hostingEnvironment, configuration, orchestrationId, jobName) { }

        private string AgentPayloadFileName => $"vsts-agent-linux-x64-{_agentRequestItem.agentConfiguration.agentVersion}.tar.gz";

        public override Uri AgentPayloadUri => new Uri($"https://vstsagentpackage.azureedge.net/agent/{_agentRequestItem.agentConfiguration.agentVersion}/{AgentPayloadFileName}");

        public override string StartupScriptName => "startagent-linux.sh";

        public override string ConstructCommand()
        {
            // Ensure the file is marked as executable prior to running.  The deployment to the web service
            // removes this.
            return $"chmod +x ./{StartupScriptName}; ./{StartupScriptName} {_queueInfo.WorkspacePath}";
        }
    }
}
