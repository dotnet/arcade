using Microsoft.DotNet.Build.Common.Desktop;

namespace Microsoft.DotNet.Helix
{
    public partial class BaseTask
    {
        static BaseTask()
        {
            AssemblyResolver.Enable();
        }
    }
}
