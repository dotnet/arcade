using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.DotNet.Darc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Maestro.Tests
{
    public class CheckAllReposAsync : IDisposable
    {
        public CheckAllReposAsync()
        {
            _options = new Mock<MaestroOptions>();
            _logger = new Mock<ILogger>();
            _darcRemote = new Mock<IRemote>();

            IOptions<MaestroOptions> opts = Options.Create(_options.Object);
            _maestro = new Maestro(_logger.Object, opts, _darcRemote.Object);
        }

        public void Dispose()
        {
            _options.VerifyNoOtherCalls();
            _darcRemote.VerifyNoOtherCalls();
        }

        private readonly Mock<MaestroOptions> _options;
        private readonly Mock<ILogger> _logger;
        private readonly Mock<IRemote> _darcRemote;
        private readonly Maestro _maestro;

        [Fact]
        public async Task DependencyThatDoesntUpdate()
        {
            var repo = new Repository("pizza", "crust");
            _options.Setup(o => o.GetRepositoriesAsync()).ReturnsAsync(new[] {repo});
            _darcRemote.Setup(d => d.GetRequiredUpdatesAsync(repo.Uri, repo.Branch))
                .ReturnsAsync(Array.Empty<DependencyItem>());

            await _maestro.CheckAllReposAsync();
            _options.Verify(o => o.GetRepositoriesAsync());
            _darcRemote.Verify(d => d.GetRequiredUpdatesAsync(repo.Uri, repo.Branch));
        }

        [Fact]
        public async Task DependencyThatUpdates()
        {
            var repo = new Repository("pizza", "crust");
            var dep = new DependencyItem {Name = "cheese", Version = "2.1"};
            _options.Setup(o => o.GetRepositoriesAsync()).ReturnsAsync(new[] {repo});
            _darcRemote.Setup(d => d.GetRequiredUpdatesAsync(repo.Uri, repo.Branch))
                .ReturnsAsync(new[] {dep});
            _darcRemote.Setup(d => d.CreatePullRequestAsync(It.IsAny<IEnumerable<DependencyItem>>(), repo.Uri, repo.Branch, null, null, null))
                .ReturnsAsync("seingwoegnw");

            await _maestro.CheckAllReposAsync();
            _options.Verify(o => o.GetRepositoriesAsync());
            _darcRemote.Verify(d => d.GetRequiredUpdatesAsync(repo.Uri, repo.Branch));
            _darcRemote.Verify(
                d => d.CreatePullRequestAsync(
                    It.Is<IEnumerable<DependencyItem>>(
                        deps => deps.Count() == 1 && deps.First().Name == dep.Name &&
                                deps.First().Version == dep.Version),
                    repo.Uri,
                    repo.Branch,
                    null,
                    null,
                    null));
        }

        [Fact]
        public async Task NoRepos()
        {
            _options.Setup(o => o.GetRepositoriesAsync()).ReturnsAsync(Array.Empty<Repository>());

            await _maestro.CheckAllReposAsync();
            _options.Verify(o => o.GetRepositoriesAsync());
        }
    }
}
