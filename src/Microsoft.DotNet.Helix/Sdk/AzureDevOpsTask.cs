using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Microsoft.DotNet.Helix.Client;

namespace Microsoft.DotNet.Helix.AzureDevOps
{
    public abstract class AzureDevOpsTask : Microsoft.Build.Utilities.Task
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
                    Log.LogWarning("Not running inside Azure Pipelines. Task will not be executed.");
                }
                else
                {
                    using (var client = new HttpClient
                    {
                        DefaultRequestHeaders =
                        {
                            Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes("unused:" + AccessToken))),
                            UserAgent =
                            {
                                new ProductInfoHeaderValue(new ProductHeaderValue("HelixSdk", GetVersion())),
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

        protected async Task LogFailedRequest(HttpRequestMessage req, HttpResponseMessage res)
        {
            Log.LogError($"Request to {req.RequestUri} returned failed status {res.StatusCode} {res.ReasonPhrase}\n\n{(res.Content != null ? await res.Content.ReadAsStringAsync() : "")}");
        }

        private string GetVersion()
        {
            return GetType().Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;
        }
    }
}
