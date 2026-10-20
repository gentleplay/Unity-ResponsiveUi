#if UNITY_EDITOR
using FDPlus.Foldable;
using FDPlus.Foldable.Editor;
using FDPlus.ResponsiveUI.Editor;
using UnityEditor;
using UnityEngine;

using FDPlus.ResponsiveUI;
using FDPlus.ResponsiveUI.Shared.Editor;

namespace FDPlus.ResponsiveUI.Editor
{
    /// <summary>
    /// Tracks Device Simulator selection and exposes which
    /// <see cref="ResponsiveWidgetLayout"/> state foldout should stay expanded.
    /// Hot path (Inspector GetPropertyHeight / OnGUI) is read-only — no Game View
    /// queries, no repaints — to avoid Windows Raw Input handle spam.
    /// </summary>
    [InitializeOnLoad]
    public static class ResponsiveUiSimulatorSlotFocus
    {
        #region Runtime state

        static string _focusedSlotName;
        static ResponsiveDevice.LayoutKey _focusedLayout;
        static string _fingerprint;
        static string _identityFingerprint;
        static bool _hasFocus;
        static int _revision;
        // Re-entrancy guard: a hub Apply can re-enter RefreshHandlersNow synchronously.
        static bool _refreshingHandlers;
        // Bumped per forced hub Apply; RefreshNow uses it to avoid a second Apply.
        static int _handlerRefreshCount;
        static SimulatorDeviceBridge.Selection _cachedSelection;
        static bool _hasCachedSelection;

        static bool _hasPushedApply;
        static string _pushedApplyFingerprint;
        static Vector2 _pushedApplySize;

        static bool _sessionRefreshQueued;
        static bool _sessionRefreshDirty;

        #endregion

        #region Lifecycle

        static ResponsiveUiSimulatorSlotFocus()
        {
            BindLifecycle();
            SyncSimulatorDeviceLayoutFromSession(preferLiveSession: true);
            EditorApplication.delayCall += RefreshFromSimulator;
        }

        static void BindLifecycle()
        {
            SimulatorDeviceBridge.SelectionChanged -= OnSimulatorSelectionChanged;
            SimulatorDeviceBridge.SelectionChanged += OnSimulatorSelectionChanged;
            FoldableEditorSession.Changed -= OnFoldableSessionChanged;
            FoldableEditorSession.Changed += OnFoldableSessionChanged;
            FoldableEditorSession.ScreenSynced -= OnFoldableScreenSynced;
            FoldableEditorSession.ScreenSynced += OnFoldableScreenSynced;
            ResponsiveUiHandler.EditorOnHandlerEnabled -= OnHandlerEnabled;
            ResponsiveUiHandler.EditorOnHandlerEnabled += OnHandlerEnabled;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            ResponsiveDevice.ShouldIgnoreStaleFlipCoverSize = IsIgnoringStaleFlipCoverSize;
        }

        internal static void Teardown()
        {
            SimulatorDeviceBridge.SelectionChanged -= OnSimulatorSelectionChanged;
            FoldableEditorSession.Changed -= OnFoldableSessionChanged;
            FoldableEditorSession.ScreenSynced -= OnFoldableScreenSynced;
            ResponsiveUiHandler.EditorOnHandlerEnabled -= OnHandlerEnabled;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            ResponsiveDevice.ShouldIgnoreStaleFlipCoverSize = null;
            _refreshingHandlers = false;
            _sessionRefreshQueued = false;
            _playModeApplyQueued = false;
        }

        #endregion

        #region Handler enable

