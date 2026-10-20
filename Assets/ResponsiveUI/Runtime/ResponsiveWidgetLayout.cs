using System;
using UnityEngine;

namespace FDPlus.ResponsiveUI
{
    /// <summary>
    /// Per-widget layout slots: Phone / Pad / Fold Cover / Fold Half Opened / Fold Cover Half Opened / Flip Cover / Flip Half Opened / iPhone Duo Cover / iPhone Duo Seated / Wide Screen × Portrait/Landscape.
    /// Disabled slots fall back along <see cref="ResponsiveWidgetSlotResolver"/>.
    /// </summary>
    [Serializable]
    public struct ResponsiveWidgetLayout
    {
        #region Slot names

        /// <summary>
        /// Canonical form-factor order for the twenty Portrait/Landscape slots and Design Forms.
        /// Equals the <see cref="ResponsiveDevice.FormFactor"/> value order, grouped by platform:
        /// shared (Phone, Pad), Android (Fold, Flip), iOS (iPhone Duo), Wide Screen.
        /// </summary>
        public static readonly ResponsiveDevice.FormFactor[] FormFactors =
        {
            ResponsiveDevice.FormFactor.Phone,
            ResponsiveDevice.FormFactor.Pad,
            ResponsiveDevice.FormFactor.FoldPhoneCover,
            ResponsiveDevice.FormFactor.FoldHalfOpened,
            ResponsiveDevice.FormFactor.FoldCoverHalfOpened,
            ResponsiveDevice.FormFactor.FlipPhoneCover,
            ResponsiveDevice.FormFactor.FlipHalfOpened,
            ResponsiveDevice.FormFactor.iPhoneDuoCover,
            ResponsiveDevice.FormFactor.iPhoneDuoSeated,
            ResponsiveDevice.FormFactor.WideScreen,
        };

        public static readonly string[] StatePropertyNames =
        {
            "phonePortrait",
            "phoneLandscape",
            "padPortrait",
            "padLandscape",
            "foldPhoneCoverPortrait",
            "foldPhoneCoverLandscape",
            "foldHalfOpenedPortrait",
            "foldHalfOpenedLandscape",
            "foldCoverHalfOpenedPortrait",
            "foldCoverHalfOpenedLandscape",
            "flipPhoneCoverPortrait",
            "flipPhoneCoverLandscape",
            "flipHalfOpenedPortrait",
            "flipHalfOpenedLandscape",
            "iPhoneDuoCoverPortrait",
            "iPhoneDuoCoverLandscape",
            "iPhoneDuoSeatedPortrait",
            "iPhoneDuoSeatedLandscape",
            "wideScreenPortrait",
            "wideScreenLandscape",
        };

        /// <summary>
        /// Inspector label for a <see cref="StatePropertyNames"/> index
        /// (Fold Cover, Flip Cover, …).
        /// </summary>
        public static string StatePropertyLabel(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= StatePropertyNames.Length)
                return "Unknown Slot";
            string orientation =
                SlotOrientation(slotIndex) == ResponsiveDevice.Orientation.Landscape
                    ? "Landscape"
                    : "Portrait";
            return ResponsiveDevice.GetFormDisplayName(SlotFormFactor(slotIndex)) +
                   " " + orientation;
        }

        /// <summary>
        /// Inspector group for a form. Phone and Pad sit with Android (they are the iPhone and
        /// iPad slots too). iPhone Duo is iOS. Wide Screen is its own group.
        /// </summary>
        public static string SlotGroupLabel(ResponsiveDevice.FormFactor form)
        {
            switch (form)
            {
                case ResponsiveDevice.FormFactor.iPhoneDuoCover:
                case ResponsiveDevice.FormFactor.iPhoneDuoSeated:
                    return "iOS";
                case ResponsiveDevice.FormFactor.WideScreen:
                    return "Wide Screen";
                default:
                    return "Android";
            }
        }

        /// <summary>
        /// Form factor for a <see cref="StatePropertyNames"/> index: slots 2i / 2i + 1 belong to
        /// <c>(FormFactor)i</c> (0–1 Phone, 2–3 Pad, 4–5 Fold Cover, 6–7 Fold Half Opened,
        /// 8–9 Fold Cover Half Opened, 10–11 Flip Cover, 12–13 Flip Half Opened,
        /// 14–15 iPhone Duo Cover, 16–17 iPhone Duo Seated, 18–19 Wide Screen).
        /// </summary>
        public static ResponsiveDevice.FormFactor SlotFormFactor(int slotIndex) =>
            (uint)slotIndex < (uint)StatePropertyNames.Length
                ? FormFactors[slotIndex >> 1]
                : ResponsiveDevice.FormFactor.Phone;

