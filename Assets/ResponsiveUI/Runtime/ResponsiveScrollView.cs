using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace FDPlus.ResponsiveUI
{
    /// <summary>
    /// Per-slot FancyScrollView Cell Interval / Scroll Offset. Add next to a
    /// FancyScrollView host and point at the Canvas <see cref="ResponsiveUI"/> hub.
    /// Works on its own: with no <see cref="Bind"/> callback it writes the sibling
    /// host's <c>cellInterval</c> / <c>scrollOffset</c> and Relayouts it. A host may
    /// still <see cref="Bind"/> a callback to own that write.
    /// </summary>
    [ExecuteAlways]
    public class ResponsiveScrollView : MonoBehaviour, IResponsiveUiClient
    {
        #region Serialized

        [Tooltip("ResponsiveUI on the parent Canvas. Auto-assigns when you add this component.")]
        [SerializeField] ResponsiveUI responsiveUi;
        [SerializeField] ResponsiveScrollLayout scrollMetrics;

        #endregion

        #region Runtime state

        ResponsiveUI _hub;
        Action _apply;
        readonly ResponsiveScrollDriver _driver = new ResponsiveScrollDriver();
        bool _hasHubLayout;
        ResponsiveDevice.LayoutKey _hubLayout;
#if UNITY_EDITOR
        [NonSerialized] bool _seedDefaultLayout;
#endif

        #endregion

        #region Access

        public ResponsiveUI Hub => ResponsiveUiHub.Resolve(responsiveUi, this);

        public ResponsiveScrollLayout ScrollMetrics => scrollMetrics;

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
            ResetUnboundHost();
            BindHub(true);
            if (_hub == null)
                _driver.Bind(this, PushHostThenApply);
            else
                ApplyNow();
        }

        void OnDisable()
        {
            BindHub(false);
            _hasHubLayout = false;
            _driver.Unbind();
        }

        void Start()
        {
            if (_hub == null)
                _driver.StartPlayMode();
        }

        void Update()
        {
            if (_hub == null)
                _driver.TickPlayMode();
        }

        void OnApplicationFocus(bool hasFocus)
        {
            if (_hub == null)
                _driver.OnPlayModeResume(hasFocus);
        }

        void OnApplicationPause(bool paused)
        {
            if (_hub == null)
                _driver.OnPlayModeResume(!paused);
        }

        void OnTransformParentChanged()
        {
            BindHub(false);
            _hasHubLayout = false;
            _driver.Unbind();
            if (!isActiveAndEnabled)
                return;
            BindHub(true);
            SyncDriverAfterHubBind();
            if (_hub == null)
                PushHostThenApply();
            else
                ApplyNow();
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (responsiveUi == null)
                responsiveUi = ResponsiveUiHub.Resolve(null, this);
            if (Hub == null)
                _driver.QueueEditorApply();
            if (!isActiveAndEnabled || ResponsiveUiHub.Resolve(responsiveUi, this) == _hub)
                return;
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this == null || !isActiveAndEnabled)
                    return;
                SyncHubBinding();
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

        #region Bind

        public void AssignHub(ResponsiveUI hub)
        {
            responsiveUi = hub;
            SyncHubBinding();
        }

        public void Bind(Action apply)
        {
            _apply = apply;
            if (isActiveAndEnabled)
                PushHostThenApply();
        }

        public bool Apply(ref float cellInterval, ref float scrollOffset)
        {
            ResponsiveDevice.LayoutKey layout = ResolveCurrentLayout();
            return scrollMetrics.Apply(layout, ref cellInterval, ref scrollOffset);
        }

        /// <summary>
        /// Applies the active orientation / form-factor slot only when its resolved metrics
        /// differ from the host values. A FancyScrollView host can use this to avoid redundant
        /// Relayout calls on hub notifications that do not change scroll geometry.
        /// </summary>
        public bool ApplyChanged(ref float cellInterval, ref float scrollOffset)
        {
            ResponsiveDevice.LayoutKey layout = ResolveCurrentLayout();
            return scrollMetrics.ApplyChanged(layout, ref cellInterval, ref scrollOffset);
        }

        public bool SeedIfEmpty(float cellInterval, float scrollOffset, bool portrait, bool landscape) =>
            scrollMetrics.SeedIfEmpty(cellInterval, scrollOffset, portrait, landscape);

        public void SeedAll(float cellInterval, float scrollOffset, bool portrait, bool landscape) =>
            scrollMetrics.SeedAll(cellInterval, scrollOffset, portrait, landscape);

        public void TakeLayout(ResponsiveScrollLayout layout, ResponsiveUI hub)
        {
            scrollMetrics = layout;
            AssignHub(hub);
        }

#if UNITY_EDITOR
        /// <summary>
        /// Editor scroll sync assigns these so hub / Simulator / Inspector Apply
        /// writes FancyScrollView <c>cellInterval</c> / <c>scrollOffset</c> through
        /// SerializedObject and does not recapture those writes into slots.
        /// </summary>
        public static Action EditorBeginApply;
        public static Action EditorEndApply;
        public static Action<ResponsiveScrollView> EditorPushHost;

        /// <summary>
        /// Unity object that owns the bound apply callback, when the callback is
        /// an instance method. Used only to include live host metrics in Undo.
        /// </summary>
        public UnityEngine.Object EditorApplyTarget =>
            _apply?.Target as UnityEngine.Object;

        /// <summary>
        /// Writes the resolved slot onto the sibling FancyScrollView host, then
        /// re-applies the bound callback (Play Mode Relayout).
        /// </summary>
        public void EditorLivePush() => PushHostThenApply();

        /// <summary>
        /// Re-applies the bound host callback only. Slot drawers write host
        /// SerializedObject themselves so Play Mode Relayout still runs.
        /// </summary>
        public void EditorInvokeApply() => InvokeApply();
#endif

        #endregion

        #region IResponsiveUiClient

        public void ApplyFromHub(
            ResponsiveUI hub,
            in ResponsiveDevice.LayoutKey layout,
            bool layoutOnly)
        {
            if (!isActiveAndEnabled || !hub.enabled)
                return;

            if (ResponsiveUiHub.RebindIfChanged(this, this, ref responsiveUi, ref _hub))
            {
                _hasHubLayout = false;
                SyncDriverAfterHubBind();
                if (_hub == null)
                {
                    PushHostThenApply();
                    return;
                }
            }

            if (_hub != null && hub != _hub)
            {
                ApplyNow();
                return;
            }

            // Every pass adopts the hub's layout (same as controllers / cameras), so a
            // layout-only pass can never keep resolving slots from a stale device state.
            bool layoutChanged = !_hasHubLayout || !_hubLayout.Equals(layout);
            _hubLayout = layout;
            _hasHubLayout = true;

            // Same form × orientation: metrics are unchanged on layout-only / canvas-ready
            // passes, but FancyScrollView still needs a host Relayout after safe-area
            // or Design Scale parents settle (RequestLayoutRefresh / Simulator canvas-ready).
            if (layoutOnly && !layoutChanged)
            {
                InvokeApply();
                return;
            }

            PushHostThenApply();
        }

        /// <summary>
        /// Play Mode same-form size ticks: Cell Interval / Scroll Offset are
        /// form × orientation only, but FancyScrollView still Relayouts when the
        /// viewport changes under Design Scale / safe-area parents. Simulator
        /// canvas-ready and <c>RequestLayoutRefresh</c> also notify this client
        /// via <c>notifyAllLayoutClients</c>.
        /// </summary>
        public bool SizeChangeRequiresReapply() => true;

#if UNITY_EDITOR
        public bool IsAuthoringSelectedTarget() => false;
#endif

        #endregion

        #region Hub

        void BindHub(bool bind) =>
            ResponsiveUiHub.BindClient(this, this, ref responsiveUi, ref _hub, bind);

        void ApplyNow() => ResponsiveUiHub.ApplyNow(this, layoutOnly: false);

        void SyncDriverAfterHubBind()
        {
            if (_hub == null)
                _driver.Bind(this, PushHostThenApply);
            else
                _driver.Unbind();
        }

        ResponsiveDevice.LayoutKey ResolveCurrentLayout()
        {
            if (isActiveAndEnabled &&
                ResponsiveUiHub.RebindIfChanged(this, this, ref responsiveUi, ref _hub))
            {
                _hasHubLayout = false;
                SyncDriverAfterHubBind();
            }

            return _hasHubLayout
                ? _hubLayout
                : _hub != null
                    ? _hub.CurrentLayout
                    : ResponsiveDevice.CurrentLayout;
        }

        void SyncHubBinding()
        {
            if (!isActiveAndEnabled)
                return;
            if (!ResponsiveUiHub.RebindIfChanged(this, this, ref responsiveUi, ref _hub))
                return;
            _hasHubLayout = false;
            SyncDriverAfterHubBind();
            if (_hub != null)
                ApplyNow();
            else
                PushHostThenApply();
        }

        void PushHostThenApply()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                EditorBeginApply?.Invoke();
                try
                {
                    EditorPushHost?.Invoke(this);
                }
                finally
                {
                    EditorEndApply?.Invoke();
                }
            }
