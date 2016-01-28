// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Xunit.Sdk;

namespace Xunit
{
    /// <summary>
    /// Apply this attribute to your test method to specify an active issue.
    /// </summary>
    [TraitDiscoverer("Xunit.NetCore.Extensions.ActiveIssueDiscoverer", "Xunit.NetCore.Extensions")]
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class ActiveIssueAttribute : Attribute, ITraitAttribute
    {
        public ActiveIssueAttribute(int issueNumber, PlatformID platforms = PlatformID.Any) { }
        public ActiveIssueAttribute(string issue, PlatformID platforms = PlatformID.Any) { }
    }
}
