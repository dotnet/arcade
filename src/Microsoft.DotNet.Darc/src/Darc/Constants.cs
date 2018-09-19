using System;
using System.IO;

namespace Microsoft.DotNet.Darc
{
    public class Constants
    {
        public const string SettingsFileName = "settings";
        public static string DarcDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".darc");
    }
}
