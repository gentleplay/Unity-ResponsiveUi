using System;

namespace FDPlus.IphoneDuo
{
    /// <summary>Current iPhone Duo posture, hold and screen mode (<see cref="IphoneDuoDevice.State"/>).</summary>
    public readonly struct IphoneDuoState : IEquatable<IphoneDuoState>
    {
        public readonly IphoneDuoPosture posture;
        public readonly IphoneDuoHold hold;
        /// <summary>True when the hold is across a one-axis Player lock (e.g. a landscape game held tall).</summary>
        public readonly bool acrossLock;
        public readonly IphoneDuoScreenMode mode;

        public IphoneDuoState(IphoneDuoPosture posture, IphoneDuoHold hold, bool acrossLock, IphoneDuoScreenMode mode)
        {
            this.posture = posture;
            this.hold = hold;
            this.acrossLock = acrossLock;
            this.mode = mode;
        }

        public bool Equals(IphoneDuoState other) =>
            posture == other.posture && hold == other.hold && acrossLock == other.acrossLock && mode == other.mode;

        public override bool Equals(object obj) => obj is IphoneDuoState other && Equals(other);

        public override int GetHashCode() =>
            ((int)posture * 397) ^ ((int)hold * 31) ^ ((int)mode * 7) ^ (acrossLock ? 1 : 0);

        public override string ToString() =>
            $"{posture} · {hold}{(acrossLock ? " (across lock)" : "")} · {mode}";
    }
}
