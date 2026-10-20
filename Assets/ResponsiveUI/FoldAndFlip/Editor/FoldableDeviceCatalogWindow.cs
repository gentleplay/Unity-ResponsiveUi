using System;
using System.Collections.Generic;
using FDPlus.ResponsiveUI.Shared.Editor;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace FDPlus.Foldable.Editor
{
    /// <summary>
    /// View / edit Fold &amp; Flip device profiles with a resizable live preview.
    /// Layout: Devices | Editor | Preview (drag splitters to resize).
    /// Profile fields stay locked until Edit; Save writes JSON and locks again.
    /// </summary>
    public class FoldableDeviceCatalogWindow : EditorWindow
    {
        #region Constants

        const string PrefListWidth = "com.futuredigitalplus.foldandflip.catalog.listWidth";
        const string PrefPreviewWidth = "com.futuredigitalplus.foldandflip.catalog.previewWidth";
        const string PrefPreviewLandscape = "com.futuredigitalplus.foldandflip.catalog.previewLandscape";
        const string PrefSelectedId = "com.futuredigitalplus.foldandflip.catalog.selectedId";

        static readonly string[] CategoryFilters = { "All", "Fold", "Flip", "Dual" };
        static readonly string[] KindLabels = { "Foldable phone", "Flipable phone", "Dual-screen" };
        static readonly string[] VerifiedLabels = { "matched", "approx", "hybrid" };
        static readonly string[] HingeLabels = { "Horizontal", "Vertical" };

        #endregion

        #region Runtime state

        string search = "";
        int categoryFilter;
        string selectedId;
        FoldableDeviceCatalog.EditableProfile draft;
        bool dirty;
        bool editing;
        string status = "";
        int screenEditIndex;
        bool previewLandscape;
        Vector2 formScroll;

        Texture2D overlayTexture;
        Vector2 overlayBorder;
        int overlayScreenW;
        int overlayScreenH;

        TwoPaneSplitView rootSplit;
        TwoPaneSplitView editorSplit;
        ListView deviceList;
        Label headerTitle;
        Label headerMeta;
        Label statusLabel;
        Label previewTitle;
        Label previewMetrics;
        IMGUIContainer formHost;
        IMGUIContainer previewHost;
        ToolbarToggle landscapeToggle;
        PopupField<string> screenPopup;
        Button editButton;
        Button cancelButton;
        Button saveButton;
        Button useButton;
        Button simulatorButton;

        #endregion

        #region Lifecycle

        [MenuItem(ResponsiveUiEditorMenus.FoldFlipCatalog, false, ResponsiveUiEditorMenus.PriorityDevices + 1)]
        [MenuItem(ResponsiveUiEditorMenus.WindowFoldFlipCatalog, false, ResponsiveUiEditorMenus.PriorityWindow + 1)]
        public static void Open()
        {
            var window = GetWindow<FoldableDeviceCatalogWindow>();
            window.titleContent = new GUIContent("Device Catalog");
            window.minSize = new Vector2(980, 560);
            window.Show();
        }

        void OnEnable()
        {
            selectedId = EditorPrefs.GetString(PrefSelectedId, "");
            previewLandscape = EditorPrefs.GetBool(PrefPreviewLandscape, false);
            FoldableDeviceCatalog.CatalogChanged += OnCatalogChanged;
            FoldableEditorSession.Changed += OnSessionChanged;
            BuildUi();
            EnsureSelection();

            // .device.off files that still carry Device ScriptedImporter metas show as blank
            // Simulator entries and can crash on select (IndexOutOfRangeException).
            int scrubbed = FoldableDeviceCatalog.CleanupDisabledDeviceGhostMetas();
            if (scrubbed > 0)
            {
                status = $"Scrubbed {scrubbed} disabled skin meta(s).";
                SimulatorDeviceBridge.ForceReloadDeviceLists();
            }

            RefreshAll();
        }

        void OnDisable()
        {
            FoldableDeviceCatalog.CatalogChanged -= OnCatalogChanged;
            FoldableEditorSession.Changed -= OnSessionChanged;
            PersistSplitSizes();
            if (!string.IsNullOrEmpty(selectedId))
                EditorPrefs.SetString(PrefSelectedId, selectedId);
            EditorPrefs.SetBool(PrefPreviewLandscape, previewLandscape);
        }

        void OnCatalogChanged()
        {
            if (!dirty)
            {
                editing = false;
                LoadSelectedIntoDraft();
            }

            RefreshAll();
        }

        void OnSessionChanged(FoldableEditorSession.Source _) => RefreshPreviewOnly();

        #endregion

        #region GUI

        void PersistSplitSizes()
        {
            if (rootSplit != null && rootSplit.childCount > 0)
                EditorPrefs.SetFloat(PrefListWidth, rootSplit[0].resolvedStyle.width);
            if (editorSplit != null && editorSplit.childCount > 1)
                EditorPrefs.SetFloat(PrefPreviewWidth, editorSplit[1].resolvedStyle.width);
        }

        void BuildUi()
        {
            rootVisualElement.Clear();
            rootVisualElement.style.flexGrow = 1;

            var root = new VisualElement
            {
                style =
                {
                    flexGrow = 1,
                    backgroundColor = new Color(0.15f, 0.15f, 0.15f)
                }
            };
            rootVisualElement.Add(root);

            root.Add(BuildToolbar());

            float listW = Mathf.Clamp(EditorPrefs.GetFloat(PrefListWidth, 240f), 180f, 420f);
            float previewW = Mathf.Clamp(EditorPrefs.GetFloat(PrefPreviewWidth, 320f), 220f, 640f);

            rootSplit = new TwoPaneSplitView(0, listW, TwoPaneSplitViewOrientation.Horizontal);
            rootSplit.style.flexGrow = 1;
            root.Add(rootSplit);

            rootSplit.Add(BuildListPane());

            editorSplit = new TwoPaneSplitView(1, previewW, TwoPaneSplitViewOrientation.Horizontal);
            editorSplit.style.flexGrow = 1;
            rootSplit.Add(editorSplit);

            editorSplit.Add(BuildEditorPane());
            editorSplit.Add(BuildPreviewPane());
        }

        VisualElement BuildToolbar()
        {
            var bar = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    height = 28,
                    paddingLeft = 6,
                    paddingRight = 6,
                    backgroundColor = new Color(0.18f, 0.18f, 0.18f),
                    borderBottomWidth = 1,
                    borderBottomColor = new Color(0.1f, 0.1f, 0.1f)
                }
            };

            var reloadButton = MakeToolbarButton("Reload", () =>
            {
                if (!ConfirmDiscardIfDirty())
                    return;
                FoldableDeviceCatalog.Reload();
                ExitEditAndReloadDraft("Reloaded profiles.");
            });
            reloadButton.tooltip = "Reload all device profiles from disk";
            bar.Add(reloadButton);

            var spacer = new VisualElement { style = { flexGrow = 1 } };
            bar.Add(spacer);

            statusLabel = new Label("")
            {
                style =
                {
                    color = new Color(0.7f, 0.7f, 0.7f),
                    fontSize = 11,
                    unityTextAlign = TextAnchor.MiddleRight,
                    marginRight = 4
                }
            };
            bar.Add(statusLabel);
            return bar;
        }

        static Button MakeToolbarButton(string text, Action onClick)
        {
            var b = new Button(onClick) { text = text };
            b.style.marginLeft = 2;
            b.style.marginRight = 2;
            b.style.height = 22;
            b.style.minWidth = 72;
            b.style.unityTextAlign = TextAnchor.MiddleCenter;
            return b;
        }

        VisualElement BuildListPane()
        {
            var pane = new VisualElement
            {
                name = "list-pane",
                style =
                {
                    flexGrow = 1,
                    flexShrink = 1,
                    minWidth = 180,
                    minHeight = 0,
                    backgroundColor = new Color(0.17f, 0.17f, 0.17f),
                    paddingTop = 6,
                    paddingBottom = 6,
                    paddingLeft = 6,
                    paddingRight = 6
                }
            };

            var title = new Label("Devices")
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    marginBottom = 4,
                    fontSize = 12,
                    flexShrink = 0
                }
            };
            pane.Add(title);

            var searchField = new ToolbarSearchField { value = search };
            searchField.style.marginBottom = 4;
            searchField.style.flexShrink = 0;
            searchField.RegisterValueChangedCallback(evt =>
            {
                string nextSearch = evt.newValue ?? "";
                if (dirty)
                {
                    searchField.SetValueWithoutNotify(search);
                    ShowFinishEditingStatus();
                    return;
                }

                search = nextSearch;
                RefreshAfterFilterChanged();
            });
            pane.Add(searchField);

            var filterRow = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    marginBottom = 4,
                    flexShrink = 0,
                    minHeight = 22,
                    height = 22,
                    overflow = Overflow.Hidden
                }
            };
            for (int i = 0; i < CategoryFilters.Length; i++)
            {
                int idx = i;
                var toggle = new ToolbarToggle { text = CategoryFilters[i], value = categoryFilter == i };
                toggle.style.flexGrow = 1;
                toggle.style.flexShrink = 1;
                toggle.style.height = 22;
                toggle.style.marginTop = 0;
                toggle.style.marginBottom = 0;
                toggle.style.marginLeft = 0;
                toggle.style.marginRight = 0;
                toggle.style.unityTextAlign = TextAnchor.MiddleCenter;
                toggle.RegisterValueChangedCallback(evt =>
                {
                    if (!evt.newValue)
                    {
                        toggle.SetValueWithoutNotify(categoryFilter == idx);
                        return;
                    }
                    if (dirty)
                    {
                        toggle.SetValueWithoutNotify(categoryFilter == idx);
                        ShowFinishEditingStatus();
                        return;
                    }

                    categoryFilter = idx;
                    RefreshFilterToggles(filterRow, idx);
                    RefreshAfterFilterChanged();
                });
                filterRow.Add(toggle);
            }

            pane.Add(filterRow);

            deviceList = new ListView
            {
                fixedItemHeight = 22,
                selectionType = SelectionType.Single,
                style =
                {
                    flexGrow = 1,
                    flexShrink = 1,
                    minHeight = 0,
                    marginBottom = 6
                }
            };
            deviceList.makeItem = () =>
            {
                var row = new Label();
                row.style.paddingLeft = 6;
                row.style.unityTextAlign = TextAnchor.MiddleLeft;
                return row;
            };
            deviceList.bindItem = (el, i) =>
            {
                var items = FilteredDevices();
                if (i < 0 || i >= items.Count)
                    return;
                var d = items[i];
                var label = (Label)el;
                bool designRef = FoldableDeviceCatalog.IsDesignCanvasReference(d.Id);
                string text = designRef
                    ? FoldableDeviceCatalog.DesignReferenceMarker + d.ShortName
                    : d.ShortName;
                if (!string.IsNullOrEmpty(d.VerifiedStatus) &&
                    !d.VerifiedStatus.Equals("matched", StringComparison.OrdinalIgnoreCase))
                    text += $"  · {d.VerifiedStatus}";
                label.text = text;
                label.tooltip = d.FriendlyName + "\n" + d.Id +
                                "\nDevice Simulator: " +
                                (d.SimulatorEnabled ? "enabled" : "disabled") +
                                (designRef ? "\n◆ Design Canvas reference (Project Settings → Responsive UI)" : "");
                label.style.backgroundColor = designRef
                    ? new Color(0.25f, 0.42f, 0.28f, 0.55f)
                    : StyleKeyword.Null;
            };
            deviceList.selectionChanged += OnListSelectionChanged;
            pane.Add(deviceList);

            var actions = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    flexShrink = 0,
                    minHeight = 26,
                    height = 26,
                    alignItems = Align.Center
                }
            };
            var addButton = MakeToolbarButton("Add", AddDevice);
            addButton.tooltip = "Create a new device profile";
            addButton.style.flexGrow = 1;
            addButton.style.marginTop = 0;
            addButton.style.marginBottom = 0;
            actions.Add(addButton);

            var duplicateButton = MakeToolbarButton("Duplicate", DuplicateSelected);
            duplicateButton.tooltip = "Duplicate the selected device profile and its available skins";
            duplicateButton.style.flexGrow = 1;
            duplicateButton.style.marginTop = 0;
            duplicateButton.style.marginBottom = 0;
            actions.Add(duplicateButton);

            var removeButton = MakeToolbarButton("Remove", RemoveSelected);
            removeButton.tooltip = "Remove the selected device profile";
            removeButton.style.flexGrow = 1;
            removeButton.style.marginTop = 0;
            removeButton.style.marginBottom = 0;
            actions.Add(removeButton);
            pane.Add(actions);

            return pane;
        }

        static void RefreshFilterToggles(VisualElement row, int active)
        {
            int i = 0;
            row.Query<ToolbarToggle>().ForEach(t =>
            {
                t.SetValueWithoutNotify(i == active);
                i++;
            });
        }

        VisualElement BuildEditorPane()
        {
            var pane = new VisualElement
            {
                name = "editor-pane",
                style =
                {
                    minWidth = 320,
                    flexGrow = 1,
                    paddingTop = 8,
                    paddingLeft = 10,
                    paddingRight = 8,
                    backgroundColor = new Color(0.19f, 0.19f, 0.19f)
                }
            };

            headerTitle = new Label("—")
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    fontSize = 14,
                    marginBottom = 2
                }
            };
            pane.Add(headerTitle);

            headerMeta = new Label("")
            {
                style =
                {
                    color = new Color(0.65f, 0.65f, 0.65f),
                    fontSize = 11,
                    marginBottom = 8
                }
            };
            pane.Add(headerMeta);

            pane.Add(BuildProfileActions());

            formHost = new IMGUIContainer(DrawFormGui)
            {
                style = { flexGrow = 1 }
            };
            pane.Add(formHost);
            return pane;
        }

        VisualElement BuildProfileActions()
        {
            var actions = new VisualElement
            {
                name = "profile-actions",
                style =
                {
                    flexDirection = FlexDirection.Row,
                    flexWrap = Wrap.Wrap,
                    marginBottom = 8
                }
            };

            editButton = MakeToolbarButton("Edit Profile", EnterEditMode);
            editButton.tooltip = "Unlock the selected profile for editing";
            actions.Add(editButton);

            cancelButton = MakeToolbarButton("Cancel", CancelEditMode);
            cancelButton.tooltip = "Discard edits to the selected profile";
            actions.Add(cancelButton);

            saveButton = MakeToolbarButton("Save Profile", SaveDraft);
            saveButton.tooltip = "Write the selected profile JSON and sync its .device sizes";
            actions.Add(saveButton);

            useButton = MakeToolbarButton("Use in Simulator", UseInSimulator);
            useButton.tooltip = "Select this profile and the previewed screen in Device Simulator";
            useButton.style.minWidth = 120;
            actions.Add(useButton);

            simulatorButton = MakeToolbarButton("Enable in Simulator", ToggleSimulatorEnabled);
            simulatorButton.tooltip =
                "Show or hide this profile in Unity Device Simulator (.device ↔ .device.off)";
            simulatorButton.style.minWidth = 140;
            actions.Add(simulatorButton);

            return actions;
        }

        VisualElement BuildPreviewPane()
        {
            var pane = new VisualElement
            {
                name = "preview-pane",
                style =
                {
                    minWidth = 220,
                    backgroundColor = new Color(0.12f, 0.12f, 0.12f),
                    borderLeftWidth = 1,
                    borderLeftColor = new Color(0.08f, 0.08f, 0.08f)
                }
            };

            var header = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    height = 28,
                    paddingLeft = 8,
                    paddingRight = 6,
                    backgroundColor = new Color(0.16f, 0.16f, 0.16f),
                    borderBottomWidth = 1,
                    borderBottomColor = new Color(0.08f, 0.08f, 0.08f)
                }
            };

            previewTitle = new Label("Preview")
            {
                style = { unityFontStyleAndWeight = FontStyle.Bold, flexGrow = 1, minWidth = 56 }
            };
            header.Add(previewTitle);

            screenPopup = new PopupField<string>(new List<string> { "—" }, 0);
            screenPopup.style.minWidth = 96;
            screenPopup.style.maxWidth = 140;
            screenPopup.tooltip = "Preview screen (Cover / Inner / …)";
            screenPopup.RegisterValueChangedCallback(evt =>
            {
                if (draft == null)
                    return;
                int idx = draft.Screens.FindIndex(s => s.Name == evt.newValue);
                if (idx < 0)
                    return;
                screenEditIndex = idx;
                RefreshPreviewOnly();
                formHost?.MarkDirtyRepaint();
            });
            header.Add(screenPopup);

            landscapeToggle = new ToolbarToggle
            {
                text = previewLandscape ? "Landscape" : "Portrait",
                value = previewLandscape
            };
            landscapeToggle.tooltip = "Preview portrait or landscape metrics";
            landscapeToggle.RegisterValueChangedCallback(evt =>
            {
                previewLandscape = evt.newValue;
                landscapeToggle.text = previewLandscape ? "Landscape" : "Portrait";
                EditorPrefs.SetBool(PrefPreviewLandscape, previewLandscape);
                RefreshPreviewOnly();
            });
            header.Add(landscapeToggle);

            pane.Add(header);

            previewMetrics = new Label("")
            {
                style =
                {
                    paddingLeft = 8,
                    paddingTop = 4,
                    paddingBottom = 2,
                    color = new Color(0.75f, 0.85f, 0.75f),
                    fontSize = 11
                }
            };
            pane.Add(previewMetrics);

            var hint = new Label("Drag the splitter to resize the preview")
            {
                style =
                {
                    paddingLeft = 8,
                    paddingBottom = 4,
                    color = new Color(0.5f, 0.5f, 0.5f),
                    fontSize = 10
                }
            };
            pane.Add(hint);

            previewHost = new IMGUIContainer(DrawPreviewGui)
            {
                style = { flexGrow = 1, minHeight = 160 }
            };
            pane.Add(previewHost);
            return pane;
        }

        #endregion

        #region Catalog

        void OnListSelectionChanged(IEnumerable<object> _)
        {
            var items = FilteredDevices();
            int idx = deviceList.selectedIndex;
            if (idx < 0 || idx >= items.Count)
                return;
            SelectDevice(items[idx].Id);
        }

        List<FoldableDeviceCatalog.DeviceProfile> FilteredDevices()
        {
            var list = new List<FoldableDeviceCatalog.DeviceProfile>();
            foreach (var d in FoldableDeviceCatalog.Devices)
            {
                if (!PassesFilter(d))
                    continue;
                list.Add(d);
            }

            return list;
        }

        bool PassesFilter(FoldableDeviceCatalog.DeviceProfile d)
        {
            if (categoryFilter == 1 &&
                !string.Equals(d.ListCategory, "Fold", StringComparison.OrdinalIgnoreCase) &&
                d.Kind != FoldableDeviceKind.FoldablePhone)
                return false;
            if (categoryFilter == 2 &&
                !string.Equals(d.ListCategory, "Flip", StringComparison.OrdinalIgnoreCase) &&
                d.Kind != FoldableDeviceKind.FlipablePhone)
                return false;
            if (categoryFilter == 3 &&
                !string.Equals(d.ListCategory, "Dual", StringComparison.OrdinalIgnoreCase) &&
                d.Kind != FoldableDeviceKind.DualScreen)
                return false;

            if (string.IsNullOrEmpty(search))
                return true;

            return (d.ShortName != null &&
                    d.ShortName.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0) ||
                   (d.Id != null && d.Id.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0) ||
                   (d.BasedOn != null &&
                    d.BasedOn.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        void EnsureSelection()
        {
            var devices = FoldableDeviceCatalog.Devices;
            if (devices == null || devices.Length == 0)
            {
                selectedId = null;
                draft = null;
                return;
            }

            if (string.IsNullOrEmpty(selectedId) || FoldableDeviceCatalog.FindById(selectedId) == null)
            {
                string bound = FoldableEditorSession.BoundFriendlyName;
                FoldableDeviceCatalog.DeviceProfile match = null;
                if (!string.IsNullOrEmpty(bound))
                {
                    foreach (var d in devices)
                    {
                        if (FoldableDeviceCatalog.NamesMatch(d.FriendlyName, bound))
                        {
                            match = d;
                            break;
                        }
                    }
                }

                selectedId = (match ?? devices[0]).Id;
            }

            if (!dirty)
                LoadSelectedIntoDraft();
        }

        void LoadSelectedIntoDraft()
        {
            var p = FoldableDeviceCatalog.FindById(selectedId);
            draft = p != null ? FoldableDeviceCatalog.EditableProfile.FromProfile(p) : null;
            dirty = false;
            screenEditIndex = 0;
            ReloadOverlay();
        }

        void SelectDevice(string id)
        {
            if (id == selectedId)
                return;
            if (!ConfirmDiscardIfDirty())
            {
                SyncListSelectionToSelectedId();
                return;
            }

            selectedId = id;
            EditorPrefs.SetString(PrefSelectedId, selectedId ?? "");
            editing = false;
            LoadSelectedIntoDraft();
            status = "";
            RefreshAll();
        }

        bool ConfirmDiscardIfDirty()
        {
            if (!dirty)
                return true;
            return EditorUtility.DisplayDialog(
                "Unsaved changes",
                "Discard unsaved edits to the current device?",
                "Discard",
                "Cancel");
        }

        void EnterEditMode()
        {
            if (draft == null || editing)
                return;
            editing = true;
            status = "Editing — change fields, then Save or Cancel.";
            RefreshAll();
        }

        void CancelEditMode()
        {
            if (!editing)
                return;
            if (!ConfirmDiscardIfDirty())
                return;

            ExitEditAndReloadDraft(
                IsDraftMissingFromCatalog() ? "New profile discarded." : "Edit cancelled.");
        }

        /// <summary>
        /// True when the current draft was created with Add and has never been saved.
        /// </summary>
        bool IsDraftMissingFromCatalog()
        {
            return draft != null && FoldableDeviceCatalog.FindById(draft.Id) == null;
        }

        /// <summary>
        /// Leave edit mode, clear dirty, and reload a catalog profile.
        /// Unsaved new drafts fall back to the last persisted selection.
        /// </summary>
        void ExitEditAndReloadDraft(string statusMessage)
        {
            bool restorePrevious = IsDraftMissingFromCatalog();
            editing = false;
            dirty = false;

            if (restorePrevious)
            {
                selectedId = EditorPrefs.GetString(PrefSelectedId, "");
                if (string.IsNullOrEmpty(selectedId) || FoldableDeviceCatalog.FindById(selectedId) == null)
                {
                    selectedId = null;
                    draft = null;
                    EnsureSelection();
                }
                else
                {
                    LoadSelectedIntoDraft();
                }
            }
            else
            {
                LoadSelectedIntoDraft();
            }

            status = statusMessage ?? "";
            RefreshAll();
        }

        void UpdateEditToolbar()
        {
            bool hasDraft = draft != null;
            editButton.SetEnabled(hasDraft && !editing);
            editButton.style.display = editing ? DisplayStyle.None : DisplayStyle.Flex;

            cancelButton.SetEnabled(hasDraft && editing);
            cancelButton.style.display = editing ? DisplayStyle.Flex : DisplayStyle.None;

            // Allow Save whenever editing so new blank profiles can be persisted immediately.
            saveButton.SetEnabled(hasDraft && editing);
            saveButton.style.display = editing ? DisplayStyle.Flex : DisplayStyle.None;

            useButton.SetEnabled(hasDraft && !IsDraftMissingFromCatalog());

            bool canToggleSim = hasDraft && !IsDraftMissingFromCatalog();
            var live = canToggleSim ? FoldableDeviceCatalog.FindById(selectedId) : null;
            bool simOn = live != null && live.SimulatorEnabled;
            simulatorButton.SetEnabled(canToggleSim && !editing && live != null &&
                                       !string.IsNullOrEmpty(live.DeviceFile));
            simulatorButton.text = simOn ? "Disable in Simulator" : "Enable in Simulator";
        }

        void ToggleSimulatorEnabled()
        {
            if (string.IsNullOrEmpty(selectedId) || IsDraftMissingFromCatalog())
            {
                status = "Save the profile before changing Simulator visibility.";
                RefreshAll();
                return;
            }

            if (dirty || editing)
            {
                status = "Save or Cancel profile edits before changing Simulator visibility.";
                RefreshAll();
                return;
            }

            var live = FoldableDeviceCatalog.FindById(selectedId);
            if (live == null)
                return;

            bool enable = !live.SimulatorEnabled;
            if (!FoldableDeviceCatalog.SetSimulatorEnabled(selectedId, enable, out string error))
            {
                EditorUtility.DisplayDialog("Device Simulator", error, "OK");
                status = "Simulator toggle failed.";
                RefreshAll();
                return;
            }

            LoadSelectedIntoDraft();
            status = enable
                ? "Enabled in Device Simulator (reopen Simulator if the list is open)."
                : "Disabled in Device Simulator (.device → .device.off).";
            RefreshAll();
        }

        #endregion

        #region GUI

        void RefreshAll()
        {
            RefreshDeviceList();
            RefreshHeader();
            RefreshScreenMenu();
            ReloadOverlay();
            UpdateEditToolbar();
            formHost?.MarkDirtyRepaint();
            previewHost?.MarkDirtyRepaint();
            if (statusLabel != null)
                statusLabel.text = status ?? "";
        }

        void RefreshPreviewOnly()
        {
            ReloadOverlay();
            RefreshScreenMenu();
            previewHost?.MarkDirtyRepaint();
            if (previewMetrics != null && draft != null && draft.Screens.Count > 0)
            {
                var s = draft.Screens[Mathf.Clamp(screenEditIndex, 0, draft.Screens.Count - 1)];
                s.NormalizePairs();
                int w = previewLandscape ? s.LandscapeWidth : s.PortraitWidth;
                int h = previewLandscape ? s.LandscapeHeight : s.PortraitHeight;
                previewMetrics.text = $"{s.Name}  ·  {w}×{h}  ·  {(previewLandscape ? "Landscape" : "Portrait")}";
            }
        }

        void RefreshDeviceList()
        {
            if (deviceList == null)
                return;
            var items = FilteredDevices();
            deviceList.itemsSource = items;
            deviceList.Rebuild();
            SyncListSelectionToSelectedId();
        }

        void RefreshAfterFilterChanged()
        {
            var items = FilteredDevices();
            if (items.Count == 0)
            {
                selectedId = null;
                draft = null;
                editing = false;
            }
            else if (items.FindIndex(d => d.Id == selectedId) < 0)
            {
                selectedId = items[0].Id;
                EditorPrefs.SetString(PrefSelectedId, selectedId);
                editing = false;
                LoadSelectedIntoDraft();
            }

            status = items.Count == 0 ? "No device profiles match the current filters." : "";
            RefreshAll();
        }

        void ShowFinishEditingStatus()
        {
            status = "Save or Cancel profile edits before changing filters.";
            statusLabel.text = status;
        }

        void SyncListSelectionToSelectedId()
        {
            if (deviceList == null)
                return;
            var items = FilteredDevices();
            int idx = items.FindIndex(d => d.Id == selectedId);
            deviceList.SetSelectionWithoutNotify(idx >= 0 ? new List<int> { idx } : new List<int>());
        }

        void RefreshHeader()
        {
            if (headerTitle == null)
                return;
            if (draft == null)
            {
                headerTitle.text = "No device selected";
                headerMeta.text = "";
                return;
            }

            headerTitle.text = dirty
                ? draft.FriendlyName + "  •"
                : draft.FriendlyName;
            string mode = editing ? "editing" : "view";
            headerMeta.text =
                $"{draft.Id}  ·  {draft.VerifiedStatus}  ·  {draft.Screens.Count} screen(s)  ·  {mode}";
        }

        void RefreshScreenMenu()
        {
            if (screenPopup == null || draft == null)
                return;

            var names = new List<string>();
            for (int i = 0; i < draft.Screens.Count; i++)
            {
                names.Add(string.IsNullOrEmpty(draft.Screens[i].Name)
                    ? $"Screen {i}"
                    : draft.Screens[i].Name);
            }

            if (names.Count == 0)
                names.Add("—");

            screenEditIndex = Mathf.Clamp(screenEditIndex, 0, names.Count - 1);
            screenPopup.choices = names;
            screenPopup.SetValueWithoutNotify(names[screenEditIndex]);
        }

        void ReloadOverlay()
        {
            overlayTexture = null;
            overlayBorder = Vector2.zero;
            overlayScreenW = 0;
            overlayScreenH = 0;
            if (draft == null || draft.Screens.Count == 0)
                return;

            var s = draft.Screens[Mathf.Clamp(screenEditIndex, 0, draft.Screens.Count - 1)];
            s.NormalizePairs();
            overlayScreenW = previewLandscape ? s.LandscapeWidth : s.PortraitWidth;
            overlayScreenH = previewLandscape ? s.LandscapeHeight : s.PortraitHeight;

            // Overlays are authored for portrait; for landscape we still load portrait bezel
            // and rotate the draw (screen metrics swap for the content rect).
            int loadW = s.PortraitWidth;
            int loadH = s.PortraitHeight;
            FoldableDeviceOverlayUtil.Load(
                draft, s.Name, loadW, loadH, out overlayTexture, out overlayBorder);

            if (previewMetrics != null)
            {
                previewMetrics.text =
                    $"{s.Name}  ·  {overlayScreenW}×{overlayScreenH}  ·  {(previewLandscape ? "Landscape" : "Portrait")}";
            }
        }

        void DrawFormGui()
        {
            if (draft == null)
            {
                EditorGUILayout.HelpBox("Select a device from the list.", MessageType.Info);
                return;
            }

            if (!editing)
            {
                EditorGUILayout.HelpBox(
                    "Fields are locked. Select Edit Profile above to make changes.",
                    MessageType.Info);
            }

            formScroll = EditorGUILayout.BeginScrollView(formScroll);
            EditorGUI.BeginChangeCheck();
            EditorGUI.BeginDisabledGroup(!editing);

            DrawSection("Identity");
            draft.Id = EditorGUILayout.TextField("Id", draft.Id);
            draft.ShortName = EditorGUILayout.TextField("Short name", draft.ShortName);
            draft.DeviceModel = EditorGUILayout.TextField("Device model", draft.DeviceModel);

            int kindIdx = draft.Kind == FoldableDeviceKind.FlipablePhone
                ? 1
                : draft.Kind == FoldableDeviceKind.DualScreen
                    ? 2
                    : 0;
            kindIdx = EditorGUILayout.Popup("Kind", kindIdx, KindLabels);
            draft.Kind = kindIdx == 1
                ? FoldableDeviceKind.FlipablePhone
                : kindIdx == 2
                    ? FoldableDeviceKind.DualScreen
                    : FoldableDeviceKind.FoldablePhone;

            int hingeIdx = draft.PreferredHinge == FoldableOrientation.Vertical ? 1 : 0;
            hingeIdx = EditorGUILayout.Popup("Preferred hinge", hingeIdx, HingeLabels);
            draft.PreferredHinge = hingeIdx == 1
                ? FoldableOrientation.Vertical
                : FoldableOrientation.Horizontal;

            draft.PreferSeparating = EditorGUILayout.Toggle("Prefer separating", draft.PreferSeparating);
            // Fold only: the runtime hinge rule ignores the flag for Flip / Dual.
            if (draft.Kind == FoldableDeviceKind.FoldablePhone)
            {
                draft.InnerLandscapeNative = EditorGUILayout.Toggle(
                    new GUIContent(
                        "Inner landscape-native",
                        "iPhone Duo: Portrait is the tall laptop fold, Landscape is the wide book fold."),
                    draft.InnerLandscapeNative);
            }
            else
            {
                draft.InnerLandscapeNative = false;
            }
            draft.BasedOn = EditorGUILayout.TextField("Based on", draft.BasedOn);
            draft.Notes = EditorGUILayout.TextField("Notes", draft.Notes);
            draft.VerifiedDate = EditorGUILayout.TextField("Verified date", draft.VerifiedDate);

            int verIdx = Array.IndexOf(VerifiedLabels, draft.VerifiedStatus);
            if (verIdx < 0) verIdx = 1;
            verIdx = EditorGUILayout.Popup("Verified status", verIdx, VerifiedLabels);
            draft.VerifiedStatus = VerifiedLabels[verIdx];

            EditorGUILayout.Space(10);
            DrawSection("Skins");
            draft.DeviceFile = EditorGUILayout.TextField("Device file", draft.DeviceFile);
            EditorGUILayout.LabelField("Overlays", EditorStyles.miniBoldLabel);
            for (int i = 0; i < draft.Overlays.Count; i++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    draft.Overlays[i].Screen =
                        EditorGUILayout.TextField(draft.Overlays[i].Screen, GUILayout.Width(88));
                    draft.Overlays[i].Path = EditorGUILayout.TextField(draft.Overlays[i].Path);
                    if (GUILayout.Button(
                            new GUIContent("−", "Remove this overlay reference"),
                            GUILayout.Width(22)))
                    {
                        draft.Overlays.RemoveAt(i);
                        break;
                    }
                }
            }

            if (GUILayout.Button(
                    new GUIContent("Add overlay", "Add an overlay reference to this profile"),
                    GUILayout.Width(100)))
                draft.Overlays.Add(new FoldableDeviceCatalog.EditableOverlay());

            EditorGUILayout.Space(10);
            DrawSection("Screens");
            if (draft.Screens.Count == 0)
            {
                EditorGUILayout.HelpBox("Add at least one screen.", MessageType.Warning);
            }
            else
            {
                var names = new string[draft.Screens.Count];
                for (int i = 0; i < draft.Screens.Count; i++)
                    names[i] = string.IsNullOrEmpty(draft.Screens[i].Name)
                        ? $"Screen {i}"
                        : draft.Screens[i].Name;
                screenEditIndex = Mathf.Clamp(screenEditIndex, 0, draft.Screens.Count - 1);

                // Allow switching screens while locked so you can inspect each posture.
                EditorGUI.EndDisabledGroup();
                int next = GUILayout.Toolbar(screenEditIndex, names);
                if (next != screenEditIndex)
                {
                    screenEditIndex = next;
                    ReloadOverlay();
                }

                EditorGUI.BeginDisabledGroup(!editing);

                var s = draft.Screens[screenEditIndex];
                s.Name = EditorGUILayout.TextField("Name", s.Name);
                s.CoverOrOuter = EditorGUILayout.Toggle("Cover / outer", s.CoverOrOuter);
                s.PortraitWidth = Mathf.Max(1, EditorGUILayout.IntField("Portrait width", s.PortraitWidth));
                s.PortraitHeight = Mathf.Max(1, EditorGUILayout.IntField("Portrait height", s.PortraitHeight));
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.IntField("Landscape width", s.PortraitHeight);
                EditorGUILayout.IntField("Landscape height", s.PortraitWidth);
                EditorGUI.EndDisabledGroup();
                EditorGUILayout.HelpBox("Landscape is auto-swapped from portrait on Save.", MessageType.None);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(
                            new GUIContent("Add screen", "Add another screen configuration to this profile"),
                            GUILayout.Width(100)))
                    {
                        draft.Screens.Add(new FoldableDeviceCatalog.EditableScreen());
                        screenEditIndex = draft.Screens.Count - 1;
                    }

                    EditorGUI.BeginDisabledGroup(draft.Screens.Count <= 1);
                    if (GUILayout.Button(
                            new GUIContent("Remove screen", "Remove the active screen configuration"),
                            GUILayout.Width(110)))
                    {
                        draft.Screens.RemoveAt(screenEditIndex);
                        screenEditIndex = Mathf.Clamp(screenEditIndex - 1, 0, draft.Screens.Count - 1);
                    }

                    EditorGUI.EndDisabledGroup();
                }
            }

            EditorGUILayout.Space(10);
            DrawSection("Sources");
            for (int i = 0; i < draft.Sources.Count; i++)
            {
                EditorGUILayout.LabelField($"Source {i + 1}", EditorStyles.miniBoldLabel);
                draft.Sources[i].Title = EditorGUILayout.TextField("Title", draft.Sources[i].Title);
                draft.Sources[i].Url = EditorGUILayout.TextField("Url", draft.Sources[i].Url);
                draft.Sources[i].Quote = EditorGUILayout.TextField("Quote", draft.Sources[i].Quote);
                if (GUILayout.Button(
                        new GUIContent("Remove source", "Remove this verification source"),
                        GUILayout.Width(110)))
                {
                    draft.Sources.RemoveAt(i);
                    break;
                }
            }

            if (GUILayout.Button(
                    new GUIContent("Add source", "Add a verification source to this profile"),
                    GUILayout.Width(100)))
                draft.Sources.Add(new FoldableDeviceCatalog.EditableSource());

            EditorGUI.EndDisabledGroup();

            if (EditorGUI.EndChangeCheck() && editing)
            {
                dirty = true;
                RefreshHeader();
                RefreshScreenMenu();
                ReloadOverlay();
            }

            EditorGUILayout.EndScrollView();
        }

        static void DrawSection(string title)
        {
            var rect = EditorGUILayout.GetControlRect(false, 18);
            EditorGUI.LabelField(rect, title, EditorStyles.boldLabel);
            var line = new Rect(rect.x, rect.yMax - 1, rect.width, 1);
            EditorGUI.DrawRect(line, new Color(0.35f, 0.35f, 0.35f, 0.6f));
            GUILayout.Space(4);
        }

        void DrawPreviewGui()
        {
            var rect = GUILayoutUtility.GetRect(10, 10, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            if (Event.current.type != EventType.Repaint)
                return;

            EditorGUI.DrawRect(rect, new Color(0.08f, 0.08f, 0.09f));

            if (draft == null || draft.Screens.Count == 0 || overlayScreenW < 1 || overlayScreenH < 1)
            {
                GUI.Label(rect, "No preview", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            float screenW = overlayScreenW;
            float screenH = overlayScreenH;
            float borderX = overlayBorder.x;
            float borderY = overlayBorder.y;

            // Portrait-authored bezel: when previewing landscape, swap border contribution.
            if (previewLandscape)
            {
                float t = borderX;
                borderX = borderY;
                borderY = t;
            }

            float totalW = screenW + borderX * 2f;
            float totalH = screenH + borderY * 2f;
            float scale = Mathf.Clamp(Mathf.Min(rect.width / totalW, rect.height / totalH) * 0.9f, 0.04f, 3f);
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

            // Soft shadow
            EditorGUI.DrawRect(
                new Rect(deviceRect.x + 4, deviceRect.y + 6, deviceRect.width, deviceRect.height),
                new Color(0f, 0f, 0f, 0.35f));

            EditorGUI.DrawRect(gameRect, new Color(0.18f, 0.2f, 0.24f));
            var s = draft.Screens[Mathf.Clamp(screenEditIndex, 0, draft.Screens.Count - 1)];
            GUI.Label(
                gameRect,
                $"\n{draft.ShortName}\n{s.Name}\n{screenW:0}×{screenH:0}",
                EditorStyles.centeredGreyMiniLabel);

            if (overlayTexture != null)
            {
                if (previewLandscape)
                {
                    // Rotate portrait bezel into landscape frame.
                    var pivot = deviceRect.center;
                    Matrix4x4 prev = GUI.matrix;
                    GUIUtility.RotateAroundPivot(90f, pivot);
                    var rotated = new Rect(
                        pivot.x - deviceRect.height * 0.5f,
                        pivot.y - deviceRect.width * 0.5f,
                        deviceRect.height,
                        deviceRect.width);
                    GUI.DrawTexture(rotated, overlayTexture, ScaleMode.StretchToFill, true);
                    GUI.matrix = prev;
                }
                else
                {
                    GUI.DrawTexture(deviceRect, overlayTexture, ScaleMode.StretchToFill, true);
                }
            }
            else
            {
                // Fallback chrome when overlay PNG is missing.
                DrawFallbackBezel(deviceRect, gameRect);
            }
        }

        static void DrawFallbackBezel(Rect deviceRect, Rect gameRect)
        {
            EditorGUI.DrawRect(deviceRect, new Color(0.12f, 0.13f, 0.14f));
            EditorGUI.DrawRect(gameRect, new Color(0.18f, 0.2f, 0.24f));
            Handles.BeginGUI();
            Handles.color = new Color(1f, 1f, 1f, 0.12f);
            Handles.DrawAAPolyLine(2f,
                new Vector3(gameRect.xMin, gameRect.yMin),
                new Vector3(gameRect.xMax, gameRect.yMin),
                new Vector3(gameRect.xMax, gameRect.yMax),
                new Vector3(gameRect.xMin, gameRect.yMax),
                new Vector3(gameRect.xMin, gameRect.yMin));
            Handles.EndGUI();
        }

        #endregion

        #region Catalog

        void SaveDraft()
        {
            if (draft == null)
                return;
            if (!editing)
            {
                status = "Select Edit Profile before changing and saving.";
                RefreshAll();
                return;
            }

            string previousId = selectedId;
            if (!FoldableDeviceCatalog.SaveProfile(draft, syncDeviceFile: true, out string error))
            {
                EditorUtility.DisplayDialog("Save failed", error, "OK");
                status = "Save failed.";
                RefreshAll();
                return;
            }

            selectedId = draft.Id;
            EditorPrefs.SetString(PrefSelectedId, selectedId);
            dirty = false;
            editing = false;
            LoadSelectedIntoDraft();
            status = previousId != selectedId
                ? $"Saved {selectedId} (id changed)."
                : $"Saved {selectedId}.";
            RefreshAll();
        }

        void UseInSimulator()
        {
            if (draft == null)
                return;

            string previewScreenName = draft.Screens.Count > 0
                ? draft.Screens[Mathf.Clamp(screenEditIndex, 0, draft.Screens.Count - 1)].Name
                : "";

            if (dirty)
            {
                bool saveFirst = EditorUtility.DisplayDialog(
                    "Unsaved changes",
                    "Save before selecting in Simulator?",
                    "Save & Use",
                    "Use without saving");
                if (saveFirst)
                {
                    SaveDraft();
                    if (dirty)
                        return;
                }
            }

            var live = FoldableDeviceCatalog.FindById(selectedId) ?? FoldableDeviceCatalog.FindById(draft.Id);
            if (live == null)
            {
                status = "Save the profile before using it in Simulator.";
                RefreshAll();
                return;
            }

            int screenIndex = -1;
            for (int i = 0; i < live.Screens.Length; i++)
            {
                if (string.Equals(live.Screens[i].Name, previewScreenName, StringComparison.OrdinalIgnoreCase))
                {
                    screenIndex = i;
                    break;
                }
            }
            if (screenIndex < 0)
                screenIndex = Mathf.Clamp(screenEditIndex, 0, Mathf.Max(0, live.Screens.Length - 1));

            if (!FoldableDeviceCatalog.SelectInSimulator(live.FriendlyName, screenIndex, out string error))
            {
                EditorUtility.DisplayDialog("Simulator", error, "OK");
                status = error;
                RefreshAll();
                return;
            }

            status = "Selected in Device Simulator: " + live.FriendlyName +
                     (live.Screens.Length > 0 ? " — " + live.Screens[screenIndex].Name : "");
            RefreshAll();
        }

        void DuplicateSelected()
        {
            if (string.IsNullOrEmpty(selectedId))
                return;
            if (!ConfirmDiscardIfDirty())
                return;

            if (IsDraftMissingFromCatalog())
            {
                ExitEditAndReloadDraft("New profile discarded.");
                return;
            }

            // ConfirmDiscardIfDirty does not clear state — drop unsaved edits before reading catalog.
            if (dirty)
            {
                editing = false;
                dirty = false;
                LoadSelectedIntoDraft();
            }

            var src = FoldableDeviceCatalog.FindById(selectedId);
            if (src == null)
                return;

            string newId = src.Id + "-copy";
            int n = 2;
            while (FoldableDeviceCatalog.FindById(newId) != null)
            {
                newId = src.Id + "-copy-" + n;
                n++;
            }

            if (!FoldableDeviceCatalog.DuplicateProfile(src.Id, newId, src.ShortName + " Copy", out string error))
            {
                EditorUtility.DisplayDialog("Duplicate failed", error, "OK");
                status = "Duplicate failed.";
                RefreshAll();
                return;
            }

            selectedId = newId;
            EditorPrefs.SetString(PrefSelectedId, selectedId);
            dirty = false;
            editing = false;
            LoadSelectedIntoDraft();
            status = "Duplicated → " + newId;
            RefreshAll();
        }

        void AddDevice()
        {
            if (!ConfirmDiscardIfDirty())
                return;

            if (IsDraftMissingFromCatalog())
            {
                // Replacing one unsaved blank with another — drop the previous draft first.
                editing = false;
                dirty = false;
            }
            else if (dirty)
            {
                editing = false;
                dirty = false;
                LoadSelectedIntoDraft();
            }

            int kind = EditorUtility.DisplayDialogComplex(
                "Add device",
                "Choose a template kind for the new profile.",
                "Fold",
                "Flip",
                "Dual");
            var k = kind == 1
                ? FoldableDeviceKind.FlipablePhone
                : kind == 2
                    ? FoldableDeviceKind.DualScreen
                    : FoldableDeviceKind.FoldablePhone;

            draft = FoldableDeviceCatalog.CreateBlank(k);
            selectedId = draft.Id;
            dirty = true;
            editing = true;
            screenEditIndex = 0;
            status = "New profile — edit fields, then Save.";
            RefreshAll();
        }

        void RemoveSelected()
        {
            if (string.IsNullOrEmpty(selectedId))
                return;
            if (!ConfirmDiscardIfDirty())
                return;

            // Unsaved Add drafts are not in the catalog yet — Cancel is the discard path.
            if (IsDraftMissingFromCatalog())
            {
                ExitEditAndReloadDraft("New profile discarded.");
                return;
            }

            var p = FoldableDeviceCatalog.FindById(selectedId);
            if (p == null)
                return;

            int choice = EditorUtility.DisplayDialogComplex(
                "Remove device",
                $"Delete profile '{p.ShortName}' ({p.Id})?",
                "Delete profile + skins",
                "Cancel",
                "Profile JSON only");
            if (choice == 1)
                return;

            if (!FoldableDeviceCatalog.DeleteProfile(p.Id, deleteSkins: choice == 0, out string error))
            {
                EditorUtility.DisplayDialog("Remove failed", error, "OK");
                status = "Remove failed.";
                RefreshAll();
                return;
            }

            dirty = false;
            editing = false;
            selectedId = null;
            draft = null;
            EnsureSelection();
            status = "Removed " + p.Id;
            RefreshAll();
        }

        #endregion
    }
}
