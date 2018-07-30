namespace Microsoft.SignCheck.Verification
{
    public static class DetailKeys
    {
        public const string File = "File";
        public const string AuthentiCode = "AuthentiCode";
        public const string StrongName = "StrongName";
        public const string Misc = "Misc";

        public static readonly string[] ResultKeysVerbose = { File, AuthentiCode, StrongName, Misc };
        public static readonly string[] ResultKeysNormal = { File };
    }
}
