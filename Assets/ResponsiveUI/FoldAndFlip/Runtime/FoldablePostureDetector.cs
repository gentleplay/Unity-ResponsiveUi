using System;
using UnityEngine;

namespace FDPlus.Foldable
{
    /// <summary>
    /// Detection-only fold/flip posture owner (Closed / Opened / HalfOpened → <see cref="LayoutChanged"/>).
    /// Does not apply camera/canvas hinge splits — ResponsiveUI form slots consume posture via
    /// <see cref="FoldablePostureSource"/>.
    /// Android player: Jetpack FoldingFeature plus optional WindowArea rear ACTIVE for Closed.
    /// Other players: the registered <see cref="IFoldablePostureBackend"/> (iPhone Duo module:
    /// UIKit UIHingeInteraction).
    /// Editor: Device Simulator / Apply Simulated. Other players (and non-foldable iPhones) no-op.
    /// Host a persistent instance in your first scene (<see cref="persistAcrossScenes"/> / DontDestroyOnLoad).
    /// Prefer one detector as the native bridge owner in a build.
    /// See Docs/DETECTION.md.
    /// </summary>
    [ExecuteAlways]
    [DefaultExecutionOrder(-200)]
    public class FoldablePostureDetector : MonoBehaviour
    {
        #region Serialized

        [Header("Device")]
        [Tooltip(
            "Book-style Fold vs clamshell Flip. Labels product states and picks defaults. " +
            "Player Fold/Flip runs on Android (Jetpack) and iPhone Duo (UIHingeInteraction); " +
            "the Editor Device Simulator still authors every slot. " +
            "Other iPhones / players ignore this detector.")]
        [SerializeField] FoldableDeviceKind deviceKind = FoldableDeviceKind.Unknown;

        [Tooltip(
            "When true in Play Mode, this detector DontDestroyOnLoads from Login and survives " +
            "Main / gameplay loads. A later scene copy destroys itself if one already persists.")]
        [SerializeField] bool persistAcrossScenes = true;

        #endregion

        #region Runtime state

        readonly FoldableLayoutState layoutState = new FoldableLayoutState();
        readonly FoldableLayoutState lastPublishedState = new FoldableLayoutState();
        bool hasPublishedState;

#if UNITY_ANDROID && !UNITY_EDITOR
        const float NativeAttachRetrySeconds = 5f;
        static FoldablePostureDetector nativeOwner;
        FoldableProxy nativeProxy;
        // Holds cached / empty snapshots after resume until Jetpack confirms (bounded).
        readonly FoldableFreshLayoutGate nativeFreshGate = new FoldableFreshLayoutGate();
        bool nativeAttached;
        bool nativeListenerMayBeRegistered;
        float nextNativeAttachTime;
        int lastWatchedScreenW;
        int lastWatchedScreenH;
#endif

#if !UNITY_EDITOR && !UNITY_ANDROID
        static FoldablePostureDetector backendOwner;
#endif

        static FoldablePostureDetector s_ActiveInstance;
        static FoldablePostureDetector s_Persistent;
        static bool s_LookupComplete;
        bool isDuplicatePersist;

#if UNITY_EDITOR
        FoldableDeviceKind editorDeviceKind;
        bool hasEditorDeviceKind;
#endif

        /// <summary>
        /// Live read-only view for allocation-free layout queries. Mutations must go through
        /// detector Apply APIs; event subscribers receive isolated snapshots.
        /// </summary>
        public FoldableLayoutState LayoutState => layoutState;

        /// <summary>
        /// True in the Unity Editor (Device Simulator / Apply Simulated), on
        /// Android players (Jetpack <c>FoldingFeature</c> via <see cref="FoldableProxy"/>), and on
        /// other players whose registered <see cref="IFoldablePostureBackend"/> supports the device
        /// (iPhone Duo). Other iPhones / players return false and keep the hardware Phone / Pad path.
        /// </summary>
        public static bool IsRuntimeSupported =>
#if UNITY_EDITOR || UNITY_ANDROID
            true;
#else
            FoldablePostureBackend.Current != null &&
            FoldablePostureBackend.Current.IsSupportedDevice;
#endif

