using BuildAssetRegistryModel;
using Maestro.Web.Data;
using Maestro.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using System;
using System.Linq;
using System.Linq.Expressions;
using System.Net;

namespace Maestro.Web.Controllers
{
    [Route("api/[controller]")]
    public class BuildsController : BaseController
    {
        private readonly BuildAssetRegistryContext _context;

        public BuildsController()
        {
            _context = new BuildAssetRegistryContext();
        }

        [HttpGet]
        public ActionResult Get(bool loadAll)
        {
            IQueryable<Build> builds = ApplyIncludes(_context.Builds, loadAll);
            return StatusCode((int)HttpStatusCode.OK, builds);
        }

        [HttpGet("build")]
        public ActionResult GetBuild(Build build, bool loadAll)
        {
            ActionResult result = CreateQueryExpression(build, out Expression<Func<Build, bool>> expression);

            if (result != null)
            {
                return result;
            }

            IQueryable<Build> builds = ApplyIncludes(_context.Builds, loadAll, expression);
            Build matchedBuild = builds.FirstOrDefault();

            return HandleNotFoundResponse(matchedBuild, build);
        }

        [HttpGet("id/{id}")]
        public ActionResult GetBuildById(int id, bool loadAll)
        {
            Build build = new Build
            {
                Id = id
            };

            return GetBuild(build, loadAll);
        }

        [HttpGet("latest/{assetName}/channel/{channelName}")]
        public ActionResult GetBuildByAssetNameAndChannel(string assetName, string channelName, bool loadAll)
        {
            assetName = assetName.Replace('*', '%').Replace('?', '%');
            channelName = channelName.Replace('*', '%').Replace('?', '%');

            Expression<Func<Build, bool>> expression = b => b.Channels.Any(c => EF.Functions.Like(c.Name, channelName)) && b.Assets.Any(a => EF.Functions.Like(a.Name, assetName));

            IQueryable<Build> builds = ApplyIncludes(_context.Builds, loadAll, expression);

            Build matchedBuild = builds
                .OrderByDescending(o => o.DateProduced)
                .FirstOrDefault();

            return HandleNotFoundResponse(matchedBuild, new { assetName, channelName });
        }

        [HttpGet("dependencies")]
        public ActionResult GetDependencies(Build build, bool loadAll)
        {
            ActionResult result = CreateQueryExpression(build, out Expression<Func<Build, bool>> expression);
            if (result != null)
            {
                return result;
            }

            Build matchedBuild = ApplyIncludes(_context.Builds, loadAll).FirstOrDefault(expression);

            return matchedBuild == null ? HandleNotFoundResponse(matchedBuild, build) : HandleNotFoundResponse(matchedBuild.Dependencies, build);
        }

        [HttpPut("upsert")]
        public ActionResult Put([FromBody]Build build)
        {
            ActionResult validationResult = ValidateRequestBody(build);

            if (validationResult != null)
            {
                return validationResult;
            }

            try
            {
                EntityEntry<Build> updatedBuild = _context.Builds.Update(build);
                _context.SaveChanges();
                return StatusCode((int)HttpStatusCode.OK, updatedBuild.Entity);
            }
            catch (Exception exc)
            {
                Console.WriteLine(exc);
                throw;
            }
        }

        private ActionResult ValidateRequestBody(Build build)
        {
            string errorMessage = null;

            if (string.IsNullOrEmpty(build.BuildNumber))
            {
                errorMessage = "BuildNumber is null.";
            }
            else if (string.IsNullOrEmpty(build.Commit))
            {
                errorMessage = "Commit is null.";
            }
            else if (build.DateProduced == default(DateTimeOffset))
            {
                errorMessage = "DateProduced is null.";
            }
            else if (!build.Assets.Any())
            {
                errorMessage = "There are no assets in this Build.";
            }

            if (!string.IsNullOrEmpty(errorMessage))
            {
                Error error = new Error(errorMessage);
                return BadRequest(error);
            }

            return null;
        }

        private IQueryable<Build> ApplyIncludes(DbSet<Build> builds, bool loadAll, Expression<Func<Build, bool>> expression = null)
        {
            IQueryable<Build> query = builds.Select(b => b);

            if (expression != null)
            {
                query = query.Where(expression);
            }

            if (loadAll)
            {
                query = query.Include(b => b.Assets).ThenInclude(a => a.Locations)
                     .Include(b => b.Channels)
                     .Include(b => b.Dependencies);
            }

            return query;
        }
    }
}
