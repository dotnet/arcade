// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.Darc.Helpers;
using Microsoft.DotNet.Darc.Options;
using Microsoft.DotNet.DarcLib;
using Microsoft.DotNet.Maestro.Client.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Darc.Operations
{
    internal class DeleteChannelOperation : Operation
    {
        DeleteChannelCommandLineOptions _options;
        public DeleteChannelOperation(DeleteChannelCommandLineOptions options)
            : base(options)
        {
            _options = options;
        }

        /// <summary>
        /// Deletes a channel by name
        /// </summary>
        /// <returns></returns>
        public override async Task<int> ExecuteAsync()
        {
            try
            {
                DarcSettings darcSettings = LocalSettings.GetDarcSettings(_options, Logger);
                // No need to set up a git type or PAT here.
                Remote remote = new Remote(darcSettings, Logger);

                // Get the ID of the channel with the specified name.
                Channel existingChannel = (await remote.GetChannelsAsync()).Where(channel => channel.Name.Equals(_options.Name, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();

                if (existingChannel == null)
                {
                    Logger.LogError($"Could not find channel with name '{_options.Name}'");
                    return Constants.ErrorCode;
                }

                await remote.DeleteChannelAsync(existingChannel.Id.Value);
                Console.WriteLine($"Successfully deleted channel '{existingChannel.Name}'.");

                return Constants.SuccessCode;
            }
            catch (Exception e)
            {
                Logger.LogError(e, "Error: Failed to delete channel.");
                return Constants.ErrorCode;
            }
        }
    }
}
