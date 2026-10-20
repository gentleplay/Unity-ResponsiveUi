using System;
using UnityEngine;

namespace FDPlus.ResponsiveUI
{
    /// <summary>
    /// Per-scroll Cell Interval / Scroll Offset slots: Phone / Pad / Fold Cover / Fold Half Opened /
    /// Fold Cover Half Opened / Flip Cover / Flip Half Opened / iPhone Duo Cover / iPhone Duo Seated /
    /// Wide Screen × Portrait/Landscape. Disabled slots fall back along
    /// <see cref="ResponsiveWidgetSlotResolver"/>.
    /// </summary>
    [Serializable]
    public struct ResponsiveScrollLayout
    {
        #region Serialized

        public static readonly string[] StatePropertyNames = ResponsiveWidgetLayout.StatePropertyNames;

        public ResponsiveScrollState phonePortrait;
        public ResponsiveScrollState phoneLandscape;

        public ResponsiveScrollState padPortrait;
        public ResponsiveScrollState padLandscape;

        [Tooltip("Fold Cover (Fold8 passport ~10:16). Leave disabled to reuse Phone slots.")]
        public ResponsiveScrollState foldPhoneCoverPortrait;
        public ResponsiveScrollState foldPhoneCoverLandscape;

        [Tooltip("Fold Half Opened (Fold7-style). Leave disabled to reuse Phone slots.")]
        public ResponsiveScrollState foldHalfOpenedPortrait;
        public ResponsiveScrollState foldHalfOpenedLandscape;

        [Tooltip("Fold Cover Half Opened (Fold8-style). Leave disabled to reuse Phone slots.")]
        public ResponsiveScrollState foldCoverHalfOpenedPortrait;
        public ResponsiveScrollState foldCoverHalfOpenedLandscape;

        [Tooltip("Flip Cover (Flip Closed). Leave disabled to reuse Phone slots.")]
        public ResponsiveScrollState flipPhoneCoverPortrait;
        public ResponsiveScrollState flipPhoneCoverLandscape;

        [Tooltip("Flip Half Opened. Leave disabled to reuse Phone slots.")]
        public ResponsiveScrollState flipHalfOpenedPortrait;
        public ResponsiveScrollState flipHalfOpenedLandscape;

        [Tooltip("iPhone Duo Cover (closed, outer display). Leave disabled to reuse Phone slots.")]
        public ResponsiveScrollState iPhoneDuoCoverPortrait;
        public ResponsiveScrollState iPhoneDuoCoverLandscape;

        [Tooltip("iPhone Duo Seated. Portrait is the horizontal laptop fold (tall screen). " +
                 "Landscape is the vertical book fold (wide screen). Leave disabled to reuse Pad " +
                 "(same shape, then the other), then Phone the same way.")]
        public ResponsiveScrollState iPhoneDuoSeatedPortrait;
        public ResponsiveScrollState iPhoneDuoSeatedLandscape;

        [Tooltip("Wide Screen (Windows / macOS / Linux / WebGL). Orientation follows the window or " +
                 "browser aspect. Leave disabled to reuse the other Wide Screen orientation " +
                 "(never Pad / Phone).")]
        public ResponsiveScrollState wideScreenPortrait;
        public ResponsiveScrollState wideScreenLandscape;

        #endregion

        #region Seed

        /// <summary>
        /// Fills every form-factor × orientation slot from the host FancyScrollView metrics.
        /// <paramref name="enablePortrait"/> / <paramref name="enableLandscape"/> control Enabled
        /// (Add Component / Reset should pass Player Allowed Orientations) by the screen axis each
        /// slot maps to (<see cref="ResponsiveDevice.SlotAllowedByPlayerAxes"/>): Fold / Fold Cover
        /// Half Opened follow the lock like other slots, iPhone Duo Seated slots always show (Seated
        /// allows only the hold's axis). Wide Screen slots start Enabled on Wide Screen targets only
        /// (<see cref="ResponsivePlatforms.SeedsEnabled"/>).
        /// Flip Cover Landscape retains the metrics but starts disabled.
        /// </summary>
        public void SeedAll(
            float cellInterval,
            float scrollOffset,
            bool enablePortrait,
            bool enableLandscape)
        {
            ResponsiveScrollState allowed = ResponsiveScrollState.From(
                cellInterval, scrollOffset, enabled: true);
            ResponsiveScrollState disabled = ResponsiveScrollState.From(
                cellInterval, scrollOffset, enabled: false);
            for (int i = 0; i < StatePropertyNames.Length; i++)
            {
                bool enable =
                    !ResponsiveWidgetLayout.IsSeedDisabledSlot(i) &&
                    ResponsivePlatforms.SeedsEnabled(ResponsiveWidgetLayout.SlotFormFactor(i)) &&
                    ResponsiveDevice.SlotAllowedByPlayerAxes(
                        ResponsiveWidgetLayout.SlotFormFactor(i),
                        ResponsiveWidgetLayout.SlotOrientation(i),
                        enablePortrait,
                        enableLandscape);
                SetSlot(i, enable ? allowed : disabled);
            }
        }

