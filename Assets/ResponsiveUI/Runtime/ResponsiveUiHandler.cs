using System;
using System.Collections;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.UI;
using FDPlus.Foldable;

namespace FDPlus.ResponsiveUI
{
    /// <summary>
    /// Shared form-factor detection for the Canvas <see cref="ResponsiveUI"/> hub.
    /// Works in the Unity Editor (Game View / Device Simulator) and on real devices:
    /// size / orientation changes, fold posture <c>LayoutChanged</c>, and app focus/resume
    /// re-run detection; the hub then notifies <see cref="IResponsiveUiClient"/> subscribers.
    /// Forms follow the build target (<see cref="ResponsivePlatforms"/>): Android Phone / Pad /
    /// Fold / Flip; iOS Phone / Pad / iPhone Duo Cover / Seated; Windows / macOS / Linux / WebGL
    /// Wide Screen only, sized from the window, with no mobile detection.
    /// Posture comes from <see cref="FoldablePostureSource"/> / <see cref="FoldablePostureDetector"/>:
    /// Fold Expand maps to Pad, Fold Normal maps to Phone, Fold8 Cover WxH maps to FoldPhoneCover,
    /// Flip Closed maps to FlipPhoneCover, Flip Opened maps to Phone, iPhone Duo Closed maps to
    /// iPhoneDuoCover, and HalfOpened postures use dedicated widget slots.
    /// </summary>
    [ExecuteAlways]
    public class ResponsiveUiHandler : MonoBehaviour
    {
        #region Serialized

        [Header("Detection (Editor)")]
        [Tooltip("When enabled, locks form factor and Portrait/Landscape instead of auto-detecting.")]
        [SerializeField] bool overrideInEditor;
        [Tooltip("Forced form factor when Override In Editor is on (the build target's forms).")]
        [SerializeField] ResponsiveDevice.FormFactor editorFormFactor = ResponsiveDevice.FormFactor.Phone;
        [Tooltip("Forced orientation when Override In Editor is on.")]
        [SerializeField] ResponsiveDevice.Orientation editorOrientation = ResponsiveDevice.Orientation.Portrait;
        [Tooltip("Game View aspect (max/min) below this counts as Pad in Editor. Phones are typically taller (higher aspect).")]
        [SerializeField] float editorPadMaxAspect = ResponsiveDevice.DefaultPadMaxAspect;
        [Tooltip(
            "When off (default), Inspector lists every slot of the build target's forms. Auto-expand still " +
            "opens the detected one; you can open the others too. When on, only the detected slot is shown.")]
        [SerializeField] bool hideOtherSlots;

        [Header("Optional Canvas Scaler")]
        [Tooltip(
            "Applies Phone / Pad / Wide Screen reference resolutions on this scaler " +
            "(Pad also for Fold HalfOpened, Fold Cover HalfOpened, iPhone Duo Seated; Wide Screen uses its own). " +
            "Scale With Screen Size fits the canvas; Design Scale still applies Width/Height/Auto/IndependentXY as a canvas-local residual.")]
        [SerializeField] bool applyCanvasScaler;
        [SerializeField] CanvasScaler canvasScaler;
        [SerializeField] Vector2 phoneReferenceResolution = ResponsiveRectState.BuiltinPhoneDesignCanvasSize;
        [SerializeField] Vector2 padReferenceResolution = ResponsiveRectState.BuiltinPadDesignCanvasSize;
        [SerializeField][Range(0f, 1f)] float phoneMatchWidthOrHeight = 0.5f;
        [SerializeField][Range(0f, 1f)] float padMatchWidthOrHeight = 0.5f;
        [Tooltip(
            "Wide Screen (Windows / macOS / Linux / WebGL) reference, stored portrait like Phone / Pad " +
            "(Landscape uses it rotated). Defaults to the built-in Wide Screen canvas (1080×1920; 1920×1080 " +
            "landscape). Seeding a Wide Screen slot from Pad in the Editor also copies the Pad refs here " +
            "while these are still the default, so the Pad coordinates match.")]
        [SerializeField] Vector2 wideScreenReferenceResolution = ResponsiveRectState.BuiltinWideScreenDesignCanvasSize;
        [SerializeField][Range(0f, 1f)] float wideScreenMatchWidthOrHeight = 0.5f;

