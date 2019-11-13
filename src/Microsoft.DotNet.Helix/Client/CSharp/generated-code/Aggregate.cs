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
    public partial interface IAggregate
    {
        Task<AggregateWorkItemSummary> AnalysisSummaryAsync(
            IImmutableList<string> groupBy,
            IImmutableList<string> otherProperties,
            string workitem,
            string filterBuild = default,
            string filterCreator = default,
            string filterName = default,
            string filterSource = default,
            string filterType = default,
            CancellationToken cancellationToken = default
        );

        Task<IImmutableList<BuildHistoryItem>> BuildHistoryAsync(
            IImmutableList<string> source,
            IImmutableList<string> type,
            CancellationToken cancellationToken = default
        );

        Task<BuildAggregation> BuildAsync(
            string buildNumber,
            IImmutableList<string> sources,
            IImmutableList<string> types,
            CancellationToken cancellationToken = default
        );

        Task<IImmutableList<AggregatedWorkItemCounts>> JobSummaryAsync(
            IImmutableList<string> groupBy,
            int maxResultSets,
            string filterBuild = default,
            string filterCreator = default,
            string filterName = default,
            string filterSource = default,
            string filterType = default,
            CancellationToken cancellationToken = default
        );

        Task<IImmutableList<AggregatedWorkItemCounts>> WorkItemSummaryAsync(
            IImmutableList<string> groupBy,
            string filterBuild = default,
            string filterCreator = default,
            string filterName = default,
            string filterSource = default,
            string filterType = default,
            CancellationToken cancellationToken = default
        );

        Task<IImmutableList<AggregateAnalysisDetail>> AnalysisDetailAsync(
            string analysisName,
            string analysisType,
            string build,
            IImmutableList<string> groupBy,
            string source,
            string type,
            string workitem,
            CancellationToken cancellationToken = default
        );

        Task<IImmutableDictionary<string, Newtonsoft.Json.Linq.JToken>> PropertiesAsync(
            string filterBuild = default,
            string filterCreator = default,
            string filterName = default,
            string filterSource = default,
            string filterType = default,
            CancellationToken cancellationToken = default
        );

        Task<InvestigationResult> Investigation_ContinueAsync(
            string id,
            CancellationToken cancellationToken = default
        );

        Task<InvestigationResult> InvestigationAsync(
            IImmutableList<string> groupBy,
            int maxGroups,
            int maxResults,
            string filterBuild = default,
            string filterCreator = default,
            string filterName = default,
            string filterSource = default,
            string filterType = default,
            CancellationToken cancellationToken = default
        );

        Task<IImmutableList<HistoricalAnalysisItem>> HistoryAsync(
            string analysisName,
            string analysisType,
            int days,
            string source,
            string type,
            string workitem,
            CancellationToken cancellationToken = default
        );

        Task<MultiSourceResponse> MultiSourceAsync(
            MultiSourceRequest body,
            CancellationToken cancellationToken = default
        );

    }

    internal partial class Aggregate : IServiceOperations<HelixApi>, IAggregate
    {
        public Aggregate(HelixApi client)
        {
            Client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public HelixApi Client { get; }

        partial void HandleFailedRequest(RestApiException ex);

        partial void HandleFailedAnalysisSummaryRequest(RestApiException ex);

        public async Task<AggregateWorkItemSummary> AnalysisSummaryAsync(
            IImmutableList<string> groupBy,
            IImmutableList<string> otherProperties,
            string workitem,
            string filterBuild = default,
            string filterCreator = default,
            string filterName = default,
            string filterSource = default,
            string filterType = default,
            CancellationToken cancellationToken = default
        )
        {
            using (var _res = await AnalysisSummaryInternalAsync(
                groupBy,
                otherProperties,
                workitem,
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

        internal async Task OnAnalysisSummaryFailed(HttpRequestMessage req, HttpResponseMessage res)
        {
            var content = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
            var ex = new RestApiException(
                new HttpRequestMessageWrapper(req, null),
                new HttpResponseMessageWrapper(res, content));
            HandleFailedAnalysisSummaryRequest(ex);
            HandleFailedRequest(ex);
            Client.OnFailedRequest(ex);
            throw ex;
        }

        internal async Task<HttpOperationResponse<AggregateWorkItemSummary>> AnalysisSummaryInternalAsync(
            IImmutableList<string> groupBy,
            IImmutableList<string> otherProperties,
            string workitem,
            string filterBuild = default,
            string filterCreator = default,
            string filterName = default,
            string filterSource = default,
            string filterType = default,
            CancellationToken cancellationToken = default
        )
        {
            if (groupBy == default)
            {
                throw new ArgumentNullException(nameof(groupBy));
            }

            if (otherProperties == default)
            {
                throw new ArgumentNullException(nameof(otherProperties));
            }

            if (string.IsNullOrEmpty(workitem))
            {
                throw new ArgumentNullException(nameof(workitem));
            }


            var _path = "/api/2019-06-17/aggregate/analysis";

            var _query = new QueryBuilder();
            if (!string.IsNullOrEmpty(workitem))
            {
                _query.Add("workitem", Client.Serialize(workitem));
            }
            if (groupBy != default)
            {
                foreach (var _item in groupBy)
                {
                    _query.Add("groupBy", Client.Serialize(_item));
                }
            }
            if (otherProperties != default)
            {
                foreach (var _item in otherProperties)
                {
                    _query.Add("otherProperties", Client.Serialize(_item));
                }
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
                if (!_res.IsSuccessStatusCode)
                {
                    await OnAnalysisSummaryFailed(_req, _res);
                }
                string _responseContent = await _res.Content.ReadAsStringAsync().ConfigureAwait(false);
                return new HttpOperationResponse<AggregateWorkItemSummary>
                {
                    Request = _req,
                    Response = _res,
                    Body = Client.Deserialize<AggregateWorkItemSummary>(_responseContent),
                };
            }
            catch (Exception)
            {
                _req?.Dispose();
                _res?.Dispose();
                throw;
            }
        }

        partial void HandleFailedBuildHistoryRequest(RestApiException ex);

        public async Task<IImmutableList<BuildHistoryItem>> BuildHistoryAsync(
            IImmutableList<string> source,
            IImmutableList<string> type,
            CancellationToken cancellationToken = default
        )
        {
            using (var _res = await BuildHistoryInternalAsync(
                source,
                type,
                cancellationToken
            ).ConfigureAwait(false))
            {
                return _res.Body;
            }
        }

        internal async Task OnBuildHistoryFailed(HttpRequestMessage req, HttpResponseMessage res)
        {
            var content = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
            var ex = new RestApiException(
                new HttpRequestMessageWrapper(req, null),
                new HttpResponseMessageWrapper(res, content));
            HandleFailedBuildHistoryRequest(ex);
            HandleFailedRequest(ex);
            Client.OnFailedRequest(ex);
            throw ex;
        }

        internal async Task<HttpOperationResponse<IImmutableList<BuildHistoryItem>>> BuildHistoryInternalAsync(
            IImmutableList<string> source,
            IImmutableList<string> type,
            CancellationToken cancellationToken = default
        )
        {
            if (source == default)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (type == default)
            {
                throw new ArgumentNullException(nameof(type));
            }


            var _path = "/api/2019-06-17/aggregate/build/history";

            var _query = new QueryBuilder();
            if (source != default)
            {
                foreach (var _item in source)
                {
                    _query.Add("source", Client.Serialize(_item));
                }
            }
            if (type != default)
            {
                foreach (var _item in type)
                {
                    _query.Add("type", Client.Serialize(_item));
                }
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
                    await OnBuildHistoryFailed(_req, _res);
                }
                string _responseContent = await _res.Content.ReadAsStringAsync().ConfigureAwait(false);
                return new HttpOperationResponse<IImmutableList<BuildHistoryItem>>
                {
                    Request = _req,
                    Response = _res,
                    Body = Client.Deserialize<IImmutableList<BuildHistoryItem>>(_responseContent),
                };
            }
            catch (Exception)
            {
                _req?.Dispose();
                _res?.Dispose();
                throw;
            }
        }

        partial void HandleFailedBuildRequest(RestApiException ex);

        public async Task<BuildAggregation> BuildAsync(
            string buildNumber,
            IImmutableList<string> sources,
            IImmutableList<string> types,
            CancellationToken cancellationToken = default
        )
        {
            using (var _res = await BuildInternalAsync(
                buildNumber,
                sources,
                types,
                cancellationToken
            ).ConfigureAwait(false))
            {
                return _res.Body;
            }
        }

        internal async Task OnBuildFailed(HttpRequestMessage req, HttpResponseMessage res)
        {
            var content = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
            var ex = new RestApiException(
                new HttpRequestMessageWrapper(req, null),
                new HttpResponseMessageWrapper(res, content));
            HandleFailedBuildRequest(ex);
            HandleFailedRequest(ex);
            Client.OnFailedRequest(ex);
            throw ex;
        }

        internal async Task<HttpOperationResponse<BuildAggregation>> BuildInternalAsync(
            string buildNumber,
            IImmutableList<string> sources,
            IImmutableList<string> types,
            CancellationToken cancellationToken = default
        )
        {
            if (string.IsNullOrEmpty(buildNumber))
            {
                throw new ArgumentNullException(nameof(buildNumber));
            }

            if (sources == default)
            {
                throw new ArgumentNullException(nameof(sources));
            }

            if (types == default)
            {
                throw new ArgumentNullException(nameof(types));
            }


            var _path = "/api/2019-06-17/aggregate/build";

            var _query = new QueryBuilder();
            if (sources != default)
            {
                foreach (var _item in sources)
                {
                    _query.Add("sources", Client.Serialize(_item));
                }
            }
            if (types != default)
            {
                foreach (var _item in types)
                {
                    _query.Add("types", Client.Serialize(_item));
                }
            }
            if (!string.IsNullOrEmpty(buildNumber))
            {
                _query.Add("buildNumber", Client.Serialize(buildNumber));
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
                    await OnBuildFailed(_req, _res);
                }
                string _responseContent = await _res.Content.ReadAsStringAsync().ConfigureAwait(false);
                return new HttpOperationResponse<BuildAggregation>
                {
                    Request = _req,
                    Response = _res,
                    Body = Client.Deserialize<BuildAggregation>(_responseContent),
                };
            }
            catch (Exception)
            {
                _req?.Dispose();
                _res?.Dispose();
                throw;
            }
        }

        partial void HandleFailedJobSummaryRequest(RestApiException ex);

        public async Task<IImmutableList<AggregatedWorkItemCounts>> JobSummaryAsync(
            IImmutableList<string> groupBy,
            int maxResultSets,
            string filterBuild = default,
            string filterCreator = default,
            string filterName = default,
            string filterSource = default,
            string filterType = default,
            CancellationToken cancellationToken = default
        )
        {
            using (var _res = await JobSummaryInternalAsync(
                groupBy,
                maxResultSets,
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

        internal async Task OnJobSummaryFailed(HttpRequestMessage req, HttpResponseMessage res)
        {
            var content = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
            var ex = new RestApiException(
                new HttpRequestMessageWrapper(req, null),
                new HttpResponseMessageWrapper(res, content));
            HandleFailedJobSummaryRequest(ex);
            HandleFailedRequest(ex);
            Client.OnFailedRequest(ex);
            throw ex;
        }

        internal async Task<HttpOperationResponse<IImmutableList<AggregatedWorkItemCounts>>> JobSummaryInternalAsync(
            IImmutableList<string> groupBy,
            int maxResultSets,
            string filterBuild = default,
            string filterCreator = default,
            string filterName = default,
            string filterSource = default,
            string filterType = default,
            CancellationToken cancellationToken = default
        )
        {
            if (groupBy == default)
            {
                throw new ArgumentNullException(nameof(groupBy));
            }

            if (maxResultSets == default)
            {
                throw new ArgumentNullException(nameof(maxResultSets));
            }


            var _path = "/api/2019-06-17/aggregate/jobs";

            var _query = new QueryBuilder();
            if (groupBy != default)
            {
                foreach (var _item in groupBy)
                {
                    _query.Add("groupBy", Client.Serialize(_item));
                }
            }
            if (maxResultSets != default)
            {
                _query.Add("maxResultSets", Client.Serialize(maxResultSets));
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
                if (!_res.IsSuccessStatusCode)
                {
                    await OnJobSummaryFailed(_req, _res);
                }
                string _responseContent = await _res.Content.ReadAsStringAsync().ConfigureAwait(false);
                return new HttpOperationResponse<IImmutableList<AggregatedWorkItemCounts>>
                {
                    Request = _req,
                    Response = _res,
                    Body = Client.Deserialize<IImmutableList<AggregatedWorkItemCounts>>(_responseContent),
                };
            }
            catch (Exception)
            {
                _req?.Dispose();
                _res?.Dispose();
                throw;
            }
        }

        partial void HandleFailedWorkItemSummaryRequest(RestApiException ex);

        public async Task<IImmutableList<AggregatedWorkItemCounts>> WorkItemSummaryAsync(
            IImmutableList<string> groupBy,
            string filterBuild = default,
            string filterCreator = default,
            string filterName = default,
            string filterSource = default,
            string filterType = default,
            CancellationToken cancellationToken = default
        )
        {
            using (var _res = await WorkItemSummaryInternalAsync(
                groupBy,
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

        internal async Task OnWorkItemSummaryFailed(HttpRequestMessage req, HttpResponseMessage res)
        {
            var content = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
            var ex = new RestApiException(
                new HttpRequestMessageWrapper(req, null),
                new HttpResponseMessageWrapper(res, content));
            HandleFailedWorkItemSummaryRequest(ex);
            HandleFailedRequest(ex);
            Client.OnFailedRequest(ex);
            throw ex;
        }

        internal async Task<HttpOperationResponse<IImmutableList<AggregatedWorkItemCounts>>> WorkItemSummaryInternalAsync(
            IImmutableList<string> groupBy,
            string filterBuild = default,
            string filterCreator = default,
            string filterName = default,
            string filterSource = default,
            string filterType = default,
            CancellationToken cancellationToken = default
        )
        {
            if (groupBy == default)
            {
                throw new ArgumentNullException(nameof(groupBy));
            }


            var _path = "/api/2019-06-17/aggregate/workitems";

            var _query = new QueryBuilder();
            if (groupBy != default)
            {
                foreach (var _item in groupBy)
                {
                    _query.Add("groupBy", Client.Serialize(_item));
                }
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
                if (!_res.IsSuccessStatusCode)
                {
                    await OnWorkItemSummaryFailed(_req, _res);
                }
                string _responseContent = await _res.Content.ReadAsStringAsync().ConfigureAwait(false);
                return new HttpOperationResponse<IImmutableList<AggregatedWorkItemCounts>>
                {
                    Request = _req,
                    Response = _res,
                    Body = Client.Deserialize<IImmutableList<AggregatedWorkItemCounts>>(_responseContent),
                };
            }
            catch (Exception)
            {
                _req?.Dispose();
                _res?.Dispose();
                throw;
            }
        }

        partial void HandleFailedAnalysisDetailRequest(RestApiException ex);

        public async Task<IImmutableList<AggregateAnalysisDetail>> AnalysisDetailAsync(
            string analysisName,
            string analysisType,
            string build,
            IImmutableList<string> groupBy,
            string source,
            string type,
            string workitem,
            CancellationToken cancellationToken = default
        )
        {
            using (var _res = await AnalysisDetailInternalAsync(
                analysisName,
                analysisType,
                build,
                groupBy,
                source,
                type,
                workitem,
                cancellationToken
            ).ConfigureAwait(false))
            {
                return _res.Body;
            }
        }

        internal async Task OnAnalysisDetailFailed(HttpRequestMessage req, HttpResponseMessage res)
        {
            var content = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
            var ex = new RestApiException(
                new HttpRequestMessageWrapper(req, null),
                new HttpResponseMessageWrapper(res, content));
            HandleFailedAnalysisDetailRequest(ex);
            HandleFailedRequest(ex);
            Client.OnFailedRequest(ex);
            throw ex;
        }

        internal async Task<HttpOperationResponse<IImmutableList<AggregateAnalysisDetail>>> AnalysisDetailInternalAsync(
            string analysisName,
            string analysisType,
            string build,
            IImmutableList<string> groupBy,
            string source,
            string type,
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

            if (string.IsNullOrEmpty(build))
            {
                throw new ArgumentNullException(nameof(build));
            }

            if (groupBy == default)
            {
                throw new ArgumentNullException(nameof(groupBy));
            }

            if (string.IsNullOrEmpty(source))
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (string.IsNullOrEmpty(type))
            {
                throw new ArgumentNullException(nameof(type));
            }

            if (string.IsNullOrEmpty(workitem))
            {
                throw new ArgumentNullException(nameof(workitem));
            }


            var _path = "/api/2019-06-17/aggregate/analysisdetail";

            var _query = new QueryBuilder();
            if (!string.IsNullOrEmpty(source))
            {
                _query.Add("source", Client.Serialize(source));
            }
            if (!string.IsNullOrEmpty(type))
            {
                _query.Add("type", Client.Serialize(type));
            }
            if (!string.IsNullOrEmpty(build))
            {
                _query.Add("build", Client.Serialize(build));
            }
            if (!string.IsNullOrEmpty(workitem))
            {
                _query.Add("workitem", Client.Serialize(workitem));
            }
            if (!string.IsNullOrEmpty(analysisType))
            {
                _query.Add("analysisType", Client.Serialize(analysisType));
            }
            if (!string.IsNullOrEmpty(analysisName))
            {
                _query.Add("analysisName", Client.Serialize(analysisName));
            }
            if (groupBy != default)
            {
                foreach (var _item in groupBy)
                {
                    _query.Add("groupBy", Client.Serialize(_item));
                }
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
                    await OnAnalysisDetailFailed(_req, _res);
                }
                string _responseContent = await _res.Content.ReadAsStringAsync().ConfigureAwait(false);
                return new HttpOperationResponse<IImmutableList<AggregateAnalysisDetail>>
                {
                    Request = _req,
                    Response = _res,
                    Body = Client.Deserialize<IImmutableList<AggregateAnalysisDetail>>(_responseContent),
                };
            }
            catch (Exception)
            {
                _req?.Dispose();
                _res?.Dispose();
                throw;
            }
        }

        partial void HandleFailedPropertiesRequest(RestApiException ex);

        public async Task<IImmutableDictionary<string, Newtonsoft.Json.Linq.JToken>> PropertiesAsync(
            string filterBuild = default,
            string filterCreator = default,
            string filterName = default,
            string filterSource = default,
            string filterType = default,
            CancellationToken cancellationToken = default
        )
        {
            using (var _res = await PropertiesInternalAsync(
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

        internal async Task OnPropertiesFailed(HttpRequestMessage req, HttpResponseMessage res)
        {
            var content = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
            var ex = new RestApiException(
                new HttpRequestMessageWrapper(req, null),
                new HttpResponseMessageWrapper(res, content));
            HandleFailedPropertiesRequest(ex);
            HandleFailedRequest(ex);
            Client.OnFailedRequest(ex);
            throw ex;
        }

        internal async Task<HttpOperationResponse<IImmutableDictionary<string, Newtonsoft.Json.Linq.JToken>>> PropertiesInternalAsync(
            string filterBuild = default,
            string filterCreator = default,
            string filterName = default,
            string filterSource = default,
            string filterType = default,
            CancellationToken cancellationToken = default
        )
        {

            var _path = "/api/2019-06-17/aggregate/properties";

            var _query = new QueryBuilder();
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
                if (!_res.IsSuccessStatusCode)
                {
                    await OnPropertiesFailed(_req, _res);
                }
                string _responseContent = await _res.Content.ReadAsStringAsync().ConfigureAwait(false);
                return new HttpOperationResponse<IImmutableDictionary<string, Newtonsoft.Json.Linq.JToken>>
                {
                    Request = _req,
                    Response = _res,
                    Body = Client.Deserialize<IImmutableDictionary<string, Newtonsoft.Json.Linq.JToken>>(_responseContent),
                };
            }
            catch (Exception)
            {
                _req?.Dispose();
                _res?.Dispose();
                throw;
            }
        }

        partial void HandleFailedInvestigation_ContinueRequest(RestApiException ex);

        public async Task<InvestigationResult> Investigation_ContinueAsync(
            string id,
            CancellationToken cancellationToken = default
        )
        {
            using (var _res = await Investigation_ContinueInternalAsync(
                id,
                cancellationToken
            ).ConfigureAwait(false))
            {
                return _res.Body;
            }
        }

        internal async Task OnInvestigation_ContinueFailed(HttpRequestMessage req, HttpResponseMessage res)
        {
            var content = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
            var ex = new RestApiException(
                new HttpRequestMessageWrapper(req, null),
                new HttpResponseMessageWrapper(res, content));
            HandleFailedInvestigation_ContinueRequest(ex);
            HandleFailedRequest(ex);
            Client.OnFailedRequest(ex);
            throw ex;
        }

        internal async Task<HttpOperationResponse<InvestigationResult>> Investigation_ContinueInternalAsync(
            string id,
            CancellationToken cancellationToken = default
        )
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(nameof(id));
            }


            var _path = "/api/2019-06-17/aggregate/investigation/continue/{id}";
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
                    await OnInvestigation_ContinueFailed(_req, _res);
                }
                string _responseContent = await _res.Content.ReadAsStringAsync().ConfigureAwait(false);
                return new HttpOperationResponse<InvestigationResult>
                {
                    Request = _req,
                    Response = _res,
                    Body = Client.Deserialize<InvestigationResult>(_responseContent),
                };
            }
            catch (Exception)
            {
                _req?.Dispose();
                _res?.Dispose();
                throw;
            }
        }

        partial void HandleFailedInvestigationRequest(RestApiException ex);

        public async Task<InvestigationResult> InvestigationAsync(
            IImmutableList<string> groupBy,
            int maxGroups,
            int maxResults,
            string filterBuild = default,
            string filterCreator = default,
            string filterName = default,
            string filterSource = default,
            string filterType = default,
            CancellationToken cancellationToken = default
        )
        {
            using (var _res = await InvestigationInternalAsync(
                groupBy,
                maxGroups,
                maxResults,
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

        internal async Task OnInvestigationFailed(HttpRequestMessage req, HttpResponseMessage res)
        {
            var content = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
            var ex = new RestApiException(
                new HttpRequestMessageWrapper(req, null),
                new HttpResponseMessageWrapper(res, content));
            HandleFailedInvestigationRequest(ex);
            HandleFailedRequest(ex);
            Client.OnFailedRequest(ex);
            throw ex;
        }

        internal async Task<HttpOperationResponse<InvestigationResult>> InvestigationInternalAsync(
            IImmutableList<string> groupBy,
            int maxGroups,
            int maxResults,
            string filterBuild = default,
            string filterCreator = default,
            string filterName = default,
            string filterSource = default,
            string filterType = default,
            CancellationToken cancellationToken = default
        )
        {
            if (groupBy == default)
            {
                throw new ArgumentNullException(nameof(groupBy));
            }

            if (maxGroups == default)
            {
                throw new ArgumentNullException(nameof(maxGroups));
            }

            if (maxResults == default)
            {
                throw new ArgumentNullException(nameof(maxResults));
            }


            var _path = "/api/2019-06-17/aggregate/investigation";

            var _query = new QueryBuilder();
            if (groupBy != default)
            {
                foreach (var _item in groupBy)
                {
                    _query.Add("groupBy", Client.Serialize(_item));
                }
            }
            if (maxGroups != default)
            {
                _query.Add("maxGroups", Client.Serialize(maxGroups));
            }
            if (maxResults != default)
            {
                _query.Add("maxResults", Client.Serialize(maxResults));
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
                if (!_res.IsSuccessStatusCode)
                {
                    await OnInvestigationFailed(_req, _res);
                }
                string _responseContent = await _res.Content.ReadAsStringAsync().ConfigureAwait(false);
                return new HttpOperationResponse<InvestigationResult>
                {
                    Request = _req,
                    Response = _res,
                    Body = Client.Deserialize<InvestigationResult>(_responseContent),
                };
            }
            catch (Exception)
            {
                _req?.Dispose();
                _res?.Dispose();
                throw;
            }
        }

        partial void HandleFailedHistoryRequest(RestApiException ex);

        public async Task<IImmutableList<HistoricalAnalysisItem>> HistoryAsync(
            string analysisName,
            string analysisType,
            int days,
            string source,
            string type,
            string workitem,
            CancellationToken cancellationToken = default
        )
        {
            using (var _res = await HistoryInternalAsync(
                analysisName,
                analysisType,
                days,
                source,
                type,
                workitem,
                cancellationToken
            ).ConfigureAwait(false))
            {
                return _res.Body;
            }
        }

        internal async Task OnHistoryFailed(HttpRequestMessage req, HttpResponseMessage res)
        {
            var content = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
            var ex = new RestApiException(
                new HttpRequestMessageWrapper(req, null),
                new HttpResponseMessageWrapper(res, content));
            HandleFailedHistoryRequest(ex);
            HandleFailedRequest(ex);
            Client.OnFailedRequest(ex);
            throw ex;
        }

        internal async Task<HttpOperationResponse<IImmutableList<HistoricalAnalysisItem>>> HistoryInternalAsync(
            string analysisName,
            string analysisType,
            int days,
            string source,
            string type,
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

            if (days == default)
            {
                throw new ArgumentNullException(nameof(days));
            }

            if (string.IsNullOrEmpty(source))
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (string.IsNullOrEmpty(type))
            {
                throw new ArgumentNullException(nameof(type));
            }

            if (string.IsNullOrEmpty(workitem))
            {
                throw new ArgumentNullException(nameof(workitem));
            }


            var _path = "/api/2019-06-17/aggregate/history/{analysisType}";
            _path = _path.Replace("{analysisType}", Client.Serialize(analysisType));

            var _query = new QueryBuilder();
            if (!string.IsNullOrEmpty(source))
            {
                _query.Add("source", Client.Serialize(source));
            }
            if (!string.IsNullOrEmpty(type))
            {
                _query.Add("type", Client.Serialize(type));
            }
            if (!string.IsNullOrEmpty(workitem))
            {
                _query.Add("workitem", Client.Serialize(workitem));
            }
            if (!string.IsNullOrEmpty(analysisName))
            {
                _query.Add("analysisName", Client.Serialize(analysisName));
            }
            if (days != default)
            {
                _query.Add("days", Client.Serialize(days));
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
                    await OnHistoryFailed(_req, _res);
                }
                string _responseContent = await _res.Content.ReadAsStringAsync().ConfigureAwait(false);
                return new HttpOperationResponse<IImmutableList<HistoricalAnalysisItem>>
                {
                    Request = _req,
                    Response = _res,
                    Body = Client.Deserialize<IImmutableList<HistoricalAnalysisItem>>(_responseContent),
                };
            }
            catch (Exception)
            {
                _req?.Dispose();
                _res?.Dispose();
                throw;
            }
        }

        partial void HandleFailedMultiSourceRequest(RestApiException ex);

        public async Task<MultiSourceResponse> MultiSourceAsync(
            MultiSourceRequest body,
            CancellationToken cancellationToken = default
        )
        {
            using (var _res = await MultiSourceInternalAsync(
                body,
                cancellationToken
            ).ConfigureAwait(false))
            {
                return _res.Body;
            }
        }

        internal async Task OnMultiSourceFailed(HttpRequestMessage req, HttpResponseMessage res)
        {
            var content = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
            var ex = new RestApiException(
                new HttpRequestMessageWrapper(req, content),
                new HttpResponseMessageWrapper(res, content));
            HandleFailedMultiSourceRequest(ex);
            HandleFailedRequest(ex);
            Client.OnFailedRequest(ex);
            throw ex;
        }

        internal async Task<HttpOperationResponse<MultiSourceResponse>> MultiSourceInternalAsync(
            MultiSourceRequest body,
            CancellationToken cancellationToken = default
        )
        {
            if (body == default)
            {
                throw new ArgumentNullException(nameof(body));
            }


            var _path = "/api/2019-06-17/aggregate/multi-source";

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
                if (!_res.IsSuccessStatusCode)
                {
                    await OnMultiSourceFailed(_req, _res);
                }
                string _responseContent = await _res.Content.ReadAsStringAsync().ConfigureAwait(false);
                return new HttpOperationResponse<MultiSourceResponse>
                {
                    Request = _req,
                    Response = _res,
                    Body = Client.Deserialize<MultiSourceResponse>(_responseContent),
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
