using System.Collections.Generic;

namespace Microsoft.DotNet.DarcLib
{
    public static class DependencyOperations
    {
        private static readonly HashSet<string> _wellKnownDependencies = new HashSet<string>
            {
                "Microsoft.DotNet.Arcade.Sdk",
                "dotnet"
            };

        public static bool IsWellKnownDependency(string name)
        {
            return _wellKnownDependencies.Contains(name);
        }
    }
}
