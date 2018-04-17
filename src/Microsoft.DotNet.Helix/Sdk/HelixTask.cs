using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Microsoft.DotNet.Helix.Client;
using Newtonsoft.Json;
using Task = Microsoft.Build.Utilities.Task;

namespace Microsoft.DotNet.Helix.Sdk
{
    public abstract class HelixTask : Task
    {
        /// <summary>
        /// The Helix Api Base Uri
        /// </summary>
        public Uri BaseUri { get; set; } = new Uri("https://helix.dot.net/");

        /// <summary>
        /// The Helix Api Access Token
        /// </summary>
        public string AccessToken { get; set; }

        [Required]
        public string ConfigFile { get; set; }

        protected HelixConfig GetHelixConfig()
        {
            if (File.Exists(ConfigFile))
            {
                return JsonConvert.DeserializeObject<HelixConfig>(
                    File.ReadAllText(ConfigFile),
                    Constants.SerializerSettings);
            }
            return new HelixConfig();
        }

        protected bool SetHelixConfig(HelixConfig config)
        {
            var dir = Path.GetDirectoryName(ConfigFile);
            if (string.IsNullOrEmpty(dir))
            {
                Log.LogError($"Path '{ConfigFile}' doesn't exist in a directory");
                return false;
            }
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllText(ConfigFile, JsonConvert.SerializeObject(config, Constants.SerializerSettings));
            return true;
        }


        protected IHelixApi HelixApi { get; private set; }

        private IHelixApi GetHelixApi()
        {
            if (string.IsNullOrEmpty(AccessToken))
            {
                return ApiFactory.GetAnonymous(BaseUri.AbsoluteUri);
            }

            return ApiFactory.GetAuthenticated(BaseUri.AbsoluteUri, AccessToken);
        }

        public sealed override bool Execute()
        {
            try
            {
                HelixApi = GetHelixApi();
                return System.Threading.Tasks.Task.Run(ExecuteCore).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                Log.LogErrorFromException(ex, true, true, null);
                return false;
            }
        }

        protected abstract Task<bool> ExecuteCore();
    }
}
