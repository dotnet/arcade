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
    public partial interface IStorage
    {
        Task<IImmutableList<ContainerInformation>> ListAsync(
            bool getSasTokens = default,
            CancellationToken cancellationToken = default
        );

        Task<ContainerInformation> NewAsync(
            ContainerCreationRequest newContainer,
            CancellationToken cancellationToken = default
        );

        Task<ContainerInformation> ExtendExpirationAsync(
            ContainerExtensionRequest extensionRequest,
            CancellationToken cancellationToken = default
        );

    }

    internal partial class Storage : IServiceOperations<HelixApi>, IStorage
    {
        public Storage(HelixApi client)
        {
            Client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public HelixApi Client { get; }

        partial void HandleFailedRequest(RestApiException ex);

        partial void HandleFailedListRequest(RestApiException ex);

        public async Task<IImmutableList<ContainerInformation>> ListAsync(
            bool getSasTokens = default,
            CancellationToken cancellationToken = default
        )
        {
            using (var _res = await ListInternalAsync(
                getSasTokens,
                cancellationToken
            ).ConfigureAwait(false))
            {
                return _res.Body;
            }
        }

        internal async Task<HttpOperationResponse<IImmutableList<ContainerInformation>>> ListInternalAsync(
            bool getSasTokens = default,
            CancellationToken cancellationToken = default
        )
        {

            var _path = "/api/2018-03-14/storage";

            var _query = new QueryBuilder();
            if (getSasTokens != default)
            {
                _query.Add("getSasTokens", Client.Serialize(getSasTokens));
            }

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
                var _responseContent = await _res.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!_res.IsSuccessStatusCode)
                {
                    var ex = new RestApiException
                    {
                        Request = new HttpRequestMessageWrapper(_req, null),
                        Response = new HttpResponseMessageWrapper(_res, _responseContent),
                    };
                    HandleFailedListRequest(ex);
                    HandleFailedRequest(ex);
                    Client.OnFailedRequest(ex);
                    throw ex;
                }
                return new HttpOperationResponse<IImmutableList<ContainerInformation>>
                {
                    Request = _req,
                    Response = _res,
                    Body = Client.Deserialize<IImmutableList<ContainerInformation>>(_responseContent),
                };
            }
            catch (Exception)
            {
                _req?.Dispose();
                _res?.Dispose();
                throw;
            }
        }

        partial void HandleFailedNewRequest(RestApiException ex);

        public async Task<ContainerInformation> NewAsync(
            ContainerCreationRequest newContainer,
            CancellationToken cancellationToken = default
        )
        {
            using (var _res = await NewInternalAsync(
                newContainer,
                cancellationToken
            ).ConfigureAwait(false))
            {
                return _res.Body;
            }
        }

        internal async Task<HttpOperationResponse<ContainerInformation>> NewInternalAsync(
            ContainerCreationRequest newContainer,
            CancellationToken cancellationToken = default
        )
        {
            if (newContainer == default)
            {
                throw new ArgumentNullException(nameof(newContainer));
            }


            var _path = "/api/2018-03-14/storage";

            var _query = new QueryBuilder();

            var _uriBuilder = new UriBuilder(Client.BaseUri);
            _uriBuilder.Path = _uriBuilder.Path.TrimEnd('/') + _path;
            _uriBuilder.Query = _query.ToString();
            var _url = _uriBuilder.Uri;

            HttpRequestMessage _req = null;
            HttpResponseMessage _res = null;
            try
            {
                _req = new HttpRequestMessage(HttpMethod.Post, _url);

                string _requestContent = null;
                if (newContainer != default)
                {
                    _requestContent = Client.Serialize(newContainer);
                    _req.Content = new StringContent(_requestContent, Encoding.UTF8)
                    {
                        Headers =
                        {
                            ContentType = MediaTypeHeaderValue.Parse("application/json; charset=utf-8"),
                        },
                    };
                }

                if (Client.Credentials != null)
                {
                    await Client.Credentials.ProcessHttpRequestAsync(_req, cancellationToken).ConfigureAwait(false);
                }

                _res = await Client.SendAsync(_req, cancellationToken).ConfigureAwait(false);
                var _responseContent = await _res.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!_res.IsSuccessStatusCode)
                {
                    var ex = new RestApiException
                    {
                        Request = new HttpRequestMessageWrapper(_req, _requestContent),
                        Response = new HttpResponseMessageWrapper(_res, _responseContent),
                    };
                    HandleFailedNewRequest(ex);
                    HandleFailedRequest(ex);
                    Client.OnFailedRequest(ex);
                    throw ex;
                }
                return new HttpOperationResponse<ContainerInformation>
                {
                    Request = _req,
                    Response = _res,
                    Body = Client.Deserialize<ContainerInformation>(_responseContent),
                };
            }
            catch (Exception)
            {
                _req?.Dispose();
                _res?.Dispose();
                throw;
            }
        }

        partial void HandleFailedExtendExpirationRequest(RestApiException ex);

        public async Task<ContainerInformation> ExtendExpirationAsync(
            ContainerExtensionRequest extensionRequest,
            CancellationToken cancellationToken = default
        )
        {
            using (var _res = await ExtendExpirationInternalAsync(
                extensionRequest,
                cancellationToken
            ).ConfigureAwait(false))
            {
                return _res.Body;
            }
        }

        internal async Task<HttpOperationResponse<ContainerInformation>> ExtendExpirationInternalAsync(
            ContainerExtensionRequest extensionRequest,
            CancellationToken cancellationToken = default
        )
        {
            if (extensionRequest == default)
            {
                throw new ArgumentNullException(nameof(extensionRequest));
            }


            var _path = "/api/2018-03-14/storage/renew";

            var _query = new QueryBuilder();

            var _uriBuilder = new UriBuilder(Client.BaseUri);
            _uriBuilder.Path = _uriBuilder.Path.TrimEnd('/') + _path;
            _uriBuilder.Query = _query.ToString();
            var _url = _uriBuilder.Uri;

            HttpRequestMessage _req = null;
            HttpResponseMessage _res = null;
            try
            {
                _req = new HttpRequestMessage(HttpMethod.Post, _url);

                string _requestContent = null;
                if (extensionRequest != default)
                {
                    _requestContent = Client.Serialize(extensionRequest);
                    _req.Content = new StringContent(_requestContent, Encoding.UTF8)
                    {
                        Headers =
                        {
                            ContentType = MediaTypeHeaderValue.Parse("application/json; charset=utf-8"),
                        },
                    };
                }

                if (Client.Credentials != null)
                {
                    await Client.Credentials.ProcessHttpRequestAsync(_req, cancellationToken).ConfigureAwait(false);
                }

                _res = await Client.SendAsync(_req, cancellationToken).ConfigureAwait(false);
                var _responseContent = await _res.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!_res.IsSuccessStatusCode)
                {
                    var ex = new RestApiException
                    {
                        Request = new HttpRequestMessageWrapper(_req, _requestContent),
                        Response = new HttpResponseMessageWrapper(_res, _responseContent),
                    };
                    HandleFailedExtendExpirationRequest(ex);
                    HandleFailedRequest(ex);
                    Client.OnFailedRequest(ex);
                    throw ex;
                }
                return new HttpOperationResponse<ContainerInformation>
                {
                    Request = _req,
                    Response = _res,
                    Body = Client.Deserialize<ContainerInformation>(_responseContent),
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
