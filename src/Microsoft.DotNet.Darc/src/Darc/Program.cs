using System.Collections.Generic;
using Microsoft.DotNet.Darc;

/*
 Placeholder class used for testing purposes during prototyping. Darc CLI will be implemented here.
*/

namespace Darc
{
    class Program
    {
        static void Main(string[] args)
        {
            DependencyItem dependencyItem = Remote.GetLatestDependencyAsync("arcade.*").Result;
            IEnumerable<DependencyItem> dependantItems = Remote.GetDependantAssetsAsync("Dependency*", type: DependencyType.Product).Result;
            IEnumerable<DependencyItem> dependencyItems = Remote.GetDependencyAssetsAsync("*.sd*").Result;
        }
    }
}
