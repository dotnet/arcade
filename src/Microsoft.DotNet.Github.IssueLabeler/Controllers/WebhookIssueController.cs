// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Microsoft.DotNet.GitHub.IssueLabeler
{
    [Route("api/WebhookIssue")]
    public class WebhookIssueController : Controller
    {
        private Labeler Issuelabeler { get; set; }
        private ILogger Logger { get; set; }

        public WebhookIssueController(Labeler labeler, ILogger<WebhookIssueController> logger)
        {
            Issuelabeler = labeler;
            Logger = logger;
        }

        [HttpPost]
        public async Task PostAsync([FromBody]IssueEventPayload data)
        {
            GitHubIssue issue = data.Issue;
            List<object> labels = issue.Labels;

            if (data.Action == "opened" && labels.Count == 0)
            {
                string title = issue.Title;
                int number = issue.Number;
                string body = issue.Body;

                await Issuelabeler.PredictAndApplyLabelAsync(number, title, body, Logger);
                Logger.LogInformation("! Labeling completed");
            }
            else
            {
                Logger.LogInformation($"! The issue {issue.Number.ToString()} is already opened or it already has a label");
            }
        }
    }
}
