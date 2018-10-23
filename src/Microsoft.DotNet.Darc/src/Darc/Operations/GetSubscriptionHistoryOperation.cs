// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.Darc.Helpers;
using Microsoft.DotNet.Darc.Options;
using Microsoft.DotNet.DarcLib;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Darc.Operations
{
    internal class GetSubscriptionHistoryOperation : Operation
    {
        GetSubscriptionHistoryCommandLineOptions _options;
        public GetSubscriptionHistoryOperation(GetSubscriptionHistoryCommandLineOptions options)
            : base(options)
        {
            _options = options;
        }

        /// <summary>
        /// Retrieve information about a particular subscription's history.
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

                // Given the ID, grab the subscription history items.
                var history = await remote.GetSubscriptionHistoryAsync(_options.SubscriptionId);

                foreach (var historyItem in history)
                {
                    Console.WriteLine($"{historyItem.Timestamp.Value.LocalDateTime}: ({(historyItem.Success.Value ? "Success" : "Failure")}) - {historyItem.Action}");
                    if (!historyItem.Success.Value)
                    {
                        Console.WriteLine($"  Error Message: {historyItem.ErrorMessage}");
                        Console.WriteLine($"  Retry Command: darc retry-subscription-update --id {_options.SubscriptionId} --update {historyItem.Timestamp.Value.ToUnixTimeSeconds()}");
                    }
                }

                return Constants.SuccessCode;
            }
            catch (Exception e)
            {
                Logger.LogError(e, "Error: Failed to retrieve subscription history");
                return Constants.ErrorCode;
            }
        }
    }
}
