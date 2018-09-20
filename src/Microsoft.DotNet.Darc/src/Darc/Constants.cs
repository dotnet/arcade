// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;

namespace Microsoft.DotNet.Darc
{
    public class Constants
    {
        public const string SettingsFileName = "settings";
        public const int ErrorCode = 42;
        public static string DarcDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".darc");
    }
}
