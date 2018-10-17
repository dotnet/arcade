// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
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
using Channel = Maestro.Data.Models.Channel;
using Subscription = Maestro.Web.Api.v2018_07_16.Models.Subscription;
using SubscriptionUpdate = Maestro.Web.Api.v2018_07_16.Models.SubscriptionUpdate;

namespace Maestro.Web.Api.v2018_07_16.Controllers
{
    [Route("subscriptions")]
    [ApiVersion("2018-07-16")]
    public class SubscriptionsController : Controller
    {
        private readonly BuildAssetRegistryContext _context;
        private readonly BackgroundQueue _queue;
        private readonly Func<ActorId, ISubscriptionActor> _subscriptionActorFactory;

        public SubscriptionsController(
            BuildAssetRegistryContext context,
            BackgroundQueue queue,
            Func<ActorId, ISubscriptionActor> subscriptionActorFactory)
        {
            _context = context;
            _queue = queue;
            _subscriptionActorFactory = subscriptionActorFactory;
        }

        [HttpGet]
        [SwaggerResponse((int) HttpStatusCode.OK, Type = typeof(List<Subscription>))]
        [ValidateModelState]
        public IActionResult GetAllSubscriptions(
            string sourceRepository = null,
            string targetRepository = null,
            int? channelId = null,
            bool? enabled = null)
        {
            IQueryable<Data.Models.Subscription> query = _context.Subscriptions.Include(s => s.Channel);

            if (!string.IsNullOrEmpty(sourceRepository))
            {
                query = query.Where(sub => sub.SourceRepository == sourceRepository);
            }

            if (!string.IsNullOrEmpty(targetRepository))
            {
                query = query.Where(sub => sub.TargetRepository == targetRepository);
            }

            if (channelId.HasValue)
            {
                query = query.Where(sub => sub.ChannelId == channelId.Value);
            }

            if (enabled.HasValue)
            {
                query = query.Where(sub => sub.Enabled == enabled.Value);
            }

            List<Subscription> results = query.AsEnumerable().Select(sub => new Subscription(sub)).ToList();
            return Ok(results);
        }

        [HttpGet("{id}")]
        [SwaggerResponse((int) HttpStatusCode.OK, Type = typeof(Subscription))]
        [ValidateModelState]
        public async Task<IActionResult> GetSubscription(Guid id)
        {
            Data.Models.Subscription subscription = await _context.Subscriptions.Include(sub => sub.LastAppliedBuild)
                .Include(sub => sub.Channel)
                .FirstOrDefaultAsync(sub => sub.Id == id);

            if (subscription == null)
            {
                return NotFound();
            }

            return Ok(new Subscription(subscription));
        }

        [HttpPatch("{id}")]
        [SwaggerResponse((int) HttpStatusCode.OK, Type = typeof(Subscription))]
        [ValidateModelState]
        public async Task<IActionResult> UpdateSubscription(Guid id, [FromBody] SubscriptionUpdate update)
        {
            Data.Models.Subscription subscription = await _context.Subscriptions.Where(sub => sub.Id == id)
                .FirstOrDefaultAsync();

            if (subscription == null)
            {
                return NotFound();
            }

            var doUpdate = false;

            if (!string.IsNullOrEmpty(update.SourceRepository))
            {
                subscription.SourceRepository = update.SourceRepository;
                doUpdate = true;
            }

            if (update.Policy != null)
            {
                subscription.PolicyObject = update.Policy.ToDb();
                doUpdate = true;
            }

            if (!string.IsNullOrEmpty(update.ChannelName))
            {
                Channel channel = await _context.Channels.Where(c => c.Name == update.ChannelName)
                    .FirstOrDefaultAsync();
                if (channel == null)
                {
                    return BadRequest(
                        new ApiError(
                            "The request is invalid",
                            new[] {$"The channel '{update.ChannelName}' could not be found."}));
                }

                subscription.Channel = channel;
                doUpdate = true;
            }

            if (update.Enabled.HasValue)
            {
                subscription.Enabled = update.Enabled.Value;
                doUpdate = true;
            }

            if (doUpdate)
            {
                _context.Subscriptions.Update(subscription);
                await _context.SaveChangesAsync();
            }


            return Ok(new Subscription(subscription));
        }

