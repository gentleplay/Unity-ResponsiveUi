using System;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.UI;

namespace FDPlus.ResponsiveUI
{
    /// <summary>
    /// Canvas hub: detection, events, Canvas Scaler, form-transition loading, debug, and
    /// background-size override. Widgets / backgrounds / Active Objects live on
    /// <see cref="ResponsiveUiController"/> clients.
    /// </summary>
    public class ResponsiveUI : ResponsiveUiHandler
    {
        #region Constants

        /// <summary>
        /// iPad Pro 13 M5 landscape — built-in background art size when Project Settings
        /// has no catalog profile (same pixels as Pad Design Canvas, oriented).
        /// </summary>
        public static readonly Vector2 BuiltinOriginalBgSize = new Vector2(
            ResponsiveRectState.BuiltinPadDesignCanvasSize.y,
            ResponsiveRectState.BuiltinPadDesignCanvasSize.x);

        /// <summary>
        /// Optional override (Editor Project Settings). When null, <see cref="BuiltinOriginalBgSize"/> is used.
        /// </summary>
        /// <remarks>
        /// Editor-installed configuration hook, not play-session state: intentionally not
        /// reset on <c>SubsystemRegistration</c> (see <see cref="ResponsiveRectState.DesignCanvasSizeProvider"/>).
        /// </remarks>
        public static Func<Vector2> OriginalBgSizeProvider;

        #endregion

        #region Serialized

        [Header("Layout")]
        [Tooltip(
            "When on, this component uses Original Bg Size instead of " +
            "Project Settings → Responsive UI → Background Cover Size.")]
        [SerializeField] bool overrideOriginalBgSize;
        [Tooltip("Designed pixel size of the background art (width, height). Used when Override is on, and baked for player builds.")]
        [SerializeField] Vector2 originalBgSize = BuiltinOriginalBgSize;

        [SerializeField] ResponsiveFormTransitionLoading formTransitionLoading =
            new ResponsiveFormTransitionLoading();

        #endregion

        #region Runtime state

        static readonly ProfilerMarker NotifyClientsMarker =
            new ProfilerMarker("ResponsiveUI.Hub.NotifyClients");
        static readonly ProfilerMarker CanvasSettleMarker =
            new ProfilerMarker("ResponsiveUI.Hub.CanvasSettle");

        RectTransform _rootCanvasRect;
        readonly List<IResponsiveUiClient> _clients =
            new List<IResponsiveUiClient>();
        readonly List<IResponsiveUiClient> _earlyNotifyClients =
            new List<IResponsiveUiClient>();
        readonly List<IResponsiveUiClient> _lateNotifyClients =
            new List<IResponsiveUiClient>();

        const int MaxDeferredNotifyPasses = 4;
        int _notifyDepth;
        bool _notifyPending;
        bool _pendingNotifyLayoutOnly;
        bool _pendingNotifyAll;

        #endregion

        #region Form transition loading

        /// <summary>
        /// Play Mode form-switch cover (black overlay Image and/or loading controller).
        /// </summary>
        public ResponsiveFormTransitionLoading FormTransitionLoading =>
            formTransitionLoading ??= new ResponsiveFormTransitionLoading();

        protected override bool BeginFormTransitionLoadingIfNeeded(
            in ResponsiveDevice.LayoutKey previous,
            in ResponsiveDevice.LayoutKey next,
            bool hasAppliedBefore)
        {
            // Skip nested object work when the feature is off (common Edit / idle path).
            ResponsiveFormTransitionLoading loading = FormTransitionLoading;
            if (!loading.Enabled)
                return false;
            return loading.Begin(this, previous, next, hasAppliedBefore);
        }

        protected override void KeepFormTransitionLoadingVisible() =>
            FormTransitionLoading.KeepVisible();

        protected override void EndFormTransitionLoading() =>
            FormTransitionLoading.End();

        #endregion

        #region Clients

        public void RegisterClient(IResponsiveUiClient client)
        {
            if (client == null)
                return;
            UnregisterClient(client);
            _clients.Add(client);
        }

        public void UnregisterClient(IResponsiveUiClient client)
        {
            if (client == null)
                return;
            _clients.Remove(client);
        }

