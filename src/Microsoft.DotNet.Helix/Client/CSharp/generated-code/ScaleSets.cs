using System;
using System.Collections.Immutable;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Core;
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
            if (date == default(DateTimeOffset))
            {
                throw new ArgumentNullException(nameof(date));
            }


            var _baseUri = Client.Options.BaseUri;
            var _url = new RequestUriBuilder();
            _url.Reset(_baseUri);
            _url.AppendPath(
                "/api/2019-06-17/scalesets/detailedHistory",
                false);

            if (date != default(DateTimeOffset))
            {
                _url.AppendQuery("date", Client.Serialize(date));
            }
            if (!string.IsNullOrEmpty(scaleSet))
            {
                _url.AppendQuery("scaleSet", Client.Serialize(scaleSet));
            }


            using (var _req = Client.Pipeline.CreateRequest())
            {
                _req.Uri = _url;
                _req.Method = RequestMethod.Get;

                using (var _res = await Client.SendAsync(_req, cancellationToken).ConfigureAwait(false))
                {
                    if (_res.Status < 200 || _res.Status >= 300)
                    {
                        await OnGetDetailedVMScalingHistoryFailed(_req, _res).ConfigureAwait(false);
                    }

                    if (_res.ContentStream == null)
                    {
                        await OnGetDetailedVMScalingHistoryFailed(_req, _res).ConfigureAwait(false);
                    }

                    using (var _reader = new StreamReader(_res.ContentStream))
                    {
                        var _content = await _reader.ReadToEndAsync().ConfigureAwait(false);
                        var _body = Client.Deserialize<IImmutableList<DetailedVMScalingHistory>>(_content);
                        return _body;
                    }
                }
            }
        }

        internal async Task OnGetDetailedVMScalingHistoryFailed(Request req, Response res)
        {
            string content = null;
            if (res.ContentStream != null)
            {
                using (var reader = new StreamReader(res.ContentStream))
                {
                    content = await reader.ReadToEndAsync().ConfigureAwait(false);
                }
            }

            var ex = new RestApiException(
                req,
                res,
                content);
            HandleFailedGetDetailedVMScalingHistoryRequest(ex);
            HandleFailedRequest(ex);
            Client.OnFailedRequest(ex);
            throw ex;
        }

        partial void HandleFailedGetAggregatedVMScalingHistoryRequest(RestApiException ex);

        public async Task<IImmutableList<AggregatedVMScalingHistory>> GetAggregatedVMScalingHistoryAsync(
            DateTimeOffset date,
            CancellationToken cancellationToken = default
        )
        {
            if (date == default(DateTimeOffset))
            {
                throw new ArgumentNullException(nameof(date));
            }


            var _baseUri = Client.Options.BaseUri;
            var _url = new RequestUriBuilder();
            _url.Reset(_baseUri);
            _url.AppendPath(
                "/api/2019-06-17/scalesets/aggregatedHistory",
                false);

            if (date != default(DateTimeOffset))
            {
                _url.AppendQuery("date", Client.Serialize(date));
            }


            using (var _req = Client.Pipeline.CreateRequest())
            {
                _req.Uri = _url;
                _req.Method = RequestMethod.Get;

                using (var _res = await Client.SendAsync(_req, cancellationToken).ConfigureAwait(false))
                {
                    if (_res.Status < 200 || _res.Status >= 300)
                    {
                        await OnGetAggregatedVMScalingHistoryFailed(_req, _res).ConfigureAwait(false);
                    }

                    if (_res.ContentStream == null)
                    {
                        await OnGetAggregatedVMScalingHistoryFailed(_req, _res).ConfigureAwait(false);
                    }

                    using (var _reader = new StreamReader(_res.ContentStream))
                    {
                        var _content = await _reader.ReadToEndAsync().ConfigureAwait(false);
                        var _body = Client.Deserialize<IImmutableList<AggregatedVMScalingHistory>>(_content);
                        return _body;
                    }
                }
            }
        }

        internal async Task OnGetAggregatedVMScalingHistoryFailed(Request req, Response res)
        {
            string content = null;
            if (res.ContentStream != null)
            {
                using (var reader = new StreamReader(res.ContentStream))
                {
                    content = await reader.ReadToEndAsync().ConfigureAwait(false);
                }
            }

            var ex = new RestApiException(
                req,
                res,
                content);
            HandleFailedGetAggregatedVMScalingHistoryRequest(ex);
            HandleFailedRequest(ex);
            Client.OnFailedRequest(ex);
            throw ex;
        }
    }
}
