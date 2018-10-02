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
            return Parser.Default.ParseArguments<AuthenticateCommandLineOptions,
                                                 GetCommandLineOptions,
                                                 AddCommandLineOptions,
                                                 GetChannelsCommandLineOptions,
                                                 AddSubscriptionCommandLineOptions,
                                                 DeleteSubscriptionCommandLineOptions>(args)
                .MapResult(
                    (AuthenticateCommandLineOptions opts) => { return RunOperation(new AuthenticateOperation(opts)); },
                    (GetCommandLineOptions opts) => { return RunOperation(new GetOperation(opts)); },
                    (AddCommandLineOptions opts) => { return RunOperation(new AddOperation(opts)); },
                    (GetChannelsCommandLineOptions opts) => { return RunOperation(new GetChannelsOperation(opts)); },
                    (AddSubscriptionCommandLineOptions opts) => { return RunOperation(new AddSubscriptionOperation(opts)); },
                    (DeleteSubscriptionCommandLineOptions opts) => { return RunOperation(new DeleteSubscriptionOperation(opts)); },
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
        private static int RunOperation(Operation operation)
        {
            try
            {
                int returnValue = operation.Execute().GetAwaiter().GetResult();
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
    }
}
