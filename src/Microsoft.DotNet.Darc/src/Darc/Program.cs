// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using CommandLine;
using Microsoft.DotNet.Darc.Operations;
using Microsoft.DotNet.Darc.Options;
using System;

namespace Microsoft.DotNet.Darc
{
    class Program
    {
        static int Main(string[] args)
        {
            return Parser.Default.ParseArguments(args, GetOptions())
                .MapResult( (CommandLineOptions opts) => RunOperation(opts),
                    (errs => 1));
        }

        /// <summary>
        /// Runs the operation and calls dispose afterwards, returning the operation exit code.
        /// </summary>
        /// <param name="operation">Operation to run</param>
        /// <returns>Exit code of the operation</returns>
        /// <remarks>The primary reason for this is a workaround for an issue in the logging factory which
        /// causes it to not dispose the logging providers on process exit.  This causes missed logs, logs that end midway through
        /// and cause issues with the console coloring, etc.</remarks>
        private static int RunOperation(CommandLineOptions opts)
        {
            try
            {
                Operation operation = opts.GetOperation();

                int returnValue = operation.ExecuteAsync().GetAwaiter().GetResult();
                operation.Dispose();
                return returnValue;
            }
            catch (Exception e)
            {
                Console.WriteLine($"Unhandled exception while running {typeof(Operation).Name}");
                Console.WriteLine(e);
                return Constants.ErrorCode;
            }
        }

        private static Type[] GetOptions()
        {
            // This order will mandate the order in which the commands are displayed if typing just 'darc'
            // so keep these sorted.
            return new Type[]
                {
                    typeof(AddChannelCommandLineOptions),
                    typeof(AddCommandLineOptions),
                    typeof(AddDefaultChannelCommandLineOptions),
                    typeof(AddSubscriptionCommandLineOptions),
                    typeof(AuthenticateCommandLineOptions),
                    typeof(DeleteChannelCommandLineOptions),
                    typeof(DeleteDefaultChannelCommandLineOptions),
                    typeof(DeleteSubscriptionCommandLineOptions),
                    typeof(GetChannelsCommandLineOptions),
                    typeof(GetDefaultChannelsCommandLineOptions),
                    typeof(GetDependenciesCommandLineOptions),
                    typeof(GetDependencyGraphCommandLineOptions),
                    typeof(GetSubscriptionHistoryCommandLineOptions),
                    typeof(GetSubscriptionsCommandLineOptions),
                    typeof(RetrySubscriptionUpdateCommandLineOptions),
                    typeof(UpdateDependenciesCommandLineOptions),
                    typeof(VerifyCommandLineOptions),
                };
        }
    }
}
