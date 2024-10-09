// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.DotNet.XHarness.Common;

namespace Microsoft.DotNet.XHarness.TestRunners.NUnit;

internal static class XmlResultJargonExtensions
{
    public static OutputWriter GetWriter(this XmlResultJargon jargon) => jargon switch
    {
        XmlResultJargon.NUnitV2 => throw new NotImplementedException(),
        XmlResultJargon.NUnitV3 => new NUnit3XmlOutputWriter(DateTime.UtcNow),
        XmlResultJargon.xUnit => throw new NotImplementedException(),
        _ => throw new InvalidOperationException($"Jargon {jargon} is not supported by this runner.")
    };
}
