using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Darc
{
    public static class IGitRepoExtension
    {


        public static async Task<HttpResponseMessage> ExecuteGitCommand(this IGitRepo gitRepo, HttpMethod method, string requestUri, string body = null, string versionOverride = null)
        {
            HttpResponseMessage response;

            using (HttpClient client = gitRepo.CreateHttpClient(versionOverride))
            {
                HttpRequestMessage message = new HttpRequestMessage(method, requestUri);

                if (!string.IsNullOrEmpty(body))
                {
                    message.Content = new StringContent(body, Encoding.UTF8, "application/json");
                }

                response = await client.SendAsync(message);

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"There was an error executing method '{method}' against URI '{requestUri}'. Request failed with error code: '{response.StatusCode}'");
                    response.EnsureSuccessStatusCode();
                }
            }

            return response;
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
