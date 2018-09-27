// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.Darc.Helpers;
using Microsoft.DotNet.Darc.Options;
using Microsoft.DotNet.DarcLib;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;

namespace Microsoft.DotNet.Darc.Operations
{
    /// <summary>
    /// Retrieves a list of subscriptions based on input information
    /// </summary>
    class GetSubscriptionsOperation : Operation
    {
        GetSubscriptionsComandLineOptions _options;
        public GetSubscriptionsOperation(GetSubscriptionsComandLineOptions options)
            : base(options)
        {
            _options = options;
        }

        public override int Execute()
        {
            try
            {
                DarcSettings darcSettings = LocalCommands.GetSettings(_options, Logger);

                // No need to set up a git type or PAT here.
                Remote remote = new Remote(darcSettings, Logger);

                var subscriptions = remote.GetSubscriptionsAsync().Result.Where(subscription => {
                    return (string.IsNullOrEmpty(_options.TargetRepository) ||
                        subscription.TargetRepository.Contains(_options.TargetRepository, StringComparison.OrdinalIgnoreCase)) &&
                    (string.IsNullOrEmpty(_options.TargetBranch) ||
                        subscription.TargetBranch.Contains(_options.TargetBranch, StringComparison.OrdinalIgnoreCase)) &&
                    (string.IsNullOrEmpty(_options.SourceRepository) ||
                        subscription.SourceRepository.Contains(_options.SourceRepository, StringComparison.OrdinalIgnoreCase)) &&
                    (string.IsNullOrEmpty(_options.Channel) ||
                        subscription.Channel.Name.Contains(_options.Channel, StringComparison.OrdinalIgnoreCase));
                    });

                if (subscriptions.Count() == 0)
                {
                    Console.WriteLine("No subscriptions found matching the specified criteria");

                }

                // Based on the current output scheme, sort by source repo, target repo, target branch, etc.
                // Concat the input strings as a simple sorting mechanism.
                foreach (var subscription in subscriptions.OrderBy( subscription =>
                                             $"{subscription.SourceRepository}{subscription.Channel}{subscription.TargetRepository}{subscription.TargetBranch}"))
                {
                    Console.WriteLine($"{subscription.SourceRepository} ({subscription.Channel.Name}) ==> '{subscription.TargetRepository}' ('{subscription.TargetBranch}')");
                    Console.WriteLine($"  Id: {subscription.Id}");
                    Console.WriteLine($"  Update Frequency: {subscription.Policy.UpdateFrequency}");
                    Console.WriteLine($"  Merge Policies:");
                    foreach (var mergePolicy in subscription.Policy.MergePolicies)
                    {
                        Console.WriteLine($"    {mergePolicy.Name}");
                        foreach (var mergePolicyProperty in mergePolicy.Properties)
                        {
                            // Provide appropriate formatting for the value of the merge policy property
                            if (mergePolicyProperty.Value is JArray)
                            {
                                JArray arr = (JArray)mergePolicyProperty.Value;
                                // Write out each value in the array ensuring correct indentation.
                                string keyString = $"      {mergePolicyProperty.Key} = [";
                                int leftPad = keyString.Length;
                                Console.WriteLine(keyString);
                                foreach (var foo in arr)
                                {
                                    Console.WriteLine(foo.ToString().PadRight
                                }
                            }
                            // Default handling using normal ToString
                            else
                            {
                                Console.WriteLine($"      {mergePolicyProperty.Key} = {mergePolicyProperty.Value}");
                            }
                            
                        }
                    }
                    Console.WriteLine($"  Last Build: {(subscription.LastAppliedBuild != null ? subscription.LastAppliedBuild.BuildNumber : "N/A")}");
                }
                return 0;
            }
            catch (Exception e)
            {
                Logger.LogError(e, "Error: Failed to retrieve subscriptions");
                return Constants.ErrorCode;
            }
        }
    }
}
