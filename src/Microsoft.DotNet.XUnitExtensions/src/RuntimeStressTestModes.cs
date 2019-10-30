using System;

namespace Xunit
{
    [Flags]
    public enum RuntimeStressTestModes
    {
        JitStress = 1,
        JitStressRegs = 1 << 1,
        JitMinOpts = 1 << 2,
        TailcallStress = 1 << 3,
        ZapDisable = 1 << 4
    }
}
