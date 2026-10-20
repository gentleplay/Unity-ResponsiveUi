using System.Collections.Generic;
using FDPlus.Foldable;
using FDPlus.Foldable.Editor;
using UnityEditor;
using UnityEditor.DeviceSimulation;
using UnityEngine;
using UnityEngine.UIElements;

namespace FDPlus.IphoneDuo.Editor
{
    /// <summary>
    /// iPhone Duo Device Simulator panel: Closed (cover) / Opened (flat inner) / Seated (half open),
    /// and how the device is held (wide or tall; Seated: book or laptop). The screen follows the
    /// project's <see cref="IphoneDuoRules"/> (Project Settings › Responsive UI › iPhone Duo) through
    /// <see cref="IphoneDuoEditorRules"/>, the same decision the iOS player makes, and the panel
    /// publishes the result as <see cref="IphoneDuoDevice.State"/> so <see cref="IphoneDuoEvents"/> and
    /// game code react in Play Mode. Sideways shows the layout the turned game renders, on the game's
    /// own axis (the Device Simulator cannot turn the picture). Drives the shared <see cref="FoldableEditorSession"/>;
    /// Android Fold / Flip profiles use the Android Fold &amp; Flip panel.
    /// </summary>
    public class IphoneDuoDeviceSimulatorPlugin : DeviceSimulatorPlugin
    {
        #region Constants

        public const string Title = "iPhone Duo";

        const string SelectDeviceHint =
            "Select iPhone Duo / iPhone Duo-like in the Simulator list to show controls.";

        const string AndroidFoldHint =
            "An Android Fold / Flip is selected. Use the Android Fold & Flip panel.";

        const string LaptopChoice = "Laptop · Seated Portrait (tall)";
        const string BookChoice = "Book · Seated Landscape (wide)";

        const string WideChoice = "Wide";
        const string TallChoice = "Tall";

        const string HoldTooltip =
            "Laptop: tall, horizontal fold, gameplay on the top pane and controls on the bottom pane. " +
            "Book: wide, vertical fold. What the game shows follows the Seated rule " +
            "(Project Settings › Responsive UI › iPhone Duo).";

        const string HeldHoldTooltip =
            "Held while Seated: Seated rotation is Locked, so rotating does not change the pose (Locked pose " +
            "picks it). Flatten or close to rotate, or set Seated rotation to Follows hold " +
            "(Project Settings › Responsive UI › iPhone Duo).";

        const string FlatHoldTooltip =
            "How the device is held. Held across the Player lock (a landscape game held tall, or a portrait " +
            "game held wide) the game follows the posture's rule (Project Settings › Responsive UI › iPhone Duo): " +
            "Sideways shows the layout of the game's axis the turned game renders, Rotate the hold's one, " +
            "System Default the letterboxed one of the game's axis.";

        const string HeldFlatHoldTooltip =
            "Held while Opened: Flat rotation is Locked, so rotating does not change the screen (Locked pose " +
            "picks it; the opposite side still flips it upright). Fold or close to rotate, or set Flat rotation " +
            "to Follow device (Project Settings › Responsive UI › iPhone Duo).";

        static readonly FoldableProductState[] Postures =
        {
            FoldableProductState.Closed,
            FoldableProductState.Opened,
            FoldableProductState.HalfOpened,
        };

        static readonly string[] PostureNames = { "Closed", "Opened", "Seated" };

        public override string title => Title;

        #endregion

        #region Serialized

        [SerializeField] bool autoApplyInPlayMode = true;
        // Physical hold, kept across postures like the device (Seated: laptop = tall, book = wide).
        [SerializeField] bool tallHold;

        #endregion

        #region Runtime state

        VisualElement controlsHost;
        Label unavailableLabel;
        Label boundLabel;
        Label statusLabel;
        readonly List<Button> postureButtons = new List<Button>();
        PopupField<string> holdField;
        PopupField<string> wideTallField;
        Label ruleLabel;
        Toggle autoToggle;
        bool ignoreUiCallbacks;
        // Seated / Opened at the last session change: a change into either takes its entry hold from the screen.
        bool lastSeated;
        bool lastFlat;
        bool orientationSyncQueued;
        bool destroyed;

