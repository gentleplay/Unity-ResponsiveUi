using System;

namespace FDPlus.IphoneDuo
{
    /// <summary>Screen autorotation flags (<c>Screen.autorotateTo*</c>).</summary>
    [Serializable]
    public struct IphoneDuoAutorotateFlags : IEquatable<IphoneDuoAutorotateFlags>
    {
        public bool portrait;
        public bool portraitUpsideDown;
        public bool landscapeLeft;
        public bool landscapeRight;

        public IphoneDuoAutorotateFlags(bool portrait, bool portraitUpsideDown, bool landscapeLeft, bool landscapeRight)
        {
            this.portrait = portrait;
            this.portraitUpsideDown = portraitUpsideDown;
            this.landscapeLeft = landscapeLeft;
            this.landscapeRight = landscapeRight;
        }

        public bool AllowsPortraitAxis => portrait || portraitUpsideDown;
        public bool AllowsLandscapeAxis => landscapeLeft || landscapeRight;

        /// <summary>True when exactly one axis is allowed (a Player orientation lock).</summary>
        public bool IsOneAxis => AllowsPortraitAxis != AllowsLandscapeAxis;

        /// <summary>True when both allow at least one orientation of the same axis.</summary>
        public bool SharesAxis(IphoneDuoAutorotateFlags other) =>
            (AllowsPortraitAxis && other.AllowsPortraitAxis) || (AllowsLandscapeAxis && other.AllowsLandscapeAxis);

        public bool Equals(IphoneDuoAutorotateFlags other) =>
            portrait == other.portrait && portraitUpsideDown == other.portraitUpsideDown &&
            landscapeLeft == other.landscapeLeft && landscapeRight == other.landscapeRight;

        public override bool Equals(object obj) => obj is IphoneDuoAutorotateFlags other && Equals(other);

        public override int GetHashCode() =>
            (portrait ? 1 : 0) | (portraitUpsideDown ? 2 : 0) | (landscapeLeft ? 4 : 0) | (landscapeRight ? 8 : 0);
    }
}
