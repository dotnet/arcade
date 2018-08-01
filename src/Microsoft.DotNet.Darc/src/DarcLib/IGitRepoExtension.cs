using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.DotNet.DarcLib
{
    public static class IGitRepoExtension
    {
        public static async Task<HttpResponseMessage> ExecuteGitCommand(this IGitRepo gitRepo, HttpMethod method, string requestUri, ILogger logger, string body = null, string versionOverride = null)
        {
            using (HttpClient client = gitRepo.CreateHttpClient(versionOverride))
            {
                HttpRequestManager requestManager = new HttpRequestManager(client, method, requestUri, logger, body, versionOverride);

                return await requestManager.ExecuteAsync();
            }
        }

        public static string GetDecodedContent(this IGitRepo gitRepo, string encodedContent)
        {
            JsonSerializerSettings serializerSettings = new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            };

            byte[] content = Convert.FromBase64String(encodedContent);
            return Encoding.UTF8.GetString(content);
        }
    }
}
