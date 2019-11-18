using System;
using Microsoft.Rest.TransientFaultHandling;

namespace Microsoft.DotNet.Helix.Client
{
    internal class HelixApiServiceClientErrorDetectionStrategy : ITransientErrorDetectionStrategy
    {
        private HttpStatusCodeErrorDetectionStrategy _httpStatusCodeErrorDetectionStrategy = new HttpStatusCodeErrorDetectionStrategy();

        public bool IsTransient(Exception ex)
        {
            if (ex is HttpRequestWithStatusException)
                return _httpStatusCodeErrorDetectionStrategy.IsTransient(ex);

            return HelixApi.IsRetryableHttpException(ex);
        }
    }
}
