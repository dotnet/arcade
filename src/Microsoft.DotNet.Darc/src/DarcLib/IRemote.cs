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

        Task<string> UpdateBranchAndRepoAsync(IEnumerable<DependencyItem> itemsToUpdate, string repoUri, string branch, string pullRequestBaseBranch = null, string pullRequestTitle = null, string pullRequestDescription = null);
    }
}
