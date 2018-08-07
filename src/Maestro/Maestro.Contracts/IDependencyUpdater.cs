using System.Threading.Tasks;
using Microsoft.ServiceFabric.Services.Remoting;

namespace Maestro.Contracts
{
    public interface IDependencyUpdater : IService
    {
        Task StartUpdateDependenciesAsync(int buildId, int channelId);
    }
}
