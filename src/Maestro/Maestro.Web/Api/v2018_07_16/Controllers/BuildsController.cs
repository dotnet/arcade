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
using Microsoft.AspNetCore.ApiPagination;
using Microsoft.AspNetCore.ApiVersioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Annotations;

namespace Maestro.Web.Api.v2018_07_16.Controllers
{
    [Route("builds")]
    [ApiVersion("2018-07-16")]
    public class BuildsController : Controller
    {
        private readonly BuildAssetRegistryContext _context;

        public BuildsController(BuildAssetRegistryContext context)
        {
            _context = context;
        }

        [HttpGet]
        [SwaggerResponse((int)HttpStatusCode.OK, Type = typeof(List<Build>))]
        [Paginated(typeof(Build))]
        [ValidateModelState]
        public IActionResult GetAllBuilds(
            string repository,
            string commit,
            string buildNumber,
            int? channelId,
            DateTimeOffset? notBefore,
            DateTimeOffset? notAfter,
            bool? loadCollections)
        {
            IQueryable<Data.Models.Build> query = Query(
                repository,
                commit,
                buildNumber,
                channelId,
                notBefore,
                notAfter,
                loadCollections);
            return Ok(query);
        }

        private IQueryable<Data.Models.Build> Query(
            string repository,
            string commit,
            string buildNumber,
            int? channelId,
            DateTimeOffset? notBefore,
            DateTimeOffset? notAfter,
            bool? loadCollections)
        {
            IQueryable<Data.Models.Build> query = _context.Builds;
            if (!string.IsNullOrEmpty(repository))
            {
                query = query.Where(b => b.Repository == repository);
            }

            if (!string.IsNullOrEmpty(commit))
            {
                query = query.Where(b => b.Commit == commit);
            }

            if (!string.IsNullOrEmpty(buildNumber))
            {
                query = query.Where(b => b.BuildNumber == buildNumber);
            }

            if (notBefore.HasValue)
            {
                query = query.Where(b => b.DateProduced >= notBefore.Value);
            }

            if (notAfter.HasValue)
            {
                query = query.Where(b => b.DateProduced <= notAfter.Value);
            }

            if (channelId.HasValue)
            {
                query = query.Where(b => b.BuildChannels.Any(c => c.ChannelId == channelId.Value));
            }

            if (loadCollections ?? false)
            {
                query = query.Include(b => b.BuildChannels)
                    .ThenInclude(bc => bc.Channel)
                    .Include(b => b.Assets)
                    .Include(b => b.Dependencies);
            }

            return query.OrderByDescending(b => b.DateProduced);
        }

        [HttpGet("{id}")]
        [SwaggerResponse((int)HttpStatusCode.OK, Type = typeof(Build))]
        [ValidateModelState]
        public async Task<IActionResult> GetBuild(int id)
        {
            Data.Models.Build build = await _context.Builds.Where(b => b.Id == id)
                .Include(b => b.BuildChannels)
                .ThenInclude(bc => bc.Channel)
                .Include(b => b.Assets)
                .Include(b => b.Dependencies)
                .FirstOrDefaultAsync();

            if (build == null)
            {
                return NotFound();
            }

            return Ok(new Build(build));
        }

        [HttpGet("latest")]
        [SwaggerResponse((int)HttpStatusCode.OK, Type = typeof(Build))]
        [ValidateModelState]
        public async Task<IActionResult> GetLatest(
            string repository,
            string commit,
            string buildNumber,
            int? channelId,
            DateTimeOffset? notBefore,
            DateTimeOffset? notAfter,
            bool? loadCollections)
        {
            IQueryable<Data.Models.Build> query = Query(
                repository,
                commit,
                buildNumber,
                channelId,
                notBefore,
                notAfter,
                loadCollections);
            Data.Models.Build build = await query.OrderByDescending(o => o.DateProduced).FirstOrDefaultAsync();
            if (build == null)
            {
                return NotFound();
            }

            return Ok(new Build(build));
        }

        [HttpPost]
        [SwaggerResponse((int)HttpStatusCode.Created, Type = typeof(Build))]
        [ValidateModelState]
        public async Task<IActionResult> Create([FromBody] BuildData build)
        {
            Data.Models.Build buildModel = build.ToDb();
            buildModel.DateProduced = DateTimeOffset.UtcNow;
            buildModel.Dependencies = build.Dependencies != null ? await _context.Builds.Where(b => build.Dependencies.Contains(b.Id)).ToListAsync() : null;
            await _context.Builds.AddAsync(buildModel);
            await _context.SaveChangesAsync();
            return CreatedAtRoute(new { action = "GetBuild", id = buildModel.Id }, new Build(buildModel));
        }
    }
}
