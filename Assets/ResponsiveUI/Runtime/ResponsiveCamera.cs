using System;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.UI;

namespace FDPlus.ResponsiveUI
{
    /// <summary>
    /// Production camera layouts for Phone / Pad / Fold Cover / Fold Half Opened /
    /// Fold Cover Half Opened / Flip Cover / Flip Half Opened / iPhone Duo Cover / iPhone Duo Seated /
    /// Wide Screen ×
    /// Portrait / Landscape. Layout key comes from the Canvas <see cref="ResponsiveUI"/> hub.
    /// </summary>
    [ExecuteAlways]
    public class ResponsiveCamera : MonoBehaviour, IResponsiveUiClient
    {
        #region Serialized

        [Tooltip("ResponsiveUI on the parent Canvas. Auto-assigns when you add this component.")]
        [SerializeField] ResponsiveUI responsiveUi;

        [Tooltip(
            "Optional UI rect used for Scale From UI Size (width×height). " +
            "When unset, uses Ui Size Canvas root, then Screen.")]
        [SerializeField] RectTransform uiSizeRect;

        [Tooltip("Optional Canvas whose root rect drives Scale From UI Size when Ui Size Rect is unset.")]
        [SerializeField] Canvas uiSizeCanvas;

        [SerializeField] ResponsiveCameraLayout[] cameras;

        #endregion

        #region Runtime state

        static readonly ProfilerMarker FocusWatchMarker =
            new ProfilerMarker("ResponsiveUI.Camera.FocusWatch");

        RectTransform _cachedUiRect;
        CanvasScaler _cachedScaler;
        bool _hasCachedScaler;
        [NonSerialized] readonly Vector3[] _focusBoundsCorners = new Vector3[4];
        [NonSerialized] ResponsiveCameraFocus.TargetBoundsCache[] _focusBoundsCaches =
            Array.Empty<ResponsiveCameraFocus.TargetBoundsCache>();
        int _focusBoundsGeometryHash;
        int _focusTargetGeometryHash;
        bool _hasFocusBoundsGeometryHash;
        ResponsiveUI _hub;
#if UNITY_EDITOR
        [NonSerialized] bool _seedDefaultLayout;
        int _focusRenderGeometryHash;
        bool _hasFocusRenderGeometryHash;

        /// <summary>
        /// Editor camera sync assigns these so hub / Simulator / Design Canvas Apply
        /// does not recapture live (already scaled or focused) cameras into authored slots.
        /// </summary>
        public static Action EditorBeginApply;
        public static Action EditorEndApply;
        public static Action<Camera> EditorIgnoreTargetCapture;
#endif

        /// <summary>Camera Targets driven by this component.</summary>
        public ResponsiveCameraLayout[] Cameras => cameras;

        public ResponsiveUI Hub => ResponsiveUiHub.Resolve(responsiveUi, this);

        #endregion

        #region Lifecycle

        void Reset()
        {
            enabled = false;
            AssignHub(ResponsiveUiHub.Resolve(null, this));
#if UNITY_EDITOR
            _seedDefaultLayout = true;
#endif
        }

        void OnEnable()
        {
            InvalidateUiSizeCache();
            BindHub(true);
            ApplyNow(layoutOnly: false);
            CaptureFocusBoundsGeometry();
        }

        void OnDisable()
        {
            InvalidateUiSizeCache();
            _hasFocusBoundsGeometryHash = false;
#if UNITY_EDITOR
            _hasFocusRenderGeometryHash = false;
#endif
            BindHub(false);
        }

        void OnTransformParentChanged()
        {
            InvalidateUiSizeCache();
            BindHub(false);
            if (!isActiveAndEnabled)
                return;
            BindHub(true);
            ApplyNow(layoutOnly: false);
            CaptureFocusBoundsGeometry();
        }

