// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Xml.Linq;

#nullable enable

namespace Microsoft.DotNet.XHarness.TestRunners.Xunit;

internal class WasmXmlResultWriter
{
    public static void WriteOnSingleLine(XElement assembliesElement)
    {
        using var ms = new MemoryStream();
        assembliesElement.Save(ms);
        ms.TryGetBuffer(out var bytes);
        var base64 = Convert.ToBase64String(bytes, Base64FormattingOptions.None);
        Console.WriteLine($"STARTRESULTXML {bytes.Count} {base64} ENDRESULTXML");
        Console.WriteLine($"Finished writing {bytes.Count} bytes of RESULTXML");
    }
}
