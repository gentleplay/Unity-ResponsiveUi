using System;
using UnityEngine;
using UnityEngine.UI;

namespace FDPlus.ResponsiveUI
{
    /// <summary>
    /// Per-group widgets, backgrounds, and Active Objects. Add to a Canvas child
    /// (or the same GameObject as <see cref="ResponsiveUI"/>) and point at the hub.
    /// </summary>
    [ExecuteAlways]
    public class ResponsiveUiController : MonoBehaviour, IResponsiveUiClient
    {
        #region Serialized

        [Tooltip("ResponsiveUI on the parent Canvas. Auto-assigns when you add this component.")]
        [SerializeField] ResponsiveUI responsiveUi;
        [SerializeField] ResponsiveBackground[] backgrounds;
        [SerializeField] ResponsiveWidgetLayout[] widgets;
        [SerializeField] ResponsiveActiveObjectEntry[] activeObjects;

        #endregion

        #region Runtime state

        ResponsiveUI _hub;
        int[] _widgetApplyOrder = Array.Empty<int>();
        int[] _widgetApplyDepths = Array.Empty<int>();
#if UNITY_EDITOR
        [NonSerialized] bool _seedDefaultLayout;
#endif

        #endregion

        #region Access

        public ResponsiveUI Hub => ResponsiveUiHub.Resolve(responsiveUi, this);

        public ResponsiveWidgetLayout[] Widgets => widgets;

        public ResponsiveBackground[] Backgrounds => backgrounds;

        public ResponsiveActiveObjectEntry[] ActiveObjects => activeObjects;

        #endregion

        #region Lifecycle

        void Reset()
        {
            enabled = true;
            AssignHub(ResponsiveUiHub.Resolve(null, this));
#if UNITY_EDITOR
            _seedDefaultLayout = true;
#endif
        }

        void OnEnable()
        {
            BindHub(true);
            ApplyNow(layoutOnly: false);
        }

        void OnDisable() => BindHub(false);

        void OnTransformParentChanged()
        {
            BindHub(false);
            if (!isActiveAndEnabled)
                return;
            BindHub(true);
            ApplyNow(layoutOnly: false);
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (responsiveUi == null)
                responsiveUi = ResponsiveUiHub.Resolve(null, this);
            QueueHubRebind();
        }

        void QueueHubRebind()
        {
            if (!isActiveAndEnabled || ResponsiveUiHub.Resolve(responsiveUi, this) == _hub)
                return;
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this == null || !isActiveAndEnabled)
                    return;
                AssignHub(responsiveUi);
            };
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

        void BindHub(bool bind) =>
            ResponsiveUiHub.BindClient(this, this, ref responsiveUi, ref _hub, bind);

        void ApplyNow(bool layoutOnly) => ResponsiveUiHub.ApplyNow(this, layoutOnly);

        #endregion

        #region IResponsiveUiClient

        public void ApplyFromHub(
            ResponsiveUI hub,
            in ResponsiveDevice.LayoutKey layout,
            bool layoutOnly) =>
            ApplyFromHub(hub, layout, layoutOnly, deferBackgrounds: false);

        internal void ApplyFromHubStaged(
            ResponsiveUI hub,
            in ResponsiveDevice.LayoutKey layout,
            bool layoutOnly) =>
            ApplyFromHub(hub, layout, layoutOnly, deferBackgrounds: true);

        void ApplyFromHub(
            ResponsiveUI hub,
            in ResponsiveDevice.LayoutKey layout,
            bool layoutOnly,
            bool deferBackgrounds)
        {
            if (!isActiveAndEnabled || !hub.enabled)
                return;
            if (!ResponsiveUiHub.BeginApplyFromHub(
                    this, this, hub, ref responsiveUi, ref _hub, layoutOnly))
                return;

            if (!layoutOnly)
                ResponsiveActiveObjects.Apply(activeObjects, layout);

            // Widgets first so Match Mode cover reads the live parent rect.
            ApplyWidgets(hub, layout);
            // Hub notification stages every controller before one shared canvas settle.
            // A direct client Apply still owns its local settle + background pass.
            if (!layoutOnly && !deferBackgrounds)
            {
                Canvas.ForceUpdateCanvases();
                ApplyBackgrounds(hub);
            }
        }