        protected override void NotifyLayoutClients(bool layoutOnly, bool notifyAllLayoutClients) =>
            NotifyClients(layoutOnly, notifyAllLayoutClients);

        /// <summary>
        /// Notifies registered clients for <see cref="ResponsiveUiHandler.CurrentLayout"/>.
        /// Re-entrant calls (a client, Active Object, or foldable event notifying again
        /// while a pass is running) are coalesced and re-run once the outer pass finishes,
        /// so the shared notify lists are never cleared mid-iteration.
        /// </summary>
        public void NotifyClients(bool layoutOnly, bool notifyAllLayoutClients = false)
        {
            if (_notifyDepth > 0)
            {
                QueueDeferredNotify(layoutOnly, notifyAllLayoutClients);
                return;
            }

            _notifyDepth++;
            try
            {
                NotifyClientsOnce(layoutOnly, notifyAllLayoutClients);
                for (int pass = 0;
                     _notifyPending && pass < MaxDeferredNotifyPasses;
                     pass++)
                {
                    bool nextLayoutOnly = _pendingNotifyLayoutOnly;
                    bool nextNotifyAll = _pendingNotifyAll;
                    _notifyPending = false;
                    NotifyClientsOnce(nextLayoutOnly, nextNotifyAll);
                }

                if (_notifyPending)
                {
                    Debug.LogWarning(
                        "[ResponsiveUi] NotifyClients kept re-entering; dropped the deferred pass.",
                        this);
                }
            }
            finally
            {
                _notifyDepth--;
                _notifyPending = false;
            }
        }

        void QueueDeferredNotify(bool layoutOnly, bool notifyAllLayoutClients)
        {
            if (!_notifyPending)
            {
                _notifyPending = true;
                _pendingNotifyLayoutOnly = layoutOnly;
                _pendingNotifyAll = notifyAllLayoutClients;
                return;
            }

            // A full notify subsumes a layout-only one; notify-all widens the set.
            _pendingNotifyLayoutOnly &= layoutOnly;
            _pendingNotifyAll |= notifyAllLayoutClients;
        }

        void NotifyClientsOnce(bool layoutOnly, bool notifyAllLayoutClients)
        {
            using var _ = NotifyClientsMarker.Auto();
            ResponsiveDevice.LayoutKey layout = CurrentLayout;
            SnapshotNotifyClients();
            ResponsiveUiApplyOrder.InsertionSortByPriority(
                _earlyNotifyClients);
            ResponsiveUiApplyOrder.InsertionSortByPriority(
                _lateNotifyClients);

            // Freeze each early batch, settle, then collect. Do not expand a batch
            // mid-Apply (that applied newly registered controllers in registration
            // order before the enabling batch had settled).
            int earlyProcessed = DrainEarlyClients(
                0, layout, layoutOnly, notifyAllLayoutClients);

            int lateIndex = 0;
            while (lateIndex < _lateNotifyClients.Count)
            {
                ApplyNotifyClient(
                    _lateNotifyClients[lateIndex],
                    layout,
                    layoutOnly,
                    notifyAllLayoutClients);
                lateIndex++;

                int earlyBefore = earlyProcessed;
                CollectNewNotifyClients();
                if (earlyProcessed < _earlyNotifyClients.Count)
                {
                    ResponsiveUiApplyOrder.InsertionSortByPriority(
                        _earlyNotifyClients, earlyProcessed);
                }

                earlyProcessed = DrainEarlyClients(
                    earlyProcessed,
                    layout,
                    layoutOnly,
                    notifyAllLayoutClients);

                if (earlyProcessed > earlyBefore)
                {
                    // Late clients already ran before this new controller geometry.
                    ResponsiveUiApplyOrder.InsertionSortByPriority(
                        _lateNotifyClients);
                    lateIndex = 0;
                    continue;
                }

                if (lateIndex < _lateNotifyClients.Count)
                {
                    ResponsiveUiApplyOrder.InsertionSortByPriority(
                        _lateNotifyClients, lateIndex);
                }
            }

            _earlyNotifyClients.Clear();
            _lateNotifyClients.Clear();
        }