        /// <summary>Active enabled detector in the loaded scenes (null if none).</summary>
        public static FoldablePostureDetector FindActiveInstance()
        {
            if (!IsRuntimeSupported)
                return null;

            if (s_ActiveInstance != null && s_ActiveInstance.isActiveAndEnabled)
                return s_ActiveInstance;

            if (!ReferenceEquals(s_ActiveInstance, null))
            {
                s_ActiveInstance = null;
                s_LookupComplete = false;
            }

            if (s_Persistent != null && s_Persistent.isActiveAndEnabled)
            {
                s_Persistent.ActivateOwner();
                return s_ActiveInstance;
            }

            if (s_LookupComplete)
                return null;

            var found = UnityEngine.Object.FindObjectsByType<FoldablePostureDetector>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < found.Length; i++)
            {
                FoldablePostureDetector candidate = found[i];
                if (candidate != null &&
                    !candidate.isDuplicatePersist &&
                    candidate.isActiveAndEnabled)
                {
                    candidate.ActivateOwner();
                    return s_ActiveInstance;
                }
            }

            s_LookupComplete = true;
            return null;
        }

        public FoldableDeviceKind DeviceKind
        {
            get =>
#if UNITY_EDITOR
                hasEditorDeviceKind ? editorDeviceKind :
#endif
                deviceKind;
            set
            {
                deviceKind = value;
#if UNITY_EDITOR
                if (hasEditorDeviceKind)
                    return;
#endif
                layoutState.deviceKind = value;
            }
        }

        public event Action<FoldableLayoutState> LayoutChanged;

        /// <summary>How many times <paramref name="handler"/> is subscribed to <see cref="LayoutChanged"/>.</summary>
        internal int CountLayoutChangedHandlers(Action<FoldableLayoutState> handler)
        {
            Action<FoldableLayoutState> handlers = LayoutChanged;
            if (handlers == null || handler == null)
                return 0;

            int count = 0;
            Delegate[] list = handlers.GetInvocationList();
            for (int i = 0; i < list.Length; i++)
            {
                if (list[i].Equals(handler))
                    count++;
            }
            return count;
        }
        public static event Action ActiveInstanceChanged;
#if UNITY_EDITOR
        public static event Action<FoldablePostureDetector> EditorInitializing;

        public void SetEditorDeviceKind(FoldableDeviceKind kind)
        {
            editorDeviceKind = kind;
            hasEditorDeviceKind = true;
            layoutState.deviceKind = kind;
        }

        public void ClearEditorDeviceKind()
        {
            hasEditorDeviceKind = false;
            layoutState.deviceKind = deviceKind;
        }
#endif

        #endregion

        #region Apply (state only)

        /// <summary>
        /// Apply a simulated or device layout immediately (Editor / Simulator / tests).
        /// Raises <see cref="LayoutChanged"/>; does not write cameras / UI.
        /// </summary>
        public void ApplySimulatedLayout(FoldableLayoutState simulated)
        {
            if (!IsRuntimeSupported || simulated == null)
                return;

            FoldableProductState previousProduct = layoutState.productState;
            FoldableOrientation previousOrientation = layoutState.orientation;
            layoutState.CopyExact(simulated);
            CommitLayout(previousProduct, previousOrientation);
        }

        /// <summary>
        /// Copy simulated signals into <see cref="LayoutState"/> without raising
        /// <see cref="LayoutChanged"/>. Edit Mode Fold Apply uses this so
        /// <see cref="FDPlus.ResponsiveUI.ResponsiveDevice"/> mapping matches the panel.
        /// </summary>
        public void SyncSimulatedLayoutState(FoldableLayoutState simulated)
        {
            if (!IsRuntimeSupported || simulated == null)
                return;

            layoutState.CopyExact(simulated);
            FoldableProductStateResolver.Enrich(layoutState, DeviceKind);
            if (layoutState.deviceKind != FoldableDeviceKind.Unknown)
                AdoptLayoutDeviceKind(layoutState.deviceKind);
            layoutState.needsUpdate = false;
        }

