namespace FDPlus.Foldable
{
    /// <summary>Raw Jetpack WindowManager fold state.</summary>
    public enum FoldableState
    {
        Undefined = -1,
        Flat = 0,
        HalfOpened = 1
    }

    /// <summary>Hinge orientation from WindowManager.</summary>
    public enum FoldableOrientation
    {
        Undefined = -1,
        Horizontal = 0,
        Vertical = 1
    }

    /// <summary>Hinge occlusion from WindowManager.</summary>
    public enum FoldableOcclusionType
    {
        Undefined = -1,
        None = 0,
        Full = 1
    }

    /// <summary>
    /// Low-level layout posture from <see cref="FoldableLayoutState.ResolvePosture"/>.
    /// Prefer <see cref="FoldableProductState"/> for Fold/Flip product UX.
    /// </summary>
    public enum FoldablePosture
    {
        Flat,
        Tabletop,
        Book,
        DualPane
    }
}