        void LateUpdate()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                RefreshEditorFocusRenderGeometry();
                return;
            }
#endif
            RefreshRuntimeFocusGeometry();
        }

        /// <summary>
        /// Play Mode watch. Inputs only: Focus Bounds HUD rect, camera render size /
        /// viewport, and Focus Target bounds. Camera pose and projection are Apply
        /// outputs and are not watched, so a script that moves the camera is not
        /// snapped back every frame. HUD / render-size changes run the full layout-only
        /// pass (Bounds Layout + canvas settle); target-only motion re-frames without
        /// re-laying the HUD or forcing a canvas rebuild.
        /// </summary>
        void RefreshRuntimeFocusGeometry()
        {
            using var _ = FocusWatchMarker.Auto();
            int hash = ComputeFocusGeometryHashes(
                out int targetHash,
                out bool hasGeometry);
            if (!hasGeometry)
            {
                _hasFocusBoundsGeometryHash = false;
                return;
            }

            if (!_hasFocusBoundsGeometryHash)
            {
                _focusBoundsGeometryHash = hash;
                _focusTargetGeometryHash = targetHash;
                _hasFocusBoundsGeometryHash = true;
                return;
            }

            bool layoutChanged = _focusBoundsGeometryHash != hash;
            bool targetChanged = _focusTargetGeometryHash != targetHash;
            if (!layoutChanged && !targetChanged)
                return;

            _focusBoundsGeometryHash = hash;
            _focusTargetGeometryHash = targetHash;
            _hasFocusBoundsGeometryHash = true;
            if (layoutChanged)
                ApplyNow(layoutOnly: true);
            else
                ApplyFocusTargetMotion();
        }

        /// <summary>
        /// Focus Target moved while the HUD and render size stayed put: re-frame the
        /// cameras without Bounds Layout writes or <see cref="Canvas.ForceUpdateCanvases"/>.
        /// </summary>
        void ApplyFocusTargetMotion()
        {
            if (!isActiveAndEnabled || cameras == null || cameras.Length == 0)
                return;
            ResponsiveUI hub = _hub != null ? _hub : Hub;
            if (hub == null || !hub.enabled || hub.ApplyInFlight)
                return;
            ApplyCameras(hub.CurrentLayout, applyBoundsLayout: false, settleCanvases: false);
        }

#if UNITY_EDITOR
        void RefreshEditorFocusRenderGeometry()
        {
            int hash = ComputeFocusRenderGeometryHash(out bool hasFocus);
            if (!hasFocus)
            {
                _hasFocusRenderGeometryHash = false;
                return;
            }

            if (!_hasFocusRenderGeometryHash)
            {
                _focusRenderGeometryHash = hash;
                _hasFocusRenderGeometryHash = true;
                return;
            }

            if (_focusRenderGeometryHash == hash)
                return;

            _focusRenderGeometryHash = hash;
            ApplyNow(layoutOnly: true);
        }
#endif

#if UNITY_EDITOR
        void OnValidate()
        {
            InvalidateUiSizeCache();
            if (responsiveUi == null)
                responsiveUi = ResponsiveUiHub.Resolve(null, this);
            if (!isActiveAndEnabled || ResponsiveUiHub.Resolve(responsiveUi, this) == _hub)
                return;
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this == null || !isActiveAndEnabled)
                    return;
                AssignHub(responsiveUi);
            };
        }

        public bool ConsumeSeedDefaultLayout()
        {
            bool seed = _seedDefaultLayout;
            _seedDefaultLayout = false;
            return seed;
        }
#endif

        #endregion

        #region Hub

        public void AssignHub(ResponsiveUI hub)
        {
            responsiveUi = hub;
            if (!isActiveAndEnabled)
                return;
            if (!ResponsiveUiHub.RebindIfChanged(this, this, ref responsiveUi, ref _hub))
                return;
            ApplyNow(layoutOnly: false);
        }

#if UNITY_EDITOR
        public ResponsiveDevice.LayoutKey GetEditorCaptureLayout()
        {
            ResponsiveUI hub = Hub;
            if (hub != null)
                return hub.GetEditorCaptureLayout();
            Vector2 watch = ResponsiveDevice.GetWatchScreenSize();
            return ResponsiveDevice.ResolveLiveLayout(watch);
        }

        public void NotifyWidgetLivePreview()
        {
            ResponsiveUI hub = Hub;
            if (hub != null)
                hub.NotifyWidgetLivePreview();
        }