        int DrainEarlyClients(
            int earlyProcessed,
            in ResponsiveDevice.LayoutKey layout,
            bool layoutOnly,
            bool notifyAllLayoutClients)
        {
            while (earlyProcessed < _earlyNotifyClients.Count)
            {
                int batchEnd = _earlyNotifyClients.Count;
                ApplyNotifyPhase(
                    _earlyNotifyClients,
                    earlyProcessed,
                    batchEnd,
                    layout,
                    layoutOnly,
                    notifyAllLayoutClients,
                    out bool geometryChanged);
                earlyProcessed = batchEnd;
                SettleControllerGeometry(
                    geometryChanged,
                    notifyAllLayoutClients);
                CollectNewNotifyClients();
                if (earlyProcessed < _earlyNotifyClients.Count)
                {
                    ResponsiveUiApplyOrder.InsertionSortByPriority(
                        _earlyNotifyClients, earlyProcessed);
                }
            }

            return earlyProcessed;
        }

        void SnapshotNotifyClients()
        {
            _earlyNotifyClients.Clear();
            _lateNotifyClients.Clear();
            for (int i = _clients.Count - 1; i >= 0; i--)
            {
                IResponsiveUiClient client = _clients[i];
                if (client is UnityEngine.Object obj && obj == null)
                {
                    _clients.RemoveAt(i);
                    continue;
                }
            }

            CollectNewNotifyClients();
        }

        void ApplyNotifyPhase(
            List<IResponsiveUiClient> clients,
            int start,
            int end,
            in ResponsiveDevice.LayoutKey layout,
            bool layoutOnly,
            bool notifyAllLayoutClients,
            out bool applied)
        {
            applied = false;
            for (int i = start; i < end; i++)
            {
                applied |= ApplyNotifyClient(
                    clients[i],
                    layout,
                    layoutOnly,
                    notifyAllLayoutClients);
            }
        }

        bool ApplyNotifyClient(
            IResponsiveUiClient client,
            in ResponsiveDevice.LayoutKey layout,
            bool layoutOnly,
            bool notifyAllLayoutClients)
        {
            if (!ShouldNotifyClient(
                    client, layoutOnly, notifyAllLayoutClients))
                return false;

            // One throwing client (e.g. a destroyed widget Target) must not leave every
            // later client on the previous device state or abort the hub Apply.
            try
            {
                if (client is ResponsiveUiController controller)
                    controller.ApplyFromHubStaged(this, layout, layoutOnly);
                else
                    client.ApplyFromHub(this, layout, layoutOnly);
            }
            // ExitGUIException is IMGUI control flow (an Inspector-driven Apply), not a
            // client failure — let it unwind the GUI event.
            catch (Exception e) when (!(e is ExitGUIException))
            {
                Debug.LogException(e, client as UnityEngine.Object ?? this);
            }
            // May have written geometry before throwing; still settle the batch.
            return true;
        }

        void CollectNewNotifyClients()
        {
            for (int i = 0; i < _clients.Count; i++)
            {
                IResponsiveUiClient client = _clients[i];
                if (client is UnityEngine.Object obj && obj == null)
                    continue;
                if (ContainsReference(_earlyNotifyClients, client) ||
                    ContainsReference(_lateNotifyClients, client))
                    continue;

                if (ResponsiveUiApplyOrder.ClientPriority(client) <
                    ResponsiveUiApplyOrder.CameraPriority)
                    _earlyNotifyClients.Add(client);
                else
                    _lateNotifyClients.Add(client);
            }
        }

        void ApplyControllerBackgrounds(bool notifyAllLayoutClients)
        {
            for (int i = 0; i < _earlyNotifyClients.Count; i++)
            {
                IResponsiveUiClient client = _earlyNotifyClients[i];
                if (client is ResponsiveUiController controller &&
                    controller.isActiveAndEnabled &&
                    controller.Hub == this &&
                    ShouldNotifyClient(
                        client,
                        layoutOnly: false,
                        notifyAllLayoutClients: notifyAllLayoutClients))
                {
                    controller.ApplyBackgrounds(this);
                }
            }
        }

