using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Helix.Client
{
    partial class HelixApi
    {
        private static readonly Random s_rand = new Random();

        public int RetryCount { get; set; } = 15;

        public double RetryBackOffFactor { get; set; } = 1.3;

        protected virtual int GetRetryDelay(int attempt)
        {
            var factor = RetryBackOffFactor;
            var min = (int) (Math.Pow(factor, attempt) * 1000);
            var max = (int) (Math.Pow(factor, attempt + 1) * 1000);
            return s_rand.Next(min, max);
        }

        private static bool IsRetryableHttpException(Exception ex)
        {
            return ex is TaskCanceledException ||
                   ex is OperationCanceledException ||
                   ex is HttpRequestException ||
                   ex is NullReferenceException // Null reference exceptions come from autorest for some reason and are retryable
                ;
        }

        public Task<T> RetryAsync<T>(Func<Task<T>> function, Action<Exception> logRetry)
        {
            return RetryAsync<T>(function, logRetry, IsRetryableHttpException);
        }

        public async Task<T> RetryAsync<T>(Func<Task<T>> function, Action<Exception> logRetry, Func<Exception, bool> isRetryable)
        {
            var attempt = 0;
            var maxAttempt = RetryCount;
            while (true)
            {
                try
                {
                    return await function();
                }
                catch (Exception ex) when (isRetryable(ex))
                {
                    if (attempt >= maxAttempt)
                    {
                        throw;
                    }

                    logRetry(ex);
                }
                await Task.Delay(GetRetryDelay(attempt));
                attempt++;
            }
        }
    }
}