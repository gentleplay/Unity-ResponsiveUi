using UnityEditor;
using UnityEngine;

namespace FDPlus.Foldable.Editor
{
    /// <summary>
    /// Keeps FoldableEditorSession device binding aligned with the active Device Simulator.
    /// Selection changes arrive through DeviceSimulator.deviceChanged, chrome rotations through
    /// the Simulator screen and device view events (<see cref="SimulatorDeviceBridge"/>).
    /// </summary>
    [InitializeOnLoad]
    static class FoldableEditorSessionWatcher
    {
        #region Runtime state

        static string lastModel = "";
        static string lastFriendly = "";
        static int lastScreenIndex = -1;
        static int lastRotationDegrees;
        static bool lastHadRotation;
        static bool hadValidSelection;
        static bool suppressDefaultsOnce;
        static bool unbindQueued;
        static int invalidSelectionChecksRemaining;

        #endregion

        #region Lifecycle

        static FoldableEditorSessionWatcher()
        {
            SimulatorDeviceBridge.SelectionChanged += OnSimulatorSelectionChanged;
            FoldableEditorSession.Changed += OnSessionChanged;
            FoldableEditorSession.ScreenSynced += SyncDetectorLayout;
            FoldablePostureDetector.EditorInitializing += InitializeDetector;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

            // Play Mode domain reload already has Simulator plugins / SlotFocus —
            // skipping avoids reflection rebind on every Play toggle.
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
                EditorApplication.delayCall += () => RefreshNow();
        }

        static void OnBeforeAssemblyReload()
        {
            unbindQueued = false;
            invalidSelectionChecksRemaining = 0;
        }

        static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            // Only re-bind after returning to Edit Mode. EnteredPlayMode is already
            // covered by Simulator plugins / SlotFocus — avoid a second RefreshNow.
            if (state != PlayModeStateChange.EnteredEditMode)
                return;

