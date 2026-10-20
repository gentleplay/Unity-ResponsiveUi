using System;
using UnityEngine;
using UnityEngine.Events;

namespace FDPlus.ResponsiveUI
{
    /// <summary>
    /// Play Mode UnityEvents for the twenty slots: Phone / Pad / Fold Cover / Fold Half Opened / Fold Cover Half Opened / Flip Cover / Flip Half Opened / iPhone Duo Cover / iPhone Duo Seated / Wide Screen ×
    /// Portrait / Landscape (<see cref="ResponsiveWidgetLayout.StatePropertyNames"/> order).
    /// Apply invokes the exact live slot (no Phone fallback). Empty events are no-ops.
    /// </summary>
    [Serializable]
    public struct ResponsiveLayoutEvents
    {
        #region Serialized

        public UnityEvent phonePortrait;
        public UnityEvent phoneLandscape;
        public UnityEvent padPortrait;
        public UnityEvent padLandscape;
        public UnityEvent foldPhoneCoverPortrait;
        public UnityEvent foldPhoneCoverLandscape;
        public UnityEvent foldHalfOpenedPortrait;
        public UnityEvent foldHalfOpenedLandscape;
        public UnityEvent foldCoverHalfOpenedPortrait;
        public UnityEvent foldCoverHalfOpenedLandscape;
        public UnityEvent flipPhoneCoverPortrait;
        public UnityEvent flipPhoneCoverLandscape;
        public UnityEvent flipHalfOpenedPortrait;
        public UnityEvent flipHalfOpenedLandscape;
        public UnityEvent iPhoneDuoCoverPortrait;
        public UnityEvent iPhoneDuoCoverLandscape;
        public UnityEvent iPhoneDuoSeatedPortrait;
        public UnityEvent iPhoneDuoSeatedLandscape;
        public UnityEvent wideScreenPortrait;
        public UnityEvent wideScreenLandscape;

        #endregion

        #region Invoke

        public void Invoke(in ResponsiveDevice.LayoutKey layout) =>
            Get(ResponsiveWidgetLayout.StatePropertyIndex(layout))?.Invoke();

        public UnityEvent Get(int slotIndex) =>
            slotIndex switch
            {
                0 => phonePortrait,
                1 => phoneLandscape,
                2 => padPortrait,
                3 => padLandscape,
                4 => foldPhoneCoverPortrait,
                5 => foldPhoneCoverLandscape,
                6 => foldHalfOpenedPortrait,
                7 => foldHalfOpenedLandscape,
                8 => foldCoverHalfOpenedPortrait,
                9 => foldCoverHalfOpenedLandscape,
                10 => flipPhoneCoverPortrait,
                11 => flipPhoneCoverLandscape,
                12 => flipHalfOpenedPortrait,
                13 => flipHalfOpenedLandscape,
                14 => iPhoneDuoCoverPortrait,
                15 => iPhoneDuoCoverLandscape,
                16 => iPhoneDuoSeatedPortrait,
                17 => iPhoneDuoSeatedLandscape,
                18 => wideScreenPortrait,
                19 => wideScreenLandscape,
                _ => null,
            };

        #endregion
    }
}
