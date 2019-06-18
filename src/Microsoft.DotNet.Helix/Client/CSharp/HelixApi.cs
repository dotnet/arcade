using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Microsoft.DotNet.Helix.Client
{
    partial class HelixApi
    {
        partial void HandleFailedRequest(RestApiException ex)
        {
            if (ex.Response.StatusCode == HttpStatusCode.BadRequest)
            {
                JObject content;
                try
                {
                    content = JObject.Parse(ex.Response.Content);
                }
                catch (Exception)
                {
                    return;
                }

                if (content["Message"] is JValue value && value.Type == JTokenType.String)
                {
                    string message = (string)value.Value;

                    throw new ArgumentException(message, ex);
                }
            }
        }

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

        public static bool IsRetryableHttpException(Exception ex)
        {
            return ex is TaskCanceledException ||
                   ex is OperationCanceledException ||
                   ex is HttpRequestException ||
                   ex is RestApiException raex && (int)raex.Response.StatusCode >= 500 && (int)raex.Response.StatusCode <= 599 ||
                   ex is NullReferenceException // Null reference exceptions come from autorest for some reason and are retryable
                ;
        }

        public Task<T> RetryAsync<T>(Func<Task<T>> function, Action<Exception> logRetry,
            CancellationToken cancellationToken)
        {
            return RetryAsync<T>(function, logRetry, IsRetryableHttpException, cancellationToken);
        }

        public async Task<T> RetryAsync<T>(Func<Task<T>> function, Action<Exception> logRetry,
            Func<Exception, bool> isRetryable, CancellationToken cancellationToken)
        {
            var attempt = 0;
            var maxAttempt = RetryCount;
            cancellationToken.ThrowIfCancellationRequested();
            while (true)
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return await function();
                }
                catch (OperationCanceledException ocex) when (ocex.CancellationToken == cancellationToken)
                {
                    throw;
                }
                catch (Exception ex) when (isRetryable(ex))
                {
                    if (attempt >= maxAttempt)
                    {
                        throw;
                    }

                    logRetry(ex);
                }
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Delay(GetRetryDelay(attempt));
                cancellationToken.ThrowIfCancellationRequested();
                attempt++;
            }
        }
    }
}