        /// <summary>
        /// Restore Flat / Opened posture state (used when leaving a Fold/Flip simulator device).
        /// </summary>
        public void ApplyFlatLayout()
        {
            if (!IsRuntimeSupported)
                return;

            ApplySimulatedLayout(
                FoldableLayoutState.CreateFlat(Screen.width, Screen.height, DeviceKind));
        }

        /// <summary>
        /// Domain reload clears <see cref="layoutState"/>. Apply the Fold panel snapshot
        /// before other Detect/Apply paths map inner size as Expand/Pad.
        /// Script execution order <c>-200</c> runs enable before HUD clients.
        /// </summary>
        void ApplyPlayLayout()
        {
            if (!Application.isPlaying)
                return;
            if (!FoldableSimulationGate.GetPlayLayout(out FoldableLayoutState pending))
                return;

            if (pending.deviceKind != FoldableDeviceKind.Unknown)
                AdoptLayoutDeviceKind(pending.deviceKind);
            ApplySimulatedLayout(pending);
        }

        #endregion

        #region Lifecycle

        void Awake()
        {
            if (!Application.isPlaying || !persistAcrossScenes)
                return;

            if (s_Persistent != null && s_Persistent != this)
            {
                isDuplicatePersist = true;
                Destroy(gameObject);
                return;
            }

            s_Persistent = this;
            DontDestroyOnLoad(gameObject);
        }

        void OnDestroy()
        {
            if (s_Persistent == this)
                s_Persistent = null;
        }

        void OnEnable()
        {
            if (isDuplicatePersist || !IsRuntimeSupported)
                return;

            if (s_ActiveInstance != null &&
                s_ActiveInstance != this &&
                s_ActiveInstance.isActiveAndEnabled)
            {
                Debug.LogWarning(
                    "FoldAndFlip: another FoldablePostureDetector is already active. " +
                    "This detector will not become the layout owner.");
                return;
            }

            ActivateOwner();
        }

        void OnDisable()
        {
            if (isDuplicatePersist)
                return;

            bool wasActive = s_ActiveInstance == this;
#if UNITY_ANDROID && !UNITY_EDITOR
            DetachNativeIfOwner();
#endif
#if !UNITY_EDITOR && !UNITY_ANDROID
            DetachBackendIfOwner();
#endif
            if (!wasActive)
                return;

            s_ActiveInstance = null;
            if (!PromoteAnotherEnabledDetector())
            {
                s_LookupComplete = true;
                ActiveInstanceChanged?.Invoke();
            }
        }

        void OnApplicationPause(bool paused)
        {
#if !UNITY_EDITOR && !UNITY_ANDROID
            // Resuming re-enables delivery, so the device reports a fold made while paused.
            if (backendOwner == this)
                FoldablePostureBackend.Current.SetPaused(paused);
#endif
#if UNITY_ANDROID && !UNITY_EDITOR
            if (nativeOwner != this)
                return;

            if (paused)
                StopNativeTracking();
            else
                AttachNative();
#endif
        }

        void OnApplicationFocus(bool hasFocus)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!hasFocus || nativeOwner != this)
                return;
            AttachNative();
