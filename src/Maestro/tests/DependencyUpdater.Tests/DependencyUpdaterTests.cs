// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Maestro.Contracts;
using Maestro.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.ServiceFabric.Actors;
using Microsoft.ServiceFabric.Data;
using Moq;
using ServiceFabricMocks;

namespace DependencyUpdater.Tests
{
    public class DependencyUpdaterTests : IDisposable
    {
        private readonly Lazy<BuildAssetRegistryContext> _context;
        protected readonly Mock<IHostingEnvironment> Env;
        protected readonly ServiceProvider Provider;
        protected readonly IServiceScope Scope;
        protected readonly MockReliableStateManager StateManager;
        protected readonly Mock<ISubscriptionActor> SubscriptionActor;

        public DependencyUpdaterTests()
        {
            var services = new ServiceCollection();
            StateManager = new MockReliableStateManager();
            SubscriptionActor = new Mock<ISubscriptionActor>(MockBehavior.Strict);
            Env = new Mock<IHostingEnvironment>(MockBehavior.Strict);
            services.AddSingleton(Env.Object);
            services.AddSingleton<IReliableStateManager>(StateManager);
            services.AddLogging();
            services.AddDbContext<BuildAssetRegistryContext>(
                options => { options.UseInMemoryDatabase("BuildAssetRegistry"); });
            services.AddSingleton<Func<ActorId, ISubscriptionActor>>(
                id =>
                {
                    ActorId = id;
                    return SubscriptionActor.Object;
                });
            Provider = services.BuildServiceProvider();
            Scope = Provider.CreateScope();

            _context = new Lazy<BuildAssetRegistryContext>(GetContext);
        }

        protected ActorId ActorId { get; private set; }

        public BuildAssetRegistryContext Context => _context.Value;

        public void Dispose()
        {
            Env.VerifyAll();
            SubscriptionActor.VerifyAll();
            Scope.Dispose();
            Provider.Dispose();
        }

        private BuildAssetRegistryContext GetContext()
        {
            return Scope.ServiceProvider.GetRequiredService<BuildAssetRegistryContext>();
        }
    }
}
