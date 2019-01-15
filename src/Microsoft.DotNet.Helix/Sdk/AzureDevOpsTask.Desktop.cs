using Microsoft.DotNet.Build.Common.Desktop;

namespace Microsoft.DotNet.Helix.AzureDevOps
{
    public partial class AzureDevOpsTask
    {
        static AzureDevOpsTask()
        {
            AssemblyResolver.Enable();
        }
    }
}
