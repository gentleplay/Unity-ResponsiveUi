using System.Collections.Generic;
using UnityEditor;
using UnityEditor.DeviceSimulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace FDPlus.Foldable.Editor
{
    /// <summary>
    /// Android Fold &amp; Flip Device Simulator panel (Galaxy Fold / Flip style profiles).
    /// iPhone Duo profiles (landscape-native inner) use the iPhone Duo panel instead.
    /// </summary>
    public class FoldableDeviceSimulatorPlugin : DeviceSimulatorPlugin
    {
        #region Constants

        const string PresetButtonPrefix = "productPreset:";

        public const string Title = "Android Fold & Flip";

        const string SelectDeviceHint =
            "Select a ★ Fold / … or ★ Flip / … device in the Simulator list to show controls.";

        const string IphoneDuoHint =
            "iPhone Duo is selected. Use the iPhone Duo panel.";

        public override string title => Title;

        #endregion

        #region Serialized

        [SerializeField] bool autoApplyInPlayMode = true;

        #endregion

        #region Runtime state

        VisualElement controlsHost;
        Label unavailableLabel;
        Label boundLabel;
        Label kindLabel;
        PopupField<string> productField;
        PopupField<string> screenOrientField;
        PopupField<string> orientField;
        Slider hingeSlider;
        IntegerField thicknessField;
        Toggle sepToggle;
        Toggle autoToggle;
        bool ignoreUiCallbacks;
        bool refreshQueued;
        bool orientationSyncQueued;
        bool destroyed;

        #endregion

        #region Lifecycle

        public override void OnCreate()
        {
            destroyed = false;
            deviceSimulator.deviceChanged += OnDeviceChanged;
            SimulatorDeviceBridge.SelectionChanged += OnBridgeSelectionChanged;
            FoldableSimulatorPanelUi.RefreshSessionBinding();
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
            FoldableEditorSession.Changed += OnSessionChanged;
        }

        public override void OnDestroy()
        {
            destroyed = true;
            if (deviceSimulator != null)
                deviceSimulator.deviceChanged -= OnDeviceChanged;
            SimulatorDeviceBridge.SelectionChanged -= OnBridgeSelectionChanged;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
            FoldableEditorSession.Changed -= OnSessionChanged;
        }

        public override VisualElement OnCreateUI()
        {
            var root = new VisualElement();
            root.style.paddingLeft = 4;
            root.style.paddingRight = 4;
            root.style.paddingTop = 4;
            root.style.paddingBottom = 8;

            unavailableLabel = FoldableSimulatorPanelUi.CreateMutedLabel(new Color(0.75f, 0.75f, 0.75f));
            unavailableLabel.text = SelectDeviceHint;
            root.Add(unavailableLabel);

            controlsHost = new VisualElement();
            root.Add(controlsHost);

            boundLabel = FoldableSimulatorPanelUi.CreateMutedLabel(new Color(0.85f, 0.85f, 0.55f));
            controlsHost.Add(boundLabel);

            kindLabel = FoldableSimulatorPanelUi.CreateMutedLabel(new Color(0.7f, 0.85f, 0.95f));
            controlsHost.Add(kindLabel);

            ignoreUiCallbacks = true;
            var initialKind = FoldableEditorSession.DeviceKind;
            var initialNames = new List<string>(
                FoldableProductStateResolver.GetDisplayNames(initialKind));
            int initialProduct = Mathf.Clamp(
                FoldableProductStateResolver.ToIndex(FoldableEditorSession.ProductState),
                0,
                initialNames.Count - 1);
            productField = new PopupField<string>(
                "Product state",
                initialNames,
                initialProduct);
            productField.RegisterValueChangedCallback(_ =>
            {
                if (!ignoreUiCallbacks)
                    PublishFromUi();
            });
            controlsHost.Add(productField);

            screenOrientField = new PopupField<string>(
                "Screen orientation",
                new List<string> { "Portrait", "Landscape" },
                FoldableEditorSession.IsLandscape ? 1 : 0);
            screenOrientField.RegisterValueChangedCallback(_ =>
            {
                if (!ignoreUiCallbacks)
                    PublishFromUi();
            });
            controlsHost.Add(screenOrientField);

            orientField = new PopupField<string>(
                "Half-open hinge",
                new List<string> { "Tabletop (Horizontal)", "Book (Vertical)" },
                0);
            orientField.RegisterValueChangedCallback(_ =>
            {
                if (!ignoreUiCallbacks)
                    PublishFromUi(hingeChanged: true);
            });
            controlsHost.Add(orientField);

            hingeSlider = new Slider("Hinge position", 0.05f, 0.95f)
            {
                value = 0.5f,
                showInputField = true
            };
            hingeSlider.RegisterValueChangedCallback(_ =>
            {
                if (!ignoreUiCallbacks)
                    PublishFromUi();
            });
            controlsHost.Add(hingeSlider);

            thicknessField = new IntegerField("Hinge thickness") { value = 24 };
            thicknessField.RegisterValueChangedCallback(_ =>
            {
                if (!ignoreUiCallbacks)
                    PublishFromUi();
            });
            controlsHost.Add(thicknessField);

            sepToggle = new Toggle("isSeparating (hinge present)")
            {
                value = FoldableEditorSession.IsSeparating,
                tooltip = "WindowManager hinge flag. On in Flex / Half Opened when the two screens are split by a hinge. " +
                          "Spanned dual-screen Expand stays full-screen Flat even when this is on."
            };
            sepToggle.RegisterValueChangedCallback(_ =>
            {
                if (!ignoreUiCallbacks)
                    PublishFromUi();
            });
            controlsHost.Add(sepToggle);

            autoToggle = new Toggle("Auto-apply on change") { value = autoApplyInPlayMode };
            autoToggle.RegisterValueChangedCallback(evt =>
            {
                autoApplyInPlayMode = evt.newValue;
                ApplyAfterChange();
            });
            controlsHost.Add(autoToggle);

            var presets = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap, marginTop = 6 }
            };
            for (int i = 0; i < 3; i++)
            {
                int index = i;
                presets.Add(CreatePresetButton(index));
            }
            controlsHost.Add(presets);

            var hint = new Label(
                "Buttons follow device kind (Fold: Normal/Expand/… · Flip: Closed/Opened/…).");
            hint.style.whiteSpace = WhiteSpace.Normal;
            hint.style.fontSize = 10;
            hint.style.color = new StyleColor(new Color(0.65f, 0.65f, 0.65f));
            hint.style.marginBottom = 4;
            controlsHost.Add(hint);

            var applyButton = new Button(ForceApply) { text = "Apply" };
            applyButton.style.marginTop = 6;
            applyButton.tooltip =
                "Apply posture to FoldablePostureDetector (LayoutChanged / SyncSimulatedLayoutState).";
            controlsHost.Add(applyButton);

            // Play Mode recreates this panel — never force Expand defaults here or Normal is lost.
            RefreshBinding();
            QueueRefreshBinding();
            root.schedule.Execute(EnableUiCallbacks);
            return root;
        }

        #endregion

        #region GUI

        Button CreatePresetButton(int productIndex)
        {
            var button = new Button(() => SetProductPreset(productIndex))
            {
                text = "State",
                name = PresetButtonPrefix + productIndex
            };
            button.style.marginRight = 4;
            button.style.marginBottom = 4;
            return button;
        }

        #endregion

        #region Simulator

        void RefreshBinding()
        {
            FoldableEditorSessionWatcher.RefreshNow();
            SyncFieldsFromSession();
            UpdateControlsVisibility();
        }

        /// <summary>An Android Fold / Flip profile is bound (not an iPhone Duo).</summary>
        static bool OwnsBoundDevice =>
            FoldableEditorSession.IsFoldableDeviceBound &&
            !FoldableEditorSession.BoundInnerLandscapeNative;

        void SetProductPreset(int index)
        {
            if (!OwnsBoundDevice)
                return;

            var product = FoldableProductStateResolver.FromIndex(index);
            var lockedHinge = FoldablePlayerOrientation.GetLockedHalfOpenHinge(innerLandscapeNative: false);
            var orient = product == FoldableProductState.HalfOpened
                ? (lockedHinge ?? FoldableProductStateResolver.DefaultHalfOpenHinge())
                : FoldableEditorSession.Orientation;

            bool separating = product == FoldableProductState.HalfOpened ||
                              (product == FoldableProductState.Opened &&
                               FoldableEditorSession.DeviceKind == FoldableDeviceKind.DualScreen);

            bool landscape = FoldableEditorSession.PresetPose(product, orient);

            FoldableEditorSession.PublishProductState(
                product,
                orient,
                0.5f,
                FoldableEditorSession.HingeThicknessPx,
                separating,
                FoldableEditorSession.MetricsWidth,
                FoldableEditorSession.MetricsHeight,
                FoldableEditorSession.Source.Simulator,
                landscape);

            ApplyProductChange();
        }

        void PublishFromUi(bool hingeChanged = false)
        {
            if (!OwnsBoundDevice)
                return;

            var choices = FoldableProductStateResolver.GetDisplayNames(FoldableEditorSession.DeviceKind);
            int idx = System.Array.IndexOf(choices, productField.value);
            if (idx < 0)
                return;

            var product = FoldableProductStateResolver.FromIndex(idx);
            var orientation = orientField.value.StartsWith("Book")
                ? FoldableOrientation.Vertical
                : FoldableOrientation.Horizontal;

            var lockedHinge = FoldablePlayerOrientation.GetLockedHalfOpenHinge(innerLandscapeNative: false);
            if (product == FoldableProductState.HalfOpened && lockedHinge.HasValue)
                orientation = lockedHinge.Value;

            // Auto Rotation (portrait + landscape): honor the panel dropdown.
            // Fixed Player Settings (or Auto with one axis): force the project lock.
            // Half-open hinge change: Fold tabletop is landscape when the hinge is horizontal.
            // The dropdown shows the pose name for the session's product (Fold half-open: tall =
            // Landscape), so turn it back into the screen shape.
            bool landscape = FoldableProductStateResolver.PoseLandscape(
                FoldableEditorSession.DeviceKind,
                FoldableEditorSession.ProductState,
                screenOrientField.value.StartsWith("Landscape"));
            // A changed half-open card decides the screen instead of the Player lock.
            var lockedLandscape = FoldableEditorSession.AndroidHalfOpenCardChanged(product, landscapeNative: false)
                ? null
                : FoldablePlayerOrientation.GetLockedLandscapeForProduct(product, innerLandscapeNative: false);
            bool? halfOpenScreen = FoldableEditorSession.AndroidHalfOpenScreenLandscape(
                product, landscapeNative: false);
            if (halfOpenScreen.HasValue)
                landscape = halfOpenScreen.Value;
            else if (lockedLandscape.HasValue)
                landscape = lockedLandscape.Value;
            else if (hingeChanged && product == FoldableProductState.HalfOpened)
            {
                landscape = FoldablePlayerOrientation.ResolveForProduct(
                    FoldableEditorSession.DeviceKind, orientation, product);
            }

            FoldableEditorSession.PublishProductState(
                product,
                orientation,
                hingeSlider.value,
                Mathf.Max(1, thicknessField.value),
                sepToggle.value,
                FoldableEditorSession.MetricsWidth,
                FoldableEditorSession.MetricsHeight,
                FoldableEditorSession.Source.Simulator,
                landscape);

            ApplyProductChange();
        }

        void ApplyProductChange()
        {
            FoldableSimulatorScreenCommit.CommitNowThenCatchup();
            SyncFieldsFromSession();
            ApplyAfterChange();
        }

        void OnSessionChanged(FoldableEditorSession.Source source)
        {
            if (!OwnsBoundDevice)
            {
                UpdateControlsVisibility();
                return;
            }

            // Half-open rotation is locked to the pose. Other postures can still be turned.
            SyncFieldsFromSession(enforceLock:
                AndroidHalfOpenRotationIsLocked ||
                source != FoldableEditorSession.Source.Bridge);
            UpdateControlsVisibility();
        }

        #endregion

        #region Fields

        void SyncFieldsFromSession(bool enforceLock = true)
        {
            if (!OwnsBoundDevice || productField == null)
                return;

            ignoreUiCallbacks = true;
            try
            {
                var kind = FoldableEditorSession.DeviceKind;
                var choices = new List<string>(FoldableProductStateResolver.GetDisplayNames(kind));
                productField.choices = choices;

                int idx = FoldableProductStateResolver.ToIndex(FoldableEditorSession.ProductState);
                productField.SetValueWithoutNotify(choices[Mathf.Clamp(idx, 0, choices.Count - 1)]);

                // A changed half-open card decides the screen instead of the Player lock; a Follow
                // device card leaves the field editable under a one-axis lock.
                bool cardChanged = FoldableEditorSession.AndroidHalfOpenCardChanged(
                    FoldableEditorSession.ProductState, landscapeNative: false);
                var lockedLandscape = cardChanged
                    ? null
                    : FoldablePlayerOrientation.GetLockedLandscapeForProduct(
                        FoldableEditorSession.ProductState, innerLandscapeNative: false);
                bool? halfOpenScreen = FoldableEditorSession.AndroidHalfOpenScreenLandscape(
                    FoldableEditorSession.ProductState, landscapeNative: false);
                bool landscape = halfOpenScreen ?? lockedLandscape ?? FoldableEditorSession.IsLandscape;
                var lockedHinge = FoldablePlayerOrientation.GetLockedHalfOpenHinge(innerLandscapeNative: false);
                // Pose name, as the slots use it: Fold half-open tall (1848×2448) is Landscape.
                bool poseLandscape = FoldableProductStateResolver.PoseLandscape(
                    kind, FoldableEditorSession.ProductState, landscape);

                screenOrientField?.SetValueWithoutNotify(
                    poseLandscape ? "Landscape" : "Portrait");
                if (screenOrientField != null)
                {
                    // Locked when Player Settings fix one axis, and while a Half Opened card
                    // holds the pose (only folding changes it). Auto Rotation + all axes →
                    // editable otherwise (screenshot case).
                    bool rotationLocked = lockedLandscape.HasValue || AndroidHalfOpenRotationIsLocked;
                    screenOrientField.SetEnabled(!rotationLocked);
                    screenOrientField.tooltip = lockedLandscape.HasValue || !rotationLocked
                        ? FoldableSimulatorPanelUi.OrientationTooltip(lockedLandscape.HasValue)
                        : FoldableSimulatorPanelUi.HalfOpenHeldOrientationTooltip;
                }

                RelabelPresetButtons(kind);

                bool half = FoldableEditorSession.ProductState == FoldableProductState.HalfOpened;
                var displayHinge = half && lockedHinge.HasValue
                    ? lockedHinge.Value
                    : FoldableEditorSession.Orientation;
                orientField.SetValueWithoutNotify(
                    displayHinge == FoldableOrientation.Vertical
                        ? "Book (Vertical)"
                        : "Tabletop (Horizontal)");
                hingeSlider.SetValueWithoutNotify(FoldableEditorSession.HingeNormalized);
                thicknessField.SetValueWithoutNotify(FoldableEditorSession.HingeThicknessPx);
                sepToggle.SetValueWithoutNotify(FoldableEditorSession.IsSeparating);
                autoToggle?.SetValueWithoutNotify(autoApplyInPlayMode);

                // Half-open hinge: editable only in Flex AND when project allows both axes.
                orientField.SetEnabled(half && !lockedHinge.HasValue);
                if (orientField != null)
                {
                    if (!half)
                    {
                        orientField.tooltip = "Available in Expand Half Opened / Flex.";
                    }
                    else if (lockedHinge.HasValue)
                    {
                        orientField.tooltip = "Locked to Player Settings → " +
                            (lockedHinge.Value == FoldableOrientation.Horizontal ? "Tabletop" : "Book") +
                            (poseLandscape ? " (Landscape)" : " (Portrait)");
                    }
                    else
                    {
                        orientField.tooltip =
                            "Flex hinge direction. Editable because Player Settings allow Auto Rotation (portrait + landscape).";
                    }
                }
                hingeSlider.SetEnabled(half);
                thicknessField.SetEnabled(half);
                // Closed has no hinge split; Half Opened / Expand keep the developer toggle.
                sepToggle.SetEnabled(
                    FoldableEditorSession.ProductState != FoldableProductState.Closed);

                UpdateStatusLabels(kind);
            }
            finally
            {
                ignoreUiCallbacks = false;
            }

            if (enforceLock)
                EnforceProjectOrientationLock();
        }

        void EnforceProjectOrientationLock()
        {
            if (!OwnsBoundDevice)
                return;

            var lockedLandscape = FoldablePlayerOrientation.GetLockedLandscapeForProduct(
                FoldableEditorSession.ProductState, innerLandscapeNative: false);
            var lockedHinge = FoldablePlayerOrientation.GetLockedHalfOpenHinge(innerLandscapeNative: false);

            bool half = FoldableEditorSession.ProductState == FoldableProductState.HalfOpened;
            bool? screenLandscape = half
                ? FoldableEditorSession.AndroidHalfOpenScreenLandscape(
                    FoldableEditorSession.ProductState, landscapeNative: false)
                : lockedLandscape;
            if (!screenLandscape.HasValue &&
                !(half && FoldableEditorSession.AndroidHalfOpenCardChanged(
                    FoldableEditorSession.ProductState, landscapeNative: false)))
                screenLandscape = lockedLandscape;

            bool needLandscape = screenLandscape.HasValue &&
                                 FoldableEditorSession.IsLandscape != screenLandscape.Value;
            bool needHinge = half && lockedHinge.HasValue &&
                             FoldableEditorSession.Orientation != lockedHinge.Value;

            if (!needLandscape && !needHinge)
                return;

            if (needHinge)
            {
                FoldableEditorSession.PublishProductState(
                    FoldableEditorSession.ProductState,
                    lockedHinge.Value,
                    FoldableEditorSession.HingeNormalized,
                    FoldableEditorSession.HingeThicknessPx,
                    FoldableEditorSession.IsSeparating,
                    FoldableEditorSession.MetricsWidth,
                    FoldableEditorSession.MetricsHeight,
                    FoldableEditorSession.Source.Simulator,
                    screenLandscape ?? FoldableEditorSession.IsLandscape);
            }
            else
            {
                FoldableEditorSession.PublishAndroidScreen(
                    screenLandscape.Value, FoldableEditorSession.Source.Simulator);
            }

            // Session updated — push Simulator chrome on the next tick (avoid re-entrancy).
            QueueOrientationSync();
        }

        void RelabelPresetButtons(FoldableDeviceKind kind)
        {
            if (controlsHost == null)
                return;

            var labels = FoldableProductStateResolver.GetDisplayNames(kind);
            int selected = FoldableProductStateResolver.ToIndex(FoldableEditorSession.ProductState);
            controlsHost.Query<Button>().ForEach(button =>
            {
                if (button.name == null || !button.name.StartsWith(PresetButtonPrefix))
                    return;

                if (!int.TryParse(button.name.Substring(PresetButtonPrefix.Length), out int index))
                    return;

                if (index >= 0 && index < labels.Length)
                    button.text = labels[index];

                FoldableSimulatorPanelUi.StyleSelected(
                    button, FoldableEditorSession.IsFoldableDeviceBound && index == selected);
            });
        }

        void UpdateStatusLabels(FoldableDeviceKind kind)
        {
            if (!FoldableEditorSession.IsFoldableDeviceBound)
            {
                if (boundLabel != null) boundLabel.text = "";
                if (kindLabel != null) kindLabel.text = "";
                return;
            }

            if (boundLabel != null)
            {
                var posture = FoldableEditorSession.BuildLayout().ResolvePosture();
                boundLabel.text =
                    $"{FoldableEditorSession.BoundFriendlyName} · {FoldableEditorSession.BoundScreenName} · " +
                    $"{FoldableEditorSession.MetricsWidth}×{FoldableEditorSession.MetricsHeight} · {posture}";
            }

            if (kindLabel != null)
            {
                string screenRot = FoldableProductStateResolver.PoseLandscape(
                    kind, FoldableEditorSession.ProductState, FoldableEditorSession.IsLandscape)
                    ? "Landscape"
                    : "Portrait";
                kindLabel.text =
                    $"{FoldableProductStateResolver.GetKindDisplayName(kind)} · " +
                    FoldableProductStateResolver.GetDisplayName(kind, FoldableEditorSession.ProductState) +
                    $" · {screenRot}" +
                    (FoldableEditorSession.ProductState == FoldableProductState.HalfOpened
                        ? (FoldableEditorSession.Orientation == FoldableOrientation.Horizontal
                            ? " · Tabletop/Flex"
                            : " · Book")
                        : "");
            }
        }

        void UpdateControlsVisibility()
        {
            bool show = !SimulatorDeviceBridge.IsSelectionPending && OwnsBoundDevice;
            if (controlsHost != null)
                controlsHost.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (unavailableLabel != null)
            {
                unavailableLabel.text = FoldableSimulatorPanelUi.UnavailableHint(
                    true, "", SelectDeviceHint, IphoneDuoHint);
                unavailableLabel.style.display = show ? DisplayStyle.None : DisplayStyle.Flex;
            }
        }

        #endregion

        #region Events

        void OnDeviceChanged()
        {
            // Unity raises deviceChanged before the reflected selection is always
            // committed. Hide Fold controls until SimulatorDeviceBridge publishes
            // a stable identity; never idle-poll the Simulator.
            SimulatorDeviceBridge.RequestSelectionPublish();
            UpdateControlsVisibility();
        }

        void OnBridgeSelectionChanged()
        {
            if (CannotRunDelayedWork())
                return;
            if (!OwnsBoundDevice)
            {
                UpdateControlsVisibility();
                return;
            }

            SyncFieldsFromSession(enforceLock: false);
            LockAndroidHalfOpenChrome();
            UpdateControlsVisibility();
        }

        /// <summary>
        /// Android Fold / Fold Cover / Flip half-open keeps its screen when the simulator is rotated:
        /// a Fold under a one-axis Player lock, or any card that holds it
        /// (<see cref="FoldableEditorSession.AndroidHalfOpenHeldScreenLandscape"/>).
        /// </summary>
        static bool AndroidHalfOpenRotationIsLocked =>
            OwnsBoundDevice &&
            FoldableEditorSession.AndroidHalfOpenHeldScreenLandscape(
                FoldableEditorSession.DeviceKind,
                FoldableEditorSession.ProductState,
                landscapeNative: false).HasValue;

        void LockAndroidHalfOpenChrome()
        {
            if (!AndroidHalfOpenRotationIsLocked)
                return;
            if (!SimulatorDeviceBridge.GetActiveSelection(out var selection) || !selection.HasRotation)
                return;

            bool chromeLandscape = SimulatorDeviceBridge.IsLandscapeRotation(selection.RotationDegrees);
            if (chromeLandscape == FoldableEditorSession.IsLandscape)
                return;

            QueueOrientationSync();
        }

        void QueueRefreshBinding()
        {
            if (refreshQueued)
                return;
            refreshQueued = true;
            EditorApplication.delayCall += () =>
            {
                refreshQueued = false;
                if (CannotRunDelayedWork())
                    return;
                RefreshBinding();
            };
        }

        void QueueOrientationSync()
        {
            if (orientationSyncQueued)
                return;
            orientationSyncQueued = true;
            EditorApplication.delayCall += () =>
            {
                orientationSyncQueued = false;
                if (CannotRunDelayedWork() || !OwnsBoundDevice)
                    return;
                FoldableSimulatorScreenCommit.CommitNowThenCatchup();
                SyncFieldsFromSession();
            };
        }

        void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredPlayMode)
                ApplyAfterChange();
        }

        void OnBeforeAssemblyReload()
        {
            destroyed = true;
            FoldableEditorSession.FlushPersistedSession();
        }

        bool CannotRunDelayedWork() =>
            destroyed || EditorApplication.isCompiling || EditorApplication.isUpdating;

        void EnableUiCallbacks()
        {
            if (!destroyed)
                ignoreUiCallbacks = false;
        }

        #endregion

        #region Apply

        void ApplyAfterChange()
        {
            if (!autoApplyInPlayMode || !OwnsBoundDevice)
                return;
            FoldableSimulatorApply.Apply(showDialogs: false, Title, SelectDeviceHint);
        }

        void ForceApply()
        {
            FoldableSimulatorScreenCommit.CommitNow();
            FoldableSimulatorApply.Apply(showDialogs: true, Title, SelectDeviceHint);
        }

        #endregion
    }
}
