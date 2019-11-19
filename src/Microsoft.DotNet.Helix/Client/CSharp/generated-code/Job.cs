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
    public partial interface IJob
    {
        Task<IImmutableList<JobSummary>> ListAsync(
            int? count = default,
            string filterBuild = default,
            string filterCreator = default,
            string filterName = default,
            string filterSource = default,
            string filterType = default,
            CancellationToken cancellationToken = default
        );

        Task<JobCreationResult> NewAsync(
            JobCreationRequest body,
            string idempotencyKey,
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

        Task<Newtonsoft.Json.Linq.JToken> CancelAsync(
            string job,
            CancellationToken cancellationToken = default
        );

        Task<Newtonsoft.Json.Linq.JToken> WaitAsync(
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
            int? count = default,
            string filterBuild = default,
            string filterCreator = default,
            string filterName = default,
            string filterSource = default,
            string filterType = default,
            CancellationToken cancellationToken = default
        )
        {

            var _baseUri = Client.Options.BaseUri;
            var _url = new RequestUriBuilder();
            _url.Reset(_baseUri);
            _url.AppendPath(
                "/api/2019-06-17/jobs",
                false);

            if (count != default(int?))
            {
                _url.AppendQuery("count", Client.Serialize(count));
            }
            if (!string.IsNullOrEmpty(filterCreator))
            {
                _url.AppendQuery("filter.creator", Client.Serialize(filterCreator));
            }
            if (!string.IsNullOrEmpty(filterSource))
            {
                _url.AppendQuery("filter.source", Client.Serialize(filterSource));
            }
            if (!string.IsNullOrEmpty(filterType))
            {
                _url.AppendQuery("filter.type", Client.Serialize(filterType));
            }
            if (!string.IsNullOrEmpty(filterBuild))
            {
                _url.AppendQuery("filter.build", Client.Serialize(filterBuild));
            }
            if (!string.IsNullOrEmpty(filterName))
            {
                _url.AppendQuery("filter.name", Client.Serialize(filterName));
            }


            using (var _req = Client.Pipeline.CreateRequest())
            {
                _req.Uri = _url;
                _req.Method = RequestMethod.Get;

                using (var _res = await Client.SendAsync(_req, cancellationToken).ConfigureAwait(false))
                {
                    if (_res.Status < 200 || _res.Status >= 300)
                    {
                        await OnListFailed(_req, _res).ConfigureAwait(false);
                    }

                    if (_res.ContentStream == null)
                    {
                        await OnListFailed(_req, _res).ConfigureAwait(false);
                    }

                    using (var _reader = new StreamReader(_res.ContentStream))
                    {
                        var _content = await _reader.ReadToEndAsync().ConfigureAwait(false);
                        var _body = Client.Deserialize<IImmutableList<JobSummary>>(_content);
                        return _body;
                    }
                }
            }
        }

        internal async Task OnListFailed(Request req, Response res)
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
            HandleFailedListRequest(ex);
            HandleFailedRequest(ex);
            Client.OnFailedRequest(ex);
            throw ex;
        }

        partial void HandleFailedNewRequest(RestApiException ex);

        public async Task<JobCreationResult> NewAsync(
            JobCreationRequest body,
            string idempotencyKey,
            CancellationToken cancellationToken = default
        )
        {
            if (body == default(JobCreationRequest))
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


            var _baseUri = Client.Options.BaseUri;
            var _url = new RequestUriBuilder();
            _url.Reset(_baseUri);
            _url.AppendPath(
                "/api/2019-06-17/jobs",
                false);



            using (var _req = Client.Pipeline.CreateRequest())
            {
                _req.Uri = _url;
                _req.Method = RequestMethod.Post;

                if (!string.IsNullOrEmpty(idempotencyKey))
                {
                    _req.Headers.Add("Idempotency-Key", idempotencyKey);
                }

                if (body != default(JobCreationRequest))
                {
                    _req.Content = RequestContent.Create(Encoding.UTF8.GetBytes(Client.Serialize(body)));
                    _req.Headers.Add("Content-Type", "application/json; charset=utf-8");
                }

                using (var _res = await Client.SendAsync(_req, cancellationToken).ConfigureAwait(false))
                {
                    if (_res.Status < 200 || _res.Status >= 300)
                    {
                        await OnNewFailed(_req, _res).ConfigureAwait(false);
                    }

                    if (_res.ContentStream == null)
                    {
                        await OnNewFailed(_req, _res).ConfigureAwait(false);
                    }

                    using (var _reader = new StreamReader(_res.ContentStream))
                    {
                        var _content = await _reader.ReadToEndAsync().ConfigureAwait(false);
                        var _body = Client.Deserialize<JobCreationResult>(_content);
                        return _body;
                    }
                }
            }
        }

        internal async Task OnNewFailed(Request req, Response res)
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
            HandleFailedNewRequest(ex);
            HandleFailedRequest(ex);
            Client.OnFailedRequest(ex);
            throw ex;
        }

        partial void HandleFailedPassFailRequest(RestApiException ex);

        public async Task<JobPassFail> PassFailAsync(
            string job,
            CancellationToken cancellationToken = default
        )
        {
            if (string.IsNullOrEmpty(job))
            {
                throw new ArgumentNullException(nameof(job));
            }


            var _baseUri = Client.Options.BaseUri;
            var _url = new RequestUriBuilder();
            _url.Reset(_baseUri);
            _url.AppendPath(
                "/api/2019-06-17/jobs/{job}/pf".Replace("{job}", Uri.EscapeDataString(Client.Serialize(job))),
                false);



            using (var _req = Client.Pipeline.CreateRequest())
            {
                _req.Uri = _url;
                _req.Method = RequestMethod.Get;

                using (var _res = await Client.SendAsync(_req, cancellationToken).ConfigureAwait(false))
                {
                    if (_res.Status < 200 || _res.Status >= 300)
                    {
                        await OnPassFailFailed(_req, _res).ConfigureAwait(false);
                    }

                    if (_res.ContentStream == null)
                    {
                        await OnPassFailFailed(_req, _res).ConfigureAwait(false);
                    }

                    using (var _reader = new StreamReader(_res.ContentStream))
                    {
                        var _content = await _reader.ReadToEndAsync().ConfigureAwait(false);
                        var _body = Client.Deserialize<JobPassFail>(_content);
                        return _body;
                    }
                }
            }
        }

        internal async Task OnPassFailFailed(Request req, Response res)
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
            HandleFailedPassFailRequest(ex);
            HandleFailedRequest(ex);
            Client.OnFailedRequest(ex);
            throw ex;
        }

        partial void HandleFailedSummaryRequest(RestApiException ex);

        public async Task<JobSummary> SummaryAsync(
            string job,
            CancellationToken cancellationToken = default
        )
        {
            if (string.IsNullOrEmpty(job))
            {
                throw new ArgumentNullException(nameof(job));
            }


            var _baseUri = Client.Options.BaseUri;
            var _url = new RequestUriBuilder();
            _url.Reset(_baseUri);
            _url.AppendPath(
                "/api/2019-06-17/jobs/{job}".Replace("{job}", Uri.EscapeDataString(Client.Serialize(job))),
                false);



            using (var _req = Client.Pipeline.CreateRequest())
            {
                _req.Uri = _url;
                _req.Method = RequestMethod.Get;

                using (var _res = await Client.SendAsync(_req, cancellationToken).ConfigureAwait(false))
                {
                    if (_res.Status < 200 || _res.Status >= 300)
                    {
                        await OnSummaryFailed(_req, _res).ConfigureAwait(false);
                    }

                    if (_res.ContentStream == null)
                    {
                        await OnSummaryFailed(_req, _res).ConfigureAwait(false);
                    }

                    using (var _reader = new StreamReader(_res.ContentStream))
                    {
                        var _content = await _reader.ReadToEndAsync().ConfigureAwait(false);
                        var _body = Client.Deserialize<JobSummary>(_content);
                        return _body;
                    }
                }
            }
        }

        internal async Task OnSummaryFailed(Request req, Response res)
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
            HandleFailedSummaryRequest(ex);
            HandleFailedRequest(ex);
            Client.OnFailedRequest(ex);
            throw ex;
        }

        partial void HandleFailedDetailsRequest(RestApiException ex);

        public async Task<JobDetails> DetailsAsync(
            string job,
            CancellationToken cancellationToken = default
        )
        {
            if (string.IsNullOrEmpty(job))
            {
                throw new ArgumentNullException(nameof(job));
            }


            var _baseUri = Client.Options.BaseUri;
            var _url = new RequestUriBuilder();
            _url.Reset(_baseUri);
            _url.AppendPath(
                "/api/2019-06-17/jobs/{job}/details".Replace("{job}", Uri.EscapeDataString(Client.Serialize(job))),
                false);



            using (var _req = Client.Pipeline.CreateRequest())
            {
                _req.Uri = _url;
                _req.Method = RequestMethod.Get;

                using (var _res = await Client.SendAsync(_req, cancellationToken).ConfigureAwait(false))
                {
                    if (_res.Status < 200 || _res.Status >= 300)
                    {
                        await OnDetailsFailed(_req, _res).ConfigureAwait(false);
                    }

                    if (_res.ContentStream == null)
                    {
                        await OnDetailsFailed(_req, _res).ConfigureAwait(false);
                    }

                    using (var _reader = new StreamReader(_res.ContentStream))
                    {
                        var _content = await _reader.ReadToEndAsync().ConfigureAwait(false);
                        var _body = Client.Deserialize<JobDetails>(_content);
                        return _body;
                    }
                }
            }
        }

        internal async Task OnDetailsFailed(Request req, Response res)
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
            HandleFailedDetailsRequest(ex);
            HandleFailedRequest(ex);
            Client.OnFailedRequest(ex);
            throw ex;
        }

        partial void HandleFailedCancelRequest(RestApiException ex);

        public async Task<Newtonsoft.Json.Linq.JToken> CancelAsync(
            string job,
            CancellationToken cancellationToken = default
        )
        {
            if (string.IsNullOrEmpty(job))
            {
                throw new ArgumentNullException(nameof(job));
            }


            var _baseUri = Client.Options.BaseUri;
            var _url = new RequestUriBuilder();
            _url.Reset(_baseUri);
            _url.AppendPath(
                "/api/2019-06-17/jobs/{job}/cancel".Replace("{job}", Uri.EscapeDataString(Client.Serialize(job))),
                false);



            using (var _req = Client.Pipeline.CreateRequest())
            {
                _req.Uri = _url;
                _req.Method = RequestMethod.Post;

                using (var _res = await Client.SendAsync(_req, cancellationToken).ConfigureAwait(false))
                {
                    if (_res.Status < 200 || _res.Status >= 300)
                    {
                        await OnCancelFailed(_req, _res).ConfigureAwait(false);
                    }

                    if (_res.ContentStream == null)
                    {
                        await OnCancelFailed(_req, _res).ConfigureAwait(false);
                    }

                    using (var _reader = new StreamReader(_res.ContentStream))
                    {
                        var _content = await _reader.ReadToEndAsync().ConfigureAwait(false);
                        var _body = Client.Deserialize<Newtonsoft.Json.Linq.JToken>(_content);
                        return _body;
                    }
                }
            }
        }

        internal async Task OnCancelFailed(Request req, Response res)
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
            HandleFailedCancelRequest(ex);
            HandleFailedRequest(ex);
            Client.OnFailedRequest(ex);
            throw ex;
        }

        partial void HandleFailedWaitRequest(RestApiException ex);

        public async Task<Newtonsoft.Json.Linq.JToken> WaitAsync(
            string job,
            CancellationToken cancellationToken = default
        )
        {
            if (string.IsNullOrEmpty(job))
            {
                throw new ArgumentNullException(nameof(job));
            }


            var _baseUri = Client.Options.BaseUri;
            var _url = new RequestUriBuilder();
            _url.Reset(_baseUri);
            _url.AppendPath(
                "/api/2019-06-17/jobs/{job}/wait".Replace("{job}", Uri.EscapeDataString(Client.Serialize(job))),
                false);



            using (var _req = Client.Pipeline.CreateRequest())
            {
                _req.Uri = _url;
                _req.Method = RequestMethod.Get;

                using (var _res = await Client.SendAsync(_req, cancellationToken).ConfigureAwait(false))
                {
                    if (_res.Status < 200 || _res.Status >= 300)
                    {
                        await OnWaitFailed(_req, _res).ConfigureAwait(false);
                    }

                    if (_res.ContentStream == null)
                    {
                        await OnWaitFailed(_req, _res).ConfigureAwait(false);
                    }

                    using (var _reader = new StreamReader(_res.ContentStream))
                    {
                        var _content = await _reader.ReadToEndAsync().ConfigureAwait(false);
                        var _body = Client.Deserialize<Newtonsoft.Json.Linq.JToken>(_content);
                        return _body;
                    }
                }
            }
        }

        internal async Task OnWaitFailed(Request req, Response res)
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
            HandleFailedWaitRequest(ex);
            HandleFailedRequest(ex);
            Client.OnFailedRequest(ex);
            throw ex;
        }
    }
}
