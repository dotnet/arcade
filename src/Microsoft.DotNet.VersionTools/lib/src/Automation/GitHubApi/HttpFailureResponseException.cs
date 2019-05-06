// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Net;
using System.Net.Http;

namespace Microsoft.DotNet.VersionTools.Automation.GitHubApi
{
    public class HttpFailureResponseException : HttpRequestException
    {
        public HttpStatusCode HttpStatusCode { get; }

        public string Content { get; }

        public HttpFailureResponseException(
            HttpStatusCode httpStatusCode,
            string message,
            string content)
            : base(message)
        {
            HttpStatusCode = httpStatusCode;
            Content = content;
        }
    }
}
