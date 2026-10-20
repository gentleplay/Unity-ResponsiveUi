using System;
using UnityEngine;

namespace FDPlus.ResponsiveUI
{
    /// <summary>
    /// Per-slot visibility for one Active Object. Unchecked = hidden in that
    /// Phone / Pad / Fold Cover / Fold Half Opened / Fold Cover Half Opened / Flip Cover / Flip Half Opened / iPhone Duo Cover / iPhone Duo Seated / Wide Screen × Portrait / Landscape slot.
    /// There is no widget-style fallback: off means hide, not reuse Phone.
    /// </summary>
    [Serializable]
    public struct ResponsiveActiveObjectSlots
    {
        #region Serialized

        public bool phonePortrait;
        public bool phoneLandscape;
        public bool padPortrait;
        public bool padLandscape;
        public bool foldPhoneCoverPortrait;
        public bool foldPhoneCoverLandscape;
        public bool foldHalfOpenedPortrait;
        public bool foldHalfOpenedLandscape;
        public bool foldCoverHalfOpenedPortrait;
        public bool foldCoverHalfOpenedLandscape;
        public bool flipPhoneCoverPortrait;
        public bool flipPhoneCoverLandscape;
        public bool flipHalfOpenedPortrait;
        public bool flipHalfOpenedLandscape;
        public bool iPhoneDuoCoverPortrait;
        public bool iPhoneDuoCoverLandscape;
        public bool iPhoneDuoSeatedPortrait;
        public bool iPhoneDuoSeatedLandscape;
        public bool wideScreenPortrait;
        public bool wideScreenLandscape;

        #endregion

        #region Factories

        /// <summary>
        /// Form Transition Loading defaults: every slot on except Flip Cover
        /// (Flip Closed should not flash a loading cover).
        /// </summary>
        public static ResponsiveActiveObjectSlots FormTransitionDefaults()
        {
            var slots = new ResponsiveActiveObjectSlots();
            int n = ResponsiveWidgetLayout.StatePropertyNames.Length;
            for (int i = 0; i < n; i++)
            {
                slots.Set(
                    i,
                    ResponsiveWidgetLayout.SlotFormFactor(i) !=
                    ResponsiveDevice.FormFactor.FlipPhoneCover);
            }
            return slots;
        }

        #endregion

        #region Lookup

        public bool IsActive(in ResponsiveDevice.LayoutKey layout) =>
            Get(ResponsiveWidgetLayout.StatePropertyIndex(layout));

        public bool Get(int slotIndex) =>
            (uint)slotIndex < (uint)ResponsiveWidgetLayout.StatePropertyNames.Length &&
            SlotRef(ref this, slotIndex);

        public void Set(int slotIndex, bool value)
        {
            if ((uint)slotIndex < (uint)ResponsiveWidgetLayout.StatePropertyNames.Length)
                SlotRef(ref this, slotIndex) = value;
        }

        /// <summary>
        /// The one slot-index → field map (<see cref="ResponsiveWidgetLayout.StatePropertyNames"/>
        /// order). Callers range-check the index.
        /// </summary>
        static ref bool SlotRef(ref ResponsiveActiveObjectSlots slots, int slotIndex)
        {
            switch (slotIndex)
            {
                case 1: return ref slots.phoneLandscape;
                case 2: return ref slots.padPortrait;
                case 3: return ref slots.padLandscape;
                case 4: return ref slots.foldPhoneCoverPortrait;
                case 5: return ref slots.foldPhoneCoverLandscape;
                case 6: return ref slots.foldHalfOpenedPortrait;
                case 7: return ref slots.foldHalfOpenedLandscape;
                case 8: return ref slots.foldCoverHalfOpenedPortrait;
                case 9: return ref slots.foldCoverHalfOpenedLandscape;
                case 10: return ref slots.flipPhoneCoverPortrait;
                case 11: return ref slots.flipPhoneCoverLandscape;
                case 12: return ref slots.flipHalfOpenedPortrait;
                case 13: return ref slots.flipHalfOpenedLandscape;
                case 14: return ref slots.iPhoneDuoCoverPortrait;
                case 15: return ref slots.iPhoneDuoCoverLandscape;
                case 16: return ref slots.iPhoneDuoSeatedPortrait;
                case 17: return ref slots.iPhoneDuoSeatedLandscape;
                case 18: return ref slots.wideScreenPortrait;
                case 19: return ref slots.wideScreenLandscape;
                default: return ref slots.phonePortrait;
            }
        }

        #endregion
    }

    /// <summary>
    /// One GameObject and the slots where it should be active.
    /// Unlisted objects are left alone. Duplicate targets: last entry wins.
    /// </summary>
    [Serializable]
    public struct ResponsiveActiveObjectEntry
    {
        public GameObject target;
        public ResponsiveActiveObjectSlots slots;
    }

    /// <summary>Applies Active Objects lists for the live layout.</summary>
    public static class ResponsiveActiveObjects
    {
        #region Apply

        public static void Apply(
            ResponsiveActiveObjectEntry[] entries,
            in ResponsiveDevice.LayoutKey layout)
        {
            if (entries == null || entries.Length == 0)
                return;

            for (int i = 0; i < entries.Length; i++)
            {
                GameObject go = entries[i].target;
                if (go == null)
                    continue;
                bool active = entries[i].slots.IsActive(layout);
                if (go.activeSelf == active)
                    continue;
                go.SetActive(active);
            }
        }

        #endregion
    }
}