        void SettleControllerGeometry(
            bool geometryChanged,
            bool notifyAllLayoutClients)
        {
            if (!geometryChanged)
                return;

            using (CanvasSettleMarker.Auto())
                Canvas.ForceUpdateCanvases();
            // Cover-fit before late clients (camera focus / scroll Relayout) so they
            // measure settled parent rects. layoutOnly size ticks used to defer cover
            // until after NotifyClients, which left Focus Bounds one frame behind.
            ApplyControllerBackgrounds(notifyAllLayoutClients);
        }

        static bool ContainsReference(
            List<IResponsiveUiClient> clients,
            IResponsiveUiClient client)
        {
            for (int i = 0; i < clients.Count; i++)
            {
                if (ReferenceEquals(clients[i], client))
                    return true;
            }

            return false;
        }

        bool ShouldNotifyClient(
            IResponsiveUiClient client,
            bool layoutOnly,
            bool notifyAllLayoutClients)
        {
            if (client == null ||
                client is UnityEngine.Object obj && obj == null)
                return false;
            if (layoutOnly &&
                !notifyAllLayoutClients &&
                !client.SizeChangeRequiresReapply())
                return false;
#if UNITY_EDITOR
            if (layoutOnly &&
                !Application.isPlaying &&
                client.IsAuthoringSelectedTarget())
                return false;
#endif
            return true;
        }

        internal bool AnyClientRequiresSizeReapply()
        {
            for (int i = 0; i < _clients.Count; i++)
            {
                IResponsiveUiClient client = _clients[i];
                if (client is UnityEngine.Object obj && obj == null)
                    continue;
                if (client.SizeChangeRequiresReapply())
                    return true;
            }

            return false;
        }

#if UNITY_EDITOR
        internal bool AnyClientIsAuthoringSelectedTarget()
        {
            for (int i = 0; i < _clients.Count; i++)
            {
                IResponsiveUiClient client = _clients[i];
                if (client is UnityEngine.Object obj && obj == null)
                    continue;
                if (client.IsAuthoringSelectedTarget())
                    return true;
            }

            return false;
        }
#endif

        #endregion

        #region Lifecycle

        void Reset()
        {
            enabled = false;
            overrideOriginalBgSize = false;
            originalBgSize = GetDefaultOriginalBgSize();
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            EditorBakeProjectBackgroundSize();
            base.OnValidate();
        }

        /// <summary>
        /// Copies Project Settings background size onto this component for player builds.
        /// Inspector Override keeps a local size.
        /// </summary>
        public bool EditorBakeProjectBackgroundSize()
        {
            if (overrideOriginalBgSize)
                return false;

            Vector2 next = GetDefaultOriginalBgSize();
            if (Mathf.Approximately(originalBgSize.x, next.x) &&
                Mathf.Approximately(originalBgSize.y, next.y))
                return false;

            originalBgSize = next;
            return true;
        }
#endif

        protected override void OnEnable()
        {
            InvalidateRootCanvasCache();
            base.OnEnable();
            FormTransitionLoading.EnsureReady();
        }

        protected override void OnDisable()
        {
            FormTransitionLoading.HideNow();
            InvalidateRootCanvasCache();
            base.OnDisable();
        }

        void OnTransformParentChanged() => InvalidateRootCanvasCache();

        /// <summary>
        /// Cover-fit controller backgrounds without re-applying widgets or events.
        /// Play Mode same-form size ticks use this when no client needs layout-only
        /// Apply. Simulator canvas-ready runs it after notifying clients.
        /// </summary>
        protected override void ApplyLayout()
        {
            for (int i = _clients.Count - 1; i >= 0; i--)
            {
                IResponsiveUiClient client = _clients[i];
                if (client is UnityEngine.Object obj && obj == null)
                {
                    _clients.RemoveAt(i);
                    continue;
                }

                if (client is ResponsiveUiController controller)
                    controller.ApplyBackgrounds(this);
            }
        }

        protected override bool SizeChangeRequiresWidgetReapply() =>
            AnyClientRequiresSizeReapply();

#if UNITY_EDITOR
        protected override bool IsAuthoringSelectedWidgetTarget() =>
            AnyClientIsAuthoringSelectedTarget();
#endif

        #endregion

