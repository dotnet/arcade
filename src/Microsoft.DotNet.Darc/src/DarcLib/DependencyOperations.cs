using System.Collections.Generic;

namespace Microsoft.DotNet.DarcLib
{
    public static class DependencyOperations
    {
        private static readonly Dictionary<string, string> _wellKnownDependencies = new Dictionary<string, string>
            {
                { "Microsoft.DotNet.Arcade.Sdk", "msbuild-sdks" },
                { "dotnet", "tools" },
            };

        public static bool TryGetKnownDependency(string name, out string parent)
        {
            if (_wellKnownDependencies.ContainsKey(name))
            {
                parent = _wellKnownDependencies[name];
                return true;
            }

            parent = null;
            return false;
        }
    }
}
