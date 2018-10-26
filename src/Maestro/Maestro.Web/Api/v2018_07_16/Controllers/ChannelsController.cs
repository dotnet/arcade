// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Maestro.Data;
using Maestro.Data.Models;
using Microsoft.AspNetCore.ApiVersioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Annotations;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Build = Maestro.Data.Models.Build;
using Channel = Maestro.Web.Api.v2018_07_16.Models.Channel;

namespace Maestro.Web.Api.v2018_07_16.Controllers
{
    [Route("channels")]
    [ApiVersion("2018-07-16")]
    public class ChannelsController : Controller
    {
        private readonly BuildAssetRegistryContext _context;

        public ChannelsController(BuildAssetRegistryContext context)
        {
            _context = context;
        }

        [HttpGet]
        [SwaggerResponse((int)HttpStatusCode.OK, Type = typeof(List<Channel>))]
        [ValidateModelState]
        public IActionResult Get(string classification = null)
        {
            IQueryable<Data.Models.Channel> query = _context.Channels;
            if (!string.IsNullOrEmpty(classification))
            {
                query = query.Where(c => c.Classification == classification);
            }

            List<Channel> results = query.AsEnumerable().Select(c => new Channel(c)).ToList();
            return Ok(results);
        }

        [HttpGet("{id}")]
        [SwaggerResponse((int)HttpStatusCode.OK, Type = typeof(Channel))]
        [ValidateModelState]
        public async Task<IActionResult> GetChannel(int id)
        {
            Data.Models.Channel channel = await _context.Channels.Where(c => c.Id == id).FirstOrDefaultAsync();

            if (channel == null)
            {
                return NotFound();
            }

            return Ok(new Channel(channel));
        }

        [HttpDelete("{id}")]
        [SwaggerResponse((int)HttpStatusCode.OK, Type = typeof(Channel))]
        [ValidateModelState]
        public async Task<IActionResult> DeleteChannel(int id)
        {
            Data.Models.Channel channel = await _context.Channels.FirstOrDefaultAsync(c => c.Id == id);

            if (channel == null)
            {
                return NotFound();
            }

            _context.Channels.Remove(channel);

            await _context.SaveChangesAsync();
            return Ok(new Channel(channel));
        }

        [HttpPost]
        [SwaggerResponse((int)HttpStatusCode.Created, Type = typeof(Channel))]
        [HandleDuplicateKeyRows("Could not create channel '{name}'. A channel with the specified name already exists.")]
        public async Task<IActionResult> CreateChannel([Required] string name, [Required] string classification)
        {
            var channelModel = new Data.Models.Channel
            {
                Name = name,
                Classification = classification
            };
            await _context.Channels.AddAsync(channelModel);
            await _context.SaveChangesAsync();
            return CreatedAtRoute(
                new
                {
                    action = "GetChannel",
                    id = channelModel.Id
                },
                new Channel(channelModel));
        }

        [HttpPost("{channelId}/builds/{buildId}")]
        [SwaggerResponse((int)HttpStatusCode.Created)]
        public async Task<IActionResult> AddBuildToChannel(int channelId, int buildId)
        {
            Data.Models.Channel channel = await _context.Channels.FindAsync(channelId);
            if (channel == null)
            {
                return NotFound(new ApiError($"The channel with id '{channelId}' was not found."));
            }

            Build build = await _context.Builds.FindAsync(buildId);
            if (build == null)
            {
                return NotFound(new ApiError($"The build with id '{buildId}' was not found."));
            }

            var buildChannel = new BuildChannel
            {
                Channel = channel,
                Build = build
            };
            await _context.BuildChannels.AddAsync(buildChannel);
            await _context.SaveChangesAsync();
            return StatusCode((int)HttpStatusCode.Created);
        }
    }
}
