using System;
using System.Collections.Generic;
using FDPlus.ResponsiveUI.Shared.Editor;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace FDPlus.PhonePad.Editor
{
    /// <summary>
    /// View / edit non-foldable phone &amp; pad device profiles.
    /// Layout: Devices | Editor | Preview.
    /// Optional Unity Device Simulator visibility via Enable/Disable (.device create/delete).
    /// </summary>
    public class PhonePadDeviceCatalogWindow : EditorWindow
    {
        #region Constants

        const string PrefListWidth = "com.futuredigitalplus.phonepad.catalog.listWidth";
        const string PrefPreviewWidth = "com.futuredigitalplus.phonepad.catalog.previewWidth";
        const string PrefPreviewLandscape = "com.futuredigitalplus.phonepad.catalog.previewLandscape";
        const string PrefSelectedId = "com.futuredigitalplus.phonepad.catalog.selectedId";

        static readonly string[] KindFilters = { "All", "Phone", "Pad" };
        static readonly string[] BrandFilters =
        {
            "All", "Apple", "Google", "Samsung", "Xiaomi", "Huawei", "OnePlus", "Motorola", "Other"
        };
        static readonly string[] KindLabels = { "Phone", "Pad" };
        static readonly string[] VerifiedLabels = { "matched", "approx" };
        static readonly string[] BrandChoices =
        {
            "Apple", "Google", "Samsung", "Xiaomi", "Huawei", "OnePlus", "Motorola", "Other"
        };

        #endregion

        #region GUI

        // Serialized so a domain reload (script compile) keeps the filters and an unsaved draft.
        [SerializeField] string search = "";
        [SerializeField] int kindFilter;
        [SerializeField] int brandFilter;
        [SerializeField] string selectedId;
        [SerializeReference] PhonePadDeviceCatalog.EditableProfile draft;
        [SerializeField] bool dirty;
        [SerializeField] bool editing;
        [SerializeField] string status = "";
        bool previewLandscape;
        [SerializeField] Vector2 formScroll;

        TwoPaneSplitView rootSplit;
        TwoPaneSplitView editorSplit;
        ListView deviceList;
        Label headerTitle;
        Label headerMeta;
        Label statusLabel;
        Label previewMetrics;
        IMGUIContainer formHost;
        IMGUIContainer previewHost;
        ToolbarToggle landscapeToggle;
        Button editButton;
        Button cancelButton;
        Button saveButton;
        Button openSourceButton;
        Button simulatorButton;
        PopupField<string> brandPopup;
        VisualElement kindFilterRow;

        [MenuItem(ResponsiveUiEditorMenus.PhonePadCatalog, false, ResponsiveUiEditorMenus.PriorityDevices)]
        [MenuItem(ResponsiveUiEditorMenus.WindowPhonePadCatalog, false, ResponsiveUiEditorMenus.PriorityWindow)]
        public static void Open()
        {
            var window = GetWindow<PhonePadDeviceCatalogWindow>();
            window.titleContent = new GUIContent("Phone & Pad Catalog");
            window.minSize = new Vector2(980, 560);
            window.Show();
        }

        void OnEnable()
        {
            // After a domain reload the serialized draft is still unsaved: keep it and its selection.
            bool keepDraft = dirty && draft != null;
            if (!keepDraft)
                selectedId = EditorPrefs.GetString(PrefSelectedId, "");
            previewLandscape = EditorPrefs.GetBool(PrefPreviewLandscape, false);
            PhonePadDeviceCatalog.CatalogChanged += OnCatalogChanged;
            BuildUi();
            if (!keepDraft)
                EnsureSelection();
            RefreshAll();
        }

        void OnDisable()
        {
            PhonePadDeviceCatalog.CatalogChanged -= OnCatalogChanged;
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
                PhonePadDeviceCatalog.Reload();
                ExitEditAndReloadDraft("Reloaded profiles.");
            });
            reloadButton.tooltip = "Reload all device profiles from disk";
            bar.Add(reloadButton);

            simulatorButton = MakeToolbarButton("Enable All in Simulator", ToggleAllSimulatorEnabled);
            simulatorButton.tooltip =
                "Enable or disable all catalog profiles in Unity Device Simulator (.device create/delete)";
            simulatorButton.style.minWidth = 160;
            bar.Add(simulatorButton);

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
                    // Critical: allow this pane to shrink inside TwoPaneSplitView so the
                    // footer actions are not pushed/clipped below the window.
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

            kindFilterRow = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    marginBottom = 6,
                    flexShrink = 0,
                    minHeight = 22,
                    height = 22,
                    overflow = Overflow.Hidden
                }
            };
            for (int i = 0; i < KindFilters.Length; i++)
            {
                int idx = i;
                var toggle = new ToolbarToggle { text = KindFilters[i], value = kindFilter == i };
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
                        toggle.SetValueWithoutNotify(kindFilter == idx);
                        return;
                    }

                    if (dirty)
                    {
                        toggle.SetValueWithoutNotify(kindFilter == idx);
                        ShowFinishEditingStatus();
                        return;
                    }

                    kindFilter = idx;
                    RefreshKindToggles();
                    RefreshAfterFilterChanged();
                });
                kindFilterRow.Add(toggle);
            }

            pane.Add(kindFilterRow);

            // Dedicated row so PopupField cannot paint over the Kind toggles above.
            var brandRow = new VisualElement
            {
                style =
                {
                    flexShrink = 0,
                    height = 22,
                    marginBottom = 4,
                    overflow = Overflow.Hidden
                }
            };
            brandPopup = new PopupField<string>(new List<string>(BrandFilters), brandFilter);
            brandPopup.style.flexGrow = 1;
            brandPopup.style.height = 22;
            brandPopup.style.marginTop = 0;
            brandPopup.style.marginBottom = 0;
            brandPopup.tooltip = "Filter by brand";
            brandPopup.RegisterValueChangedCallback(evt =>
            {
                int idx = Array.IndexOf(BrandFilters, evt.newValue);
                if (idx < 0)
                    return;
                if (dirty)
                {
                    brandPopup.SetValueWithoutNotify(BrandFilters[brandFilter]);
                    ShowFinishEditingStatus();
                    return;
                }

                brandFilter = idx;
                RefreshAfterFilterChanged();
            });
            brandRow.Add(brandPopup);
            pane.Add(brandRow);

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
                bool designRef = PhonePadDeviceCatalog.IsDesignCanvasReference(d.Id);
                string text = designRef
                    ? PhonePadDeviceCatalog.DesignReferenceMarker + d.ShortName
                    : d.ShortName;
                if (!string.IsNullOrEmpty(d.VerifiedStatus) &&
                    !d.VerifiedStatus.Equals("matched", StringComparison.OrdinalIgnoreCase))
                    text += $"  · {d.VerifiedStatus}";
                label.text = text;
                label.tooltip =
                    $"{d.DisplayLabel}\n{d.Id}\n{d.PortraitWidth}×{d.PortraitHeight} @ {d.Dpi:0} dpi\n" +
                    $"{d.DiagonalInches:0.##}\"" +
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
            addButton.tooltip = "Create a new phone or pad profile";
            addButton.style.flexGrow = 1;
            addButton.style.marginTop = 0;
            addButton.style.marginBottom = 0;
            actions.Add(addButton);

            var duplicateButton = MakeToolbarButton("Duplicate", DuplicateSelected);
            duplicateButton.tooltip = "Duplicate the selected device profile";
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

        void RefreshKindToggles()
        {
            if (kindFilterRow == null)
                return;
            int i = 0;
            kindFilterRow.Query<ToolbarToggle>().ForEach(t =>
            {
                t.SetValueWithoutNotify(i == kindFilter);
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
            saveButton.tooltip = "Write the selected profile JSON";
            actions.Add(saveButton);

            openSourceButton = MakeToolbarButton("Open Official Page", OpenOfficialSource);
            openSourceButton.tooltip = "Open the first official source URL in your browser";
            openSourceButton.style.minWidth = 120;
            actions.Add(openSourceButton);

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

            header.Add(new Label("Preview")
            {
                style = { unityFontStyleAndWeight = FontStyle.Bold, flexGrow = 1, minWidth = 56 }
            });

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

        List<PhonePadDeviceCatalog.DeviceProfile> FilteredDevices()
        {
            var list = new List<PhonePadDeviceCatalog.DeviceProfile>();
            foreach (var d in PhonePadDeviceCatalog.Devices)
            {
                if (!PassesFilter(d))
                    continue;
                list.Add(d);
            }

            return list;
        }

        bool PassesFilter(PhonePadDeviceCatalog.DeviceProfile d)
        {
            if (kindFilter == 1 && d.Kind != PhonePadDeviceKind.Phone)
                return false;
            if (kindFilter == 2 && d.Kind != PhonePadDeviceKind.Pad)
                return false;

            if (brandFilter > 0)
            {
                string wanted = BrandFilters[brandFilter];
                if (wanted == "Other")
                {
                    bool known = false;
                    for (int i = 1; i < BrandFilters.Length - 1; i++)
                    {
                        if (string.Equals(d.Brand, BrandFilters[i], StringComparison.OrdinalIgnoreCase))
                        {
                            known = true;
                            break;
                        }
                    }

                    if (known)
                        return false;
                }
                else if (!string.Equals(d.Brand, wanted, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            if (string.IsNullOrEmpty(search))
                return true;

            return (d.ShortName != null &&
                    d.ShortName.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0) ||
                   (d.Id != null && d.Id.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0) ||
                   (d.Brand != null && d.Brand.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0) ||
                   (d.BasedOn != null &&
                    d.BasedOn.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        void EnsureSelection()
        {
            var devices = PhonePadDeviceCatalog.Devices;
            if (devices == null || devices.Length == 0)
            {
                selectedId = null;
                draft = null;
                return;
            }

            if (string.IsNullOrEmpty(selectedId) || PhonePadDeviceCatalog.FindById(selectedId) == null)
                selectedId = devices[0].Id;

            if (!dirty)
                LoadSelectedIntoDraft();
        }

        void LoadSelectedIntoDraft()
        {
            var p = PhonePadDeviceCatalog.FindById(selectedId);
            draft = p != null ? PhonePadDeviceCatalog.EditableProfile.FromProfile(p) : null;
            dirty = false;
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

        bool IsDraftMissingFromCatalog() =>
            draft != null && PhonePadDeviceCatalog.FindById(draft.Id) == null;

        void ExitEditAndReloadDraft(string statusMessage)
        {
            bool restorePrevious = IsDraftMissingFromCatalog();
            editing = false;
            dirty = false;

            if (restorePrevious)
            {
                selectedId = EditorPrefs.GetString(PrefSelectedId, "");
                if (string.IsNullOrEmpty(selectedId) || PhonePadDeviceCatalog.FindById(selectedId) == null)
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

            saveButton.SetEnabled(hasDraft && editing);
            saveButton.style.display = editing ? DisplayStyle.Flex : DisplayStyle.None;

            bool hasSource = hasDraft && draft.Sources.Count > 0 &&
                             !string.IsNullOrWhiteSpace(draft.Sources[0].Url);
            openSourceButton.SetEnabled(hasSource);
            openSourceButton.style.display = hasSource ? DisplayStyle.Flex : DisplayStyle.None;

            if (simulatorButton == null)
                return;

            bool hasDevices = PhonePadDeviceCatalog.Devices != null &&
                              PhonePadDeviceCatalog.Devices.Length > 0;
            simulatorButton.SetEnabled(hasDevices && !editing && !dirty);
            // Any skin on → Disable All; none on → Enable All (mixed sets can still be cleared).
            bool anyOn = hasDevices && PhonePadDeviceCatalog.AreAnySimulatorSkinsEnabled();
            simulatorButton.text = anyOn ? "Disable All in Simulator" : "Enable All in Simulator";
        }

        #endregion

        #region Simulator

        void ToggleAllSimulatorEnabled()
        {
            if (dirty || editing)
            {
                status = "Save or Cancel edits before changing Simulator visibility.";
                RefreshAll();
                return;
            }

            var devices = PhonePadDeviceCatalog.Devices;
            if (devices == null || devices.Length == 0)
            {
                status = "No device profiles to update.";
                RefreshAll();
                return;
            }

            bool enable = !PhonePadDeviceCatalog.AreAnySimulatorSkinsEnabled();
            string verb = enable ? "Enable" : "Disable";
            if (!EditorUtility.DisplayDialog(
                    verb + " All in Simulator",
                    $"{verb} all {devices.Length} phone/pad profiles in Unity Device Simulator?",
                    verb + " All",
                    "Cancel"))
                return;

            if (!PhonePadDeviceCatalog.SetAllSimulatorEnabled(enable, out string error, out int changed))
            {
                EditorUtility.DisplayDialog("Device Simulator", error ?? "Bulk toggle failed.", "OK");
                status = "Simulator bulk toggle failed.";
                RefreshAll();
                return;
            }

            LoadSelectedIntoDraft();
            status = enable
                ? $"Enabled {changed} profile(s) in Device Simulator."
                : $"Disabled {changed} profile(s). Design Canvas references (◆) stay enabled.";
            RefreshAll();
        }

        #endregion

        #region GUI

        void OpenOfficialSource()
        {
            if (draft == null || draft.Sources.Count == 0)
                return;
            string url = draft.Sources[0].Url;
            if (string.IsNullOrWhiteSpace(url))
                return;
            Application.OpenURL(url);
            status = "Opened official page.";
            if (statusLabel != null)
                statusLabel.text = status;
        }

        void RefreshAll()
        {
            RefreshDeviceList();
            RefreshHeader();
            RefreshPreviewOnly();
            UpdateEditToolbar();
            formHost?.MarkDirtyRepaint();
            previewHost?.MarkDirtyRepaint();
            if (statusLabel != null)
                statusLabel.text = status ?? "";
        }

        void RefreshPreviewOnly()
        {
            previewHost?.MarkDirtyRepaint();
            if (previewMetrics == null)
                return;
            if (draft == null)
            {
                previewMetrics.text = "";
                return;
            }

            draft.NormalizeSizePairs();
            int w = previewLandscape ? draft.LandscapeWidth : draft.PortraitWidth;
            int h = previewLandscape ? draft.LandscapeHeight : draft.PortraitHeight;
            previewMetrics.text =
                $"{draft.Kind}  ·  {w}×{h}  ·  {(previewLandscape ? "Landscape" : "Portrait")}";
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

            headerTitle.text = dirty ? draft.DisplayLabel + "  •" : draft.DisplayLabel;
            string verified = string.IsNullOrEmpty(draft.VerifiedStatus) ? "approx" : draft.VerifiedStatus;
            string mode = editing ? "editing" : "view";
            headerMeta.text = $"{draft.Id}  ·  {draft.Kind}  ·  {verified}  ·  {mode}";
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

            int brandIdx = Array.IndexOf(BrandChoices, draft.Brand);
            if (brandIdx < 0) brandIdx = BrandChoices.Length - 1;
            brandIdx = EditorGUILayout.Popup("Brand", brandIdx, BrandChoices);
            draft.Brand = BrandChoices[brandIdx];

            int kindIdx = draft.Kind == PhonePadDeviceKind.Pad ? 1 : 0;
            kindIdx = EditorGUILayout.Popup("Kind", kindIdx, KindLabels);
            draft.Kind = kindIdx == 1 ? PhonePadDeviceKind.Pad : PhonePadDeviceKind.Phone;

            draft.BasedOn = EditorGUILayout.TextField("Based on", draft.BasedOn);
            draft.Notes = EditorGUILayout.TextField("Notes", draft.Notes);
            draft.VerifiedDate = EditorGUILayout.TextField("Verified date", draft.VerifiedDate);

            int verIdx = Array.IndexOf(VerifiedLabels, draft.VerifiedStatus);
            if (verIdx < 0) verIdx = 1;
            verIdx = EditorGUILayout.Popup("Verified status", verIdx, VerifiedLabels);
            draft.VerifiedStatus = VerifiedLabels[verIdx];

            EditorGUILayout.Space(10);
            DrawSection("Screen");
            draft.PortraitWidth = Mathf.Max(1, EditorGUILayout.IntField("Portrait width", draft.PortraitWidth));
            draft.PortraitHeight = Mathf.Max(1, EditorGUILayout.IntField("Portrait height", draft.PortraitHeight));
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.IntField("Landscape width", draft.PortraitHeight);
            EditorGUILayout.IntField("Landscape height", draft.PortraitWidth);
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.HelpBox("Landscape is auto-swapped from portrait on Save.", MessageType.None);

            draft.Dpi = Mathf.Max(0f, EditorGUILayout.FloatField("DPI", draft.Dpi));
            draft.DiagonalInches = Mathf.Max(0f, EditorGUILayout.FloatField("Diagonal (inches)", draft.DiagonalInches));

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
                draft.Sources.Add(new PhonePadDeviceCatalog.EditableSource());

            EditorGUI.EndDisabledGroup();

            if (EditorGUI.EndChangeCheck() && editing)
            {
                dirty = true;
                RefreshHeader();
                RefreshPreviewOnly();
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

            if (draft == null)
            {
                GUI.Label(rect, "No preview", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            draft.NormalizeSizePairs();
            float screenW = previewLandscape ? draft.LandscapeWidth : draft.PortraitWidth;
            float screenH = previewLandscape ? draft.LandscapeHeight : draft.PortraitHeight;
            if (screenW < 1 || screenH < 1)
            {
                GUI.Label(rect, "No preview", EditorStyles.centeredGreyMiniLabel);
                return;
            }

            // Phone/pad differ only in bezel thickness; colors match FoldAndFlip fallback.
            float border = draft.Kind == PhonePadDeviceKind.Pad ? 26f : 16f;
            float totalW = screenW + border * 2f;
            float totalH = screenH + border * 2f;
            float scale = Mathf.Clamp(Mathf.Min(rect.width / totalW, rect.height / totalH) * 0.9f, 0.04f, 3f);
            float drawW = totalW * scale;
            float drawH = totalH * scale;
            var deviceRect = new Rect(
                rect.x + (rect.width - drawW) * 0.5f,
                rect.y + (rect.height - drawH) * 0.5f,
                drawW,
                drawH);
            var gameRect = new Rect(
                deviceRect.x + border * scale,
                deviceRect.y + border * scale,
                screenW * scale,
                screenH * scale);

            EditorGUI.DrawRect(
                new Rect(deviceRect.x + 4, deviceRect.y + 6, deviceRect.width, deviceRect.height),
                new Color(0f, 0f, 0f, 0.35f));

            DrawFallbackBezel(deviceRect, gameRect);

            GUI.Label(
                gameRect,
                $"\n{draft.ShortName}\n{screenW:0}×{screenH:0}",
                EditorStyles.centeredGreyMiniLabel);
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
                status = "Click Edit Profile before saving.";
                RefreshAll();
                return;
            }

            string previousId = selectedId;
            if (!PhonePadDeviceCatalog.SaveProfile(draft, out string error))
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

            if (dirty)
            {
                editing = false;
                dirty = false;
                LoadSelectedIntoDraft();
            }

            var src = PhonePadDeviceCatalog.FindById(selectedId);
            if (src == null)
                return;

            string newId = src.Id + "-copy";
            int n = 2;
            while (PhonePadDeviceCatalog.FindById(newId) != null)
            {
                newId = src.Id + "-copy-" + n;
                n++;
            }

            if (!PhonePadDeviceCatalog.DuplicateProfile(src.Id, newId, src.ShortName + " Copy", out string error))
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
                editing = false;
                dirty = false;
            }
            else if (dirty)
            {
                editing = false;
                dirty = false;
                LoadSelectedIntoDraft();
            }

            int choice = EditorUtility.DisplayDialogComplex(
                "Add device",
                "Choose a template for the new profile.",
                "Phone",
                "Cancel",
                "Pad");
            if (choice == 1)
                return;
            var kind = choice == 2 ? PhonePadDeviceKind.Pad : PhonePadDeviceKind.Phone;

            draft = PhonePadDeviceCatalog.CreateBlank(kind);
            selectedId = draft.Id;
            dirty = true;
            editing = true;
            status = "New profile — edit fields, then Save.";
            RefreshAll();
        }

        void RemoveSelected()
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

            var p = PhonePadDeviceCatalog.FindById(selectedId);
            if (p == null)
                return;

            if (!EditorUtility.DisplayDialog(
                    "Remove device",
                    $"Delete profile '{p.ShortName}' ({p.Id})?",
                    "Delete",
                    "Cancel"))
                return;

            if (!PhonePadDeviceCatalog.DeleteProfile(p.Id, out string error))
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
