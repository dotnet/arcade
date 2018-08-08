// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.DotNet.DarcLib
{
    public class HttpRequestManager
    {
        private readonly HttpRequestMessage _message;
        private readonly HttpClient _client;
        private readonly ILogger _logger;

        public HttpRequestManager(HttpClient client, HttpMethod method, string requestUri, ILogger logger, string body = null, string versionOverride = null)
        {
            _client = client;
            _logger = logger;
            _message = new HttpRequestMessage(method, requestUri);

            if (!string.IsNullOrEmpty(body))
            {
                _message.Content = new StringContent(body, Encoding.UTF8, "application/json");
            }
        }

        public async Task<HttpResponseMessage> ExecuteAsync()
        {
            HttpResponseMessage response = await _client.SendAsync(_message);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError($"There was an error executing method '{_message.Method}' against URI '{_message.RequestUri}'. Request failed with error code: '{response.StatusCode}'");
                response.EnsureSuccessStatusCode();
            }

            return response;
        }
    }
}
