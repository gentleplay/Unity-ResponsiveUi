using UnityEngine;

namespace FDPlus.ResponsiveUI.Shared.Editor
{
    /// <summary>
    /// ObjectField helpers for Inspector Target assign / clear.
    /// Unity compares destroyed (Missing) references as null; the serialized
    /// instance ID stays non-zero until the field is cleared to None.
    /// </summary>
    public static class ResponsiveUiObjectReference
    {
        /// <summary>
        /// First assign, or replace a Missing ref — previous value compares as null.
        /// </summary>
        public static bool IsFirstAssign(Object previousValue) =>
            previousValue == null;

        /// <summary>
        /// Cleared to None (including from a Missing / destroyed reference).
        /// </summary>
        public static bool WasClearedToNone(int previousInstanceId, Object nextValue) =>
            nextValue == null && previousInstanceId != 0;
    }
}
