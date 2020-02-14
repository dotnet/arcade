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
    [TraitDiscoverer("Microsoft.DotNet.XUnitExtensions.ActiveIssueDiscoverer", "Microsoft.DotNet.XUnitExtensions")]
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
    public class ActiveIssueAttribute : Attribute, ITraitAttribute
    {
        public ActiveIssueAttribute(string issue, TestPlatforms platforms) { }
        public ActiveIssueAttribute(string issue, TargetFrameworkMonikers framework) { }
        public ActiveIssueAttribute(string issue, TestRuntimes runtimes) { }
        public ActiveIssueAttribute(string issue, TestPlatforms platforms = TestPlatforms.Any, TargetFrameworkMonikers framework = (TargetFrameworkMonikers)0, TestRuntimes runtimes = TestRuntimes.Any) { }
    }
}
