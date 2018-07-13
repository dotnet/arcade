using BuildAssetRegistryModel;
using Maestro.Web.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Linq.Expressions;
using System.Net;

namespace Maestro.Web.Controllers
{
    [Route("api/[controller]")]
    public class ChannelsController : BaseController
    {
        private readonly BuildAssetRegistryContext _context;

        public ChannelsController()
        {
            _context = new BuildAssetRegistryContext();
        }

        [HttpGet]
        public ActionResult Get()
        {
            return StatusCode((int)HttpStatusCode.OK, _context.Channels);
        }

        [HttpGet("channel")]
        public ActionResult GetChannel(Channel channel)
        {
            ActionResult result = CreateQueryExpression(channel, out Expression<Func<Channel, bool>> expression);

            if (result != null)
            {
                return result;
            }

            Channel matchedChannel = _context.Channels.FirstOrDefault(expression);

            return HandleNotFoundResponse(matchedChannel, channel);
        }

        [HttpGet("id/{id}")]
        public ActionResult GetChannelById(int id)
        {
            Channel channel = new Channel
            {
                Id = id
            };

            return GetChannel(channel);
        }


        [HttpGet("inbuild")]
        public ActionResult GetChannels(Build build)
        {
            ActionResult result = CreateQueryExpression(build, out Expression<Func<Build, bool>> expression);

            if (result != null)
            {
                return result;
            }

            Build matchedBuild = _context.Builds.Include(b => b.Channels).FirstOrDefault(expression);

            return matchedBuild == null ? HandleNotFoundResponse(matchedBuild, build) : HandleNotFoundResponse(matchedBuild.Channels, build);
        }
    }
}
