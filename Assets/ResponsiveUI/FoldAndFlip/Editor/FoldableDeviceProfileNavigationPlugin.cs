using System.Collections.Generic;
using UnityEditor;
using UnityEditor.DeviceSimulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace FDPlus.Foldable.Editor
{
    /// <summary>
    /// Device Simulator panel for stepping / auto-cycling displayed device profiles.
    /// Separate from Fold &amp; Flip posture controls so navigation works for every device.
    /// </summary>
    public sealed class FoldableDeviceProfileNavigationPlugin : DeviceSimulatorPlugin
    {
        #region Constants

        const string IntervalSessionKey = "FDPlus.DeviceProfileNav.IntervalSec";
        const float DefaultIntervalSec = 2f;
        const float MinIntervalSec = 0.5f;
        const float MaxIntervalSec = 60f;

        enum AutoMode : byte
        {
            Off = 0,
            Next = 1,
            Previous = 2,
        }

        #endregion

        #region UI

        Label currentProfileLabel;
        Label autoStatusLabel;
        Button autoPreviousButton;
        Button autoNextButton;
        Button pauseButton;
        FloatField intervalField;

        #endregion

        #region State

        int currentProfileIndex = -1;
        string pendingProfileName = "";
        string previousProfileName = "";
        string lastProfileLabelText = "";
        string lastAutoStatusText = "";
        readonly List<string> displayedProfileNames = new List<string>();
        bool profileNamesDirty = true;

        AutoMode autoMode = AutoMode.Off;
        bool autoPaused;
        float intervalSec = DefaultIntervalSec;
        double nextAdvanceAt;
        bool updateHooked;
        bool destroyed;

        #endregion

        public override string title => "Device Profiles";

        public override void OnCreate()
        {
            destroyed = false;
            intervalSec = Mathf.Clamp(
                SessionState.GetFloat(IntervalSessionKey, DefaultIntervalSec),
                MinIntervalSec,
                MaxIntervalSec);
            SimulatorDeviceBridge.SelectionChanged += OnSelectionChanged;
            FoldableEditorSession.Changed += OnSessionChanged;
            FoldableDeviceCatalog.CatalogChanged += OnCatalogChanged;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        public override void OnDestroy()
        {
            destroyed = true;
            StopAuto();
            SimulatorDeviceBridge.SelectionChanged -= OnSelectionChanged;
            FoldableEditorSession.Changed -= OnSessionChanged;
            FoldableDeviceCatalog.CatalogChanged -= OnCatalogChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        }

        public override VisualElement OnCreateUI()
        {
            var root = new VisualElement();
            root.style.paddingLeft = 4;
            root.style.paddingRight = 4;
            root.style.paddingTop = 4;
            root.style.paddingBottom = 8;

            currentProfileLabel = CreateMutedLabel(new Color(0.75f, 0.75f, 0.75f), minHeight: 36);
            root.Add(currentProfileLabel);

            root.Add(CreateButtonRow(
                CreateButton("◀ Previous",
                    "Select the previous profile in Unity's Device Simulator list.",
                    () => SelectAdjacentProfile(-1)),
                CreateButton("Next ▶",
                    "Select the next profile in Unity's Device Simulator list.",
                    () => SelectAdjacentProfile(1))));

            autoPreviousButton = CreateButton(
                "◀ Auto Prev",
                "Automatically step to the previous profile on the interval below.",
                ToggleAutoPrevious);
            pauseButton = CreateButton(
                "Pause",
                "Pause or resume automatic profile switching.",
                TogglePause);
            autoNextButton = CreateButton(
                "Auto Next ▶",
                "Automatically step to the next profile on the interval below.",
                ToggleAutoNext);
            root.Add(CreateButtonRow(autoPreviousButton, pauseButton, autoNextButton));

            intervalField = new FloatField("Interval (sec)")
            {
                value = intervalSec,
                tooltip = "Seconds between automatic profile switches."
            };
            intervalField.style.marginBottom = 2;
            intervalField.RegisterValueChangedCallback(OnIntervalChanged);
            root.Add(intervalField);

            autoStatusLabel = CreateMutedLabel(new Color(0.65f, 0.8f, 0.95f), minHeight: 0);
            autoStatusLabel.style.marginTop = 2;
            root.Add(autoStatusLabel);

            RefreshAutoUi();
            UpdateCurrentProfile();
            return root;
        }

        #region UI helpers

        static Label CreateMutedLabel(Color color, float minHeight)
        {
            var label = new Label
            {
                style =
                {
                    whiteSpace = WhiteSpace.Normal,
                    unityTextAlign = TextAnchor.MiddleLeft,
                    color = new StyleColor(color),
                    marginBottom = 4,
                }
            };
            if (minHeight > 0f)
                label.style.minHeight = minHeight;
            return label;
        }

        static Button CreateButton(string text, string tooltip, System.Action onClick)
        {
            var button = new Button(onClick) { text = text, tooltip = tooltip };
            button.style.flexGrow = 1;
            return button;
        }

        static VisualElement CreateButtonRow(params VisualElement[] children)
        {
            var row = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row, marginBottom = 4 }
            };
            for (int i = 0; i < children.Length; i++)
                row.Add(children[i]);
            return row;
        }

        void SetLabelText(Label label, ref string cache, string text)
        {
            if (label == null || cache == text)
                return;
            cache = text;
            label.text = text;
        }

        #endregion

        #region Auto controls

        bool IsAutoRunning => autoMode != AutoMode.Off && !autoPaused;

        int AutoDirection => autoMode == AutoMode.Previous ? -1 : 1;

        void OnIntervalChanged(ChangeEvent<float> evt)
        {
            intervalSec = Mathf.Clamp(evt.newValue, MinIntervalSec, MaxIntervalSec);
            if (!Mathf.Approximately(intervalField.value, intervalSec))
                intervalField.SetValueWithoutNotify(intervalSec);
            SessionState.SetFloat(IntervalSessionKey, intervalSec);
            if (IsAutoRunning)
                ScheduleNextAdvance();
        }

        void ToggleAutoNext()
        {
            if (autoMode == AutoMode.Next && !autoPaused)
                StopAuto();
            else
                StartAuto(AutoMode.Next);
        }

        void ToggleAutoPrevious()
        {
            if (autoMode == AutoMode.Previous && !autoPaused)
                StopAuto();
            else
                StartAuto(AutoMode.Previous);
        }

        void TogglePause()
        {
            if (autoMode == AutoMode.Off)
                return;

            autoPaused = !autoPaused;
            if (autoPaused)
                UnhookUpdate();
            else
            {
                HookUpdate();
                ScheduleNextAdvance();
            }

            RefreshAutoUi();
        }

        void StartAuto(AutoMode mode)
        {
            autoMode = mode;
            autoPaused = false;
            HookUpdate();
            SelectAdjacentProfile(AutoDirection);
            // Interval restarts when the pending selection settles (ClearPendingSelection).
            RefreshAutoUi();
        }

        void StopAuto()
        {
            autoMode = AutoMode.Off;
            autoPaused = false;
            UnhookUpdate();
            RefreshAutoUi();
        }

        void ScheduleNextAdvance() =>
            nextAdvanceAt = EditorApplication.timeSinceStartup + intervalSec;

        void HookUpdate()
        {
            if (updateHooked || destroyed)
                return;
            EditorApplication.update += OnEditorUpdate;
            updateHooked = true;
        }

        void UnhookUpdate()
        {
            if (!updateHooked)
                return;
            EditorApplication.update -= OnEditorUpdate;
            updateHooked = false;
        }

        void OnEditorUpdate()
        {
            if (destroyed || !IsAutoRunning)
                return;
            // Cheap early-outs — do not scan Simulator device lists here.
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;
            if (!string.IsNullOrEmpty(pendingProfileName))
                return;
            if (EditorApplication.timeSinceStartup < nextAdvanceAt)
                return;

            SelectAdjacentProfile(AutoDirection);
        }

        void OnBeforeAssemblyReload() => StopAuto();

        void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingEditMode ||
                change == PlayModeStateChange.ExitingPlayMode)
                StopAuto();
        }

        void RefreshAutoUi()
        {
            if (autoPreviousButton == null)
                return;

            bool prevActive = autoMode == AutoMode.Previous && !autoPaused;
            bool nextActive = autoMode == AutoMode.Next && !autoPaused;
            autoPreviousButton.text = prevActive ? "● Auto Prev" : "◀ Auto Prev";
            autoNextButton.text = nextActive ? "● Auto Next" : "Auto Next ▶";
            pauseButton.text = autoPaused && autoMode != AutoMode.Off ? "Resume" : "Pause";
            pauseButton.SetEnabled(autoMode != AutoMode.Off);

            string status;
            if (autoMode == AutoMode.Off)
            {
                status = "Auto: off";
            }
            else
            {
                string direction = autoMode == AutoMode.Next ? "next" : "previous";
                status = autoPaused
                    ? $"Auto: paused ({direction}, every {intervalSec:0.##}s)"
                    : $"Auto: {direction} every {intervalSec:0.##}s";
            }

            SetLabelText(autoStatusLabel, ref lastAutoStatusText, status);
        }

        #endregion

        #region Manual / selection

        void OnSelectionChanged() => UpdateCurrentProfile();

        void OnSessionChanged(FoldableEditorSession.Source _) => UpdateCurrentProfile();

        void OnCatalogChanged()
        {
            profileNamesDirty = true;
            UpdateCurrentProfile();
        }

        void SelectAdjacentProfile(int direction)
        {
            EnsureDisplayedProfileNames(force: true);
            if (displayedProfileNames.Count == 0)
            {
                Debug.LogWarning(
                    "FoldAndFlip: Unity's Device Simulator profile list is unavailable.");
                StopAuto();
                return;
            }

            string currentName = CurrentDeviceName();
            int resolvedIndex = ResolveProfileIndex(displayedProfileNames, currentName);
            int start = resolvedIndex >= 0
                ? resolvedIndex
                : direction > 0 ? -1 : 0;
            for (int offset = 1; offset <= displayedProfileNames.Count; offset++)
            {
                int index = (start + direction * offset) % displayedProfileNames.Count;
                if (index < 0)
                    index += displayedProfileNames.Count;

                string targetName = displayedProfileNames[index];
                FoldableEditorSession.ResolveScreenForProduct(
                    targetName,
                    FoldableEditorSession.ProductState,
                    out _,
                    out int screenIndex);

                if (!SimulatorDeviceBridge.SelectDevice(
                        targetName,
                        Mathf.Max(0, screenIndex)))
                    continue;

                currentProfileIndex = index;
                previousProfileName = currentName;
                pendingProfileName = targetName;
                UpdateCurrentProfileLabel(pendingProfileName, isPending: true);
                SimulatorDeviceBridge.RequestSelectionPublish();
                return;
            }

            Debug.LogWarning(
                "FoldAndFlip: no Device Simulator profile could be selected.");
            StopAuto();
        }

        void UpdateCurrentProfile()
        {
            if (currentProfileLabel == null)
                return;

            EnsureDisplayedProfileNames(force: false);
            string currentName = CurrentDeviceName();
            if (!string.IsNullOrEmpty(pendingProfileName))
            {
                int pendingIndex = ResolveProfileIndex(
                    displayedProfileNames,
                    pendingProfileName);
                if (pendingIndex < 0)
                {
                    // List may have changed under us — force one refresh before giving up.
                    EnsureDisplayedProfileNames(force: true);
                    pendingIndex = ResolveProfileIndex(
                        displayedProfileNames,
                        pendingProfileName);
                }

                if (pendingIndex < 0)
                {
                    ClearPendingSelection();
                }
                else if (ProfileNamesEqual(currentName, pendingProfileName))
                {
                    currentProfileIndex = pendingIndex;
                    ClearPendingSelection();
                }
                else if (string.IsNullOrEmpty(currentName) ||
                         ProfileNamesEqual(currentName, previousProfileName))
                {
                    currentProfileIndex = pendingIndex;
                    UpdateCurrentProfileLabel(pendingProfileName, isPending: true);
                    return;
                }
                else
                {
                    ClearPendingSelection();
                }
            }

            currentProfileIndex = ResolveProfileIndex(displayedProfileNames, currentName);
            if (currentProfileIndex < 0 && !string.IsNullOrEmpty(currentName))
            {
                EnsureDisplayedProfileNames(force: true);
                currentProfileIndex = ResolveProfileIndex(displayedProfileNames, currentName);
            }

            UpdateCurrentProfileLabel(currentName, isPending: false);
        }

        void UpdateCurrentProfileLabel(string currentName, bool isPending)
        {
            string text;
            if (string.IsNullOrEmpty(currentName))
            {
                text = "No Simulator device selected";
            }
            else
            {
                int displayedPosition = currentProfileIndex + 1;
                text = displayedPosition > 0
                    ? $"Current profile {displayedPosition}/{displayedProfileNames.Count}: {currentName}" +
                      (isPending ? " (switching…)" : "")
                    : "Current device is not in the displayed profile list: " + currentName;
            }

            SetLabelText(currentProfileLabel, ref lastProfileLabelText, text);
        }

        void ClearPendingSelection()
        {
            bool hadPending = !string.IsNullOrEmpty(pendingProfileName);
            pendingProfileName = "";
            previousProfileName = "";
            // Start the interval after the Simulator selection actually lands.
            if (hadPending && IsAutoRunning)
                ScheduleNextAdvance();
        }

        void EnsureDisplayedProfileNames(bool force)
        {
            if (!force && !profileNamesDirty && displayedProfileNames.Count > 0)
                return;

            // FindObjectsOfTypeAll + reflection — only when the displayed list may change.
            SimulatorDeviceBridge.GetDeviceNamesInDisplayOrder(displayedProfileNames);
            profileNamesDirty = false;
        }

        static int ResolveProfileIndex(List<string> profileNames, string currentName)
        {
            for (int i = 0; i < profileNames.Count; i++)
            {
                if (ProfileNamesEqual(profileNames[i], currentName))
                    return i;
            }

            return -1;
        }

        static bool ProfileNamesEqual(string first, string second) =>
            string.Equals(first, second, System.StringComparison.OrdinalIgnoreCase);

        static string CurrentDeviceName()
        {
            if (SimulatorDeviceBridge.GetActiveSelection(out var selection) &&
                selection.IsValid)
                return selection.FriendlyName;

            return FoldableEditorSession.BoundFriendlyName;
        }

        #endregion
    }
}
