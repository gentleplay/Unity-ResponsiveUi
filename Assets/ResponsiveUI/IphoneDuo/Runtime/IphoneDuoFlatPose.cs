namespace FDPlus.IphoneDuo
{
    /// <summary>
    /// The screen a <see cref="IphoneDuoFlatRotation.Locked"/> Flat keeps (<see cref="IphoneDuoRules"/>).
    /// </summary>
    public enum IphoneDuoFlatPose
    {
        /// <summary>
        /// From the Player lock: a landscape-only game is wide, a portrait-only game tall; with both
        /// axes allowed, the hold Flat was entered with.
        /// </summary>
        Auto = 0,
        /// <summary>The wide hold (Pad · Landscape).</summary>
        Wide = 1,
        /// <summary>The tall hold (Pad · Portrait).</summary>
        Tall = 2,
    }
}