        /// <summary>
        /// Disabled→enabled (GameObject or component) never sees Simulator selection events.
        /// Push the current focused layout so widget Targets snap to the active slot.
        /// Does not queue a global handler refresh (avoids a second full Apply on enable).
        /// </summary>
        static void OnHandlerEnabled(ResponsiveUiHandler handler)
        {
            if (handler == null || Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (!_hasFocus)
                EnsureFocusFromSimulator();

            bool hasFocusedLayout = GetFocusedLayout(out var focusedLayout);
            handler.RefreshEditorLayoutNow(hasFocusedLayout, focusedLayout);
            // Same canvas-ready pass as Simulator profile changes — do not keep a
            // second enable-only delayCall in the handler.
            ResponsiveUiEditorLayoutScheduler.ScheduleCanvasCatchup();
        }

        /// <summary>
        /// Sync focus from the current Simulator selection without refreshing every handler.
        /// </summary>
        static void EnsureFocusFromSimulator()
        {
            if (Application.isBatchMode)
                return;

            if (!SimulatorDeviceBridge.GetActiveSelection(out var selection) || !selection.IsValid)
                return;

            _cachedSelection = selection;
            _hasCachedSelection = true;
            if (BuildIdentityFingerprint(selection, out string identity))
                _identityFingerprint = identity;
            RecomputeFocus();
        }

        #endregion

        #region Public API

        public static int Revision => _revision;

        public static void RefreshNow()
        {
            int appliesBefore = _handlerRefreshCount;
            RefreshFromSimulator();
            // Identity / size changed too: RefreshFromSimulator already re-applied.
            if (_handlerRefreshCount != appliesBefore)
                return;
            if (Application.isBatchMode ||
                Application.isPlaying ||
                EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            // Settings changes (e.g. Player Allowed Orientations flipping slot Enabled)
            // leave the Simulator identity / size unchanged, so RefreshFromSimulator
            // returns early. Re-resolve focus and force one hub Apply so live Targets
            // move off a now-disabled slot before the next capture.
            if (RecomputeFocus())
                QueueInspectorRepaint();
            if (FoldableSimulatorScreenCommit.InFlight)
                return;
            _hasPushedApply = false;
            RefreshHandlersIfNeeded();
        }

        /// <summary>
        /// When true, Inspector should expand only <paramref name="slotPropertyName"/>
        /// and collapse other state foldouts on each widget.
        /// Read-only — safe to call from property drawers every frame.
        /// </summary>
        public static bool GetFocusedSlotName(out string slotPropertyName)
        {
            slotPropertyName = _focusedSlotName;
            return _hasFocus && !string.IsNullOrEmpty(_focusedSlotName);
        }

        public static bool GetFocusedLayout(out ResponsiveDevice.LayoutKey layout)
        {
            layout = _focusedLayout;
            return _hasFocus;
        }

        #endregion

        #region Simulator events

        static void OnSimulatorSelectionChanged()
        {
            if (Application.isBatchMode)
                return;

            // Play Mode: keep Phone/Pad mapping current without Inspector foldout churn.
            // Full RefreshFromSimulator would soft-repaint Inspectors on every resolution spam.
            if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                SyncPlayModeFromSimulator();
                return;
            }

            RefreshFromSimulator();
        }

        static void OnFoldableSessionChanged(FoldableEditorSession.Source _)
        {
            // Play Mode: still publish Simulator→Phone/Pad mapping (Cover must not become Pad),
            // but skip Inspector foldout churn.
            if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                RefreshCachedSelectionForSession();
                SyncSimulatorDeviceLayoutFromSession(preferLiveSession: true);
                QueuePlayModeHandlerApply();
                return;
            }

            // Product-state Changed can fire before Cover/Inner chrome settles.
            // FoldableSimulatorScreenCommit.InFlight skips widget Apply until ScreenSynced.
            _sessionRefreshDirty = true;
            if (_sessionRefreshQueued)
                return;

            _sessionRefreshQueued = true;
            EditorApplication.delayCall += ProcessSessionRefresh;
        }

        /// <summary>
        /// Cover→Main chrome committed. The single Edit Mode Apply for Fold/Flip
        /// after Unity Screen matches the session product.
        /// </summary>
        static void OnFoldableScreenSynced()
        {
            if (Application.isBatchMode)
                return;

            if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                RefreshCachedSelectionForSession();
                SyncSimulatorDeviceLayoutFromSession(preferLiveSession: true);
                QueuePlayModeHandlerApply();
                return;
            }

            ApplySettledLayout();
        }

