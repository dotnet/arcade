using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Microsoft.DotNet.Helix.Client;
using Microsoft.DotNet.Helix.Sdk;
using Newtonsoft.Json.Linq;

namespace Microsoft.DotNet.Helix.AzureDevOps
{
    public abstract class AzureDevOpsTask : BaseTask
    {
        private bool InAzurePipeline => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("BUILD_BUILDNUMBER"));

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

        protected abstract Task ExecuteCoreAsync(HttpClient client);

        public override bool Execute()
        {
            try
            {
                if (!InAzurePipeline)
                {
                    Log.LogError("This task must be run inside an Azure Pipelines Build");
                }
                else
                {
                    using (var client = new HttpClient(new HttpClientHandler
                    {
                        AllowAutoRedirect = false,
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
                    })
                    {
                        ExecuteCoreAsync(client).GetAwaiter().GetResult();
                    }
                }
            }
            catch (Exception ex)
            {
                Log.LogErrorFromException(ex, true);
            }

            return !Log.HasLoggedErrors;
        }

        protected Task RetryAsync(Func<Task> function)
        {
            // Grab the retry logic from the helix api client
            return ApiFactory.GetAnonymous()
                .RetryAsync(
                    async () =>
                    {
                        await function();
                        return false; // the retry function requires a return, give it one
                    },
                    ex => Log.LogMessage(MessageImportance.Low, $"Azure Dev Ops Operation failed: {ex}\nRetrying..."));

        }

        protected Task<T> RetryAsync<T>(Func<Task<T>> function)
        {
            // Grab the retry logic from the helix api client
            return ApiFactory.GetAnonymous()
                .RetryAsync(
                    async () => await function(),
                    ex => Log.LogMessage(MessageImportance.Low, $"Azure Dev Ops Operation failed: {ex}\nRetrying..."));

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
    }
}
