using System;

namespace FDPlus.Foldable
{
    /// <summary>
    /// Single lookup and event fan-out for ResponsiveUI and Fold tools over
    /// <see cref="FoldablePostureDetector"/> (detection-only posture owner).
    /// Host the detector in a scene that stays loaded; Create Fold &amp; Flip Setup adds one
    /// (optional <see cref="FoldableSimulator"/> overlay in the Editor).
    /// </summary>
    public static class FoldablePostureSource
    {
        static event Action<FoldableLayoutState> s_LayoutChanged;
        static event Action s_ActiveInstanceChanged;
        static FoldablePostureDetector s_BoundDetector;
        static bool s_OwnerHooks;

        public static bool IsRuntimeSupported =>
            FoldablePostureDetector.IsRuntimeSupported;

        /// <summary>
        /// Raised when the active detector's <c>LayoutChanged</c> fires.
        /// </summary>
        public static event Action<FoldableLayoutState> LayoutChanged
        {
            add
            {
                bool first = s_LayoutChanged == null;
                s_LayoutChanged += value;
                UpdateOwnerHook();
                if (first)
                    RebindLayoutForward();
            }
            remove
            {
                s_LayoutChanged -= value;
                if (s_LayoutChanged == null)
                    ClearLayoutForward();
                UpdateOwnerHook();
            }
        }

        /// <summary>
        /// Raised when detector active ownership changes.
        /// </summary>
        public static event Action ActiveInstanceChanged
        {
            add
            {
                s_ActiveInstanceChanged += value;
                UpdateOwnerHook();
            }
            remove
            {
                s_ActiveInstanceChanged -= value;
                UpdateOwnerHook();
            }
        }

        /// <summary>
        /// Active detector layout, or null when no detector is enabled.
        /// </summary>
        public static FoldableLayoutState FindActiveLayoutState()
        {
            FoldablePostureDetector detector = FoldablePostureDetector.FindActiveInstance();
            return detector != null ? detector.LayoutState : null;
        }

        /// <summary>True when an enabled detector owns posture in the loaded scenes.</summary>
        public static bool HasActiveOwner() => FoldablePostureDetector.FindActiveInstance() != null;

#if UNITY_EDITOR
        public static bool SetEditorDeviceKind(FoldableDeviceKind kind)
        {
            FoldablePostureDetector detector = FoldablePostureDetector.FindActiveInstance();
            if (detector == null)
                return false;

            detector.SetEditorDeviceKind(kind);
            return true;
        }
#endif

        /// <summary>
        /// Push simulated posture (Play Mode / Simulator). Raises <c>LayoutChanged</c>.
        /// </summary>
        public static bool ApplySimulatedLayout(FoldableLayoutState layout)
        {
            if (layout == null)
                return false;

            FoldablePostureDetector detector = FoldablePostureDetector.FindActiveInstance();
            if (detector == null)
                return false;

            detector.ApplySimulatedLayout(layout);
            return true;
        }

        /// <summary>
        /// Edit Mode / silent sync of layout state without raising <c>LayoutChanged</c>.
        /// </summary>
        public static bool SyncSimulatedLayoutState(FoldableLayoutState layout)
        {
            if (layout == null)
                return false;

            FoldablePostureDetector detector = FoldablePostureDetector.FindActiveInstance();
            if (detector == null)
                return false;

            detector.SyncSimulatedLayoutState(layout);
            return true;
        }

        public static bool ApplyFlatLayout()
        {
            FoldablePostureDetector detector = FoldablePostureDetector.FindActiveInstance();
            if (detector == null)
                return false;

            detector.ApplyFlatLayout();
            return true;
        }

        static void UpdateOwnerHook()
        {
            bool shouldHook = s_LayoutChanged != null || s_ActiveInstanceChanged != null;
            if (shouldHook == s_OwnerHooks)
                return;

            s_OwnerHooks = shouldHook;
            if (shouldHook)
            {
                FoldablePostureDetector.ActiveInstanceChanged += OnOwnerLifecycle;
            }
            else
            {
                FoldablePostureDetector.ActiveInstanceChanged -= OnOwnerLifecycle;
                ClearLayoutForward();
            }
        }

        static void OnOwnerLifecycle()
        {
            if (s_LayoutChanged != null)
                RebindLayoutForward();
            s_ActiveInstanceChanged?.Invoke();
        }

        static void RebindLayoutForward()
        {
            // FindActiveInstance may promote an owner, which raises ActiveInstanceChanged and
            // re-enters this method through OnOwnerLifecycle. Resolve first, then clear whatever
            // the nested call bound, then attach exactly once (-= before += is idempotent).
            FoldablePostureDetector detector = FoldablePostureDetector.FindActiveInstance();
            ClearLayoutForward();
            if (s_LayoutChanged == null || detector == null)
                return;

            s_BoundDetector = detector;
            detector.LayoutChanged -= ForwardLayout;
            detector.LayoutChanged += ForwardLayout;
        }

        static void ClearLayoutForward()
        {
            // ReferenceEquals: still unsubscribe / release a destroyed (Unity-null) detector.
            if (!object.ReferenceEquals(s_BoundDetector, null))
                s_BoundDetector.LayoutChanged -= ForwardLayout;
            s_BoundDetector = null;
        }

        /// <summary>
        /// Number of forwarding handlers this source has attached to <paramref name="detector"/>
        /// (0 or 1). Diagnostics / tests.
        /// </summary>
        internal static int CountForwardingSubscriptions(FoldablePostureDetector detector) =>
            detector != null ? detector.CountLayoutChangedHandlers(ForwardLayout) : 0;

        static void ForwardLayout(FoldableLayoutState layout) =>
            RaiseIsolated(s_LayoutChanged, layout);

        /// <summary>
        /// Invokes each subscriber with its own snapshot of <paramref name="layout"/>. Isolated:
        /// one throwing listener must not stop the others from receiving the posture change
        /// (they would stay on the old form).
        /// </summary>
        internal static void RaiseIsolated(
            Action<FoldableLayoutState> handlers,
            FoldableLayoutState layout)
        {
            if (handlers == null)
                return;

            Delegate[] subscribers = handlers.GetInvocationList();
            for (int i = 0; i < subscribers.Length; i++)
            {
                try
                {
                    ((Action<FoldableLayoutState>)subscribers[i])
                        .Invoke(layout.CreateSnapshot());
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogException(e, subscribers[i].Target as UnityEngine.Object);
                }
            }
        }
    }
}
