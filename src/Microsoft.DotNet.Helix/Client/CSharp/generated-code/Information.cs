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
    public partial interface IInformation
    {
        Task<QueueInfo> QueueInfoAsync(
            string queueId,
            CancellationToken cancellationToken = default
        );

        Task<IImmutableList<QueueInfo>> QueueInfoListAsync(
            CancellationToken cancellationToken = default
        );

    }

    internal partial class Information : IServiceOperations<HelixApi>, IInformation
    {
        public Information(HelixApi client)
        {
            Client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public HelixApi Client { get; }

        partial void HandleFailedRequest(RestApiException ex);

        partial void HandleFailedQueueInfoRequest(RestApiException ex);

        public async Task<QueueInfo> QueueInfoAsync(
            string queueId,
            CancellationToken cancellationToken = default
        )
        {
            using (var _res = await QueueInfoInternalAsync(
                queueId,
                cancellationToken
            ).ConfigureAwait(false))
            {
                return _res.Body;
            }
        }

        internal async Task OnQueueInfoFailed(HttpRequestMessage req, HttpResponseMessage res)
        {
            var content = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
            var ex = new RestApiException(
                new HttpRequestMessageWrapper(req, null),
                new HttpResponseMessageWrapper(res, content));
            HandleFailedQueueInfoRequest(ex);
            HandleFailedRequest(ex);
            Client.OnFailedRequest(ex);
            throw ex;
        }

        internal async Task<HttpOperationResponse<QueueInfo>> QueueInfoInternalAsync(
            string queueId,
            CancellationToken cancellationToken = default
        )
        {
            if (string.IsNullOrEmpty(queueId))
            {
                throw new ArgumentNullException(nameof(queueId));
            }


            var _path = "/api/2019-06-17/info/queues/{queueId}";
            _path = _path.Replace("{queueId}", Client.Serialize(queueId));

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
                if (!_res.IsSuccessStatusCode)
                {
                    await OnQueueInfoFailed(_req, _res);
                }
                string _responseContent = await _res.Content.ReadAsStringAsync().ConfigureAwait(false);
                return new HttpOperationResponse<QueueInfo>
                {
                    Request = _req,
                    Response = _res,
                    Body = Client.Deserialize<QueueInfo>(_responseContent),
                };
            }
            catch (Exception)
            {
                _req?.Dispose();
                _res?.Dispose();
                throw;
            }
        }

        partial void HandleFailedQueueInfoListRequest(RestApiException ex);

        public async Task<IImmutableList<QueueInfo>> QueueInfoListAsync(
            CancellationToken cancellationToken = default
        )
        {
            using (var _res = await QueueInfoListInternalAsync(
                cancellationToken
            ).ConfigureAwait(false))
            {
                return _res.Body;
            }
        }

        internal async Task OnQueueInfoListFailed(HttpRequestMessage req, HttpResponseMessage res)
        {
            var content = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
            var ex = new RestApiException(
                new HttpRequestMessageWrapper(req, null),
                new HttpResponseMessageWrapper(res, content));
            HandleFailedQueueInfoListRequest(ex);
            HandleFailedRequest(ex);
            Client.OnFailedRequest(ex);
            throw ex;
        }

        internal async Task<HttpOperationResponse<IImmutableList<QueueInfo>>> QueueInfoListInternalAsync(
            CancellationToken cancellationToken = default
        )
        {

            var _path = "/api/2019-06-17/info/queues";

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
                if (!_res.IsSuccessStatusCode)
                {
                    await OnQueueInfoListFailed(_req, _res);
                }
                string _responseContent = await _res.Content.ReadAsStringAsync().ConfigureAwait(false);
                return new HttpOperationResponse<IImmutableList<QueueInfo>>
                {
                    Request = _req,
                    Response = _res,
                    Body = Client.Deserialize<IImmutableList<QueueInfo>>(_responseContent),
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
