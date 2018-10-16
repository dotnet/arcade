// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Maestro.Data;
using Maestro.Web.Api.v2018_07_16.Controllers;
using Microsoft.ApplicationInsights.AspNetCore.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Maestro.Web.Api.v2018_07_16.Models
{
    public class RepositoryHistoryItem
    {
        public RepositoryHistoryItem(RepositoryBranchUpdateHistoryEntry other, IUrlHelper url, HttpContext context)
        {
            RepositoryName = other.Repository;
            BranchName = other.Branch;
            Timestamp = DateTime.SpecifyKind(other.Timestamp, DateTimeKind.Utc);
            ErrorMessage = other.ErrorMessage;
            Success = other.Success;
            Action = other.Action;
            if (!other.Success)
            {
                string pathAndQuery = url.Action(
                    nameof(RepositoryController.RetryActionAsync),
                    new
                    {
                        repository = other.Repository,
                        branch = other.Branch,
                        timestamp = Timestamp.ToUnixTimeSeconds()
                    });
                (string path, string query) = pathAndQuery.Split2('?');
                RetryUrl = new UriBuilder
                {
                    Scheme = "https",
                    Host = context.Request.GetUri().Host,
                    Path = path,
                    Query = query
                }.Uri.AbsoluteUri;
            }
        }

        public string RepositoryName { get; }

        public string BranchName { get; }

        public DateTimeOffset Timestamp { get; }

        public string ErrorMessage { get; }

        public bool Success { get; }

        public string Action { get; }

        public string RetryUrl { get; }
    }
}
