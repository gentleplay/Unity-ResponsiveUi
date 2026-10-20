using System;
using UnityEngine;

namespace FDPlus.ResponsiveUI
{
    /// <summary>
    /// FancyScrollView Cell Interval / Scroll Offset for one form-factor × orientation slot.
    /// </summary>
    [Serializable]
    public struct ResponsiveScrollState
    {
        #region Constants

        public const float MinCellInterval = 1e-2f;
        public const float DefaultCellInterval = 0.2f;
        public const float DefaultScrollOffset = 0.5f;

        #endregion

        #region Serialized

        [Tooltip("When off, Apply ignores this state and the slot resolver falls back.")]
        public bool enabled;

        [Tooltip("Space between cells (FancyScrollView Cell Interval). Unset (0) applies as 0.2.")]
        [Range(MinCellInterval, 1f)]
        public float cellInterval;

        [Tooltip("Scroll position reference (0.5 places the first cell at center when position is 0).")]
        [Range(0f, 1f)]
        public float scrollOffset;

        #endregion

        #region Factory

        public static ResponsiveScrollState CreateDefault(bool enabled = true) =>
            new ResponsiveScrollState
            {
                enabled = enabled,
                cellInterval = DefaultCellInterval,
                scrollOffset = DefaultScrollOffset,
            };

        public static ResponsiveScrollState From(float cellInterval, float scrollOffset, bool enabled = true) =>
            new ResponsiveScrollState
            {
                enabled = enabled,
                cellInterval = ClampCellInterval(cellInterval),
                scrollOffset = Mathf.Clamp01(scrollOffset),
            };

        #endregion

        #region Apply

        public void Apply(ref float cellInterval, ref float scrollOffset)
        {
            if (!enabled)
                return;

            cellInterval = ClampCellInterval(this.cellInterval);
            scrollOffset = Mathf.Clamp01(this.scrollOffset);
        }

        /// <summary>Unset YAML (0) applies as <see cref="DefaultCellInterval"/>, not <see cref="MinCellInterval"/>.</summary>
        public static float ClampCellInterval(float value) =>
            value <= 0f
                ? DefaultCellInterval
                : Mathf.Clamp(value, MinCellInterval, 1f);

        #endregion
    }

}
