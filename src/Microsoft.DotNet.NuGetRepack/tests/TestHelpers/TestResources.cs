 // Copyright(c) Microsoft.All Rights Reserved.Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

namespace TestResources
{
    public static class ReleasePackages
    {
        public const string Version = "1.0.0";

        private static byte[] s_A;
        public static byte[] A => ResourceLoader.GetOrCreateResource(ref s_A, NameA);
        public const string NameA = nameof(A) + "." + Version + ".nupkg";

        private static byte[] s_B;
        public static byte[] B => ResourceLoader.GetOrCreateResource(ref s_B, NameB);
        public const string NameB = nameof(B) + "." + Version + ".nupkg";

        private static byte[] s_C;
        public static byte[] C => ResourceLoader.GetOrCreateResource(ref s_C, NameC);
        public const string NameC = nameof(C) + "." + Version + ".nupkg";

        private static byte[] s_D;
        public static byte[] D => ResourceLoader.GetOrCreateResource(ref s_D, NameD);
        public const string NameD = nameof(D) + "." + Version + ".nupkg";

        private static byte[] s_E;
        public static byte[] E => ResourceLoader.GetOrCreateResource(ref s_E, NameE);
        public const string NameE = nameof(E) + "." + Version + ".nupkg";

        private static byte[] s_F;
        public static byte[] F => ResourceLoader.GetOrCreateResource(ref s_F, NameF);
        public const string NameF = nameof(F) + "." + Version + ".nupkg";
    }

    public static class PreReleasePackages
    {
        public const string SemVer1 = "1.0.0-beta-final";
        public const string SemVer2 = "1.0.0-beta.final";

        private static byte[] s_A;
        public static byte[] A => ResourceLoader.GetOrCreateResource(ref s_A, NameA);
        public const string NameA = nameof(A) + "." + SemVer1 + ".nupkg";

        private static byte[] s_B;
        public static byte[] B => ResourceLoader.GetOrCreateResource(ref s_B, NameB);
        public const string NameB = nameof(B) + "." + SemVer1 + ".nupkg";

        private static byte[] s_C;
        public static byte[] C => ResourceLoader.GetOrCreateResource(ref s_C, NameC);
        public const string NameC = nameof(C) + "." + SemVer1 + ".nupkg";

        private static byte[] s_D;
        public static byte[] D => ResourceLoader.GetOrCreateResource(ref s_D, NameD);
        public const string NameD = nameof(D) + "." + SemVer1 + ".nupkg";

        private static byte[] s_E;
        public static byte[] E => ResourceLoader.GetOrCreateResource(ref s_E, NameE);
        public const string NameE = nameof(E) + "." + SemVer2 + ".nupkg";

        private static byte[] s_F;
        public static byte[] F => ResourceLoader.GetOrCreateResource(ref s_F, NameF);
        public const string NameF = nameof(F) + "." + SemVer2 + ".nupkg";
    }

    public static class DailyBuildPackages
    {
        public const string SemVer1 = "1.0.0-beta-12345-01";
        public const string SemVer2 = "1.0.0-beta.12345.1";

        private static byte[] s_A;
        public static byte[] A => ResourceLoader.GetOrCreateResource(ref s_A, NameA);
        public const string NameA = nameof(A) + "." + SemVer1 + ".nupkg";

        private static byte[] s_B;
        public static byte[] B => ResourceLoader.GetOrCreateResource(ref s_B, NameB);
        public const string NameB = nameof(B) + "." + SemVer1 + ".nupkg";

        private static byte[] s_C;
        public static byte[] C => ResourceLoader.GetOrCreateResource(ref s_C, NameC);
        public const string NameC = nameof(C) + "." + SemVer1 + ".nupkg";

        private static byte[] s_D;
        public static byte[] D => ResourceLoader.GetOrCreateResource(ref s_D, NameD);
        public const string NameD = nameof(D) + "." + SemVer1 + ".nupkg";

        private static byte[] s_E;
        public static byte[] E => ResourceLoader.GetOrCreateResource(ref s_E, NameE);
        public const string NameE = nameof(E) + "." + SemVer2 + ".nupkg";

        private static byte[] s_F;
        public static byte[] F => ResourceLoader.GetOrCreateResource(ref s_F, NameF);
        public const string NameF = nameof(F) + "." + SemVer2 + ".nupkg";
    }

    public static class MiscPackages
    {
        private static byte[] s_Signed;
        public static byte[] Signed => ResourceLoader.GetOrCreateResource(ref s_Signed, NameSigned);
        public const string NameSigned = "Signed.1.2.3.nupkg";

        private static byte[] s_DotneTool;
        public static byte[] DotnetTool => ResourceLoader.GetOrCreateResource(ref s_DotneTool, NameDotnetTool);
        public const string NameDotnetTool = "DotnetTool.1.0.0-beta-12345-01.nupkg";
    }
}
