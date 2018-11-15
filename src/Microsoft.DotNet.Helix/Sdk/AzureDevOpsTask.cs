using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Helix.AzureDevOps
{
    public abstract class AzureDevOpsTask : Microsoft.Build.Utilities.Task
    {
        private bool InAzDev => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("BUILD_BUILDNUMBER"));

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

        protected HttpClient Client { get; private set; }

        protected abstract Task ExecuteCore();

        public override bool Execute()
        {
            try
            {
                if (!InAzDev)
                {
                    Log.LogWarning("Not running inside Azure Pipelines. Task will not be executed.");
                }
                else
                {
                    using (Client = new HttpClient
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
                        ExecuteCore().GetAwaiter().GetResult();
                    }
                }
            }
            catch (Exception ex)
            {
                Log.LogErrorFromException(ex);
            }

            return !Log.HasLoggedErrors;
        }

        protected async Task LogFailedRequest(HttpRequestMessage req, HttpResponseMessage res)
        {
            Log.LogError($"Request to {req.RequestUri} returned failed status {res.StatusCode} {res.ReasonPhrase}\n\n{await res.Content.ReadAsStringAsync()}");
        }

        private string GetVersion()
        {
            return GetType().Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;
        }
    }
}