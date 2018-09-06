using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Maestro.Data;
using Maestro.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace Maestro.Web.Api.v2018_07_16.Models
{
    public class SubscriptionHistoryItem
    {
        public static async Task<List<SubscriptionHistoryItem>> GetAllForSubscription(
            Guid subscriptionId,
            BuildAssetRegistryContext context)
        {
            var result = new List<SubscriptionHistoryItem>();

            var dbItems = await context.SubscriptionUpdates.FromSql(
                @"
SELECT * FROM [SubscriptionUpdates]
FOR SYSTEM_TIME ALL
WHERE [SubscriptionId] = {0}",
                subscriptionId)
                .Select(
                    u => new
                    {
                        u.SubscriptionId,
                        u.Action,
                        u.Success,
                        u.ErrorMessage,
                        SysStartTime = EF.Property<DateTime>(u, "SysStartTime")
                    })
                .OrderByDescending(u => u.SysStartTime)
                .ToListAsync();
            foreach (var item in dbItems)
            {
                result.Add(
                    new SubscriptionHistoryItem(
                        item.SubscriptionId,
                        item.Success,
                        item.Action,
                        item.ErrorMessage,
                        DateTime.SpecifyKind(item.SysStartTime, DateTimeKind.Utc)));
            }

            return result;
        }

        private SubscriptionHistoryItem(Guid subscriptionId, bool success, string action, string errorMessage, DateTimeOffset timestamp)
        {
            SubscriptionId = subscriptionId;
            Action = action;
            Success = success;
            ErrorMessage = errorMessage;
            Timestamp = timestamp;
        }

        public DateTimeOffset Timestamp { get; }

        public string ErrorMessage { get; }

        public bool Success { get; }

        public Guid SubscriptionId { get; }

        public string Action { get; }
    }
}
