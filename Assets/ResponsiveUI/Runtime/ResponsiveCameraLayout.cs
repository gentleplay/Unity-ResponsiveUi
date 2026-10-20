using System;
using UnityEngine;

namespace FDPlus.ResponsiveUI
{
    /// <summary>
    /// Per-camera layout slots: Phone / Pad / Fold Cover / Fold Half Opened /
    /// Fold Cover Half Opened / Flip Cover / Flip Half Opened / iPhone Duo Cover / iPhone Duo Seated /
    /// Wide Screen × Portrait/Landscape.
    /// Disabled slots fall back along <see cref="ResponsiveWidgetSlotResolver"/>.
    /// </summary>
    [Serializable]
    public struct ResponsiveCameraLayout
    {
        #region Serialized

        public static readonly string[] StatePropertyNames = ResponsiveWidgetLayout.StatePropertyNames;

        public Camera target;

        [Tooltip(
            "Single background SpriteRenderer fitted to this Target camera's visible view. " +
            "ResponsiveCamera places it behind camera content.")]
        public SpriteRenderer background;

        [Tooltip(
            "Stretch fills width and height (may change aspect). " +
            "Cover fills the view (may crop). Fit contains inside the view (may letterbox).")]
        public ResponsiveCameraSpriteFitMode spriteFitMode;

        public ResponsiveCameraState phonePortrait;
        public ResponsiveCameraState phoneLandscape;

        public ResponsiveCameraState padPortrait;
        public ResponsiveCameraState padLandscape;

        [Tooltip("Fold Cover (Fold8 passport ~10:16). Leave disabled to reuse Phone slots.")]
        public ResponsiveCameraState foldPhoneCoverPortrait;
        public ResponsiveCameraState foldPhoneCoverLandscape;

        [Tooltip("Fold Half Opened (Fold7-style). Leave disabled to reuse Phone slots.")]
        public ResponsiveCameraState foldHalfOpenedPortrait;
        public ResponsiveCameraState foldHalfOpenedLandscape;

        [Tooltip("Fold Cover Half Opened (Fold8-style). Leave disabled to reuse Phone slots.")]
        public ResponsiveCameraState foldCoverHalfOpenedPortrait;
        public ResponsiveCameraState foldCoverHalfOpenedLandscape;

        [Tooltip("Flip Cover (Flip Closed). Leave disabled to reuse Phone slots.")]
        public ResponsiveCameraState flipPhoneCoverPortrait;
        public ResponsiveCameraState flipPhoneCoverLandscape;

        [Tooltip("Flip Half Opened. Leave disabled to reuse Phone slots.")]
        public ResponsiveCameraState flipHalfOpenedPortrait;
        public ResponsiveCameraState flipHalfOpenedLandscape;

        [Tooltip("iPhone Duo Cover (closed, outer display). Leave disabled to reuse Phone slots.")]
        public ResponsiveCameraState iPhoneDuoCoverPortrait;
        public ResponsiveCameraState iPhoneDuoCoverLandscape;

        [Tooltip("iPhone Duo Seated. Portrait is the horizontal laptop fold (tall screen). " +
                 "Landscape is the vertical book fold (wide screen). Leave disabled to reuse Pad " +
                 "(same shape, then the other), then Phone the same way.")]
        public ResponsiveCameraState iPhoneDuoSeatedPortrait;
        public ResponsiveCameraState iPhoneDuoSeatedLandscape;

        [Tooltip("Wide Screen (Windows / macOS / Linux / WebGL). Orientation follows the window or " +
                 "browser aspect. Leave disabled to reuse the other Wide Screen orientation " +
                 "(never Pad / Phone).")]
        public ResponsiveCameraState wideScreenPortrait;
        public ResponsiveCameraState wideScreenLandscape;

        #endregion

        #region Apply

        public void Apply(ResponsiveDevice.LayoutKey key) =>
            Apply(key, applyViewport: true, Vector2.zero);

        public void Apply(ResponsiveDevice.LayoutKey key, bool applyViewport) =>
            Apply(key, applyViewport, Vector2.zero);

