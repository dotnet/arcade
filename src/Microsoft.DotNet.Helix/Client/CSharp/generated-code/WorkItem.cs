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
    public partial interface IWorkItem
    {
        Task<System.IO.Stream> GetFileAsync(
            string file,
            string id,
            string job,
            CancellationToken cancellationToken = default
        );

        Task<IImmutableList<UploadedFile>> ListFilesAsync(
            string id,
            string job,
            CancellationToken cancellationToken = default
        );

        Task<System.IO.Stream> ConsoleLogAsync(
            string id,
            string job,
            CancellationToken cancellationToken = default
        );

        Task<IImmutableList<WorkItemSummary>> ListAsync(
            string job,
            CancellationToken cancellationToken = default
        );

        Task<WorkItemDetails> DetailsAsync(
            string id,
            string job,
            CancellationToken cancellationToken = default
        );

    }

    internal partial class WorkItem : IServiceOperations<HelixApi>, IWorkItem
    {
        public WorkItem(HelixApi client)
        {
            Client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public HelixApi Client { get; }

        partial void HandleFailedRequest(RestApiException ex);

        partial void HandleFailedGetFileRequest(RestApiException ex);

        public async Task<System.IO.Stream> GetFileAsync(
            string file,
            string id,
            string job,
            CancellationToken cancellationToken = default
        )
        {
            var _res = await GetFileInternalAsync(
                file,
                id,
                job,
                cancellationToken
            ).ConfigureAwait(false);
            return new ResponseStream(_res.Body, _res);
        }

        internal async Task OnGetFileFailed(HttpRequestMessage req, HttpResponseMessage res)
        {
            var content = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
            var ex = new RestApiException(
                new HttpRequestMessageWrapper(req, null),
                new HttpResponseMessageWrapper(res, content));
            HandleFailedGetFileRequest(ex);
            HandleFailedRequest(ex);
            Client.OnFailedRequest(ex);
            throw ex;
        }

        internal async Task<HttpOperationResponse<System.IO.Stream>> GetFileInternalAsync(
            string file,
            string id,
            string job,
            CancellationToken cancellationToken = default
        )
        {
            if (string.IsNullOrEmpty(file))
            {
                throw new ArgumentNullException(nameof(file));
            }

            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            if (string.IsNullOrEmpty(job))
            {
                throw new ArgumentNullException(nameof(job));
            }


            var _path = "/api/2019-06-17/jobs/{job}/workitems/{id}/files/{file}";
            _path = _path.Replace("{job}", Client.Serialize(job));
            _path = _path.Replace("{id}", Client.Serialize(id));
            _path = _path.Replace("{file}", Client.Serialize(file));

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
                    await OnGetFileFailed(_req, _res);
                }
                System.IO.Stream _responseStream = await _res.Content.ReadAsStreamAsync().ConfigureAwait(false);
                return new HttpOperationResponse<System.IO.Stream>
                {
                    Request = _req,
                    Response = _res,
                    Body = _responseStream
                };
            }
            catch (Exception)
            {
                _req?.Dispose();
                _res?.Dispose();
                throw;
            }
        }

        partial void HandleFailedListFilesRequest(RestApiException ex);

        public async Task<IImmutableList<UploadedFile>> ListFilesAsync(
            string id,
            string job,
            CancellationToken cancellationToken = default
        )
        {
            using (var _res = await ListFilesInternalAsync(
                id,
                job,
                cancellationToken
            ).ConfigureAwait(false))
            {
                return _res.Body;
            }
        }

        internal async Task OnListFilesFailed(HttpRequestMessage req, HttpResponseMessage res)
        {
            var content = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
            var ex = new RestApiException(
                new HttpRequestMessageWrapper(req, null),
                new HttpResponseMessageWrapper(res, content));
            HandleFailedListFilesRequest(ex);
            HandleFailedRequest(ex);
            Client.OnFailedRequest(ex);
            throw ex;
        }

        internal async Task<HttpOperationResponse<IImmutableList<UploadedFile>>> ListFilesInternalAsync(
            string id,
            string job,
            CancellationToken cancellationToken = default
        )
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            if (string.IsNullOrEmpty(job))
            {
                throw new ArgumentNullException(nameof(job));
            }


            var _path = "/api/2019-06-17/jobs/{job}/workitems/{id}/files";
            _path = _path.Replace("{job}", Client.Serialize(job));
            _path = _path.Replace("{id}", Client.Serialize(id));

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
                    await OnListFilesFailed(_req, _res);
                }
                string _responseContent = await _res.Content.ReadAsStringAsync().ConfigureAwait(false);
                return new HttpOperationResponse<IImmutableList<UploadedFile>>
                {
                    Request = _req,
                    Response = _res,
                    Body = Client.Deserialize<IImmutableList<UploadedFile>>(_responseContent),
                };
            }
            catch (Exception)
            {
                _req?.Dispose();
                _res?.Dispose();
                throw;
            }
        }

        partial void HandleFailedConsoleLogRequest(RestApiException ex);

        public async Task<System.IO.Stream> ConsoleLogAsync(
            string id,
            string job,
            CancellationToken cancellationToken = default
        )
        {
            var _res = await ConsoleLogInternalAsync(
                id,
                job,
                cancellationToken
            ).ConfigureAwait(false);
            return new ResponseStream(_res.Body, _res);
        }

        internal async Task OnConsoleLogFailed(HttpRequestMessage req, HttpResponseMessage res)
        {
            var content = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
            var ex = new RestApiException(
                new HttpRequestMessageWrapper(req, null),
                new HttpResponseMessageWrapper(res, content));
            HandleFailedConsoleLogRequest(ex);
            HandleFailedRequest(ex);
            Client.OnFailedRequest(ex);
            throw ex;
        }

        internal async Task<HttpOperationResponse<System.IO.Stream>> ConsoleLogInternalAsync(
            string id,
            string job,
            CancellationToken cancellationToken = default
        )
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            if (string.IsNullOrEmpty(job))
            {
                throw new ArgumentNullException(nameof(job));
            }


            var _path = "/api/2019-06-17/jobs/{job}/workitems/{id}/console";
            _path = _path.Replace("{job}", Client.Serialize(job));
            _path = _path.Replace("{id}", Client.Serialize(id));

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
                    await OnConsoleLogFailed(_req, _res);
                }
                System.IO.Stream _responseStream = await _res.Content.ReadAsStreamAsync().ConfigureAwait(false);
                return new HttpOperationResponse<System.IO.Stream>
                {
                    Request = _req,
                    Response = _res,
                    Body = _responseStream
                };
            }
            catch (Exception)
            {
                _req?.Dispose();
                _res?.Dispose();
                throw;
            }
        }

        partial void HandleFailedListRequest(RestApiException ex);

        public async Task<IImmutableList<WorkItemSummary>> ListAsync(
            string job,
            CancellationToken cancellationToken = default
        )
        {
            using (var _res = await ListInternalAsync(
                job,
                cancellationToken
            ).ConfigureAwait(false))
            {
                return _res.Body;
            }
        }

        internal async Task OnListFailed(HttpRequestMessage req, HttpResponseMessage res)
        {
            var content = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
            var ex = new RestApiException(
                new HttpRequestMessageWrapper(req, null),
                new HttpResponseMessageWrapper(res, content));
            HandleFailedListRequest(ex);
            HandleFailedRequest(ex);
            Client.OnFailedRequest(ex);
            throw ex;
        }

        internal async Task<HttpOperationResponse<IImmutableList<WorkItemSummary>>> ListInternalAsync(
            string job,
            CancellationToken cancellationToken = default
        )
        {
            if (string.IsNullOrEmpty(job))
            {
                throw new ArgumentNullException(nameof(job));
            }


            var _path = "/api/2019-06-17/jobs/{job}/workitems";
            _path = _path.Replace("{job}", Client.Serialize(job));

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
                    await OnListFailed(_req, _res);
                }
                string _responseContent = await _res.Content.ReadAsStringAsync().ConfigureAwait(false);
                return new HttpOperationResponse<IImmutableList<WorkItemSummary>>
                {
                    Request = _req,
                    Response = _res,
                    Body = Client.Deserialize<IImmutableList<WorkItemSummary>>(_responseContent),
                };
            }
            catch (Exception)
            {
                _req?.Dispose();
                _res?.Dispose();
                throw;
            }
        }

        partial void HandleFailedDetailsRequest(RestApiException ex);

        public async Task<WorkItemDetails> DetailsAsync(
            string id,
            string job,
            CancellationToken cancellationToken = default
        )
        {
            using (var _res = await DetailsInternalAsync(
                id,
                job,
                cancellationToken
            ).ConfigureAwait(false))
            {
                return _res.Body;
            }
        }

        internal async Task OnDetailsFailed(HttpRequestMessage req, HttpResponseMessage res)
        {
            var content = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
            var ex = new RestApiException(
                new HttpRequestMessageWrapper(req, null),
                new HttpResponseMessageWrapper(res, content));
            HandleFailedDetailsRequest(ex);
            HandleFailedRequest(ex);
            Client.OnFailedRequest(ex);
            throw ex;
        }

        internal async Task<HttpOperationResponse<WorkItemDetails>> DetailsInternalAsync(
            string id,
            string job,
            CancellationToken cancellationToken = default
        )
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            if (string.IsNullOrEmpty(job))
            {
                throw new ArgumentNullException(nameof(job));
            }


            var _path = "/api/2019-06-17/jobs/{job}/workitems/{id}";
            _path = _path.Replace("{job}", Client.Serialize(job));
            _path = _path.Replace("{id}", Client.Serialize(id));

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
                    await OnDetailsFailed(_req, _res);
                }
                string _responseContent = await _res.Content.ReadAsStringAsync().ConfigureAwait(false);
                return new HttpOperationResponse<WorkItemDetails>
                {
                    Request = _req,
                    Response = _res,
                    Body = Client.Deserialize<WorkItemDetails>(_responseContent),
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
