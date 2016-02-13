// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Xunit.Sdk;

namespace Xunit.NetCore.Extensions
{
    internal struct XunitConstants
    {
        public const string NonFreeBSDTest = "nonfreebsdtests";
        public const string NonLinuxTest = "nonlinuxtests";
        public const string NonOSXTest = "nonosxtests";
        public const string NonWindowsTest = "nonwindowstests";
        public const string Category = "category";
        public const string Failing = "failing";
        public const string ActiveIssue = "activeissue";
        public const string OuterLoop = "outerloop";
        public const string IgnoreForCI = "ignoreforci";
    }
}
