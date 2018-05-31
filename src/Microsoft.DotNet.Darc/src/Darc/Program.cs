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
            DarcSettings settings = new DarcSettings {
                PersonalAccessToken = "fdsadfdsfsdf",
            };
            DarcLib darc = new DarcLib(settings);
            DependencyItem dependencyItem = darc.RemoteAction.GetLatestDependencyAsync("arcade.*").Result;
            IEnumerable<DependencyItem> dependantItems = darc.RemoteAction.GetDependantAssetsAsync("Dependency*", type: DependencyType.Product).Result;
            IEnumerable<DependencyItem> dependencyItems = darc.RemoteAction.GetDependencyAssetsAsync("*.sd*").Result;
        }
    }
}
