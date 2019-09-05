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
            using (var _res = await GetRepositoriesInternalAsync(
                vcb,
                cancellationToken
            ).ConfigureAwait(false))
            {
                return _res.Body;
            }
        }

        internal async Task OnGetRepositoriesFailed(HttpRequestMessage req, HttpResponseMessage res)
        {
            var content = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
            var ex = new RestApiException(
                new HttpRequestMessageWrapper(req, null),
                new HttpResponseMessageWrapper(res, content));
            HandleFailedGetRepositoriesRequest(ex);
            HandleFailedRequest(ex);
            Client.OnFailedRequest(ex);
            throw ex;
        }

        internal async Task<HttpOperationResponse<ViewConfiguration>> GetRepositoriesInternalAsync(
            string vcb = default,
            CancellationToken cancellationToken = default
        )
        {

            var _path = "/api/2019-06-17/repo";

            var _query = new QueryBuilder();
            if (!string.IsNullOrEmpty(vcb))
            {
                _query.Add("_vcb", Client.Serialize(vcb));
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
                    await OnGetRepositoriesFailed(_req, _res);
                }
                string _responseContent = await _res.Content.ReadAsStringAsync().ConfigureAwait(false);
                return new HttpOperationResponse<ViewConfiguration>
                {
                    Request = _req,
                    Response = _res,
                    Body = Client.Deserialize<ViewConfiguration>(_responseContent),
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
