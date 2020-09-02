// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Build.Tasks.Feed.Tests.TestDoubles
{
    public static class FakeHttpClient
    {
        public static HttpClient WithResponse(HttpResponseMessage response)
            => WithResponses(new[] { response });

        public static HttpClient WithResponses(IEnumerable<HttpResponseMessage> responses)
            => new HttpClient(
                new FakeHttpMessageHandler(responses));

        private class FakeHttpMessageHandler : HttpMessageHandler
        {
            private readonly IEnumerator<HttpResponseMessage> _responseEnumerator;

            public FakeHttpMessageHandler(IEnumerable<HttpResponseMessage> responses)
            {
                _responseEnumerator = responses.GetEnumerator();
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    _responseEnumerator.Dispose();
                }
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                if (!_responseEnumerator.MoveNext())
                    throw new InvalidOperationException($"Unexpected end of response sequence. Number of predefined responses should be at least equal to number of requests invoked by HttpClient.");

                return Task.FromResult(_responseEnumerator.Current);
            }
        }
    }

}
