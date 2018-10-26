// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Maestro.Data;
using Maestro.Web.Api.v2018_07_16.Models;
using Microsoft.AspNetCore.ApiVersioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Annotations;
using Channel = Maestro.Data.Models.Channel;

namespace Maestro.Web.Api.v2018_07_16.Controllers
{
    [Route("default-channels")]
    [ApiVersion("2018-07-16")]
    public class DefaultChannelsController : Controller
    {
        private readonly BuildAssetRegistryContext _context;

        public DefaultChannelsController(BuildAssetRegistryContext context)
        {
            _context = context;
        }

        [HttpGet]
        [SwaggerResponse((int) HttpStatusCode.OK, Type = typeof(List<DefaultChannel>))]
        public IActionResult List(string repository = null, string branch = null, int? channelId = null)
        {
            IQueryable<Data.Models.DefaultChannel> query = _context.DefaultChannels.Include(dc => dc.Channel)
                .AsNoTracking();

            if (!string.IsNullOrEmpty(repository))
            {
                query = query.Where(dc => dc.Repository == repository);
            }

            if (!string.IsNullOrEmpty(branch))
            {
                query = query.Where(dc => dc.Branch == branch);
            }

            if (channelId.HasValue)
            {
                query = query.Where(dc => dc.ChannelId == channelId.Value);
            }

            List<DefaultChannel> results = query.AsEnumerable().Select(dc => new DefaultChannel(dc)).ToList();
            return Ok(results);
        }

        [HttpPost]
        [SwaggerResponse((int) HttpStatusCode.Created)]
        [ValidateModelState]
        [HandleDuplicateKeyRows("A default channel with the same (repository, branch, channel) already exists.")]
        public async Task<IActionResult> Create([FromBody] DefaultChannel.PostData data)
        {
            int channelId = data.ChannelId;
            Channel channel = await _context.Channels.FindAsync(channelId);
            if (channel == null)
            {
                return NotFound(new ApiError($"The channel with id '{channelId}' was not found."));
            }

            var defaultChannel = new Data.Models.DefaultChannel
            {
                Channel = channel,
                Repository = data.Repository,
                Branch = data.Branch
            };
            await _context.DefaultChannels.AddAsync(defaultChannel);
            await _context.SaveChangesAsync();
            return CreatedAtRoute(
                new
                {
                    action = "Get",
                    id = defaultChannel.Id
                },
                new DefaultChannel(defaultChannel));
        }

        [HttpGet("{id}")]
        [SwaggerResponse((int) HttpStatusCode.OK, Type = typeof(DefaultChannel))]
        [ValidateModelState]
        public async Task<IActionResult> Get(int id)
        {
            Data.Models.DefaultChannel defaultChannel = await _context.DefaultChannels.FindAsync(id);
            if (defaultChannel == null)
            {
                return NotFound();
            }

            return Ok(new DefaultChannel(defaultChannel));
        }

        [HttpDelete("{id}")]
        [ValidateModelState]
        [SwaggerResponse((int) HttpStatusCode.Accepted)]
        public async Task<IActionResult> Delete(int id)
        {
            Data.Models.DefaultChannel defaultChannel = await _context.DefaultChannels.FindAsync(id);
            if (defaultChannel == null)
            {
                return NotFound();
            }

            _context.DefaultChannels.Remove(defaultChannel);
            await _context.SaveChangesAsync();
            return StatusCode((int) HttpStatusCode.Accepted);
        }
    }
}
