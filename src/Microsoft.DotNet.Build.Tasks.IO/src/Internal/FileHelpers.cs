// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;

namespace Microsoft.DotNet.Build.Tasks.IO.Internal
{
    internal class FileHelpers
    {
        public static string EnsureTrailingSlash(string path)
            => !HasTrailingSlash(path)
                ? path + Path.DirectorySeparatorChar
                : path;

        public static bool HasTrailingSlash(string path)
            => !string.IsNullOrEmpty(path)
                && (path[path.Length - 1] == Path.DirectorySeparatorChar || path[path.Length - 1] == Path.AltDirectorySeparatorChar);
    }
}