using Microsoft.DotNet.Darc;
using Microsoft.Extensions.DependencyInjection;

namespace Maestro
{
    public static class Startup
    {
        public static void ConfigureServices(IServiceCollection services)
        {
            services.AddOptions();
            services.AddScoped<Maestro>();
            services.AddScoped<IRemote>(provider => new RemoteActions(new DarcSettings
            {
                PersonalAccessToken = "",
            }));
        }
    }
}
