// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Xunit.Sdk;

namespace Xunit
{
    /// <summary>
    /// Apply this attribute to your test method to specify that it should be ignored by the CI testing system
    /// </summary>
    [TraitDiscoverer("Xunit.NetCore.Extensions.IgnoreForCIDiscoverer", "Xunit.NetCore.Extensions")]
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
    public class IgnoreForCIAttribute : Attribute, ITraitAttribute
    {
        public IgnoreForCIAttribute() { }
    }
}
