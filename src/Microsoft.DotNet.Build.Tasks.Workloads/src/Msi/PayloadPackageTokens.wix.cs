﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.DotNet.Build.Tasks.Workloads.Msi
{
    /// <summary>
    /// Defines token names used to create an MSI payload package (NuGet).
    /// </summary>
    internal static class PayloadPackageTokens
    {
        public static readonly string __AUTHORS__ = nameof(__AUTHORS__);
        public static readonly string __COPYRIGHT__ = nameof(__COPYRIGHT__);
        public static readonly string __DESCRIPTION__ = nameof(__DESCRIPTION__);
        public static readonly string __LICENSE_FILENAME__ = nameof(__LICENSE_FILENAME__);
        public static readonly string __MSI__ = nameof(__MSI__);
        public static readonly string __MSI_JSON__ = nameof(__MSI_JSON__);
        public static readonly string __PACKAGE_ID__ = nameof(__PACKAGE_ID__);
        public static readonly string __PACKAGE_PROJECT_URL__ = nameof(__PACKAGE_PROJECT_URL__);
        public static readonly string __PACKAGE_VERSION__ = nameof(__PACKAGE_VERSION__);
    }
}
