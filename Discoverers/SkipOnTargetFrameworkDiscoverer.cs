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
    /// applied the TestOnTargetFrameworkDiscoverer attribute
    /// </summary>
    public class SkipOnTargetFrameworkDiscoverer : ITraitDiscoverer
    {
        /// <summary>
        /// Gets the trait values from the Category attribute.
        /// </summary>
        /// <param name="traitAttribute">The trait attribute containing the trait values.</param>
        /// <returns>The trait values.</returns>
        public IEnumerable<KeyValuePair<string, string>> GetTraits(IAttributeInfo traitAttribute)
        {
            TargetFrameworkMoniker platform = (TargetFrameworkMoniker)traitAttribute.GetConstructorArguments().First();
            if (platform.HasFlag(TargetFrameworkMoniker.Net45))
                yield return new KeyValuePair<string, string>(XunitConstants.Category, XunitConstants.NonNet45Test);
            if (platform.HasFlag(TargetFrameworkMoniker.Net451))
                yield return new KeyValuePair<string, string>(XunitConstants.Category, XunitConstants.NonNet451Test);
            if (platform.HasFlag(TargetFrameworkMoniker.Net452))
                yield return new KeyValuePair<string, string>(XunitConstants.Category, XunitConstants.NonNet452Test);
            if (platform.HasFlag(TargetFrameworkMoniker.Net46))
                yield return new KeyValuePair<string, string>(XunitConstants.Category, XunitConstants.NonNet46Test);
            if (platform.HasFlag(TargetFrameworkMoniker.Net461))
                yield return new KeyValuePair<string, string>(XunitConstants.Category, XunitConstants.NonNet461Test);
            if (platform.HasFlag(TargetFrameworkMoniker.Net462))
                yield return new KeyValuePair<string, string>(XunitConstants.Category, XunitConstants.NonNet462Test);
            if (platform.HasFlag(TargetFrameworkMoniker.Net463))
                yield return new KeyValuePair<string, string>(XunitConstants.Category, XunitConstants.NonNet463Test);
            if (platform.HasFlag(TargetFrameworkMoniker.Netcore50))
                yield return new KeyValuePair<string, string>(XunitConstants.Category, XunitConstants.NonNetcore50Test);
            if (platform.HasFlag(TargetFrameworkMoniker.Netcore50aot))
                yield return new KeyValuePair<string, string>(XunitConstants.Category, XunitConstants.NonNetcore50aotTest);
            if (platform.HasFlag(TargetFrameworkMoniker.Netcoreapp1_0))
                yield return new KeyValuePair<string, string>(XunitConstants.Category, XunitConstants.NonNetcoreapp1_0Test);
            if (platform.HasFlag(TargetFrameworkMoniker.Netcoreapp1_1))
                yield return new KeyValuePair<string, string>(XunitConstants.Category, XunitConstants.NonNetcoreapp1_1Test);
        }
    }
}
