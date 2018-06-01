using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Darc
{
    public class GitHubClient
    {
        private readonly string personalAccessToken;
        private const string GitHubApiUri = "https://api.github.com";

        public GitHubClient(string accessToken)
        {
            personalAccessToken = accessToken;
        }

        public async Task<string> GetFileContentsAsync(string filePath, string repoUri, string branch)
        {
            Console.WriteLine($"Getting the contents of file '{filePath}' from repo '{repoUri}' in branch '{branch}'...");

            using (HttpClient client = new HttpClient())
            {
                string repo = GetRepoOnly(repoUri);
                client.BaseAddress = new Uri(GitHubApiUri);
                client.DefaultRequestHeaders.Add("Authorization", $"Token {personalAccessToken}");
                client.DefaultRequestHeaders.Add("User-Agent", "DarcLib");

                HttpResponseMessage response = await client.GetAsync($"repos/{repo}contents/{filePath}?ref={branch}");

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Getting the contents of file '{filePath}' from repo '{repoUri}' in branch '{branch}' failed with code '{response.StatusCode}'");
                    return null;
                }

                Console.WriteLine($"Getting the contents of file '{filePath}' from repo '{repoUri}' in branch '{branch}' succeeded!");

                return GetDecodedContent(await response.Content.ReadAsStringAsync());
            }
        }

        private string GetRepoOnly(string repoUri)
        {
            return repoUri.Replace("https://github.com/", string.Empty);
        }

        private string GetDecodedContent(string encodedContent)
        {
            JsonSerializerSettings serializerSettings = new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            };

            FileContent fileContent = JsonConvert.DeserializeObject<FileContent>(encodedContent, serializerSettings);

            byte[] content = Convert.FromBase64String(fileContent.Content);
            return Encoding.UTF8.GetString(content);
        }
    }
}
