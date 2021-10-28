using System.Threading;
using System.Threading.Tasks;


namespace Microsoft.DotNet.Helix.Client
{
    internal interface IBlobHelper
    {
        Task<IBlobContainer> GetContainerAsync(string requestedName, string targetQueue, CancellationToken cancellationToken);
    }
}
