using System;
using System.Threading.Tasks;
using Maestro.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.DotNet.DarcLib;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.ServiceFabric.Data;
using Microsoft.ServiceFabric.Data.Collections;
using Moq;
using ServiceFabricMocks;

namespace DependencyUpdater.Tests
{
    public class DependencyUpdaterTests : IDisposable
    {
        private readonly Lazy<BuildAssetRegistryContext> _context;
        protected readonly Mock<IRemote> Darc;
        protected readonly Mock<IHostingEnvironment> Env;
        protected readonly ServiceProvider Provider;
        protected readonly IReliableDictionary<string, InProgressPullRequest> PullRequests;
        protected readonly IReliableDictionary<int, string> PullRequestsBySubscription;
        protected readonly IServiceScope Scope;
        protected readonly MockReliableStateManager StateManager;

        public DependencyUpdaterTests()
        {
            var services = new ServiceCollection();
            StateManager = new MockReliableStateManager();
            Darc = new Mock<IRemote>(MockBehavior.Strict);
            Env = new Mock<IHostingEnvironment>(MockBehavior.Strict);
            services.AddSingleton(Env.Object);
            services.AddSingleton<IReliableStateManager>(StateManager);
            services.AddLogging();
            services.AddDbContext<BuildAssetRegistryContext>(
                options => { options.UseInMemoryDatabase("BuildAssetRegistry"); });
            services.AddSingleton(
                Mock.Of<IDarcRemoteFactory>(
                    f => f.CreateAsync(It.IsAny<string>(), It.IsAny<long>()) == Task.FromResult(Darc.Object)));
            Provider = services.BuildServiceProvider();
            Scope = Provider.CreateScope();

            _context = new Lazy<BuildAssetRegistryContext>(GetContext);

            // Sync over async is fine here because the mock Reliable State manager is not async
            PullRequests = StateManager
                .GetOrAddAsync<IReliableDictionary<string, InProgressPullRequest>>("pullRequests")
                .GetAwaiter()
                .GetResult();
            PullRequestsBySubscription = StateManager
                .GetOrAddAsync<IReliableDictionary<int, string>>("pullRequestsBySubscription")
                .GetAwaiter()
                .GetResult();
        }

        public BuildAssetRegistryContext Context => _context.Value;

        public void Dispose()
        {
            Scope.Dispose();
            Provider.Dispose();
        }

        private BuildAssetRegistryContext GetContext()
        {
            return Scope.ServiceProvider.GetRequiredService<BuildAssetRegistryContext>();
        }
    }
}