        [Header("Events")]
        [SerializeField] ResponsiveLayoutEvents layoutEvents;

        [Header("Debug")]
        [SerializeField] bool logDetection;

        #endregion

        #region Runtime state

        static readonly ProfilerMarker ApplyMarker =
            new ProfilerMarker("ResponsiveUI.Hub.Apply");
        static readonly ProfilerMarker LayoutRefreshMarker =
            new ProfilerMarker("ResponsiveUI.Hub.LayoutRefresh");

        Vector2 _lastWatchSize;
        Vector2 _lastSceneLayoutSize;
        bool _postLoadApplyQueued;
        bool _hasAppliedLayout;
        int _applyInFlightDepth;
        bool _applyDeferred;
        const int MaxDeferredApplyPasses = 4;
        bool _layoutRefreshRequested;
        ResponsiveDevice.LayoutKey _lastAppliedLayout;
        // Layout events: first fired from Start (after every scene Awake / OnEnable), then
        // only when the applied layout changes.
        bool _started;
        bool _hasInvokedLayoutEvents;
        ResponsiveDevice.LayoutKey _lastEventLayout;

#if UNITY_EDITOR
        bool _validateApplyQueued;
        bool _postEnableLayoutQueued;
        bool _skipNextValidateApply;
        bool _hasEditorSimulatorLayout;
        ResponsiveDevice.LayoutKey _editorSimulatorLayout;
#endif

        #endregion

        #region Current layout

        public ResponsiveDevice.FormFactor CurrentFormFactor { get; private set; }
        public ResponsiveDevice.Orientation CurrentOrientation { get; private set; }
        public ResponsiveDevice.LayoutKey CurrentLayout =>
            new ResponsiveDevice.LayoutKey(CurrentFormFactor, CurrentOrientation);

        /// <summary>
        /// True while this hub is applying layout (full Apply or scene-layout notify).
        /// Client OnEnable self-apply skips so the in-flight notify is the single Apply
        /// for a newly registered client (including form-transition loading activate).
        /// </summary>
        public bool ApplyInFlight => _applyInFlightDepth > 0;

        /// <summary>
        /// True when this hub's canvas already performs resolution fitting.
        /// Widget Design Scale then uses the canvas local rect instead of Screen so
        /// CanvasScaler and per-slot Width/Height/IndependentXY do not double-count.
        /// </summary>
        public bool UsesCanvasScalerResolutionFit =>
            canvasScaler != null &&
            canvasScaler.enabled &&
            (applyCanvasScaler ||
             canvasScaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize);

        /// <summary>
        /// Includes a Scale With Screen Size scaler owned by a nested target Canvas.
        /// </summary>
        public bool UsesCanvasScalerResolutionFitFor(RectTransform target)
        {
            if (UsesCanvasScalerResolutionFit)
                return true;
            if (target == null)
                return false;

            return FindParentScaleWithScreenSizeCanvas(target, out _) != null;
        }

        /// <summary>
        /// Nearest parent Canvas with an enabled Scale With Screen Size scaler, or null.
        /// <paramref name="nearestCanvas"/> receives the first parent Canvas found (any scaler).
        /// Parent first: a Canvas (and its inert non-root scaler) on the Target itself is not
        /// the Target's layout space (Unity ignores CanvasScaler on non-root canvases).
        /// </summary>
        internal static Canvas FindParentScaleWithScreenSizeCanvas(
            RectTransform target,
            out Canvas nearestCanvas)
        {
            nearestCanvas = null;
            if (target == null)
                return null;

            for (Transform current = target.parent;
                 current != null;
                 current = current.parent)
            {
                if (!current.TryGetComponent(out Canvas canvas))
                    continue;
                if (nearestCanvas == null)
                    nearestCanvas = canvas;
                if (canvas.TryGetComponent(out CanvasScaler scaler) &&
                    scaler.enabled &&
                    scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize)
                    return canvas;
            }

            return null;
        }

