using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FDPlus.ResponsiveUI
{
    public enum ResponsiveUiSide
    {
        Left,
        Right,
    }

    /// <summary>
    /// One UI object with a normal layout and optional tween start layout on each side.
    /// </summary>
    [Serializable]
    public struct ResponsiveTwoSideWidget
    {
        public string name;
        public bool animationEnabled;
        [Min(0f)] public float duration;
        [Min(0f)] public float delay;
        public AnimationCurve curve;
        [HideInInspector] public bool initialized;

        public static readonly string[] LayoutPropertyNames =
        {
            "leftNormal",
            "leftAnimationStart",
            "rightNormal",
            "rightAnimationStart",
        };

        public ResponsiveWidgetLayout leftNormal;
        public ResponsiveWidgetLayout leftAnimationStart;
        public ResponsiveWidgetLayout rightNormal;
        public ResponsiveWidgetLayout rightAnimationStart;

        public ResponsiveWidgetLayout GetLayout(
            ResponsiveUiSide side,
            bool animationStart)
        {
            if (animationStart && animationEnabled)
                return side == ResponsiveUiSide.Left ? leftAnimationStart : rightAnimationStart;
            return side == ResponsiveUiSide.Left ? leftNormal : rightNormal;
        }

        public RectTransform GetTarget(ResponsiveUiSide side, bool animationStart) =>
            GetLayout(side, animationStart).target;
    }

    /// <summary>
    /// Reusable responsive controller for UI that can live on either side of a Canvas.
    /// Every widget owns Normal and optional Animation Start layouts on both sides.
    /// Position transitions use the widget's duration, delay, and animation curve.
    /// </summary>
    [ExecuteAlways]
    public class ResponsiveTwoSideUiTweenController : MonoBehaviour, IResponsiveUiClient
    {
        [Tooltip("ResponsiveUI on the parent Canvas. Auto-assigned when possible.")]
        [SerializeField] ResponsiveUI responsiveUi;
        [SerializeField] ResponsiveUiSide side = ResponsiveUiSide.Right;
        [SerializeField] bool useUnscaledTime = true;
        [SerializeField] ResponsiveTwoSideWidget[] widgets =
            Array.Empty<ResponsiveTwoSideWidget>();
        [SerializeField, HideInInspector] bool previewAnimationStart = false;

        ResponsiveUI _hub;
        Coroutine _animation;
        bool[] _widgetVisible = Array.Empty<bool>();
        WidgetTween[] _tweens = Array.Empty<WidgetTween>();
        int[] _hierarchyOrder = Array.Empty<int>();
        int[] _hierarchyDepths = Array.Empty<int>();
        Vector3[] _startCenters = Array.Empty<Vector3>();
        readonly Vector3[] _corners = new Vector3[4];
#if UNITY_EDITOR
        [NonSerialized] bool _seedDefaultLayout;
        List<PositionTransition> _editorTransitions;
        bool _editorPreviewPlaying;
        bool _editorPreviewPaused;
        float _editorPreviewElapsed;
        double _editorPreviewLastTime;
        Action<RectTransform> _editorBeforePreviewWrite;
#endif

        public ResponsiveUI Hub => ResponsiveUiHub.Resolve(responsiveUi, this);

        public ResponsiveUiSide Side => side;

        public bool IsVisible
        {
            get
            {
                EnsureWidgetVisibleStates();
                if (_widgetVisible.Length == 0)
                    return true;

                for (int i = 0; i < _widgetVisible.Length; i++)
                {
                    if (!_widgetVisible[i])
                        return false;
                }

                return true;
            }
        }

        public ResponsiveTwoSideWidget[] Widgets => widgets;

        void Reset()
        {
            responsiveUi = ResponsiveUiHub.Resolve(null, this);
#if UNITY_EDITOR
            _seedDefaultLayout = true;
#endif
        }

        void OnEnable()
        {
            BindHub(true);
            ApplyImmediate();
        }

        void OnDisable()
        {
            StopAnimation();
#if UNITY_EDITOR
            StopEditorPreviewInternal();
#endif
            BindHub(false);
        }

        void OnTransformParentChanged()
        {
            BindHub(false);
            if (!isActiveAndEnabled)
                return;

            BindHub(true);
            ApplyImmediate();
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (responsiveUi == null)
                responsiveUi = ResponsiveUiHub.Resolve(null, this);

            InitializeWidgets();
            UnityEditor.EditorApplication.delayCall -= ApplyEditorPreview;
            UnityEditor.EditorApplication.delayCall += ApplyEditorPreview;
        }

        void ApplyEditorPreview()
        {
            if (this == null || !isActiveAndEnabled)
                return;

            ResponsiveUiHub.RebindIfChanged(
                this,
                this,
                ref responsiveUi,
                ref _hub);
            ApplyImmediate();
            UnityEditor.SceneView.RepaintAll();
            UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
        }

        public bool ConsumeSeedDefaultLayout()
        {
            bool seed = _seedDefaultLayout;
            _seedDefaultLayout = false;
            return seed;
        }

        void InitializeWidgets()
        {
            if (widgets == null)
                return;

            for (int i = 0; i < widgets.Length; i++)
            {
                ref ResponsiveTwoSideWidget widget = ref widgets[i];
                if (!widget.initialized)
                {
                    widget.animationEnabled = true;
                    widget.duration = 0.25f;
                    widget.curve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
                    widget.initialized = true;
                }

                CopyMissingLayouts(ref widget);
            }
        }

        static void CopyMissingLayouts(ref ResponsiveTwoSideWidget widget)
        {
            int assignedCount = 0;
            assignedCount += widget.leftNormal.target != null ? 1 : 0;
            assignedCount += widget.leftAnimationStart.target != null ? 1 : 0;
            assignedCount += widget.rightNormal.target != null ? 1 : 0;
            assignedCount += widget.rightAnimationStart.target != null ? 1 : 0;
            if (assignedCount != 1)
                return;

            ResponsiveWidgetLayout source = FirstAssignedLayout(widget);
            if (widget.leftNormal.target == null)
                widget.leftNormal = source;
            if (widget.leftAnimationStart.target == null)
                widget.leftAnimationStart = source;
            if (widget.rightNormal.target == null)
                widget.rightNormal = source;
            if (widget.rightAnimationStart.target == null)
                widget.rightAnimationStart = source;
        }

        static ResponsiveWidgetLayout FirstAssignedLayout(
            in ResponsiveTwoSideWidget widget)
        {
            if (widget.leftNormal.target != null)
                return widget.leftNormal;
            if (widget.leftAnimationStart.target != null)
                return widget.leftAnimationStart;
            if (widget.rightNormal.target != null)
                return widget.rightNormal;
            return widget.rightAnimationStart;
        }
#endif

        public void AssignHub(ResponsiveUI hub)
        {
            responsiveUi = hub;
            if (!isActiveAndEnabled)
                return;

            ResponsiveUiHub.RebindIfChanged(
                this,
                this,
                ref responsiveUi,
                ref _hub);
            ApplyImmediate();
        }

        public void SetSide(ResponsiveUiSide value) =>
            SetSideState(value, animate: true);

        public void SetSide(int value) =>
            SetSide(value == 0 ? ResponsiveUiSide.Left : ResponsiveUiSide.Right);

        public void SetSideImmediate(ResponsiveUiSide value) =>
            SetSideState(value, animate: false);

        public void SetVisible(bool value) =>
            SetAllVisible(value, animate: true);

        public void SetVisibleImmediate(bool value) =>
            SetAllVisible(value, animate: false);

        public void SetVisible(Transform target, bool value) =>
            SetTargetVisible(target, value, animate: true);

        public void SetVisibleImmediate(Transform target, bool value) =>
            SetTargetVisible(target, value, animate: false);

        public void SetVisible(int widgetIndex, bool value) =>
            SetWidgetVisible(widgetIndex, value, animate: true);

        public void SetVisibleImmediate(int widgetIndex, bool value) =>
            SetWidgetVisible(widgetIndex, value, animate: false);

        public void Show() => SetVisible(true);

        public void Hide() => SetVisible(false);

        public void Trigger(bool value) => SetVisible(value);

        public bool IsWidgetVisible(Transform target) =>
            FindWidgetIndex(target, out int index) &&
            IsWidgetVisible(index);

        public bool IsWidgetVisible(int index)
        {
            EnsureWidgetVisibleStates();
            return index >= 0 &&
                index < _widgetVisible.Length &&
                _widgetVisible[index];
        }

        public bool FindWidgetIndex(Transform target, out int index)
        {
            index = -1;
            if (target == null || widgets == null)
                return false;

            for (Transform current = target;
                 current != null;
                 current = current.parent)
            {
                if (current is not RectTransform rect)
                    continue;

                for (int i = 0; i < widgets.Length; i++)
                {
                    if (!OwnsTarget(widgets[i], rect))
                        continue;

                    index = i;
                    return true;
                }
            }

            return false;
        }

        public void Preview() => ApplyImmediate();

#if UNITY_EDITOR
        public bool EditorPreviewPlaying => _editorPreviewPlaying;

        public bool EditorPreviewPaused => _editorPreviewPaused;

        public bool EditorCanPlayPreview
        {
            get
            {
                ResponsiveUI hub = Hub;
                if (hub == null || !hub.enabled || widgets == null)
                    return false;

                for (int i = 0; i < widgets.Length; i++)
                {
                    if (widgets[i].animationEnabled &&
                        LayoutRef(i, animationStart: false).target != null)
                        return true;
                }
                return false;
            }
        }

        public void EditorPlayPreview(
            Action<RectTransform> beforePreviewWrite)
        {
            if (_editorPreviewPlaying && _editorPreviewPaused)
            {
                _editorPreviewPaused = false;
                _editorPreviewLastTime =
                    UnityEditor.EditorApplication.timeSinceStartup;
                return;
            }

            StopEditorPreviewInternal();
            ResponsiveUI hub = Hub;
            if (hub == null || !hub.enabled || widgets == null)
                return;

            _editorBeforePreviewWrite = beforePreviewWrite;
            previewAnimationStart = true;
            NotifyEditorPreviewWriteTargets(animationStart: true);
            ApplyLayouts(hub, hub.CurrentLayout);

            Vector3[] startCenters = new Vector3[widgets.Length];
            Vector3[] corners = new Vector3[4];
            for (int i = 0; i < widgets.Length; i++)
            {
                RectTransform target = LayoutRef(i, animationStart: true).target;
                if (target != null)
                    startCenters[i] = WorldCenter(target, corners);
            }

            previewAnimationStart = false;
            _editorTransitions =
                new List<PositionTransition>(widgets.Length);
            int[] order = GetHierarchyOrder();
            for (int i = 0; i < order.Length; i++)
            {
                int widgetIndex = order[i];
                ref ResponsiveTwoSideWidget widget = ref widgets[widgetIndex];
                ref ResponsiveWidgetLayout layout =
                    ref LayoutRef(widgetIndex, animationStart: false);
                if (layout.target == null)
                    continue;

                _editorBeforePreviewWrite?.Invoke(layout.target);
                layout.Apply(hub, hub.CurrentLayout);
                if (!widget.animationEnabled)
                    continue;

                Vector2 destination = layout.target.anchoredPosition;
                Vector3 destinationCenter = WorldCenter(
                    layout.target,
                    corners);
                layout.target.position +=
                    startCenters[widgetIndex] - destinationCenter;
                _editorTransitions.Add(new PositionTransition(
                    layout.target,
                    layout.target.anchoredPosition,
                    destination,
                    widget.duration,
                    widget.delay,
                    widget.curve));
            }

            if (_editorTransitions.Count == 0)
            {
                StopEditorPreviewInternal();
                return;
            }

            _editorPreviewPlaying = true;
            _editorPreviewPaused = false;
            _editorPreviewElapsed = 0f;
            _editorPreviewLastTime =
                UnityEditor.EditorApplication.timeSinceStartup;
            UnityEditor.EditorApplication.update -= UpdateEditorPreview;
            UnityEditor.EditorApplication.update += UpdateEditorPreview;
        }

        public void EditorPausePreview()
        {
            if (!_editorPreviewPlaying)
                return;

            _editorPreviewPaused = !_editorPreviewPaused;
            _editorPreviewLastTime =
                UnityEditor.EditorApplication.timeSinceStartup;
        }

        public void EditorStopPreview()
        {
            StopEditorPreviewInternal();
            previewAnimationStart = false;
            ApplyImmediate();
        }

        void UpdateEditorPreview()
        {
            double now = UnityEditor.EditorApplication.timeSinceStartup;
            if (_editorPreviewPaused)
            {
                _editorPreviewLastTime = now;
                return;
            }

            _editorPreviewElapsed += (float)(now - _editorPreviewLastTime);
            _editorPreviewLastTime = now;
            bool complete = true;
            for (int i = 0; i < _editorTransitions.Count; i++)
            {
                PositionTransition transition = _editorTransitions[i];
                if (transition.Target == null)
                    continue;

                bool delayComplete =
                    _editorPreviewElapsed >= transition.Delay;
                float progress = !delayComplete
                    ? 0f
                    : transition.Duration > 0f
                        ? Mathf.Clamp01(
                            (_editorPreviewElapsed - transition.Delay) /
                            transition.Duration)
                        : 1f;
                _editorBeforePreviewWrite?.Invoke(transition.Target);
                transition.Target.anchoredPosition =
                    Vector2.LerpUnclamped(
                        transition.Start,
                        transition.End,
                        EvaluateCurve(transition.Curve, progress));
                if (!delayComplete || progress < 1f)
                    complete = false;
            }

            UnityEditor.SceneView.RepaintAll();
            UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
            if (complete)
                StopEditorPreviewInternal();
        }

        void StopEditorPreviewInternal()
        {
            UnityEditor.EditorApplication.update -= UpdateEditorPreview;
            _editorPreviewPlaying = false;
            _editorPreviewPaused = false;
            _editorTransitions = null;
            _editorBeforePreviewWrite = null;
        }

        void NotifyEditorPreviewWriteTargets(bool animationStart)
        {
            for (int i = 0; i < widgets.Length; i++)
            {
                RectTransform target = LayoutRef(i, animationStart).target;
                if (target != null)
                    _editorBeforePreviewWrite?.Invoke(target);
            }
        }
#endif

        public void ApplyFromHub(
            ResponsiveUI hub,
            in ResponsiveDevice.LayoutKey layout,
            bool layoutOnly)
        {
            if (!isActiveAndEnabled || !hub.enabled)
                return;
            if (!ResponsiveUiHub.BeginApplyFromHub(
                    this, this, hub, ref responsiveUi, ref _hub, layoutOnly))
                return;

            // Layout-only passes must not snap a running tween.
            // Idle sibling widgets (the other panel) still re-layout.
            if (layoutOnly)
            {
#if UNITY_EDITOR
                if (_editorPreviewPlaying)
                    return;
#endif
                ApplyLayouts(hub, layout, skipAnimating: true);
                return;
            }

            StopAnimation();
            ApplyLayouts(hub, layout);
        }

        /// <summary>
        /// Play Mode same-form size ticks: Design Scale widgets that are not under
        /// a Scale With Screen Size canvas. Simulator canvas-ready always notifies.
        /// </summary>
        public bool SizeChangeRequiresReapply()
        {
            if (widgets == null)
                return false;

            ResponsiveUI hub = _hub != null ? _hub : Hub;
            EnsureWidgetVisibleStates();
            ResponsiveDevice.LayoutKey layout = hub != null
                ? hub.CurrentLayout
                : default;
            for (int i = 0; i < widgets.Length; i++)
            {
                ref ResponsiveWidgetLayout layoutState = ref CurrentLayoutRef(i);
                layoutState.ResolveActiveSlot(
                    layout,
                    out _,
                    out _,
                    out _,
                    out ResponsiveRectState state);
                if (ResponsiveUiApplyOrder.RequiresDesignScaleSizeReapply(
                        hub, layoutState.target, state.scaleFromDesignCanvas))
                    return true;
            }

            return false;
        }

#if UNITY_EDITOR
        public bool IsAuthoringSelectedTarget()
        {
            if (widgets == null)
                return false;

            UnityEngine.Object[] selected = UnityEditor.Selection.objects;
            for (int s = 0; s < selected.Length; s++)
            {
                if (!ResponsiveUI.GetEditorRectTransform(
                        selected[s],
                        out RectTransform target))
                    continue;

                for (int i = 0; i < widgets.Length; i++)
                {
                    if (OwnsTarget(widgets[i], target))
                        return true;
                }
            }

            return false;
        }

        public bool EditorOwnsWidgetTarget(RectTransform target)
        {
            if (target == null || widgets == null)
                return false;

            for (int i = 0; i < widgets.Length; i++)
            {
                if (OwnsTarget(widgets[i], target))
                    return true;
            }

            return false;
        }
#endif

        void SetSideState(ResponsiveUiSide nextSide, bool animate)
        {
            if (side == nextSide)
                return;

            side = nextSide;
            ApplyOrAnimateAll(animate);
        }

        void SetAllVisible(bool visible, bool animate)
        {
            EnsureWidgetVisibleStates();
            bool changed = false;
            for (int i = 0; i < _widgetVisible.Length; i++)
            {
                if (_widgetVisible[i] == visible)
                    continue;

                _widgetVisible[i] = visible;
                changed = true;
            }

            if (!changed && (!animate || !AnyAnimating()))
                return;

            ApplyOrAnimateAll(animate);
        }

        void SetTargetVisible(Transform target, bool visible, bool animate)
        {
            if (!FindWidgetIndex(target, out int index))
                return;

            SetWidgetVisible(index, visible, animate);
        }

        void SetWidgetVisible(int widgetIndex, bool visible, bool animate)
        {
            EnsureWidgetVisibleStates();
            if (widgetIndex < 0 || widgetIndex >= _widgetVisible.Length)
                return;
            if (_widgetVisible[widgetIndex] == visible &&
                (!animate || !_tweens[widgetIndex].Active))
                return;

            _widgetVisible[widgetIndex] = visible;
            if (!Application.isPlaying || !animate)
            {
                ApplyWidgetImmediate(widgetIndex);
                return;
            }

            StartWidgetTransition(widgetIndex);
        }

        void ApplyOrAnimateAll(bool animate)
        {
            if (!Application.isPlaying || !animate)
            {
                ApplyImmediate();
                return;
            }

            StartTransitionAll();
        }

        void BindHub(bool bind) =>
            ResponsiveUiHub.BindClient(
                this,
                this,
                ref responsiveUi,
                ref _hub,
                bind);

        void ApplyImmediate()
        {
            StopAnimation();
#if UNITY_EDITOR
            if (!Application.isPlaying)
                StopEditorPreviewInternal();
#endif
            ResponsiveUI hub = Hub;
            if (hub == null || !hub.enabled)
                return;
            if (hub.ApplyInFlight)
                return;

            ApplyLayouts(hub, hub.CurrentLayout);
        }

#if UNITY_EDITOR
        /// <summary>
        /// Inspector bulk / Enabled-toggle preview. Slot Enabled chooses layout
        /// (Cover off → Phone); it does not SetActive Targets.
        /// </summary>
        public void EditorApplyLayouts(in ResponsiveDevice.LayoutKey layout)
        {
            ResponsiveUI hub = Hub;
            if (!isActiveAndEnabled || hub == null || !hub.enabled)
                return;
            ApplyLayouts(hub, layout);
        }
#endif

        void ApplyLayouts(
            ResponsiveUI hub,
            in ResponsiveDevice.LayoutKey layout,
            bool skipAnimating = false)
        {
            if (widgets == null)
                return;

            EnsureWidgetVisibleStates();
            int[] order = GetHierarchyOrder();
            for (int i = 0; i < order.Length; i++)
            {
                int widgetIndex = order[i];
                if (skipAnimating && _tweens[widgetIndex].Active)
                {
                    // The tween's End was solved for the old geometry; re-apply the
                    // live layout once it finishes instead of resting on a stale End.
                    _tweens[widgetIndex].Stale = true;
                    continue;
                }

                CurrentLayoutRef(widgetIndex).Apply(hub, layout);
            }
        }

        void ApplyWidgetImmediate(int widgetIndex)
        {
            StopWidgetTween(widgetIndex);
            ResponsiveUI hub = Hub;
            if (hub == null || !hub.enabled || widgets == null)
                return;

            CurrentLayoutRef(widgetIndex).Apply(hub, hub.CurrentLayout);
        }

        void StartTransitionAll()
        {
            EnsureWidgetVisibleStates();
            if (widgets == null)
                return;

            CaptureStartCenters();
            int[] order = GetHierarchyOrder();
            for (int i = 0; i < order.Length; i++)
                StartWidgetTransition(order[i], _startCenters[order[i]]);
        }

        void StartWidgetTransition(int widgetIndex)
        {
            EnsureWidgetVisibleStates();
            RectTransform target = GetCurrentTarget(widgetIndex);
            Vector3 startCenter = target != null
                ? WorldCenter(target, _corners)
                : default;
            StartWidgetTransition(widgetIndex, startCenter);
        }

        void StartWidgetTransition(int widgetIndex, Vector3 startCenter)
        {
            StopWidgetTween(widgetIndex);
            ResponsiveUI hub = Hub;
            if (hub == null || !hub.enabled || widgets == null)
                return;

            ref ResponsiveTwoSideWidget widget = ref widgets[widgetIndex];
            ref ResponsiveWidgetLayout layout = ref CurrentLayoutRef(widgetIndex);
            if (layout.target == null)
                return;

            layout.Apply(hub, hub.CurrentLayout);
            if (!widget.animationEnabled)
                return;
            // Inactive host cannot run the tween coroutine: stay snapped at the end
            // layout just applied (no StartCoroutine error, no stuck Active flag).
            if (!isActiveAndEnabled)
                return;

            Vector2 destination = layout.target.anchoredPosition;
            Vector3 destinationCenter = WorldCenter(layout.target, _corners);
            layout.target.position += startCenter - destinationCenter;
            _tweens[widgetIndex].Elapsed = 0f;
            _tweens[widgetIndex].Stale = false;
            _tweens[widgetIndex].Active = true;
            _tweens[widgetIndex].Transition = new PositionTransition(
                layout.target,
                layout.target.anchoredPosition,
                destination,
                widget.duration,
                widget.delay,
                widget.curve);
            if (_animation == null)
                _animation = StartCoroutine(AnimatePositions());
        }

        void CaptureStartCenters()
        {
            int count = widgets.Length;
            if (_startCenters.Length != count)
                _startCenters = new Vector3[count];

            for (int i = 0; i < count; i++)
            {
                RectTransform target = GetCurrentTarget(i);
                _startCenters[i] = target != null
                    ? WorldCenter(target, _corners)
                    : default;
            }
        }

        IEnumerator AnimatePositions()
        {
            while (true)
            {
                float delta = useUnscaledTime
                    ? Time.unscaledDeltaTime
                    : Time.deltaTime;
                bool any = false;
                for (int i = 0; i < _tweens.Length; i++)
                {
                    if (!_tweens[i].Active)
                        continue;

                    any = true;
                    WidgetTween tween = _tweens[i];
                    tween.Elapsed += delta;
                    PositionTransition transition = tween.Transition;
                    if (transition.Target == null)
                    {
                        tween.Active = false;
                        _tweens[i] = tween;
                        continue;
                    }

                    bool delayComplete = tween.Elapsed >= transition.Delay;
                    float progress = !delayComplete
                        ? 0f
                        : transition.Duration > 0f
                            ? Mathf.Clamp01(
                                (tween.Elapsed - transition.Delay) /
                                transition.Duration)
                            : 1f;
                    transition.Target.anchoredPosition = Vector2.LerpUnclamped(
                        transition.Start,
                        transition.End,
                        EvaluateCurve(transition.Curve, progress));
                    bool finishedStale = false;
                    if (delayComplete && progress >= 1f)
                    {
                        transition.Target.anchoredPosition = transition.End;
                        tween.Active = false;
                        finishedStale = tween.Stale;
                        tween.Stale = false;
                    }

                    _tweens[i] = tween;
                    if (finishedStale)
                        ApplyFinishedStaleWidget(i);
                }

                if (!any)
                    break;

                yield return null;
            }

            _animation = null;
        }

        /// <summary>
        /// A layout-only hub pass skipped this widget while it was tweening. Apply the
        /// current device layout now so it rests where the live geometry puts it.
        /// </summary>
        void ApplyFinishedStaleWidget(int widgetIndex)
        {
            ResponsiveUI hub = _hub != null ? _hub : Hub;
            if (hub == null || !hub.enabled || widgets == null ||
                widgetIndex >= widgets.Length)
                return;

            CurrentLayoutRef(widgetIndex).Apply(hub, hub.CurrentLayout);
        }

        int[] GetHierarchyOrder()
        {
            EnsureWidgetVisibleStates();
            int count = widgets != null ? widgets.Length : 0;
            if (_hierarchyOrder.Length != count)
            {
                _hierarchyOrder = new int[count];
                _hierarchyDepths = new int[count];
            }

            for (int i = 0; i < count; i++)
            {
                _hierarchyOrder[i] = i;
                _hierarchyDepths[i] = ResponsiveUiApplyOrder.HierarchyDepth(
                    CurrentLayoutRef(i).target);
            }

            ResponsiveUiApplyOrder.InsertionSortByDepth(
                _hierarchyOrder, _hierarchyDepths, count);
            return _hierarchyOrder;
        }

        ref ResponsiveWidgetLayout CurrentLayoutRef(int widgetIndex)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                return ref LayoutRef(widgetIndex, previewAnimationStart);
#endif
            return ref LayoutRef(widgetIndex, !_widgetVisible[widgetIndex]);
        }

        ref ResponsiveWidgetLayout LayoutRef(int widgetIndex, bool animationStart)
        {
            ref ResponsiveTwoSideWidget widget = ref widgets[widgetIndex];
            if (animationStart && widget.animationEnabled)
            {
                if (side == ResponsiveUiSide.Left)
                    return ref widget.leftAnimationStart;
                return ref widget.rightAnimationStart;
            }

            if (side == ResponsiveUiSide.Left)
                return ref widget.leftNormal;
            return ref widget.rightNormal;
        }

        RectTransform GetCurrentTarget(int widgetIndex) =>
            CurrentLayoutRef(widgetIndex).target;

        void EnsureWidgetVisibleStates()
        {
            int count = widgets != null ? widgets.Length : 0;
            if (_widgetVisible.Length == count && _tweens.Length == count)
                return;

            bool[] nextVisible = new bool[count];
            for (int i = 0; i < count; i++)
            {
                nextVisible[i] = i < _widgetVisible.Length
                    ? _widgetVisible[i]
                    : true;
            }

            StopAnimation();
            _widgetVisible = nextVisible;
            _tweens = new WidgetTween[count];
        }

        bool AnyAnimating()
        {
            for (int i = 0; i < _tweens.Length; i++)
            {
                if (_tweens[i].Active)
                    return true;
            }

            return false;
        }

        static bool OwnsTarget(
            in ResponsiveTwoSideWidget widget,
            RectTransform target) =>
            target == widget.leftNormal.target ||
            target == widget.leftAnimationStart.target ||
            target == widget.rightNormal.target ||
            target == widget.rightAnimationStart.target;

        /// <summary>
        /// Curve value at <paramref name="t"/>. A missing or key-less curve
        /// (code-built widget, never inspected) is linear.
        /// </summary>
        internal static float EvaluateCurve(AnimationCurve curve, float t)
        {
            if (curve == null || curve.length == 0)
                return t;
            return curve.Evaluate(t);
        }

        static Vector3 WorldCenter(RectTransform target, Vector3[] corners)
        {
            target.GetWorldCorners(corners);
            return (corners[0] + corners[2]) * 0.5f;
        }

        void StopAnimation()
        {
            for (int i = 0; i < _tweens.Length; i++)
                _tweens[i].Active = false;

            if (_animation == null)
                return;

            StopCoroutine(_animation);
            _animation = null;
        }

        void StopWidgetTween(int index)
        {
            if (index < 0 || index >= _tweens.Length)
                return;

            _tweens[index].Active = false;
        }

        struct WidgetTween
        {
            public bool Active;
            // A layout-only pass was skipped mid-tween; re-apply on completion.
            public bool Stale;
            public float Elapsed;
            public PositionTransition Transition;
        }

        readonly struct PositionTransition
        {
            public readonly RectTransform Target;
            public readonly Vector2 Start;
            public readonly Vector2 End;
            public readonly float Duration;
            public readonly float Delay;
            public readonly AnimationCurve Curve;

            public PositionTransition(
                RectTransform target,
                Vector2 start,
                Vector2 end,
                float duration,
                float delay,
                AnimationCurve curve)
            {
                Target = target;
                Start = start;
                End = end;
                Duration = duration;
                Delay = delay;
                Curve = curve;
            }
        }
    }
}
