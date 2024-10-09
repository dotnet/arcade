// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.DotNet.XHarness.CLI.CommandArguments.Wasm;

using OpenQA.Selenium;
using System;

internal class PageLoadStrategyArgument : EnumPageLoadStrategyArgument
{
    private const string HelpMessage =
        $@"Decides how long WebDriver will hold off on completing a navigation method.
        NORMAL (default): Does not block WebDriver at all. Ready state: complete.
        EAGER: DOM access is ready, but other resources like images may still be loading. Ready state: interactive.
        NONE: Does not block WebDriver at all. Ready state: any.";

    public PageLoadStrategyArgument(PageLoadStrategy defaultValue)
        : base("pageLoadStrategy=", HelpMessage, defaultValue)
    {}
}
