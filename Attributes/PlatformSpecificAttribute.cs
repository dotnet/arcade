// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Xunit.Sdk;

namespace Xunit
{
    /// <summary>
    /// Apply this attribute to your test method to specify this is a platform specific test.
    /// </summary>
    [TraitDiscoverer("Xunit.NetCore.Extensions.PlatformSpecificDiscoverer", "Xunit.NetCore.Extensions")]
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
    public class PlatformSpecificAttribute : Attribute, ITraitAttribute
    {
        public PlatformSpecificAttribute(PlatformID platform) { }
    }
}
