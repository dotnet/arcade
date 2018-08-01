using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.DotNet.DarcLib
{
    public class BuildAssetRegistryClient
    {
        private readonly string _password;
        private readonly string _barUri;
        private readonly ILogger _logger;

        public BuildAssetRegistryClient(string barPassword, string barUri, ILogger logger)
        {
            _password = barPassword;
            _barUri = barUri;
            _logger = logger;
        }

        public async Task<string> GetLastestBuildAsync(string repoUri, string branch, string assetName)
        {
            using (HttpClient client = CreateHttpClient())
            {
                _logger.LogInformation($"Getting the latest commit that build asset '{assetName}' in repo '{repoUri}' and branch '{branch}'...");

                HttpRequestManager requestManager = new HttpRequestManager(client, HttpMethod.Get, $"/builds?repository={repoUri}&branch={branch}&assetName={assetName}", _logger);

                HttpResponseMessage response = await requestManager.ExecuteAsync();

                _logger.LogInformation($"Getting the latest commit that build asset '{assetName}' in repo '{repoUri}' and branch '{branch}' succeeded!");

                return await response.Content.ReadAsStringAsync();
            }
        }

        public async Task<string> CreateChannelAsync(string name, string classification)
        {
            _logger.LogInformation($"Creating new channel '{name}' with classification '{classification}'...");

            using (HttpClient client = CreateHttpClient())
            {
                Channel channel = new Channel
                {
                    Classification = classification,
                    Name = name
                };

                JsonSerializerSettings serializerSettings = new JsonSerializerSettings
                {
                    ContractResolver = new CamelCasePropertyNamesContractResolver()
                };

                string body = JsonConvert.SerializeObject(channel, serializerSettings);

                HttpRequestManager requestManager = new HttpRequestManager(client, HttpMethod.Post, "/channels", _logger, body);

                HttpResponseMessage response = await requestManager.ExecuteAsync();

                dynamic responseContent = JsonConvert.DeserializeObject<dynamic>(await response.Content.ReadAsStringAsync());

                _logger.LogInformation($"Creating new channel succeeded! Id is {responseContent.id}.");

                return responseContent.id;
            }
        }

        public async Task<string> GetSubscriptionsAsync(string sourceRepo = null, string targetRepo = null, int? channelId = null)
        {
            _logger.LogInformation("Querying subscriptions...");

            StringBuilder queryParameters = new StringBuilder();

            if (!string.IsNullOrEmpty(sourceRepo))
            {
                queryParameters.Append($"sourceRepository={sourceRepo}&");
            }

            if (!string.IsNullOrEmpty(targetRepo))
            {
                queryParameters.Append($"targetRepository={targetRepo}&");
            }

            if (channelId != null)
            {
                queryParameters.Append($"channelId={channelId}");
            }

            HttpResponseMessage response = await QuerySubscriptionsAsync(queryParameters.ToString());

            _logger.LogInformation("Querying for subscriptions succeeded!");

            return await response.Content.ReadAsStringAsync();
        }

        public async Task<string> GetSubscriptionAsync(int subscriptionId)
        {
            using (HttpClient client = CreateHttpClient())
            {
                _logger.LogInformation($"Querying for a subscription with id {subscriptionId}...");

                HttpRequestManager requestManager = new HttpRequestManager(client, HttpMethod.Get, $"/subscriptions/{subscriptionId}", _logger);

                HttpResponseMessage response = await requestManager.ExecuteAsync();

                _logger.LogInformation($"Querying for a subscription with id {subscriptionId} succeeded!");

                return await response.Content.ReadAsStringAsync();
            }
        }

        public async Task<string> CreateSubscriptionAsync(string channelName, string sourceRepo, string targetRepo, string targetBranch, string updateFrequency, string mergePolicy)
        {
            _logger.LogInformation($"Creating new subscription with channel name '{channelName}', " +
                $"source repo '{sourceRepo}', " +
                $"target repo '{targetRepo}', " +
                $"target branch '{targetBranch}', " +
                $"update frequency '{updateFrequency}' " +
                $"and merge policy '{mergePolicy}'...");

            if (string.IsNullOrEmpty(channelName) || string.IsNullOrEmpty(sourceRepo) || string.IsNullOrEmpty(targetRepo) || string.IsNullOrEmpty(targetBranch) ||
                string.IsNullOrEmpty(updateFrequency) || string.IsNullOrEmpty(mergePolicy))
            {
                throw new ArgumentException("One of the following required fields is missing: channelName, sourceRepo, targetRepo, targetBranch, updateFrequency, mergePolity");
            }

            if (!Enum.TryParse(updateFrequency, out UpdateFrequency frequency))
            {
                throw new FormatException($"Failed to convert '{updateFrequency}' to UpdateFrequency.");
            }

            if (!Enum.TryParse(mergePolicy, out MergePolicy policy))
            {
                throw new FormatException($"Failed to convert '{mergePolicy}' to MergePolicy.");
            }

            using (HttpClient client = CreateHttpClient())
            {
                SubscriptionData subscriptionData = new SubscriptionData
                {
                    ChannelName = channelName,
                    Policy = new SubscriptionPolicy
                    {
                        MergePolicy = policy,
                        UpdateFrequency = frequency
                    },
                    SourceRepository = sourceRepo,
                    TargetBranch = targetBranch,
                    TargetRepository = targetRepo
                };

                JsonSerializerSettings serializerSettings = new JsonSerializerSettings
                {
                    ContractResolver = new CamelCasePropertyNamesContractResolver()
                };

                string body = JsonConvert.SerializeObject(subscriptionData, serializerSettings);

                HttpRequestManager requestManager = new HttpRequestManager(client, HttpMethod.Post, "/subscriptions", _logger, body);

                HttpResponseMessage response = await requestManager.ExecuteAsync();

                dynamic responseContent = JsonConvert.DeserializeObject<dynamic>(await response.Content.ReadAsStringAsync());

                _logger.LogInformation($"Creating new subscription succeeded! Id is {responseContent.id}.");

                return responseContent.id;
            }
        }

        private HttpClient CreateHttpClient()
        {
            if (string.IsNullOrEmpty(_barUri))
            {
                throw new ArgumentException("The BuildAssetRegistryBaseUri is not set.");
            }

            HttpClient client = new HttpClient
            {
                BaseAddress = new Uri(_barUri)
            };
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_password}");

            return client;
        }

        private async Task<HttpResponseMessage> QuerySubscriptionsAsync(string queryParameter)
        {
            using (HttpClient client = CreateHttpClient())
            {
                HttpRequestManager requestManager = new HttpRequestManager(client, HttpMethod.Get, $"/subscriptions?{queryParameter}", _logger);
                return await requestManager.ExecuteAsync();
            }
        }
    }
}