        /// <summary>
        /// Orientation for a <see cref="StatePropertyNames"/> index (even = Portrait, odd = Landscape).
        /// </summary>
        public static ResponsiveDevice.Orientation SlotOrientation(int slotIndex) =>
            (slotIndex & 1) == 1
                ? ResponsiveDevice.Orientation.Landscape
                : ResponsiveDevice.Orientation.Portrait;

        /// <summary>True for Fold/Flip HalfOpened / iPhone Duo Seated slots (opt-in; disabled falls back).</summary>
        public static bool IsHalfOpenedSlot(int slotIndex) =>
            ResponsiveDevice.IsHalfOpenedForm(SlotFormFactor(slotIndex));

        /// <summary>True for Flip Closed Flip Cover slots (opt-in for cameras/scroll/two-side).</summary>
        public static bool IsFlipPhoneCoverSlot(int slotIndex) =>
            SlotFormFactor(slotIndex) == ResponsiveDevice.FormFactor.FlipPhoneCover;

        /// <summary>True for Wide Screen slots (desktop / WebGL).</summary>
        public static bool IsWideScreenSlot(int slotIndex) =>
            SlotFormFactor(slotIndex) == ResponsiveDevice.FormFactor.WideScreen;

        /// <summary>
        /// New / seeded rows leave Flip Cover Landscape Disabled (Flip Closed landscape hides until
        /// authored). Platform-independent: seeding also applies
        /// <see cref="ResponsivePlatforms.SeedsEnabled"/> (Wide Screen), kept out of this check so
        /// capture and sync never disable authored Wide Screen slots.
        /// </summary>
        public static bool IsSeedDisabledSlot(int slotIndex) =>
            IsFlipPhoneCoverSlot(slotIndex) &&
            SlotOrientation(slotIndex) == ResponsiveDevice.Orientation.Landscape;

        /// <summary>
        /// HalfOpened, Cover or Wide Screen — falls back when Disabled (see
        /// <see cref="ResponsiveWidgetSlotResolver"/>).
        /// </summary>
        public static bool IsOptInSlot(int slotIndex) =>
            IsHalfOpenedSlot(slotIndex) ||
            ResponsiveDevice.IsCoverForm(SlotFormFactor(slotIndex)) ||
            IsWideScreenSlot(slotIndex);

        public static ResponsiveDevice.FormFactor SlotFormFactor(string propertyName) =>
            SlotFormFactor(StatePropertyIndex(propertyName));

        public static ResponsiveDevice.Orientation SlotOrientation(string propertyName) =>
            SlotOrientation(StatePropertyIndex(propertyName));

        #endregion

        #region Serialized

        public RectTransform target;

        public ResponsiveRectState phonePortrait;
        public ResponsiveRectState phoneLandscape;

        public ResponsiveRectState padPortrait;
        public ResponsiveRectState padLandscape;

        [Tooltip("Fold Cover (Fold8 passport ~10:16). Disabled falls back to Phone.")]
        public ResponsiveRectState foldPhoneCoverPortrait;
        public ResponsiveRectState foldPhoneCoverLandscape;

        [Tooltip("Fold Half Opened (Fold7-style). Live layout uses Landscape only. Disabled falls " +
                 "back to Phone.")]
        public ResponsiveRectState foldHalfOpenedPortrait;
        public ResponsiveRectState foldHalfOpenedLandscape;

        [Tooltip("Fold Cover Half Opened (Fold8-style). Live layout uses Landscape only. Disabled " +
                 "falls back to Phone.")]
        public ResponsiveRectState foldCoverHalfOpenedPortrait;
        public ResponsiveRectState foldCoverHalfOpenedLandscape;

        [Tooltip("Flip Cover (Flip Closed). Disabled falls back to Phone.")]
        public ResponsiveRectState flipPhoneCoverPortrait;
        [Tooltip("Flip Cover landscape. Starts Disabled. Disabled falls back to Phone.")]
        public ResponsiveRectState flipPhoneCoverLandscape;

