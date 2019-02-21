using System.Runtime.InteropServices;

namespace Microsoft.DotNet.SignTool
{
    internal class PEInfo
    {
        internal bool IsManaged { get; set; } = false;
        internal bool IsCrossgened { get; set; } = false;
        internal string Copyright { get; set; } = string.Empty;
        internal string PublicKeyToken { get; set; } = string.Empty;
        internal string TargetFramework { get; set; } = string.Empty;
    }
}
