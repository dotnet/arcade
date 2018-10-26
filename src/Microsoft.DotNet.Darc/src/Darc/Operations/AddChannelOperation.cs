// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.DotNet.Darc.Helpers;
using Microsoft.DotNet.Darc.Options;
using Microsoft.DotNet.DarcLib;
using Microsoft.DotNet.Maestro.Client.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Net;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Darc.Operations
{
    internal class AddChannelOperation : Operation
    {
        AddChannelCommandLineOptions _options;
        public AddChannelOperation(AddChannelCommandLineOptions options)
            : base(options)
        {
            _options = options;
        }

        /// <summary>
        /// Adds a new channel with the specified name.
        /// </summary>
        /// <returns>Process exit code.</returns>
        public override async Task<int> ExecuteAsync()
        {
            try
            {
                DarcSettings darcSettings = LocalSettings.GetDarcSettings(_options, Logger);
                // No need to set up a git type or PAT here.
                Remote remote = new Remote(darcSettings, Logger);

                // If the user tried to mark as internal, indicate that this is currently
                // unsupported.
                if (_options.Internal)
                {
                    Logger.LogError("Cannot currently mark channels as internal.");
                    return Constants.ErrorCode;
                }

                Channel newChannelInfo = await remote.CreateChannelAsync(_options.Name, _options.Classification);
                Console.WriteLine($"Successfully created new channel with name '{_options.Name}'.");

                return Constants.SuccessCode;
            }
            catch (ApiErrorException e) when (e.Response.StatusCode == HttpStatusCode.Conflict)
            {
                Logger.LogError($"An existing channel with name '{_options.Name}' already exists");
                return Constants.ErrorCode;
            }
            catch (Exception e) 
            {
                Logger.LogError(e, "Error: Failed to create new channel.");
                return Constants.ErrorCode;
            }
        }
    }
}
