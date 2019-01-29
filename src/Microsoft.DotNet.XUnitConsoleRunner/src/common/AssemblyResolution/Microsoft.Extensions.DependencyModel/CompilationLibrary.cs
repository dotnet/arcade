// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Internal.Microsoft.Extensions.DependencyModel.Resolution;

namespace Internal.Microsoft.Extensions.DependencyModel
{
    internal class CompilationLibrary : Library
    {
        public CompilationLibrary(string type,
            string name,
            string version,
            string hash,
            IEnumerable<string> assemblies,
            IEnumerable<Dependency> dependencies,
            bool serviceable)
            : this(type, name, version, hash, assemblies, dependencies, serviceable, path: null, hashPath: null)
        {
        }

        public CompilationLibrary(string type,
            string name,
            string version,
            string hash,
            IEnumerable<string> assemblies,
            IEnumerable<Dependency> dependencies,
            bool serviceable,
            string path,
            string hashPath)
            : base(type, name, version, hash, dependencies, serviceable, path, hashPath)
        {
            if (assemblies == null)
            {
                throw new ArgumentNullException(nameof(assemblies));
            }
            Assemblies = assemblies.ToArray();
        }

        public IReadOnlyList<string> Assemblies { get; }

        internal static ICompilationAssemblyResolver DefaultResolver { get; } = new CompositeCompilationAssemblyResolver(new ICompilationAssemblyResolver[]
        {
            new AppBaseCompilationAssemblyResolver(),
            new ReferenceAssemblyPathResolver(),
            new PackageCompilationAssemblyResolver()
        });

        private IEnumerable<string> ResolveReferencePaths(ICompilationAssemblyResolver resolver, List<string> assemblies)
        {
            if (!resolver.TryResolveAssemblyPaths(this, assemblies))
            {
                throw new InvalidOperationException($"Cannot find compilation library location for package '{Name}'");
            }
            return assemblies;
        }
    }
}
