using Microsoft.DotNet.Darc.Helpers;
using Microsoft.DotNet.Darc.Options;
using Microsoft.DotNet.DarcLib;
using Microsoft.DotNet.Maestro.Client.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Darc.Operations
{
    internal class DeleteSubscriptionOperation : Operation
    {
        DeleteSubscriptionCommandLineOptions _options;
        public DeleteSubscriptionOperation(DeleteSubscriptionCommandLineOptions options)
            : base(options)
        {
            _options = options;
        }

        public override async Task<int> Execute()
        {
            DarcSettings darcSettings = LocalCommands.GetSettings(_options, Logger);
            // No need to set up a git type or PAT here.
            Remote remote = new Remote(darcSettings, Logger);

            try
            {
                Subscription deletedSubscription = await remote.DeleteSubscriptionAsync(_options.Id);
                Console.WriteLine($"Successfully deleted subscription with id '{_options.Id}'");
                return Constants.SuccessCode;
            }
            catch (ApiErrorException e) when (e.Response.StatusCode == HttpStatusCode.NotFound)
            {
                // Not found is fine to ignore.  If we get this, it will be an aggregate exception with an inner API exception
                // that has a response message code of NotFound.  Return success.
                Console.WriteLine($"Subscription with id '{_options.Id}' does not exist.");
                return Constants.SuccessCode;
            }
            catch (Exception e)
            {
                Logger.LogError(e, $"Failed to delete subscription with id '{_options.Id}'");
                return Constants.ErrorCode;
            }
        }
    }
}
