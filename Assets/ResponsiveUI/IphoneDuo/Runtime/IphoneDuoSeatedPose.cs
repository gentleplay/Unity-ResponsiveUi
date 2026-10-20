namespace FDPlus.IphoneDuo
{
    /// <summary>
    /// The pose a <see cref="IphoneDuoSeatedRotation.Locked"/> Seated keeps (<see cref="IphoneDuoRules"/>).
    /// </summary>
    public enum IphoneDuoSeatedPose
    {
        /// <summary>
        /// From the Player lock: a landscape-only game is Seated Portrait (tall laptop), a portrait-only
        /// game Seated Landscape (wide book); with both axes allowed, the hold Seated was entered with.
        /// </summary>
        Auto = 0,
        /// <summary>The tall laptop hold (horizontal crease).</summary>
        SeatedPortrait = 1,
        /// <summary>The wide book hold (vertical crease).</summary>
        SeatedLandscape = 2,
    }
}