#endif
#if !UNITY_EDITOR && !UNITY_ANDROID
            // Folding can move the app between displays while unfocused.
            if (hasFocus && backendOwner == this)
                PollBackend(force: true);
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        void Update()
        {
            if (nativeOwner != this)
                return;

            if (!nativeAttached && Time.unscaledTime >= nextNativeAttachTime)
                AttachNative();

            // Unity Screen can update on Expand↔Normal / Flex before Jetpack republishes.
            // Refresh the bridge so cover/HalfOpened heuristics see current window metrics.
            if (nativeAttached)
                RefreshNativeIfScreenChanged();

            // After resume Java publishes cached / empty folding data flagged awaitingFresh: keep
            // the previous product until Jetpack confirms or the gate times out (no flicker).
            float now = Time.unscaledTime;
            FoldableLayoutState release;
            bool merge;
            if (nativeProxy != null &&
                nativeProxy.ConsumeLatest(out FoldableLayoutState nativeSnapshot, out bool awaitingFresh))
            {
                merge = nativeFreshGate.Offer(nativeSnapshot, awaitingFresh, now, out release);
            }
            else
            {
                merge = nativeFreshGate.Poll(now, out release);
            }
            if (merge)
                MergeNative(release);
        }

        void MergeNative(FoldableLayoutState nativeSnapshot)
        {
            if (nativeSnapshot == null)
                return;

            FoldableProductState previousProduct = layoutState.productState;
            FoldableOrientation previousOrientation = layoutState.orientation;
            layoutState.MergeNativeSnapshot(nativeSnapshot);
            if (layoutState.deviceKind == FoldableDeviceKind.Unknown)
                layoutState.deviceKind = DeviceKind;
            CommitLayout(previousProduct, previousOrientation, forceNotify: false);
        }

        void RefreshNativeIfScreenChanged()
        {
            int w = Screen.width;
            int h = Screen.height;
            if (w <= 0 || h <= 0)
                return;
            if (w == lastWatchedScreenW && h == lastWatchedScreenH)
                return;

            lastWatchedScreenW = w;
            lastWatchedScreenH = h;
            try
            {
                using (var helper = new AndroidJavaClass(
                           "com.futuredigitalplus.foldandflip.FoldableHelper"))
                    helper.CallStatic("refresh");
            }
            catch (Exception ex)
            {
                Debug.LogWarning("FoldAndFlip: Android bridge refresh failed: " + ex.Message);
            }
        }
#endif

        void BecomeActiveOwner()
        {
            s_ActiveInstance = this;
            s_LookupComplete = true;
#if UNITY_ANDROID && !UNITY_EDITOR
            AttachNative();
#endif
#if !UNITY_EDITOR && !UNITY_ANDROID
            AttachBackend();
#endif
        }

        void ActivateOwner()
        {
            BecomeActiveOwner();
#if UNITY_EDITOR
            EditorInitializing?.Invoke(this);
#endif
            ApplyPlayLayout();
            ActiveInstanceChanged?.Invoke();
        }

        bool PromoteAnotherEnabledDetector()
        {
            var found = UnityEngine.Object.FindObjectsByType<FoldablePostureDetector>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < found.Length; i++)
            {
                FoldablePostureDetector other = found[i];
                if (other != null &&
                    other != this &&
                    !other.isDuplicatePersist &&
                    other.isActiveAndEnabled)
                {
                    other.ActivateOwner();
                    return true;
                }
            }

            return false;
        }

        #endregion

        #region Android bridge

