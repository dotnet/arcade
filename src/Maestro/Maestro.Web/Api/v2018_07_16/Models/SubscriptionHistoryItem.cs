using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Maestro.Data;
using Maestro.Data.Models;
using Maestro.Web.Api.v2018_07_16.Controllers;
using Microsoft.ApplicationInsights.AspNetCore.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.EntityFrameworkCore;

namespace Maestro.Web.Api.v2018_07_16.Models
{
    public class SubscriptionHistoryItem
    {
        public SubscriptionHistoryItem(SubscriptionUpdateHistoryEntry other, IUrlHelper url, HttpContext context)
        {
            SubscriptionId = other.SubscriptionId;
            Action = other.Action;
            Success = other.Success;
            ErrorMessage = other.ErrorMessage;
            Timestamp = DateTime.SpecifyKind(other.Timestamp, DateTimeKind.Utc);
            if (!other.Success)
            {
                RetryUrl = new UriBuilder
                {
                    Scheme = "https",
                    Host = context.Request.GetUri().Host,
                    Path = url.Action(
                        nameof(SubscriptionsController.RetrySubscriptionActionAsync),
                        new {id = other.SubscriptionId, timestamp = Timestamp.ToUnixTimeSeconds()}),
                }.Uri.AbsoluteUri;
            }
        }

        public DateTimeOffset Timestamp { get; }

        public string ErrorMessage { get; }

        public bool Success { get; }

        public Guid SubscriptionId { get; }

        public string Action { get; }

        public string RetryUrl { get; }
    }
}
