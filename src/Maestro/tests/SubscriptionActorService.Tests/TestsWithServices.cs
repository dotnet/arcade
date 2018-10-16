using System;
using System.Threading.Tasks;
using Autofac;

namespace SubscriptionActorService.Tests
{
    public class TestsWithServices : TestsWithMocks
    {
        protected ContainerBuilder Builder { get; }

        public TestsWithServices()
        {
            Builder = new ContainerBuilder();
        }

        protected virtual Task BeforeExecute(IComponentContext context)
        {
            return Task.CompletedTask;
        }

        protected async Task Execute(Func<IComponentContext, Task> run)
        {
            using (var container = Builder.Build())
            using (var scope = container.BeginLifetimeScope())
            {
                await BeforeExecute(scope);
                await run(scope);
            }
        }
    }
}