namespace FDPlus.IphoneDuo
{
    /// <summary>
    /// What a one-axis game (Player lock: landscape only or portrait only) does when the display is
    /// held across that lock, e.g. a landscape game held tall (<see cref="IphoneDuoRules"/>).
    /// </summary>
    public enum IphoneDuoAcrossHold
    {
        /// <summary>
        /// Keep the game's orientation and fill the display turned a quarter (Android Fold style).
        /// On the cover this is what iOS already shows.
        /// </summary>
        Sideways = 0,
        /// <summary>Rotate the game to the hold (it needs layouts for that screen shape).</summary>
        Rotate = 1,
        /// <summary>
        /// iOS default: the Player lock stays. The inner display letterboxes the game; the cover shows
        /// it sideways.
        /// </summary>
        SystemDefault = 2,
    }
}
