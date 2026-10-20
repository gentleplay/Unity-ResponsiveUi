#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

using FDPlus.ResponsiveUI;

namespace FDPlus.ResponsiveUI.Editor
{
/// <summary>
/// Inspector for <see cref="ResponsiveTextBanner"/>: hub, targets,
/// Left Middle / Right Up / … alignment (writes RectTransform pivot), and display options.
/// </summary>
[CustomEditor(typeof(ResponsiveTextBanner))]
public class ResponsiveTextBannerEditor : UnityEditor.Editor
{
    #region Constants

    static readonly ResponsiveTextBanner.Align[] AlignmentGrid =
    {
        ResponsiveTextBanner.Align.LeftUp,
        ResponsiveTextBanner.Align.CenterUp,
        ResponsiveTextBanner.Align.RightUp,
        ResponsiveTextBanner.Align.LeftMiddle,
        ResponsiveTextBanner.Align.CenterMiddle,
        ResponsiveTextBanner.Align.RightMiddle,
        ResponsiveTextBanner.Align.LeftDown,
        ResponsiveTextBanner.Align.CenterDown,
        ResponsiveTextBanner.Align.RightDown,
    };

    static readonly GUIContent AlignmentLabel = new GUIContent(
        "Alignment",
        "Which point stays put when text width changes. Writes the banner RectTransform pivot.");

    static readonly GUIContent[] AlignmentButtons =
    {
        Point("Left Up", "top-left stays put (pivot 0, 1). Width grows right."),
        Point("Center Up", "top-center stays put (pivot 0.5, 1). Width grows both ways."),
        Point("Right Up", "top-right stays put (pivot 1, 1). Width grows left."),
        Point("Left Middle", "left edge stays put (pivot 0, 0.5). Width grows right."),
        Point("Center Middle", "center stays put (pivot 0.5, 0.5). Width grows both ways."),
        Point("Right Middle", "right edge stays put (pivot 1, 0.5). Width grows left."),
        Point("Left Down", "bottom-left stays put (pivot 0, 0). Width grows right."),
        Point("Center Down", "bottom-center stays put (pivot 0.5, 0). Width grows both ways."),
        Point("Right Down", "bottom-right stays put (pivot 1, 0). Width grows left."),
    };

    #endregion

    #region Inspector

    public override void OnInspectorGUI()
    {
        var banner = target as ResponsiveTextBanner;
        serializedObject.Update();
        ResponsiveUiInspectorFoldouts.EnsureHubField(serializedObject, banner);

        ResponsiveUiInspectorFoldouts.DrawHubField(serializedObject);

        ResponsiveUiInspectorFoldouts.ColoredFoldoutSection(
            InspectorKey("Targets"),
            "Targets",
            null,
            ResponsiveUiInspectorFoldouts.TintTeal,
            () =>
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("banner"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("text"));
            },
            defaultOpen: true);

        ResponsiveUiInspectorFoldouts.ColoredFoldoutSection(
            InspectorKey("Sizing"),
            "Sizing",
            null,
            ResponsiveUiInspectorFoldouts.TintGreen,
            () =>
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("textPadding"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("boxPadding"));
                DrawAlignmentGrid(serializedObject.FindProperty("alignment"));
            },
            defaultOpen: true);

        ResponsiveUiInspectorFoldouts.ColoredFoldoutSection(
            InspectorKey("Display"),
            "Display",
            null,
            ResponsiveUiInspectorFoldouts.TintAmber,
            () =>
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("displayDuration"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("clearTextOnHide"));
            },
            defaultOpen: true);

        if (serializedObject.ApplyModifiedProperties() && banner != null)
            banner.RefreshLayout();

        EditorGUILayout.Space();
        ResponsiveUiInspectorFoldouts.ColoredFoldoutSection(
            InspectorKey("Preview"),
            "Preview",
            null,
            ResponsiveUiInspectorFoldouts.TintPurple,
            () =>
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Preview") && banner != null)
                {
                    Undo.RecordObject(banner, "Preview Text Banner");
                    banner.SetPreviewLock(true);
                    banner.Show("Preview", autoHide: false);
                    EditorUtility.SetDirty(banner);
                }
                if (GUILayout.Button("Hide") && banner != null)
                {
                    Undo.RecordObject(banner, "Hide Text Banner");
                    banner.SetPreviewLock(false);
                    EditorUtility.SetDirty(banner);
                }
                EditorGUILayout.EndHorizontal();
            },
            defaultOpen: true);
    }

    #endregion

    #region Helpers

    string InspectorKey(string section) =>
        ResponsiveUiInspectorFoldouts.InspectorSectionKey(target, "Banner", section);

    static GUIContent Point(string name, string detail) =>
        new GUIContent(string.Empty, name + " — " + detail);

    static void DrawAlignmentGrid(SerializedProperty property)
    {
        const float gap = 2f;
        float cell = EditorGUIUtility.singleLineHeight;
        float gridSize = 3f * cell + 2f * gap;
        Rect row = EditorGUILayout.GetControlRect(true, gridSize);
        EditorGUI.BeginProperty(row, AlignmentLabel, property);
        Rect field = EditorGUI.PrefixLabel(row, AlignmentLabel);

        Rect grid = new Rect(field.x, field.y, gridSize, gridSize);
        int current = property.intValue;
        for (int i = 0; i < AlignmentGrid.Length; i++)
        {
            int col = i % 3;
            int buttonRow = i / 3;
            Rect button = new Rect(
                grid.x + col * (cell + gap),
                grid.y + buttonRow * (cell + gap),
                cell,
                cell);
            bool on = !property.hasMultipleDifferentValues &&
                      current == (int)AlignmentGrid[i];
            if (GUI.Toggle(button, on, AlignmentButtons[i], EditorStyles.miniButton) && !on)
                property.intValue = (int)AlignmentGrid[i];
        }

        float nameX = grid.xMax + EditorGUIUtility.standardVerticalSpacing + 4f;
        var nameRect = new Rect(
            nameX,
            field.y,
            Mathf.Max(0f, field.xMax - nameX),
            cell);
        if (nameRect.width >= 48f)
        {
            string name = property.hasMultipleDifferentValues
                ? "—"
                : ObjectNames.NicifyVariableName(
                    AlignmentGrid[Mathf.Clamp(current, 0, AlignmentGrid.Length - 1)]
                        .ToString());
            EditorGUI.LabelField(nameRect, name);
        }

        EditorGUI.EndProperty();
    }

    #endregion
}
}
#endif
