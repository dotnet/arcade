// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using System.Collections.Generic;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Xunit.NetCore.Extensions
{
    /// <summary>
    /// This class discovers all of the tests and test classes that have
    /// applied the PlatformSpecific attribute
    /// </summary>
    public class PlatformSpecificDiscoverer : ITraitDiscoverer
    {
        /// <summary>
        /// Gets the trait values from the Category attribute.
        /// </summary>
        /// <param name="traitAttribute">The trait attribute containing the trait values.</param>
        /// <returns>The trait values.</returns>
        public IEnumerable<KeyValuePair<string, string>> GetTraits(IAttributeInfo traitAttribute)
        {
            PlatformID platform = (PlatformID)traitAttribute.GetConstructorArguments().First();
            if (!platform.HasFlag(PlatformID.Windows))
                yield return new KeyValuePair<string, string>(XunitConstants.Category, XunitConstants.NonWindowsTest);
            if (!platform.HasFlag(PlatformID.Linux))
                yield return new KeyValuePair<string, string>(XunitConstants.Category, XunitConstants.NonLinuxTest);
            if (!platform.HasFlag(PlatformID.OSX))
                yield return new KeyValuePair<string, string>(XunitConstants.Category, XunitConstants.NonOSXTest);
            if (!platform.HasFlag(PlatformID.FreeBSD))
                yield return new KeyValuePair<string, string>(XunitConstants.Category, XunitConstants.NonFreeBSDTest);
            if (!platform.HasFlag(PlatformID.NetBSD))
                yield return new KeyValuePair<string, string>(XunitConstants.Category, XunitConstants.NonNetBSDTest);
        }
    }
}
