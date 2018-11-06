// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.Darc.Helpers;
using Microsoft.DotNet.Darc.Options;
using Microsoft.DotNet.DarcLib;
using Microsoft.DotNet.DarcLib.Helpers;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Darc.Operations
{
    internal class VerifyOperation : Operation
    {
        VerifyCommandLineOptions _options;
        public VerifyOperation(VerifyCommandLineOptions options)
            : base(options)
        {
            _options = options;
        }

        /// <summary>
        /// Verify that the repository has a correct dependency structure.
        /// </summary>
        /// <param name="options">Command line options</param>
        /// <returns>Process exit code.</returns>
        public override async Task<int> ExecuteAsync()
        {
            Local local = new Local(LocalHelpers.GetGitDir(Logger), Logger);

            try
            {
                if (!(await local.Verify()))
                {
                    Console.WriteLine("Dependency verification failed.");
                    return Constants.ErrorCode;
                }
                Console.WriteLine("Dependency verification succeeded.");
                return Constants.SuccessCode;
            }
            catch (Exception e)
            {
                Logger.LogError(e, "Error: Failed to verify repository dependency state.");
                return Constants.ErrorCode;
            }
        }
    }
}
