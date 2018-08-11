using System.Threading.Tasks;
using Microsoft.ServiceFabric.Actors;

namespace Maestro.Contracts
{
    public interface ISubscriptionActor : IActor
    {
        Task SynchronizeInProgressPRAsync();
        Task UpdateAsync(int buildId);
    }
}
