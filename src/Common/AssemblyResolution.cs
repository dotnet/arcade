// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#if NET461

using System;
using System.IO;
using System.Reflection;

namespace Microsoft.DotNet
{
    internal static class AssemblyResolution
    {
        public static void Initialize()
        {
            AppDomain.CurrentDomain.AssemblyResolve += AssemblyResolve;
        }

        private static Assembly AssemblyResolve(object sender, ResolveEventArgs args)
        {
            var name = new AssemblyName(args.Name);

            if (!name.Name.Equals("System.Collections.Immutable", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "System.Collections.Immutable.dll");

            Assembly sci;
            try
            {
                sci = Assembly.LoadFile(fullPath);
            }
            catch
            {
                return null;
            }

            if (name.Version <= sci.GetName().Version)
            {
                return sci;
            }

            return null;
        }
    }
}

#endif
