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
        Task<IImmutableList<JobSummary>> ListAsync(
            int count = default,
            string filterBuild = default,
            string filterCreator = default,
            string filterName = default,
            string filterSource = default,
            string filterType = default,
            CancellationToken cancellationToken = default
        );

        Task<JobCreationResult> NewAsync(
            JobCreationRequest newJob,
            CancellationToken cancellationToken = default
        );

        Task<JenkinsResponse> JenkinsAsync(
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

        Task<WaitResponse> WaitAsync(
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

        partial void HandleFailedListRequest(RestApiException ex);

        public async Task<IImmutableList<JobSummary>> ListAsync(
            int count = default,
            string filterBuild = default,
            string filterCreator = default,
            string filterName = default,
            string filterSource = default,
            string filterType = default,
            CancellationToken cancellationToken = default
        )
        {
            using (var _res = await ListInternalAsync(
                count,
                filterBuild,
                filterCreator,
                filterName,
                filterSource,
                filterType,
                cancellationToken
            ).ConfigureAwait(false))
            {
                return _res.Body;
            }
        }

        internal async Task<HttpOperationResponse<IImmutableList<JobSummary>>> ListInternalAsync(
            int count = default,
            string filterBuild = default,
            string filterCreator = default,
            string filterName = default,
            string filterSource = default,
            string filterType = default,
            CancellationToken cancellationToken = default
        )
        {

            var _path = "/api/2018-03-14/jobs";

            var _query = new QueryBuilder();
            if (count != default)
            {
                _query.Add("count", Client.Serialize(count));
            }
            if (!string.IsNullOrEmpty(filterCreator))
            {
                _query.Add("filter.creator", Client.Serialize(filterCreator));
            }
            if (!string.IsNullOrEmpty(filterSource))
            {
                _query.Add("filter.source", Client.Serialize(filterSource));
            }
            if (!string.IsNullOrEmpty(filterType))
            {
                _query.Add("filter.type", Client.Serialize(filterType));
            }
            if (!string.IsNullOrEmpty(filterBuild))
            {
                _query.Add("filter.build", Client.Serialize(filterBuild));
            }
            if (!string.IsNullOrEmpty(filterName))
            {
                _query.Add("filter.name", Client.Serialize(filterName));
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

        partial void HandleFailedNewRequest(RestApiException ex);

        public async Task<JobCreationResult> NewAsync(
            JobCreationRequest newJob,
            CancellationToken cancellationToken = default
        )
        {
            using (var _res = await NewInternalAsync(
                newJob,
                cancellationToken
            ).ConfigureAwait(false))
            {
                return _res.Body;
            }
        }

        internal async Task<HttpOperationResponse<JobCreationResult>> NewInternalAsync(
            JobCreationRequest newJob,
            CancellationToken cancellationToken = default
        )
        {
            if (newJob == default)
            {
                throw new ArgumentNullException(nameof(newJob));
            }

            if (!newJob.IsValid)
            {
                throw new ArgumentException("The parameter is not valid", nameof(newJob));
            }


            var _path = "/api/2018-03-14/jobs";

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
                if (newJob != default)
                {
                    _requestContent = Client.Serialize(newJob);
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

        partial void HandleFailedJenkinsRequest(RestApiException ex);

        public async Task<JenkinsResponse> JenkinsAsync(
            CancellationToken cancellationToken = default
        )
        {
            using (var _res = await JenkinsInternalAsync(
                cancellationToken
            ).ConfigureAwait(false))
            {
                return _res.Body;
            }
        }

        internal async Task<HttpOperationResponse<JenkinsResponse>> JenkinsInternalAsync(
            CancellationToken cancellationToken = default
        )
        {

            var _path = "/api/2018-03-14/jobs/jenkins";

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
                    HandleFailedJenkinsRequest(ex);
                    HandleFailedRequest(ex);
                    Client.OnFailedRequest(ex);
                    throw ex;
                }
                return new HttpOperationResponse<JenkinsResponse>
                {
                    Request = _req,
                    Response = _res,
                    Body = Client.Deserialize<JenkinsResponse>(_responseContent),
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

        internal async Task<HttpOperationResponse<JobSummary>> SummaryInternalAsync(
            string job,
            CancellationToken cancellationToken = default
        )
        {
            if (string.IsNullOrEmpty(job))
            {
                throw new ArgumentNullException(nameof(job));
            }


            var _path = "/api/2018-03-14/jobs/{job}";
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
                var _responseContent = await _res.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!_res.IsSuccessStatusCode)
                {
                    var ex = new RestApiException
                    {
                        Request = new HttpRequestMessageWrapper(_req, null),
                        Response = new HttpResponseMessageWrapper(_res, _responseContent),
                    };
                    HandleFailedSummaryRequest(ex);
                    HandleFailedRequest(ex);
                    Client.OnFailedRequest(ex);
                    throw ex;
                }
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

        internal async Task<HttpOperationResponse<JobDetails>> DetailsInternalAsync(
            string job,
            CancellationToken cancellationToken = default
        )
        {
            if (string.IsNullOrEmpty(job))
            {
                throw new ArgumentNullException(nameof(job));
            }


            var _path = "/api/2018-03-14/jobs/{job}/details";
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
                var _responseContent = await _res.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!_res.IsSuccessStatusCode)
                {
                    var ex = new RestApiException
                    {
                        Request = new HttpRequestMessageWrapper(_req, null),
                        Response = new HttpResponseMessageWrapper(_res, _responseContent),
                    };
                    HandleFailedDetailsRequest(ex);
                    HandleFailedRequest(ex);
                    Client.OnFailedRequest(ex);
                    throw ex;
                }
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

        partial void HandleFailedWaitRequest(RestApiException ex);

        public async Task<WaitResponse> WaitAsync(
            string job,
            CancellationToken cancellationToken = default
        )
        {
            using (var _res = await WaitInternalAsync(
                job,
                cancellationToken
            ).ConfigureAwait(false))
            {
                return _res.Body;
            }
        }

        internal async Task<HttpOperationResponse<WaitResponse>> WaitInternalAsync(
            string job,
            CancellationToken cancellationToken = default
        )
        {
            if (string.IsNullOrEmpty(job))
            {
                throw new ArgumentNullException(nameof(job));
            }


            var _path = "/api/2018-03-14/jobs/{job}/wait";
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
                var _responseContent = await _res.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!_res.IsSuccessStatusCode)
                {
                    var ex = new RestApiException
                    {
                        Request = new HttpRequestMessageWrapper(_req, null),
                        Response = new HttpResponseMessageWrapper(_res, _responseContent),
                    };
                    HandleFailedWaitRequest(ex);
                    HandleFailedRequest(ex);
                    Client.OnFailedRequest(ex);
                    throw ex;
                }
                return new HttpOperationResponse<WaitResponse>
                {
                    Request = _req,
                    Response = _res,
                    Body = Client.Deserialize<WaitResponse>(_responseContent),
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
