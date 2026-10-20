using System;
using UnityEngine;

namespace FDPlus.ResponsiveUI
{
    /// <summary>
    /// Snapshot of <see cref="Camera"/> viewport, transform, and projection for one
    /// form-factor × orientation slot. Optional UI-size scaling mirrors widget Design Scale.
    /// </summary>
    [Serializable]
    public struct ResponsiveCameraState
    {
        #region Constants

        public static readonly Rect FullViewport = new Rect(0f, 0f, 1f, 1f);

        #endregion

        #region Serialized

        [Tooltip("When off, Apply ignores this state and falls back via the slot resolver.")]
        public bool enabled;

        [Tooltip("Normalized Camera.rect (x, y, width, height) in [0, 1].")]
        public Rect viewport;

        [Tooltip("Part of Apply Local. When Apply Local is on, writes Transform.localPosition. Stored per slot even when off.")]
        public bool applyLocalPosition;

        [Tooltip("Local position for this slot. Kept per device profile. Scaled by UI size ratio when Scale From UI Size is on.")]
        public Vector3 localPosition;

        [Tooltip("Part of Apply Local. When Apply Local is on, writes Transform.localEulerAngles.")]
        public bool applyLocalRotation;

        [Tooltip("Local euler angles in degrees for this slot (not scaled by UI size).")]
        public Vector3 localEulerAngles;

        [Tooltip(
            "When on: localPosition scales by live display size / Design UI Size. " +
            "Design UI Size is locked to Responsive UI settings for this form factor. " +
            "Live size uses Screen / Device Simulator (not CanvasScaler Expand height).")]
        public bool scaleFromUiSize;

        [Tooltip(
            "When on: Orthographic Size and Field Of View follow Scale Mode as camera framing " +
            "(Width = keep horizontal world span via aspect; Height = keep vertical span; " +
            "Auto = pick Width or Height from live vs Design Canvas aspect). " +
            "Not raw pixel canvas ratio — that would crush smaller phones.")]
        public bool scaleProjection;

        [Tooltip(
            "Width / Height / Auto / UniformMin / UniformMax / IndependentXY — same modes as widget Design Scale. " +
            "Auto fits like UniformMin: wider than Design Canvas → Height, taller → Width.")]
        public ResponsiveDesignScaleMode uiScaleMode;

        [Tooltip("Orthographic or Perspective. Applied to the Target camera; Capture / first assign copy from Target.")]
        public bool orthographic;

        [Tooltip("When on and Projection is Perspective, writes Camera.fieldOfView.")]
        public bool applyFieldOfView;

        [Tooltip("Perspective field of view (degrees). Design-space when Scale Projection is on.")]
        [Min(0.1f)]
        public float fieldOfView;

        [Tooltip("When on and Projection is Orthographic, writes Camera.orthographicSize.")]
        public bool applyOrthographicSize;

        [Tooltip("Orthographic half-height. Design-space when Scale Projection is on.")]
        [Min(0.01f)]
        public float orthographicSize;

        [Tooltip("World object to keep in this slot's camera (pool table, board, …).")]
        public Transform focusTarget;

        [Tooltip(
            "None off. Auto uses this slot's Focus Bounds values when set, otherwise the whole camera. " +
            "Bounds uses Focus Bounds (Full if unset). Full ignores Focus Bounds. " +
            "Does not write Camera.rect.")]
        public ResponsiveCameraFocusScreen focusScreen;

        [Tooltip(
            "Center slides the camera so the object sits in the pane. " +
            "Cover fills the pane (may crop). Fit shows the whole object (may letterbox).")]
        public ResponsiveCameraFocusFit focusFit;

        [Tooltip("Extra world margin for Cover / Fit (1 = bounds only).")]
        [Min(1f)]
        public float focusPadding;

        [Tooltip(
            "HUD rect to frame for Auto / Bounds on this slot. Per-slot layout below " +
            "authors it like a widget item (anchors, Pos / Size). Does not write Camera.rect.")]
        public RectTransform focusBounds;