#endif
            InvokeApply();
        }

        void InvokeApply()
        {
            if (!isActiveAndEnabled)
                return;

            // A throwing host must not abort the hub pass for every later client.
            try
            {
                if (_apply != null)
                    _apply.Invoke();
                else
                    ApplyUnboundHost();
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
            }
        }

        #endregion

        #region Unbound host

        const BindingFlags HostMemberFlags =
            BindingFlags.Instance | BindingFlags.Public |
            BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        /// <summary>Reflected FancyScrollView members, resolved once per host type.</summary>
        sealed class HostMembers
        {
            public FieldInfo Interval;
            public FieldInfo Offset;
            public FieldInfo Initialized;
            public FieldInfo Position;
            public MethodInfo Relayout;
            public MethodInfo UpdatePosition;
        }

        static readonly Dictionary<Type, HostMembers> HostMembersByType =
            new Dictionary<Type, HostMembers>();
        static readonly List<MonoBehaviour> HostSearchBuffer = new List<MonoBehaviour>(8);
        static readonly Type[] UpdatePositionSignature = { typeof(float) };

        MonoBehaviour _unboundHost;
        HostMembers _unboundMembers;
        Action _unboundRelayout;
        Action<float> _unboundUpdatePosition;
        bool _unboundSearched;

        /// <summary>
        /// No <see cref="Bind"/> callback: write the resolved slot onto the sibling
        /// FancyScrollView (serialized <c>cellInterval</c> / <c>scrollOffset</c>) and
        /// Relayout it. A host that never binds would otherwise resolve the right slot
        /// and silently apply nothing. Edit Mode is covered by <c>EditorPushHost</c>.
        /// </summary>
        void ApplyUnboundHost()
        {
            if (!Application.isPlaying || !ResolveUnboundHost())
                return;

            HostMembers members = _unboundMembers;
            float interval = (float)members.Interval.GetValue(_unboundHost);
            float offset = (float)members.Offset.GetValue(_unboundHost);
            if (ApplyChanged(ref interval, ref offset))
            {
                members.Interval.SetValue(_unboundHost, interval);
                members.Offset.SetValue(_unboundHost, offset);
            }

            // Relayout before the first UpdateContents would Initialize with no items.
            if (members.Initialized != null &&
                !(bool)members.Initialized.GetValue(_unboundHost))
                return;

            if (_unboundRelayout != null)
            {
                _unboundRelayout();
                return;
            }

            // Relayout can be stripped from a player build when no host calls it.
            // UpdatePosition(float) survives: hosts hand it to Scroller.OnValueChanged.
            if (_unboundUpdatePosition != null && members.Position != null)
                _unboundUpdatePosition((float)members.Position.GetValue(_unboundHost));
        }

        /// <summary>
        /// Finds the sibling host once per enable. Layout passes after a failed search
        /// stay free; <see cref="ResetUnboundHost"/> re-arms it.
        /// </summary>
        bool ResolveUnboundHost()
        {
            if (_unboundHost != null)
                return true;
            if (_unboundSearched)
                return false;

            _unboundSearched = true;
            _unboundMembers = null;
            _unboundRelayout = null;
            _unboundUpdatePosition = null;

            GetComponents(HostSearchBuffer);
            try
            {
                for (int i = 0; i < HostSearchBuffer.Count; i++)
                {
                    MonoBehaviour behaviour = HostSearchBuffer[i];
                    if (behaviour == null || behaviour == this)
                        continue;

                    HostMembers members = GetHostMembers(behaviour.GetType());
                    if (members == null)
                        continue;

                    _unboundHost = behaviour;
                    _unboundMembers = members;
                    // Closed delegates: no per-call reflection or argument boxing.
                    _unboundRelayout = CreateHostCall<Action>(behaviour, members.Relayout);
                    _unboundUpdatePosition =
                        CreateHostCall<Action<float>>(behaviour, members.UpdatePosition);
                    return true;
                }
            }
            finally
            {
                HostSearchBuffer.Clear();
            }

            return false;
        }

        void ResetUnboundHost()
        {
            _unboundHost = null;
            _unboundSearched = false;
        }

        static HostMembers GetHostMembers(Type hostType)
        {
            if (HostMembersByType.TryGetValue(hostType, out HostMembers cached))
                return cached;

            HostMembers members = null;
            for (Type type = hostType;
                 type != null && type != typeof(MonoBehaviour);
                 type = type.BaseType)
            {
                FieldInfo interval = FieldOfType(type, "cellInterval", typeof(float));
                FieldInfo offset = FieldOfType(type, "scrollOffset", typeof(float));
                if (interval == null || offset == null)
                    continue;

                members = new HostMembers
                {
                    Interval = interval,
                    Offset = offset,
                    Initialized = FieldOfType(type, "initialized", typeof(bool)),
                    Position = FieldOfType(type, "currentPosition", typeof(float)),
                    Relayout = type.GetMethod(
                        "Relayout", HostMemberFlags, null, Type.EmptyTypes, null),
                    UpdatePosition = type.GetMethod(
                        "UpdatePosition", HostMemberFlags, null, UpdatePositionSignature, null),
                };
                break;
            }

            // Cache misses too: non-host siblings (Image, Scroller) are asked every search.
            HostMembersByType[hostType] = members;
            return members;
        }

        static FieldInfo FieldOfType(Type type, string name, Type fieldType)
        {
            FieldInfo field = type.GetField(name, HostMemberFlags);
            return field != null && field.FieldType == fieldType ? field : null;
        }

        static T CreateHostCall<T>(MonoBehaviour host, MethodInfo method) where T : Delegate
        {
            if (method == null)
                return null;
            try
            {
                return (T)Delegate.CreateDelegate(typeof(T), host, method);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        #endregion
    }
}
