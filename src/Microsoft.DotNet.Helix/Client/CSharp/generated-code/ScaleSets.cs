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
    public partial interface IScaleSets
    {
        Task<IImmutableList<DetailedVMScalingHistory>> GetDetailedVMScalingHistoryAsync(
            DateTimeOffset date,
            string scaleSet = default,
            CancellationToken cancellationToken = default
        );

        Task<IImmutableList<AggregatedVMScalingHistory>> GetAggregatedVMScalingHistoryAsync(
            DateTimeOffset date,
            CancellationToken cancellationToken = default
        );

    }

    internal partial class ScaleSets : IServiceOperations<HelixApi>, IScaleSets
    {
        public ScaleSets(HelixApi client)
        {
            Client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public HelixApi Client { get; }

        partial void HandleFailedRequest(RestApiException ex);

        partial void HandleFailedGetDetailedVMScalingHistoryRequest(RestApiException ex);

        public async Task<IImmutableList<DetailedVMScalingHistory>> GetDetailedVMScalingHistoryAsync(
            DateTimeOffset date,
            string scaleSet = default,
            CancellationToken cancellationToken = default
        )
        {
            using (var _res = await GetDetailedVMScalingHistoryInternalAsync(
                date,
                scaleSet,
                cancellationToken
            ).ConfigureAwait(false))
            {
                return _res.Body;
            }
        }

        internal async Task OnGetDetailedVMScalingHistoryFailed(HttpRequestMessage req, HttpResponseMessage res)
        {
            var content = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
            var ex = new RestApiException(
                new HttpRequestMessageWrapper(req, null),
                new HttpResponseMessageWrapper(res, content));
            HandleFailedGetDetailedVMScalingHistoryRequest(ex);
            HandleFailedRequest(ex);
            Client.OnFailedRequest(ex);
            throw ex;
        }

        internal async Task<HttpOperationResponse<IImmutableList<DetailedVMScalingHistory>>> GetDetailedVMScalingHistoryInternalAsync(
            DateTimeOffset date,
            string scaleSet = default,
            CancellationToken cancellationToken = default
        )
        {
            if (date == default)
            {
                throw new ArgumentNullException(nameof(date));
            }


            var _path = "/api/2019-06-17/scalesets/detailedHistory";

            var _query = new QueryBuilder();
            if (date != default)
            {
                _query.Add("date", Client.Serialize(date));
            }
            if (!string.IsNullOrEmpty(scaleSet))
            {
                _query.Add("scaleSet", Client.Serialize(scaleSet));
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
                if (!_res.IsSuccessStatusCode)
                {
                    await OnGetDetailedVMScalingHistoryFailed(_req, _res);
                }
                string _responseContent = await _res.Content.ReadAsStringAsync().ConfigureAwait(false);
                return new HttpOperationResponse<IImmutableList<DetailedVMScalingHistory>>
                {
                    Request = _req,
                    Response = _res,
                    Body = Client.Deserialize<IImmutableList<DetailedVMScalingHistory>>(_responseContent),
                };
            }
            catch (Exception)
            {
                _req?.Dispose();
                _res?.Dispose();
                throw;
            }
        }

        partial void HandleFailedGetAggregatedVMScalingHistoryRequest(RestApiException ex);

        public async Task<IImmutableList<AggregatedVMScalingHistory>> GetAggregatedVMScalingHistoryAsync(
            DateTimeOffset date,
            CancellationToken cancellationToken = default
        )
        {
            using (var _res = await GetAggregatedVMScalingHistoryInternalAsync(
                date,
                cancellationToken
            ).ConfigureAwait(false))
            {
                return _res.Body;
            }
        }

        internal async Task OnGetAggregatedVMScalingHistoryFailed(HttpRequestMessage req, HttpResponseMessage res)
        {
            var content = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
            var ex = new RestApiException(
                new HttpRequestMessageWrapper(req, null),
                new HttpResponseMessageWrapper(res, content));
            HandleFailedGetAggregatedVMScalingHistoryRequest(ex);
            HandleFailedRequest(ex);
            Client.OnFailedRequest(ex);
            throw ex;
        }

        internal async Task<HttpOperationResponse<IImmutableList<AggregatedVMScalingHistory>>> GetAggregatedVMScalingHistoryInternalAsync(
            DateTimeOffset date,
            CancellationToken cancellationToken = default
        )
        {
            if (date == default)
            {
                throw new ArgumentNullException(nameof(date));
            }


            var _path = "/api/2019-06-17/scalesets/aggregatedHistory";

            var _query = new QueryBuilder();
            if (date != default)
            {
                _query.Add("date", Client.Serialize(date));
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
                if (!_res.IsSuccessStatusCode)
                {
                    await OnGetAggregatedVMScalingHistoryFailed(_req, _res);
                }
                string _responseContent = await _res.Content.ReadAsStringAsync().ConfigureAwait(false);
                return new HttpOperationResponse<IImmutableList<AggregatedVMScalingHistory>>
                {
                    Request = _req,
                    Response = _res,
                    Body = Client.Deserialize<IImmutableList<AggregatedVMScalingHistory>>(_responseContent),
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
