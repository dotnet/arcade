using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.DotNet.ServiceFabric.ServiceHost
{
    public interface IServiceImplementation
    {
        Task RunAsync(CancellationToken cancellationToken);
    }
}