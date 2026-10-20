namespace FDPlus.IphoneDuo
{
    /// <summary>Physical shape of the display in use, as the player holds it.</summary>
    public enum IphoneDuoHold
    {
        /// <summary>Face up / down or not reported yet.</summary>
        Unknown = 0,
        /// <summary>Wider than tall (the book hold when Seated).</summary>
        Wide = 1,
        /// <summary>Taller than wide (the laptop hold when Seated).</summary>
        Tall = 2,
    }
}
