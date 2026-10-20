#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

using FDPlus.ResponsiveUI;

namespace FDPlus.ResponsiveUI.Editor
{
/// <summary>
/// Inspector for the Canvas <see cref="ResponsiveUI"/> hub:
/// Detection, Events, Canvas Scaler, hinge overlay, debug, background-size override.
/// </summary>
[CustomEditor(typeof(ResponsiveUI))]
public class ResponsiveUIEditor : UnityEditor.Editor
{
    #region Inspector

    public override void OnInspectorGUI()
    {
        var ui = target as ResponsiveUI;
        if (ui != null && !ui.enabled)
        {
            EditorGUILayout.HelpBox(
                "ResponsiveUI hub is disabled — detection, events, and client Apply will not run until you enable it.\n\n" +
                "Add ResponsiveUiController on this Canvas or a child for widgets / backgrounds / Active Objects.",
                MessageType.Warning);
        }

        if (ui != null && ui.GetComponentInParent<Canvas>(true) == null)
        {
            EditorGUILayout.HelpBox(
                "No Canvas in parents. Add this hub to the Main Canvas (nested canvases are OK).",
                MessageType.Warning);
        }

        serializedObject.Update();

        ResponsiveUiInspectorFoldouts.ColoredFoldoutSection(
            InspectorKey("Detection"),
            ResponsiveUiDetectionDrawer.SectionTitle(serializedObject, ui),
            null,
            ResponsiveUiInspectorFoldouts.TintBlue,
            () => ResponsiveUiDetectionDrawer.Draw(serializedObject, ui));
        DrawScalerSection();
        ResponsiveLayoutEventsDrawer.DrawSection(serializedObject);
        DrawHingeSection();
        ResponsiveUiInspectorFoldouts.ColoredFoldoutSection(
            InspectorKey("Debug"),
            "Debug",
            null,
            ResponsiveUiInspectorFoldouts.TintSlate,
            () => ResponsiveUiInspectorFoldouts.Property(serializedObject, "logDetection"));
            ResponsiveUiInspectorFoldouts.ColoredFoldoutSection(
            InspectorKey("Layout"),
            "Layout",
            null,
            ResponsiveUiInspectorFoldouts.TintGreen,
            () =>
            {
                DrawBackgroundSize(serializedObject);
                EditorGUILayout.PropertyField(
                    serializedObject.FindProperty("formTransitionLoading"),
                    includeChildren: true);
            },
            defaultOpen: true);

        serializedObject.ApplyModifiedProperties();
    }

    #endregion

    #region Helpers

    void DrawBackgroundSize(SerializedObject so)
    {
        SerializedProperty overrideProp = so.FindProperty("overrideOriginalBgSize");
        SerializedProperty sizeProp = so.FindProperty("originalBgSize");
        if (overrideProp == null || sizeProp == null)
            return;

        Vector2 projectSize = ResponsiveUI.GetDefaultOriginalBgSize();
        EditorGUI.BeginChangeCheck();
        EditorGUILayout.PropertyField(
            overrideProp,
            new GUIContent(
                "Override Background Size",
                "When off, this component follows Project Settings → Responsive UI → Background Cover Size."));
        if (EditorGUI.EndChangeCheck() && !overrideProp.boolValue)
            sizeProp.vector2Value = projectSize;

        using (new EditorGUI.DisabledScope(!overrideProp.boolValue))
        {
            EditorGUILayout.PropertyField(
                sizeProp,
                new GUIContent("Original Bg Size", "Designed pixel size of the background art."));
        }

        // Override off: ResponsiveUI.OnValidate and ResponsiveUiBackgroundSizeWatch bake the
        // Project Settings size; the Inspector only shows it.
        if (overrideProp.boolValue)
            return;

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.HelpBox(
            $"From Project Settings → Responsive UI: {projectSize.x:0}×{projectSize.y:0}. " +
            "Changing the catalog device there updates every ResponsiveUI that does not Override.",
            MessageType.None);
        if (GUILayout.Button("Open", GUILayout.Width(52f), GUILayout.Height(38f)))
            SettingsService.OpenProjectSettings("Project/Responsive UI");
        EditorGUILayout.EndHorizontal();
    }

    void DrawScalerSection()
    {
        ResponsiveUiInspectorFoldouts.ColoredFoldoutSection(
            InspectorKey("Scaler"),
            "Optional Canvas Scaler",
            null,
            ResponsiveUiInspectorFoldouts.TintPurple,
            () =>
            {
                ResponsiveUiInspectorFoldouts.Property(serializedObject, "applyCanvasScaler");
                ResponsiveUiInspectorFoldouts.Property(serializedObject, "canvasScaler");
                // Phone / Pad refs on every target (Android and iOS slots). Wide Screen refs on its targets.
                if (ResponsiveUiEditorSettings.FormVisible(ResponsiveDevice.FormFactor.Phone))
                {
                    ResponsiveUiInspectorFoldouts.Property(serializedObject, "phoneReferenceResolution");
                    ResponsiveUiInspectorFoldouts.Property(serializedObject, "padReferenceResolution");
                    ResponsiveUiInspectorFoldouts.Property(serializedObject, "phoneMatchWidthOrHeight");
                    ResponsiveUiInspectorFoldouts.Property(serializedObject, "padMatchWidthOrHeight");
                }
                if (ResponsiveUiEditorSettings.FormVisible(ResponsiveDevice.FormFactor.WideScreen))
                {
                    ResponsiveUiInspectorFoldouts.Property(serializedObject, "wideScreenReferenceResolution");
                    ResponsiveUiInspectorFoldouts.Property(serializedObject, "wideScreenMatchWidthOrHeight");
                }
                EditorGUILayout.HelpBox(
                    "These scaler refs are authored for this Canvas and are not overwritten " +
                    "by Design Canvas. Widget slots and cameras use Project Settings Design Canvas.",
                    MessageType.None);
            });
    }

    string InspectorKey(string section) =>
        ResponsiveUiInspectorFoldouts.InspectorSectionKey(target, "Hub", section);

    void DrawHingeSection()
    {
        ResponsiveUiInspectorFoldouts.ColoredFoldoutSection(
            InspectorKey("Hinge"),
            "Fold Half-Open Guide",
            null,
            ResponsiveUiInspectorFoldouts.TintAmber,
            () =>
            {
                EditorGUILayout.HelpBox(
                    "One Device Simulator overlay for the project when Expand Half Opened is selected. " +
                    "Writes Project Settings → Responsive UI (not a per-controller line).",
                    MessageType.None);
                ResponsiveUiSettingsProvider.DrawHingeGuideFields();
            });
    }

    #endregion
}
}

#endif
