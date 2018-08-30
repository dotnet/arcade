// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Maestro.Web.Api.v2018_07_16.Models;
using Maestro.Data;
using Microsoft.AspNetCore.ApiVersioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Annotations;

namespace Maestro.Web.Api.v2018_07_16.Controllers
{
    [Route("subscriptions")]
    [ApiVersion("2018-07-16")]
    public class SubscriptionsController : Controller
    {
        private readonly BuildAssetRegistryContext _context;

        public SubscriptionsController(BuildAssetRegistryContext context)
        {
            _context = context;
        }

        [HttpGet]
        [SwaggerResponse((int) HttpStatusCode.OK, Type = typeof(List<Subscription>))]
        [ValidateModelState]
        public IActionResult GetAllSubscriptions(string sourceRepository = null, string targetRepository = null, int? channelId = null)
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

            List<Subscription> results = query.AsEnumerable().Select(sub => new Subscription(sub)).ToList();
            return Ok(results);
        }

        [HttpGet("{id}")]
        [SwaggerResponse((int) HttpStatusCode.OK, Type = typeof(Subscription))]
        [ValidateModelState]
        public async Task<IActionResult> GetSubscription(Guid id)
        {
            Data.Models.Subscription subscription = await _context.Subscriptions.Where(sub => sub.Id == id)
                .FirstOrDefaultAsync();

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

            bool doUpdate = false;

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
                Data.Models.Channel channel = await _context.Channels.Where(c => c.Name == update.ChannelName)
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
            Data.Models.Subscription subscription = await _context.Subscriptions
                .FirstOrDefaultAsync(sub => sub.Id == id);

            if (subscription == null)
            {
                return NotFound();
            }

            Data.Models.SubscriptionUpdate subscriptionUpdate = await _context.SubscriptionUpdates
                .FirstOrDefaultAsync(u => u.SubscriptionId == subscription.Id);

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
        public async Task<IActionResult> GetSubscriptionHistory(Guid id)
        {
            Data.Models.Subscription subscription = await _context.Subscriptions.Where(sub => sub.Id == id)
                .FirstOrDefaultAsync();

            if (subscription == null)
            {
                return NotFound();
            }

            return Ok(await SubscriptionHistoryItem.GetAllForSubscription(id, _context));
        }

        [HttpPost]
        [SwaggerResponse((int) HttpStatusCode.Created, Type = typeof(Subscription))]
        [ValidateModelState]
        public async Task<IActionResult> Create([FromBody] SubscriptionData subscription)
        {
            Data.Models.Channel channel = await _context.Channels.Where(c => c.Name == subscription.ChannelName)
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
                var repoInstallation = await _context.RepoInstallations.FindAsync(subscription.TargetRepository);
                if (repoInstallation == null)
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

            var subscriptionModel = subscription.ToDb();
            subscriptionModel.Channel = channel;
            await _context.Subscriptions.AddAsync(subscriptionModel);
            await _context.SaveChangesAsync();
            return CreatedAtRoute(
                new {action = "GetSubscription", id = subscriptionModel.Id},
                new Subscription(subscriptionModel));
        }
    }
}