        [Tooltip("Flip Half Opened. Disabled falls back to Phone.")]
        public ResponsiveRectState flipHalfOpenedPortrait;
        public ResponsiveRectState flipHalfOpenedLandscape;

        [Tooltip("iPhone Duo Cover (closed, outer display). Disabled falls back to Phone.")]
        public ResponsiveRectState iPhoneDuoCoverPortrait;
        public ResponsiveRectState iPhoneDuoCoverLandscape;

        [Tooltip("iPhone Duo Seated. Portrait is the horizontal laptop fold (tall screen). " +
                 "Landscape is the vertical book fold (wide screen). Disabled falls back to Pad " +
                 "(same shape, then the other), then Phone the same way.")]
        public ResponsiveRectState iPhoneDuoSeatedPortrait;
        public ResponsiveRectState iPhoneDuoSeatedLandscape;

        [Tooltip("Wide Screen (Windows / macOS / Linux / WebGL). Orientation follows the window or " +
                 "browser aspect. Disabled falls back only to the other Wide Screen orientation " +
                 "(never Pad / Phone); with both disabled the Target is left as is. Enabling " +
                 "copies Pad once.")]
        public ResponsiveRectState wideScreenPortrait;
        public ResponsiveRectState wideScreenLandscape;

        #endregion

        #region Apply

        /// <summary>
        /// Writes this Target through the hub so Design Scale ratio is solved once.
        /// Disabled slots fall back along <see cref="ResponsiveWidgetSlotResolver"/>; with no
        /// enabled candidate the Target is left as is.
        /// </summary>
        public void Apply(ResponsiveUI hub, in ResponsiveDevice.LayoutKey layout)
        {
            if (target == null || hub == null)
                return;

            if (!ResolveActiveSlot(
                    layout,
                    out _,
                    out ResponsiveDevice.FormFactor sourceForm,
                    out ResponsiveDevice.Orientation sourceOrientation,
                    out ResponsiveRectState state))
                return;
            hub.ApplyWidget(target, state, sourceForm, sourceOrientation);
        }

        /// <summary>
        /// True when <paramref name="propertyName"/> is the exact live slot,
        /// not a Landscape→Portrait or Cover→Phone fallback.
        /// </summary>
        public static bool IsExactLayoutSlot(
            in ResponsiveDevice.LayoutKey layout,
            string propertyName) =>
            propertyName == StatePropertyName(layout);

        /// <summary>
        /// Applies the resolved slot (no enabled candidate leaves the Target as is).
        /// <paramref name="currentCanvasSize"/> drives Design Scale
        /// (<c>localScale = defaultScale × ratio</c> when on; restores origin when off).
        /// </summary>
        public void Apply(ResponsiveDevice.LayoutKey key, Vector2 currentCanvasSize)
        {
            if (target == null)
                return;

            if (!ResolveActiveSlot(
                    key,
                    out _,
                    out ResponsiveDevice.FormFactor sourceForm,
                    out ResponsiveDevice.Orientation sourceOrientation,
                    out ResponsiveRectState state))
                return;
            // Fallback reuses the source slot's authored coordinate system. A Phone layout
            // shown on Cover/Fold/Flip must therefore retain the Phone design canvas and
            // the source slot's screen shape. A Landscape → Portrait fallback measures the
            // live size in that shape too (same as hub ApplyWidget).
            if (ResponsiveDevice.OrientationForForm(sourceForm, sourceOrientation) !=
                ResponsiveDevice.OrientationForForm(key.Form, key.Orientation))
                currentCanvasSize = ResponsiveUI.OrientSizeForSlot(
                    currentCanvasSize, sourceForm, sourceOrientation);
            state.Apply(target, currentCanvasSize, sourceForm, sourceOrientation);
        }

        /// <summary>True when any of this widget's slots has Scale From Design Canvas on.</summary>
        public bool UsesDesignScale()
        {
            for (int i = 0; i < StatePropertyNames.Length; i++)
            {
                if (GetSlot(i).scaleFromDesignCanvas)
                    return true;
            }
            return false;
        }

        public void Apply(
            ResponsiveDevice.FormFactor form,
            ResponsiveDevice.Orientation orientation,
            Vector2 currentCanvasSize) =>
            Apply(new ResponsiveDevice.LayoutKey(form, orientation), currentCanvasSize);

