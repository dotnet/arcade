// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Frameworks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace Microsoft.DotNet.Build.Tasks.Packaging
{
    public class Generations
    {
        public static Version DetermineGenerationForFramework(NuGetFramework framework, bool useNetPlatform)
        {
            FrameworkExpander expander = new FrameworkExpander();
            var generationFramework = useNetPlatform ? FrameworkConstants.FrameworkIdentifiers.NetPlatform : FrameworkConstants.FrameworkIdentifiers.NetStandard;
            var generationFxs = expander.Expand(framework).Where(fx => fx.Framework == generationFramework).Select(fx => fx.Version);

            return generationFxs.Max();
        }
    }
}