        [HttpDelete("{id}")]
        [SwaggerResponse((int) HttpStatusCode.OK, Type = typeof(Subscription))]
        [ValidateModelState]
        public async Task<IActionResult> DeleteSubscription(Guid id)
        {
            Data.Models.Subscription subscription =
                await _context.Subscriptions.FirstOrDefaultAsync(sub => sub.Id == id);

            if (subscription == null)
            {
                return NotFound();
            }

            Data.Models.SubscriptionUpdate subscriptionUpdate =
                await _context.SubscriptionUpdates.FirstOrDefaultAsync(u => u.SubscriptionId == subscription.Id);

            if (subscriptionUpdate != null)
            {
                _context.SubscriptionUpdates.Remove(subscriptionUpdate);
            }

            _context.Subscriptions.Remove(subscription);

            await _context.SaveChangesAsync();
            return Ok(new Subscription(subscription));
        }

        [HttpGet("{id}/history")]
        [SwaggerResponse((int) HttpStatusCode.OK, Type = typeof(List<SubscriptionHistoryItem>))]
        [Paginated(typeof(SubscriptionHistoryItem))]
        public async Task<IActionResult> GetSubscriptionHistory(Guid id)
        {
            Data.Models.Subscription subscription = await _context.Subscriptions.Where(sub => sub.Id == id)
                .FirstOrDefaultAsync();

            if (subscription == null)
            {
                return NotFound();
            }

            IOrderedQueryable<SubscriptionUpdateHistoryEntry> query = _context.SubscriptionUpdateHistory
                .Where(u => u.SubscriptionId == id)
                .OrderByDescending(u => u.Timestamp);

            return Ok(query);
        }

        [HttpPost("{id}/retry/{timestamp}")]
        [SwaggerResponse((int) HttpStatusCode.Accepted)]
        public async Task<IActionResult> RetrySubscriptionActionAsync(Guid id, long timestamp)
        {
            DateTime ts = DateTimeOffset.FromUnixTimeSeconds(timestamp).UtcDateTime;

            Data.Models.Subscription subscription = await _context.Subscriptions.Where(sub => sub.Id == id)
                .FirstOrDefaultAsync();

            if (subscription == null)
            {
                return NotFound();
            }

            SubscriptionUpdateHistoryEntry update = await _context.SubscriptionUpdateHistory
                .Where(u => u.SubscriptionId == id)
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

            _queue.Post(
                async () =>
                {
                    ISubscriptionActor actor = _subscriptionActorFactory(new ActorId(subscription.Id));
                    await actor.RunActionAsync(update.Method, update.Arguments);
                });

            return Accepted();
        }


        [HttpPost]
        [SwaggerResponse((int) HttpStatusCode.Created, Type = typeof(Subscription))]
        [ValidateModelState]
        public async Task<IActionResult> Create([FromBody] SubscriptionData subscription)
        {
            Channel channel = await _context.Channels.Where(c => c.Name == subscription.ChannelName)
                .FirstOrDefaultAsync();
            if (channel == null)
            {
                return BadRequest(
                    new ApiError(
                        "the request is invalid",
                        new[] {$"The channel '{subscription.ChannelName}' could not be found."}));
            }

            if (subscription.TargetRepository.Contains("github.com"))
            {
                // If we have no repository information or an invalid installation id
                // then we will fail when trying to update things, so we fail early.
                Repository repo = await _context.Repositories.FindAsync(subscription.TargetRepository);
                if (repo == null || repo.InstallationId <= 0)
                {
                    return BadRequest(
                        new ApiError(
                            "the request is invalid",
                            new[]
                            {
                                $"The repository '{subscription.TargetRepository}' does not have an associated github installation. " +
                                "The Maestro github application must be installed by the repository's owner and given access to the repository."
                            }));
                }
            }

            Data.Models.Subscription subscriptionModel = subscription.ToDb();
            subscriptionModel.Channel = channel;
            await _context.Subscriptions.AddAsync(subscriptionModel);
            await _context.SaveChangesAsync();
            return CreatedAtRoute(
                new
                {
                    action = "GetSubscription",
                    id = subscriptionModel.Id
                },
                new Subscription(subscriptionModel));
        }
    }
}
