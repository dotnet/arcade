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
        ZapDisable = 1 << 4,
        GCStress3 = 1 << 5,
        GCStressC = 1 << 6,
        CheckedRuntime = 1 << 7,
        AnyGCStress = GCStress3 | GCStressC,
        Any = ~0
    }
}