        #region Editor

#if UNITY_EDITOR
        /// <summary>Edit Mode: resolve a Selection / Undo target to a <see cref="RectTransform"/>.</summary>
        public static bool GetEditorRectTransform(UnityEngine.Object selected, out RectTransform rt)
        {
            switch (selected)
            {
                case RectTransform rect:
                    rt = rect;
                    return true;
                case GameObject go when go.transform is RectTransform goRect:
                    rt = goRect;
                    return true;
                case Component component when component != null &&
                                             component.transform is RectTransform compRect:
                    rt = compRect;
                    return true;
                default:
                    rt = null;
                    return false;
            }
        }
#endif

        #endregion

        #region Design Scale

        /// <summary>
        /// Widget ratio in the slot's design coordinate space. Scale With Screen Size
        /// maps the authoritative canvas rect from scaler-reference units to design
        /// units. The active product state's default profile (Phone, Pad, Cover,
        /// Fold Expand, Flip Opened, Fold/Flip HalfOpened) is identity even when
        /// Expand inflates canvas.rect. Cameras use the same watch via
        /// <see cref="ResponsiveDesignScale"/> identity.
        /// <see cref="ResponsiveRectState.ResidualRatio"/> then removes ancestor localScale only
        /// when that canvas ratio is not 1.
        /// </summary>
        public Vector2 GetLayoutRatio(
            in ResponsiveRectState state,
            ResponsiveDevice.FormFactor deviceForm,
            RectTransform target = null,
            ResponsiveDevice.Orientation orientation = ResponsiveDevice.Orientation.Portrait)
        {
            if (!state.scaleFromDesignCanvas)
                return Vector2.one;
            Vector2 designSize = OrientSizeForSlot(
                state.GetResolvedDesignCanvasSize(deviceForm),
                deviceForm,
                orientation);
            Canvas canvas = LayoutSpaceCanvas(
                target, out bool localSized, out bool nestedUnsized);
            Vector2 liveSize = IsDesignCanvasReferenceIdentity(
                    target, designSize, orientation, deviceForm)
                ? designSize
                : GetWidgetDesignSpaceSize(
                    target,
                    ResponsiveDevice.OrientationForForm(deviceForm, orientation),
                    designSize,
                    canvas,
                    localSized,
                    nestedUnsized);
            Vector2 desired = state.ComputeLayoutRatio(
                liveSize,
                deviceForm,
                orientation);
            return ResponsiveRectState.ResidualRatio(
                desired,
                ResponsiveRectState.AncestorLocalScale(
                    target,
                    canvas != null ? canvas.transform : null));
        }

        /// <summary>
        /// True when the simulated device is this slot's default-profile reference.
        /// Inspector values then copy 1:1 onto the Target (Default Scale = localScale).
        /// Extra matches beyond the slot Design Canvas: Fold8 Cover safe-area, and Fold
        /// Expand / Dual Opened (Pad slots whose Design Canvas is iPad, watch is Fold
        /// inner). A constrained nested Canvas pane is not the device, so it stays
        /// off identity.
        /// </summary>
        bool IsDesignCanvasReferenceIdentity(
            RectTransform target,
            Vector2 orientedDesignSize,
            ResponsiveDevice.Orientation orientation,
            ResponsiveDevice.FormFactor deviceForm)
        {
            if (ResponsiveDesignScale.GetConstrainedCanvasSize(target, out _))
                return false;

            Vector2 watch = GetWatchSize();
            if (watch.x <= 0f || watch.y <= 0f)
                return false;

            return ResponsiveDesignScale.IsDesignCanvasReferenceWatch(
                deviceForm, watch, orientation, orientedDesignSize);
        }

        /// <summary>XY localScale applied to a widget under this hub.</summary>
        public Vector2 GetAppliedWidgetScale(
            in ResponsiveRectState state,
            ResponsiveDevice.FormFactor deviceForm,
            RectTransform target = null,
            ResponsiveDevice.Orientation orientation = ResponsiveDevice.Orientation.Portrait)
        {
            if (!state.scaleFromDesignCanvas)
                return state.GetResolvedDefaultScale(deviceForm);

            return GetAppliedWidgetScale(
                state,
                deviceForm,
                GetLayoutRatio(state, deviceForm, target, orientation));
        }

