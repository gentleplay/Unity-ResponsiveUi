namespace FDPlus.IphoneDuo
{
    /// <summary>
    /// Whether turning the device (or the Device Simulator chrome) while Flat changes the screen
    /// between wide (Pad · Landscape) and tall (Pad · Portrait) (<see cref="IphoneDuoRules"/>).
    /// </summary>
    public enum IphoneDuoFlatRotation
    {
        /// <summary>Today's Flat rules: the screen follows the hold (across-lock rule, flip 180°).</summary>
        FollowDevice = 0,
        /// <summary>
        /// Flat keeps one screen, picked by <see cref="IphoneDuoRules.flatLockedPose"/>; turning to the
        /// flipped side of the same hold still flips it upright. Only folding or closing ends it.
        /// </summary>
        Locked = 1,
    }
}
