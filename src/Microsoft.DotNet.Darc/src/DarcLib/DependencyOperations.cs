// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Microsoft.DotNet.DarcLib
{
    public static class DependencyOperations
    {
        public static readonly Dictionary<string, KnownDependencyType> _knownAssetNames =
            new Dictionary<string, KnownDependencyType>
            {
                {"Microsoft.DotNet.Arcade.Sdk", KnownDependencyType.GlobalJson},
                {"Microsoft.DotNet.Helix.Sdk", KnownDependencyType.GlobalJson},
                {"dotnet", KnownDependencyType.GlobalJson}
            };

        private static readonly Dictionary<KnownDependencyType, Delegate> _dependenciesToFuncMapping =
            new Dictionary<KnownDependencyType, Delegate>
            {
                {
                    KnownDependencyType.GlobalJson,
                    new Func<GitFileManager, string, string, DependencyDetail, Task>(UpdateGlobalJson)
                }
            };

        public static bool TryGetKnownUpdater(string name, out Delegate function)
        {
            if (_knownAssetNames.ContainsKey(name))
            {
                function = _dependenciesToFuncMapping[_knownAssetNames[name]];
                return true;
            }

            function = null;
            return false;
        }

        public static async Task UpdateGlobalJson(
            GitFileManager fileManager,
            string repository,
            string branch,
            DependencyDetail dependency)
        {
            var dependencyMapping = new Dictionary<string, string>
            {
                {"Microsoft.DotNet.Arcade.Sdk", "msbuild-sdks"},
                {"Microsoft.DotNet.Helix.Sdk", "msbuild-sdks"},
                {"dotnet", "tools"}
            };

            if (!dependencyMapping.ContainsKey(dependency.Name))
            {
                throw new Exception($"Dependency '{dependency.Name}' has no parent mapping defined.");
            }

            string parent = dependencyMapping[dependency.Name];

            await fileManager.AddDependencyToGlobalJson(
                repository,
                branch,
                parent,
                dependency.Name,
                dependency.Version);
            await fileManager.AddDependencyToVersionDetailsAsync(
                repository,
                branch,
                dependency,
                DependencyType.Toolset);
        }
    }
}
