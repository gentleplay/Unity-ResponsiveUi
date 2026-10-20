using System;
using System.Collections.Generic;
using FDPlus.ResponsiveUI.Shared.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace FDPlus.Foldable.Editor
{
    /// <summary>
    /// Simplified foldable posture simulator.
    /// Open with Responsive UI → Devices → Posture Simulator.
    /// </summary>
    public class FoldablePostureSimulatorWindow : EditorWindow
    {
        #region Constants

        const string PrefDevice = "com.futuredigitalplus.foldandflip.posture.device";
        const string PrefScreen = "com.futuredigitalplus.foldandflip.posture.screen";

        #endregion

        #region Serialized

        [SerializeField] int deviceIndex;
        [SerializeField] int screenIndex;
        [SerializeField] FoldableState foldState = FoldableState.Flat;
        [SerializeField] FoldableOrientation hinge = FoldableOrientation.Horizontal;
        [SerializeField] float hingeNormalized = 0.5f;
        [SerializeField] int hingeThicknessPx = 24;
        [SerializeField] bool isSeparating = false;
        [SerializeField] bool autoApply = true;
        [SerializeField] bool syncSimulatorDevice = true;

        #endregion

        #region Runtime state

        Texture2D overlayTexture;
        Vector2 overlayBorder;
        Label boundLabel;
        VisualElement toolbar;
        PopupField<string> statePopup;

        #endregion

        #region Lifecycle

        [MenuItem(ResponsiveUiEditorMenus.PostureSimulator, false, ResponsiveUiEditorMenus.PriorityDevices + 2)]
        [MenuItem(ResponsiveUiEditorMenus.WindowPostureSimulator, false, ResponsiveUiEditorMenus.PriorityWindow + 2)]
        public static void Open()
        {
            // Prefer Simulator Play Mode view for resolution / chrome.
            try
            {
                PlayModeWindow.SetViewType(PlayModeWindow.PlayModeViewTypes.SimulatorView);
            }
            catch
            {
                // Ignore if Play Mode view cannot switch (e.g. no Game view open yet).
            }

            var window = GetWindow<FoldablePostureSimulatorWindow>();
            window.titleContent = new GUIContent("Posture Simulator");
            window.minSize = new Vector2(460, 520);
            window.Show();
        }

        void OnEnable()
        {
            // Recompile / Play Mode re-enable this window: read the session, never publish or switch
            // the Simulator here (a bound Duo would lose Seated, or another device would bind).
            titleContent = new GUIContent("Posture Simulator");
            deviceIndex = EditorPrefs.GetInt(PrefDevice, 0);
            screenIndex = EditorPrefs.GetInt(PrefScreen, 0);
            LoadBoundSelection();
            if (ClampIndices())
            {
                var device = FoldableDeviceCatalog.Devices[deviceIndex];
                var screen = device.Screens[screenIndex];
                LoadOverlay(device, screen);
            }
            BuildUi();
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        }

        void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredPlayMode ||
                !autoApply ||
                !FoldableEditorSession.IsFoldableDeviceBound ||
                !FoldablePostureSource.HasActiveOwner())
                return;

            FoldablePostureSource.ApplySimulatedLayout(FoldableEditorSession.BuildLayout());
        }

        #endregion

        #region GUI

        void BuildUi()
        {
            rootVisualElement.Clear();
            if (!ClampIndices())
            {
                rootVisualElement.Add(new Label(
                    "No device profiles with screens in the Fold & Flip catalog.")
                {
                    style = { whiteSpace = WhiteSpace.Normal, paddingLeft = 8, paddingTop = 8 }
                });
                return;
            }

            var root = new VisualElement { style = { flexGrow = 1, backgroundColor = new Color(0.12f, 0.12f, 0.12f) } };
            rootVisualElement.Add(root);

            toolbar = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    flexWrap = Wrap.Wrap,
                    paddingLeft = 6,
                    paddingRight = 6,
                    paddingTop = 4,
                    paddingBottom = 4,
                    backgroundColor = new Color(0.17f, 0.17f, 0.17f)
                }
            };
            root.Add(toolbar);

            toolbar.Add(new Label("Posture")
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    width = 64,
                    unityTextAlign = TextAnchor.MiddleLeft,
                    marginRight = 6
                }
            });

            var deviceNames = new List<string>();
            foreach (var d in FoldableDeviceCatalog.Devices)
                deviceNames.Add(d.FriendlyName);

            deviceIndex = Mathf.Clamp(deviceIndex, 0, Mathf.Max(0, deviceNames.Count - 1));
            var devicePopup = new PopupField<string>(deviceNames, deviceIndex);
            devicePopup.style.minWidth = 220;
            devicePopup.style.unityFontStyleAndWeight = FontStyle.Bold;
            devicePopup.style.color = new StyleColor(new Color(1f, 0.85f, 0.25f));
            devicePopup.tooltip = "Toolkit devices (★ Fold / · ★ Flip / · ★ Dual / — same as Device Simulator).";
            devicePopup.RegisterValueChangedCallback(evt =>
            {
                deviceIndex = deviceNames.IndexOf(evt.newValue);
                screenIndex = 0;
                EditorPrefs.SetInt(PrefDevice, deviceIndex);
                EditorPrefs.SetInt(PrefScreen, 0);
                ApplySelectedDeviceSize(applyDefaults: true);
                RefreshScreenPopup();
                ApplyFold();
                Repaint();
            });
            toolbar.Add(devicePopup);

            RefreshScreenPopup();

            statePopup = new PopupField<string>(
                new List<string> { "Flat", "HalfOpened" },
                foldState == FoldableState.HalfOpened ? 1 : 0);
            statePopup.style.width = 110;
            statePopup.RegisterValueChangedCallback(evt =>
            {
                foldState = evt.newValue == "HalfOpened" ? FoldableState.HalfOpened : FoldableState.Flat;
                PublishSelection(applyDefaults: false);
                ApplyFold();
                RefreshBoundLabel();
            });
            toolbar.Add(statePopup);

            toolbar.Add(Btn("Flat", () => SetFold(FoldableState.Flat, hinge, false)));
            toolbar.Add(Btn("Tabletop", () => SetFold(FoldableState.HalfOpened, FoldableOrientation.Horizontal, false)));
            toolbar.Add(Btn("Book", () => SetFold(FoldableState.HalfOpened, FoldableOrientation.Vertical, false)));

            var autoToggle = new Toggle("Auto apply") { value = autoApply };
            autoToggle.RegisterValueChangedCallback(evt =>
            {
                autoApply = evt.newValue;
                if (autoApply)
                    ApplyFold();
            });
            toolbar.Add(autoToggle);
            toolbar.Add(Btn("Apply", () => ApplyFold(force: true)));

            var syncToggle = new Toggle("Sync Simulator device list") { value = syncSimulatorDevice };
            syncToggle.tooltip = "When enabled, also try to keep Device Simulator on a matching foldable device.";
            syncToggle.RegisterValueChangedCallback(evt =>
            {
                syncSimulatorDevice = evt.newValue;
                if (syncSimulatorDevice)
                    SyncSimulatorSelection();
            });
            toolbar.Add(syncToggle);

            boundLabel = new Label { style = { marginLeft = 8, unityTextAlign = TextAnchor.MiddleLeft } };
            toolbar.Add(boundLabel);

            root.Add(new Label(
                "Simple Flat / HalfOpened posture preview. Applies to FoldablePostureDetector " +
                "in Edit or Play Mode. Use Device Simulator's Android Fold & Flip or iPhone Duo panel for " +
                "Closed / Normal / Expand product-state authoring.")
            {
                style =
                {
                    whiteSpace = WhiteSpace.Normal,
                    color = new StyleColor(new Color(0.72f, 0.72f, 0.72f)),
                    paddingLeft = 8,
                    paddingRight = 8,
                    paddingTop = 6,
                    paddingBottom = 4
                }
            });

            root.Add(new IMGUIContainer(DrawPreview) { style = { flexGrow = 1 } });
            RefreshBoundLabel();
        }

        void SetFold(FoldableState state, FoldableOrientation orientation, bool separating)
        {
            foldState = state;
            hinge = orientation;
            isSeparating = separating;
            statePopup?.SetValueWithoutNotify(state == FoldableState.HalfOpened ? "HalfOpened" : "Flat");
            PublishSelection(applyDefaults: false);
            ApplyFold();
            RefreshBoundLabel();
        }

        static Button Btn(string label, System.Action action) =>
            new Button(action) { text = label, style = { marginRight = 2 } };

        void RefreshScreenPopup()
        {
            var existing = toolbar.Q<VisualElement>("screen-popup-host");
            existing?.RemoveFromHierarchy();

            var host = new VisualElement { name = "screen-popup-host" };
            if (!ClampIndices())
                return;

            var device = FoldableDeviceCatalog.Devices[deviceIndex];
            var screenNames = new List<string>();
            foreach (var s in device.Screens)
                screenNames.Add(s.Name);

            screenIndex = Mathf.Clamp(screenIndex, 0, screenNames.Count - 1);
            var screenPopup = new PopupField<string>(screenNames, screenIndex);
            screenPopup.style.minWidth = 84;
            screenPopup.RegisterValueChangedCallback(evt =>
            {
                screenIndex = screenNames.IndexOf(evt.newValue);
                EditorPrefs.SetInt(PrefScreen, screenIndex);
                ApplySelectedDeviceSize(applyDefaults: false);
                ApplyFold();
                Repaint();
            });
            host.Add(screenPopup);
            toolbar.Insert(2, host);
        }

        #endregion

        #region Catalog

        /// <summary>Device / screen / fold of the bound session, when one is bound.</summary>
        void LoadBoundSelection()
        {
            if (!FoldableEditorSession.IsFoldableDeviceBound)
                return;

            var devices = FoldableDeviceCatalog.Devices;
            var bound = FoldableEditorSession.FindDeviceByFriendlyName(FoldableEditorSession.BoundFriendlyName);
            int index = devices == null || bound == null ? -1 : Array.IndexOf(devices, bound);
            if (index < 0)
                return;

            deviceIndex = index;
            screenIndex = bound.Screens == null
                ? 0
                : Mathf.Max(0, Array.FindIndex(
                    bound.Screens, s => s.Name == FoldableEditorSession.BoundScreenName));
            foldState = FoldableEditorSession.State;
            hinge = FoldableEditorSession.Orientation;
            hingeNormalized = FoldableEditorSession.HingeNormalized;
            hingeThicknessPx = FoldableEditorSession.HingeThicknessPx;
            isSeparating = FoldableEditorSession.IsSeparating;
        }

        /// <summary>
        /// Clamps the device / screen selection to the catalog. False when there is no device or
        /// the selected device has no screens (nothing to index).
        /// </summary>
        bool ClampIndices()
        {
            var devices = FoldableDeviceCatalog.Devices;
            if (devices == null || devices.Length == 0)
                return false;

            deviceIndex = Mathf.Clamp(deviceIndex, 0, devices.Length - 1);
            var screens = devices[deviceIndex].Screens;
            if (screens == null || screens.Length == 0)
            {
                screenIndex = 0;
                return false;
            }

            screenIndex = Mathf.Clamp(screenIndex, 0, screens.Length - 1);
            return true;
        }

        void ApplySelectedDeviceSize(bool applyDefaults)
        {
            if (!ClampIndices())
                return;
            var device = FoldableDeviceCatalog.Devices[deviceIndex];
            var screen = device.Screens[screenIndex];

            if (applyDefaults)
            {
                FoldableDeviceCatalog.ApplyDeviceDefaults(
                    new FoldableDeviceCatalog.Resolution
                    {
                        Device = device,
                        Screen = screen,
                        IsKnownFoldable = true
                    },
                    ref foldState, ref hinge, ref hingeNormalized, ref hingeThicknessPx, ref isSeparating);
                statePopup?.SetValueWithoutNotify(foldState == FoldableState.HalfOpened ? "HalfOpened" : "Flat");
            }

            LoadOverlay(device, screen);
            PublishSelection(applyDefaults);
            if (syncSimulatorDevice)
                SyncSimulatorSelection();
            RefreshBoundLabel();
        }

        void PublishSelection(bool applyDefaults)
        {
            if (!ClampIndices())
                return;
            var device = FoldableDeviceCatalog.Devices[deviceIndex];
            var screen = device.Screens[screenIndex];
            FoldableEditorSession.SetDeviceBinding(
                new FoldableDeviceCatalog.Resolution
                {
                    Device = device,
                    Screen = screen,
                    IsKnownFoldable = true
                },
                FoldableEditorSession.Source.Posture,
                applyDefaults);

            var product = foldState == FoldableState.HalfOpened
                ? FoldableProductState.HalfOpened
                : screen.CoverOrOuter
                    ? FoldableProductState.Closed
                    : FoldableProductState.Opened;
            // iPhone Duo Seated: the hold names the screen shape (book = Landscape, laptop = Portrait).
            bool? landscape = FoldableProductStateResolver.IsLandscapeNativeHalfOpen(
                product, device.HasLandscapeNativeInner)
                ? hinge == FoldableOrientation.Vertical
                : (bool?)null;
            FoldableEditorSession.PublishProductState(
                product,
                hinge,
                hingeNormalized,
                hingeThicknessPx,
                isSeparating,
                screen.Width,
                screen.Height,
                FoldableEditorSession.Source.Posture,
                landscape);
        }

        void SyncSimulatorSelection()
        {
            if (!ClampIndices())
                return;
            var device = FoldableDeviceCatalog.Devices[deviceIndex];
            SimulatorDeviceBridge.SelectToolkitDevice(device.FriendlyName, screenIndex);
            FoldableEditorSessionWatcher.RefreshNow();
        }

        #endregion

        #region Skins / overlays

        void LoadOverlay(
            FoldableDeviceCatalog.DeviceProfile device,
            FoldableDeviceCatalog.ScreenProfile screen)
        {
            FoldableDeviceOverlayUtil.Load(
                device,
                screen.Name,
                screen.Width,
                screen.Height,
                out overlayTexture,
                out overlayBorder);
        }

        #endregion

        #region GUI

        void RefreshBoundLabel()
        {
            if (boundLabel == null || !ClampIndices())
                return;
            var s = FoldableDeviceCatalog.Devices[deviceIndex].Screens[screenIndex];
            boundLabel.text = $"{s.Width}×{s.Height} · {foldState}";
        }

        void DrawPreview()
        {
            var rect = GUILayoutUtility.GetRect(10, 10, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            if (Event.current.type != EventType.Repaint)
                return;

            EditorGUI.DrawRect(rect, new Color(0.09f, 0.09f, 0.09f));
            if (!ClampIndices())
                return;
            var screen = FoldableDeviceCatalog.Devices[deviceIndex].Screens[screenIndex];
            float screenW = screen.Width;
            float screenH = screen.Height;
            float borderX = overlayBorder.x;
            float borderY = overlayBorder.y;
            float totalW = screenW + borderX * 2f;
            float totalH = screenH + borderY * 2f;
            if (totalW < 1f || totalH < 1f)
                return;

            float scale = Mathf.Clamp(Mathf.Min(rect.width / totalW, rect.height / totalH) * 0.92f, 0.05f, 2f);
            float drawW = totalW * scale;
            float drawH = totalH * scale;
            var deviceRect = new Rect(
                rect.x + (rect.width - drawW) * 0.5f,
                rect.y + (rect.height - drawH) * 0.5f,
                drawW,
                drawH);
            var gameRect = new Rect(
                deviceRect.x + borderX * scale,
                deviceRect.y + borderY * scale,
                screenW * scale,
                screenH * scale);

            EditorGUI.DrawRect(gameRect, new Color(0.16f, 0.18f, 0.22f));
            var posture = FoldableLayoutState.CreateSimulated(
                foldState, hinge, screen.Width, screen.Height, hingeNormalized, hingeThicknessPx, isSeparating)
                .ResolvePosture();
            GUI.Label(gameRect,
                $"\n{FoldableDeviceCatalog.Devices[deviceIndex].FriendlyName}\n{screen.Name}\n{posture}\n{(EditorApplication.isPlaying ? "Play Mode · applying to posture owner" : "Edit Mode preview")}",
                EditorStyles.centeredGreyMiniLabel);

            // Schematic split overlay inside the screen area
            DrawPostureSchematic(gameRect, posture, screen);

            if (overlayTexture != null)
                GUI.DrawTexture(deviceRect, overlayTexture, ScaleMode.StretchToFill, true);
        }

        void DrawPostureSchematic(Rect gameRect, FoldablePosture posture, FoldableDeviceCatalog.ScreenProfile screen)
        {
            if (posture == FoldablePosture.Flat)
                return;

            var hingeColor = new Color(1f, 0.85f, 0.2f, 0.55f);
            var paneColor = new Color(0.25f, 0.55f, 0.35f, 0.35f);

            if (posture == FoldablePosture.Book ||
                (posture == FoldablePosture.DualPane && hinge == FoldableOrientation.Vertical))
            {
                float x = gameRect.x + gameRect.width * hingeNormalized;
                EditorGUI.DrawRect(new Rect(gameRect.x, gameRect.y, x - gameRect.x, gameRect.height), paneColor);
                EditorGUI.DrawRect(new Rect(x - 2, gameRect.y, 4, gameRect.height), hingeColor);
            }
            else
            {
                float yFromBottom = gameRect.height * hingeNormalized;
                float y = gameRect.yMax - yFromBottom;
                EditorGUI.DrawRect(new Rect(gameRect.x, y, gameRect.width, gameRect.yMax - y), paneColor);
                EditorGUI.DrawRect(new Rect(gameRect.x, y - 2, gameRect.width, 4), hingeColor);
            }
        }

        #endregion

        #region Apply

        void ApplyFold(bool force = false)
        {
            RefreshBoundLabel();
            if (!autoApply && !force)
                return;

            if (!FoldablePostureSource.HasActiveOwner())
                return;

            // Pushes the session the user actions published (PublishSelection); never re-publishes.
            FoldablePostureSource.SetEditorDeviceKind(FoldableEditorSession.DeviceKind);
            FoldableLayoutState layout = FoldableEditorSession.BuildLayout();
            if (EditorApplication.isPlaying)
                FoldablePostureSource.ApplySimulatedLayout(layout);
            else
                FoldablePostureSource.SyncSimulatedLayoutState(layout);
        }

        #endregion
    }
}