        [Tooltip(
            "Per-slot layout for Focus Bounds, same fields as a ResponsiveUiController widget. " +
            "Phone and Fold store their own values.")]
        [HideInInspector]
        public ResponsiveRectState focusBoundsLayout;

        #endregion

        public bool HasFocus() =>
            focusScreen != ResponsiveCameraFocusScreen.None &&
            focusTarget != null;

        public void ApplyFocusBoundsLayout(
            Vector2 currentUiSize,
            ResponsiveDevice.FormFactor slotForm,
            ResponsiveDevice.Orientation slotOrientation,
            ResponsiveUI hub = null)
        {
            if (focusBounds == null || !focusBoundsLayout.enabled)
                return;

            if (hub != null)
            {
                hub.ApplyWidget(
                    focusBounds,
                    focusBoundsLayout,
                    slotForm,
                    slotOrientation);
                return;
            }

            // Landscape → Portrait fallback: measure the live size in the source slot's
            // screen shape, same as ResponsiveWidgetLayout.Apply (no-op when they match).
            focusBoundsLayout.Apply(
                focusBounds,
                ResponsiveUI.OrientSizeForSlot(
                    currentUiSize, slotForm, slotOrientation),
                slotForm,
                slotOrientation);
        }

        /// <summary>
        /// One Apply Local checkbox: writes local position and local euler together.
        /// Either flag on means Apply Local is on; the setter keeps the two in step.
        /// </summary>
        public bool applyLocalTransform
        {
            get => applyLocalPosition || applyLocalRotation;
            set
            {
                applyLocalPosition = value;
                applyLocalRotation = value;
            }
        }

        #region Factory

        public static ResponsiveCameraState CreateDefault(bool enabled = true) =>
            new ResponsiveCameraState
            {
                enabled = enabled,
                viewport = FullViewport,
                applyLocalPosition = false,
                localPosition = Vector3.zero,
                applyLocalRotation = false,
                localEulerAngles = Vector3.zero,
                scaleFromUiSize = false,
                scaleProjection = false,
                uiScaleMode = ResponsiveDesignScaleMode.Auto,
                orthographic = true,
                applyFieldOfView = false,
                fieldOfView = 60f,
                applyOrthographicSize = false,
                orthographicSize = 5f,
                focusTarget = null,
                focusScreen = ResponsiveCameraFocusScreen.None,
                focusFit = ResponsiveCameraFocusFit.Center,
                focusPadding = 1f,
                focusBounds = null,
                focusBoundsLayout = ResponsiveRectState.CreateDefault(enabled: false),
            };

        /// <summary>
        /// Captures live camera values and auto-detects Orthographic vs Perspective.
        /// </summary>
        public static ResponsiveCameraState From(Camera camera)
        {
            if (camera == null)
                return CreateDefault(enabled: true);

            Transform t = camera.transform;
            bool ortho = camera.orthographic;
            return new ResponsiveCameraState
            {
                enabled = true,
                viewport = camera.rect,
                applyLocalPosition = true,
                localPosition = t.localPosition,
                applyLocalRotation = true,
                localEulerAngles = t.localEulerAngles,
                scaleFromUiSize = false,
                scaleProjection = false,
                uiScaleMode = ResponsiveDesignScaleMode.Auto,
                orthographic = ortho,
                applyFieldOfView = !ortho,
                fieldOfView = camera.fieldOfView,
                applyOrthographicSize = ortho,
                orthographicSize = camera.orthographicSize,
                focusTarget = null,
                focusScreen = ResponsiveCameraFocusScreen.None,
                focusFit = ResponsiveCameraFocusFit.Center,
                focusPadding = 1f,
                focusBounds = null,
                focusBoundsLayout = ResponsiveRectState.CreateDefault(enabled: false),
            };
        }

        #endregion

        #region Design UI Size

