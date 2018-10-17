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

namespace Maestro.Web.Api.v2018_07_16.Controllers
{
    [Route("assets")]
    [ApiVersion("2018-07-16")]
    public class AssetsController : Controller
    {
        private readonly BuildAssetRegistryContext _context;

        public AssetsController(BuildAssetRegistryContext context)
        {
            _context = context;
        }

        [HttpGet]
        [SwaggerResponse((int) HttpStatusCode.OK, Type = typeof(List<Asset>))]
        [ValidateModelState]
        public IActionResult Get(string name, [FromQuery] string version, int? buildId, bool? loadLocations)
        {
            IQueryable<Data.Models.Asset> query = _context.Assets;
            if (!string.IsNullOrEmpty(name))
            {
                query = query.Where(asset => asset.Name == name);
            }

            if (!string.IsNullOrEmpty(version))
            {
                query = query.Where(asset => asset.Version == version);
            }

            if (buildId.HasValue)
            {
                query = query.Where(asset => asset.BuildId == buildId.Value);
            }

            if (loadLocations ?? false)
            {
                query = query.Include(asset => asset.Locations);
            }

            List<Asset> results = query.AsEnumerable().Select(asset => new Asset(asset)).ToList();
            return Ok(results);
        }

        [HttpGet("{id}")]
        [SwaggerResponse((int) HttpStatusCode.OK, Type = typeof(Asset))]
        [ValidateModelState]
        public async Task<IActionResult> GetAsset(int id)
        {
            Data.Models.Asset asset = await _context.Assets.Where(a => a.Id == id)
                .Include(a => a.Locations)
                .FirstOrDefaultAsync();

            if (asset == null)
            {
                return NotFound();
            }

            return Ok(new Asset(asset));
        }
    }
}
