using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Maestro.Data;
using Maestro.Data.Models;
using Maestro.GitHub;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebHooks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Octokit;
using Octokit.Internal;
using Repository = Octokit.Repository;

namespace Maestro.Web.Controllers
{
    public class WebHooksController : Controller
    {
        private readonly Lazy<BuildAssetRegistryContext> _context;
        public ILogger<WebHooksController> Logger { get; }
        public IGitHubTokenProvider GitHubTokenProvider { get; }
        public BuildAssetRegistryContext Context => _context.Value;

        public WebHooksController(ILogger<WebHooksController> logger, IGitHubTokenProvider gitHubTokenProvider, Lazy<BuildAssetRegistryContext> context)
        {
            _context = context;
            Logger = logger;
            GitHubTokenProvider = gitHubTokenProvider;
        }

        public class InstallationEvent
        {
            public string Action { get; set; }
            public Installation Installation { get; set; }
            public List<InstallationRepository> Repositories { get; set; }
            public User Sender { get; set; }
        }

        public class InstallationRepository
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public string FullName { get; set; }
            public bool Private { get; set; }
        }

        [GitHubWebHook(EventName = "installation")]
        [ValidateModelState]
        public async Task<IActionResult> InstallationHandler(JObject data)
        {
            var ser = new SimpleJsonSerializer();
            var payload = ser.Deserialize<InstallationEvent>(data.ToString());
            switch (payload.Action)
            {
                case "deleted":
                    await RemoveInstallationRepositoriesAsync(payload.Installation.Id);
                    break;
                case "created":
                    await SynchronizeInstallationRepositoriesAsync(payload.Installation.Id);
                    break;
                default:
                    Logger.LogError("Received Unknown action '{action}' for installation event. Payload: {payload}", payload.Action, data.ToString());
                    break;
            }
            return Ok();
        }

        private async Task RemoveInstallationRepositoriesAsync(long installationId)
        {
            foreach (var repo in Context.Repositories.Where(r => r.InstallationId == installationId))
            {
                Context.Update(repo);
                repo.InstallationId = 0;
            }
            await Context.SaveChangesAsync();
        }

        public class InstallationRepositoriesEvent
        {
            public string Action { get; set; }
            public Installation Installation { get; set; }
            public StringEnum<InstallationRepositorySelection> RepositorySelection { get; set; }
            public List<InstallationRepository> RepositoriesAdded { get; set; }
            public List<InstallationRepository> RepositoriesRemoved { get; set; }
            public User Sender { get; set; }
        }

        [GitHubWebHook(EventName = "installation_repositories")]
        [ValidateModelState]
        public async Task<IActionResult> InstallationRepositoriesHandler(JObject data)
        {
            var ser = new SimpleJsonSerializer();
            var payload = ser.Deserialize<InstallationRepositoriesEvent>(data.ToString());
            await SynchronizeInstallationRepositoriesAsync(payload.Installation.Id);
            return Ok();
        }

        public class InstallationRepositoriesResponse
        {
            public int TotalCount { get; set; }
            public StringEnum<InstallationRepositorySelection> RepositorySelection { get; set; }
            public List<Repository> Repositories { get; set; }
        }

        private async Task SynchronizeInstallationRepositoriesAsync(long installationId)
        {
            string token = await GitHubTokenProvider.GetTokenForInstallation(installationId);
            IReadOnlyList<Repository> gitHubRepos = await GetAllInstallationRepositories(token);

            List<Data.Models.Repository> toRemove =
                (await Context.Repositories.Where(r => r.InstallationId == installationId).ToListAsync());

            foreach (Repository repo in gitHubRepos)
            {
                Data.Models.Repository existing = await Context.Repositories.FindAsync(repo.HtmlUrl);
                if (existing == null)
                {
                    Context.Repositories.Add(
                        new Data.Models.Repository {RepositoryName = repo.HtmlUrl, InstallationId = installationId});
                }
                else
                {
                    toRemove.Remove(existing);
                    existing.InstallationId = installationId;
                    Context.Update(existing);
                }
            }

            foreach (var repository in toRemove)
            {
                repository.InstallationId = 0;
                Context.Update(repository);
            }

            await Context.SaveChangesAsync();
        }

        private static Task<IReadOnlyList<Repository>> GetAllInstallationRepositories(string token)
        {
            var product = new ProductHeaderValue(
                "Maestro",
                Assembly.GetEntryAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion);
            var client = new GitHubClient(product) {Credentials = new Credentials(token, AuthenticationType.Bearer)};
            var pagination = new ApiPagination();
            var uri = new Uri("installation/repositories", UriKind.Relative);

            async Task<IApiResponse<List<Repository>>> GetInstallationRepositories(Uri u)
            {
                var response =
                    await client.Connection.Get<InstallationRepositoriesResponse>(u, null, AcceptHeaders.GitHubAppsPreview);
                return new ApiResponse<List<Repository>>(response.HttpResponse, response.Body.Repositories);
            }

            return pagination.GetAllPages<Repository>(
                async () => new ReadOnlyPagedCollection<Repository>(
                    await GetInstallationRepositories(uri),
                    GetInstallationRepositories),
                uri);
        }

        [GitHubWebHook]
        [ValidateModelState]
        public IActionResult GitHubHandler(string id, string @event, JObject data)
        {
            Logger.LogWarning("Received unhandled event {eventName}", @event);
            return NoContent();
        }
    }
}