        /// <param name="applyViewport">
        /// When false, skips <see cref="Camera.rect"/>.
        /// </param>
        /// <param name="currentUiSize">
        /// Live UI / canvas size for <see cref="ResponsiveCameraState.scaleFromUiSize"/>
        /// and <see cref="ResponsiveCameraState.scaleProjection"/>.
        /// Focus Bounds layout uses <paramref name="hub"/> widget ratio when set.
        /// </param>
        /// <param name="settleCanvases">
        /// When false, Focus skips <see cref="Canvas.ForceUpdateCanvases"/> (target-only
        /// motion where the Focus Bounds HUD is already settled).
        /// </param>
        public void Apply(
            ResponsiveDevice.LayoutKey key,
            bool applyViewport,
            Vector2 currentUiSize,
            bool applyBoundsLayout = true,
            ResponsiveUI hub = null,
            bool settleCanvases = true)
        {
            if (target == null)
                return;

            if (ResolveActiveSlot(
                    key,
                    out _,
                    out ResponsiveDevice.FormFactor sourceForm,
                    out ResponsiveDevice.Orientation sourceOrientation,
                    out ResponsiveCameraState state))
            {
                state.Apply(
                    target,
                    applyViewport,
                    currentUiSize,
                    sourceForm,
                    sourceOrientation);
                ApplyFocus(
                    target,
                    state,
                    background,
                    currentUiSize,
                    sourceForm,
                    sourceOrientation,
                    applyBoundsLayout,
                    hub,
                    settleCanvases);
                ResponsiveCameraSpriteFit.ApplyBackground(background, target, spriteFitMode);
                return;
            }

            // No enabled slot: clear stale half-open / custom rects on device posture changes.
            if (applyViewport)
                ResponsiveCameraState.ApplyFullViewport(target);

            ResponsiveCameraSpriteFit.ApplyBackground(background, target, spriteFitMode);
        }

        static void ApplyFocus(
            Camera camera,
            in ResponsiveCameraState state,
            SpriteRenderer ignoreBackground,
            Vector2 currentUiSize,
            ResponsiveDevice.FormFactor sourceForm,
            ResponsiveDevice.Orientation sourceOrientation,
            bool applyBoundsLayout,
            ResponsiveUI hub,
            bool settleCanvases)
        {
            if (applyBoundsLayout)
                state.ApplyFocusBoundsLayout(
                    currentUiSize, sourceForm, sourceOrientation, hub);
            if (!state.HasFocus())
                return;

            ResponsiveCameraFocus.Apply(
                camera,
                state.focusTarget,
                state.focusScreen,
                state.focusFit,
                state.focusPadding > 0f ? state.focusPadding : 1f,
                ignoreBackground,
                state.focusBounds,
                default,
                settleCanvases);
        }

        public bool HasFocus()
        {
            for (int i = 0; i < StatePropertyNames.Length; i++)
            {
                if (GetSlot(i).HasFocus())
                    return true;
            }
            return false;
        }

        public bool HasBackground() => background != null;

        public bool UsesFocusBounds(RectTransform bounds)
        {
            if (bounds == null)
                return false;
            for (int i = 0; i < StatePropertyNames.Length; i++)
            {
                if (UsesFocusBounds(GetSlot(i), bounds))
                    return true;
            }
            return false;
        }

        public bool UsesFocusBoundsRect(RectTransform bounds)
        {
            if (bounds == null)
                return false;
            for (int i = 0; i < StatePropertyNames.Length; i++)
            {
                if (HasFocusBoundsRect(GetSlot(i), bounds))
                    return true;
            }
            return false;
        }

        /// <summary>True when the resolved active slot owns this Focus Bounds rect.</summary>
        public bool ActiveSlotUsesFocusBoundsRect(
            RectTransform bounds,
            in ResponsiveDevice.LayoutKey key)
        {
            if (bounds == null ||
                !ResolveActiveSlot(key, out _, out _, out ResponsiveCameraState state))
                return false;
            return state.focusBounds == bounds;
        }

        static bool UsesFocusBounds(in ResponsiveCameraState state, RectTransform bounds) =>
            state.focusBounds == bounds && state.HasFocus();

        static bool HasFocusBoundsRect(in ResponsiveCameraState state, RectTransform bounds) =>
            state.focusBounds == bounds;

