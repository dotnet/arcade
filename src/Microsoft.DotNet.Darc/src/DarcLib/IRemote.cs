using System.Collections.Generic;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Darc
{
    public interface IRemote
    {
        Task<IEnumerable<DependencyItem>> GetDependantAssetsAsync(string assetName, string version = null, string repoUri = null, string branch = null, string sha = null, DependencyType type = DependencyType.Unknown);

        Task<IEnumerable<DependencyItem>> GetDependencyAssetsAsync(string assetName, string version = null, string repoUri = null, string branch = null, string sha = null, DependencyType type = DependencyType.Unknown);

        Task<DependencyItem> GetLatestDependencyAsync(string assetName);

        Task<IEnumerable<DependencyItem>> GetRequiredUpdatesAsync(string repoUri, string branch);

        Task<string> CreatePullRequestAsync(IEnumerable<DependencyItem> itemsToUpdate, string repoUri, string branch, string pullRequestBaseBranch = null, string pullRequestTitle = null, string pullRequestDescription = null);

        Task<string> UpdatePullRequestAsync(IEnumerable<DependencyItem> itemsToUpdate, string repoUri, string branch, int pullRequestId, string pullRequestBaseBranch = null, string pullRequestTitle = null, string pullRequestDescription = null);
    }
}
