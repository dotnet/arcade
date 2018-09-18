using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Text;

namespace Microsoft.DotNet.Darc.Helpers
{
    public static class SecureFile
    {
        private static readonly string _darcSecureDirectoryPath;

        static SecureFile()
        {
            _darcSecureDirectoryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".darc");
        }

        public static int Create(string fileName, string jsonContent, ILogger logger)
        {
            try
            {
                Directory.CreateDirectory(_darcSecureDirectoryPath);
                byte[] textBytes = Encoding.UTF8.GetBytes(jsonContent);
                string encodedContent = Convert.ToBase64String(textBytes);
                File.WriteAllText(Path.Combine(_darcSecureDirectoryPath, fileName), encodedContent);
                return 0;
            }
            catch (Exception exc)
            {
                logger.LogError(exc.Message);
                return -1;
            }
        }

        public static JToken Read(string fileName)
        {
            string encodedString = File.ReadAllText(Path.Combine(_darcSecureDirectoryPath, fileName));
            byte[] encodedBytes = Convert.FromBase64String(encodedString);
            string decodedString = Encoding.UTF8.GetString(encodedBytes);
            return JToken.Parse(decodedString);
        }
    }
}
