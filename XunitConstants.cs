// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Xunit.Sdk;

namespace Xunit.NetCore.Extensions
{
    public struct XunitConstants
    {
        internal const string NonFreeBSDTest = "nonfreebsdtests";
        internal const string NonLinuxTest = "nonlinuxtests";
        internal const string NonOSXTest = "nonosxtests";
        internal const string NonWindowsTest = "nonwindowstests";
        internal const string Failing = "failing";
        internal const string ActiveIssue = "activeissue";
        internal const string OuterLoop = "outerloop";
        public const string Category = "category";
        public const string IgnoreForCI = "ignoreforci";
    }
}
