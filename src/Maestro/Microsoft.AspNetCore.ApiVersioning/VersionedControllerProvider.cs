// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Microsoft.AspNetCore.ApiVersioning
{
    [PublicAPI]
    public class VersionedControllerProvider
    {
        public VersionedControllerProvider(
            ApplicationPartManager manager,
            ILogger<VersionedControllerProvider> logger,
            IOptions<ApiVersioningOptions> optionsAccessor)
        {
            Options = optionsAccessor.Value;
            if (Options.GetVersion == null)
            {
                throw new InvalidOperationException("ApiVersioningOptions.GetVersion must be set.");
            }

            Logger = logger;
            var controllerFeature = new ControllerFeature();
            manager.PopulateFeature(controllerFeature);
            Versions = GetVersions(controllerFeature.Controllers);
        }

        private ILogger<VersionedControllerProvider> Logger { get; }

        public ApiVersioningOptions Options { get; }

        public IReadOnlyDictionary<string, IReadOnlyDictionary<string, TypeInfo>> Versions { get; }

        private IReadOnlyDictionary<string, IReadOnlyDictionary<string, TypeInfo>> GetVersions(
            IList<TypeInfo> controllerTypes)
        {
            List<string> versionList = controllerTypes.Select(Options.GetVersion)
                .Where(v => v != null)
                .Distinct()
                .OrderBy(v => v)
                .ToList();
            var versions =
                new Dictionary<string, IReadOnlyDictionary<string, TypeInfo>>(StringComparer.OrdinalIgnoreCase);
            foreach (string version in versionList)
            {
                Dictionary<string, TypeInfo> controllers = GetControllersForVersion(version, controllerTypes);
                versions[version] = controllers;
            }

            // Move controllers forward to latest versions if they aren't overridden
            var currentControllers = new Dictionary<string, TypeInfo>(StringComparer.OrdinalIgnoreCase);
            foreach (string version in versions.Keys.OrderBy(n => n))
            {
                var currentVersion = (Dictionary<string, TypeInfo>) versions[version];
                List<string> names = currentControllers.Keys.Concat(versions[version].Keys).Distinct().ToList();
                foreach (string name in names)
                {
                    if (currentVersion.ContainsKey(name))
                    {
                        currentControllers[name] = currentVersion[name];
                    }
                    else if (currentControllers.ContainsKey(name) && !currentVersion.ContainsKey(name))
                    {
                        currentVersion[name] = currentControllers[name];
                    }
                }
            }

            return versions;
        }

        private Dictionary<string, TypeInfo> GetControllersForVersion(
            string version,
            IEnumerable<TypeInfo> apiControllerTypes)
        {
            var controllers = new Dictionary<string, TypeInfo>(StringComparer.OrdinalIgnoreCase);
            // Find controllers in this version
            foreach (TypeInfo controllerType in apiControllerTypes.Where(t => Options.GetVersion(t) == version))
            {
                string name = Options.GetName(controllerType);
                if (controllers.ContainsKey(name))
                {
                    Logger.LogWarning(
                        $"Skipped duplicate controller: '{controllerType.FullName}' because it has the same name as '{controllers[name].FullName}'");
                    continue;
                }

                controllers[name] = controllerType;
            }

            return controllers;
        }
    }
}
