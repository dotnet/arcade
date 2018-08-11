using System.Threading.Tasks;

namespace Maestro.GitHub
{
    public interface IGitHubTokenProvider
    {
        Task<string> GetTokenForInstallation(long installationId);
    }
}