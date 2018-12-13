using System;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Helix.Client
{
    partial interface IHelixApi
    {
        int RetryCount { get; set; }
        double RetryBackOffFactor { get; set; }
        Task<T> RetryAsync<T>(Func<Task<T>> function, Action<Exception> logRetry);
        Task<T> RetryAsync<T>(Func<Task<T>> function, Action<Exception> logRetry, Func<Exception, bool> isRetryable);
    }
}