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
        public SentJob(IJob jobApi, JobCreationResult newJob)
        {
            JobApi = jobApi;
            CorrelationId = newJob.Name;
        }

        public IJob JobApi { get; }
        public string CorrelationId { get; }
    }
}