        #endregion

        #region Slot resolve

        /// <summary>
        /// Finds the enabled slot Apply/capture should use
        /// (fallback order: <see cref="ResponsiveWidgetSlotResolver"/>, which skips another
        /// platform's forms). Returns false when no enabled candidate exists; the outputs then
        /// name <paramref name="key"/>'s raw slot (enabled or not) for capture and Inspectors,
        /// and Apply leaves the Target as is.
        /// </summary>
        public bool ResolveActiveSlot(
            ResponsiveDevice.LayoutKey key,
            out string propertyName,
            out ResponsiveDevice.FormFactor sourceForm,
            out ResponsiveRectState state) =>
            ResolveActiveSlot(
                key,
                out propertyName,
                out sourceForm,
                out _,
                out state);

        /// <summary>
        /// Same as <see cref="ResolveActiveSlot(ResponsiveDevice.LayoutKey, out string, out ResponsiveDevice.FormFactor, out ResponsiveRectState)"/>,
        /// and reports the source slot's orientation (Landscape fallback keeps Portrait).
        /// </summary>
        public bool ResolveActiveSlot(
            ResponsiveDevice.LayoutKey key,
            out string propertyName,
            out ResponsiveDevice.FormFactor sourceForm,
            out ResponsiveDevice.Orientation sourceOrientation,
            out ResponsiveRectState state)
        {
            for (int i = 0; i < ResponsiveWidgetSlotResolver.MaxCandidates; i++)
            {
                if (!ResponsiveWidgetSlotResolver.GetCandidate(
                        key, i, out var form, out var orientation))
                    continue;
                int slotIndex = StatePropertyIndex(form, orientation);
                state = GetSlot(slotIndex);
                if (!state.enabled)
                    continue;

                propertyName = StatePropertyNames[slotIndex];
                sourceForm = form;
                sourceOrientation = orientation;
                return true;
            }

            int keyIndex = StatePropertyIndex(key);
            sourceForm = key.Form;
            sourceOrientation = key.Orientation;
            propertyName = StatePropertyNames[keyIndex];
            state = GetSlot(keyIndex);
            return false;
        }

        /// <summary>Raw slot at a <see cref="StatePropertyNames"/> index (no fallback).</summary>
        ResponsiveRectState GetSlot(int slotIndex) => SlotRef(ref this, slotIndex);

        /// <summary>
        /// The one slot-index → field map (<see cref="StatePropertyNames"/> order). Out-of-range
        /// indices read Phone Portrait.
        /// </summary>
        static ref ResponsiveRectState SlotRef(ref ResponsiveWidgetLayout layout, int slotIndex)
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

        public static string StatePropertyName(ResponsiveDevice.LayoutKey key) =>
            StatePropertyNames[StatePropertyIndex(key)];

        public static string StatePropertyName(
            ResponsiveDevice.FormFactor form,
            ResponsiveDevice.Orientation orientation) =>
            StatePropertyNames[StatePropertyIndex(form, orientation)];

        public static int StatePropertyIndex(in ResponsiveDevice.LayoutKey key) =>
            StatePropertyIndex(key.Form, key.Orientation);

        /// <summary>
        /// Form i owns slots 2i (Portrait) and 2i + 1 (Landscape): the
        /// <see cref="ResponsiveDevice.FormFactor"/> value is the slot order
        /// (<see cref="FormFactors"/>). An unknown form maps to Phone.
        /// </summary>
        public static int StatePropertyIndex(
            ResponsiveDevice.FormFactor form,
            ResponsiveDevice.Orientation orientation)
        {
            int formIndex = (int)form;
            if ((uint)formIndex >= (uint)FormFactors.Length)
                formIndex = 0;
            return formIndex * 2 +
                   (orientation == ResponsiveDevice.Orientation.Landscape ? 1 : 0);
        }

        public static bool GetStatePropertyIndex(string propertyName, out int index)
        {
            string[] names = StatePropertyNames;
            for (int i = 0; i < names.Length; i++)
            {
                if (names[i] != propertyName)
                    continue;
                index = i;
                return true;
            }

            index = 0;
            return false;
        }

        public static int StatePropertyIndex(string propertyName) =>
            GetStatePropertyIndex(propertyName, out int index) ? index : 0;

        #endregion
    }

}