        /// <summary>
        /// Camera UI scaling is project-controlled: every slot follows the Responsive UI
        /// Design Canvas default for its form factor.
        /// </summary>
        public Vector2 GetResolvedDesignUiSize(
            ResponsiveDevice.FormFactor slotForm,
            ResponsiveDevice.Orientation slotOrientation = ResponsiveDevice.Orientation.Portrait) =>
            ResponsiveUI.OrientSizeForSlot(
                ResponsiveRectState.GetDefaultDesignCanvasSize(slotForm), slotForm, slotOrientation);

        public bool UsesCanvasScale => scaleFromUiSize || scaleProjection;

        /// <summary>
        /// Live UI size after default-profile identity (same watch rules as widgets).
        /// Fold Expand inner on Pad slots, Flip Opened main on Phone slots, and
        /// Flip Closed cover on Flip Cover slots, are 1:1.
        /// </summary>
        public Vector2 ResolveLiveUiSize(
            Vector2 currentUiSize,
            ResponsiveDevice.FormFactor slotForm,
            ResponsiveDevice.Orientation slotOrientation = ResponsiveDevice.Orientation.Portrait) =>
            ResponsiveDesignScale.ResolveIdentityLiveSize(
                currentUiSize,
                slotForm,
                slotOrientation,
                GetResolvedDesignUiSize(slotForm, slotOrientation));

        /// <summary>
        /// Live UI / canvas ÷ Design UI Size. Same <see cref="ResponsiveRectState.ComputeCanvasRatio"/>
        /// widgets use for Design Scale, including default-profile identity.
        /// </summary>
        public Vector2 ComputeCanvasScaleRatio(
            Vector2 currentUiSize,
            ResponsiveDevice.FormFactor slotForm,
            ResponsiveDevice.Orientation slotOrientation = ResponsiveDevice.Orientation.Portrait)
        {
            Vector2 design = GetResolvedDesignUiSize(slotForm, slotOrientation);
            return ResponsiveRectState.ComputeCanvasRatio(
                ResolveLiveUiSize(currentUiSize, slotForm, slotOrientation),
                design,
                uiScaleMode);
        }

        public Vector2 ComputeUiScaleRatio(
            Vector2 currentUiSize,
            ResponsiveDevice.FormFactor slotForm,
            ResponsiveDevice.Orientation slotOrientation = ResponsiveDevice.Orientation.Portrait) =>
            scaleFromUiSize
                ? ComputeCanvasScaleRatio(currentUiSize, slotForm, slotOrientation)
                : Vector2.one;

        #endregion

        #region Apply

        public void Apply(Camera camera) =>
            Apply(camera, applyViewport: true, Vector2.zero, ResponsiveDevice.FormFactor.Phone);

        public void Apply(Camera camera, bool applyViewport) =>
            Apply(camera, applyViewport, Vector2.zero, ResponsiveDevice.FormFactor.Phone);

        /// <param name="applyViewport">
        /// When false, skips <see cref="Camera.rect"/>.
        /// </param>
        /// <param name="currentUiSize">
        /// Live UI / canvas size used when <see cref="scaleFromUiSize"/> or
        /// <see cref="scaleProjection"/> is on.
        /// </param>
        /// <param name="slotForm">Form factor for Design UI Size defaults.</param>
        /// <param name="slotOrientation">Orientation used to resolve the locked form default.</param>
        public void Apply(
            Camera camera,
            bool applyViewport,
            Vector2 currentUiSize,
            ResponsiveDevice.FormFactor slotForm,
            ResponsiveDevice.Orientation slotOrientation = ResponsiveDevice.Orientation.Portrait)
        {
            if (!enabled || camera == null)
                return;

            if (applyViewport)
                camera.rect = ClampViewport(viewport);

            Vector2 liveUi = ResolveLiveUiSize(currentUiSize, slotForm, slotOrientation);
            Vector2 designUi = UsesCanvasScale
                ? GetResolvedDesignUiSize(slotForm, slotOrientation)
                : Vector2.zero;

            if (applyLocalTransform)
            {
                camera.transform.localPosition = scaleFromUiSize
                    ? ScaleDesignPosition(
                        localPosition,
                        ResponsiveRectState.ComputeCanvasRatio(
                            liveUi, designUi, uiScaleMode))
                    : localPosition;
                camera.transform.localEulerAngles = localEulerAngles;
            }

            // Projection type is authoritative per slot (Capture / first Target assign auto-detect).
            camera.orthographic = orthographic;

            if (orthographic)
            {
                if (applyOrthographicSize)
                {
                    float ortho = orthographicSize;
                    if (scaleProjection)
                    {
                        ortho = ScaleOrthographicSize(
                            ortho, liveUi, designUi, uiScaleMode);
                    }
                    camera.orthographicSize = Mathf.Max(0.01f, ortho);
                }
            }
            else if (applyFieldOfView)
            {
                float fov = fieldOfView;
                if (scaleProjection)
                    fov = ScaleFieldOfView(fov, liveUi, designUi, uiScaleMode);
                camera.fieldOfView = Mathf.Clamp(fov, 0.1f, 179f);
            }
        }