        #endregion

        #region Editor capture

#if UNITY_EDITOR
        /// <summary>
        /// Editor tools subscribe so newly enabled handlers receive the focused Simulator
        /// layout immediately (disabled objects are skipped by the global handler refresh).
        /// Editor-installed hook: intentionally not reset on <c>SubsystemRegistration</c>.
        /// </summary>
        public static Action<ResponsiveUiHandler> EditorOnHandlerEnabled;

        /// <summary>
        /// Client Inspectors read these for every slot draw; reading the field avoids a
        /// new hub SerializedObject per slot per GUI event.
        /// </summary>
        public bool EditorOverrideInEditor => overrideInEditor;

        public bool EditorHideOtherSlots => hideOtherSlots;

        /// <summary>
        /// Override In Editor form as applied: the serialized value constrained to the build
        /// target (<see cref="ResponsivePlatforms.ConstrainDetected"/>), so a Phone override on a
        /// Wide Screen target lays out Wide Screen.
        /// </summary>
        public ResponsiveDevice.FormFactor EditorFormFactor =>
            ResponsivePlatforms.ConstrainDetected(editorFormFactor);

        /// <summary>
        /// Skip the next queued validate Apply (LivePush / capture just wrote intentionally).
        /// </summary>
        public void NotifyWidgetLivePreview() => _skipNextValidateApply = true;

        /// <summary>
        /// Layout key for editor widget capture. Does not touch global
        /// <see cref="ResponsiveDevice"/> override (safe with multiple handlers in one scene).
        /// </summary>
        public ResponsiveDevice.LayoutKey GetEditorCaptureLayout()
        {
            if (overrideInEditor)
                return new ResponsiveDevice.LayoutKey(EditorFormFactor, editorOrientation);
            if (!Application.isPlaying &&
                ResponsiveDevice.EditorPreviewLayout(out ResponsiveDevice.LayoutKey preview))
                return preview;
            if (_hasEditorSimulatorLayout)
                return _editorSimulatorLayout;

            Vector2 watch = GetWatchSize();
            ResponsiveDevice.FormFactor form =
                ResponsiveDevice.DetectActiveFormFactor(watch, editorPadMaxAspect);
            return new ResponsiveDevice.LayoutKey(
                form,
                ResponsiveDevice.DetectOrientationForForm(form, watch.x, watch.y));
        }
#endif

        #endregion

        #region Lifecycle

        protected virtual void OnEnable()
        {
            FoldablePostureSource.LayoutChanged += OnFoldableLayoutChanged;
            FoldablePostureSource.ActiveInstanceChanged += OnActiveFoldableChanged;
            // Sync after bind — Foldable may already have a posture (LayoutChanged can fire
            // before we subscribe), and disabled→enabled must re-detect.
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                // Drop stale LivePush suppress from while disabled (OnValidate never queued).
                _skipNextValidateApply = false;
                // Pull focused Simulator layout before Apply; global refresh skips inactive handlers.
                if (EditorOnHandlerEnabled != null)
                {
                    EditorOnHandlerEnabled.Invoke(this);
                    return;
                }

                Apply();
                Canvas.ForceUpdateCanvases();
                QueuePostEnableSceneLayout();
                return;
            }
#endif
            Apply();
        }

        protected virtual void OnDisable()
        {
            FoldablePostureSource.LayoutChanged -= OnFoldableLayoutChanged;
            FoldablePostureSource.ActiveInstanceChanged -= OnActiveFoldableChanged;
            _hasAppliedLayout = false;
            _hasInvokedLayoutEvents = false;
            _layoutRefreshRequested = false;
            _applyDeferred = false;
        }

        protected virtual void Start()
        {
            if (!Application.isPlaying)
                return;

            // Scene load / first Play frame: detect Screen + fold posture, then apply the
            // matching form-factor × Portrait/Landscape widget slot.
            _started = true;
            ResponsiveDevice.NotifyDisplayChanged();
            Canvas.ForceUpdateCanvases();
            Apply();
            // Canvas / FoldablePostureDetector often settle one frame after load.
            QueuePlayModePostLoadApply();
        }

