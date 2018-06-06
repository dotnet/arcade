using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Config;
using Microsoft.Extensions.DependencyInjection;

namespace Maestro.Inject
{
    public class InjectConfiguration : IExtensionConfigProvider
    {
        public void Initialize(ExtensionConfigContext context)
        {
            var services = new ServiceCollection();
            Startup.ConfigureServices(services);
            services.AddSingleton(context.Config.LoggerFactory);
            services.AddScoped<LoggerContainer>();
            services.AddTransient(p => p.GetRequiredService<LoggerContainer>().Logger);

            ServiceProvider provider = services.BuildServiceProvider();


            context.AddBindingRule<InjectAttribute>().Bind(new InjectBindingProvider(provider));

            var registry = context.Config.GetService<IExtensionRegistry>();
            var filter = new ScopeCleanupFilter();
            registry.RegisterExtension<IFunctionInvocationFilter>(filter);
            registry.RegisterExtension<IFunctionExceptionFilter>(filter);
        }
    }
}