            unbindQueued = false;
            invalidSelectionChecksRemaining = 0;
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isCompiling ||
                    EditorApplication.isUpdating ||
                    EditorApplication.isPlayingOrWillChangePlaymode)
                    return;
                RefreshNow();
            };
        }

        #endregion

        #region Session

        public static void SuppressNextDeviceDefaults() => suppressDefaultsOnce = true;

        /// <summary>
        /// Re-reads the Simulator selection. <paramref name="forceRebind"/> rebinds even when the
        /// selection identity is unchanged (catalog edits) and drops a pending defaults suppression.
        /// </summary>
        public static void RefreshNow(bool forceRebind = false)
        {
            if (forceRebind)
                suppressDefaultsOnce = false;
            SimulatorDeviceBridge.EnsureSelectionEventBindings();
            RefreshSelection(forceRebind);
        }

        static void OnSimulatorSelectionChanged() => RefreshSelection(forceRebind: false);

        static void OnSessionChanged(FoldableEditorSession.Source source)
        {
            // Simulated layouts follow the binding whichever panel or window published it.
            FoldableSimulationGate.AllowSimulatedLayouts = FoldableEditorSession.IsFoldableDeviceBound;

            if (source == FoldableEditorSession.Source.Bridge)
                SyncDetectorDeviceKind();
        }

        static void RefreshSelection(bool forceRebind)
        {
            if (!SimulatorDeviceBridge.GetActiveSelection(out var selection) || !selection.IsValid)
            {
                // Cover/Inner and rotation destroy the Simulator HWND for a tick.
                // Debounce before restoring Flat; a permanently closed Simulator
                // must still release the previous Fold/Flip binding.
                QueueUnbind();
                return;
            }

            unbindQueued = false;
            invalidSelectionChecksRemaining = 0;

            string friendly = selection.FriendlyName ?? "";
            string model = selection.DeviceModel ?? "";
            int screenIndex = selection.ScreenIndex;
            int rotationDegrees = selection.RotationDegrees;

            bool identityChanged = !hadValidSelection ||
                                   model != lastModel ||
                                   friendly != lastFriendly ||
                                   screenIndex != lastScreenIndex ||
                                   selection.HasRotation != lastHadRotation ||
                                   (selection.HasRotation && rotationDegrees != lastRotationDegrees);

            // Ignore ScreenWidth/Height flicker entirely — catalog owns metrics.
            if (!identityChanged && !forceRebind)
                return;

            lastModel = model;
            lastFriendly = friendly;
            lastScreenIndex = screenIndex;
            lastRotationDegrees = rotationDegrees;
            lastHadRotation = selection.HasRotation;
            hadValidSelection = true;

            FoldableDeviceCatalog.Resolution bound = ResolveSelection(selection);
            if (!bound.IsKnownFoldable)
            {
                // Play Mode / skin switch can report a valid phone for one tick.
                // Immediate unbind resets the panel to Expand, then Fold rebind
                // looks like Half Opened → Expand → Half Opened.
                if (FoldableEditorSession.IsFoldableDeviceBound)
                {
                    QueueUnbind();
                    return;
                }

                // Ghost Dual/Generic skins must not UnbindFoldable — that clears
                // hadValidSelection and re-enters on every Simulator tick.
                return;
            }

            FoldableSimulationGate.AllowSimulatedLayouts = true;

            bool firstBind = !FoldableEditorSession.IsFoldableDeviceBound;
            bool profileChanged = FoldableEditorSession.IsFoldableDeviceBound &&
                                  (!FoldableDeviceCatalog.NamesMatch(
                                       FoldableEditorSession.BoundFriendlyName,
                                       bound.Device.FriendlyName) ||
                                   FoldableEditorSession.BoundDeviceModel !=
                                   (bound.Device?.DeviceModel ?? ""));

            // Catalog Expand / Cover defaults only on first Fold/Flip bind.
            // Switching another ★ Fold / ★ Flip profile of the same kind keeps the
            // Fold panel product state (Half Opened stays Half Opened). Phone or Fold
            // → Flip starts Opened (Main), not Cover/Closed. Play Mode domain-reloads
            // statics; the session restores the last product first, so this is not a
            // first bind to Expand.
            bool applyDefaults = firstBind && !suppressDefaultsOnce;
            if (suppressDefaultsOnce)
                suppressDefaultsOnce = false;

            // In-flight before Changed so widget Apply waits for Unity Screen (Cover→Main).
            if (firstBind || profileChanged)
                FoldableSimulatorScreenCommit.QueueDeferred();
            FoldableEditorSession.SetDeviceBinding(bound, FoldableEditorSession.Source.Bridge, applyDefaults);
            // Profile switches keep the pose: the new device's own panel rotates the chrome.
            if (bound.Device.HasLandscapeNativeInner)
                ApplyIosChromeRotation(selection.HasRotation, rotationDegrees, profileChanged);
            else
                ApplyAndroidChromeRotation(selection.HasRotation, rotationDegrees, profileChanged);
        }

        /// <summary>
        /// Android Fold / Fold Cover / Flip. A turn Player Settings do not allow is ignored,
        /// so the orientation lock does not write the simulator rotation back.
        /// </summary>
        internal static void ApplyAndroidChromeRotation(bool hasRotation, int rotationDegrees, bool profileChanged)
        {
            bool? poseLandscape = PoseFromChromeRotation(hasRotation, rotationDegrees, profileChanged);
            if (!poseLandscape.HasValue)
                return;

            // Android half-open screen held by its card (Player lock, or the pose it entered
            // Half Opened with). A simulator rotate must not publish the other size. iPhone Duo
            // does not use this path.
            bool? halfOpenScreen = FoldableEditorSession.AndroidHalfOpenHeldScreenLandscape(
                FoldableEditorSession.DeviceKind,
                FoldableEditorSession.ProductState,
                landscapeNative: false);
            if (halfOpenScreen.HasValue)
            {
                if (poseLandscape.Value != halfOpenScreen.Value)
                    return;
                FoldableEditorSession.PublishAndroidScreen(
                    poseLandscape.Value,
                    FoldableEditorSession.Source.Bridge);
                return;
            }

            // A changed Follow device card turns with the device even under a one-axis lock
            // (the runtime hold allows both axes for that pose), so both shapes are reachable.
            if (FoldableEditorSession.AndroidHalfOpenFollowsDevice(
                    FoldableEditorSession.ProductState, landscapeNative: false))
            {
                FoldableEditorSession.PublishAndroidScreen(
                    poseLandscape.Value,
                    FoldableEditorSession.Source.Bridge);
                return;
            }

            poseLandscape = PoseAfterAndroidLock(
                poseLandscape,
                innerLandscapeNative: false,
                FoldablePlayerOrientation.GetLockedLandscape());
            if (!poseLandscape.HasValue)
                return;

            FoldableEditorSession.PublishAndroidScreen(
                poseLandscape.Value,
                FoldableEditorSession.Source.Bridge);
        }

        /// <summary>
        /// iPhone Duo. The chrome turn is the screen, including a portrait laptop, except while a
        /// locked Seated or a locked Flat holds its screen (the iPhone Duo panel turns the chrome back).
        /// </summary>
        static void ApplyIosChromeRotation(bool hasRotation, int rotationDegrees, bool profileChanged)
        {
            bool? poseLandscape = PoseAfterIosHalfOpenLock(
                PoseFromChromeRotation(hasRotation, rotationDegrees, profileChanged),
                FoldableEditorSession.IosHalfOpenHeldScreenLandscape(
                    FoldableEditorSession.ProductState,
                    FoldableEditorSession.IosHalfOpenRotationLocked?.Invoke() == true,
                    FoldableEditorSession.IsLandscape) ??
                FoldableEditorSession.IosFlatHeldScreenLandscape(
                    FoldableEditorSession.ProductState,
                    FoldableEditorSession.IosFlatRotationLocked?.Invoke() == true,
                    FoldableEditorSession.IsLandscape));
            if (!poseLandscape.HasValue)
                return;

            FoldableEditorSession.PublishIosScreen(
                poseLandscape.Value,
                FoldableEditorSession.Source.Bridge);
        }

        /// <summary>
        /// Pose behind the Simulator chrome rotation, or null when the chrome must not drive it.
        /// After a profile switch the chrome still shows the previous profile's rotation (a Fold
        /// laptop is landscape chrome, a Duo laptop is portrait chrome), so the session keeps its
        /// pose and the queued screen commit rotates the chrome to match.
        /// </summary>
        internal static bool? PoseFromChromeRotation(
            bool hasRotation,
            int rotationDegrees,
            bool profileChanged)
        {
            if (!hasRotation || profileChanged)
                return null;

            return SimulatorDeviceBridge.IsLandscapeRotation(rotationDegrees);
        }

        /// <summary>
        /// Drops an iPhone Duo chrome turn away from the screen a locked Seated holds
        /// (<paramref name="heldLandscape"/>, see <see cref="FoldableEditorSession.IosHalfOpenHeldScreenLandscape"/>);
        /// nothing held keeps the turn.
        /// </summary>
        internal static bool? PoseAfterIosHalfOpenLock(bool? poseLandscape, bool? heldLandscape)
        {
            if (!poseLandscape.HasValue || !heldLandscape.HasValue)
                return poseLandscape;
            return poseLandscape.Value == heldLandscape.Value ? poseLandscape : null;
        }

        /// <summary>
        /// Drops an Android chrome turn the Player lock rejects. Publishing that turn makes
        /// the lock set the simulator rotation back, so the rotate control never sticks.
        /// iPhone Duo (<paramref name="innerLandscapeNative"/>) keeps the turn.
        /// </summary>
        internal static bool? PoseAfterAndroidLock(
            bool? poseLandscape,
            bool innerLandscapeNative,
            bool? lockedLandscape)
        {
            if (!poseLandscape.HasValue || innerLandscapeNative)
                return poseLandscape;
            if (lockedLandscape.HasValue && lockedLandscape.Value != poseLandscape.Value)
                return null;
            return poseLandscape;
        }

        static void SyncDetectorDeviceKind()
        {
            if (!FoldableEditorSession.IsFoldableDeviceBound)
                return;

            FoldablePostureSource.SetEditorDeviceKind(FoldableEditorSession.DeviceKind);
        }

        static void SyncDetectorLayout()
        {
            if (!FoldableEditorSession.IsFoldableDeviceBound)
                return;

            FoldablePostureSource.SyncSimulatedLayoutState(
                FoldableEditorSession.BuildLayout());
        }

        static void InitializeDetector(FoldablePostureDetector detector)
        {
            if (!FoldableEditorSession.IsFoldableDeviceBound)
                return;

            detector.SetEditorDeviceKind(FoldableEditorSession.DeviceKind);
            detector.SyncSimulatedLayoutState(FoldableEditorSession.BuildLayout());
        }

        static void QueueUnbind()
        {
            if (unbindQueued)
                return;
            if (!hadValidSelection && !FoldableEditorSession.IsFoldableDeviceBound)
                return;

            unbindQueued = true;
            invalidSelectionChecksRemaining = 3;
            EditorApplication.delayCall += FlushQueuedUnbind;
        }

        static void FlushQueuedUnbind()
        {
            if (!unbindQueued)
                return;

            // Do not spin delayCall while the editor is compiling / domain-reloading.
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                unbindQueued = false;
                invalidSelectionChecksRemaining = 0;
                return;
            }

            if (!SimulatorDeviceBridge.GetActiveSelection(out var selection) || !selection.IsValid)
            {
                // InFlight used to reschedule forever; budget it with the same counter.
                if (FoldableSimulatorScreenCommit.InFlight &&
                    invalidSelectionChecksRemaining > 0)
                {
                    invalidSelectionChecksRemaining--;
                    EditorApplication.delayCall += FlushQueuedUnbind;
                    return;
                }

                invalidSelectionChecksRemaining--;
                if (invalidSelectionChecksRemaining > 0)
                {
                    EditorApplication.delayCall += FlushQueuedUnbind;
                    return;
                }

                unbindQueued = false;
                UnbindFoldable();
                return;
            }

            unbindQueued = false;
            invalidSelectionChecksRemaining = 0;
            if (ResolveSelection(selection).IsKnownFoldable)
            {
                RefreshSelection(forceRebind: false);
                return;
            }

            UnbindFoldable();
        }

        /// <summary>
        /// Catalog profile behind the Simulator selection. The Editor binds Android Fold / Flip
        /// and iPhone Duo profiles on any build target. Players still use only their platform.
        /// </summary>
        static FoldableDeviceCatalog.Resolution ResolveSelection(
            SimulatorDeviceBridge.Selection selection)
        {
            if (!FoldableDeviceCatalog.IsToolkitListName(selection.FriendlyName))
                return new FoldableDeviceCatalog.Resolution { IsKnownFoldable = false };

            return FoldableDeviceCatalog.ResolveByFriendlyName(
                selection.FriendlyName,
                selection.DeviceModel,
                selection.ScreenWidth,
                selection.ScreenHeight,
                selection.ScreenIndex);
        }

        /// <summary>
        /// Releases the Fold / Flip binding: product defaults reset, scenes back to Flat, and
        /// Simulator listeners notified.
        /// </summary>
        static void UnbindFoldable()
        {
            bool shouldRestore =
                FoldableEditorSession.IsFoldableDeviceBound ||
                FoldableSimulationGate.AllowSimulatedLayouts;

            hadValidSelection = false;
            lastModel = "";
            lastFriendly = "";
            lastScreenIndex = -1;
            lastRotationDegrees = 0;
            lastHadRotation = false;
            FoldableSimulationGate.AllowSimulatedLayouts = false;
            FoldableEditorSession.ClearDeviceBinding(FoldableEditorSession.Source.Bridge);
            FoldableEditorSession.ResetProductDefaults();
            if (shouldRestore)
                FoldableFlatRestore.RestoreAllInLoadedScenes();
            suppressDefaultsOnce = false;
            SimulatorDeviceBridge.NotifySelectionChanged();
        }

        #endregion
    }
}
