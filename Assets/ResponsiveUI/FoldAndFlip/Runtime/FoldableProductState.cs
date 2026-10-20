namespace FDPlus.Foldable
{
    /// <summary>
    /// Product-facing posture shared by Fold and Flip devices.
    /// Display names differ by <see cref="FoldableDeviceKind"/> — use
    /// <see cref="FoldableProductStateResolver.GetDisplayName"/>.
    /// </summary>
    public enum FoldableProductState
    {
        Undefined = -1,

        /// <summary>Cover/outer screen. Fold → Normal. Flip → Closed.</summary>
        Closed = 0,

        /// <summary>Fully open flat main/inner. Fold → Expand. Flip → Opened.</summary>
        Opened = 1,

        /// <summary>Flex / half-fold on main/inner. Fold → Expand Half Opened. Flip → Half Opened.</summary>
        HalfOpened = 2
    }
}
