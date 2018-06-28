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
            Name = newJob.Name;
        }

        public IJob JobApi { get; }
        public string Name { get; }

        public async Task WaitAsync()
        {
            while (true)
            {
                try
                {
                    await JobApi.WaitAsync(Name);
                    Console.WriteLine("Job's done!");
                    return;
                }
                catch (HttpOperationException ex) when ((int) ex.Response.StatusCode >= 500)
                {
                    string retryAfterStr = ex.Response.Headers["Retry-After"].FirstOrDefault();
                    RetryConditionHeaderValue retryAfter =
                        retryAfterStr == null ? null : RetryConditionHeaderValue.Parse(retryAfterStr);
                    TimeSpan retryTimeSpan = retryAfter?.Delta ?? TimeSpan.FromMinutes(1);
                    await Task.Delay(retryTimeSpan);
                }
                catch (TaskCanceledException)
                {
                    await Task.Delay(TimeSpan.FromMinutes(1));
                }
            }
        }
    }
}