        #endregion

        #region Lifecycle

        public override void OnCreate()
        {
            destroyed = false;
            IphoneDuoEditorRules.Changed += OnRulesChanged;
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
            IphoneDuoEditorRules.Changed -= OnRulesChanged;
            if (deviceSimulator != null)
                deviceSimulator.deviceChanged -= OnDeviceChanged;
            SimulatorDeviceBridge.SelectionChanged -= OnBridgeSelectionChanged;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
            FoldableEditorSession.Changed -= OnSessionChanged;
            // The preview ends with the panel.
            IphoneDuoDevice.Publish(default);
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

            statusLabel = FoldableSimulatorPanelUi.CreateMutedLabel(new Color(0.7f, 0.85f, 0.95f));
            controlsHost.Add(statusLabel);

            ruleLabel = FoldableSimulatorPanelUi.CreateMutedLabel(new Color(0.75f, 0.9f, 0.7f));
            ruleLabel.style.whiteSpace = WhiteSpace.Normal;
            controlsHost.Add(ruleLabel);

            var postures = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row, flexWrap = Wrap.Wrap, marginTop = 2 }
            };
            postureButtons.Clear();
            for (int i = 0; i < Postures.Length; i++)
            {
                FoldableProductState product = Postures[i];
                var button = new Button(() => SetPosture(product)) { text = PostureNames[i] };
                button.style.marginRight = 4;
                button.style.marginBottom = 4;
                postureButtons.Add(button);
                postures.Add(button);
            }
            controlsHost.Add(postures);

            holdField = new PopupField<string>(
                "Seated hold",
                new List<string> { LaptopChoice, BookChoice },
                0)
            {
                tooltip = HoldTooltip
            };
            holdField.RegisterValueChangedCallback(evt =>
            {
                if (!ignoreUiCallbacks)
                    PublishHold(evt.newValue == LaptopChoice);
            });
            controlsHost.Add(holdField);

            wideTallField = new PopupField<string>(
                "Hold",
                new List<string> { WideChoice, TallChoice },
                tallHold ? 1 : 0)
            {
                tooltip = FlatHoldTooltip
            };
            wideTallField.RegisterValueChangedCallback(evt =>
            {
                if (!ignoreUiCallbacks)
                    PublishHold(evt.newValue == TallChoice);
            });
            controlsHost.Add(wideTallField);

            autoToggle = new Toggle("Auto-apply on change") { value = autoApplyInPlayMode };
            autoToggle.RegisterValueChangedCallback(evt =>
            {
                autoApplyInPlayMode = evt.newValue;
                ApplyAfterChange();
            });
            controlsHost.Add(autoToggle);

            var hint = FoldableSimulatorPanelUi.CreateMutedLabel(new Color(0.65f, 0.65f, 0.65f));
            hint.style.fontSize = 10;
            hint.text =
                "Closed: cover display · Opened: flat inner (Pad) · Seated: half open (iPhone Duo Seated). " +
                "The hold is kept across postures, like the device.";
            controlsHost.Add(hint);

            var rulesButton = new Button(() => SettingsService.OpenProjectSettings(IphoneDuoSettingsProvider.Path))
            {
                text = "Rules…",
                tooltip = "Project Settings › Responsive UI › iPhone Duo: what each posture does when held across the Player lock.",
            };
            rulesButton.style.marginTop = 4;
            controlsHost.Add(rulesButton);

            var applyButton = new Button(ForceApply)
            {
                text = "Apply",
                tooltip = "Apply posture to FoldablePostureDetector (LayoutChanged / SyncSimulatedLayoutState).",
            };
            applyButton.style.marginTop = 6;
            controlsHost.Add(applyButton);

