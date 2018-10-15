using System;

namespace SignCheck
{
    [Flags]
    public enum FileStatus
    {
        NoFiles = 0,
        UnsignedFiles = 0x01,
        SignedFiles = 0x02,
        SkippedFiles = 0x04,
        ExcludedFiles = 0x08,
        AllFiles = UnsignedFiles | SignedFiles | SkippedFiles | ExcludedFiles
    }
}
