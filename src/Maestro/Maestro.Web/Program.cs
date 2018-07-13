using Microsoft.DotNet.ServiceFabric.ServiceHost;

namespace Maestro.Web
{
    internal static class Program
    {
        /// <summary>
        /// This is the entry point of the service host process.
        /// </summary>
        private static void Main()
        {
            ServiceHost.Run(
              host => host.RegisterStatelessWebService<Startup>("Maestro.WebType"));
        }
    }
}
