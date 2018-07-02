using System.Collections.Generic;
using System.Threading.Tasks;

namespace Microsoft.DotNet.Darc
{
    public interface IRemote
    {
        Task<IEnumerable<BuildAsset>> GetDependantAssetsAsync(string assetName, string version = null, string repoUri = null, string branch = null, string sha = null, DependencyType type = DependencyType.Unknown);

        Task<IEnumerable<BuildAsset>> GetDependencyAssetsAsync(string assetName, string version = null, string repoUri = null, string branch = null, string sha = null, DependencyType type = DependencyType.Unknown);

        Task<BuildAsset> GetLatestDependencyAsync(string assetName);

        Task<IEnumerable<BuildAsset>> GetRequiredUpdatesAsync(string repoUri, string branch);

        Task<string> CreatePullRequestAsync(IEnumerable<BuildAsset> itemsToUpdate, string repoUri, string branch, string pullRequestBaseBranch = null, string pullRequestTitle = null, string pullRequestDescription = null);

        Task<string> UpdatePullRequestAsync(IEnumerable<BuildAsset> itemsToUpdate, string repoUri, string branch, int pullRequestId, string pullRequestBaseBranch = null, string pullRequestTitle = null, string pullRequestDescription = null);
    }
}