        /// <summary>
        /// XY localScale from an already-computed <see cref="GetLayoutRatio"/> result.
        /// Widget Apply should call this instead of the target overload so ratio is
        /// not solved twice.
        /// </summary>
        public Vector2 GetAppliedWidgetScale(
            in ResponsiveRectState state,
            ResponsiveDevice.FormFactor deviceForm,
            Vector2 layoutRatio)
        {
            Vector2 origin = state.GetResolvedDefaultScale(deviceForm);
            if (!state.scaleFromDesignCanvas)
                return origin;

            return Vector2.Scale(origin, layoutRatio);
        }

        /// <summary>
        /// Writes one widget Target from a resolved slot. Computes layout ratio once.
        /// </summary>
        public void ApplyWidget(
            RectTransform target,
            in ResponsiveRectState state,
            ResponsiveDevice.FormFactor deviceForm,
            ResponsiveDevice.Orientation orientation)
        {
            if (target == null)
                return;

            Vector2 ratio = GetLayoutRatio(state, deviceForm, target, orientation);
            state.Apply(
                target,
                ratio,
                GetAppliedWidgetScale(state, deviceForm, ratio));
        }

        Vector2 GetWidgetDesignSpaceSize(
            RectTransform target,
            ResponsiveDevice.Orientation screenOrientation,
            Vector2 designSize,
            Canvas canvas,
            bool localSized,
            bool nestedUnsized)
        {
            if (!UsesCanvasScalerResolutionFitFor(target) ||
                !GetCanvasSize(canvas, out Vector2 canvasSize))
            {
                // Match the Scale With Screen Size path below: full-display live size is
                // oriented to the resolved slot (Landscape → Portrait fallback measures a
                // portrait display). A constrained nested pane keeps its own rect.
                if (ResponsiveDesignScale.GetConstrainedCanvasSize(target, out Vector2 paneSize))
                    return paneSize;
                return OrientSize(GetDesignScaleCurrentSize(target), screenOrientation);
            }

            if (canvas.renderMode == RenderMode.WorldSpace)
                return canvasSize;

            if (!canvas.isRootCanvas)
                return canvasSize;

            // LayoutCanvasFor fallback: a nested canvas may exist with a zero-size rect.
            // A sized local canvas is already `canvas`, so do not walk parents again.
            if (!localSized && nestedUnsized)
                return canvasSize;

            Vector2 localSize = OrientSize(canvasSize, screenOrientation);
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            Vector2 referenceSize = scaler != null
                ? OrientSize(scaler.referenceResolution, screenOrientation)
                : Vector2.zero;
            return ResponsiveRectState.MapScalerCanvasToDesignSpace(
                localSize,
                referenceSize,
                designSize);
        }

        Canvas LayoutSpaceCanvas(
            RectTransform target,
            out bool localSized,
            out bool nestedUnsized)
        {
            localSized = false;
            nestedUnsized = false;
            if (target != null)
            {
                Canvas local = ResponsiveRectState.GetParentCanvas(target);
                if (GetCanvasSize(local, out _))
                {
                    localSized = true;
                    return local;
                }

                nestedUnsized = local != null && !local.isRootCanvas;
            }

            return LayoutCanvasFor(target);
        }

        static bool GetCanvasSize(Canvas canvas, out Vector2 size)
        {
            size = Vector2.zero;
            if (canvas != null && canvas.transform is RectTransform canvasRt)
            {
                size = canvasRt.rect.size;
                return size.x > 0f && size.y > 0f;
            }

            return false;
        }

        Canvas LayoutCanvasFor(RectTransform target)
        {
            Canvas scaled = FindParentScaleWithScreenSizeCanvas(target, out Canvas nearest);
            if (scaled != null)
                return scaled;
            if (nearest != null)
                return nearest.rootCanvas;

            return GetComponent<Canvas>();
        }

        #endregion

        #region Background art size

        /// <summary>
        /// Project Settings / built-in background art size (ignores per-component Override).
        /// </summary>
        public static Vector2 GetDefaultOriginalBgSize()
        {
            if (OriginalBgSizeProvider != null)
            {
                Vector2 size = OriginalBgSizeProvider();
                if (size.x > 0f && size.y > 0f)
                    return size;
            }

            return BuiltinOriginalBgSize;
        }

