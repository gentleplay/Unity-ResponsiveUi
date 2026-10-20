#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using FDPlus.Foldable;
using FDPlus.Foldable.Editor;
using FDPlus.PhonePad.Editor;
using FDPlus.ResponsiveUI;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace FDPlus.ResponsiveUI.Editor
{
    /// <summary>
    /// Project Settings → Responsive UI page: slot visibility rules, Design Scale reference
    /// devices per form factor, Background Cover Size, and the half-open hinge overlay.
    /// Values live on <see cref="ResponsiveUiEditorSettings"/>.
    /// </summary>
    static class ResponsiveUiSettingsProvider
    {
        #region Constants

        const string Path = "Project/Responsive UI";

        #endregion

        #region GUI

        [SettingsProvider]
        public static SettingsProvider Create()
        {
            return new SettingsProvider(Path, SettingsScope.Project)
            {
                label = "Responsive UI",
                guiHandler = _ =>
                {
                    ResponsiveUiEditorSettings settings = ResponsiveUiEditorSettings.instance;

                    EditorGUILayout.HelpBox(
                        "Widget Portrait / Landscape slots follow Player Allowed Orientations:\n" +
                        "• Allowed axis → all form-factor slots Shown + Enabled\n" +
                        "• Disallowed axis → those slots Hidden + Disabled\n" +
                        "• Fold Half Opened Landscape and Fold Cover Half Opened Landscape stay shown either way\n" +
                        "• Wide Screen (both slots) is on for Windows / macOS / Linux / WebGL targets and off on iOS / Android; it ignores the mobile lock\n" +
                        "• iPhone Duo Seated (both slots) stays shown: while Seated the device allows only the hold's " +
                        "axis; with Seated rotation = Locked (default) one hold is kept, with Follows hold it switches " +
                        "(laptop hold → Seated Portrait, book hold → Seated Landscape)\n\n" +
                        "Design Canvas Reference Device (per form factor):\n" +
                        "• Highlighted with ◆ in Phone/Pad and Fold/Flip catalogs\n" +
                        "• Device Simulator lists them with Flip / Fold Cover / iPhone Duo / Fold role labels at the top of the list\n" +
                        "• Always kept enabled in Simulator (Disable All skips them)\n" +
                        "• Changing the reference here rewrites Simulator labels automatically\n" +
                        "• Fold Cover uses the Fold Cover/outer portrait size (e.g. Fold8 1248×1972)\n" +
                        "• Fold uses Fold7-like Inner for HalfOpened (e.g. 1968×2184); Landscape = tall (flat turned 90°), Portrait = wide\n" +
                        "• Fold Cover HalfOpened uses Fold8-like Inner (e.g. 1848×2448); Landscape = tall, Portrait = wide (2448×1848)\n" +
                        "• Flip Cover uses the Flip Cover/outer portrait size (e.g. Flip8 948×1048)\n" +
                        "• iPhone Duo Seated uses iPhone Duo-like Inner (e.g. 2007×2853): Seated Portrait (laptop) is tall, Seated Landscape (book) is wide\n" +
                        "• iPhone Duo Cover uses iPhone Duo-like Cover (e.g. 1398×2034)\n" +
                        "• Wide Screen (Windows / macOS / Linux / WebGL) has no catalog device: edit its Design Canvas Size " +
                        "(built-in 1080×1920, 1920×1080 landscape) or Use Pad Design Canvas; Design Scale follows the window / browser size\n" +
                        "• Rows and Inspector slots follow the build target: Android → Phone / Pad / Fold / Flip, " +
                        "iOS → iPhone / iPad / iPhone Duo, Standalone / WebGL → Wide Screen\n\n" +
                        "Background Cover Size:\n" +
                        "• Pick a catalog device (default: iPad Pro 13 M5 landscape)\n" +
                        "• Or Follow a Design Canvas reference\n" +
                        "• Every ResponsiveUI without Override updates immediately in loaded scenes\n\n" +
                        "Source: Player Settings → Resolution and Presentation\n" +
                        "(Default Orientation, or Allowed Orientations when Auto Rotation).\n\n" +
                        $"Current: {FoldablePlayerOrientation.DescribeProjectOrientation()}\n" +
                        $"Portrait slots: {(ResponsiveUiEditorSettings.IsPortraitAllowedByPlayer() ? "visible" : "hidden")}\n" +
                        $"Landscape slots: {(ResponsiveUiEditorSettings.IsLandscapeAllowedByPlayer() ? "visible" : "hidden")}\n" +
                        "Fold / Fold Cover Half Opened Landscape: always visible\n" +
                        "iPhone Duo Seated: both visible (Seated allows only the hold's axis)",
                        MessageType.Info);

                    EditorGUILayout.Space(8);

                    EditorGUI.BeginChangeCheck();
                    bool locked = EditorGUILayout.Toggle(
                        new GUIContent(
                            "Lock Enabled Toggles",
                            "When on, widget slot Enabled checkboxes are read-only in the Inspector."),
                        ResponsiveUiEditorSettings.LockEnabledToggles);
                    if (EditorGUI.EndChangeCheck())
                        settings.SetLockEnabledToggles(locked);

                    EditorGUILayout.Space(12);
                    EditorGUILayout.LabelField("Design Scale Defaults", EditorStyles.boldLabel);
                    EditorGUILayout.HelpBox(
                        "Pick a Device Catalog profile as the reference for each form factor. " +
                        "That device's portrait size becomes the default Design Canvas Size for new / unset slots " +
                        "(Wide Screen has no device: its size is edited directly). " +
                        "Edit catalogs via Responsive UI → Devices.",
                        MessageType.None);

                    EditorGUILayout.LabelField(
                        "Build target: " + PlatformDisplayName(ResponsivePlatforms.Active) +
                        ". Android and iOS forms are listed together. A Device Simulator profile " +
                        "drives detection on any target. Wide Screen follows the build target.",
                        EditorStyles.miniLabel);
                    ResponsiveDevice.FormFactor[] forms = ResponsiveWidgetLayout.FormFactors;
                    string group = "";
                    for (int i = 0; i < forms.Length; i++)
                    {
                        if (!ResponsiveUiEditorSettings.FormVisible(forms[i]))
                            continue;
                        ResponsiveSlotGroupUi.LayoutHeader(ref group, forms[i]);
                        DrawFormDefaults(settings, forms[i]);
                    }

                    EditorGUILayout.Space(8);
                    if (GUILayout.Button("Reset Design Defaults to Preferred Devices"))
                        settings.ResetDesignDefaultsToBuiltins();

                    DrawBackgroundArt(settings);
                    DrawHingeGuide();
                },
                keywords = new[]
                {
                "Responsive", "UI", "Enabled", "Lock", "Widget",
                "Phone", "iPhone", "Fold Cover", "Flip Cover", "Pad", "iPad", "Portrait", "Landscape", "Orientation",
                "Design", "Scale", "Canvas", "Fold", "Flip", "Cover", "Model", "Profile", "Device",
                "Background", "Art", "originalBgSize",
                "Hinge", "Guide", "Half-Open",
                "Fold Cover Half Opened", "Flip Half Opened", "iPhone Duo", "iPhone Duo Cover",
                "Seated", "iPhone Duo Seated", "Wide Screen", "WebGL", "Desktop",
            },
            };
        }

        static void DrawBackgroundArt(ResponsiveUiEditorSettings settings)
        {
            EditorGUILayout.Space(16);
            EditorGUILayout.LabelField("Background Cover Size", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Designed pixel size of background art used by every ResponsiveUI cover Image " +
                "(Match Mode popup: Cover → Fit → Stretch → Fit Width → Fit Height → Width Only → Height Only → Manual). Default is iPad Pro 13 M5 landscape (2752×2064). " +
                "Changing this updates loaded scene components immediately. " +
                "Open other scenes once so they bake the size for player builds. " +
                "A component can Override to keep a local size.",
                MessageType.None);

            ResponsiveUiEditorSettings.BackgroundArtDefaults current =
                ResponsiveUiEditorSettings.GetBackgroundArt();
            Vector2 resolved = ResponsiveUiEditorSettings.GetBackgroundArtSize();

            // The build target's forms plus the stored one, named per GUI pass (iOS: iPhone / iPad).
            var formChoices = new List<ResponsiveDevice.FormFactor>();
            var formLabels = new List<string>();
            ResponsiveDevice.FormFactor[] forms = ResponsiveWidgetLayout.FormFactors;
            for (int i = 0; i < forms.Length; i++)
            {
                if (forms[i] != current.form && !ResponsiveUiEditorSettings.FormVisible(forms[i]))
                    continue;
                formChoices.Add(forms[i]);
                formLabels.Add(
                    ResponsiveWidgetLayout.SlotGroupLabel(forms[i]) + " / " +
                    ResponsiveDevice.GetFormDisplayName(forms[i]));
            }

            int formIndex = Mathf.Max(0, formChoices.IndexOf(current.form));
            bool hasCatalog = ResponsiveUiEditorSettings.HasCatalogReference(current.form);

            EditorGUI.BeginChangeCheck();
            int nextForm = EditorGUILayout.Popup(
                new GUIContent(
                    "Form Factor",
                    "Which device catalog to pick from (Phone/Pad vs Fold Cover vs Fold/Flip inner). " +
                    "Wide Screen has no catalog and always follows its Design Canvas."),
                formIndex,
                formLabels.ToArray());
            bool follow;
            using (new EditorGUI.DisabledScope(!hasCatalog))
            {
                follow = EditorGUILayout.Toggle(
                    new GUIContent(
                        "Follow Design Canvas Reference",
                        "Use the Design Scale Defaults size for the form factor above (oriented below)."),
                    current.followDesignCanvas);
            }

            ResponsiveDevice.Orientation orientation = (ResponsiveDevice.Orientation)EditorGUILayout.EnumPopup(
                new GUIContent(
                    "Orientation",
                    "Catalog sizes are stored as portrait. Landscape swaps to the device's landscape WxH."),
                current.orientation);
            if (EditorGUI.EndChangeCheck())
            {
                ResponsiveDevice.FormFactor form = formChoices[Mathf.Clamp(nextForm, 0, formChoices.Count - 1)];
                ApplyBackgroundFormChange(settings, current, form, follow, orientation);
                current = ResponsiveUiEditorSettings.GetBackgroundArt();
                resolved = ResponsiveUiEditorSettings.GetBackgroundArtSize();
            }

            if (current.followDesignCanvas)
            {
                EditorGUILayout.LabelField(
                    "Following",
                    $"{ResponsiveUiEditorSettings.GetModelName(current.form)}  ({resolved.x:0}×{resolved.y:0})");
            }
            else
            {
                BuildProfileChoices(current.form, out List<string> ids, out List<string> labels, current.orientation);
                int selected = IndexOfId(ids, current.profileId);
                DrawScrollablePopup(
                    new GUIContent(
                        "Reference Device",
                        "Catalog profile whose screen size becomes the background art size. Searchable and scrollable."),
                    selected,
                    labels.ToArray(),
                    string.IsNullOrEmpty(current.modelName) ? current.profileId : current.modelName,
                    next =>
                    {
                        if (next < 0 || next >= ids.Count || next == selected)
                            return;
                        if (!settings.ApplyBackgroundCatalogProfile(
                            current.form, ids[next], current.orientation, out string error))
                            Debug.LogWarning("[Responsive UI] " + error);
                    });
            }

            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.Vector2Field(
                new GUIContent(
                    "Size",
                    "Resolved from the selected device profile (or Follow Design Canvas)."),
                resolved);
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space(8);
            if (GUILayout.Button("Reset Background Cover Size to iPad Pro 13 M5"))
                settings.ResetBackgroundArtToBuiltin();
        }

        static void DrawHingeGuide()
        {
            EditorGUILayout.Space(16);
            EditorGUILayout.LabelField("Fold Half-Open Guide", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "One Device Simulator overlay for the whole project when Expand Half Opened is selected. " +
                "Not drawn once per ResponsiveUI, and not drawn in Scene view, Game view, or player builds.",
                MessageType.None);
            DrawHingeGuideFields();
        }

        /// <summary>
        /// Project-wide half-open hinge overlay fields (line on / color / thickness). Shared by
        /// this page and the hub Inspector; both write <see cref="ResponsiveUiEditorSettings"/>.
        /// </summary>
        internal static void DrawHingeGuideFields()
        {
            EditorGUI.BeginChangeCheck();
            bool show = EditorGUILayout.Toggle(
                new GUIContent(
                    "Draw Half-Open Hinge Line",
                    "Single Simulator overlay. Independent of how many ResponsiveUI components are in the scene."),
                ResponsiveUiEditorSettings.ShowHalfOpenedHingeGuide);
            Color color = ResponsiveUiEditorSettings.HingeGuideColor;
            float thickness = ResponsiveUiEditorSettings.HingeGuideThicknessPx;
            using (new EditorGUI.DisabledScope(!show))
            {
                color = EditorGUILayout.ColorField(
                    new GUIContent("Hinge Line Color"),
                    color,
                    showEyedropper: true,
                    showAlpha: true,
                    hdr: false);
                thickness = EditorGUILayout.Slider(
                    new GUIContent("Hinge Line Thickness (px)"),
                    thickness,
                    ResponsiveUiHingeGuide.MinThicknessPx,
                    ResponsiveUiHingeGuide.MaxThicknessPx);
            }

            if (EditorGUI.EndChangeCheck())
                ResponsiveUiEditorSettings.instance.SetHingeGuide(show, color, thickness);
        }

        static void ApplyBackgroundFormChange(
            ResponsiveUiEditorSettings settings,
            ResponsiveUiEditorSettings.BackgroundArtDefaults current,
            ResponsiveDevice.FormFactor form,
            bool follow,
            ResponsiveDevice.Orientation orientation)
        {
            if (follow || !ResponsiveUiEditorSettings.HasCatalogReference(form))
            {
                ResponsiveUiEditorSettings.DesignFormDefaults design =
                    ResponsiveUiEditorSettings.GetFormDefaults(form);
                settings.SetBackgroundArt(new ResponsiveUiEditorSettings.BackgroundArtDefaults
                {
                    form = form,
                    followDesignCanvas = true,
                    profileId = string.IsNullOrEmpty(design.profileId)
                        ? ResponsiveUiEditorSettings.GetPreferredProfileId(form)
                        : design.profileId,
                    modelName = ResponsiveUiEditorSettings.GetModelName(form),
                    orientation = orientation,
                    size = ResponsiveUiEditorSettings.GetDesignCanvasSize(form, orientation),
                });
                return;
            }

            if (!string.IsNullOrEmpty(current.profileId) &&
                settings.ApplyBackgroundCatalogProfile(form, current.profileId, orientation, out _))
                return;

            string followId = ResponsiveUiEditorSettings.GetFormDefaults(form).profileId;
            if (!string.IsNullOrEmpty(followId) &&
                settings.ApplyBackgroundCatalogProfile(form, followId, orientation, out _))
                return;

            string preferredId = ResponsiveUiEditorSettings.GetPreferredProfileId(form);
            if (settings.ApplyBackgroundCatalogProfile(form, preferredId, orientation, out _))
                return;

            settings.SetBackgroundArt(ResponsiveUiEditorSettings.BackgroundArtDefaults.CreateBuiltin());
        }

        static void DrawFormDefaults(ResponsiveUiEditorSettings settings, ResponsiveDevice.FormFactor form)
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField(ResponsiveDevice.GetFormDisplayName(form), EditorStyles.boldLabel);

            ResponsiveUiEditorSettings.DesignFormDefaults builtin =
                ResponsiveUiEditorSettings.DesignFormDefaults.CreateBuiltin(form);
            ResponsiveUiEditorSettings.DesignFormDefaults current =
                ResponsiveUiEditorSettings.GetFormDefaults(form);

            if (!ResponsiveUiEditorSettings.HasCatalogReference(form))
            {
                DrawEditableFormDefaults(settings, form, current, builtin);
                return;
            }

            BuildProfileChoices(form, out List<string> ids, out List<string> labels);
            int selected = IndexOfId(ids, current.profileId);
            DrawScrollablePopup(
                new GUIContent(
                    "Reference Device",
                    "Catalog profile used as this form factor's default design canvas. Searchable and scrollable."),
                selected,
                labels.ToArray(),
                current.modelName,
                next =>
                {
                    if (next < 0 || next >= ids.Count || next == selected)
                        return;
                    if (!settings.ApplyCatalogProfile(form, ids[next], out string error))
                        Debug.LogWarning("[Responsive UI] " + error);
                });

            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.TextField(
                new GUIContent("Model Name", "Reference device label from the catalog profile."),
                ResponsiveUiEditorSettings.GetModelName(form));
            EditorGUILayout.Vector2Field(
                new GUIContent(
                    "Design Canvas Size",
                    $"Portrait width × height from the catalog profile. Preferred: {builtin.designCanvasSize.x} × {builtin.designCanvasSize.y}"),
                ResponsiveUiEditorSettings.GetDesignCanvasSize(form));
            EditorGUI.EndDisabledGroup();
            DrawDefaultScale(settings, form, current);
        }

        /// <summary>
        /// Wide Screen: no catalog device, so Model Name and Design Canvas Size are edited here
        /// (or copied once from Pad). The window / browser size drives Design Scale at runtime.
        /// </summary>
        static void DrawEditableFormDefaults(
            ResponsiveUiEditorSettings settings,
            ResponsiveDevice.FormFactor form,
            ResponsiveUiEditorSettings.DesignFormDefaults current,
            ResponsiveUiEditorSettings.DesignFormDefaults builtin)
        {
            EditorGUI.BeginChangeCheck();
            string modelName = EditorGUILayout.DelayedTextField(
                new GUIContent("Model Name", "Label for this design canvas (no catalog device)."),
                ResponsiveUiEditorSettings.GetModelName(form));
            Vector2 canvas = EditorGUILayout.Vector2Field(
                new GUIContent(
                    "Design Canvas Size",
                    "Portrait width × height; the Landscape slot uses it rotated. " +
                    $"Built-in: {builtin.designCanvasSize.x} × {builtin.designCanvasSize.y}"),
                ResponsiveUiEditorSettings.GetDesignCanvasSize(form));
            if (EditorGUI.EndChangeCheck() && !ResponsiveRectState.IsUnsetDesignCanvasSize(canvas))
                SetEditableFormDefaults(settings, form, current, modelName, canvas);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(
                        new GUIContent(
                            "Use Pad Design Canvas",
                            "Copy the Pad Design Canvas Size. Pad-seeded Wide Screen slots then match the hubs' " +
                            "Wide Screen Canvas Scaler refs, which the Pad seed copies from Pad while they are " +
                            "still the built-in default."),
                        GUILayout.Width(170f)))
                {
                    SetEditableFormDefaults(
                        settings,
                        form,
                        current,
                        "Wide Screen (Pad canvas)",
                        ResponsiveUiEditorSettings.GetDesignCanvasSize(ResponsiveDevice.FormFactor.Pad));
                }
            }

            DrawDefaultScale(settings, form, ResponsiveUiEditorSettings.GetFormDefaults(form));
        }

        static void SetEditableFormDefaults(
            ResponsiveUiEditorSettings settings,
            ResponsiveDevice.FormFactor form,
            ResponsiveUiEditorSettings.DesignFormDefaults current,
            string modelName,
            Vector2 canvas)
        {
            settings.SetFormDefaults(
                form,
                new ResponsiveUiEditorSettings.DesignFormDefaults
                {
                    modelName = modelName,
                    designCanvasSize = canvas,
                    defaultScale = current.defaultScale,
                });
        }

        static void DrawDefaultScale(
            ResponsiveUiEditorSettings settings,
            ResponsiveDevice.FormFactor form,
            ResponsiveUiEditorSettings.DesignFormDefaults current)
        {
            EditorGUI.BeginChangeCheck();
            Vector2 scale = EditorGUILayout.Vector2Field(
                new GUIContent("Default Scale", "Extra multiplier (usually 1,1)."),
                current.defaultScale);
            if (!EditorGUI.EndChangeCheck())
                return;

            if (ApproxVec(scale, current.defaultScale))
                return;

            settings.SetFormDefaults(
                form,
                new ResponsiveUiEditorSettings.DesignFormDefaults
                {
                    profileId = current.profileId,
                    modelName = current.modelName,
                    designCanvasSize = current.designCanvasSize,
                    defaultScale = scale,
                });
        }

        static string PlatformDisplayName(ResponsivePlatform platform) =>
            platform switch
            {
                ResponsivePlatform.Android => "Android (Phone / Pad / Fold / Flip)",
                ResponsivePlatform.Ios => "iOS (iPhone / iPad / iPhone Duo)",
                _ => "Wide Screen (Windows / macOS / Linux / WebGL)",
            };

        static bool ApproxVec(Vector2 a, Vector2 b) =>
            Mathf.Approximately(a.x, b.x) && Mathf.Approximately(a.y, b.y);

        static int IndexOfId(List<string> ids, string id)
        {
            if (ids == null || string.IsNullOrEmpty(id))
                return -1;
            for (int i = 0; i < ids.Count; i++)
            {
                if (ids[i] == id)
                    return i;
            }

            return -1;
        }

        static void BuildProfileChoices(
            ResponsiveDevice.FormFactor form,
            out List<string> ids,
            out List<string> labels,
            ResponsiveDevice.Orientation orientation = ResponsiveDevice.Orientation.Portrait)
        {
            ids = new List<string>();
            labels = new List<string>();

            if (ResponsiveUiEditorSettings.IsPhonePadForm(form))
            {
                var devices = PhonePadDeviceCatalog.Devices;
                if (devices == null)
                    return;
                for (int i = 0; i < devices.Length; i++)
                    AddProfileChoice(form, devices[i]?.Id, devices[i]?.ShortName, orientation, ids, labels);
                return;
            }

            var foldDevices = FoldableDeviceCatalog.Devices;
            if (foldDevices == null)
                return;
            for (int i = 0; i < foldDevices.Length; i++)
                AddProfileChoice(form, foldDevices[i]?.Id, foldDevices[i]?.ShortName, orientation, ids, labels);
        }

        /// <summary>Kind / family filtering is <see cref="ResponsiveUiEditorSettings.ResolveCatalogSize"/>.</summary>
        static void AddProfileChoice(
            ResponsiveDevice.FormFactor form,
            string id,
            string shortName,
            ResponsiveDevice.Orientation orientation,
            List<string> ids,
            List<string> labels)
        {
            if (string.IsNullOrEmpty(id) ||
                !ResponsiveUiEditorSettings.ResolveCatalogSize(
                    form, id, orientation, out _, out Vector2 canvas, out _))
                return;
            ids.Add(id);
            labels.Add($"{shortName}  ({canvas.x:0}×{canvas.y:0})");
        }

        static void DrawScrollablePopup(
            GUIContent label,
            int selected,
            string[] labels,
            string shownFallback,
            Action<int> onPicked)
        {
            Rect row = EditorGUILayout.GetControlRect();
            Rect field = EditorGUI.PrefixLabel(row, label);
            string shown = selected >= 0 && selected < labels.Length
                ? labels[selected]
                : (shownFallback ?? "");
            if (!EditorGUI.DropdownButton(field, new GUIContent(shown), FocusType.Keyboard, EditorStyles.popup))
                return;
            if (labels == null || labels.Length == 0)
                return;

            var dropdown = new ScrollableStringDropdown(
                new AdvancedDropdownState(),
                labels,
                field.width,
                onPicked);
            dropdown.Show(field);
        }

        sealed class ScrollableStringDropdown : AdvancedDropdown
        {
            readonly string[] labels;
            readonly Action<int> onPicked;

            public ScrollableStringDropdown(
                AdvancedDropdownState state,
                string[] labels,
                float width,
                Action<int> onPicked)
                : base(state)
            {
                this.labels = labels;
                this.onPicked = onPicked;
                minimumSize = new Vector2(Mathf.Max(width, 240f), 360f);
            }

            protected override AdvancedDropdownItem BuildRoot()
            {
                var root = new AdvancedDropdownItem("Reference Device");
                for (int i = 0; i < labels.Length; i++)
                {
                    string name = (labels[i] ?? "").Replace("/", "\u2215");
                    root.AddChild(new IndexedItem(name, i));
                }

                return root;
            }

            protected override void ItemSelected(AdvancedDropdownItem item)
            {
                if (item is IndexedItem indexed)
                    onPicked(indexed.Index);
            }

            sealed class IndexedItem : AdvancedDropdownItem
            {
                public readonly int Index;

                public IndexedItem(string name, int index) : base(name)
                {
                    Index = index;
                    id = index;
                }
            }
        }

        #endregion
    }
}

#endif
