// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.Darc.Helpers;
using Microsoft.DotNet.Darc.Options;
using Microsoft.DotNet.DarcLib;
using Microsoft.DotNet.Maestro.Client.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Net;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Darc.Operations
{
    internal class RetrySubscriptionUpdateOperation : Operation
    {
        RetrySubscriptionUpdateCommandLineOptions _options;
        public RetrySubscriptionUpdateOperation(RetrySubscriptionUpdateCommandLineOptions options)
            : base(options)
        {
            _options = options;
        }

        /// <summary>
        /// Retry a specified subscription update.
        /// </summary>
        /// <param name="options">Command line options</param>
        /// <returns>Process exit code.</returns>
        public override async Task<int> ExecuteAsync()
        {
            try
            {
                DarcSettings darcSettings = LocalSettings.GetDarcSettings(_options, Logger);
                // No need to set up a git type or PAT here.
                Remote remote = new Remote(darcSettings, Logger);

                Console.WriteLine("Attempting to retry update...");
                // Attempt to retry the update.
                // TODO: Would be great if the controller returned the update result here.
                await remote.RetrySubscriptionUpdateAsync(_options.SubscriptionId, _options.UpdateId);
                Console.WriteLine("Update retry queued, please check subscription history in a few moments.");
                return Constants.SuccessCode;
            }
            catch (ApiErrorException e) when (e.Response.StatusCode == HttpStatusCode.NotFound)
            {
                Console.WriteLine($"Subscription with id '{_options.SubscriptionId}' or subscription update with id '{_options.UpdateId}' was not found.");
                return Constants.ErrorCode;
            }
            catch (ApiErrorException e) when (e.Response.StatusCode == HttpStatusCode.NotAcceptable)
            {
                Console.WriteLine($"Update '{_options.UpdateId}' did not fail and cannot be retried");
                return Constants.ErrorCode;
            }
            catch (Exception e)
            {
                Logger.LogError(e, "Error: Failed to retry subscription update.");
                return Constants.ErrorCode;
            }
        }
    }
}
