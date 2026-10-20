namespace FDPlus.IphoneDuo
{
    /// <summary>How the game is shown right now (<see cref="IphoneDuoDevice.State"/>).</summary>
    public enum IphoneDuoScreenMode
    {
        /// <summary>The Player orientation settings apply unchanged.</summary>
        Native = 0,
        /// <summary>Only the hold's axis is allowed, so iOS rotates the game to the hold.</summary>
        HoldAxis = 1,
        /// <summary>
        /// The game keeps its orientation and fills the display turned a quarter (Android Fold style):
        /// iOS keeps the interface of the hold's axis and the Unity view is turned (a landscape game held
        /// tall, or a portrait game held wide).
        /// </summary>
        Sideways = 2,
        /// <summary>
        /// Flat: the interface orientation is locked, so turning the device keeps the game as it is
        /// (sideways when held across the Player lock).
        /// </summary>
        Locked = 3,
    }
}