        /// <summary>
        /// Play Mode same-form size ticks: Design Scale widgets (not under a
        /// Scale With Screen Size canvas) need a layout-only client Apply.
        /// Backgrounds do not — cover uses hub <c>ApplyLayout</c>.
        /// Device Simulator canvas-ready notifies this client regardless.
        /// </summary>
        public bool SizeChangeRequiresReapply()
        {
            if (widgets == null || widgets.Length == 0)
                return false;

            ResponsiveUI hub = _hub != null ? _hub : Hub;
            ResponsiveDevice.LayoutKey layout = hub != null
                ? hub.CurrentLayout
                : default;
            for (int i = 0; i < widgets.Length; i++)
            {
                ref ResponsiveWidgetLayout widget = ref widgets[i];
                widget.ResolveActiveSlot(
                    layout,
                    out _,
                    out _,
                    out _,
                    out ResponsiveRectState state);
                if (ResponsiveUiApplyOrder.RequiresDesignScaleSizeReapply(
                        hub, widget.target, state.scaleFromDesignCanvas))
                    return true;
            }

            return false;
        }

#if UNITY_EDITOR
        public bool IsAuthoringSelectedTarget()
        {
            if (widgets == null || widgets.Length == 0)
                return false;

            UnityEngine.Object[] selected = UnityEditor.Selection.objects;
            if (selected == null || selected.Length == 0)
                return false;

            for (int i = 0; i < widgets.Length; i++)
            {
                ref ResponsiveWidgetLayout widget = ref widgets[i];
                RectTransform target = widget.target;
                if (target == null)
                    continue;

                for (int s = 0; s < selected.Length; s++)
                {
                    if (!ResponsiveUI.GetEditorRectTransform(selected[s], out RectTransform rt))
                        continue;
                    if (rt == target)
                        return true;
                }
            }

            return false;
        }

        public bool EditorOwnsWidgetTarget(RectTransform rt)
        {
            if (rt == null || widgets == null || widgets.Length == 0)
                return false;

            for (int i = 0; i < widgets.Length; i++)
            {
                ref ResponsiveWidgetLayout widget = ref widgets[i];
                if (widget.target == rt)
                    return true;
            }

            return false;
        }

        public bool ConsumeSeedDefaultLayout()
        {
            bool seed = _seedDefaultLayout;
            _seedDefaultLayout = false;
            return seed;
        }
#endif

        #endregion

        #region Apply

        internal void ApplyBackgrounds(ResponsiveUI hub)
        {
            if (hub == null || !hub.enabled)
                return;
            // Edit Mode: allow inactive panels (Settings starts disabled in the hierarchy).
            // Play Mode: only live objects.
#if UNITY_EDITOR
            if (Application.isPlaying && !isActiveAndEnabled)
                return;
#else
            if (!isActiveAndEnabled)
                return;
#endif

            if (backgrounds == null || backgrounds.Length == 0)
                return;

            Vector2 fallbackArt = hub.GetOriginalBgSize();
            if (!ApplyBackgrounds(fallbackArt))
            {
                Canvas.ForceUpdateCanvases();
                ApplyBackgrounds(fallbackArt);
            }
        }

#if UNITY_EDITOR
        /// <summary>Inspector Match Mode / Image edits re-cover without a full hub Apply.</summary>
        public void EditorApplyBackgrounds()
        {
            ResponsiveUI hub = Hub;
            if (hub == null || !hub.enabled)
                return;
            ApplyBackgrounds(hub);
        }

        /// <summary>
        /// Inspector bulk edits preview this controller's widgets only.
        /// A missing or disabled hub leaves the serialized edit saved without
        /// mutating scene targets.
        /// </summary>
        public void EditorApplyWidgets(in ResponsiveDevice.LayoutKey layout)
        {
            ResponsiveUI hub = Hub;
            if (!isActiveAndEnabled || hub == null || !hub.enabled)
                return;
            ApplyWidgets(hub, layout);
        }
#endif

        bool ApplyBackgrounds(Vector2 fallbackArt)
        {
            bool skippedEmptyParent = false;
            for (int i = 0; i < backgrounds.Length; i++)
            {
                Image bg = backgrounds[i].image;
                if (bg == null)
                    continue;
                if (!ResponsiveBackgroundCover.GetCoverParentSize(bg, out Vector2 parentSize))
                {
                    skippedEmptyParent = true;
                    continue;
                }

                ResponsiveBackgroundCover.ApplyBackground(
                    bg, fallbackArt, parentSize, backgrounds[i].coverMode);
            }

            return !skippedEmptyParent;
        }

        void ApplyWidgets(ResponsiveUI hub, in ResponsiveDevice.LayoutKey layout)
        {
            if (widgets == null || widgets.Length == 0)
                return;

            int[] order = GetWidgetApplyOrder();
            for (int n = 0; n < order.Length; n++)
            {
                ref ResponsiveWidgetLayout widget = ref widgets[order[n]];
                widget.Apply(hub, layout);
            }
        }

        int[] GetWidgetApplyOrder()
        {
            int count = widgets.Length;
            if (_widgetApplyOrder.Length != count)
            {
                _widgetApplyOrder = new int[count];
                _widgetApplyDepths = new int[count];
            }

            for (int i = 0; i < count; i++)
            {
                _widgetApplyOrder[i] = i;
                _widgetApplyDepths[i] = ResponsiveUiApplyOrder.HierarchyDepth(widgets[i].target);
            }

            ResponsiveUiApplyOrder.InsertionSortByDepth(
                _widgetApplyOrder, _widgetApplyDepths, count);
            return _widgetApplyOrder;
        }

        #endregion
    }
}