        void QueuePlayModePostLoadApply()
        {
            if (_postLoadApplyQueued)
                return;

            _postLoadApplyQueued = true;
            StartCoroutine(PlayModePostLoadApply());
        }

        IEnumerator PlayModePostLoadApply()
        {
            yield return null;
            _postLoadApplyQueued = false;
            if (this == null || !isActiveAndEnabled || !Application.isPlaying)
                yield break;

            ResponsiveDevice.NotifyDisplayChanged();
            Canvas.ForceUpdateCanvases();
            Apply();
        }

        protected virtual void LateUpdate()
        {
            // Edit Mode refresh is event-driven by editor tools; Play Mode uses this.
            if (!Application.isPlaying)
                return;
            RefreshLayoutIfWatchSizeChanged();
            if (_layoutRefreshRequested)
                RefreshRequestedLayout();
        }

        /// <summary>
        /// Resume / focus: re-detect orientation and fold posture on real devices and Editor Play.
        /// iOS and Android both raise focus on resume (after <c>OnApplicationPause(false)</c>), so
        /// one handler applies once. A resumed-but-unfocused Android multi-window pane is still
        /// caught by the LateUpdate size watch and fold posture events.
        /// </summary>
        protected virtual void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus || !Application.isPlaying || !isActiveAndEnabled)
                return;
            ResponsiveDevice.NotifyDisplayChanged();
            Apply();
        }

        #endregion

        #region Editor apply queue

#if UNITY_EDITOR
        protected virtual void OnValidate()
        {
            if (!isActiveAndEnabled)
                return;

            QueueEditorApply();
        }

        void QueueEditorApply()
        {
            // Already scheduled — leave _skipNextValidateApply alone so LivePush / capture
            // can still suppress the pending Apply.
            if (_validateApplyQueued)
                return;

            _validateApplyQueued = true;
            UnityEditor.EditorApplication.delayCall += () =>
            {
                _validateApplyQueued = false;
                if (this == null || !isActiveAndEnabled)
                    return;
                if (_skipNextValidateApply)
                {
                    _skipNextValidateApply = false;
                    return;
                }
                // Do not snap Targets back while the author is editing a selected widget Target.
                if (!Application.isPlaying && IsAuthoringSelectedWidgetTarget())
                    return;
                Apply();
            };
        }

        /// <summary>
        /// Fallback when <see cref="EditorOnHandlerEnabled"/> is unset (no Simulator focus hook).
        /// After enable, canvas size may still be zero on the Apply frame. Re-run cover + widgets
        /// once without re-invoking layout events or toggling Active Objects.
        /// </summary>
        void QueuePostEnableSceneLayout()
        {
            if (_postEnableLayoutQueued)
                return;

            _postEnableLayoutQueued = true;
            UnityEditor.EditorApplication.delayCall += () =>
            {
                _postEnableLayoutQueued = false;
                if (this == null || !isActiveAndEnabled)
                    return;
                Canvas.ForceUpdateCanvases();
                RefreshEditorLayoutAfterCanvas(
                    _hasEditorSimulatorLayout, _editorSimulatorLayout);
            };
        }

        public void RefreshEditorLayoutNow(
            bool hasSimulatorLayout,
            ResponsiveDevice.LayoutKey simulatorLayout)
        {
            if (Application.isPlaying || this == null || !isActiveAndEnabled)
                return;

            _hasEditorSimulatorLayout = hasSimulatorLayout;
            _editorSimulatorLayout = simulatorLayout;
            Apply();
        }

        /// <summary>
        /// Canvas-ready layoutOnly pass after Device Simulator / CanvasScaler settle.
        /// Notifies every client (scroll no-ops; authored Targets are skipped) and
        /// re-covers backgrounds. Play Mode same-form size ticks still use
        /// <see cref="SizeChangeRequiresWidgetReapply"/>.
        /// </summary>
        public void RefreshEditorLayoutAfterCanvas(
            bool hasSimulatorLayout,
            ResponsiveDevice.LayoutKey simulatorLayout)
        {
            if (Application.isPlaying || this == null || !isActiveAndEnabled)
                return;

            _hasEditorSimulatorLayout = hasSimulatorLayout;
            _editorSimulatorLayout = simulatorLayout;

            // A Simulator / fold-state change that reaches this canvas pass without a
            // full Apply first would otherwise notify clients with the previous key.
            ResponsiveDevice.LayoutKey previous = CurrentLayout;
            ResolveCurrent();
            if (!CurrentLayout.Equals(previous))
            {
                Apply();
                return;
            }

            RememberSizeSnapshots();
            ApplySceneLayout(layoutOnly: true, notifyAllLayoutClients: true);
        }
