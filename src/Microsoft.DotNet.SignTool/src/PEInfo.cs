using System.Runtime.InteropServices;

namespace Microsoft.DotNet.SignTool
{
    internal class PEInfo
    {
        internal bool isManaged { get; set; } = false;
        internal bool isCrossgened { get; set; } = false;
        internal string copyright { get; set; } = string.Empty;
        internal string publicKeyToken { get; set; } = string.Empty;
        internal string targetFramework { get; set; } = string.Empty;
    }
}
