// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.Darc.Helpers;
using Microsoft.DotNet.Darc.Options;
using Microsoft.DotNet.DarcLib;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Darc.Operations
{
    /// <summary>
    /// Retrieves a list of subscriptions based on input information
    /// </summary>
    class GetSubscriptionsOperation : Operation
    {
        GetSubscriptionsCommandLineOptions _options;
        public GetSubscriptionsOperation(GetSubscriptionsCommandLineOptions options)
            : base(options)
        {
            _options = options;
        }

        public override async Task<int> ExecuteAsync()
        {
            try
            {
                DarcSettings darcSettings = LocalSettings.GetDarcSettings(_options, Logger);

                // No need to set up a git type or PAT here.
                Remote remote = new Remote(darcSettings, Logger);

                var subscriptions = (await remote.GetSubscriptionsAsync()).Where(subscription =>
                {
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
                    Console.WriteLine("No subscriptions found matching the specified criteria.");
                }

                // Based on the current output schema, sort by source repo, target repo, target branch, etc.
                // Concat the input strings as a simple sorting mechanism.
                foreach (var subscription in subscriptions.OrderBy(subscription =>
                                            $"{subscription.SourceRepository}{subscription.Channel}{subscription.TargetRepository}{subscription.TargetBranch}"))
                {
                    Console.WriteLine($"{subscription.SourceRepository} ({subscription.Channel.Name}) ==> '{subscription.TargetRepository}' ('{subscription.TargetBranch}')");
                    Console.WriteLine($"  - Id: {subscription.Id}");
                    Console.WriteLine($"  - Update Frequency: {subscription.Policy.UpdateFrequency}");
                    Console.WriteLine($"  - Merge Policies:");
                    foreach (var mergePolicy in subscription.Policy.MergePolicies)
                    {
                        Console.WriteLine($"    {mergePolicy.Name}");
                        foreach (var mergePolicyProperty in mergePolicy.Properties)
                        {
                            // The merge policy property is a key value pair.  For formatting, turn it into a string.
                            // It's often a JToken, so handle appropriately
                            // 1. If the number of lines in the string is 1, write on same line as key
                            // 2. If the number of lines in the string is more than one, start on new
                            //    line and indent.
                            string valueString = mergePolicyProperty.Value.ToString();
                            string[] valueLines = valueString.Split(System.Environment.NewLine);
                            string keyString = $"      {mergePolicyProperty.Key} = ";
                            Console.Write(keyString);
                            if (valueLines.Length == 1)
                            {
                                Console.WriteLine(valueString);
                            }
                            else
                            {
                                string indentString = new string(' ', keyString.Length);
                                Console.WriteLine();
                                foreach (string line in valueLines)
                                {
                                    Console.WriteLine($"{indentString}{line}");
                                }
                            }
                        }
                    }
                    Console.WriteLine($"  - Last Build: {(subscription.LastAppliedBuild != null ? subscription.LastAppliedBuild.BuildNumber : "N/A")}");
                }
                return Constants.SuccessCode;
            }
            catch (Exception e)
            {
                Logger.LogError(e, "Error: Failed to retrieve subscriptions");
                return Constants.ErrorCode;
            }
        }
    }
}