        static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode &&
                state != PlayModeStateChange.EnteredEditMode)
                return;

            RefreshCachedSelectionForSession();
            SyncSimulatorDeviceLayoutFromSession(preferLiveSession: true);
            if (state == PlayModeStateChange.EnteredPlayMode)
                QueuePlayModeHandlerApply();
        }

        static void ProcessSessionRefresh()
        {
            _sessionRefreshQueued = false;
            if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                _sessionRefreshDirty = false;
                return;
            }

            if (!_sessionRefreshDirty)
                return;
            _sessionRefreshDirty = false;

            if (FoldableSimulatorScreenCommit.InFlight)
            {
                RefreshCachedSelectionForSession();
                PublishEditorHingeHint();
                SyncSimulatorDeviceLayoutFromSession(preferLiveSession: true);
                if (RecomputeFocus())
                    QueueInspectorRepaint();
            }
            else
            {
                ApplySettledLayout();
            }

            // Another panel/sync Changed arrived while we were applying — run once more.
            if (_sessionRefreshDirty && !_sessionRefreshQueued)
            {
                _sessionRefreshQueued = true;
                EditorApplication.delayCall += ProcessSessionRefresh;
            }
        }

        static void ApplySettledLayout()
        {
            RefreshCachedSelectionForSession();
            PublishEditorHingeHint();
            SyncSimulatorDeviceLayoutFromSession(preferLiveSession: true);
            bool changed = RecomputeFocus();
            Canvas.ForceUpdateCanvases();
            bool hasLayout = GetFocusedLayout(out var layout);
            _hasPushedApply = true;
            _pushedApplyFingerprint = BuildApplyFingerprint(hasLayout, layout);
            _pushedApplySize = GetAuthoritativeScreenSize();
            RefreshHandlersNow();
            if (changed)
                QueueInspectorRepaint();
        }

        /// <summary>
        /// Best-effort refresh of the Simulator selection cache after Fold session changes.
        /// Always rebuilds the identity fingerprint so ProductState / landscape are current.
        /// </summary>
        static void RefreshCachedSelectionForSession()
        {
            if (SimulatorDeviceBridge.GetActiveSelection(out var selection) && selection.IsValid)
            {
                _cachedSelection = selection;
                _hasCachedSelection = true;
                if (BuildIdentityFingerprint(selection, out string identity))
                    _identityFingerprint = identity;
                return;
            }

            if (!_hasCachedSelection)
                return;

            // Transient Simulator read failure — keep last selection, refresh product in identity.
            if (BuildIdentityFingerprint(_cachedSelection, out string cachedIdentity))
                _identityFingerprint = cachedIdentity;
        }

        #endregion

        #region Focus

        static void RefreshFromSimulator()
        {
            if (Application.isBatchMode)
                return;

            // Device Simulator emits resolution/orientation spam in Play Mode. Updating focus
            // revision / soft-repainting Inspectors here is what looks like "constant repainting".
            if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (!SimulatorDeviceBridge.GetActiveSelection(out var selection) || !selection.IsValid)
            {
                if (_hasFocus)
                {
                    ClearFocus();
                    RefreshHandlersIfNeeded();
                    QueueInspectorRepaint();
                }
                return;
            }

            if (!BuildIdentityFingerprint(selection, out string identity))
            {
                // Transient incomplete read (0×0) — keep last stable focus; do not Apply/repaint.
                return;
            }

            bool identityChanged = !_hasCachedSelection || identity != _identityFingerprint;
            bool selectionSizeChanged = !_hasCachedSelection ||
                               ScreenSizeRequiresLayoutApply(
                                   _cachedSelection.OrientedScreenSize,
                                   selection.OrientedScreenSize);
            if (!identityChanged && !selectionSizeChanged)
                return;

            _cachedSelection = selection;
            _hasCachedSelection = true;
            _identityFingerprint = identity;

            PublishEditorHingeHint();
            bool changed = RecomputeFocus();
            // Only wait while Cover→Main chrome is actively committing. A bound,
            // already-settled Fold/Flip selection must re-apply its resolved slot;
            // ScreenSynced is not guaranteed when the requested screen was current.
            if (!FoldableSimulatorScreenCommit.InFlight)
                RefreshHandlersIfNeeded();
            if (changed)
                QueueInspectorRepaint();
        }

        static bool BuildIdentityFingerprint(
            SimulatorDeviceBridge.Selection selection,
            out string identity)
        {
            string orientationPart;
            if (selection.HasRotation)
            {
                orientationPart = "r" + selection.RotationDegrees;
            }
            else if (selection.ScreenWidth > 0 && selection.ScreenHeight > 0)
            {
                orientationPart = ResponsiveDevice.DetectOrientation(
                    selection.ScreenWidth, selection.ScreenHeight).ToString();
            }
            else
            {
                identity = null;
                return false;
            }

            identity = selection.FriendlyName + "|" +
                       selection.DeviceModel + "|" +
                       selection.ScreenIndex + "|" +
                       orientationPart + "|" +
                       FoldableEditorSession.ProductState;
            return true;
        }

        /// <returns>True when the focused layout or visible Inspector slot changed (revision bumped).</returns>
        static bool RecomputeFocus()
        {
            if (!_hasCachedSelection)
                return false;

            ResponsiveDevice.LayoutKey raw =
                ApplyCoverPhoneOverride(ResponsiveUiSimulatorLayoutResolver.Resolve(_cachedSelection));
            // Publish the profile before focus. Apply already reads that pin; focus, capture,
            // and the expanded slot have to be the same layout or scene edits land in another slot.
            SyncSimulatorDeviceLayout(raw);
            ResponsiveDevice.LayoutKey key = ConstrainFocusKey(raw);

            string slotName = ResolveVisibleSlotName(key);
            string fingerprint =
                _identityFingerprint + "|" + key;

            if (string.IsNullOrEmpty(slotName))
                return ClearFocus();

            bool layoutChanged = !_hasFocus || !key.Equals(_focusedLayout);
            bool slotChanged = !_hasFocus || slotName != _focusedSlotName;
            if (_hasFocus && fingerprint == _fingerprint && !layoutChanged && !slotChanged)
                return false;

            _hasFocus = true;
            _focusedSlotName = slotName;
            _focusedLayout = key;
            _fingerprint = fingerprint;

            // Revision drives Inspector foldout sync — layout key (Phone/Pad/Fold/Flip ×
            // orientation) or visible slot name, whichever changes first.
            if (!layoutChanged && !slotChanged)
                return false;

            _revision++;
            return true;
        }

        /// <summary>
        /// Focus (Detection, foldouts, capture) uses the build target's form when no Device
        /// Simulator profile is selected. A selected profile uses the same layout Apply writes
        /// onto widgets, cameras, scroll, and banners (<see cref="ResponsiveDevice.CurrentLayout"/>).
        /// </summary>
        static ResponsiveDevice.LayoutKey ConstrainFocusKey(ResponsiveDevice.LayoutKey key)
        {
            if (ResponsiveDevice.EditorPreviewLayout(out ResponsiveDevice.LayoutKey preview))
                return preview;
            // key.Orientation is the screen shape; name the slot like CurrentLayout does.
            ResponsiveDevice.FormFactor form = ResponsivePlatforms.ConstrainDetected(key.Form);
            return new ResponsiveDevice.LayoutKey(
                form, ResponsiveDevice.OrientationForForm(form, key.Orientation));
        }

        /// <returns>True when focus was cleared and revision bumped.</returns>
        static bool ClearFocus()
        {
            bool had = _hasFocus;
            _hasFocus = false;
            _focusedSlotName = null;
            _focusedLayout = default;
            _fingerprint = null;
            _identityFingerprint = null;
            _hasCachedSelection = false;
            _cachedSelection = default;
            _hasPushedApply = false;
            _pushedApplyFingerprint = null;
            _pushedApplySize = Vector2.zero;
            ResponsiveUiHingeGuide.ClearEditorSessionHint();
            ResponsiveDevice.ClearSimulatorLayout();
            if (!had)
                return false;
            _revision++;
            return true;
        }

        /// <summary>
        /// Closed / Cover screen: known wide cover WxH → its cover form (Fold Cover / iPhone Duo
        /// Cover); landscape-native inner Closed → iPhone Duo Cover; Flip → Flip Cover;
        /// otherwise Phone.
        /// Flip Opened / HalfOpened must not treat leftover Cover chrome as Closed.
        /// Must run before focus is stored so Detection and foldouts match the published pin.
        /// </summary>
        static ResponsiveDevice.LayoutKey ApplyCoverPhoneOverride(ResponsiveDevice.LayoutKey key)
        {
            bool ignoreStaleFlipCover = ResponsiveDevice.IsIgnoringStaleFlipCoverSize();
            bool forceCoverPhone =
                FoldableEditorSession.IsFoldableDeviceBound &&
                FoldableEditorSession.ProductState == FoldableProductState.Closed;

            if (!forceCoverPhone &&
                FoldableEditorSession.IsFoldableDeviceBound &&
                FoldableEditorSession.BoundScreenIsCover &&
                !ignoreStaleFlipCover)
                forceCoverPhone = true;

            float coverW = 0f;
            float coverH = 0f;
            if (_hasCachedSelection)
            {
                coverW = _cachedSelection.ScreenWidth;
                coverH = _cachedSelection.ScreenHeight;
            }

            Vector2 watch = ResponsiveDevice.GetWatchScreenSize();
            bool wideCover =
                ResponsiveDevice.MapKnownWideCoverSize(
                    coverW, coverH, out ResponsiveDevice.FormFactor wideCoverForm) ||
                ResponsiveDevice.MapKnownWideCoverSize(watch.x, watch.y, out wideCoverForm);
            if (wideCover)
                forceCoverPhone = true;

            if (!forceCoverPhone &&
                !ignoreStaleFlipCover &&
                (ResponsiveDevice.IsKnownFlipCoverPhoneSize(coverW, coverH) ||
                 ResponsiveDevice.IsKnownFlipCoverPhoneSize(watch.x, watch.y)))
                forceCoverPhone = true;

            if (!forceCoverPhone)
                return key;

            if (wideCover)
                return new ResponsiveDevice.LayoutKey(wideCoverForm, key.Orientation);

            if (FoldableEditorSession.IsFoldableDeviceBound &&
                FoldableEditorSession.BoundInnerLandscapeNative)
            {
                return new ResponsiveDevice.LayoutKey(
                    ResponsiveDevice.FormFactor.iPhoneDuoCover,
                    key.Orientation);
            }

            if (FoldableEditorSession.DeviceKind == FoldableDeviceKind.FlipablePhone ||
                ResponsiveDevice.IsKnownFlipCoverPhoneSize(coverW, coverH) ||
                ResponsiveDevice.IsKnownFlipCoverPhoneSize(watch.x, watch.y))
            {
                return new ResponsiveDevice.LayoutKey(
                    ResponsiveDevice.FormFactor.FlipPhoneCover,
                    key.Orientation);
            }

            return new ResponsiveDevice.LayoutKey(
                ResponsiveDevice.FormFactor.Phone,
                key.Orientation);
        }

        static bool IsIgnoringStaleFlipCoverSize() =>
            FoldableEditorSession.IsFoldableDeviceBound &&
            FoldableEditorSession.DeviceKind == FoldableDeviceKind.FlipablePhone &&
            FoldableEditorSession.ProductState != FoldableProductState.Closed &&
            FoldableEditorSession.ProductState != FoldableProductState.Undefined;

        /// <summary>
        /// <see cref="ApplyCoverPhoneOverride"/>, then Cover / HalfOpened refinement from
        /// the authoritative screen size (skipped while that size is unknown).
        /// </summary>
        static ResponsiveDevice.LayoutKey RefineCoverLayout(
            ResponsiveDevice.LayoutKey key,
            Vector2 size)
        {
            key = ApplyCoverPhoneOverride(key);
            if (size.x <= 0f || size.y <= 0f)
                return key;

            return new ResponsiveDevice.LayoutKey(
                ResponsiveDevice.RefineCoverForms(key.Form, size.x, size.y),
                key.Orientation);
        }

        /// <summary>
        /// A focus key names the slot (<see cref="ConstrainFocusKey"/>); the Simulator pin takes the
        /// screen shape. <see cref="ResponsiveDevice.OrientationForForm"/> is its own inverse.
        /// </summary>
        static ResponsiveDevice.LayoutKey ScreenShapeKey(ResponsiveDevice.LayoutKey slotKey) =>
            new ResponsiveDevice.LayoutKey(
                slotKey.Form,
                ResponsiveDevice.OrientationForForm(slotKey.Form, slotKey.Orientation));

        /// <param name="key">Form + screen shape (not the slot name).</param>
        static void SyncSimulatorDeviceLayout(ResponsiveDevice.LayoutKey key)
        {
            Vector2 size = GetAuthoritativeScreenSize();
            int width = size.x > 0f ? Mathf.RoundToInt(size.x) : 0;
            int height = size.y > 0f ? Mathf.RoundToInt(size.y) : 0;
            key = RefineCoverLayout(key, new Vector2(width, height));
            ResponsiveDevice.SetSimulatorLayout(key.Form, key.Orientation, width, height);
        }

        /// <summary>
        /// Fold session catalog WxH when bound (already product + landscape). Do not
        /// trust lagged Device Simulator Cover chrome after Phone→Flip first-bind.
        /// </summary>
        static bool GetSessionCatalogSize(out Vector2 size)
        {
            size = default;
            if (!FoldableEditorSession.IsFoldableDeviceBound)
                return false;

            int width = FoldableEditorSession.MetricsWidth;
            int height = FoldableEditorSession.MetricsHeight;
            if (width <= 0 || height <= 0)
                return false;

            size = new Vector2(width, height);
            return true;
        }

        static Vector2 GetAuthoritativeScreenSize()
        {
            if (GetSessionCatalogSize(out Vector2 catalog))
                return catalog;
            if (_hasCachedSelection)
                return _cachedSelection.OrientedScreenSize;
            return Vector2.zero;
        }

        /// <param name="preferLiveSession">
        /// When true (Play Mode), ignore Edit Mode <see cref="_focusedLayout"/> — it can still
        /// say Expand/Pad after the user clicks Normal/Cover.
        /// </param>
        static void SyncSimulatorDeviceLayoutFromSession(bool preferLiveSession = false)
        {
            if (!FoldableEditorSession.IsFoldableDeviceBound)
            {
                if (GetFocusedLayout(out var nonFold) && !preferLiveSession)
                    SyncSimulatorDeviceLayout(ScreenShapeKey(nonFold));
                else if (preferLiveSession &&
                         SimulatorDeviceBridge.GetActiveSelection(out var live) &&
                         live.IsValid)
                    SyncSimulatorDeviceLayout(ResponsiveUiSimulatorLayoutResolver.Resolve(live));
                else if (!GetFocusedLayout(out _))
                    ResponsiveDevice.ClearSimulatorLayout();
                return;
            }

            // Live selection / session first — ProductState is authoritative for Cover→Phone.
            if (SimulatorDeviceBridge.GetActiveSelection(out var selection) && selection.IsValid)
            {
                _cachedSelection = selection;
                _hasCachedSelection = true;
                ResponsiveDevice.LayoutKey liveKey =
                    ResponsiveUiSimulatorLayoutResolver.Resolve(selection);
                SyncSimulatorDeviceLayout(liveKey);
                QuietUpdateFocus(liveKey);
                return;
            }

            if (_hasCachedSelection)
            {
                ResponsiveDevice.LayoutKey cachedKey =
                    ResponsiveUiSimulatorLayoutResolver.Resolve(_cachedSelection);
                SyncSimulatorDeviceLayout(cachedKey);
                QuietUpdateFocus(cachedKey);
                return;
            }

            if (!preferLiveSession && GetFocusedLayout(out var focused))
            {
                SyncSimulatorDeviceLayout(ScreenShapeKey(focused));
                return;
            }

            // Bound foldable without a selection read — still map product (Cover → Phone).
            ResponsiveDevice.FormFactor form = ResponsiveDevice.MapFoldableProduct(
                FoldableEditorSession.DeviceKind,
                FoldableEditorSession.ProductState);
            ResponsiveDevice.Orientation orientation = FoldableEditorSession.IsLandscape
                ? ResponsiveDevice.Orientation.Landscape
                : ResponsiveDevice.Orientation.Portrait;
            var fallback = new ResponsiveDevice.LayoutKey(form, orientation);
            SyncSimulatorDeviceLayout(fallback);
            QuietUpdateFocus(fallback);
        }

        /// <summary>
        /// Updates focus caches. Bumps Inspector revision when the visible slot changes
        /// (Play Mode included — drawers expand from live layout; revision still drives repaint).
        /// </summary>
        static void QuietUpdateFocus(ResponsiveDevice.LayoutKey key)
        {
            key = ConstrainFocusKey(RefineCoverLayout(key, GetAuthoritativeScreenSize()));
            string slot = ResolveVisibleSlotName(key);
            if (string.IsNullOrEmpty(slot))
            {
                // Drop only the stale focus; the Simulator pin and cached selection stay.
                if (!_hasFocus)
                    return;
                _hasFocus = false;
                _focusedSlotName = null;
                _focusedLayout = default;
                _fingerprint = null;
                _revision++;
                QueueInspectorRepaint();
                return;
            }

            bool layoutChanged = !_hasFocus || !key.Equals(_focusedLayout);
            bool slotChanged = !_hasFocus || slot != _focusedSlotName;
            _hasFocus = true;
            _focusedLayout = key;
            _focusedSlotName = slot;
            if (!layoutChanged && !slotChanged)
                return;

            _revision++;
            QueueInspectorRepaint();
        }

        #endregion

        #region Play Mode

        static string _playModeLayoutFingerprint;
        static bool _playModeApplyQueued;

        static void SyncPlayModeFromSimulator()
        {
            if (!SimulatorDeviceBridge.GetActiveSelection(out var selection) || !selection.IsValid)
            {
                SyncSimulatorDeviceLayoutFromSession(preferLiveSession: true);
                return;
            }

            if (!BuildIdentityFingerprint(selection, out string identity))
                return;

            _cachedSelection = selection;
            _hasCachedSelection = true;
            _identityFingerprint = identity;

            ResponsiveDevice.LayoutKey key =
                ResponsiveUiSimulatorLayoutResolver.Resolve(selection);
            SyncSimulatorDeviceLayout(key);
            QuietUpdateFocus(key);

            // Debounce Apply: Device Simulator spams resolution events in Play Mode.
            if (identity == _playModeLayoutFingerprint)
                return;

            _playModeLayoutFingerprint = identity;
            QueuePlayModeHandlerApply();
        }

        static void QueuePlayModeHandlerApply()
        {
            if (_playModeApplyQueued)
                return;

            _playModeApplyQueued = true;
            EditorApplication.delayCall += () =>
            {
                _playModeApplyQueued = false;
                ApplyPlayModeHandlers();
            };
        }

        static void ApplyPlayModeHandlers()
        {
            if (!Application.isPlaying)
                return;

            SyncSimulatorDeviceLayoutFromSession(preferLiveSession: true);
            ResponsiveUiHandler[] handlers = Object.FindObjectsByType<ResponsiveUiHandler>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            for (int i = 0; i < handlers.Length; i++)
            {
                ResponsiveUiHandler handler = handlers[i];
                if (handler != null && handler.isActiveAndEnabled)
                    RunIsolated(handler, handler.Apply);
            }
        }

        /// <summary>
        /// One hub throwing (e.g. a Play Mode layout-event listener) must not leave
        /// every later hub on the previous Simulator / fold state.
        /// </summary>
        static void RunIsolated(ResponsiveUiHandler handler, System.Action action)
        {
            try
            {
                action();
            }
            catch (System.Exception e) when (!(e is ExitGUIException))
            {
                Debug.LogException(e, handler);
            }
        }

        #endregion

        #region Hinge / foldouts / refresh

        /// <summary>
        /// Publishes Fold session hinge bounds so <see cref="ResponsiveUI"/> can draw the
        /// dual-pane border even before / without a scene FoldablePostureDetector Apply.
        /// </summary>
        static void PublishEditorHingeHint() =>
            ResponsiveUiHingeGuide.SetEditorSessionHint(
                FoldableEditorSession.IsFoldableDeviceBound
                    ? FoldableEditorSession.BuildLayout()
                    : null);

        /// <summary>
        /// Inspector foldout target for <paramref name="key"/>: exact form × orientation
        /// (FoldPhoneCover / HalfOpened included); a hidden Landscape slot (Player lock or build
        /// target) falls back to the same form's Portrait slot.
        /// Does <b>not</b> apply FoldPhoneCover→Phone fallback —
        /// opt-in slots must expand so authors can enable them.
        /// </summary>
        public static string ResolveFoldoutSlotName(ResponsiveDevice.LayoutKey key) =>
            ResolveVisibleSlotName(key);

        static string ResolveVisibleSlotName(ResponsiveDevice.LayoutKey key)
        {
            string exact = ResponsiveWidgetLayout.StatePropertyName(key);
            if (ResponsiveUiEditorSettings.SlotVisible(exact))
                return exact;

            if (key.Orientation == ResponsiveDevice.Orientation.Landscape)
            {
                string tall = ResponsiveWidgetLayout.StatePropertyName(
                    key.Form,
                    ResponsiveDevice.Orientation.Portrait);
                if (ResponsiveUiEditorSettings.SlotVisible(tall))
                    return tall;
            }

            return null;
        }

        static void QueueInspectorRepaint()
        {
            if (Application.isBatchMode)
                return;
            // delayCall — safe in Play Mode. Callers only invoke when the visible slot changed.
            ResponsiveUiInspectorRepaint.Queue();
        }

        /// <summary>
        /// Phone/Pad: re-Apply when layout key, Simulator identity, or catalog WxH
        /// changes (2px hysteresis). Fold/Flip uses ApplySettledLayout after ScreenSynced.
        /// </summary>
        static void RefreshHandlersIfNeeded()
        {
            bool hasLayout = GetFocusedLayout(out var layout);
            string applyFingerprint = BuildApplyFingerprint(hasLayout, layout);
            Vector2 size = GetAuthoritativeScreenSize();
            bool sizeChanged = ScreenSizeRequiresLayoutApply(_pushedApplySize, size);

            if (_hasPushedApply &&
                applyFingerprint == _pushedApplyFingerprint &&
                !sizeChanged)
                return;

            _hasPushedApply = true;
            _pushedApplyFingerprint = applyFingerprint;
            _pushedApplySize = size;
            RefreshHandlersNow();
        }

        /// <summary>
        /// True when Simulator catalog pixels changed enough to re-layout.
        /// Cover ~948×1048 vs Main 2520×1080 must apply; 1px flicker must not.
        /// </summary>
        public static bool ScreenSizeRequiresLayoutApply(Vector2 previous, Vector2 current) =>
            Mathf.Abs(current.x - previous.x) >= 2f ||
            Mathf.Abs(current.y - previous.y) >= 2f;

        static string BuildApplyFingerprint(bool hasLayout, ResponsiveDevice.LayoutKey layout)
        {
            return (_identityFingerprint ?? "") + "|" + (hasLayout ? layout.ToString() : "none");
        }

        /// <summary>
        /// Applies the focused layout to every hub immediately so Inspector / capture see the
        /// new key before the soft-repaint delayCall (foldout expand/collapse).
        /// </summary>
        static void RefreshHandlersNow()
        {
            if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            if (_refreshingHandlers)
                return;

            _refreshingHandlers = true;
            _handlerRefreshCount++;
            try
            {
                ResponsiveUiEditorLayoutScheduler.Apply(scheduleCanvasCatchup: true);
            }
            finally
            {
                _refreshingHandlers = false;
            }
        }

        /// <summary>Immediate focused-layout Apply for every enabled handler.</summary>
        public static void ApplyFocusedLayoutNow() =>
            RefreshHandlers();

        /// <summary>
        /// Canvas-settled layoutOnly pass. Coalesces rapid profile switches to the latest focus.
        /// </summary>
        public static void ApplyFocusedLayoutAfterCanvas()
        {
            if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            var handlers = Object.FindObjectsByType<ResponsiveUiHandler>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            bool hasFocusedLayout = GetFocusedLayout(out var focusedLayout);
            for (int i = 0; i < handlers.Length; i++)
            {
                ResponsiveUiHandler handler = handlers[i];
                RunIsolated(handler, () =>
                    handler.RefreshEditorLayoutAfterCanvas(hasFocusedLayout, focusedLayout));
            }
        }

        static void RefreshHandlers()
        {
            if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            var handlers = Object.FindObjectsByType<ResponsiveUiHandler>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            bool hasFocusedLayout = GetFocusedLayout(out var focusedLayout);
            for (int i = 0; i < handlers.Length; i++)
            {
                ResponsiveUiHandler handler = handlers[i];
                RunIsolated(handler, () =>
                    handler.RefreshEditorLayoutNow(hasFocusedLayout, focusedLayout));
            }
        }

        #endregion
    }
}

#endif
