using System;
using Microsoft.Build.Framework;
using Microsoft.DotNet.Helix.Client;
using Microsoft.Rest;
using System.Net;
using System.Threading;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.DotNet.Helix.Sdk;
using Newtonsoft.Json.Linq;

namespace Microsoft.DotNet.Helix.AzureDevOps
{
    /// <summary>
    /// This is a combination of AzureDevOpsTask and HelixTask because we need to be able to use information from Helix jobs in AzDO test results. 
    /// </summary>
    public abstract class AzureDevOpsHelixTask : BaseTask, ICancelableTask
    {
        private bool InAzurePipeline => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("BUILD_BUILDNUMBER"));

        private readonly CancellationTokenSource _cancel = new CancellationTokenSource();

        /// <summary>
        /// The Helix Api Base Uri
        /// </summary>
        public string BaseUri { get; set; } = "https://helix.dot.net/";

        /// <summary>
        /// The Helix Api Access Token (is AccessToken in base HelixTask class)
        /// </summary>
        public string HelixAccessToken { get; set; }

        protected string GetEnvironmentVariable(string name)
        {
            var result = Environment.GetEnvironmentVariable(name);
            if (string.IsNullOrEmpty(result))
            {
                throw new InvalidOperationException($"Required environment variable {name} not set.");
            }

            return result;
        }

        protected string AccessToken => GetEnvironmentVariable("SYSTEM_ACCESSTOKEN");

        protected string CollectionUri => GetEnvironmentVariable("SYSTEM_TEAMFOUNDATIONCOLLECTIONURI");

        protected string TeamProject => GetEnvironmentVariable("SYSTEM_TEAMPROJECT");

        protected string BuildId => GetEnvironmentVariable("BUILD_BUILDID");

        protected abstract Task ExecuteCoreAsync(HttpClient client, CancellationToken cancellationToken);

        protected IHelixApi HelixApi { get; private set; }

        protected IHelixApi AnonymousApi { get; private set; }

        public override bool Execute()
        {
            try
            {
                HelixApi = GetHelixApi();
                AnonymousApi = ApiFactory.GetAnonymous(BaseUri);

                if (!InAzurePipeline)
                {
                    Log.LogError("This task must be run inside an Azure Pipelines Build");
                }
                else
                {
                    using (var client = CreateHttpClient())
                    {
                        ExecuteCoreAsync(client, _cancel.Token).GetAwaiter().GetResult();
                    }
                }
            }
            catch (HttpOperationException ex) when (ex.Response.StatusCode == HttpStatusCode.Unauthorized)
            {
                Log.LogError("Helix operation returned 'Unauthorized'. Did you forget to set HelixAccessToken?");
            }
            catch (HttpOperationException ex) when (ex.Response.StatusCode == HttpStatusCode.Forbidden)
            {
                Log.LogError("Helix operation returned 'Forbidden'.");
            }
            catch (OperationCanceledException ocex) when (ocex.CancellationToken == _cancel.Token)
            {
                // Canceled
                return false;
            }
            catch (Exception ex)
            {
                Log.LogErrorFromException(ex, true, true, null);
            }

            return !Log.HasLoggedErrors;
        }        

        protected Task RetryAsync(Func<Task> function)
        {
            // Grab the retry logic from the helix api client
            return ApiFactory.GetAnonymous().RetryAsync(
                    async () =>
                    {
                        await function();
                        return false; // the retry function requires a return, give it one
                    },
                    ex => Log.LogMessage(MessageImportance.Low, $"Azure Dev Ops Operation failed: {ex}\nRetrying..."),
                    CancellationToken.None);

        }

        protected Task<T> RetryAsync<T>(Func<Task<T>> function)
        {
            // Grab the retry logic from the helix api client
            return ApiFactory.GetAnonymous().RetryAsync(
                    async () => await function(),
                    ex => Log.LogMessage(MessageImportance.Low, $"Azure Dev Ops Operation failed: {ex}\nRetrying..."),
                    CancellationToken.None);

        }

        protected async Task LogFailedRequest(HttpRequestMessage req, HttpResponseMessage res)
        {
            Log.LogError($"Request to {req.RequestUri} returned failed status {(int)res.StatusCode} {res.ReasonPhrase}\n\n{(res.Content != null ? await res.Content.ReadAsStringAsync() : "")}");
            if (res.StatusCode == HttpStatusCode.Found)
            {
                Log.LogError(
                    "A call to an Azure DevOps api returned 302 Indicating a bad 'System.AccessToken' value.\n\nPlease Check the 'Make secrets available to builds of forks' in the pipeline pull request validation trigger settings.\nWe have evaluated the security considerations of this setting and have determined that it is fine to use for our public PR validation builds.");
            }
        }

        protected async Task<JObject> ParseResponseAsync(HttpRequestMessage req, HttpResponseMessage res)
        {
            if (!res.IsSuccessStatusCode)
            {
                await LogFailedRequest(req, res);
                return null;
            }

            var responseContent = await res.Content.ReadAsStringAsync();

            try
            {
                return JObject.Parse(responseContent);
            }
            catch (Exception)
            {
                Log.LogError($"Request to {req.RequestUri} returned unexpected response: {responseContent}");
            }

            return null;
        }       

        private IHelixApi GetHelixApi()
        {
            if (string.IsNullOrEmpty(HelixAccessToken))
            {
                Log.LogMessage(MessageImportance.Low, "No HelixAccessToken provided, using anonymous access to helix api.");
                return ApiFactory.GetAnonymous(BaseUri);
            }

            Log.LogMessage(MessageImportance.Low, "Authenticating to helix api using provided HelixAccessToken");
            return ApiFactory.GetAuthenticated(BaseUri, HelixAccessToken);
        }

        public void Cancel()
        {
            _cancel.Cancel();
        }

        protected void LogExceptionRetry(Exception ex)
        {
            Log.LogMessage(MessageImportance.Low, $"Checking for job completion failed with: {ex}\nRetrying...");
        }

        private HttpClient CreateHttpClient()
        {
            return new HttpClient(new HttpClientHandler
            {
                AllowAutoRedirect = false,
                CheckCertificateRevocationList = true,
            })
            {
                DefaultRequestHeaders =
                        {
                            Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes("unused:" + AccessToken))),
                            UserAgent =
                            {
                                Helpers.UserAgentHeaderValue
                            },
                        },
            };
        }
    }
}
