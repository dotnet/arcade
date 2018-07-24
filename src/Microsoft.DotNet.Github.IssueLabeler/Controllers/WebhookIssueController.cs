// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.KeyVault;
using Microsoft.Azure.KeyVault.Models;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Serilog;
using System.IO;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Github.IssueLabeler
{
    [Route("api/WebhookIssue")]
    public class WebhookIssueController : Controller
    {
        public static IConfiguration Configuration { get; set; }

        [HttpPost]
        public async void Post([FromBody] string body)
        {    
            dynamic data = JsonConvert.DeserializeObject(body);
            string Action = data?.action;
            dynamic issue = data?.issue;
            dynamic labels = issue?.labels;

            if (Action == "opened" && labels.Count == 0)
            {
                string title = issue?.title;
                int number = issue?.number;
                string body1 = issue?.body;
                Log.Information($"A {number.ToString()} issue with {title} has been opened.");

                Configuration = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json").Build();

                var labeler = new Labeler(Configuration["GitHubRepoOwner"], Configuration["GitHubRepoName"], await GetPasswordAsync(Configuration["GitHubSecretUri"]));

                await labeler.PredictAndApplyLabelAsync(number, title, body1);
                Log.Information("Labeling completed");
            }
            else
            {
                Log.Information("The issue is already opened or it already has a label");
            }
        }

        public async Task<string> GetPasswordAsync(string secretUri)
        {
            var kv = new KeyVaultClient(new KeyVaultClient.AuthenticationCallback(new AzureServiceTokenProvider().KeyVaultTokenCallback));
            SecretBundle bundle = await kv.GetSecretAsync(secretUri);
            return bundle.Value;
        }
    }
}
