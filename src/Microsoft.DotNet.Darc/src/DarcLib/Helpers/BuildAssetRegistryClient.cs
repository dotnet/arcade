// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

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
        private readonly string _barUri;
        private readonly ILogger _logger;

        public BuildAssetRegistryClient(string barUri, ILogger logger)
        {
            _barUri = barUri;
            _logger = logger;
        }

        public async Task<string> CreateChannelAsync(string name, string classification, string barPassword)
        {
            _logger.LogInformation($"Creating new channel '{name}' with classification '{classification}'...");

            using (HttpClient client = CreateHttpClient(barPassword))
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

        public async Task<string> GetSubscriptionsAsync(string barPassword, string sourceRepo = null, string targetRepo = null, int? channelId = null)
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

            HttpResponseMessage response = await QuerySubscriptionsAsync(queryParameters.ToString(), barPassword);

            _logger.LogInformation("Querying for subscriptions succeeded!");

            return await response.Content.ReadAsStringAsync();
        }

        public async Task<string> GetSubscriptionAsync(int subscriptionId, string barPassword)
        {
            using (HttpClient client = CreateHttpClient(barPassword))
            {
                _logger.LogInformation($"Querying for a subscription with id {subscriptionId}...");

                HttpRequestManager requestManager = new HttpRequestManager(client, HttpMethod.Get, $"/subscriptions/{subscriptionId}", _logger);

                HttpResponseMessage response = await requestManager.ExecuteAsync();

                _logger.LogInformation($"Querying for a subscription with id {subscriptionId} succeeded!");

                return await response.Content.ReadAsStringAsync();
            }
        }

        public async Task<string> CreateSubscriptionAsync(string channelName, string sourceRepo, string targetRepo, string targetBranch, string updateFrequency, string mergePolicy, string barPassword)
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

            using (HttpClient client = CreateHttpClient(barPassword))
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

        private HttpClient CreateHttpClient(string barPassword)
        {
            if (string.IsNullOrEmpty(_barUri))
            {
                throw new ArgumentException("The BuildAssetRegistryBaseUri is not set.");
            }

            HttpClient client = new HttpClient
            {
                BaseAddress = new Uri(_barUri)
            };
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {barPassword}");

            return client;
        }

        private async Task<HttpResponseMessage> QuerySubscriptionsAsync(string queryParameter, string barPassword)
        {
            using (HttpClient client = CreateHttpClient(barPassword))
            {
                HttpRequestManager requestManager = new HttpRequestManager(client, HttpMethod.Get, $"/subscriptions?{queryParameter}", _logger);
                return await requestManager.ExecuteAsync();
            }
        }
    }
}