#endif

        #endregion

        #region Apply

        /// <summary>
        /// Re-run detection, scaler, events, then notify hub clients.
        /// A call while this hub is already applying (foldable owner change, Active
        /// Object OnEnable, UnityEvent listener) is deferred and re-run once the
        /// outer pass finishes, so the final layout reflects the latest state.
        /// </summary>
        public void Apply()
        {
            if (ApplyInFlight)
            {
                _applyDeferred = true;
                return;
            }

            ApplyOnce();
            RunDeferredApply();
        }

        void RunDeferredApply()
        {
            for (int pass = 0;
                 _applyDeferred && !ApplyInFlight && pass < MaxDeferredApplyPasses;
                 pass++)
            {
                _applyDeferred = false;
                if (this == null)
                    return;
                ApplyOnce();
            }

            if (_applyDeferred && !ApplyInFlight)
            {
                _applyDeferred = false;
                Debug.LogWarning(
                    "[ResponsiveUi] Apply kept re-entering; dropped the deferred pass.",
                    this);
            }
        }

        void ApplyOnce()
        {
            using var _ = ApplyMarker.Auto();
            _layoutRefreshRequested = false;
            ResolveCurrent();

            EnterApplyInFlight();
            bool formTransition = false;
            try
            {
                // Activate while ApplyInFlight so the loading controller OnEnable
                // skips ApplyNow; NotifyClients (after scaler) is the single layout pass.
                formTransition = BeginFormTransitionLoadingIfNeeded(
                    _lastAppliedLayout, CurrentLayout, _hasAppliedLayout);
#if DEVELOPMENT_BUILD && !UNITY_EDITOR
                // Development players: one line per layout change, for posture / rotation QA.
                if (!_hasAppliedLayout || !_lastAppliedLayout.Equals(CurrentLayout))
                    Debug.Log(
                        $"[ResponsiveUi] {name}: layout {CurrentFormFactor}/{CurrentOrientation} " +
                        $"(screen {Screen.width}x{Screen.height})",
                        this);
#endif

                if (logDetection)
                    Debug.Log(
                        $"[ResponsiveUi] form={CurrentFormFactor} orientation={CurrentOrientation} " +
                        $"diagonal={ResponsiveDevice.DiagonalInches:F2}\"");

                if (applyCanvasScaler && canvasScaler != null)
                    ApplyScaler(CurrentLayout);

                // ExecuteAlways Apply also runs in Edit Mode — keep object toggles for
                // authoring, but do not invoke gameplay UnityEvents off Play Mode.
                InvokeLayoutEventsIfChanged();

                Vector2 watch = GetWatchSize();
                if (ShouldForceCanvasUpdate(
                        _hasAppliedLayout, _lastAppliedLayout, CurrentLayout, _lastWatchSize, watch))
                    Canvas.ForceUpdateCanvases();

                RememberSizeSnapshots();
                ApplySceneLayout();
                if (formTransition)
                    KeepFormTransitionLoadingVisible();
                _lastAppliedLayout = CurrentLayout;
                _hasAppliedLayout = true;
            }
            finally
            {
                ExitApplyInFlight();
                if (formTransition)
                    EndFormTransitionLoading();
            }
        }

        /// <summary>
        /// Play Mode slot events. Scene load, resume and fold callbacks re-Apply the same layout,
        /// so events fire once Start has run (every listener has woken), then only when the
        /// applied layout changes; re-enabling the hub fires the current slot again.
        /// </summary>
        void InvokeLayoutEventsIfChanged()
        {
            if (!Application.isPlaying || !_started)
                return;
            if (_hasInvokedLayoutEvents && _lastEventLayout.Equals(CurrentLayout))
                return;

            _hasInvokedLayoutEvents = true;
            _lastEventLayout = CurrentLayout;
            layoutEvents.Invoke(CurrentLayout);
        }

        /// <summary>
        /// Form-transition loading (ResponsiveUI). Default: no-op.
        /// Called under ApplyInFlight before scaler / NotifyClients.
        /// </summary>
        protected virtual bool BeginFormTransitionLoadingIfNeeded(
            in ResponsiveDevice.LayoutKey previous,
            in ResponsiveDevice.LayoutKey next,
            bool hasAppliedBefore) =>
            false;

        protected virtual void KeepFormTransitionLoadingVisible()
        {
        }

        protected virtual void EndFormTransitionLoading()
        {
        }

        void EnterApplyInFlight() => _applyInFlightDepth++;

        void ExitApplyInFlight()
        {
            if (_applyInFlightDepth > 0)
                _applyInFlightDepth--;
        }

        /// <summary>
        /// Coalesces an external geometry change (safe area, constrained pane, or host
        /// layout) into one canvas-settled layout-only pass in the next Play Mode
        /// LateUpdate. Edit Mode callers use the Simulator/editor scheduler.
        /// </summary>
        public void RequestLayoutRefresh()
        {
            if (!Application.isPlaying || !isActiveAndEnabled)
                return;
            _layoutRefreshRequested = true;
        }

        /// <summary>
        /// True when a full Apply should rebuild canvases (first apply, form/orientation flip,
        /// or a watch-size change of at least <see cref="ResponsiveDevice.SizeHysteresisPixels"/>).
        /// </summary>
        public static bool ShouldForceCanvasUpdate(
            bool hasApplied,
            in ResponsiveDevice.LayoutKey lastLayout,
            in ResponsiveDevice.LayoutKey currentLayout,
            Vector2 lastWatch,
            Vector2 currentWatch,
            float hysteresisPixels = ResponsiveDevice.SizeHysteresisPixels)
        {
            if (!hasApplied)
                return true;
            if (!lastLayout.Equals(currentLayout))
                return true;
            return Mathf.Abs(currentWatch.x - lastWatch.x) >= hysteresisPixels ||
                   Mathf.Abs(currentWatch.y - lastWatch.y) >= hysteresisPixels;
        }

        protected virtual void ApplyLayout()
        {
        }

        /// <summary>
        /// Notifies scene clients for <see cref="CurrentLayout"/> (the <see cref="ResponsiveUI"/>
        /// hub). Default: no clients.
        /// </summary>
        protected virtual void NotifyLayoutClients(bool layoutOnly, bool notifyAllLayoutClients)
        {
        }

        /// <summary>
        /// Scene layout + hub clients (no detection or events).
        /// </summary>
        protected void ApplySceneLayout(
            bool layoutOnly = false,
            bool notifyAllLayoutClients = false)
        {
            EnterApplyInFlight();
            try
            {
                NotifyLayoutClients(layoutOnly, notifyAllLayoutClients);
                if (layoutOnly)
                    ApplyLayout();
            }
            finally
            {
                ExitApplyInFlight();
            }

            // Standalone layout-only pass (LateUpdate / Simulator canvas-ready): run a
            // full Apply deferred during it. Nested inside Apply, the outer Apply does.
            if (!ApplyInFlight)
                RunDeferredApply();
        }

        /// <summary>
        /// Size used for scene layout / Design Scale (default: screen / Game View).
        /// Override to prefer root canvas rect when available.
        /// </summary>
        protected virtual Vector2 GetSceneLayoutSize() => GetWatchSize();

        void RememberSizeSnapshots()
        {
            _lastWatchSize = GetWatchSize();
            _lastSceneLayoutSize = GetSceneLayoutSize();
        }

        #endregion

        #region Foldable

        void OnFoldableLayoutChanged(FoldableLayoutState _)
        {
            if (!isActiveAndEnabled)
                return;
#if UNITY_EDITOR
            if (overrideInEditor)
                return;
#endif
            ResponsiveDevice.NotifyDisplayChanged();
            Apply();
        }

        void OnActiveFoldableChanged()
        {
#if UNITY_EDITOR
            if (overrideInEditor)
                return;
#endif
            Apply();
        }

        #endregion

        #region Size watch

        void RefreshRequestedLayout()
        {
            using var _ = LayoutRefreshMarker.Auto();
            _layoutRefreshRequested = false;
            Canvas.ForceUpdateCanvases();
            RememberSizeSnapshots();
            ApplySceneLayout(layoutOnly: true, notifyAllLayoutClients: true);
        }

        void RefreshLayoutIfWatchSizeChanged()
        {
            Vector2 watch = GetWatchSize();
            Vector2 layoutSize = GetSceneLayoutSize();
            // Ignore sub-pixel / 1px Simulator flicker that triggers Raw Input spam.
            bool watchChanged =
                Mathf.Abs(watch.x - _lastWatchSize.x) >= ResponsiveDevice.SizeHysteresisPixels ||
                Mathf.Abs(watch.y - _lastWatchSize.y) >= ResponsiveDevice.SizeHysteresisPixels;
            bool layoutChanged =
                Mathf.Abs(layoutSize.x - _lastSceneLayoutSize.x) >= ResponsiveDevice.SizeHysteresisPixels ||
                Mathf.Abs(layoutSize.y - _lastSceneLayoutSize.y) >= ResponsiveDevice.SizeHysteresisPixels;
            if (!watchChanged && !layoutChanged)
                return;

            // This size-driven pass subsumes any safe-area/host request queued earlier
            // in the frame while preserving full form/orientation detection.
            bool notifyAllLayoutClients = _layoutRefreshRequested;
            _layoutRefreshRequested = false;
            if (watchChanged)
                ResponsiveDevice.NotifyDisplayChanged();

            _lastWatchSize = watch;
            _lastSceneLayoutSize = layoutSize;

            // Form / orientation flip needs full Apply (objects, events, scaler).
            if (watchChanged && WatchSizeRequiresFullApply(watch))
            {
                Apply();
                return;
            }

            Canvas.ForceUpdateCanvases();
            _lastSceneLayoutSize = GetSceneLayoutSize();

            if (notifyAllLayoutClients)
            {
                ApplySceneLayout(
                    layoutOnly: true,
                    notifyAllLayoutClients: true);
                return;
            }

            // Same form: cover always. Layout-only client Apply only when a client
            // still needs Design Scale / camera size (not Simulator canvas-ready).
            if (SizeChangeRequiresWidgetReapply())
            {
                ApplySceneLayout(layoutOnly: true);
                return;
            }

            ApplyLayout();
        }

        /// <summary>
        /// Play Mode same-form size tick: when true, also notify clients that
        /// <see cref="IResponsiveUiClient.SizeChangeRequiresReapply"/>.
        /// </summary>
        protected virtual bool SizeChangeRequiresWidgetReapply() => false;

