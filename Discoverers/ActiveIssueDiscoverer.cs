// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Xunit.NetCore.Extensions
{
    /// <summary>
    /// This class discovers all of the tests and test classes that have
    /// applied the ActiveIssue attribute
    /// </summary>
    public class ActiveIssueDiscoverer : ITraitDiscoverer
    {
        /// <summary>
        /// Gets the trait values from the Category attribute.
        /// </summary>
        /// <param name="traitAttribute">The trait attribute containing the trait values.</param>
        /// <returns>The trait values.</returns>
        public IEnumerable<KeyValuePair<string, string>> GetTraits(IAttributeInfo traitAttribute)
        {
            IEnumerable<object> ctorArgs = traitAttribute.GetConstructorArguments();
            Debug.Assert(ctorArgs.Count() >= 2);

            string issue = ctorArgs.First().ToString();
            PlatformID platforms = (PlatformID)ctorArgs.Last();
            if ((platforms.HasFlag(PlatformID.FreeBSD) && RuntimeInformation.IsOSPlatform(OSPlatform.Create("FREEBSD"))) ||
                (platforms.HasFlag(PlatformID.Linux) && RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) ||
                (platforms.HasFlag(PlatformID.NetBSD) && RuntimeInformation.IsOSPlatform(OSPlatform.Create("NETBSD"))) ||
                (platforms.HasFlag(PlatformID.OSX) && RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) ||
                (platforms.HasFlag(PlatformID.Windows) && RuntimeInformation.IsOSPlatform(OSPlatform.Windows)))
            {
                yield return new KeyValuePair<string, string>(XunitConstants.Category, XunitConstants.Failing);
                yield return new KeyValuePair<string, string>(XunitConstants.ActiveIssue, issue);
            }

        }
    }
}
