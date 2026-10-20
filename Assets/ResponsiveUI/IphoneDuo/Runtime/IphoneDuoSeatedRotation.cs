namespace FDPlus.IphoneDuo
{
    /// <summary>
    /// Whether turning the device (or the Device Simulator chrome) while Seated switches between
    /// Seated Portrait (tall laptop hold) and Seated Landscape (wide book hold) (<see cref="IphoneDuoRules"/>).
    /// </summary>
    public enum IphoneDuoSeatedRotation
    {
        /// <summary>
        /// Seated keeps one pose, picked by <see cref="IphoneDuoRules.seatedLockedPose"/>; only
        /// flattening or closing the device ends it.
        /// </summary>
        Locked = 0,
        /// <summary>Seated follows the hold: laptop → Seated Portrait, book → Seated Landscape.</summary>
        FollowsHold = 1,
    }
}
