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
    internal class AddDefaultChannelOperation : Operation
    {
        AddDefaultChannelCommandLineOptions _options;
        public AddDefaultChannelOperation(AddDefaultChannelCommandLineOptions options)
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

                await remote.AddDefaultChannelAsync(_options.Repository, _options.Branch, _options.Channel);

                return Constants.SuccessCode;
            }
            catch (Exception e)
            {
                Logger.LogError(e, "Error: Failed to add a new default channel association.");
                return Constants.ErrorCode;
            }
        }
    }
}