        public bool UsesUiSizeScale()
        {
            for (int i = 0; i < StatePropertyNames.Length; i++)
            {
                if (GetSlot(i).UsesCanvasScale)
                    return true;
            }
            return false;
        }

        #endregion

        #region Slot resolve

        public bool ResolveActiveSlot(
            ResponsiveDevice.LayoutKey key,
            out string propertyName,
            out ResponsiveDevice.FormFactor sourceForm,
            out ResponsiveCameraState state) =>
            ResolveActiveSlot(
                key,
                out propertyName,
                out sourceForm,
                out _,
                out state);

        public bool ResolveActiveSlot(
            ResponsiveDevice.LayoutKey key,
            out string propertyName,
            out ResponsiveDevice.FormFactor sourceForm,
            out ResponsiveDevice.Orientation sourceOrientation,
            out ResponsiveCameraState state)
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
                sourceOrientation = orientation;
                return true;
            }

            int keyIndex = ResponsiveWidgetLayout.StatePropertyIndex(key);
            sourceForm = key.Form;
            sourceOrientation = key.Orientation;
            propertyName = StatePropertyNames[keyIndex];
            state = GetSlot(keyIndex);
            return false;
        }

        /// <summary>
        /// Samples the active slot's Focus Bounds HUD into that slot's widget layout.
        /// Stores design-space edges and origin Default Scale (same as widget capture).
        /// Skips the write when the live HUD already matches Apply.
        /// Other slots keep their own values.
        /// </summary>
        public bool CaptureActiveFocusBoundsLayout(
            ResponsiveDevice.LayoutKey key,
            Vector2 currentUiSize,
            ResponsiveUI hub = null)
        {
            ResolveActiveSlot(
                key,
                out _,
                out ResponsiveDevice.FormFactor form,
                out ResponsiveDevice.Orientation orientation,
                out _);

            int slotIndex = ResponsiveWidgetLayout.StatePropertyIndex(form, orientation);
            ResponsiveCameraState slot = GetSlot(slotIndex);
            if (slot.focusBounds == null)
                return false;

            ResponsiveRectState existing = slot.focusBoundsLayout;
            Vector2 ratio = CaptureFocusBoundsRatio(
                existing, slot.focusBounds, form, orientation, currentUiSize, hub);
            Vector2 expectedLocal = hub != null
                ? hub.GetAppliedWidgetScale(existing, form, ratio)
                : existing.ComputeAppliedLocalScale(currentUiSize, form, orientation);
            if (existing.enabled &&
                existing.MatchesApplied(slot.focusBounds, ratio, expectedLocal))
                return false;

            ResponsiveRectState captured = ResponsiveRectState.From(
                slot.focusBounds, ratio, form);
            captured.CopyDesignScaleSettingsFrom(existing);
            captured.enabled = true;
            if (captured.HasSameLayout(existing))
                return false;

            slot.focusBoundsLayout = captured;
            SetSlot(slotIndex, slot);
            return true;
        }

        static Vector2 CaptureFocusBoundsRatio(
            in ResponsiveRectState existing,
            RectTransform bounds,
            ResponsiveDevice.FormFactor form,
            ResponsiveDevice.Orientation orientation,
            Vector2 currentUiSize,
            ResponsiveUI hub) =>
            hub != null
                ? hub.GetLayoutRatio(existing, form, bounds, orientation)
                : existing.ComputeLayoutRatio(
                    ResponsiveUI.OrientSizeForSlot(
                        currentUiSize, form, orientation),
                    form,
                    orientation);

        /// <summary>
        /// Raw slot at a <see cref="StatePropertyNames"/> index (no fallback; disabled slots too).
        /// </summary>
        public ResponsiveCameraState GetSlot(int slotIndex) => SlotRef(ref this, slotIndex);

        void SetSlot(int slotIndex, in ResponsiveCameraState state) =>
            SlotRef(ref this, slotIndex) = state;

        /// <summary>
        /// The one slot-index → field map (<see cref="StatePropertyNames"/> order). Out-of-range
        /// indices read / write Phone Portrait.
        /// </summary>
        static ref ResponsiveCameraState SlotRef(ref ResponsiveCameraLayout layout, int slotIndex)
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

        #region Capture

        /// <summary>Captures the live camera into every enabled slot (or Phone Portrait when none).</summary>
        /// <remarks>
        /// When a slot has Scale From UI Size, position is stored in design space.
        /// When it has Scale Projection, Orthographic Size / Field Of View is stored in design space.
        /// Projection type is auto-detected from the Target.
        /// </remarks>
        public void CaptureFromTarget(
            Vector2 currentUiSize = default,
            bool enablePhonePortraitIfEmpty = true)
        {
            if (target == null)
                return;

            ResponsiveCameraState live = ResponsiveCameraState.From(target);
            bool anyEnabled = false;
            for (int i = 0; i < StatePropertyNames.Length; i++)
            {
                if (GetSlot(i).enabled)
                {
                    anyEnabled = true;
                    break;
                }
            }

            if (!anyEnabled && enablePhonePortraitIfEmpty)
            {
                phonePortrait = MergeCaptured(
                    phonePortrait, live, currentUiSize,
                    ResponsiveDevice.FormFactor.Phone, ResponsiveDevice.Orientation.Portrait);
                return;
            }

            for (int i = 0; i < StatePropertyNames.Length; i++)
            {
                ResponsiveCameraState existing = GetSlot(i);
                if (!existing.enabled)
                    continue;
                SetSlot(
                    i,
                    MergeCaptured(
                        existing, live, currentUiSize,
                        ResponsiveWidgetLayout.SlotFormFactor(i),
                        ResponsiveWidgetLayout.SlotOrientation(i)));
            }
        }

        static ResponsiveCameraState MergeCaptured(
            ResponsiveCameraState existing,
            ResponsiveCameraState live,
            Vector2 currentUiSize,
            ResponsiveDevice.FormFactor slotForm,
            ResponsiveDevice.Orientation slotOrientation)
        {
            Vector3 localPosition = live.localPosition;
            float orthographicSize = live.orthographicSize;
            float fieldOfView = live.fieldOfView;
            if (existing.UsesCanvasScale)
            {
                Vector2 designUi = existing.GetResolvedDesignUiSize(slotForm, slotOrientation);
                Vector2 liveUi = existing.ResolveLiveUiSize(
                    currentUiSize, slotForm, slotOrientation);
                if (existing.scaleFromUiSize)
                {
                    Vector2 ratio = ResponsiveRectState.ComputeCanvasRatio(
                        liveUi, designUi, existing.uiScaleMode);
                    localPosition = ResponsiveCameraState.UnscaleDesignPosition(localPosition, ratio);
                }
                if (existing.scaleProjection)
                {
                    orthographicSize = ResponsiveCameraState.UnscaleOrthographicSize(
                        orthographicSize, liveUi, designUi, existing.uiScaleMode);
                    fieldOfView = ResponsiveCameraState.UnscaleFieldOfView(
                        fieldOfView, liveUi, designUi, existing.uiScaleMode);
                }
            }

            live.orthographicSize = orthographicSize;
            live.fieldOfView = fieldOfView;
            live.scaleFromUiSize = existing.scaleFromUiSize;
            live.scaleProjection = existing.scaleProjection;
            live.uiScaleMode = existing.uiScaleMode;
            live.focusTarget = existing.focusTarget;
            live.focusScreen = existing.focusScreen;
            live.focusFit = existing.focusFit;
            live.focusPadding = existing.focusPadding > 0f ? existing.focusPadding : 1f;
            live.focusBounds = existing.focusBounds;
            live.focusBoundsLayout = existing.focusBoundsLayout;
            if (existing.HasFocus())
            {
                bool applyLocal = existing.applyLocalTransform;
                live.localPosition = existing.localPosition;
                live.applyLocalPosition = applyLocal;
                live.applyLocalRotation = applyLocal;
                if (!applyLocal)
                    live.localEulerAngles = existing.localEulerAngles;
            }
            else
                live.localPosition = localPosition;
            // Projection + apply toggles come from live (auto-detected Target)
            // unless Focus owns the slot pose.
            return live;
        }

        #endregion
    }

}
