using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Rest;
using Microsoft.DotNet.Helix.Client.Models;

namespace Microsoft.DotNet.Helix.Client
{
    public partial interface ILogSearch
    {
        Task DoBuildSearchAsync(
            DateTimeOffset endDate,
            Newtonsoft.Json.Linq.JToken responseType,
            DateTimeOffset startDate,
            string repository = default,
            string searchString = default,
            CancellationToken cancellationToken = default
        );

        Task DoTestLogSearchAsync(
            DateTimeOffset endDate,
            Newtonsoft.Json.Linq.JToken responseType,
            DateTimeOffset startDate,
            string repository = default,
            string searchString = default,
            CancellationToken cancellationToken = default
        );

    }

    internal partial class LogSearch : IServiceOperations<HelixApi>, ILogSearch
    {
        public LogSearch(HelixApi client)
        {
            Client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public HelixApi Client { get; }

        partial void HandleFailedRequest(RestApiException ex);

        partial void HandleFailedDoBuildSearchRequest(RestApiException ex);

        public async Task DoBuildSearchAsync(
            DateTimeOffset endDate,
            Newtonsoft.Json.Linq.JToken responseType,
            DateTimeOffset startDate,
            string repository = default,
            string searchString = default,
            CancellationToken cancellationToken = default
        )
        {
            using (await DoBuildSearchInternalAsync(
                endDate,
                responseType,
                startDate,
                repository,
                searchString,
                cancellationToken
            ).ConfigureAwait(false))
            {
                return;
            }
        }

        internal async Task OnDoBuildSearchFailed(HttpRequestMessage req, HttpResponseMessage res)
        {
            var content = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
            var ex = new RestApiException<ApiError>(
                new HttpRequestMessageWrapper(req, null),
                new HttpResponseMessageWrapper(res, content),
                Client.Deserialize<ApiError>(content)
                );
            HandleFailedDoBuildSearchRequest(ex);
            HandleFailedRequest(ex);
            Client.OnFailedRequest(ex);
            throw ex;
        }

        internal async Task<HttpOperationResponse> DoBuildSearchInternalAsync(
            DateTimeOffset endDate,
            Newtonsoft.Json.Linq.JToken responseType,
            DateTimeOffset startDate,
            string repository = default,
            string searchString = default,
            CancellationToken cancellationToken = default
        )
        {
            if (endDate == default)
            {
                throw new ArgumentNullException(nameof(endDate));
            }

            if (responseType == default)
            {
                throw new ArgumentNullException(nameof(responseType));
            }

            if (startDate == default)
            {
                throw new ArgumentNullException(nameof(startDate));
            }

            const string apiVersion = "2019-06-17";

            var _path = "/api/logs/search/build";

            var _query = new QueryBuilder();
            if (!string.IsNullOrEmpty(repository))
            {
                _query.Add("repository", Client.Serialize(repository));
            }
            if (!string.IsNullOrEmpty(searchString))
            {
                _query.Add("searchString", Client.Serialize(searchString));
            }
            if (startDate != default)
            {
                _query.Add("startDate", Client.Serialize(startDate));
            }
            if (endDate != default)
            {
                _query.Add("endDate", Client.Serialize(endDate));
            }
            if (responseType != default)
            {
                _query.Add("responseType", Client.Serialize(responseType));
            }
            _query.Add("api-version", Client.Serialize(apiVersion));

            var _uriBuilder = new UriBuilder(Client.BaseUri);
            _uriBuilder.Path = _uriBuilder.Path.TrimEnd('/') + _path;
            _uriBuilder.Query = _query.ToString();
            var _url = _uriBuilder.Uri;

            HttpRequestMessage _req = null;
            HttpResponseMessage _res = null;
            try
            {
                _req = new HttpRequestMessage(HttpMethod.Get, _url);

                if (Client.Credentials != null)
                {
                    await Client.Credentials.ProcessHttpRequestAsync(_req, cancellationToken).ConfigureAwait(false);
                }

                _res = await Client.SendAsync(_req, cancellationToken).ConfigureAwait(false);
                if (!_res.IsSuccessStatusCode)
                {
                    await OnDoBuildSearchFailed(_req, _res);
                }
                string _responseContent = await _res.Content.ReadAsStringAsync().ConfigureAwait(false);
                return new HttpOperationResponse
                {
                    Request = _req,
                    Response = _res,
                };
            }
            catch (Exception)
            {
                _req?.Dispose();
                _res?.Dispose();
                throw;
            }
        }

        partial void HandleFailedDoTestLogSearchRequest(RestApiException ex);

        public async Task DoTestLogSearchAsync(
            DateTimeOffset endDate,
            Newtonsoft.Json.Linq.JToken responseType,
            DateTimeOffset startDate,
            string repository = default,
            string searchString = default,
            CancellationToken cancellationToken = default
        )
        {
            using (await DoTestLogSearchInternalAsync(
                endDate,
                responseType,
                startDate,
                repository,
                searchString,
                cancellationToken
            ).ConfigureAwait(false))
            {
                return;
            }
        }

        internal async Task OnDoTestLogSearchFailed(HttpRequestMessage req, HttpResponseMessage res)
        {
            var content = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
            var ex = new RestApiException<ApiError>(
                new HttpRequestMessageWrapper(req, null),
                new HttpResponseMessageWrapper(res, content),
                Client.Deserialize<ApiError>(content)
                );
            HandleFailedDoTestLogSearchRequest(ex);
            HandleFailedRequest(ex);
            Client.OnFailedRequest(ex);
            throw ex;
        }

        internal async Task<HttpOperationResponse> DoTestLogSearchInternalAsync(
            DateTimeOffset endDate,
            Newtonsoft.Json.Linq.JToken responseType,
            DateTimeOffset startDate,
            string repository = default,
            string searchString = default,
            CancellationToken cancellationToken = default
        )
        {
            if (endDate == default)
            {
                throw new ArgumentNullException(nameof(endDate));
            }

            if (responseType == default)
            {
                throw new ArgumentNullException(nameof(responseType));
            }

            if (startDate == default)
            {
                throw new ArgumentNullException(nameof(startDate));
            }

            const string apiVersion = "2019-06-17";

            var _path = "/api/logs/search/test";

            var _query = new QueryBuilder();
            if (!string.IsNullOrEmpty(repository))
            {
                _query.Add("repository", Client.Serialize(repository));
            }
            if (!string.IsNullOrEmpty(searchString))
            {
                _query.Add("searchString", Client.Serialize(searchString));
            }
            if (startDate != default)
            {
                _query.Add("startDate", Client.Serialize(startDate));
            }
            if (endDate != default)
            {
                _query.Add("endDate", Client.Serialize(endDate));
            }
            if (responseType != default)
            {
                _query.Add("responseType", Client.Serialize(responseType));
            }
            _query.Add("api-version", Client.Serialize(apiVersion));

            var _uriBuilder = new UriBuilder(Client.BaseUri);
            _uriBuilder.Path = _uriBuilder.Path.TrimEnd('/') + _path;
            _uriBuilder.Query = _query.ToString();
            var _url = _uriBuilder.Uri;

            HttpRequestMessage _req = null;
            HttpResponseMessage _res = null;
            try
            {
                _req = new HttpRequestMessage(HttpMethod.Get, _url);

                if (Client.Credentials != null)
                {
                    await Client.Credentials.ProcessHttpRequestAsync(_req, cancellationToken).ConfigureAwait(false);
                }

                _res = await Client.SendAsync(_req, cancellationToken).ConfigureAwait(false);
                if (!_res.IsSuccessStatusCode)
                {
                    await OnDoTestLogSearchFailed(_req, _res);
                }
                string _responseContent = await _res.Content.ReadAsStringAsync().ConfigureAwait(false);
                return new HttpOperationResponse
                {
                    Request = _req,
                    Response = _res,
                };
            }
            catch (Exception)
            {
                _req?.Dispose();
                _res?.Dispose();
                throw;
            }
        }
    }
}