            SyncFieldsFromSession();
            UpdateControlsVisibility();
            return root;
        }

        #endregion

        #region Publish

        /// <summary>An iPhone Duo profile (landscape-native inner) is bound.</summary>
        static bool OwnsBoundDevice =>
            FoldableEditorSession.IsFoldableDeviceBound &&
            FoldableEditorSession.BoundInnerLandscapeNative;

        static bool IsSeated => FoldableEditorSession.ProductState == FoldableProductState.HalfOpened;

        /// <summary>Seated holds its pose against chrome turns (<see cref="IphoneDuoRules.seatedRotation"/> Locked).</summary>
        static bool SeatedRotationLocked =>
            OwnsBoundDevice && IsSeated && IphoneDuoEditorRules.SeatedRotationLocked;

        static bool IsFlat => FoldableEditorSession.ProductState == FoldableProductState.Opened;

        /// <summary>
        /// Flat holds its screen against chrome turns (<see cref="IphoneDuoRules.flatRotation"/> Locked
        /// with a hold of its own, <see cref="IphoneDuoEditorRules.FlatRotationLocked"/>).
        /// </summary>
        static bool FlatRotationLocked =>
            OwnsBoundDevice && IsFlat && IphoneDuoEditorRules.FlatRotationLocked;

        IphoneDuoEditorRules.Outcome Outcome(FoldableProductState product) =>
            IphoneDuoEditorRules.Resolve(IphoneDuoHinge.ToPosture(product), tallHold);

        void SetPosture(FoldableProductState product)
        {
            if (!OwnsBoundDevice)
                return;
            PublishRules(product);
        }

        void PublishHold(bool tall)
        {
            tallHold = tall;
            if (OwnsBoundDevice)
                PublishRules(FoldableEditorSession.ProductState);
        }

        /// <summary>Publishes the screen the rules give <paramref name="product"/> and the current hold.</summary>
        void PublishRules(FoldableProductState product)
        {
            // Locked Seated: the Locked pose picks the hold (entering, the current hold is the entry hold).
            if (product == FoldableProductState.HalfOpened)
                tallHold = IphoneDuoEditorRules.SeatedHoldTall(entryTall: tallHold, holdTall: tallHold);
            // Locked Flat: the Locked pose picks the hold the same way.
            else if (product == FoldableProductState.Opened)
                tallHold = !IphoneDuoEditorRules.FlatHoldWide(entryWide: !tallHold, holdWide: !tallHold);
            bool landscape = Outcome(product).landscape;
            // Seated: the fold follows the screen shape (tall → horizontal laptop fold, wide → vertical).
            FoldableOrientation hinge = product == FoldableProductState.HalfOpened
                ? FoldableProductStateResolver.LandscapeNativeHalfOpenHinge(landscape)
                : FoldableEditorSession.Orientation;
            Publish(product, hinge, landscape);
        }

        void Publish(FoldableProductState product, FoldableOrientation hinge, bool landscape)
        {
            FoldableEditorSession.PublishProductState(
                product,
                hinge,
                0.5f,
                FoldableEditorSession.HingeThicknessPx,
                isSeparating: product == FoldableProductState.HalfOpened,
                FoldableEditorSession.MetricsWidth,
                FoldableEditorSession.MetricsHeight,
                FoldableEditorSession.Source.Simulator,
                landscape);
            FoldableSimulatorScreenCommit.CommitNowThenCatchup();
            SyncFieldsFromSession();
            ApplyAfterChange();
        }

        #endregion

        #region Fields

        void SyncFieldsFromSession()
        {
            if (holdField != null)
                SyncFields();

            // Preview state for IphoneDuoEvents / game code in Play Mode.
            IphoneDuoDevice.Publish(OwnsBoundDevice ? Outcome(FoldableEditorSession.ProductState).state : default);
        }

        void SyncFields()
        {
            ignoreUiCallbacks = true;
            try
            {
                bool seated = IsSeated;
                for (int i = 0; i < postureButtons.Count; i++)
                {
                    FoldableSimulatorPanelUi.StyleSelected(
                        postureButtons[i],
                        OwnsBoundDevice && Postures[i] == FoldableEditorSession.ProductState);
                }

                holdField.SetValueWithoutNotify(tallHold ? LaptopChoice : BookChoice);
                holdField.style.display = seated ? DisplayStyle.Flex : DisplayStyle.None;
                bool held = SeatedRotationLocked;
                holdField.SetEnabled(!held);
                holdField.tooltip = held ? HeldHoldTooltip : HoldTooltip;
                wideTallField.SetValueWithoutNotify(tallHold ? TallChoice : WideChoice);
                wideTallField.style.display = seated ? DisplayStyle.None : DisplayStyle.Flex;
                bool flatHeld = FlatRotationLocked;
                wideTallField.SetEnabled(!flatHeld);
                wideTallField.tooltip = flatHeld ? HeldFlatHoldTooltip : FlatHoldTooltip;

                autoToggle?.SetValueWithoutNotify(autoApplyInPlayMode);
                UpdateStatusLabels();
            }
            finally
            {
                ignoreUiCallbacks = false;
            }
        }

        /// <summary>
        /// Puts the Simulator screen on the shape the rules give the posture and hold (a fixed Default
        /// Orientation keeps its own shape).
        /// </summary>
        void SyncScreenToRules()
        {
            if (!OwnsBoundDevice)
                return;

            bool landscape = Outcome(FoldableEditorSession.ProductState).landscape;
            if (FoldableEditorSession.IsLandscape == landscape)
                return;

            FoldableEditorSession.PublishIosScreen(landscape, FoldableEditorSession.Source.Simulator);
            QueueOrientationSync();
        }

        void UpdateStatusLabels()
        {
            if (!OwnsBoundDevice)
            {
                boundLabel.text = "";
                statusLabel.text = "";
                ruleLabel.text = "";
                return;
            }

            boundLabel.text =
                $"{FoldableEditorSession.BoundFriendlyName} · {FoldableEditorSession.BoundScreenName} · " +
                $"{FoldableEditorSession.MetricsWidth}×{FoldableEditorSession.MetricsHeight}";

            string orientation = FoldableEditorSession.IsLandscape ? "Landscape" : "Portrait";
            ruleLabel.text = Outcome(FoldableEditorSession.ProductState).summary;
            switch (FoldableEditorSession.ProductState)
            {
                case FoldableProductState.Closed:
                    statusLabel.text = $"Closed · cover · held {Hold} · screen {orientation}";
                    break;
                case FoldableProductState.HalfOpened:
                    statusLabel.text = $"Seated {orientation} · {(tallHold ? "laptop (tall)" : "book (wide)")}";
                    break;
                default:
                    statusLabel.text = $"Opened · flat inner · held {Hold} · screen {orientation}";
                    break;
            }
        }

        string Hold => tallHold ? "tall" : "wide";

        void UpdateControlsVisibility()
        {
            bool show = !SimulatorDeviceBridge.IsSelectionPending && OwnsBoundDevice;
            if (controlsHost != null)
                controlsHost.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (unavailableLabel != null)
            {
                unavailableLabel.text = FoldableSimulatorPanelUi.UnavailableHint(
                    true, "", SelectDeviceHint, AndroidFoldHint);
                unavailableLabel.style.display = show ? DisplayStyle.None : DisplayStyle.Flex;
            }
        }

        #endregion

        #region Events

        void OnSessionChanged(FoldableEditorSession.Source source)
        {
            if (!OwnsBoundDevice)
            {
                SyncFieldsFromSession();
                UpdateControlsVisibility();
                return;
            }

            // The session's screen picks the hold when the two holds give different screens (Seated
            // laptop / book): rotating the Device Simulator changes the hold, and the render, hinge and
            // layout follow on the next tick. When they give the same one, rotating snaps back to the
            // rule's screen.
            IphoneDuoPosture posture = IphoneDuoHinge.ToPosture(FoldableEditorSession.ProductState);
            bool tallLandscape = IphoneDuoEditorRules.Resolve(posture, true).landscape;
            bool holdsDiffer = tallLandscape != IphoneDuoEditorRules.Resolve(posture, false).landscape;
            bool entering = IsSeated && !lastSeated;
            bool enteringFlat = IsFlat && !lastFlat;
            lastSeated = IsSeated;
            lastFlat = IsFlat;
            if (SeatedRotationLocked)
            {
                // Locked Seated keeps the Locked pose's hold; entering, the screen gives the entry hold.
                // A screen the hold does not give (a bridge publish) snaps back to it.
                bool entryTall = entering && holdsDiffer
                    ? FoldableEditorSession.IsLandscape == tallLandscape
                    : tallHold;
                tallHold = IphoneDuoEditorRules.SeatedHoldTall(entryTall, holdTall: tallHold);
                if (source == FoldableEditorSession.Source.Bridge)
                    SyncScreenToRules();
            }
            else if (FlatRotationLocked)
            {
                // Locked Flat keeps the Locked pose's hold the same way.
                bool entryTall = enteringFlat && holdsDiffer
                    ? FoldableEditorSession.IsLandscape == tallLandscape
                    : tallHold;
                tallHold = !IphoneDuoEditorRules.FlatHoldWide(entryWide: !entryTall, holdWide: !tallHold);
                if (source == FoldableEditorSession.Source.Bridge)
                    SyncScreenToRules();
            }
            else if (holdsDiffer)
            {
                bool tall = FoldableEditorSession.IsLandscape == tallLandscape;
                if (tall != tallHold && source == FoldableEditorSession.Source.Bridge)
                    QueueOrientationSync();
                tallHold = tall;
            }
            else if (source == FoldableEditorSession.Source.Bridge)
            {
                SyncScreenToRules();
            }

            SyncFieldsFromSession();
            UpdateControlsVisibility();
        }

        void OnRulesChanged()
        {
            if (CannotRunDelayedWork() || !OwnsBoundDevice)
                return;
            PublishRules(FoldableEditorSession.ProductState);
        }

        void OnDeviceChanged()
        {
            SimulatorDeviceBridge.RequestSelectionPublish();
            UpdateControlsVisibility();
        }

        void OnBridgeSelectionChanged()
        {
            if (CannotRunDelayedWork())
                return;
            if (!OwnsBoundDevice)
            {
                SyncFieldsFromSession();
                UpdateControlsVisibility();
                return;
            }

            SyncFieldsFromSession();
            SyncScreenToRules();
            LockHeldChrome();
            UpdateControlsVisibility();
        }

        /// <summary>
        /// Locked Seated or Flat: the session ignored the chrome turn
        /// (<see cref="FoldableEditorSession.IosHalfOpenHeldScreenLandscape"/>,
        /// <see cref="FoldableEditorSession.IosFlatHeldScreenLandscape"/>), so turn the chrome back to
        /// the held screen.
        /// </summary>
        void LockHeldChrome()
        {
            if (!SeatedRotationLocked && !FlatRotationLocked)
                return;
            if (!SimulatorDeviceBridge.GetActiveSelection(out var selection) || !selection.HasRotation)
                return;
            if (SimulatorDeviceBridge.IsLandscapeRotation(selection.RotationDegrees) == FoldableEditorSession.IsLandscape)
                return;
            QueueOrientationSync();
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
                ApplyAfterChange();
            };
        }

        void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredPlayMode)
                return;
            // Play Mode starts with a clean IphoneDuoDevice.State: publish the preview again.
            SyncFieldsFromSession();
            ApplyAfterChange();
        }

        void OnBeforeAssemblyReload()
        {
            destroyed = true;
            FoldableEditorSession.FlushPersistedSession();
        }

        bool CannotRunDelayedWork() =>
            destroyed || EditorApplication.isCompiling || EditorApplication.isUpdating;

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
