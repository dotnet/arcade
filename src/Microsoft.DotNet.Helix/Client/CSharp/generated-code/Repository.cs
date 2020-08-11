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
    public partial interface IRepository
    {
        Task<ViewConfiguration> GetRepositoriesAsync(
            string vcb = default,
            CancellationToken cancellationToken = default
        );

    }

    internal partial class Repository : IServiceOperations<HelixApi>, IRepository
    {
        public Repository(HelixApi client)
        {
            Client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public HelixApi Client { get; }

        partial void HandleFailedRequest(RestApiException ex);

        partial void HandleFailedGetRepositoriesRequest(RestApiException ex);

        public async Task<ViewConfiguration> GetRepositoriesAsync(
            string vcb = default,
            CancellationToken cancellationToken = default
        )
        {

            var _baseUri = Client.Options.BaseUri;
            var _url = new RequestUriBuilder();
            _url.Reset(_baseUri);
            _url.AppendPath(
                "/api/2019-06-17/repo",
                false);

            if (!string.IsNullOrEmpty(vcb))
            {
                _url.AppendQuery("_vcb", Client.Serialize(vcb));
            }


            using (var _req = Client.Pipeline.CreateRequest())
            {
                _req.Uri = _url;
                _req.Method = RequestMethod.Get;

                using (var _res = await Client.SendAsync(_req, cancellationToken).ConfigureAwait(false))
                {
                    if (_res.Status < 200 || _res.Status >= 300)
                    {
                        await OnGetRepositoriesFailed(_req, _res).ConfigureAwait(false);
                    }

                    if (_res.ContentStream == null)
                    {
                        await OnGetRepositoriesFailed(_req, _res).ConfigureAwait(false);
                    }

                    using (var _reader = new StreamReader(_res.ContentStream))
                    {
                        var _content = await _reader.ReadToEndAsync().ConfigureAwait(false);
                        var _body = Client.Deserialize<ViewConfiguration>(_content);
                        return _body;
                    }
                }
            }
        }

        internal async Task OnGetRepositoriesFailed(Request req, Response res)
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
            HandleFailedGetRepositoriesRequest(ex);
            HandleFailedRequest(ex);
            Client.OnFailedRequest(ex);
            throw ex;
        }
    }
}
