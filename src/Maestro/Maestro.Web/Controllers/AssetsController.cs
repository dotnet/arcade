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
    public class AssetsController : BaseController
    {
        private readonly BuildAssetRegistryContext _context;

        public AssetsController()
        {
            _context = new BuildAssetRegistryContext();
        }

        [HttpGet]
        public ActionResult Get(bool loadAll)
        {
            IQueryable<Asset> assets = ApplyIncludes(_context.Assets, loadAll);
            return StatusCode((int)HttpStatusCode.OK, assets);
        }

        [HttpGet("asset")]
        public ActionResult GetAsset(Asset asset, bool loadAll)
        {
            ActionResult result = CreateQueryExpression(asset, out Expression<Func<Asset, bool>> expression);

            if (result != null)
            {
                return result;
            }

            IQueryable<Asset> assets = ApplyIncludes(_context.Assets, loadAll, expression);
            Asset matchedAsset = assets.FirstOrDefault();

            return HandleNotFoundResponse(matchedAsset, asset);
        }

        [HttpGet("id/{id}")]
        public ActionResult GetAssetById(int id, bool loadAll)
        {
            Asset asset = new Asset
            {
                Id = id
            };

            return GetAsset(asset, loadAll);
        }

        [HttpGet("inbuild")]
        public ActionResult GetAssetsInBuild(Build build, bool loadAll)
        {
            ActionResult result = CreateQueryExpression(build, out Expression<Func<Build, bool>> expression);

            if (result != null)
            {
                return result;
            }

            Build matchedBuild = _context.Builds.Include(b => b.Assets).FirstOrDefault(expression);

            if (matchedBuild == null)
            {
                return HandleNotFoundResponse(matchedBuild, build);
            }

            IQueryable<Asset> assets = ApplyIncludes(_context.Assets, loadAll);

            return HandleNotFoundResponse(assets, build);
        }

        [HttpGet("{id}/locations")]
        public ActionResult GetLocations(int id)
        {
            Asset asset = _context.Assets.Include(a => a.Locations).FirstOrDefault(b => b.Id == id);

            return asset == null ? HandleNotFoundResponse(asset, id) : HandleNotFoundResponse(asset.Locations, id);
        }

        private IQueryable<Asset> ApplyIncludes(DbSet<Asset> assets, bool loadAll, Expression<Func<Asset, bool>> expression = null)
        {
            IQueryable<Asset> query = assets.Select(b => b);

            if (expression != null)
            {
                query = query.Where(expression);
            }

            if (loadAll)
            {
                query = query.Include(b => b.Locations);
            }

            return query;
        }
    }
}