#endif

        void BindHub(bool bind) =>
            ResponsiveUiHub.BindClient(this, this, ref responsiveUi, ref _hub, bind);

        void ApplyNow(bool layoutOnly) => ResponsiveUiHub.ApplyNow(this, layoutOnly);

        #endregion

        #region IResponsiveUiClient

        public void ApplyFromHub(
            ResponsiveUI hub,
            in ResponsiveDevice.LayoutKey layout,
            bool layoutOnly)
        {
            if (!isActiveAndEnabled ||
                !hub.enabled ||
                cameras == null ||
                cameras.Length == 0)
                return;
            if (!ResponsiveUiHub.BeginApplyFromHub(
                    this, this, hub, ref responsiveUi, ref _hub, layoutOnly))
                return;

            ApplyCameras(layout);
        }

        void ApplyCameras(
            in ResponsiveDevice.LayoutKey layout,
            bool applyBoundsLayout = true,
            bool settleCanvases = true)
        {
#if UNITY_EDITOR
            EditorBeginApply?.Invoke();
            try
            {
#endif
                Vector2 uiSize = GetUiLayoutSize();
                ResponsiveUI hub = Hub;

                // Each row writes Camera.rect for its Target (a row without one is skipped).
                for (int i = 0; i < cameras.Length; i++)
                {
                    cameras[i].Apply(
                        layout,
                        applyViewport: true,
                        uiSize,
                        applyBoundsLayout,
                        hub,
                        settleCanvases);
                }
                CaptureFocusBoundsGeometry();
#if UNITY_EDITOR
            }
            finally
            {
                for (int i = 0; i < cameras.Length; i++)
                    EditorIgnoreTargetCapture?.Invoke(cameras[i].target);
                EditorEndApply?.Invoke();
            }
#endif
        }

        /// <summary>
        /// Play Mode same-form size ticks: re-apply when UI scale, sprite background,
        /// or focus needs settled geometry. Scale From UI Size / Scale Projection
        /// still re-apply under a Scale With Screen Size canvas (live size is watch).
        /// Simulator canvas-ready always notifies this client.
        /// </summary>
        public bool SizeChangeRequiresReapply()
        {
            if (cameras == null || cameras.Length == 0)
                return false;

            ResponsiveUI hub = _hub != null ? _hub : Hub;
            ResponsiveDevice.LayoutKey layout = hub != null
                ? hub.CurrentLayout
                : default;
            for (int i = 0; i < cameras.Length; i++)
            {
                ref ResponsiveCameraLayout camera = ref cameras[i];
                camera.ResolveActiveSlot(
                    layout,
                    out _,
                    out _,
                    out ResponsiveCameraState state);
                bool needsUiScale = state.UsesCanvasScale;
                bool hasBoundsLayout =
                    state.focusBounds != null &&
                    state.focusBoundsLayout.enabled;
                if (needsUiScale || camera.HasBackground() || state.HasFocus() || hasBoundsLayout)
                    return true;
            }

            return false;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Applies the supplied authoring layout directly to this component's
        /// camera and sprite targets without notifying unrelated hub clients.
        /// </summary>
        public void EditorApplyCameras(
            in ResponsiveDevice.LayoutKey layout,
            bool applyBoundsLayout = true)
        {
            if (!isActiveAndEnabled || cameras == null || cameras.Length == 0)
                return;
            ApplyCameras(layout, applyBoundsLayout);
        }

        public bool IsAuthoringSelectedTarget()
        {
            if (cameras == null || cameras.Length == 0)
                return false;

            UnityEngine.Object[] selected = UnityEditor.Selection.objects;
            if (selected == null || selected.Length == 0)
                return false;

            for (int i = 0; i < cameras.Length; i++)
            {
                ref ResponsiveCameraLayout camera = ref cameras[i];
                Camera target = camera.target;
                if (target != null)
                {
                    for (int s = 0; s < selected.Length; s++)
                    {
                        if (GetEditorCamera(selected[s], out Camera cam) && cam == target)
                            return true;
                    }
                }

                SpriteRenderer background = camera.background;
                if (background != null)
                {
                    for (int s = 0; s < selected.Length; s++)
                    {
                        if (GetEditorSprite(selected[s], out SpriteRenderer sr) &&
                            sr == background)
                            return true;
                    }
                }
            }

            ResponsiveDevice.LayoutKey activeLayout = GetEditorCaptureLayout();
            for (int s = 0; s < selected.Length; s++)
            {
                if (!ResponsiveUI.GetEditorRectTransform(
                        selected[s],
                        out RectTransform bounds))
                    continue;
                for (int i = 0; i < cameras.Length; i++)
                {
                    if (cameras[i].ActiveSlotUsesFocusBoundsRect(bounds, activeLayout))
                        return true;
                }
            }

            return false;
        }

        /// <summary>Edit Mode: true when <paramref name="camera"/> is assigned as a Cameras Target.</summary>
        public bool EditorOwnsCameraTarget(Camera camera)
        {
            if (camera == null || cameras == null || cameras.Length == 0)
                return false;

            for (int i = 0; i < cameras.Length; i++)
            {
                if (cameras[i].target == camera)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Edit Mode: true when <paramref name="bounds"/> is assigned as Focus Bounds
        /// on any slot. Authoring does not require Focus Target / Screen.
        /// </summary>
        public bool EditorUsesFocusBounds(RectTransform bounds)
        {
            if (bounds == null || cameras == null || cameras.Length == 0)
                return false;

            for (int i = 0; i < cameras.Length; i++)
            {
                if (cameras[i].UsesFocusBoundsRect(bounds))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Samples the live Focus Bounds HUD into the active Simulator slot.
        /// Other slots keep their own Bounds Layout.
        /// </summary>
        public bool EditorCaptureActiveFocusBounds(in ResponsiveDevice.LayoutKey key)
        {
            if (cameras == null)
                return false;

            Vector2 uiSize = GetUiLayoutSize();
            ResponsiveUI hub = Hub;
            bool captured = false;
            for (int i = 0; i < cameras.Length; i++)
            {
                ref ResponsiveCameraLayout layout = ref cameras[i];
                captured |= layout.CaptureActiveFocusBoundsLayout(key, uiSize, hub);
            }

            if (captured)
                UnityEditor.EditorUtility.SetDirty(this);
            CaptureFocusBoundsGeometry();
            return captured;
        }

        /// <summary>Edit Mode: resolve a Selection target to a <see cref="Camera"/>.</summary>
        public static bool GetEditorCamera(UnityEngine.Object selected, out Camera camera)
        {
            switch (selected)
            {
                case Camera cam:
                    camera = cam;
                    return true;
                case GameObject go:
                    camera = go.GetComponent<Camera>();
                    return camera != null;
                case Component component:
                    camera = component as Camera;
                    if (camera == null)
                        camera = component.GetComponent<Camera>();
                    return camera != null;
                default:
                    camera = null;
                    return false;
            }
        }

        /// <summary>Edit Mode: resolve a Selection target to a <see cref="SpriteRenderer"/>.</summary>
        public static bool GetEditorSprite(UnityEngine.Object selected, out SpriteRenderer sprite)
        {
            switch (selected)
            {
                case SpriteRenderer sr:
                    sprite = sr;
                    return true;
                case GameObject go:
                    sprite = go.GetComponent<SpriteRenderer>();
                    return sprite != null;
                case Component component:
                    sprite = component as SpriteRenderer;
                    if (sprite == null)
                        sprite = component.GetComponent<SpriteRenderer>();
                    return sprite != null;
                default:
                    sprite = null;
                    return false;
            }
        }
#endif

        #endregion

        #region UI size

        /// <summary>
        /// Live UI / canvas size for Scale From UI Size and Scale Projection.
        /// Prefers Screen / Device Simulator size when the UI source uses Scale With Screen Size
        /// or no scaler is found — CanvasScaler Expand inflates canvas height (e.g. Fold Cover
        /// on phone refs) and breaks Height-mode projection / position. Constant Pixel Size
        /// still uses the canvas rect. Falls back to the rect size, then Screen.
        /// </summary>
        public Vector2 GetUiLayoutSize()
        {
            Vector2 watch = ResponsiveDevice.GetWatchScreenSize();
            RectTransform rt = ResolveUiSizeRect();
            CanvasScaler scaler = rt != null ? ResolveCanvasScaler(rt) : null;
            Vector2 canvas = rt != null ? rt.rect.size : Vector2.zero;
            bool scaleWithScreen =
                scaler != null &&
                scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize;
            return ResolveUiLayoutSize(watch, canvas, scaler != null, scaleWithScreen);
        }

        /// <summary>
        /// Testable live-size pick for camera scale. Watch / Simulator wins when there is
        /// no scaler or the scaler is Scale With Screen Size. Non-SWS canvases keep rect size.
        /// </summary>
        public static Vector2 ResolveUiLayoutSize(
            Vector2 watch,
            Vector2 canvasRectSize,
            bool hasScaler,
            bool scaleWithScreenSize)
        {
            bool preferWatch = !hasScaler || scaleWithScreenSize;
            if (preferWatch && watch.x > 0f && watch.y > 0f)
                return watch;
            if (canvasRectSize.x > 0f && canvasRectSize.y > 0f)
                return canvasRectSize;
            return watch;
        }

        RectTransform ResolveUiSizeRect()
        {
            if (uiSizeRect != null)
                return uiSizeRect;

            if (uiSizeCanvas != null)
                return uiSizeCanvas.rootCanvas.transform as RectTransform;

            if (_cachedUiRect != null)
                return _cachedUiRect;

            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
                return null;

            _cachedUiRect = canvas.rootCanvas.transform as RectTransform;
            return _cachedUiRect;
        }

        CanvasScaler ResolveCanvasScaler(RectTransform rt)
        {
            if (_hasCachedScaler)
                return _cachedScaler;

            if (uiSizeCanvas != null)
            {
                CanvasScaler onCanvas = uiSizeCanvas.GetComponent<CanvasScaler>();
                if (onCanvas != null)
                {
                    _cachedScaler = onCanvas;
                    _hasCachedScaler = true;
                    return _cachedScaler;
                }
            }

            if (rt == null)
            {
                _hasCachedScaler = true;
                return null;
            }

            // Explicit Unity null checks: Editor GetComponent returns a fake-null
            // object that `??` would treat as a hit, skipping the parent lookup.
            CanvasScaler scaler = rt.GetComponent<CanvasScaler>();
            if (scaler == null)
                scaler = rt.GetComponentInParent<CanvasScaler>();
            _cachedScaler = scaler != null ? scaler : null;
            _hasCachedScaler = true;
            return _cachedScaler;
        }

        void InvalidateUiSizeCache()
        {
            _cachedUiRect = null;
            _cachedScaler = null;
            _hasCachedScaler = false;
        }

        #endregion

        #region Focus bounds watch

        void CaptureFocusBoundsGeometry()
        {
            _focusBoundsGeometryHash = ComputeFocusGeometryHashes(
                out _focusTargetGeometryHash,
                out bool hasGeometry);
            _hasFocusBoundsGeometryHash = hasGeometry;
#if UNITY_EDITOR
            _focusRenderGeometryHash = ComputeFocusRenderGeometryHash(out bool hasFocus);
            _hasFocusRenderGeometryHash = hasFocus;
#endif
        }

        /// <summary>
        /// Returns the Focus Bounds HUD + camera render-size hash; <paramref name="targetHash"/>
        /// receives the Focus Target bounds hash.
        /// </summary>
        int ComputeFocusGeometryHashes(out int targetHash, out bool hasGeometry)
        {
            hasGeometry = false;
            targetHash = 17;
            if (cameras == null || cameras.Length == 0)
                return 0;

            ResponsiveDevice.LayoutKey activeLayout = GetActiveLayout();
            int hash = 17;
            unchecked
            {
                for (int i = 0; i < cameras.Length; i++)
                {
                    ref ResponsiveCameraLayout layout = ref cameras[i];
                    if (!layout.ResolveActiveSlot(
                            activeLayout,
                            out _,
                            out _,
                            out ResponsiveCameraState state))
                        continue;
                    HashFocusBounds(
                        layout.target,
                        layout.background,
                        state,
                        GetFocusBoundsCache(i),
                        ref hash,
                        ref targetHash,
                        ref hasGeometry);
                }
            }

            return hash;
        }

#if UNITY_EDITOR
        int ComputeFocusRenderGeometryHash(out bool hasFocus)
        {
            hasFocus = false;
            if (cameras == null || cameras.Length == 0)
                return 0;

            ResponsiveDevice.LayoutKey activeLayout = GetActiveLayout();
            int hash = 17;
            unchecked
            {
                for (int i = 0; i < cameras.Length; i++)
                {
                    ref ResponsiveCameraLayout layout = ref cameras[i];
                    if (!layout.ResolveActiveSlot(
                            activeLayout,
                            out _,
                            out _,
                            out ResponsiveCameraState state) ||
                        !state.HasFocus() ||
                        layout.target == null)
                        continue;

                    Camera camera = layout.target;
                    hasFocus = true;
                    hash = hash * 31 + camera.GetInstanceID();
                    hash = hash * 31 + camera.pixelWidth;
                    hash = hash * 31 + camera.pixelHeight;
                    hash = hash * 31 + camera.aspect.GetHashCode();
                }
            }

            return hash;
        }
#endif

        ResponsiveDevice.LayoutKey GetActiveLayout()
        {
            ResponsiveUI hub = _hub != null ? _hub : Hub;
            return hub != null ? hub.CurrentLayout : default;
        }

        void HashFocusBounds(
            Camera camera,
            SpriteRenderer ignoreBackground,
            in ResponsiveCameraState state,
            ResponsiveCameraFocus.TargetBoundsCache boundsCache,
            ref int hash,
            ref int targetHash,
            ref bool hasFocusBounds)
        {
            RectTransform bounds = state.focusBounds;
            ResponsiveCameraFocusScreen screen = state.focusScreen;
            bool focusUsesBounds =
                state.HasFocus() &&
                (screen == ResponsiveCameraFocusScreen.Auto ||
                 screen == ResponsiveCameraFocusScreen.Bounds);
            bool ownsBoundsLayout =
                bounds != null &&
                state.focusBoundsLayout.enabled;
            if (bounds != null && (focusUsesBounds || ownsBoundsLayout))
            {
                hasFocusBounds = true;
                bounds.GetWorldCorners(_focusBoundsCorners);
                hash = hash * 31 + bounds.GetInstanceID();
                hash = hash * 31 + (bounds.gameObject.activeInHierarchy ? 1 : 0);
                for (int c = 0; c < _focusBoundsCorners.Length; c++)
                    HashVector(_focusBoundsCorners[c], ref hash);
            }

            if (!state.HasFocus())
                return;

            hasFocusBounds = true;
            targetHash = targetHash * 31 + state.focusTarget.GetInstanceID();
            if (boundsCache.GetBounds(
                    state.focusTarget,
                    ignoreBackground,
                    out Bounds targetBounds))
            {
                HashVector(targetBounds.center, ref targetHash);
                HashVector(targetBounds.extents, ref targetHash);
            }
            else
                HashVector(state.focusTarget.position, ref targetHash);

            if (camera == null)
                return;
            // Render size / viewport only. Pose and ortho / FOV are Apply outputs;
            // watching them would re-apply (and fight) external camera scripts.
            hash = hash * 31 + camera.GetInstanceID();
            hash = hash * 31 + camera.rect.GetHashCode();
            hash = hash * 31 + camera.pixelWidth;
            hash = hash * 31 + camera.pixelHeight;
            hash = hash * 31 + camera.aspect.GetHashCode();
        }

        ResponsiveCameraFocus.TargetBoundsCache GetFocusBoundsCache(int index)
        {
            int count = cameras != null ? cameras.Length : 0;
            if (_focusBoundsCaches.Length != count)
            {
                var next =
                    new ResponsiveCameraFocus.TargetBoundsCache[count];
                int copy = Mathf.Min(_focusBoundsCaches.Length, count);
                for (int i = 0; i < copy; i++)
                    next[i] = _focusBoundsCaches[i];
                _focusBoundsCaches = next;
            }

            return _focusBoundsCaches[index] ??=
                new ResponsiveCameraFocus.TargetBoundsCache();
        }

        static void HashVector(Vector3 value, ref int hash)
        {
            hash = hash * 31 + value.x.GetHashCode();
            hash = hash * 31 + value.y.GetHashCode();
            hash = hash * 31 + value.z.GetHashCode();
        }

        #endregion
    }
}
