// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommandLine;
using Microsoft.DotNet.Darc.Operations;

namespace Microsoft.DotNet.Darc.Options
{
    [Verb("get-subscription-history", HelpText = "View information about the history of a subscription.")]
    internal class GetSubscriptionHistoryCommandLineOptions : CommandLineOptions
    {
        [Option('i', "id", Required = true, HelpText = "ID of subscription.  To obtain subscription ID's, use the get-subscriptions verb.")]
        public string SubscriptionId { get; set; }

        public override Operation GetOperation()
        {
            return new GetSubscriptionHistoryOperation(this);
        }
    }
}
