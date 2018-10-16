// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Maestro.Contracts;
using Maestro.Data;
using Maestro.Data.Models;
using Maestro.Web.Api.v2018_07_16.Models;
using Microsoft.AspNetCore.ApiPagination;
using Microsoft.AspNetCore.ApiVersioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.ServiceFabric.Actors;
using Swashbuckle.AspNetCore.Annotations;

namespace Maestro.Web.Api.v2018_07_16.Controllers
{
    [Route("repo-config")]
    [ApiVersion("2018-07-16")]
    public class RepositoryController : Controller
    {
        public RepositoryController(
            BuildAssetRegistryContext context,
            BackgroundQueue queue,
            Func<ActorId, IPullRequestActor> pullRequestActorFactory)
        {
            Context = context;
            Queue = queue;
            PullRequestActorFactory = pullRequestActorFactory;
        }

        public BuildAssetRegistryContext Context { get; }
        public BackgroundQueue Queue { get; }
        public Func<ActorId, IPullRequestActor> PullRequestActorFactory { get; }

        [HttpGet("merge-policy")]
        [SwaggerResponse((int) HttpStatusCode.OK, Type = typeof(IList<MergePolicy>))]
        public async Task<IActionResult> GetMergePolicies(string repository, string branch)
        {
            RepositoryBranch repoBranch = await Context.RepositoryBranches.FindAsync(repository, branch);
            if (repoBranch == null)
            {
                return NotFound();
            }

            List<MergePolicyDefinition> policies =
                repoBranch.PolicyObject?.MergePolicies ?? new List<MergePolicyDefinition>();
            return Ok(policies.Select(p => new MergePolicy(p)));
        }

        [HttpPost("merge-policy")]
        public async Task<IActionResult> SetMergePolicies(
            string repository,
            string branch,
            [FromBody] IImmutableList<MergePolicy> policies)
        {
            if (string.IsNullOrEmpty(repository))
            {
                ModelState.TryAddModelError(nameof(repository), "The repository parameter is required");
            }

            if (string.IsNullOrEmpty(branch))
            {
                ModelState.TryAddModelError(nameof(branch), "The branch parameter is required");
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            RepositoryBranch repoBranch = await GetRepositoryBranch(repository, branch);
            RepositoryBranch.Policy policy = repoBranch.PolicyObject ?? new RepositoryBranch.Policy();
            policy.MergePolicies = policies?.Select(p => p.ToDb()).ToList() ?? new List<MergePolicyDefinition>();
            repoBranch.PolicyObject = policy;
            await Context.SaveChangesAsync();
            return Ok();
        }

        [HttpGet("history")]
        [SwaggerResponse((int) HttpStatusCode.OK, Type = typeof(List<RepositoryHistoryItem>))]
        [Paginated(typeof(RepositoryHistoryItem))]
        public async Task<IActionResult> GetHistory(string repository, string branch)
        {
            if (string.IsNullOrEmpty(repository))
            {
                ModelState.TryAddModelError(nameof(repository), "The repository parameter is required");
            }

            if (string.IsNullOrEmpty(branch))
            {
                ModelState.TryAddModelError(nameof(branch), "The branch parameter is required");
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            RepositoryBranch repoBranch = await Context.RepositoryBranches.FindAsync(repository, branch);

            if (repoBranch == null)
            {
                return NotFound();
            }

            IOrderedQueryable<RepositoryBranchUpdateHistoryEntry> query = Context.RepositoryBranchUpdateHistory
                .Where(u => u.Repository == repository && u.Branch == branch)
                .OrderByDescending(u => u.Timestamp);

            return Ok(query);
        }

        [HttpPost("retry/{timestamp}")]
        [SwaggerResponse((int) HttpStatusCode.Accepted)]
        public async Task<IActionResult> RetryActionAsync(string repository, string branch, long timestamp)
        {
            if (string.IsNullOrEmpty(repository))
            {
                ModelState.TryAddModelError(nameof(repository), "The repository parameter is required");
            }

            if (string.IsNullOrEmpty(branch))
            {
                ModelState.TryAddModelError(nameof(branch), "The branch parameter is required");
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            DateTime ts = DateTimeOffset.FromUnixTimeSeconds(timestamp).UtcDateTime;

            RepositoryBranch repoBranch = await Context.RepositoryBranches.FindAsync(repository, branch);

            if (repoBranch == null)
            {
                return NotFound();
            }

            RepositoryBranchUpdateHistoryEntry update = await Context.RepositoryBranchUpdateHistory
                .Where(u => u.Repository == repository && u.Branch == branch)
                .FirstOrDefaultAsync(u => Math.Abs(EF.Functions.DateDiffSecond(u.Timestamp, ts)) < 1);

            if (update == null)
            {
                return NotFound();
            }

            if (update.Success)
            {
                return StatusCode(
                    (int) HttpStatusCode.NotAcceptable,
                    new ApiError("That action was successful, it cannot be retried."));
            }

            Queue.Post(
                async () =>
                {
                    IPullRequestActor actor =
                        PullRequestActorFactory(PullRequestActorId.Create(update.Repository, update.Branch));
                    await actor.RunActionAsync(update.Method, update.Arguments);
                });

            return Accepted();
        }

        private async Task<RepositoryBranch> GetRepositoryBranch(string repository, string branch)
        {
            RepositoryBranch repoBranch = await Context.RepositoryBranches.FindAsync(repository, branch);
            if (repoBranch == null)
            {
                Context.RepositoryBranches.Add(
                    repoBranch = new RepositoryBranch
                    {
                        RepositoryName = repository,
                        BranchName = branch
                    });
            }
            else
            {
                Context.RepositoryBranches.Update(repoBranch);
            }

            return repoBranch;
        }
    }
}
