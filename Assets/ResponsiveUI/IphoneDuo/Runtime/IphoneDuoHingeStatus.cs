namespace FDPlus.IphoneDuo
{
    /// <summary>
    /// iPhone Duo hinge status from <c>UIHingeInteraction</c>. Values mirror the
    /// <c>FDPHinge*</c> constants in <c>Plugins/iOS/IphoneDuoHinge.mm</c>.
    /// </summary>
    public enum IphoneDuoHingeStatus
    {
        /// <summary>
        /// Bridge not started / stopped, or no hinge update yet (non-foldable iPhone, older iOS).
        /// A nil hinge later (view left a hinge hierarchy) keeps the last status instead.
        /// </summary>
        None = -1,
        Unknown = 0,
        Closed = 1,
        PartiallyOpen = 2,
        FullyOpen = 3
    }
}
