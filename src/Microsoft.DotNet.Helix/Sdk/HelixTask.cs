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

        protected IHelixApi HelixApi { get; private set; }

        protected IHelixApi AnonymousApi { get; private set; }

        private IHelixApi GetHelixApi()
        {
            if (string.IsNullOrEmpty(AccessToken))
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
                HelixApi = GetHelixApi();
                AnonymousApi = ApiFactory.GetAnonymous(BaseUri);
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
