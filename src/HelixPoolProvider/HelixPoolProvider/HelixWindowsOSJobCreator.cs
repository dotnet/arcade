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
    public class HelixWindowsOSJobCreator : HelixJobCreator
    {
        public HelixWindowsOSJobCreator(AgentAcquireItem agentRequestItem, QueueInfo queueInfo, IHelixApi api,
            ILoggerFactory loggerFactory, IHostingEnvironment hostingEnvironment,
            Config configuration, string orchestrationId, string jobName)
            : base(agentRequestItem, queueInfo, api, loggerFactory, hostingEnvironment, configuration, orchestrationId, jobName) { }

        public override string ConstructCommand()
        {
            // The Helix DB tends to have the workspace path with a lot of extra \\'s.  Replace these. with \
            String workspacePath = _queueInfo.WorkspacePath.Replace(@"\\", @"\");
            return $"{StartupScriptName} {workspacePath}";
        }

        public override Uri AgentPayloadUri => new Uri($"https://vstsagentpackage.azureedge.net/agent/{_agentRequestItem.agentConfiguration.agentVersion}/vsts-agent-win-x64-{_agentRequestItem.agentConfiguration.agentVersion}.zip");

        public override string StartupScriptName => "startagent-win.cmd";
    }
}
