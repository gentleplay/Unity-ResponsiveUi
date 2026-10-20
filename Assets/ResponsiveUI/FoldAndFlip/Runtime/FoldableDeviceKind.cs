namespace FDPlus.Foldable
{
    /// <summary>
    /// Hardware form factor. Android WindowManager does not report this — set it on
    /// <see cref="FoldablePostureDetector"/>
    /// or take it from the Device Simulator catalog.
    /// </summary>
    public enum FoldableDeviceKind
    {
        Unknown = 0,
        /// <summary>Book-style foldable (Galaxy Z Fold, Pixel Fold). Outer cover + large inner.</summary>
        FoldablePhone = 1,
        /// <summary>Clamshell flip (Galaxy Z Flip, Razr). Cover + tall main display.</summary>
        FlipablePhone = 2,
        /// <summary>Spanned dual-pane / hinge tablet-phone (Surface Duo class).</summary>
        DualScreen = 3
    }
}
