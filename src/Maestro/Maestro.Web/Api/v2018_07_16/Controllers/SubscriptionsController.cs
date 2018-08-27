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
        public IActionResult Get(string sourceRepository = null, string targetRepository = null, int? channelId = null)
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
