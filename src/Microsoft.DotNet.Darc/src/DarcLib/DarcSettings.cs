namespace Microsoft.DotNet.DarcLib
{
    public class DarcSettings
    {
        public string BuildAssetRegistryPassword { get; set; }

        public string PersonalAccessToken { get; set; }

        public string BuildAssetRegistryBaseUri { get; set; }

        public GitRepoType GitType { get; set; }
    }
}
