// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.ApplicationInsights;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.DotNet.Helix.Client;
using Microsoft.DotNet.Helix.Client.Models;
using Microsoft.DotNet.HelixPoolProvider.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.DotNet.HelixPoolProvider.Controllers
{
    public class PoolProviderController : Controller
    {
        private ILogger _logger;
        private ILoggerFactory _loggerFactory;
        private Config _configuration;
        private IHostingEnvironment _hostingEnvironment;

        public PoolProviderController(ILoggerFactory loggerFactory, IConfiguration config, IHostingEnvironment hostingEnvironment)
        {
            _logger = loggerFactory.CreateLogger<PoolProviderController>();
            _loggerFactory = loggerFactory;
            _configuration = new Config(config, loggerFactory);
            _hostingEnvironment = hostingEnvironment;
        }

        #region Debugging
        private void LogHeaders()
        {
            _logger.LogInformation($"Headers:");
            _logger.LogInformation($"Response has {Request.Headers.Count} headers");
            foreach (var header in Request.Headers)
            {
                _logger.LogInformation($"{header.Key}={header.Value}");
            }
        }

        private void LogRequestBody()
        {
            _logger.LogInformation($"Body:");
            if (Request != null && Request.Body != null)
            {
                Request.Body.Seek(0, System.IO.SeekOrigin.Begin);
                using (System.IO.StreamReader reader = new System.IO.StreamReader(Request.Body, System.Text.Encoding.UTF8))
                {
                    _logger.LogInformation(reader.ReadToEnd());
                }
            }
        }
        #endregion

        private (string orchestrationId, string jobName) ExtractRequestSourceInfo()
        {
            // Attempt to get the orcheration/job info.
            string orchestrationId = "unknown";
            string jobName = "unknown";
            if (Request.Headers.TryGetValue("X-VSS-OrchestrationId", out StringValues headerValue))
            {
                string id = headerValue.FirstOrDefault();
                if (!string.IsNullOrEmpty(id))
                {
                    int firstDot = id.IndexOf(".");
                    if (firstDot != -1)
                    {
                        orchestrationId = id.Substring(0, firstDot);
                        if (firstDot != id.Length - 1)
                        {
                            jobName = id.Substring(firstDot + 1);
                        }
                    }
                }
            }

            return (orchestrationId, jobName);
        }

        /// <summary>
        /// Acquire an agent from the pool provider.
        /// </summary>
        /// <param name="agentRequestItem">Info about the agent being requested.</param>
        /// <returns>New agent info if agent is able to be provided, blob indicating that the request could not be accepted otherwise.</returns>
        /// <remarks>https://github.com/Microsoft/vsts-pool-providers/blob/master/docs/subdocs/httpspec.md#acquireagent---required</remarks>
        [HttpPost("/acquireagent", Name = nameof(AcquireAgent))]
        [ValidateModelState]
        [Authorize(Policy = "ValidAzDORequestSource")]
        public async Task<IActionResult> AcquireAgent([FromBody] AgentAcquireItem agentRequestItem)
        {
            (string orchestrationId, string jobName) = ExtractRequestSourceInfo();

            using (_logger.BeginScope("Starting acquire operation for " +
                "Agent Id={agentId} Pool={agentPool} OrchestrationId={orchestrationId} JobName={jobName}",
                agentRequestItem.agentId, agentRequestItem.agentPool, orchestrationId, jobName))
            {
                // To acquire a new agent, we'll need to do the following:
                // 1. Determine what queue VSTS is asking for.
                // 2. Determine whether such a queue exists in Helix (and whether we can use it)
                // 3. If the queue exists, submit work to it.

                string queueId;
                try
                {
                    queueId = ExtractQueueId(agentRequestItem.agentSpecification);

                    if (queueId == null)
                    {
                        return Json(new AgentInfoItem() { accepted = false });
                    }
                }
                catch (Exception e)
                {
                    _logger.LogError($"Unable to extract queue id from request: {e.ToString()}");
                    return BadRequest();
                }

                var agentSettings = agentRequestItem.agentConfiguration.agentSettings;

                _logger.LogInformation("Acquiring agent for queue {queueId}", queueId);

                var jobCreator = await GetHelixJobCreator(agentRequestItem, queueId, orchestrationId, jobName);
                if (jobCreator == null)
                {
                    return Json(new AgentInfoItem() { accepted = false });
                }
                return Json(await jobCreator.CreateJob());
            }
        }

        /// <summary>
        /// Retrieves a helix job creator specific to the type of queue that queueId is
        /// </summary>
        /// <param name="agentRequestItem">Acquire agent request info</param>
        /// <param name="queueId">Queue id</param>
        /// <returns>New helix job creator, null if queue info could not be obtained or the queue is not able to be used.</returns>
        private async Task<HelixJobCreator> GetHelixJobCreator(AgentAcquireItem agentRequestItem, string queueId, string orchestrationId, string jobName)
        {
            // Check the queue.
            QueueInfo queueInfo = await GetQueueInfo(queueId);
            if (queueInfo == null)
            {
                _logger.LogInformation($"Queue {queueId} does not exist");
                return null;
            }
            else if (!queueInfo.IsAvailable.HasValue || !queueInfo.IsAvailable.Value)
            {
                _logger.LogInformation($"{queueId} exists but is not available");
                return null;
            }
            else if (!IsAllowableQueue(queueInfo))
            {
                _logger.LogInformation($"{queueId} exists and is available but is not allowed based on the security settings of this pool provider.");
                return null;
            }

            // Based on the os string, return the right job creator
            switch (queueInfo.OperatingSystemGroup.ToLowerInvariant())
            {
                case "windows":
                    return new HelixWindowsOSJobCreator(agentRequestItem, queueInfo, GetHelixApi(!queueInfo.IsInternalOnly.Value),
                        _loggerFactory, _hostingEnvironment, _configuration, orchestrationId, jobName);
                case "linux":
                    return new HelixLinuxOSJobCreator(agentRequestItem, queueInfo, GetHelixApi(!queueInfo.IsInternalOnly.Value),
                        _loggerFactory, _hostingEnvironment, _configuration, orchestrationId, jobName);
                case "osx":
                    return new HelixMacOSJobCreator(agentRequestItem, queueInfo, GetHelixApi(!queueInfo.IsInternalOnly.Value),
                        _loggerFactory, _hostingEnvironment, _configuration, orchestrationId, jobName);
                default:
                    throw new NotImplementedException($"Operating system group {queueInfo.OperatingSystemGroup} unexpected");
            }
        }

        /// <summary>
        /// Get the HelixApi based on the settings of this pool provider
        /// </summary>
        /// <returns>For now, just an unauthenticated api client</returns>
        private IHelixApi GetHelixApi(bool isAnonymous)
        {
            IHelixApi api;
            if (isAnonymous)
            {
                api = ApiFactory.GetAnonymous();
            }
            else
            {
                api = ApiFactory.GetAuthenticated(_configuration.ApiAuthorizationPat);
            }
            // Alter the base URI based on configuration.  It's also useful to note that in the current version of the API, the endpoint isn't
            // defaulted to https, and so unless this is done every request will fail.
            api.BaseUri = new Uri(_configuration.HelixEndpoint);
            return api;
        }

        // Verify the queue is available, can be used by this pool provider, etc.
        private async Task<Helix.Client.Models.QueueInfo> GetQueueInfo(string queueId)
        {
            var helixApi = GetHelixApi(true /*isAnonymous*/);
            try
            {
                return await helixApi.Information.QueueInfoAsync(queueId);
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"Error obtaining queue information");
                return null;
            }
        }

        /// <summary>
        /// Extract the agent specification from the blob sent by Azure DevOps.
        /// We expect an agent spec like:
        /// "agentSpecification" :
        /// {
        ///     "queue": "<helix queue name></helix>"
        /// }
        /// </summary>
        /// <param name="agentSpecification">Agent specification blob</param>
        /// <returns>Helix queue name</returns>
        /// <remarks>Rather than tweaking the model to encode the expected agent spec explicitly,
        /// a blob was used to provide better error logging when the user doesn't provide the
        /// correct pool spec in a YAML file.</remarks>
        private string ExtractQueueId(object agentSpecification)
        {
            _logger.LogTrace($"Extracting target queue from agent spec {agentSpecification}");
             
            if (agentSpecification == null)
            {
                _logger.LogError("Agent specification is not present in agent request");
                return null;
            }

            if (!(agentSpecification is JObject))
            {
                _logger.LogError("Agent specification is not in expected format");
                return null;
            }

            JObject agentSpecificationObject = (JObject)agentSpecification;
            JToken queueName;
            if (!agentSpecificationObject.TryGetValue("queue", out queueName))
            {
                _logger.LogError("Could not find 'queue' element under agent specification");
                return null;
            }

            if (queueName.Type != JTokenType.String)
            {
                _logger.LogError("'queue' element should be a string");
                return null;
            }

            // Agent spec isn't currently available
            return queueName.Value<string>();
        }

        /// <summary>
        /// Determine whether the queue can be used based on the pool provider's settings
        /// </summary>
        /// <param name="queueInfo">Queue information</param>
        /// <returns>True if the queue can be used, false otherwise</returns>
        private bool IsAllowableQueue(QueueInfo queueInfo)
        {
            if (_configuration.AllowedTargetQueues == AllowableHelixQueues.Any)
            {
                return true;
            }
            else if (_configuration.AllowedTargetQueues == AllowableHelixQueues.Specific)
            {
                return _configuration.AllowedTargetQueueNames.Any(queueName => queueName.Equals(queueInfo.QueueId, StringComparison.OrdinalIgnoreCase));
            }
            else if (_configuration.AllowedTargetQueues == AllowableHelixQueues.NoInternal ||
                     _configuration.AllowedTargetQueues == AllowableHelixQueues.OnlyInternal)
            {
                if (!queueInfo.IsInternalOnly.HasValue)
                {
                    _logger.LogWarning($"Warning, unknown whether {queueInfo.QueueId} is internal only or not");
                    return false;
                }
                return (_configuration.AllowedTargetQueues == AllowableHelixQueues.NoInternal && !queueInfo.IsInternalOnly.Value) ||
                       (_configuration.AllowedTargetQueues == AllowableHelixQueues.OnlyInternal && queueInfo.IsInternalOnly.Value);
            }
            else
            {
                throw new NotImplementedException($"Unexpected allowed target queue setting '{_configuration.AllowedTargetQueues}'");
            }
        }

        /// <summary>
        /// Release an agent
        /// </summary>
        /// <param name="agentReleaseItem">Agent to release</param>
        /// <returns>Accepted.</returns>
        /// <remarks>https://github.com/Microsoft/vsts-pool-providers/blob/master/docs/subdocs/httpspec.md#releaseagent---required</remarks
        [HttpPost("/releaseagent", Name = nameof(ReleaseAgent))]
        [ValidateModelState]
        [Authorize(Policy = "ValidAzDORequestSource")]
        public IActionResult ReleaseAgent([FromBody] AgentReleaseItem agentReleaseItem)
        {
            (string orchestrationId, string jobName) = ExtractRequestSourceInfo();

            using (_logger.BeginScope("Starting release operation for " +
                "Agent Id={agentId} Pool={agentPool} OrchestrationId={orchestrationId} JobName={jobName}",
                agentReleaseItem.agentId, agentReleaseItem.agentPool, orchestrationId, jobName))
            {
                _logger.LogInformation("Releasing agent corresponding to helix job {helixJob}, work item {workItem}",
                    agentReleaseItem.agentData?.correlationId, agentReleaseItem.agentData?.workItemId);
                // Nothing to do here AFAIK.  VSTS will have shut down the agent connection, causing the agent process to exit.
                // This means that the corresponding Helix work item will be done and can continue to process work.
                return Accepted();
            }
        }

        /// <summary>
        /// Returns a status message json blob based on the agent request status
        /// </summary>
        /// <returns>Json blob with text of status message</returns>
        /// <remarks>https://github.com/Microsoft/vsts-pool-providers/blob/master/docs/subdocs/httpspec.md#getagentrequeststatus---optional</remarks>
        [HttpPost("/status", Name = nameof(GetAgentRequestStatus))]
        [ValidateModelState]
        [Authorize(Policy = "ValidAzDORequestSource")]
        public async Task<IActionResult> GetAgentRequestStatus([FromBody] AgentRequestStatusItem agentRequestStatusItem)
        {
            // Need to know the job correlation id and work item id.
            // Work item id is the agent id, and job correlation is in the agent data.
            string workItemId = agentRequestStatusItem.agentId;
            string correlationId = agentRequestStatusItem.agentData.correlationId;

            WorkItemDetails workItemDetails;
            try
            {
                _logger.LogTrace($"Looking up work item details for agent {workItemId} in Helix Job {correlationId}");

                using (IHelixApi api = GetHelixApi(agentRequestStatusItem.agentData.isPublicQueue))
                {
                    workItemDetails = await api.WorkItem.DetailsAsync(correlationId, workItemId);
                }
            }
            catch (Exception e)
            {
                _logger.LogError($"Failed to find work item {workItemId} in Helix Job {correlationId}:{Environment.NewLine}{e.ToString()}");
                return BadRequest();
            }

            _logger.LogTrace($"Work item {workItemId} in Helix job {correlationId} is {workItemDetails.State}");

            switch (workItemDetails.State.ToLowerInvariant())
            {
                case "running":
                case "finished":
                case "passed":
                    return Json(new AgentStatusItem()
                    {
                        statusMessage = $"Helix work item in job {correlationId} for agent {workItemId} was picked up by machine {workItemDetails.MachineName} and is {workItemDetails.State}"
                    });
                case "unscheduled":
                    return Json(new AgentStatusItem()
                    {
                        statusMessage = $"Helix work item in job {correlationId} for agent {workItemId} is currently {workItemDetails.State} or does not exist."
                    });
                case "waiting":
                    return Json(new AgentStatusItem()
                    {
                        statusMessage = $"Helix work item in job {correlationId} for agent {workItemId} is currently waiting for a machine."
                    });
                case "failed":
                    return Json(new AgentStatusItem()
                    {
                        statusMessage = $"Helix work item in job {correlationId} for agent {workItemId} failed.  Please check the logs."
                    });
                default:
                    throw new NotImplementedException(
                        $"Got unexpected state '{workItemDetails.State}' for agent {workItemId} in job {correlationId}");
            }
        }

        private string GetAgentDefinitionUrl(string queueId)
        {
            return Url.RouteUrl(nameof(GetAgentDefinition),
                                new { agentDefinitionId = queueId },
                                Request.Scheme, Request.Host.Value);
        }

        /// <summary>
        /// Returns agent definitions that are available
        /// </summary>
        /// <param name="agentDefinition">Optional, agent definition </param>
        /// <returns>Json blob with agent definitions.</returns>
        [HttpGet("/agentdefinitions", Name = nameof(ListAgentDefinitions))]
        public async Task<IActionResult> ListAgentDefinitions()
        {
            try
            {
                _logger.LogTrace($"Looking up available helix queues.");

                using (IHelixApi api = GetHelixApi(false))
                {
                    var helixQueues = await api.Information.QueueInfoListAsync();

                    AgentDefinitionsItem agentDefinitions = new AgentDefinitionsItem()
                    {
                        value = helixQueues.Where(q => IsAllowableQueue(q))
                                           .Select<QueueInfo, AgentDefinitionItem>(q =>
                                               new AgentDefinitionItem(q, GetAgentDefinitionUrl(q.QueueId))).ToList()
                    };
                    return new JsonResult(agentDefinitions);
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"Failed to obtain information on available Helix queues");
                return BadRequest();
            }
        }

        /// <summary>
        /// Returns information on a specific agent definition
        /// </summary>
        /// <param name="agentDefinitionId">Agent definition ID.  Matches the helix queue name</param>
        /// <returns></returns>
        [HttpGet("/agentdefinitions/{agentDefinitionId}", Name = nameof(GetAgentDefinition))]
        public async Task<IActionResult> GetAgentDefinition(string agentDefinitionId)
        {
            try
            {
                _logger.LogTrace($"Looking helix queue named {agentDefinitionId}");

                using (IHelixApi api = GetHelixApi(false))
                {
                    var queueInfo = await api.Information.QueueInfoAsync(agentDefinitionId);

                    // Filter the queue info based on the allowable helix queues.
                    if(!IsAllowableQueue(queueInfo))
                    {
                        return NotFound();
                    }

                    return new JsonResult(
                        new AgentDefinitionItem(queueInfo, GetAgentDefinitionUrl(queueInfo.QueueId))
                    );
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"Failed to obtain information on Helix queue {agentDefinitionId}");
                return BadRequest();
            }
        }

        /// <summary>
        /// Returns information about the pool provider
        /// </summary>
        /// <returns></returns>
        [HttpGet("/info", Name = nameof(GetInformation))]
        public IActionResult GetInformation()
        {
            try
            {
                return new JsonResult(new
                {
                    containerName = _configuration.ContainerName,
                    allowedTargetQueues = Enum.GetName(typeof(AllowableHelixQueues), _configuration.AllowedTargetQueues),
                    allowedTargetQueueNames = _configuration.AllowedTargetQueues == AllowableHelixQueues.Specific ? _configuration.AllowedTargetQueueNames : null,
                    helixEndpoint = _configuration.HelixEndpoint,
                    timeoutInMinutes = _configuration.TimeoutInMinutes,
                    availableConnectionString = _configuration.ConnectionStringIsConfigured,
                    availablePat = _configuration.ApiAuthorizationPatIsConfigured,
                    availableSharedSecret = _configuration.SharedSecretIsConfigured
                });
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"Failed to obtain pool provider information");
                return BadRequest();
            }
        }

        /// <summary>
        /// Returns the maximum parallelism for the account.  
        /// </summary>
        /// <param name="accountParallelismItem"></param>
        /// <returns>Json blob indicating the maximum parallelism</returns>
        /// <remarks>https://github.com/Microsoft/vsts-pool-providers/blob/master/docs/subdocs/httpspec.md#getaccountparallelism---optional
        /// Currently returns a configuration item.  Could be more sophisticated and instead return something based on the scaleset info.
        /// </remarks>
        [HttpPost("/parallelism", Name = nameof(GetAccountParallelism))]
        public IActionResult GetAccountParallelism([FromBody] AccountParallelismItem accountParallelismItem)
        {
            return Json(new MaxParallelismItem() { maxParallelism = _configuration.MaxParallelism });
        }

        /// <summary>
        /// Registers a new cloud connection.
        /// </summary>
        /// <param name="registerConnectionItem">Information about the vsts agent pool</param>
        /// <returns>New cloud info</returns>
        /// <remarks>https://github.com/Microsoft/vsts-pool-providers/blob/master/docs/subdocs/httpspec.md#registerconnection---required</remarks>
        [HttpPost("/register", Name = nameof(RegisterConnection))]
        [ValidateModelState]
        public IActionResult RegisterConnection([FromBody] RegisterConnectionItem registerConnectionItem)
        {
            // TODO: Auth/HMAC when this API actually works on the AzDO side.
            return Json(new PoolProviderInfoItem()
            {
                acquireAgentUrl = Url.RouteUrl(nameof(AcquireAgent), null, Request.Scheme, Request.Host.Value),
                releaseAgentUrl = Url.RouteUrl(nameof(ReleaseAgent), null, Request.Scheme, Request.Host.Value),
                getAccountParallelismUrl = Url.RouteUrl(nameof(GetAccountParallelism), null, Request.Scheme, Request.Host.Value),
                getAgentDefinitionsUrl = Url.RouteUrl(nameof(ListAgentDefinitions), null, Request.Scheme, Request.Host.Value),
                getAgentRequestStatusUrl = Url.RouteUrl(nameof(GetAgentRequestStatus), null, Request.Scheme, Request.Host.Value),
                poolProviderProtocolVersion = "1.0.0",
                poolProviderVersion = "1.0"
            });
        }
    }
}
