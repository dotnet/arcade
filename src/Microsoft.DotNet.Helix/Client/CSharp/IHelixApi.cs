using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Helix.Client
{
    partial interface IHelixApi
    {
        int RetryCount { get; set; }
        double RetryBackOffFactor { get; set; }
        bool IsRetryableHttpException(Exception ex);
        Task<T> RetryAsync<T>(Func<Task<T>> function, Action<Exception> logRetry, CancellationToken cancellationToken);
        Task<T> RetryAsync<T>(Func<Task<T>> function, Action<Exception> logRetry, Func<Exception, bool> isRetryable, CancellationToken cancellationToken);        
    }
}
