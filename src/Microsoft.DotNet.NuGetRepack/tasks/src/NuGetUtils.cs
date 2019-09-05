// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;

namespace Microsoft.DotNet.Tools
{
	internal static class NuGetUtils
	{
        public const string DefaultNuspecXmlns = "http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd";
        public const string SignaturePartUri = ".signature.p7s";

        public static void ParseName(string partName, out string fileName, out string dirName)
        {
            int lastSeparator = partName.LastIndexOf('/');
            fileName = partName.Substring(lastSeparator + 1);
            dirName = (lastSeparator == -1) ? "" : (lastSeparator == 0) ? "/" : partName.Substring(0, lastSeparator);
        }

        public static bool IsNuSpec(string partName)
        {
            ParseName(partName, out var fileName, out var dirName);
            return dirName == "/" && fileName.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase);
        }
    }
}
