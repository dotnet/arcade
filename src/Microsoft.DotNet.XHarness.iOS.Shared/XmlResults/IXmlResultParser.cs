// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;

#nullable enable
namespace Microsoft.DotNet.XHarness.iOS.Shared.XmlResults;

public interface IXmlResultParser
{
    (string resultLine, bool failed) ParseXml(TextReader stream, TextWriter? humanReadableOutput);
}
