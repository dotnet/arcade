// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommandLine;
using Microsoft.DotNet.Darc.Operations;

namespace Microsoft.DotNet.Darc.Options
{
    [Verb("retry-subscription-update", HelpText = "Retry a failed subscription update.")]
    internal class RetrySubscriptionUpdateCommandLineOptions : CommandLineOptions
    {
        [Option('i', "id", Required = true, HelpText = "ID of subscription.  To obtain subscription ID's, use the get-subscriptions verb.")]
        public string SubscriptionId { get; set; }

        [Option('u', "update", Required = true, HelpText = "Timestamp of update to retry.  You can view failed updates using the get-subscription-history verb.")]
        public long UpdateId { get; set; }

        public override Operation GetOperation()
        {
            return new RetrySubscriptionUpdateOperation(this);
        }
    }
}
