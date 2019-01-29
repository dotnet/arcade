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
    public partial interface IAnalysis
    {
        Task SetReasonAsync(
            string analysisName,
            string analysisType,
            string job,
            FailureReason reason,
            string workitem,
            CancellationToken cancellationToken = default
        );

        Task<GetDetailsResponse> GetDetailsAsync(
            string analysisName,
            string analysisType,
            string job,
            string workitem,
            CancellationToken cancellationToken = default
        );

    }

    internal partial class Analysis : IServiceOperations<HelixApi>, IAnalysis
    {
        public Analysis(HelixApi client)
        {
            Client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public HelixApi Client { get; }

        partial void HandleFailedRequest(RestApiException ex);

        partial void HandleFailedSetReasonRequest(RestApiException ex);

        public async Task SetReasonAsync(
            string analysisName,
            string analysisType,
            string job,
            FailureReason reason,
            string workitem,
            CancellationToken cancellationToken = default
        )
        {
            using (var _res = await SetReasonInternalAsync(
                analysisName,
                analysisType,
                job,
                reason,
                workitem,
                cancellationToken
            ).ConfigureAwait(false))
            {
                return;
            }
        }

        internal async Task<HttpOperationResponse> SetReasonInternalAsync(
            string analysisName,
            string analysisType,
            string job,
            FailureReason reason,
            string workitem,
            CancellationToken cancellationToken = default
        )
        {
            if (string.IsNullOrEmpty(analysisName))
            {
                throw new ArgumentNullException(nameof(analysisName));
            }

            if (string.IsNullOrEmpty(analysisType))
            {
                throw new ArgumentNullException(nameof(analysisType));
            }

            if (string.IsNullOrEmpty(job))
            {
                throw new ArgumentNullException(nameof(job));
            }

            if (reason == default)
            {
                throw new ArgumentNullException(nameof(reason));
            }

            if (string.IsNullOrEmpty(workitem))
            {
                throw new ArgumentNullException(nameof(workitem));
            }


            var _path = "/api/2018-03-14/analysis/{job}/{analysisType}/reason";
            _path = _path.Replace("{job}", Client.Serialize(job));
            _path = _path.Replace("{analysisType}", Client.Serialize(analysisType));

            var _query = new QueryBuilder();
            if (!string.IsNullOrEmpty(workitem))
            {
                _query.Add("workitem", Client.Serialize(workitem));
            }
            if (!string.IsNullOrEmpty(analysisName))
            {
                _query.Add("analysisName", Client.Serialize(analysisName));
            }

            var _uriBuilder = new UriBuilder(Client.BaseUri);
            _uriBuilder.Path = _uriBuilder.Path.TrimEnd('/') + _path;
            _uriBuilder.Query = _query.ToString();
            var _url = _uriBuilder.Uri;

            HttpRequestMessage _req = null;
            HttpResponseMessage _res = null;
            try
            {
                _req = new HttpRequestMessage(HttpMethod.Put, _url);

                string _requestContent = null;
                if (reason != default)
                {
                    _requestContent = Client.Serialize(reason);
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
                    HandleFailedSetReasonRequest(ex);
                    HandleFailedRequest(ex);
                    Client.OnFailedRequest(ex);
                    throw ex;
                }
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

        partial void HandleFailedGetDetailsRequest(RestApiException ex);

        public async Task<GetDetailsResponse> GetDetailsAsync(
            string analysisName,
            string analysisType,
            string job,
            string workitem,
            CancellationToken cancellationToken = default
        )
        {
            using (var _res = await GetDetailsInternalAsync(
                analysisName,
                analysisType,
                job,
                workitem,
                cancellationToken
            ).ConfigureAwait(false))
            {
                return _res.Body;
            }
        }

        internal async Task<HttpOperationResponse<GetDetailsResponse>> GetDetailsInternalAsync(
            string analysisName,
            string analysisType,
            string job,
            string workitem,
            CancellationToken cancellationToken = default
        )
        {
            if (string.IsNullOrEmpty(analysisName))
            {
                throw new ArgumentNullException(nameof(analysisName));
            }

            if (string.IsNullOrEmpty(analysisType))
            {
                throw new ArgumentNullException(nameof(analysisType));
            }

            if (string.IsNullOrEmpty(job))
            {
                throw new ArgumentNullException(nameof(job));
            }

            if (string.IsNullOrEmpty(workitem))
            {
                throw new ArgumentNullException(nameof(workitem));
            }


            var _path = "/api/2018-03-14/analysis/{job}/{analysisType}";
            _path = _path.Replace("{job}", Client.Serialize(job));
            _path = _path.Replace("{analysisType}", Client.Serialize(analysisType));

            var _query = new QueryBuilder();
            if (!string.IsNullOrEmpty(workitem))
            {
                _query.Add("workitem", Client.Serialize(workitem));
            }
            if (!string.IsNullOrEmpty(analysisName))
            {
                _query.Add("analysisName", Client.Serialize(analysisName));
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
                    HandleFailedGetDetailsRequest(ex);
                    HandleFailedRequest(ex);
                    Client.OnFailedRequest(ex);
                    throw ex;
                }
                return new HttpOperationResponse<GetDetailsResponse>
                {
                    Request = _req,
                    Response = _res,
                    Body = Client.Deserialize<GetDetailsResponse>(_responseContent),
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
