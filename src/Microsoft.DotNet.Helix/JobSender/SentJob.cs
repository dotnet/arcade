using System;
using System.Linq;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.DotNet.Helix.Client.Models;
using Microsoft.Rest;

namespace Microsoft.DotNet.Helix.Client
{
    internal class SentJob : ISentJob
    {
        public SentJob(IJob jobApi, JobCreationResult newJob, string resultsContainerUri, string resultsContainerReadSAS)
        {
            JobApi = jobApi;
            CorrelationId = newJob.Name;
            ResultsContainerUri = resultsContainerUri;
            ResultsContainerReadSAS = resultsContainerReadSAS;
        }

        public IJob JobApi { get; }
        public string CorrelationId { get; }
        public string ResultsContainerUri { get; }
        public string ResultsContainerReadSAS { get; }
    }
}
