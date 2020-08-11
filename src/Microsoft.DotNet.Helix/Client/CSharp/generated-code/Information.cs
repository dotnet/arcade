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
            if (string.IsNullOrEmpty(queueId))
            {
                throw new ArgumentNullException(nameof(queueId));
            }


            var _baseUri = Client.Options.BaseUri;
            var _url = new RequestUriBuilder();
            _url.Reset(_baseUri);
            _url.AppendPath(
                "/api/2019-06-17/info/queues/{queueId}".Replace("{queueId}", Uri.EscapeDataString(Client.Serialize(queueId))),
                false);



            using (var _req = Client.Pipeline.CreateRequest())
            {
                _req.Uri = _url;
                _req.Method = RequestMethod.Get;

                using (var _res = await Client.SendAsync(_req, cancellationToken).ConfigureAwait(false))
                {
                    if (_res.Status < 200 || _res.Status >= 300)
                    {
                        await OnQueueInfoFailed(_req, _res).ConfigureAwait(false);
                    }

                    if (_res.ContentStream == null)
                    {
                        await OnQueueInfoFailed(_req, _res).ConfigureAwait(false);
                    }

                    using (var _reader = new StreamReader(_res.ContentStream))
                    {
                        var _content = await _reader.ReadToEndAsync().ConfigureAwait(false);
                        var _body = Client.Deserialize<QueueInfo>(_content);
                        return _body;
                    }
                }
            }
        }

        internal async Task OnQueueInfoFailed(Request req, Response res)
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
            HandleFailedQueueInfoRequest(ex);
            HandleFailedRequest(ex);
            Client.OnFailedRequest(ex);
            throw ex;
        }

        partial void HandleFailedQueueInfoListRequest(RestApiException ex);

        public async Task<IImmutableList<QueueInfo>> QueueInfoListAsync(
            CancellationToken cancellationToken = default
        )
        {

            var _baseUri = Client.Options.BaseUri;
            var _url = new RequestUriBuilder();
            _url.Reset(_baseUri);
            _url.AppendPath(
                "/api/2019-06-17/info/queues",
                false);



            using (var _req = Client.Pipeline.CreateRequest())
            {
                _req.Uri = _url;
                _req.Method = RequestMethod.Get;

                using (var _res = await Client.SendAsync(_req, cancellationToken).ConfigureAwait(false))
                {
                    if (_res.Status < 200 || _res.Status >= 300)
                    {
                        await OnQueueInfoListFailed(_req, _res).ConfigureAwait(false);
                    }

                    if (_res.ContentStream == null)
                    {
                        await OnQueueInfoListFailed(_req, _res).ConfigureAwait(false);
                    }

                    using (var _reader = new StreamReader(_res.ContentStream))
                    {
                        var _content = await _reader.ReadToEndAsync().ConfigureAwait(false);
                        var _body = Client.Deserialize<IImmutableList<QueueInfo>>(_content);
                        return _body;
                    }
                }
            }
        }

        internal async Task OnQueueInfoListFailed(Request req, Response res)
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
            HandleFailedQueueInfoListRequest(ex);
            HandleFailedRequest(ex);
            Client.OnFailedRequest(ex);
            throw ex;
        }
    }
}
