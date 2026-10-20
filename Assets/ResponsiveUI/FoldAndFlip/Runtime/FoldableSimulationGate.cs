namespace FDPlus.Foldable
{
    /// <summary>
    /// Editor simulation flags shared by Device Simulator, Play Mode overlay,
    /// and <see cref="FoldablePostureDetector"/>.
    /// Android players ignore this — Jetpack owns layout.
    /// </summary>
    public static class FoldableSimulationGate
    {
        /// <summary>
        /// When false, Editor Play Mode overlay restores full-screen Flat UI.
        /// </summary>
        public static bool AllowSimulatedLayouts { get; set; } = true;

        /// <summary>
        /// Last Fold &amp; Flip panel layout. Play Mode copies this onto the active
        /// posture owner on enable so HUD does not flash Expand.
        /// </summary>
        public static FoldableLayoutState PlayLayout { get; private set; }

        public static bool GetPlayLayout(out FoldableLayoutState layout)
        {
            layout = PlayLayout;
            return layout != null && layout.productState != FoldableProductState.Undefined;
        }

        public static void SetPlayLayout(FoldableLayoutState layout) =>
            PlayLayout = layout;

        public static void ClearPlayLayout() =>
            PlayLayout = null;
    }
}