        /// <summary>Production safe default when no slot is enabled — full-screen viewport only.</summary>
        public static void ApplyFullViewport(Camera camera)
        {
            if (camera == null)
                return;
            camera.rect = FullViewport;
        }

        #endregion

        #region Scale math

        public static Vector3 ScaleDesignPosition(Vector3 designPosition, Vector2 ratio)
        {
            if (ResponsiveRectState.IsIdentityScale(ratio))
                return designPosition;

            float rz = UniformScale(ratio);
            return new Vector3(
                designPosition.x * ratio.x,
                designPosition.y * ratio.y,
                designPosition.z * rz);
        }

        /// <summary>Inverse of <see cref="ScaleDesignPosition"/> for capture into design-space slots.</summary>
        public static Vector3 UnscaleDesignPosition(Vector3 livePosition, Vector2 ratio)
        {
            if (ResponsiveRectState.IsIdentityScale(ratio))
                return livePosition;

            float rz = UniformScale(ratio);
            return new Vector3(
                Mathf.Abs(ratio.x) < 1e-5f ? livePosition.x : livePosition.x / ratio.x,
                Mathf.Abs(ratio.y) < 1e-5f ? livePosition.y : livePosition.y / ratio.y,
                Mathf.Abs(rz) < 1e-5f ? livePosition.z : livePosition.z / rz);
        }

        /// <summary>
        /// Design → live orthographic size. Uses aspect framing from <paramref name="mode"/>,
        /// not raw pixel canvas ratio (pixel ratio would crush ortho on smaller phones).
        /// Width / IndependentXY: keep horizontal world span.
        /// Height: keep vertical world span (authored ortho).
        /// Auto: pick Width or Height from live vs design aspect (same fit as UniformMin).
        /// UniformMin: fit (larger ortho). UniformMax: cover (smaller ortho).
        /// Design size is oriented to the live size so opposite-orientation capture / fallback
        /// does not invert aspect and crush the value.
        /// </summary>
        public static float ScaleOrthographicSize(
            float designOrtho,
            Vector2 currentUiSize,
            Vector2 designUiSize,
            ResponsiveDesignScaleMode mode)
        {
            if (designOrtho <= 0f ||
                currentUiSize.x <= 0f || currentUiSize.y <= 0f ||
                designUiSize.x <= 0f || designUiSize.y <= 0f)
                return designOrtho;

            Vector2 design = ResponsiveRectState.OrientSizeToMatch(designUiSize, currentUiSize);
            float designAspect = design.x / design.y;
            float liveAspect = currentUiSize.x / currentUiSize.y;
            if (liveAspect < 1e-5f)
                return designOrtho;

            mode = ResponsiveRectState.ResolveAutoScaleMode(mode, liveAspect, designAspect);
            float widthMatched = designOrtho * (designAspect / liveAspect);
            float heightMatched = designOrtho;

            switch (mode)
            {
                case ResponsiveDesignScaleMode.Height:
                    return heightMatched;
                case ResponsiveDesignScaleMode.UniformMin:
                    return Mathf.Max(widthMatched, heightMatched);
                case ResponsiveDesignScaleMode.UniformMax:
                    return Mathf.Min(widthMatched, heightMatched);
                case ResponsiveDesignScaleMode.Width:
                case ResponsiveDesignScaleMode.IndependentXY:
                default:
                    return widthMatched;
            }
        }

