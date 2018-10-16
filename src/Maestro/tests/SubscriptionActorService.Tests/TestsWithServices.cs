// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using Autofac;

namespace SubscriptionActorService.Tests
{
    public class TestsWithServices : TestsWithMocks
    {
        public TestsWithServices()
        {
            Builder = new ContainerBuilder();
        }

        protected ContainerBuilder Builder { get; }

        protected virtual Task BeforeExecute(IComponentContext context)
        {
            return Task.CompletedTask;
        }

        protected async Task Execute(Func<IComponentContext, Task> run)
        {
            using (IContainer container = Builder.Build())
            using (ILifetimeScope scope = container.BeginLifetimeScope())
            {
                await BeforeExecute(scope);
                await run(scope);
            }
        }
    }
}
