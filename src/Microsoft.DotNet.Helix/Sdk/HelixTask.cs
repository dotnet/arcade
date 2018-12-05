using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Microsoft.DotNet.Helix.Client;
using Microsoft.Rest;
using Task = Microsoft.Build.Utilities.Task;

namespace Microsoft.DotNet.Helix.Sdk
{
    public abstract class HelixTask : Task
    {
        /// <summary>
        /// The Helix Api Base Uri
        /// </summary>
        public string BaseUri { get; set; } = "https://helix.dot.net/";

        /// <summary>
        /// The Helix Api Access Token
        /// </summary>
        public string AccessToken { get; set; }

        /// <summary>
        /// A boolean value determining whether this job should be considered "external" for creation purposes
        /// </summary>
        public bool IsExternal { get; set; } = false;

        protected IHelixApi HelixApi { get; private set; }

        protected IHelixApi AnonymousApi { get; private set; }

        private IHelixApi GetHelixApi(bool requestAnonymous = false)
        {
            if (string.IsNullOrEmpty(AccessToken) || requestAnonymous)
            {
                Log.LogMessage(MessageImportance.Low, "No AccessToken provided, using anonymous access to helix api.");
                return ApiFactory.GetAnonymous(BaseUri);
            }

            Log.LogMessage(MessageImportance.Low, "Authenticating to helix api using provided AccessToken");
            return ApiFactory.GetAuthenticated(BaseUri, AccessToken);
        }

        public sealed override bool Execute()
        {
            try
            {
                if (IsExternal)
                {
                    AnonymousApi = GetHelixApi(requestAnonymous: true);
                }
                HelixApi = GetHelixApi();
                System.Threading.Tasks.Task.Run(ExecuteCore).GetAwaiter().GetResult();
            }
            catch (HttpOperationException ex) when (ex.Response.StatusCode == HttpStatusCode.Unauthorized)
            {
                Log.LogError("Helix operation returned 'Unauthorized'. Did you forget to set HelixAccessToken?");
            }
            catch (HttpOperationException ex) when (ex.Response.StatusCode == HttpStatusCode.Forbidden)
            {
                Log.LogError("Helix operation returned 'Forbidden'.");
            }
            catch (Exception ex)
            {
                Log.LogErrorFromException(ex, true, true, null);
            }

            return !Log.HasLoggedErrors;
        }

        protected abstract System.Threading.Tasks.Task ExecuteCore();
    }
}
