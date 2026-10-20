namespace FDPlus.IphoneDuo
{
    /// <summary>iPhone Duo posture as the game sees it (<see cref="IphoneDuoDevice.State"/>).</summary>
    public enum IphoneDuoPosture
    {
        /// <summary>Not an iPhone Duo, or no posture yet.</summary>
        Unknown = 0,
        /// <summary>Closed: the cover display, which behaves like an iPhone.</summary>
        Cover = 1,
        /// <summary>Inner display partly open: the laptop hold (tall) or the book hold (wide).</summary>
        Seated = 2,
        /// <summary>Inner display fully open.</summary>
        Flat = 3,
    }
}
