using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.DotNet.Darc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Maestro
{
    public class Maestro
    {
        private readonly IRemote _darcRemote;
        private readonly IOptions<MaestroOptions> _options;
        private readonly ILogger _logger;

        public Maestro(ILogger logger, IOptions<MaestroOptions> options, IRemote darcRemote)
        {
            _options = options;
            _darcRemote = darcRemote;
            _logger = logger;
        }

        public async Task CheckAllReposAsync()
        {
            using (_logger.BeginScope("Checking All Repositories"))
            {
                IReadOnlyList<Repository> repositories = await _options.Value.GetRepositoriesAsync();
                foreach (Repository repository in repositories)
                {
                    await CheckRepositoryAsync(repository);
                }
            }
        }

        private async Task CheckRepositoryAsync(Repository repository)
        {
            _logger.LogInformation("Checking Repository '{repository}' branch '{branc}' for updates.", repository.Uri, repository.Branch);
            List<DependencyItem> updates = (await _darcRemote.GetRequiredUpdatesAsync(repository.Uri, repository.Branch)).ToList();

            if (updates.Any())
            {
                string prUrl = await _darcRemote.CreatePullRequestAsync(updates, repository.Uri, repository.Branch);
                _logger.LogInformation("Pull Request {url} created.", prUrl);
            }
        }
    }
}