        /// <summary>Inverse of <see cref="ScaleOrthographicSize"/> for capture into design space.</summary>
        public static float UnscaleOrthographicSize(
            float liveOrtho,
            Vector2 currentUiSize,
            Vector2 designUiSize,
            ResponsiveDesignScaleMode mode)
        {
            if (liveOrtho <= 0f ||
                currentUiSize.x <= 0f || currentUiSize.y <= 0f ||
                designUiSize.x <= 0f || designUiSize.y <= 0f)
                return liveOrtho;

            Vector2 design = ResponsiveRectState.OrientSizeToMatch(designUiSize, currentUiSize);
            float designAspect = design.x / design.y;
            float liveAspect = currentUiSize.x / currentUiSize.y;
            if (designAspect < 1e-5f)
                return liveOrtho;

            mode = ResponsiveRectState.ResolveAutoScaleMode(mode, liveAspect, designAspect);
            float widthUnscaled = liveOrtho * (liveAspect / designAspect);
            float heightUnscaled = liveOrtho;
            bool widthDominates = designAspect >= liveAspect;

            switch (mode)
            {
                case ResponsiveDesignScaleMode.Height:
                    return heightUnscaled;
                case ResponsiveDesignScaleMode.UniformMin:
                    return widthDominates ? widthUnscaled : heightUnscaled;
                case ResponsiveDesignScaleMode.UniformMax:
                    return widthDominates ? heightUnscaled : widthUnscaled;
                case ResponsiveDesignScaleMode.Width:
                case ResponsiveDesignScaleMode.IndependentXY:
                default:
                    return widthUnscaled;
            }
        }

        /// <summary>
        /// Design → live Field Of View. Same aspect framing as
        /// <see cref="ScaleOrthographicSize"/> (tan of half-FOV), not pixel canvas ratio.
        /// </summary>
        public static float ScaleFieldOfView(
            float designFov,
            Vector2 currentUiSize,
            Vector2 designUiSize,
            ResponsiveDesignScaleMode mode)
        {
            float tanHalf = FovToTan(designFov);
            float liveTan = ScaleOrthographicSize(tanHalf, currentUiSize, designUiSize, mode);
            return TanToFov(liveTan);
        }

        /// <summary>Inverse of <see cref="ScaleFieldOfView"/> for capture into design space.</summary>
        public static float UnscaleFieldOfView(
            float liveFov,
            Vector2 currentUiSize,
            Vector2 designUiSize,
            ResponsiveDesignScaleMode mode)
        {
            float tanHalf = FovToTan(liveFov);
            float designTan = UnscaleOrthographicSize(tanHalf, currentUiSize, designUiSize, mode);
            return TanToFov(designTan);
        }

        static float FovToTan(float fovDeg)
        {
            float halfDeg = Mathf.Clamp(fovDeg * 0.5f, 0.05f, 89.9f);
            return Mathf.Tan(halfDeg * Mathf.Deg2Rad);
        }

        static float TanToFov(float tanHalf)
        {
            if (tanHalf <= 0f)
                return 0.1f;
            return Mathf.Atan(tanHalf) * 2f * Mathf.Rad2Deg;
        }

        public static float UniformScale(Vector2 ratio) =>
            Mathf.Approximately(ratio.x, ratio.y) ? ratio.x : (ratio.x + ratio.y) * 0.5f;

        public static Rect ClampViewport(Rect rect)
        {
            float x = Mathf.Clamp01(rect.x);
            float y = Mathf.Clamp01(rect.y);
            float w = Mathf.Clamp(rect.width, 0f, 1f - x);
            float h = Mathf.Clamp(rect.height, 0f, 1f - y);
            // Degenerate authored rects → full screen (avoids a blank camera on device).
            if (w <= 0f || h <= 0f)
                return FullViewport;
            return new Rect(x, y, w, h);
        }

        #endregion
    }

}