        /// <summary>
        /// Swap width/height so <paramref name="size"/> matches <paramref name="orientation"/>.
        /// Catalog portrait sizes become landscape by swapping.
        /// </summary>
        public static Vector2 OrientSize(Vector2 size, ResponsiveDevice.Orientation orientation)
        {
            if (size.x <= 0f || size.y <= 0f)
                return size;

            bool landscape = size.x >= size.y;
            if (orientation == ResponsiveDevice.Orientation.Landscape)
                return landscape ? size : new Vector2(size.y, size.x);

            return landscape ? new Vector2(size.y, size.x) : size;
        }

        /// <summary>
        /// <see cref="OrientSize(Vector2, ResponsiveDevice.Orientation)"/> for a slot: orients to
        /// the screen shape the slot shows (<see cref="ResponsiveDevice.OrientationForForm"/>), so
        /// Fold / Fold Cover Half Opened Portrait is the wide flat screen (Fold8 2448×1848) and
        /// Landscape the tall one (1848×2448). Other forms orient to the slot name.
        /// </summary>
        public static Vector2 OrientSizeForSlot(
            Vector2 size,
            ResponsiveDevice.FormFactor slotForm,
            ResponsiveDevice.Orientation slotOrientation) =>
            OrientSize(size, ResponsiveDevice.OrientationForForm(slotForm, slotOrientation));

        /// <summary>
        /// Cover-fit art size: Project Settings unless this component Overrides.
        /// Player builds use the baked serialized size when Override is off.
        /// </summary>
        public Vector2 GetOriginalBgSize()
        {
            if (overrideOriginalBgSize && originalBgSize.x > 0f && originalBgSize.y > 0f)
                return originalBgSize;

#if UNITY_EDITOR
            if (OriginalBgSizeProvider != null)
                return GetDefaultOriginalBgSize();
#endif

            if (originalBgSize.x > 0f && originalBgSize.y > 0f)
                return originalBgSize;

            return GetDefaultOriginalBgSize();
        }

        #endregion

        #region Canvas size

        /// <summary>Simulated device pixels for scene layout; canvas rect if Screen is 0.</summary>
        protected override Vector2 GetSceneLayoutSize() => GetDesignScaleCurrentSize();

        /// <summary>
        /// Live size for Design Scale. A constrained nested Canvas uses its pane rect;
        /// otherwise catalog Design Canvas is device pixels (e.g. iPhone 17 Pro Max
        /// 1320×2868), preferring Simulator catalog WxH then <c>UnityEngine.Device.Screen</c>.
        /// Editor <c>UnityEngine.Screen</c> is the Game View widget (Scale to Fit, e.g. ~1221)
        /// and must not be used — that ratio rewrote Default Scale to ~2.35 on enable.
        /// <c>canvas.rect</c> is CanvasScaler Expand space and is only a fallback when Screen is 0.
        /// </summary>
        public Vector2 GetDesignScaleCurrentSize(RectTransform target = null)
        {
            // Foldable layouts can constrain a nested Canvas to one pane. In an unscaled
            // hierarchy that pane, not the full display, is the widget's live design space.
            if (target != null &&
                ResponsiveDesignScale.GetConstrainedCanvasSize(target, out Vector2 nestedSize))
                return nestedSize;

            Vector2 watch = GetWatchSize();
            if (watch.x > 0f && watch.y > 0f)
                return watch;

            RectTransform canvasRt = GetRootCanvasRect();
            if (canvasRt == null)
                return watch;

            Vector2 canvasSize = canvasRt.rect.size;
            return canvasSize.x > 0f && canvasSize.y > 0f ? canvasSize : watch;
        }

        RectTransform GetRootCanvasRect()
        {
            if (_rootCanvasRect != null)
                return _rootCanvasRect;

            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
                return null;

            _rootCanvasRect = canvas.rootCanvas.transform as RectTransform;
            return _rootCanvasRect;
        }

        void InvalidateRootCanvasCache() => _rootCanvasRect = null;

        #endregion
    }

}
