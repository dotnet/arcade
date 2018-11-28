// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Microsoft.DotNet.DarcLib
{
    public static class IGitRepoExtension
    {
        public static async Task<HttpResponseMessage> ExecuteGitCommand(
            this IGitRepo gitRepo,
            HttpMethod method,
            string requestUri,
            ILogger logger,
            string body = null,
            string versionOverride = null)
        {
            using (HttpClient client = gitRepo.CreateHttpClient(versionOverride))
            {
                var requestManager = new HttpRequestManager(client, method, requestUri, logger, body, versionOverride);

                return await requestManager.ExecuteAsync();
            }
        }

        public static string GetDecodedContent(this IGitRepo gitRepo, string encodedContent)
        {
            try
            {
                byte[] content = Convert.FromBase64String(encodedContent);
                return Encoding.UTF8.GetString(content);
            }
            catch (FormatException)
            {
                return encodedContent;
            }
        }

        public static byte[] GetContentBytes(this IGitRepo gitRepo, string content)
        {
            string decodedContent = GetDecodedContent(gitRepo, content);
            return Encoding.UTF8.GetBytes(decodedContent);
        }
    }
}
