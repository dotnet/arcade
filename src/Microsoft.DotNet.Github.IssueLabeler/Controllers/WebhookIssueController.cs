// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
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
            IssueModel issueOrPullRequest = data.Issue ?? data.Pull_Request;
            GithubObjectType issueOrPr = data.Issue == null ? GithubObjectType.PullRequest : GithubObjectType.Issue;

            if (data.Action == "opened" && issueOrPullRequest.Labels.Count == 0)
            {
                string title = issueOrPullRequest.Title;
                int number = issueOrPullRequest.Number;
                string body = issueOrPullRequest.Body;

                await Issuelabeler.PredictAndApplyLabelAsync(number, title, body, issueOrPr, Logger);
                Logger.LogInformation("! Labeling completed");
            }
            else
            {
                Logger.LogInformation($"! The {issueOrPr} {issueOrPullRequest.Number} is already opened or it already has a label");
            }
        }
    }
}