        /// <summary>
        /// First Editor pass on empty YAML (no authored Cell Interval): same as
        /// <see cref="SeedAll"/>. Enabled-only zeros still seed. Leaves authored intervals alone.
        /// </summary>
        public bool SeedIfEmpty(
            float cellInterval,
            float scrollOffset,
            bool enablePortrait,
            bool enableLandscape)
        {
            if (HasAnyAuthoredInterval())
                return false;
            SeedAll(cellInterval, scrollOffset, enablePortrait, enableLandscape);
            return true;
        }

        bool HasAnyAuthoredInterval()
        {
            for (int i = 0; i < StatePropertyNames.Length; i++)
            {
                if (HasAuthoredInterval(GetSlot(i)))
                    return true;
            }
            return false;
        }

        static bool HasAuthoredInterval(ResponsiveScrollState state) =>
            state.cellInterval > 0f;

        #endregion

        #region Apply

        /// <summary>
        /// Writes the resolved slot into <paramref name="cellInterval"/> / <paramref name="scrollOffset"/>.
        /// Returns false when no enabled slot exists (caller keeps current values).
        /// </summary>
        public bool Apply(
            ResponsiveDevice.LayoutKey key,
            ref float cellInterval,
            ref float scrollOffset)
        {
            if (!ResolveActiveSlot(key, out _, out _, out ResponsiveScrollState state))
                return false;

            state.Apply(ref cellInterval, ref scrollOffset);
            return true;
        }

        /// <summary>
        /// Like <see cref="Apply"/>, but returns false when values are already current
        /// (host can skip Relayout).
        /// </summary>
        public bool ApplyChanged(
            ResponsiveDevice.LayoutKey key,
            ref float cellInterval,
            ref float scrollOffset)
        {
            float interval = cellInterval;
            float offset = scrollOffset;
            if (!Apply(key, ref interval, ref offset))
                return false;
            if (Mathf.Approximately(cellInterval, interval) &&
                Mathf.Approximately(scrollOffset, offset))
                return false;

            cellInterval = interval;
            scrollOffset = offset;
            return true;
        }

        #endregion

        #region Slot resolve

        public bool ResolveActiveSlot(
            ResponsiveDevice.LayoutKey key,
            out string propertyName,
            out ResponsiveDevice.FormFactor sourceForm,
            out ResponsiveScrollState state)
        {
            for (int i = 0; i < ResponsiveWidgetSlotResolver.MaxCandidates; i++)
            {
                if (!ResponsiveWidgetSlotResolver.GetCandidate(
                        key, i, out var form, out var orientation))
                    continue;
                int slotIndex = ResponsiveWidgetLayout.StatePropertyIndex(form, orientation);
                state = GetSlot(slotIndex);
                if (!state.enabled)
                    continue;

                propertyName = StatePropertyNames[slotIndex];
                sourceForm = form;
                return true;
            }

            int keyIndex = ResponsiveWidgetLayout.StatePropertyIndex(key);
            sourceForm = key.Form;
            propertyName = StatePropertyNames[keyIndex];
            state = GetSlot(keyIndex);
            return false;
        }

        /// <summary>Raw slot at a <see cref="StatePropertyNames"/> index (no fallback).</summary>
        ResponsiveScrollState GetSlot(int slotIndex) => SlotRef(ref this, slotIndex);

        void SetSlot(int slotIndex, in ResponsiveScrollState state) =>
            SlotRef(ref this, slotIndex) = state;

        /// <summary>
        /// The one slot-index → field map (<see cref="StatePropertyNames"/> order). Out-of-range
        /// indices read / write Phone Portrait.
        /// </summary>
        static ref ResponsiveScrollState SlotRef(ref ResponsiveScrollLayout layout, int slotIndex)
        {
            switch (slotIndex)
            {
                case 1: return ref layout.phoneLandscape;
                case 2: return ref layout.padPortrait;
                case 3: return ref layout.padLandscape;
                case 4: return ref layout.foldPhoneCoverPortrait;
                case 5: return ref layout.foldPhoneCoverLandscape;
                case 6: return ref layout.foldHalfOpenedPortrait;
                case 7: return ref layout.foldHalfOpenedLandscape;
                case 8: return ref layout.foldCoverHalfOpenedPortrait;
                case 9: return ref layout.foldCoverHalfOpenedLandscape;
                case 10: return ref layout.flipPhoneCoverPortrait;
                case 11: return ref layout.flipPhoneCoverLandscape;
                case 12: return ref layout.flipHalfOpenedPortrait;
                case 13: return ref layout.flipHalfOpenedLandscape;
                case 14: return ref layout.iPhoneDuoCoverPortrait;
                case 15: return ref layout.iPhoneDuoCoverLandscape;
                case 16: return ref layout.iPhoneDuoSeatedPortrait;
                case 17: return ref layout.iPhoneDuoSeatedLandscape;
                case 18: return ref layout.wideScreenPortrait;
                case 19: return ref layout.wideScreenLandscape;
                default: return ref layout.phonePortrait;
            }
        }

        #endregion
    }

}
