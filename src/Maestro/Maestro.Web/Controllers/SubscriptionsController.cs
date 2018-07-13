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
    public class SubscriptionsController : BaseController
    {
        private readonly BuildAssetRegistryContext _context;

        public SubscriptionsController()
        {
            _context = new BuildAssetRegistryContext();
        }

        [HttpGet]
        public ActionResult Get(bool loadAll)
        {
            IQueryable<Subscription> subscriptions = ApplyIncludes(_context.Subscriptions, loadAll);
            return StatusCode((int)HttpStatusCode.OK, subscriptions);
        }

        [HttpGet("subscription")]
        public ActionResult GetSubscription(Subscription subscription, bool loadAll)
        {
            ActionResult result = CreateQueryExpression(subscription, out Expression<Func<Subscription, bool>> expression);

            if (result != null)
            {
                return result;
            }

            IQueryable<Subscription> subscriptions = ApplyIncludes(_context.Subscriptions, loadAll, expression);
            Subscription matchedSubscription = subscriptions.FirstOrDefault();

            return HandleNotFoundResponse(matchedSubscription, subscription);
        }

        [HttpGet("id/{id}")]
        public ActionResult Get(int id, bool loadAll)
        {
            Subscription subscription = new Subscription
            {
                Id = id
            };

            return GetSubscription(subscription, loadAll);
        }
        
        [HttpPut("upsert")]
        public ActionResult Put([FromBody]Subscription subscription)
        {
            ActionResult validationResult = ValidateRequestBody(subscription);

            if (validationResult != null)
            {
                return validationResult;
            }

            try
            {
                EntityEntry<Subscription> updatedSubscription = _context.Subscriptions.Update(subscription);
                _context.SaveChanges();
                return StatusCode((int)HttpStatusCode.OK, updatedSubscription);
            }
            catch (Exception exc)
            {
                Console.WriteLine(exc);
                throw;
            }
        }

        private ActionResult ValidateRequestBody(Subscription subscription)
        {
            string errorMessage = null;

            if (subscription == null)
            {
                errorMessage = "The Subscription is null.";
            }
            else if (string.IsNullOrEmpty(subscription.SourceRepository))
            {
                errorMessage = "SourceRepository is null.";
            }
            else if (string.IsNullOrEmpty(subscription.TargetRepository))
            {
                errorMessage = "TargetRepository is null.";
            }
            else if (string.IsNullOrEmpty(subscription.TargetBranch))
            {
                errorMessage = "TargetBranch is null.";
            }
            else if (subscription.Policy == null)
            {
                errorMessage = "Policy is null.";
            }

            if (!string.IsNullOrEmpty(errorMessage))
            {
                Error error = new Error(errorMessage);
                return BadRequest(error);
            }

            return null;
        }

        private IQueryable<Subscription> ApplyIncludes(DbSet<Subscription> subscriptions, bool loadAll, Expression<Func<Subscription, bool>> expression = null)
        {
            IQueryable<Subscription> query = subscriptions.Select(b => b);

            if (expression != null)
            {
                query = query.Where(expression);
            }

            if (loadAll)
            {
                query = query.Include(b => b.Policy);
            }

            return query;
        }
    }
}
