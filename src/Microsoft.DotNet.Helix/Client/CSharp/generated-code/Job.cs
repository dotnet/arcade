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
    public partial interface IJob
    {
        Task<JobCreationResult> NewAsync(
            JobCreationRequest body,
            string idempotencyKey,
            CancellationToken cancellationToken = default
        );

        Task<IImmutableList<JobSummary>> ListAsync(
            string build = default,
            int? count = default,
            string creator = default,
            string name = default,
            string source = default,
            string type = default,
            CancellationToken cancellationToken = default
        );

        Task<JobPassFail> PassFailAsync(
            string job,
            CancellationToken cancellationToken = default
        );

        Task<JobSummary> SummaryAsync(
            string job,
            CancellationToken cancellationToken = default
        );

        Task<JobDetails> DetailsAsync(
            string job,
            CancellationToken cancellationToken = default
        );

        Task CancelAsync(
            string job,
            string jobCancellationToken = default,
            CancellationToken cancellationToken = default
        );

        Task WaitAsync(
            string job,
            CancellationToken cancellationToken = default
        );

    }

    internal partial class Job : IServiceOperations<HelixApi>, IJob
    {
        public Job(HelixApi client)
        {
            Client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public HelixApi Client { get; }

        partial void HandleFailedRequest(RestApiException ex);

        partial void HandleFailedNewRequest(RestApiException ex);

        public async Task<JobCreationResult> NewAsync(
            JobCreationRequest body,
            string idempotencyKey,
            CancellationToken cancellationToken = default
        )
        {
            using (var _res = await NewInternalAsync(
                body,
                idempotencyKey,
                cancellationToken
            ).ConfigureAwait(false))
            {
                return _res.Body;
            }
        }

        internal async Task OnNewFailed(HttpRequestMessage req, HttpResponseMessage res)
        {
            var content = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
            var ex = new RestApiException<ApiError>(
                new HttpRequestMessageWrapper(req, content),
                new HttpResponseMessageWrapper(res, content),
                Client.Deserialize<ApiError>(content)
                );
            HandleFailedNewRequest(ex);
            HandleFailedRequest(ex);
            Client.OnFailedRequest(ex);
            throw ex;
        }

        internal async Task<HttpOperationResponse<JobCreationResult>> NewInternalAsync(
            JobCreationRequest body,
            string idempotencyKey,
            CancellationToken cancellationToken = default
        )
        {
            if (body == default)
            {
                throw new ArgumentNullException(nameof(body));
            }

            if (!body.IsValid)
            {
                throw new ArgumentException("The parameter is not valid", nameof(body));
            }

            if (string.IsNullOrEmpty(idempotencyKey))
            {
                throw new ArgumentNullException(nameof(idempotencyKey));
            }

            const string apiVersion = "2019-06-17";

            var _path = "/api/jobs";

            var _query = new QueryBuilder();
            _query.Add("api-version", Client.Serialize(apiVersion));

            var _uriBuilder = new UriBuilder(Client.BaseUri);
            _uriBuilder.Path = _uriBuilder.Path.TrimEnd('/') + _path;
            _uriBuilder.Query = _query.ToString();
            var _url = _uriBuilder.Uri;

            HttpRequestMessage _req = null;
            HttpResponseMessage _res = null;
            try
            {
                _req = new HttpRequestMessage(HttpMethod.Post, _url);

                if (!string.IsNullOrEmpty(idempotencyKey))
                {
                    _req.Headers.Add("Idempotency-Key", idempotencyKey);
                }

                string _requestContent = null;
                if (body != default)
                {
                    _requestContent = Client.Serialize(body);
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
                if (!_res.IsSuccessStatusCode)
                {
                    await OnNewFailed(_req, _res);
                }
                string _responseContent = await _res.Content.ReadAsStringAsync().ConfigureAwait(false);
                return new HttpOperationResponse<JobCreationResult>
                {
                    Request = _req,
                    Response = _res,
                    Body = Client.Deserialize<JobCreationResult>(_responseContent),
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

        public async Task<IImmutableList<JobSummary>> ListAsync(
            string build = default,
            int? count = default,
            string creator = default,
            string name = default,
            string source = default,
            string type = default,
            CancellationToken cancellationToken = default
        )
        {
            using (var _res = await ListInternalAsync(
                build,
                count,
                creator,
                name,
                source,
                type,
                cancellationToken
            ).ConfigureAwait(false))
            {
                return _res.Body;
            }
        }

        internal async Task OnListFailed(HttpRequestMessage req, HttpResponseMessage res)
        {
            var content = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
            var ex = new RestApiException<ApiError>(
                new HttpRequestMessageWrapper(req, null),
                new HttpResponseMessageWrapper(res, content),
                Client.Deserialize<ApiError>(content)
                );
            HandleFailedListRequest(ex);
            HandleFailedRequest(ex);
            Client.OnFailedRequest(ex);
            throw ex;
        }

        internal async Task<HttpOperationResponse<IImmutableList<JobSummary>>> ListInternalAsync(
            string build = default,
            int? count = default,
            string creator = default,
            string name = default,
            string source = default,
            string type = default,
            CancellationToken cancellationToken = default
        )
        {
            const string apiVersion = "2019-06-17";

            var _path = "/api/jobs";

            var _query = new QueryBuilder();
            if (!string.IsNullOrEmpty(creator))
            {
                _query.Add("Creator", Client.Serialize(creator));
            }
            if (!string.IsNullOrEmpty(source))
            {
                _query.Add("Source", Client.Serialize(source));
            }
            if (!string.IsNullOrEmpty(type))
            {
                _query.Add("Type", Client.Serialize(type));
            }
            if (!string.IsNullOrEmpty(build))
            {
                _query.Add("Build", Client.Serialize(build));
            }
            if (!string.IsNullOrEmpty(name))
            {
                _query.Add("Name", Client.Serialize(name));
            }
            if (count != default)
            {
                _query.Add("count", Client.Serialize(count));
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
                    await OnListFailed(_req, _res);
                }
                string _responseContent = await _res.Content.ReadAsStringAsync().ConfigureAwait(false);
                return new HttpOperationResponse<IImmutableList<JobSummary>>
                {
                    Request = _req,
                    Response = _res,
                    Body = Client.Deserialize<IImmutableList<JobSummary>>(_responseContent),
                };
            }
            catch (Exception)
            {
                _req?.Dispose();
                _res?.Dispose();
                throw;
            }
        }

        partial void HandleFailedPassFailRequest(RestApiException ex);

        public async Task<JobPassFail> PassFailAsync(
            string job,
            CancellationToken cancellationToken = default
        )
        {
            using (var _res = await PassFailInternalAsync(
                job,
                cancellationToken
            ).ConfigureAwait(false))
            {
                return _res.Body;
            }
        }

        internal async Task OnPassFailFailed(HttpRequestMessage req, HttpResponseMessage res)
        {
            var content = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
            var ex = new RestApiException<ApiError>(
                new HttpRequestMessageWrapper(req, null),
                new HttpResponseMessageWrapper(res, content),
                Client.Deserialize<ApiError>(content)
                );
            HandleFailedPassFailRequest(ex);
            HandleFailedRequest(ex);
            Client.OnFailedRequest(ex);
            throw ex;
        }

        internal async Task<HttpOperationResponse<JobPassFail>> PassFailInternalAsync(
            string job,
            CancellationToken cancellationToken = default
        )
        {
            if (string.IsNullOrEmpty(job))
            {
                throw new ArgumentNullException(nameof(job));
            }

            const string apiVersion = "2019-06-17";

            var _path = "/api/jobs/{job}/pf";
            _path = _path.Replace("{job}", Client.Serialize(job));

            var _query = new QueryBuilder();
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
                    await OnPassFailFailed(_req, _res);
                }
                string _responseContent = await _res.Content.ReadAsStringAsync().ConfigureAwait(false);
                return new HttpOperationResponse<JobPassFail>
                {
                    Request = _req,
                    Response = _res,
                    Body = Client.Deserialize<JobPassFail>(_responseContent),
                };
            }
            catch (Exception)
            {
                _req?.Dispose();
                _res?.Dispose();
                throw;
            }
        }

        partial void HandleFailedSummaryRequest(RestApiException ex);

        public async Task<JobSummary> SummaryAsync(
            string job,
            CancellationToken cancellationToken = default
        )
        {
            using (var _res = await SummaryInternalAsync(
                job,
                cancellationToken
            ).ConfigureAwait(false))
            {
                return _res.Body;
            }
        }

        internal async Task OnSummaryFailed(HttpRequestMessage req, HttpResponseMessage res)
        {
            var content = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
            var ex = new RestApiException<ApiError>(
                new HttpRequestMessageWrapper(req, null),
                new HttpResponseMessageWrapper(res, content),
                Client.Deserialize<ApiError>(content)
                );
            HandleFailedSummaryRequest(ex);
            HandleFailedRequest(ex);
            Client.OnFailedRequest(ex);
            throw ex;
        }

        internal async Task<HttpOperationResponse<JobSummary>> SummaryInternalAsync(
            string job,
            CancellationToken cancellationToken = default
        )
        {
            if (string.IsNullOrEmpty(job))
            {
                throw new ArgumentNullException(nameof(job));
            }

            const string apiVersion = "2019-06-17";

            var _path = "/api/jobs/{job}";
            _path = _path.Replace("{job}", Client.Serialize(job));

            var _query = new QueryBuilder();
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
                    await OnSummaryFailed(_req, _res);
                }
                string _responseContent = await _res.Content.ReadAsStringAsync().ConfigureAwait(false);
                return new HttpOperationResponse<JobSummary>
                {
                    Request = _req,
                    Response = _res,
                    Body = Client.Deserialize<JobSummary>(_responseContent),
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

        public async Task<JobDetails> DetailsAsync(
            string job,
            CancellationToken cancellationToken = default
        )
        {
            using (var _res = await DetailsInternalAsync(
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
            var ex = new RestApiException<ApiError>(
                new HttpRequestMessageWrapper(req, null),
                new HttpResponseMessageWrapper(res, content),
                Client.Deserialize<ApiError>(content)
                );
            HandleFailedDetailsRequest(ex);
            HandleFailedRequest(ex);
            Client.OnFailedRequest(ex);
            throw ex;
        }

        internal async Task<HttpOperationResponse<JobDetails>> DetailsInternalAsync(
            string job,
            CancellationToken cancellationToken = default
        )
        {
            if (string.IsNullOrEmpty(job))
            {
                throw new ArgumentNullException(nameof(job));
            }

            const string apiVersion = "2019-06-17";

            var _path = "/api/jobs/{job}/details";
            _path = _path.Replace("{job}", Client.Serialize(job));

            var _query = new QueryBuilder();
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
                    await OnDetailsFailed(_req, _res);
                }
                string _responseContent = await _res.Content.ReadAsStringAsync().ConfigureAwait(false);
                return new HttpOperationResponse<JobDetails>
                {
                    Request = _req,
                    Response = _res,
                    Body = Client.Deserialize<JobDetails>(_responseContent),
                };
            }
            catch (Exception)
            {
                _req?.Dispose();
                _res?.Dispose();
                throw;
            }
        }

        partial void HandleFailedCancelRequest(RestApiException ex);

        public async Task CancelAsync(
            string job,
            string jobCancellationToken = default,
            CancellationToken cancellationToken = default
        )
        {
            using (await CancelInternalAsync(
                job,
                jobCancellationToken,
                cancellationToken
            ).ConfigureAwait(false))
            {
                return;
            }
        }

        internal async Task OnCancelFailed(HttpRequestMessage req, HttpResponseMessage res)
        {
            var content = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
            var ex = new RestApiException<ApiError>(
                new HttpRequestMessageWrapper(req, null),
                new HttpResponseMessageWrapper(res, content),
                Client.Deserialize<ApiError>(content)
                );
            HandleFailedCancelRequest(ex);
            HandleFailedRequest(ex);
            Client.OnFailedRequest(ex);
            throw ex;
        }

        internal async Task<HttpOperationResponse> CancelInternalAsync(
            string job,
            string jobCancellationToken = default,
            CancellationToken cancellationToken = default
        )
        {
            if (string.IsNullOrEmpty(job))
            {
                throw new ArgumentNullException(nameof(job));
            }

            const string apiVersion = "2019-06-17";

            var _path = "/api/jobs/{job}/cancel";
            _path = _path.Replace("{job}", Client.Serialize(job));

            var _query = new QueryBuilder();
            if (!string.IsNullOrEmpty(jobCancellationToken))
            {
                _query.Add("jobCancellationToken", Client.Serialize(jobCancellationToken));
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
                _req = new HttpRequestMessage(HttpMethod.Post, _url);

                if (Client.Credentials != null)
                {
                    await Client.Credentials.ProcessHttpRequestAsync(_req, cancellationToken).ConfigureAwait(false);
                }

                _res = await Client.SendAsync(_req, cancellationToken).ConfigureAwait(false);
                if (!_res.IsSuccessStatusCode)
                {
                    await OnCancelFailed(_req, _res);
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

        partial void HandleFailedWaitRequest(RestApiException ex);

        public async Task WaitAsync(
            string job,
            CancellationToken cancellationToken = default
        )
        {
            using (await WaitInternalAsync(
                job,
                cancellationToken
            ).ConfigureAwait(false))
            {
                return;
            }
        }

        internal async Task OnWaitFailed(HttpRequestMessage req, HttpResponseMessage res)
        {
            var content = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
            var ex = new RestApiException<ApiError>(
                new HttpRequestMessageWrapper(req, null),
                new HttpResponseMessageWrapper(res, content),
                Client.Deserialize<ApiError>(content)
                );
            HandleFailedWaitRequest(ex);
            HandleFailedRequest(ex);
            Client.OnFailedRequest(ex);
            throw ex;
        }

        internal async Task<HttpOperationResponse> WaitInternalAsync(
            string job,
            CancellationToken cancellationToken = default
        )
        {
            if (string.IsNullOrEmpty(job))
            {
                throw new ArgumentNullException(nameof(job));
            }

            const string apiVersion = "2019-06-17";

            var _path = "/api/jobs/{job}/wait";
            _path = _path.Replace("{job}", Client.Serialize(job));

            var _query = new QueryBuilder();
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
                    await OnWaitFailed(_req, _res);
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
