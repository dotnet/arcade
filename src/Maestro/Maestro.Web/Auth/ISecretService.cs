using Microsoft.ServiceFabric.Services.Remoting;
using System.Threading.Tasks;

namespace Maestro.Web
{
    public interface ISecretService : IService
    {
        Task<string> GetValueAsync(string key);
    }
}
