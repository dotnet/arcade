using Microsoft.DotNet.Build.Common.Desktop;

namespace Microsoft.DotNet.Helix.Sdk
{
    public partial class HelixTask
    {
        static HelixTask()
        {
            AssemblyResolver.Enable();
        }
    }
}
