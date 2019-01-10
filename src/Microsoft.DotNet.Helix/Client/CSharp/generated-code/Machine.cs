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
    public partial interface IMachine
    {
        Task<MachineInformation> GetMachineStatusAsync(
            string machineName,
            string queueId,
            CancellationToken cancellationToken = default
        );

        Task<ChangeStateResponse> ChangeStateAsync(
            string machineName,
            string queueId,
            MachineStateChangeRequest requestInfo,
            CancellationToken cancellationToken = default
        );

    }

    internal partial class Machine : IServiceOperations<HelixApi>, IMachine
    {
        public Machine(HelixApi client)
        {
            Client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public HelixApi Client { get; }

        partial void HandleFailedRequest(RestApiException ex);

        partial void HandleFailedGetMachineStatusRequest(RestApiException ex);

        public async Task<MachineInformation> GetMachineStatusAsync(
            string machineName,
            string queueId,
            CancellationToken cancellationToken = default
        )
        {
            using (var _res = await GetMachineStatusInternalAsync(
                machineName,
                queueId,
                cancellationToken
            ).ConfigureAwait(false))
            {
                return _res.Body;
            }
        }

        internal async Task<HttpOperationResponse<MachineInformation>> GetMachineStatusInternalAsync(
            string machineName,
            string queueId,
            CancellationToken cancellationToken = default
        )
        {
            if (string.IsNullOrEmpty(machineName))
            {
                throw new ArgumentNullException(nameof(machineName));
            }

            if (string.IsNullOrEmpty(queueId))
            {
                throw new ArgumentNullException(nameof(queueId));
            }


            var _path = "/api/2018-03-14/machines/{queueId}/{machineName}/state";
            _path = _path.Replace("{queueId}", Client.Serialize(queueId));
            _path = _path.Replace("{machineName}", Client.Serialize(machineName));

            var _query = new QueryBuilder();

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
                    HandleFailedGetMachineStatusRequest(ex);
                    HandleFailedRequest(ex);
                    Client.OnFailedRequest(ex);
                    throw ex;
                }
                return new HttpOperationResponse<MachineInformation>
                {
                    Request = _req,
                    Response = _res,
                    Body = Client.Deserialize<MachineInformation>(_responseContent),
                };
            }
            catch (Exception)
            {
                _req?.Dispose();
                _res?.Dispose();
                throw;
            }
        }

        partial void HandleFailedChangeStateRequest(RestApiException ex);

        public async Task<ChangeStateResponse> ChangeStateAsync(
            string machineName,
            string queueId,
            MachineStateChangeRequest requestInfo,
            CancellationToken cancellationToken = default
        )
        {
            using (var _res = await ChangeStateInternalAsync(
                machineName,
                queueId,
                requestInfo,
                cancellationToken
            ).ConfigureAwait(false))
            {
                return _res.Body;
            }
        }

        internal async Task<HttpOperationResponse<ChangeStateResponse>> ChangeStateInternalAsync(
            string machineName,
            string queueId,
            MachineStateChangeRequest requestInfo,
            CancellationToken cancellationToken = default
        )
        {
            if (string.IsNullOrEmpty(machineName))
            {
                throw new ArgumentNullException(nameof(machineName));
            }

            if (string.IsNullOrEmpty(queueId))
            {
                throw new ArgumentNullException(nameof(queueId));
            }

            if (requestInfo == default)
            {
                throw new ArgumentNullException(nameof(requestInfo));
            }


            var _path = "/api/2018-03-14/machines/{queueId}/{machineName}/state";
            _path = _path.Replace("{queueId}", Client.Serialize(queueId));
            _path = _path.Replace("{machineName}", Client.Serialize(machineName));

            var _query = new QueryBuilder();

            var _uriBuilder = new UriBuilder(Client.BaseUri);
            _uriBuilder.Path = _uriBuilder.Path.TrimEnd('/') + _path;
            _uriBuilder.Query = _query.ToString();
            var _url = _uriBuilder.Uri;

            HttpRequestMessage _req = null;
            HttpResponseMessage _res = null;
            try
            {
                _req = new HttpRequestMessage(HttpMethod.Put, _url);

                string _requestContent = null;
                if (requestInfo != default)
                {
                    _requestContent = Client.Serialize(requestInfo);
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
                    HandleFailedChangeStateRequest(ex);
                    HandleFailedRequest(ex);
                    Client.OnFailedRequest(ex);
                    throw ex;
                }
                return new HttpOperationResponse<ChangeStateResponse>
                {
                    Request = _req,
                    Response = _res,
                    Body = Client.Deserialize<ChangeStateResponse>(_responseContent),
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
