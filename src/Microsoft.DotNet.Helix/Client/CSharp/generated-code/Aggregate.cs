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
            if (groupBy == default(IImmutableList<string>))
            {
                throw new ArgumentNullException(nameof(groupBy));
            }

            if (otherProperties == default(IImmutableList<string>))
            {
                throw new ArgumentNullException(nameof(otherProperties));
            }

            if (string.IsNullOrEmpty(workitem))
            {
                throw new ArgumentNullException(nameof(workitem));
            }


            var _baseUri = Client.Options.BaseUri;
            var _url = new RequestUriBuilder();
            _url.Reset(_baseUri);
            _url.AppendPath(
                "/api/2019-06-17/aggregate/analysis",
                false);

            if (!string.IsNullOrEmpty(workitem))
            {
                _url.AppendQuery("workitem", Client.Serialize(workitem));
            }
            if (groupBy != default(IImmutableList<string>))
            {
                foreach (var _item in groupBy)
                {
                    _url.AppendQuery("groupBy", Client.Serialize(_item));
                }
            }
            if (otherProperties != default(IImmutableList<string>))
            {
                foreach (var _item in otherProperties)
                {
                    _url.AppendQuery("otherProperties", Client.Serialize(_item));
                }
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
                        await OnAnalysisSummaryFailed(_req, _res).ConfigureAwait(false);
                    }

                    if (_res.ContentStream == null)
                    {
                        await OnAnalysisSummaryFailed(_req, _res).ConfigureAwait(false);
                    }

                    using (var _reader = new StreamReader(_res.ContentStream))
                    {
                        var _content = await _reader.ReadToEndAsync().ConfigureAwait(false);
                        var _body = Client.Deserialize<AggregateWorkItemSummary>(_content);
                        return _body;
                    }
                }
            }
        }

        internal async Task OnAnalysisSummaryFailed(Request req, Response res)
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
            HandleFailedAnalysisSummaryRequest(ex);
            HandleFailedRequest(ex);
            Client.OnFailedRequest(ex);
            throw ex;
        }

        partial void HandleFailedBuildHistoryRequest(RestApiException ex);

        public async Task<IImmutableList<BuildHistoryItem>> BuildHistoryAsync(
            IImmutableList<string> source,
            IImmutableList<string> type,
            CancellationToken cancellationToken = default
        )
        {
            if (source == default(IImmutableList<string>))
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (type == default(IImmutableList<string>))
            {
                throw new ArgumentNullException(nameof(type));
            }


            var _baseUri = Client.Options.BaseUri;
            var _url = new RequestUriBuilder();
            _url.Reset(_baseUri);
            _url.AppendPath(
                "/api/2019-06-17/aggregate/build/history",
                false);

            if (source != default(IImmutableList<string>))
            {
                foreach (var _item in source)
                {
                    _url.AppendQuery("source", Client.Serialize(_item));
                }
            }
            if (type != default(IImmutableList<string>))
            {
                foreach (var _item in type)
                {
                    _url.AppendQuery("type", Client.Serialize(_item));
                }
            }


            using (var _req = Client.Pipeline.CreateRequest())
            {
                _req.Uri = _url;
                _req.Method = RequestMethod.Get;

                using (var _res = await Client.SendAsync(_req, cancellationToken).ConfigureAwait(false))
                {
                    if (_res.Status < 200 || _res.Status >= 300)
                    {
                        await OnBuildHistoryFailed(_req, _res).ConfigureAwait(false);
                    }

                    if (_res.ContentStream == null)
                    {
                        await OnBuildHistoryFailed(_req, _res).ConfigureAwait(false);
                    }

                    using (var _reader = new StreamReader(_res.ContentStream))
                    {
                        var _content = await _reader.ReadToEndAsync().ConfigureAwait(false);
                        var _body = Client.Deserialize<IImmutableList<BuildHistoryItem>>(_content);
                        return _body;
                    }
                }
            }
        }

        internal async Task OnBuildHistoryFailed(Request req, Response res)
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
            HandleFailedBuildHistoryRequest(ex);
            HandleFailedRequest(ex);
            Client.OnFailedRequest(ex);
            throw ex;
        }

        partial void HandleFailedBuildRequest(RestApiException ex);

        public async Task<BuildAggregation> BuildAsync(
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

            if (sources == default(IImmutableList<string>))
            {
                throw new ArgumentNullException(nameof(sources));
            }

            if (types == default(IImmutableList<string>))
            {
                throw new ArgumentNullException(nameof(types));
            }


            var _baseUri = Client.Options.BaseUri;
            var _url = new RequestUriBuilder();
            _url.Reset(_baseUri);
            _url.AppendPath(
                "/api/2019-06-17/aggregate/build",
                false);

            if (sources != default(IImmutableList<string>))
            {
                foreach (var _item in sources)
                {
                    _url.AppendQuery("sources", Client.Serialize(_item));
                }
            }
            if (types != default(IImmutableList<string>))
            {
                foreach (var _item in types)
                {
                    _url.AppendQuery("types", Client.Serialize(_item));
                }
            }
            if (!string.IsNullOrEmpty(buildNumber))
            {
                _url.AppendQuery("buildNumber", Client.Serialize(buildNumber));
            }


            using (var _req = Client.Pipeline.CreateRequest())
            {
                _req.Uri = _url;
                _req.Method = RequestMethod.Get;

                using (var _res = await Client.SendAsync(_req, cancellationToken).ConfigureAwait(false))
                {
                    if (_res.Status < 200 || _res.Status >= 300)
                    {
                        await OnBuildFailed(_req, _res).ConfigureAwait(false);
                    }

                    if (_res.ContentStream == null)
                    {
                        await OnBuildFailed(_req, _res).ConfigureAwait(false);
                    }

                    using (var _reader = new StreamReader(_res.ContentStream))
                    {
                        var _content = await _reader.ReadToEndAsync().ConfigureAwait(false);
                        var _body = Client.Deserialize<BuildAggregation>(_content);
                        return _body;
                    }
                }
            }
        }

        internal async Task OnBuildFailed(Request req, Response res)
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
            HandleFailedBuildRequest(ex);
            HandleFailedRequest(ex);
            Client.OnFailedRequest(ex);
            throw ex;
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
            if (groupBy == default(IImmutableList<string>))
            {
                throw new ArgumentNullException(nameof(groupBy));
            }

            if (maxResultSets == default(int))
            {
                throw new ArgumentNullException(nameof(maxResultSets));
            }


            var _baseUri = Client.Options.BaseUri;
            var _url = new RequestUriBuilder();
            _url.Reset(_baseUri);
            _url.AppendPath(
                "/api/2019-06-17/aggregate/jobs",
                false);

            if (groupBy != default(IImmutableList<string>))
            {
                foreach (var _item in groupBy)
                {
                    _url.AppendQuery("groupBy", Client.Serialize(_item));
                }
            }
            if (maxResultSets != default(int))
            {
                _url.AppendQuery("maxResultSets", Client.Serialize(maxResultSets));
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
                        await OnJobSummaryFailed(_req, _res).ConfigureAwait(false);
                    }

                    if (_res.ContentStream == null)
                    {
                        await OnJobSummaryFailed(_req, _res).ConfigureAwait(false);
                    }

                    using (var _reader = new StreamReader(_res.ContentStream))
                    {
                        var _content = await _reader.ReadToEndAsync().ConfigureAwait(false);
                        var _body = Client.Deserialize<IImmutableList<AggregatedWorkItemCounts>>(_content);
                        return _body;
                    }
                }
            }
        }

        internal async Task OnJobSummaryFailed(Request req, Response res)
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
            HandleFailedJobSummaryRequest(ex);
            HandleFailedRequest(ex);
            Client.OnFailedRequest(ex);
            throw ex;
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
            if (groupBy == default(IImmutableList<string>))
            {
                throw new ArgumentNullException(nameof(groupBy));
            }


            var _baseUri = Client.Options.BaseUri;
            var _url = new RequestUriBuilder();
            _url.Reset(_baseUri);
            _url.AppendPath(
                "/api/2019-06-17/aggregate/workitems",
                false);

            if (groupBy != default(IImmutableList<string>))
            {
                foreach (var _item in groupBy)
                {
                    _url.AppendQuery("groupBy", Client.Serialize(_item));
                }
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
                        await OnWorkItemSummaryFailed(_req, _res).ConfigureAwait(false);
                    }

                    if (_res.ContentStream == null)
                    {
                        await OnWorkItemSummaryFailed(_req, _res).ConfigureAwait(false);
                    }

                    using (var _reader = new StreamReader(_res.ContentStream))
                    {
                        var _content = await _reader.ReadToEndAsync().ConfigureAwait(false);
                        var _body = Client.Deserialize<IImmutableList<AggregatedWorkItemCounts>>(_content);
                        return _body;
                    }
                }
            }
        }

        internal async Task OnWorkItemSummaryFailed(Request req, Response res)
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
            HandleFailedWorkItemSummaryRequest(ex);
            HandleFailedRequest(ex);
            Client.OnFailedRequest(ex);
            throw ex;
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

            if (groupBy == default(IImmutableList<string>))
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


            var _baseUri = Client.Options.BaseUri;
            var _url = new RequestUriBuilder();
            _url.Reset(_baseUri);
            _url.AppendPath(
                "/api/2019-06-17/aggregate/analysisdetail",
                false);

            if (!string.IsNullOrEmpty(source))
            {
                _url.AppendQuery("source", Client.Serialize(source));
            }
            if (!string.IsNullOrEmpty(type))
            {
                _url.AppendQuery("type", Client.Serialize(type));
            }
            if (!string.IsNullOrEmpty(build))
            {
                _url.AppendQuery("build", Client.Serialize(build));
            }
            if (!string.IsNullOrEmpty(workitem))
            {
                _url.AppendQuery("workitem", Client.Serialize(workitem));
            }
            if (!string.IsNullOrEmpty(analysisType))
            {
                _url.AppendQuery("analysisType", Client.Serialize(analysisType));
            }
            if (!string.IsNullOrEmpty(analysisName))
            {
                _url.AppendQuery("analysisName", Client.Serialize(analysisName));
            }
            if (groupBy != default(IImmutableList<string>))
            {
                foreach (var _item in groupBy)
                {
                    _url.AppendQuery("groupBy", Client.Serialize(_item));
                }
            }


            using (var _req = Client.Pipeline.CreateRequest())
            {
                _req.Uri = _url;
                _req.Method = RequestMethod.Get;

                using (var _res = await Client.SendAsync(_req, cancellationToken).ConfigureAwait(false))
                {
                    if (_res.Status < 200 || _res.Status >= 300)
                    {
                        await OnAnalysisDetailFailed(_req, _res).ConfigureAwait(false);
                    }

                    if (_res.ContentStream == null)
                    {
                        await OnAnalysisDetailFailed(_req, _res).ConfigureAwait(false);
                    }

                    using (var _reader = new StreamReader(_res.ContentStream))
                    {
                        var _content = await _reader.ReadToEndAsync().ConfigureAwait(false);
                        var _body = Client.Deserialize<IImmutableList<AggregateAnalysisDetail>>(_content);
                        return _body;
                    }
                }
            }
        }

        internal async Task OnAnalysisDetailFailed(Request req, Response res)
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
            HandleFailedAnalysisDetailRequest(ex);
            HandleFailedRequest(ex);
            Client.OnFailedRequest(ex);
            throw ex;
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

            var _baseUri = Client.Options.BaseUri;
            var _url = new RequestUriBuilder();
            _url.Reset(_baseUri);
            _url.AppendPath(
                "/api/2019-06-17/aggregate/properties",
                false);

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
                        await OnPropertiesFailed(_req, _res).ConfigureAwait(false);
                    }

                    if (_res.ContentStream == null)
                    {
                        await OnPropertiesFailed(_req, _res).ConfigureAwait(false);
                    }

                    using (var _reader = new StreamReader(_res.ContentStream))
                    {
                        var _content = await _reader.ReadToEndAsync().ConfigureAwait(false);
                        var _body = Client.Deserialize<IImmutableDictionary<string, Newtonsoft.Json.Linq.JToken>>(_content);
                        return _body;
                    }
                }
            }
        }

        internal async Task OnPropertiesFailed(Request req, Response res)
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
            HandleFailedPropertiesRequest(ex);
            HandleFailedRequest(ex);
            Client.OnFailedRequest(ex);
            throw ex;
        }

        partial void HandleFailedInvestigation_ContinueRequest(RestApiException ex);

        public async Task<InvestigationResult> Investigation_ContinueAsync(
            string id,
            CancellationToken cancellationToken = default
        )
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentNullException(nameof(id));
            }


            var _baseUri = Client.Options.BaseUri;
            var _url = new RequestUriBuilder();
            _url.Reset(_baseUri);
            _url.AppendPath(
                "/api/2019-06-17/aggregate/investigation/continue/{id}".Replace("{id}", Uri.EscapeDataString(Client.Serialize(id))),
                false);



            using (var _req = Client.Pipeline.CreateRequest())
            {
                _req.Uri = _url;
                _req.Method = RequestMethod.Get;

                using (var _res = await Client.SendAsync(_req, cancellationToken).ConfigureAwait(false))
                {
                    if (_res.Status < 200 || _res.Status >= 300)
                    {
                        await OnInvestigation_ContinueFailed(_req, _res).ConfigureAwait(false);
                    }

                    if (_res.ContentStream == null)
                    {
                        await OnInvestigation_ContinueFailed(_req, _res).ConfigureAwait(false);
                    }

                    using (var _reader = new StreamReader(_res.ContentStream))
                    {
                        var _content = await _reader.ReadToEndAsync().ConfigureAwait(false);
                        var _body = Client.Deserialize<InvestigationResult>(_content);
                        return _body;
                    }
                }
            }
        }

        internal async Task OnInvestigation_ContinueFailed(Request req, Response res)
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
            HandleFailedInvestigation_ContinueRequest(ex);
            HandleFailedRequest(ex);
            Client.OnFailedRequest(ex);
            throw ex;
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
            if (groupBy == default(IImmutableList<string>))
            {
                throw new ArgumentNullException(nameof(groupBy));
            }

            if (maxGroups == default(int))
            {
                throw new ArgumentNullException(nameof(maxGroups));
            }

            if (maxResults == default(int))
            {
                throw new ArgumentNullException(nameof(maxResults));
            }


            var _baseUri = Client.Options.BaseUri;
            var _url = new RequestUriBuilder();
            _url.Reset(_baseUri);
            _url.AppendPath(
                "/api/2019-06-17/aggregate/investigation",
                false);

            if (groupBy != default(IImmutableList<string>))
            {
                foreach (var _item in groupBy)
                {
                    _url.AppendQuery("groupBy", Client.Serialize(_item));
                }
            }
            if (maxGroups != default(int))
            {
                _url.AppendQuery("maxGroups", Client.Serialize(maxGroups));
            }
            if (maxResults != default(int))
            {
                _url.AppendQuery("maxResults", Client.Serialize(maxResults));
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
                        await OnInvestigationFailed(_req, _res).ConfigureAwait(false);
                    }

                    if (_res.ContentStream == null)
                    {
                        await OnInvestigationFailed(_req, _res).ConfigureAwait(false);
                    }

                    using (var _reader = new StreamReader(_res.ContentStream))
                    {
                        var _content = await _reader.ReadToEndAsync().ConfigureAwait(false);
                        var _body = Client.Deserialize<InvestigationResult>(_content);
                        return _body;
                    }
                }
            }
        }

        internal async Task OnInvestigationFailed(Request req, Response res)
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
            HandleFailedInvestigationRequest(ex);
            HandleFailedRequest(ex);
            Client.OnFailedRequest(ex);
            throw ex;
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
            if (string.IsNullOrEmpty(analysisName))
            {
                throw new ArgumentNullException(nameof(analysisName));
            }

            if (string.IsNullOrEmpty(analysisType))
            {
                throw new ArgumentNullException(nameof(analysisType));
            }

            if (days == default(int))
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


            var _baseUri = Client.Options.BaseUri;
            var _url = new RequestUriBuilder();
            _url.Reset(_baseUri);
            _url.AppendPath(
                "/api/2019-06-17/aggregate/history/{analysisType}".Replace("{analysisType}", Uri.EscapeDataString(Client.Serialize(analysisType))),
                false);

            if (!string.IsNullOrEmpty(source))
            {
                _url.AppendQuery("source", Client.Serialize(source));
            }
            if (!string.IsNullOrEmpty(type))
            {
                _url.AppendQuery("type", Client.Serialize(type));
            }
            if (!string.IsNullOrEmpty(workitem))
            {
                _url.AppendQuery("workitem", Client.Serialize(workitem));
            }
            if (!string.IsNullOrEmpty(analysisName))
            {
                _url.AppendQuery("analysisName", Client.Serialize(analysisName));
            }
            if (days != default(int))
            {
                _url.AppendQuery("days", Client.Serialize(days));
            }


            using (var _req = Client.Pipeline.CreateRequest())
            {
                _req.Uri = _url;
                _req.Method = RequestMethod.Get;

                using (var _res = await Client.SendAsync(_req, cancellationToken).ConfigureAwait(false))
                {
                    if (_res.Status < 200 || _res.Status >= 300)
                    {
                        await OnHistoryFailed(_req, _res).ConfigureAwait(false);
                    }

                    if (_res.ContentStream == null)
                    {
                        await OnHistoryFailed(_req, _res).ConfigureAwait(false);
                    }

                    using (var _reader = new StreamReader(_res.ContentStream))
                    {
                        var _content = await _reader.ReadToEndAsync().ConfigureAwait(false);
                        var _body = Client.Deserialize<IImmutableList<HistoricalAnalysisItem>>(_content);
                        return _body;
                    }
                }
            }
        }

        internal async Task OnHistoryFailed(Request req, Response res)
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
            HandleFailedHistoryRequest(ex);
            HandleFailedRequest(ex);
            Client.OnFailedRequest(ex);
            throw ex;
        }

        partial void HandleFailedMultiSourceRequest(RestApiException ex);

        public async Task<MultiSourceResponse> MultiSourceAsync(
            MultiSourceRequest body,
            CancellationToken cancellationToken = default
        )
        {
            if (body == default(MultiSourceRequest))
            {
                throw new ArgumentNullException(nameof(body));
            }


            var _baseUri = Client.Options.BaseUri;
            var _url = new RequestUriBuilder();
            _url.Reset(_baseUri);
            _url.AppendPath(
                "/api/2019-06-17/aggregate/multi-source",
                false);



            using (var _req = Client.Pipeline.CreateRequest())
            {
                _req.Uri = _url;
                _req.Method = RequestMethod.Post;

                if (body != default(MultiSourceRequest))
                {
                    _req.Content = RequestContent.Create(Encoding.UTF8.GetBytes(Client.Serialize(body)));
                    _req.Headers.Add("Content-Type", "application/json; charset=utf-8");
                }

                using (var _res = await Client.SendAsync(_req, cancellationToken).ConfigureAwait(false))
                {
                    if (_res.Status < 200 || _res.Status >= 300)
                    {
                        await OnMultiSourceFailed(_req, _res).ConfigureAwait(false);
                    }

                    if (_res.ContentStream == null)
                    {
                        await OnMultiSourceFailed(_req, _res).ConfigureAwait(false);
                    }

                    using (var _reader = new StreamReader(_res.ContentStream))
                    {
                        var _content = await _reader.ReadToEndAsync().ConfigureAwait(false);
                        var _body = Client.Deserialize<MultiSourceResponse>(_content);
                        return _body;
                    }
                }
            }
        }

        internal async Task OnMultiSourceFailed(Request req, Response res)
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
            HandleFailedMultiSourceRequest(ex);
            HandleFailedRequest(ex);
            Client.OnFailedRequest(ex);
            throw ex;
        }
    }
}
