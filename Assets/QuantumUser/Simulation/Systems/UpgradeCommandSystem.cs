namespace Quantum {
    using UnityEngine.Scripting;

    [Preserve]
    public unsafe class UpgradeSignalSystem : SystemSignalsOnly {
        // This system just handles upgrade signals - no commands needed
        // The actual upgrade logic is in UpgradeSystem
    }
}