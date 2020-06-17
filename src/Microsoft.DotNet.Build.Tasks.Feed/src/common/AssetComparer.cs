using Microsoft.DotNet.Maestro.Client.Models;
using System.Collections.Generic;

namespace Microsoft.DotNet.Build.Tasks.Feed
{
    class AssetComparer : IEqualityComparer<Asset>
    {
        public bool Equals(Asset obj1, Asset obj2)
        {
            if (ReferenceEquals(obj1, obj2))
            {
                return true;
            }

            Asset assetA = (Asset)obj1;
            Asset assetB = (Asset)obj2;

            return
                (assetA.Id == assetB.Id) &&
                (assetA.Name == assetB.Name) &&
                (assetA.Version == assetB.Version);
        }

        public int GetHashCode(Asset asset)
        {
            return (asset.Id, asset.Name, asset.Version).GetHashCode();
        }
    }
}
