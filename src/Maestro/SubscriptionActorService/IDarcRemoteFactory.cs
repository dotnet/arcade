using System.Threading.Tasks;
using Microsoft.DotNet.DarcLib;

namespace DependencyUpdater
{
    public interface IDarcRemoteFactory
    {
        Task<IRemote> CreateAsync(string repoUrl, long installationId);
    }
}