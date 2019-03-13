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
    public partial interface ITelemetry
    {
        Task<string> StartJobAsync(
            JobInfo body,
            CancellationToken cancellationToken = default
        );

        Task<string> StartBuildWorkItemAsync(
            string buildUri,
            string xHelixJobToken,
            CancellationToken cancellationToken = default
        );

        Task FinishBuildWorkItemAsync(
            int errorCount,
            string id,
            int warningCount,
            string xHelixJobToken,
            string logUri = default,
            CancellationToken cancellationToken = default
        );

        Task<string> StartXUnitWorkItemAsync(
            string friendlyName,
            string xHelixJobToken,
            CancellationToken cancellationToken = default
        );

        Task FinishXUnitWorkItemAsync(
            int exitCode,
            string id,
            string resultsXmlUri,
            string xHelixJobToken,
            string logUri = default,
            CancellationToken cancellationToken = default
        );

        Task WarningAsync(
            string eid,
            string id,
            string xHelixJobToken,
            string logUri = default,
            string message = default,
            CancellationToken cancellationToken = default
        );

        Task ErrorAsync(
            string eid,
            string id,
            string xHelixJobToken,
            string logUri = default,
            string message = default,
            CancellationToken cancellationToken = default
        );

        Task LogAsync(
            string id,
            string logUri,
            string xHelixJobToken,
            string format = default,
            string module = default,
            CancellationToken cancellationToken = default
        );

    }

    internal partial class Telemetry : IServiceOperations<HelixApi>, ITelemetry
    {
        public Telemetry(HelixApi client)
        {
            Client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public HelixApi Client { get; }

        partial void HandleFailedRequest(RestApiException ex);

        partial void HandleFailedStartJobRequest(RestApiException ex);

        public async Task<string> StartJobAsync(
            JobInfo body,
            CancellationToken cancellationToken = default
        )
        {
            using (var _res = await StartJobInternalAsync(
                body,
                cancellationToken
            ).ConfigureAwait(false))
            {
                return _res.Body;
            }
        }

        internal async Task<HttpOperationResponse<string>> StartJobInternalAsync(
            JobInfo body,
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


            var _path = "/api/2018-03-14/telemetry/job";

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
                string _responseContent;
                if (!_res.IsSuccessStatusCode)
                {
                    _responseContent = await _res.Content.ReadAsStringAsync().ConfigureAwait(false);
                    var ex = new RestApiException(
                        new HttpRequestMessageWrapper(_req, _requestContent),
                        new HttpResponseMessageWrapper(_res, _responseContent));
                    HandleFailedStartJobRequest(ex);
                    HandleFailedRequest(ex);
                    Client.OnFailedRequest(ex);
                    throw ex;
                }
                _responseContent = await _res.Content.ReadAsStringAsync().ConfigureAwait(false);
                return new HttpOperationResponse<string>
                {
                    Request = _req,
                    Response = _res,
                    Body = Client.Deserialize<string>(_responseContent),
                };
            }
            catch (Exception)
            {
                _req?.Dispose();
                _res?.Dispose();
                throw;
            }
        }

        partial void HandleFailedStartBuildWorkItemRequest(RestApiException ex);

        public async Task<string> StartBuildWorkItemAsync(
            string buildUri,
            string xHelixJobToken,
            CancellationToken cancellationToken = default
        )
        {
            using (var _res = await StartBuildWorkItemInternalAsync(
                buildUri,
                xHelixJobToken,
                cancellationToken
            ).ConfigureAwait(false))
            {
                return _res.Body;
            }
        }

        internal async Task<HttpOperationResponse<string>> StartBuildWorkItemInternalAsync(
            string buildUri,
            string xHelixJobToken,
            CancellationToken cancellationToken = default
        )
        {
            if (string.IsNullOrEmpty(buildUri))
            {
                throw new ArgumentNullException(nameof(buildUri));
            }

            if (string.IsNullOrEmpty(xHelixJobToken))
            {
                throw new ArgumentNullException(nameof(xHelixJobToken));
            }


            var _path = "/api/2018-03-14/telemetry/job/build";

            var _query = new QueryBuilder();
            if (!string.IsNullOrEmpty(buildUri))
            {
                _query.Add("buildUri", Client.Serialize(buildUri));
            }

            var _uriBuilder = new UriBuilder(Client.BaseUri);
            _uriBuilder.Path = _uriBuilder.Path.TrimEnd('/') + _path;
            _uriBuilder.Query = _query.ToString();
            var _url = _uriBuilder.Uri;

            HttpRequestMessage _req = null;
            HttpResponseMessage _res = null;
            try
            {
                _req = new HttpRequestMessage(HttpMethod.Post, _url);

                if (!string.IsNullOrEmpty(xHelixJobToken))
                {
                    _req.Headers.Add("X-Helix-Job-Token", xHelixJobToken);
                }

                if (Client.Credentials != null)
                {
                    await Client.Credentials.ProcessHttpRequestAsync(_req, cancellationToken).ConfigureAwait(false);
                }

                _res = await Client.SendAsync(_req, cancellationToken).ConfigureAwait(false);
                string _responseContent;
                if (!_res.IsSuccessStatusCode)
                {
                    _responseContent = await _res.Content.ReadAsStringAsync().ConfigureAwait(false);
                    var ex = new RestApiException(
                        new HttpRequestMessageWrapper(_req, null),
                        new HttpResponseMessageWrapper(_res, _responseContent));
                    HandleFailedStartBuildWorkItemRequest(ex);
                    HandleFailedRequest(ex);
                    Client.OnFailedRequest(ex);
                    throw ex;
                }
                _responseContent = await _res.Content.ReadAsStringAsync().ConfigureAwait(false);
                return new HttpOperationResponse<string>
                {
                    Request = _req,
                    Response = _res,
                    Body = Client.Deserialize<string>(_responseContent),
                };
            }
            catch (Exception)
            {
                _req?.Dispose();
                _res?.Dispose();
                throw;
            }
        }

        partial void HandleFailedFinishBuildWorkItemRequest(RestApiException ex);

        public async Task FinishBuildWorkItemAsync(
            int errorCount,
            string id,
            int warningCount,
            string xHelixJobToken,
            string logUri = default,
            CancellationToken cancellationToken = default
        )
        {
            using (await FinishBuildWorkItemInternalAsync(
                errorCount,
                id,
                warningCount,
                xHelixJobToken,
                logUri,
                cancellationToken
            ).ConfigureAwait(false))
            {
                return;
            }
        }

        internal async Task<HttpOperationResponse> FinishBuildWorkItemInternalAsync(
            int errorCount,
            string id,
            int warningCount,
            string xHelixJobToken,
            string logUri = default,
            CancellationToken cancellationToken = default
        )
        {
            if (errorCount == default)
            {
                throw new ArgumentNullException(nameof(errorCount));
            }

            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            if (warningCount == default)
            {
                throw new ArgumentNullException(nameof(warningCount));
            }

            if (string.IsNullOrEmpty(xHelixJobToken))
            {
                throw new ArgumentNullException(nameof(xHelixJobToken));
            }


            var _path = "/api/2018-03-14/telemetry/job/build/{id}/finish";
            _path = _path.Replace("{id}", Client.Serialize(id));

            var _query = new QueryBuilder();
            if (errorCount != default)
            {
                _query.Add("errorCount", Client.Serialize(errorCount));
            }
            if (warningCount != default)
            {
                _query.Add("warningCount", Client.Serialize(warningCount));
            }
            if (!string.IsNullOrEmpty(logUri))
            {
                _query.Add("logUri", Client.Serialize(logUri));
            }

            var _uriBuilder = new UriBuilder(Client.BaseUri);
            _uriBuilder.Path = _uriBuilder.Path.TrimEnd('/') + _path;
            _uriBuilder.Query = _query.ToString();
            var _url = _uriBuilder.Uri;

            HttpRequestMessage _req = null;
            HttpResponseMessage _res = null;
            try
            {
                _req = new HttpRequestMessage(HttpMethod.Post, _url);

                if (!string.IsNullOrEmpty(xHelixJobToken))
                {
                    _req.Headers.Add("X-Helix-Job-Token", xHelixJobToken);
                }

                if (Client.Credentials != null)
                {
                    await Client.Credentials.ProcessHttpRequestAsync(_req, cancellationToken).ConfigureAwait(false);
                }

                _res = await Client.SendAsync(_req, cancellationToken).ConfigureAwait(false);
                string _responseContent;
                if (!_res.IsSuccessStatusCode)
                {
                    _responseContent = await _res.Content.ReadAsStringAsync().ConfigureAwait(false);
                    var ex = new RestApiException(
                        new HttpRequestMessageWrapper(_req, null),
                        new HttpResponseMessageWrapper(_res, _responseContent));
                    HandleFailedFinishBuildWorkItemRequest(ex);
                    HandleFailedRequest(ex);
                    Client.OnFailedRequest(ex);
                    throw ex;
                }
                _responseContent = await _res.Content.ReadAsStringAsync().ConfigureAwait(false);
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

        partial void HandleFailedStartXUnitWorkItemRequest(RestApiException ex);

        public async Task<string> StartXUnitWorkItemAsync(
            string friendlyName,
            string xHelixJobToken,
            CancellationToken cancellationToken = default
        )
        {
            using (var _res = await StartXUnitWorkItemInternalAsync(
                friendlyName,
                xHelixJobToken,
                cancellationToken
            ).ConfigureAwait(false))
            {
                return _res.Body;
            }
        }

        internal async Task<HttpOperationResponse<string>> StartXUnitWorkItemInternalAsync(
            string friendlyName,
            string xHelixJobToken,
            CancellationToken cancellationToken = default
        )
        {
            if (string.IsNullOrEmpty(friendlyName))
            {
                throw new ArgumentNullException(nameof(friendlyName));
            }

            if (string.IsNullOrEmpty(xHelixJobToken))
            {
                throw new ArgumentNullException(nameof(xHelixJobToken));
            }


            var _path = "/api/2018-03-14/telemetry/job/xunit";

            var _query = new QueryBuilder();
            if (!string.IsNullOrEmpty(friendlyName))
            {
                _query.Add("friendlyName", Client.Serialize(friendlyName));
            }

            var _uriBuilder = new UriBuilder(Client.BaseUri);
            _uriBuilder.Path = _uriBuilder.Path.TrimEnd('/') + _path;
            _uriBuilder.Query = _query.ToString();
            var _url = _uriBuilder.Uri;

            HttpRequestMessage _req = null;
            HttpResponseMessage _res = null;
            try
            {
                _req = new HttpRequestMessage(HttpMethod.Post, _url);

                if (!string.IsNullOrEmpty(xHelixJobToken))
                {
                    _req.Headers.Add("X-Helix-Job-Token", xHelixJobToken);
                }

                if (Client.Credentials != null)
                {
                    await Client.Credentials.ProcessHttpRequestAsync(_req, cancellationToken).ConfigureAwait(false);
                }

                _res = await Client.SendAsync(_req, cancellationToken).ConfigureAwait(false);
                string _responseContent;
                if (!_res.IsSuccessStatusCode)
                {
                    _responseContent = await _res.Content.ReadAsStringAsync().ConfigureAwait(false);
                    var ex = new RestApiException(
                        new HttpRequestMessageWrapper(_req, null),
                        new HttpResponseMessageWrapper(_res, _responseContent));
                    HandleFailedStartXUnitWorkItemRequest(ex);
                    HandleFailedRequest(ex);
                    Client.OnFailedRequest(ex);
                    throw ex;
                }
                _responseContent = await _res.Content.ReadAsStringAsync().ConfigureAwait(false);
                return new HttpOperationResponse<string>
                {
                    Request = _req,
                    Response = _res,
                    Body = Client.Deserialize<string>(_responseContent),
                };
            }
            catch (Exception)
            {
                _req?.Dispose();
                _res?.Dispose();
                throw;
            }
        }

        partial void HandleFailedFinishXUnitWorkItemRequest(RestApiException ex);

        public async Task FinishXUnitWorkItemAsync(
            int exitCode,
            string id,
            string resultsXmlUri,
            string xHelixJobToken,
            string logUri = default,
            CancellationToken cancellationToken = default
        )
        {
            using (await FinishXUnitWorkItemInternalAsync(
                exitCode,
                id,
                resultsXmlUri,
                xHelixJobToken,
                logUri,
                cancellationToken
            ).ConfigureAwait(false))
            {
                return;
            }
        }

        internal async Task<HttpOperationResponse> FinishXUnitWorkItemInternalAsync(
            int exitCode,
            string id,
            string resultsXmlUri,
            string xHelixJobToken,
            string logUri = default,
            CancellationToken cancellationToken = default
        )
        {
            if (exitCode == default)
            {
                throw new ArgumentNullException(nameof(exitCode));
            }

            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            if (string.IsNullOrEmpty(resultsXmlUri))
            {
                throw new ArgumentNullException(nameof(resultsXmlUri));
            }

            if (string.IsNullOrEmpty(xHelixJobToken))
            {
                throw new ArgumentNullException(nameof(xHelixJobToken));
            }


            var _path = "/api/2018-03-14/telemetry/job/xunit/{id}/finish";
            _path = _path.Replace("{id}", Client.Serialize(id));

            var _query = new QueryBuilder();
            if (exitCode != default)
            {
                _query.Add("exitCode", Client.Serialize(exitCode));
            }
            if (!string.IsNullOrEmpty(resultsXmlUri))
            {
                _query.Add("resultsXmlUri", Client.Serialize(resultsXmlUri));
            }
            if (!string.IsNullOrEmpty(logUri))
            {
                _query.Add("logUri", Client.Serialize(logUri));
            }

            var _uriBuilder = new UriBuilder(Client.BaseUri);
            _uriBuilder.Path = _uriBuilder.Path.TrimEnd('/') + _path;
            _uriBuilder.Query = _query.ToString();
            var _url = _uriBuilder.Uri;

            HttpRequestMessage _req = null;
            HttpResponseMessage _res = null;
            try
            {
                _req = new HttpRequestMessage(HttpMethod.Post, _url);

                if (!string.IsNullOrEmpty(xHelixJobToken))
                {
                    _req.Headers.Add("X-Helix-Job-Token", xHelixJobToken);
                }

                if (Client.Credentials != null)
                {
                    await Client.Credentials.ProcessHttpRequestAsync(_req, cancellationToken).ConfigureAwait(false);
                }

                _res = await Client.SendAsync(_req, cancellationToken).ConfigureAwait(false);
                string _responseContent;
                if (!_res.IsSuccessStatusCode)
                {
                    _responseContent = await _res.Content.ReadAsStringAsync().ConfigureAwait(false);
                    var ex = new RestApiException(
                        new HttpRequestMessageWrapper(_req, null),
                        new HttpResponseMessageWrapper(_res, _responseContent));
                    HandleFailedFinishXUnitWorkItemRequest(ex);
                    HandleFailedRequest(ex);
                    Client.OnFailedRequest(ex);
                    throw ex;
                }
                _responseContent = await _res.Content.ReadAsStringAsync().ConfigureAwait(false);
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

        partial void HandleFailedWarningRequest(RestApiException ex);

        public async Task WarningAsync(
            string eid,
            string id,
            string xHelixJobToken,
            string logUri = default,
            string message = default,
            CancellationToken cancellationToken = default
        )
        {
            using (await WarningInternalAsync(
                eid,
                id,
                xHelixJobToken,
                logUri,
                message,
                cancellationToken
            ).ConfigureAwait(false))
            {
                return;
            }
        }

        internal async Task<HttpOperationResponse> WarningInternalAsync(
            string eid,
            string id,
            string xHelixJobToken,
            string logUri = default,
            string message = default,
            CancellationToken cancellationToken = default
        )
        {
            if (string.IsNullOrEmpty(eid))
            {
                throw new ArgumentNullException(nameof(eid));
            }

            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            if (string.IsNullOrEmpty(xHelixJobToken))
            {
                throw new ArgumentNullException(nameof(xHelixJobToken));
            }


            var _path = "/api/2018-03-14/telemetry/job/workitem/{id}/warning";
            _path = _path.Replace("{id}", Client.Serialize(id));

            var _query = new QueryBuilder();
            if (!string.IsNullOrEmpty(eid))
            {
                _query.Add("eid", Client.Serialize(eid));
            }
            if (!string.IsNullOrEmpty(message))
            {
                _query.Add("message", Client.Serialize(message));
            }
            if (!string.IsNullOrEmpty(logUri))
            {
                _query.Add("logUri", Client.Serialize(logUri));
            }

            var _uriBuilder = new UriBuilder(Client.BaseUri);
            _uriBuilder.Path = _uriBuilder.Path.TrimEnd('/') + _path;
            _uriBuilder.Query = _query.ToString();
            var _url = _uriBuilder.Uri;

            HttpRequestMessage _req = null;
            HttpResponseMessage _res = null;
            try
            {
                _req = new HttpRequestMessage(HttpMethod.Post, _url);

                if (!string.IsNullOrEmpty(xHelixJobToken))
                {
                    _req.Headers.Add("X-Helix-Job-Token", xHelixJobToken);
                }

                if (Client.Credentials != null)
                {
                    await Client.Credentials.ProcessHttpRequestAsync(_req, cancellationToken).ConfigureAwait(false);
                }

                _res = await Client.SendAsync(_req, cancellationToken).ConfigureAwait(false);
                string _responseContent;
                if (!_res.IsSuccessStatusCode)
                {
                    _responseContent = await _res.Content.ReadAsStringAsync().ConfigureAwait(false);
                    var ex = new RestApiException(
                        new HttpRequestMessageWrapper(_req, null),
                        new HttpResponseMessageWrapper(_res, _responseContent));
                    HandleFailedWarningRequest(ex);
                    HandleFailedRequest(ex);
                    Client.OnFailedRequest(ex);
                    throw ex;
                }
                _responseContent = await _res.Content.ReadAsStringAsync().ConfigureAwait(false);
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

        partial void HandleFailedErrorRequest(RestApiException ex);

        public async Task ErrorAsync(
            string eid,
            string id,
            string xHelixJobToken,
            string logUri = default,
            string message = default,
            CancellationToken cancellationToken = default
        )
        {
            using (await ErrorInternalAsync(
                eid,
                id,
                xHelixJobToken,
                logUri,
                message,
                cancellationToken
            ).ConfigureAwait(false))
            {
                return;
            }
        }

        internal async Task<HttpOperationResponse> ErrorInternalAsync(
            string eid,
            string id,
            string xHelixJobToken,
            string logUri = default,
            string message = default,
            CancellationToken cancellationToken = default
        )
        {
            if (string.IsNullOrEmpty(eid))
            {
                throw new ArgumentNullException(nameof(eid));
            }

            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            if (string.IsNullOrEmpty(xHelixJobToken))
            {
                throw new ArgumentNullException(nameof(xHelixJobToken));
            }


            var _path = "/api/2018-03-14/telemetry/job/workitem/{id}/error";
            _path = _path.Replace("{id}", Client.Serialize(id));

            var _query = new QueryBuilder();
            if (!string.IsNullOrEmpty(eid))
            {
                _query.Add("eid", Client.Serialize(eid));
            }
            if (!string.IsNullOrEmpty(message))
            {
                _query.Add("message", Client.Serialize(message));
            }
            if (!string.IsNullOrEmpty(logUri))
            {
                _query.Add("logUri", Client.Serialize(logUri));
            }

            var _uriBuilder = new UriBuilder(Client.BaseUri);
            _uriBuilder.Path = _uriBuilder.Path.TrimEnd('/') + _path;
            _uriBuilder.Query = _query.ToString();
            var _url = _uriBuilder.Uri;

            HttpRequestMessage _req = null;
            HttpResponseMessage _res = null;
            try
            {
                _req = new HttpRequestMessage(HttpMethod.Post, _url);

                if (!string.IsNullOrEmpty(xHelixJobToken))
                {
                    _req.Headers.Add("X-Helix-Job-Token", xHelixJobToken);
                }

                if (Client.Credentials != null)
                {
                    await Client.Credentials.ProcessHttpRequestAsync(_req, cancellationToken).ConfigureAwait(false);
                }

                _res = await Client.SendAsync(_req, cancellationToken).ConfigureAwait(false);
                string _responseContent;
                if (!_res.IsSuccessStatusCode)
                {
                    _responseContent = await _res.Content.ReadAsStringAsync().ConfigureAwait(false);
                    var ex = new RestApiException(
                        new HttpRequestMessageWrapper(_req, null),
                        new HttpResponseMessageWrapper(_res, _responseContent));
                    HandleFailedErrorRequest(ex);
                    HandleFailedRequest(ex);
                    Client.OnFailedRequest(ex);
                    throw ex;
                }
                _responseContent = await _res.Content.ReadAsStringAsync().ConfigureAwait(false);
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

        partial void HandleFailedLogRequest(RestApiException ex);

        public async Task LogAsync(
            string id,
            string logUri,
            string xHelixJobToken,
            string format = default,
            string module = default,
            CancellationToken cancellationToken = default
        )
        {
            using (await LogInternalAsync(
                id,
                logUri,
                xHelixJobToken,
                format,
                module,
                cancellationToken
            ).ConfigureAwait(false))
            {
                return;
            }
        }

        internal async Task<HttpOperationResponse> LogInternalAsync(
            string id,
            string logUri,
            string xHelixJobToken,
            string format = default,
            string module = default,
            CancellationToken cancellationToken = default
        )
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(nameof(id));
            }

            if (string.IsNullOrEmpty(logUri))
            {
                throw new ArgumentNullException(nameof(logUri));
            }

            if (string.IsNullOrEmpty(xHelixJobToken))
            {
                throw new ArgumentNullException(nameof(xHelixJobToken));
            }


            var _path = "/api/2018-03-14/telemetry/job/workitem/{id}/log";
            _path = _path.Replace("{id}", Client.Serialize(id));

            var _query = new QueryBuilder();
            if (!string.IsNullOrEmpty(logUri))
            {
                _query.Add("logUri", Client.Serialize(logUri));
            }
            if (!string.IsNullOrEmpty(format))
            {
                _query.Add("format", Client.Serialize(format));
            }
            if (!string.IsNullOrEmpty(module))
            {
                _query.Add("module", Client.Serialize(module));
            }

            var _uriBuilder = new UriBuilder(Client.BaseUri);
            _uriBuilder.Path = _uriBuilder.Path.TrimEnd('/') + _path;
            _uriBuilder.Query = _query.ToString();
            var _url = _uriBuilder.Uri;

            HttpRequestMessage _req = null;
            HttpResponseMessage _res = null;
            try
            {
                _req = new HttpRequestMessage(HttpMethod.Post, _url);

                if (!string.IsNullOrEmpty(xHelixJobToken))
                {
                    _req.Headers.Add("X-Helix-Job-Token", xHelixJobToken);
                }

                if (Client.Credentials != null)
                {
                    await Client.Credentials.ProcessHttpRequestAsync(_req, cancellationToken).ConfigureAwait(false);
                }

                _res = await Client.SendAsync(_req, cancellationToken).ConfigureAwait(false);
                string _responseContent;
                if (!_res.IsSuccessStatusCode)
                {
                    _responseContent = await _res.Content.ReadAsStringAsync().ConfigureAwait(false);
                    var ex = new RestApiException(
                        new HttpRequestMessageWrapper(_req, null),
                        new HttpResponseMessageWrapper(_res, _responseContent));
                    HandleFailedLogRequest(ex);
                    HandleFailedRequest(ex);
                    Client.OnFailedRequest(ex);
                    throw ex;
                }
                _responseContent = await _res.Content.ReadAsStringAsync().ConfigureAwait(false);
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