#if UNITY_ANDROID && !UNITY_EDITOR
        void AttachNative()
        {
            if (nativeOwner != null && nativeOwner != this)
                return;

            nativeOwner = this;
            nextNativeAttachTime = Time.unscaledTime + NativeAttachRetrySeconds;
            try
            {
                if (nativeProxy == null)
                    nativeProxy = new FoldableProxy();

                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                {
                    AndroidJavaObject activity =
                        unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                    if (activity == null)
                    {
                        Debug.LogWarning(
                            "FoldAndFlip: Unity currentActivity is null; " +
                            "will retry the Android bridge automatically and on resume.");
                        return;
                    }

                    using (activity)
                    using (var helper = new AndroidJavaClass(
                               "com.futuredigitalplus.foldandflip.FoldableHelper"))
                    {
                        if (!nativeAttached)
                        {
                            nativeListenerMayBeRegistered = true;
                            nativeAttached = helper.CallStatic<bool>(
                                "attachNativeListener", nativeProxy, activity);
                            nativeListenerMayBeRegistered = nativeAttached;
                        }
                        if (nativeAttached)
                            helper.CallStatic("start", activity);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("FoldAndFlip: Android bridge attach failed: " + ex.Message);
            }
        }

        void StopNativeTracking()
        {
            nativeFreshGate.NotifyTrackingStopped();
            if (!nativeAttached)
                return;

            try
            {
                using (var helper = new AndroidJavaClass(
                           "com.futuredigitalplus.foldandflip.FoldableHelper"))
                    helper.CallStatic("stop");
            }
            catch (Exception ex)
            {
                Debug.LogWarning("FoldAndFlip: Android bridge stop failed: " + ex.Message);
            }
        }

        void DetachNativeIfOwner()
        {
            if (nativeOwner != this)
                return;

            try
            {
                if (nativeAttached || nativeListenerMayBeRegistered)
                {
                    // detachNativeListener also stops tracking.
                    using (var helper = new AndroidJavaClass(
                               "com.futuredigitalplus.foldandflip.FoldableHelper"))
                        helper.CallStatic("detachNativeListener", nativeProxy);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("FoldAndFlip: Android bridge detach failed: " + ex.Message);
            }
            finally
            {
                nativeAttached = false;
                nativeListenerMayBeRegistered = false;
                nativeProxy = null;
                nativeFreshGate.Reset();
                nativeOwner = null;
            }
        }
#endif

        #endregion

        #region Platform backend

#if !UNITY_EDITOR && !UNITY_ANDROID
        void Update()
        {
            if (backendOwner != this)
                return;
            PollBackend(force: false);
            FoldablePostureBackend.Current.Tick(layoutState);
        }

        void AttachBackend()
        {
            IFoldablePostureBackend backend = FoldablePostureBackend.Current;
            if (backend == null || (backendOwner != null && backendOwner != this))
                return;

            backendOwner = this;
            backend.Attach();
            PollBackend(force: true);
        }

        /// <summary>
        /// Rebuilds the layout when the backend reports a change (hinge status, or a screen resize
        /// such as the cover ↔ inner display switch, which happens without backgrounding).
        /// </summary>
        void PollBackend(bool force)
        {
            FoldableProductState previousProduct = layoutState.productState;
            FoldableOrientation previousOrientation = layoutState.orientation;
            if (!FoldablePostureBackend.Current.Poll(force, layoutState))
                return;
            CommitLayout(previousProduct, previousOrientation, forceNotify: false);
        }

        void DetachBackendIfOwner()
        {
            if (backendOwner != this)
                return;

            FoldablePostureBackend.Current.Detach();
            backendOwner = null;
        }
#endif

        #endregion

        #region Commit layout

        void CommitLayout(
            FoldableProductState previousProduct,
            FoldableOrientation previousOrientation = FoldableOrientation.Undefined,
            bool forceNotify = true)
        {
            if (!IsRuntimeSupported)
            {
                layoutState.needsUpdate = false;
                return;
            }

            FoldableProductStateResolver.Enrich(
                layoutState, DeviceKind, previousProduct, previousOrientation);
            if (layoutState.deviceKind != FoldableDeviceKind.Unknown)
                AdoptLayoutDeviceKind(layoutState.deviceKind);

            layoutState.needsUpdate = false;
            if (!forceNotify &&
                hasPublishedState &&
                layoutState.ContentEquals(lastPublishedState))
            {
                return;
            }

#if DEVELOPMENT_BUILD && !UNITY_EDITOR
            // Development players: one line per posture change (not per metrics-only update),
            // for hardware QA.
            if (!hasPublishedState ||
                lastPublishedState.productState != layoutState.productState ||
                lastPublishedState.orientation != layoutState.orientation)
            {
                Debug.Log(
                    $"FoldAndFlip: posture {layoutState.productState} ({layoutState.deviceKind}, " +
                    $"{layoutState.orientation} hinge) screen {Screen.width}x{Screen.height}");
            }
#endif
            lastPublishedState.CopyExact(layoutState);
            hasPublishedState = true;
            PublishLayoutChanged();
        }

        void AdoptLayoutDeviceKind(FoldableDeviceKind kind)
        {
#if UNITY_EDITOR
            SetEditorDeviceKind(kind);
#else
            deviceKind = kind;
#endif
        }

        void PublishLayoutChanged() =>
            FoldablePostureSource.RaiseIsolated(LayoutChanged, layoutState);

        #endregion
    }
}
