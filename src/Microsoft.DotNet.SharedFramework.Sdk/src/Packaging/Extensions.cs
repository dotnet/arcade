// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using NuGet;
using NuGet.Frameworks;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.Versioning;

// This file implements a subset of the Extensions class in the
// Microsoft.DotNet.Build.Tasks.Packaging project to support
// the Shared Framework SDK's usage of the VerifyClosure and
// VerifyTypes tasks used in shared framework validation.

namespace Microsoft.DotNet.Build.Tasks.Packaging
{
    public static class Extensions
    {
        public static IEnumerable<T> NullAsEmpty<T>(this IEnumerable<T> source)
        {
            if (source == null)
            {
                return Enumerable.Empty<T>();
            }

            return source;
        }
    }
}

