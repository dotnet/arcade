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
        public SubscriptionHistoryItem(SubscriptionUpdateHistoryEntry other)
        {
            SubscriptionId = other.SubscriptionId;
            Action = other.Action;
            Success = other.Success;
            ErrorMessage = other.ErrorMessage;
            Timestamp = DateTime.SpecifyKind(other.Timestamp, DateTimeKind.Utc);
        }

        public DateTimeOffset Timestamp { get; }

        public string ErrorMessage { get; }

        public bool Success { get; }

        public Guid SubscriptionId { get; }

        public string Action { get; }
    }
}
