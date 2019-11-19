using Azure.Core;

namespace Microsoft.DotNet.Helix.Client
{
    partial class HelixApiOptions
    {
        partial void InitializeOptions()
        {
            if (Credentials != null)
            {
                AddPolicy(new HelixApiTokenAuthenticationPolicy(Credentials), HttpPipelinePosition.PerCall);
            }
        }
    }
}
