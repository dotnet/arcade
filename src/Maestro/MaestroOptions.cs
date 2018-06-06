using System.Collections.Generic;
using System.Threading.Tasks;

namespace Maestro
{
    public class MaestroOptions
    {
        public virtual Task<IReadOnlyList<Repository>> GetRepositoriesAsync()
        {
            //TODO: Read this from configuration and github
            return Task.FromResult<IReadOnlyList<Repository>>(new[]
            {
                new Repository("https://github.com/dotnet/arcade/", "maestro-test"),
                // TODO: For some reason darc requires this to have a trailing slash
                //new Repository("https://github.com/alexperovich/maestro-test/", "master"), 
            });
        }
    }
}