#if UNITY_EDITOR
        /// <summary>
        /// True when the editor selection includes a widget target owned by this handler.
        /// Used to avoid Apply overwriting in-progress Scene / Inspector edits.
        /// </summary>
        protected virtual bool IsAuthoringSelectedWidgetTarget() => false;
#endif

        bool WatchSizeRequiresFullApply(Vector2 watch)
        {
#if UNITY_EDITOR
            // Locked override: Game View size changes should not flip device/orientation.
            if (overrideInEditor)
                return false;

            float padMax = editorPadMaxAspect;
#else
            const float padMax = ResponsiveDevice.DefaultPadMaxAspect;
#endif
            // Editor + device: same detection entry as ResolveCurrent (fold mapping,
            // Simulator pin orientation, overrides, then Editor aspect / device hardware).
            ResponsiveDevice.LayoutKey detected =
                ResponsiveDevice.ResolveLiveLayout(watch, padMax);
            return !detected.Equals(CurrentLayout);
        }

        protected static Vector2 GetWatchSize() => ResponsiveDevice.GetWatchScreenSize();

        #endregion

        #region Detection

        void ResolveCurrent()
        {
#if UNITY_EDITOR
            if (overrideInEditor)
            {
                CurrentFormFactor = EditorFormFactor;
                CurrentOrientation = editorOrientation;
                return;
            }
            // Edit Mode: the selected Simulator profile, not a stale focused layout.
            if (!Application.isPlaying &&
                ResponsiveDevice.EditorPreviewLayout(out ResponsiveDevice.LayoutKey preview))
            {
                CurrentFormFactor = preview.Form;
                CurrentOrientation = preview.Orientation;
                return;
            }

            if (!Application.isPlaying && _hasEditorSimulatorLayout)
            {
                Vector2 editorWatch = GetWatchSize();
                CurrentFormFactor = ResponsiveDevice.RefineCoverForms(
                    _editorSimulatorLayout.Form, editorWatch.x, editorWatch.y);
                // The focused key already names its slot. Back to the screen shape (the
                // mapping is its own inverse), then name it for the refined form.
                CurrentOrientation = ResponsiveDevice.OrientationForForm(
                    CurrentFormFactor,
                    ResponsiveDevice.OrientationForForm(
                        _editorSimulatorLayout.Form, _editorSimulatorLayout.Orientation));
                return;
            }
#endif
            Vector2 watch = GetWatchSize();
#if UNITY_EDITOR
            float padMax = editorPadMaxAspect;
#else
            const float padMax = ResponsiveDevice.DefaultPadMaxAspect;
#endif
            // Shared path: FoldAndFlip when present; Editor Simulator layout pin; else aspect/hardware.
            ResponsiveDevice.LayoutKey live = ResponsiveDevice.ResolveLiveLayout(watch, padMax);
            CurrentFormFactor = live.Form;
            CurrentOrientation = live.Orientation;
        }

        #endregion

        #region Canvas scaler

        void ApplyScaler(ResponsiveDevice.LayoutKey layout)
        {
            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.screenMatchMode =
                CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;

            // Authored Phone / Pad / Wide Screen refs stay as serialized. Do not overwrite with Design
            // Canvas catalog — that rewrites live canvas.rect and jumps existing HUD.
            // Wide Screen has its own refs (never the mobile Phone / Pad values).
            bool wide = layout.Form == ResponsiveDevice.FormFactor.WideScreen;
            bool usePadRefs = UsesPadCanvasScalerRefs(layout.Form);
            Vector2 reference = wide
                ? wideScreenReferenceResolution
                : usePadRefs ? padReferenceResolution : phoneReferenceResolution;
            // Turn to the slot's screen shape (Fold half-open Portrait is the wide screen).
            if (ResponsiveDevice.OrientationForForm(layout.Form, layout.Orientation) ==
                ResponsiveDevice.Orientation.Landscape)
                reference = new Vector2(reference.y, reference.x);

            canvasScaler.referenceResolution = reference;
            canvasScaler.matchWidthOrHeight = wide
                ? wideScreenMatchWidthOrHeight
                : usePadRefs ? padMatchWidthOrHeight : phoneMatchWidthOrHeight;
        }

        /// <summary>
        /// Pad + Fold HalfOpened / Fold Cover HalfOpened / iPhone Duo Seated use pad CanvasScaler
        /// refs. Flip HalfOpened and the covers stay phone-like. Wide Screen has its own refs.
        /// </summary>
        public static bool UsesPadCanvasScalerRefs(ResponsiveDevice.FormFactor form) =>
            form == ResponsiveDevice.FormFactor.Pad ||
            form == ResponsiveDevice.FormFactor.FoldHalfOpened ||
            form == ResponsiveDevice.FormFactor.FoldCoverHalfOpened ||
            form == ResponsiveDevice.FormFactor.iPhoneDuoSeated;

        #endregion
    }

}
