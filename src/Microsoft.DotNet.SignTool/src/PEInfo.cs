using System.Runtime.InteropServices;

namespace Microsoft.DotNet.SignTool
{
    internal class PEInfo
    {
        internal bool IsManaged { get; }
        internal bool IsCrossgened { get; }
        internal string Copyright { get; }
        internal string PublicKeyToken { get; }
        internal string TargetFramework { get; }

        public PEInfo(bool isManaged, bool isCrossgened = false, string copyright = null, string publicKeyToken = null, string targetFramework = null)
        {
            IsManaged = isManaged;
            IsCrossgened = isCrossgened;
            Copyright = copyright;
            PublicKeyToken = publicKeyToken;
            TargetFramework = targetFramework;
        }
    }
}
