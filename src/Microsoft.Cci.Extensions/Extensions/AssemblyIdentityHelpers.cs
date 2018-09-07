// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Linq;
using Microsoft.Cci;

namespace Microsoft.Cci.Extensions
{
    public static class AssemblyIdentityHelpers
    {
        public static string Format(this AssemblyIdentity assemblyIdentity)
        {
            var name = new System.Reflection.AssemblyName();
            name.Name = assemblyIdentity.Name.Value;
#if !COREFX
            name.CultureInfo = new CultureInfo(assemblyIdentity.Culture);
#endif
            name.Version = assemblyIdentity.Version;
            name.SetPublicKeyToken(assemblyIdentity.PublicKeyToken.ToArray());
#if !COREFX
            name.CodeBase = assemblyIdentity.Location;
#endif
            return name.ToString();
        }

        public static AssemblyIdentity Parse(INameTable nameTable, string formattedName)
        {
            var name = new System.Reflection.AssemblyName(formattedName);
            return new AssemblyIdentity(nameTable.GetNameFor(name.Name),
                                        name.CultureName,
                                        name.Version,
                                        name.GetPublicKeyToken(),
#if COREFX
                                        "");
#else
                                        name.CodeBase);
#endif
        }
    }
}
