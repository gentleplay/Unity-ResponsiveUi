#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

using FDPlus.ResponsiveUI;

namespace FDPlus.ResponsiveUI.Editor
{
/// <summary>
/// Draws <see cref="ResponsiveScrollState"/>: Enabled, Cell Interval, Scroll Offset.
/// Detected slot auto-expands; other slots stay listed so they can be opened and edited.
/// </summary>
[CustomPropertyDrawer(typeof(ResponsiveScrollState))]
public class ResponsiveScrollStateDrawer : PropertyDrawer
{
    #region Inspector

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float line = EditorGUIUtility.singleLineHeight;
        float s = EditorGUIUtility.standardVerticalSpacing;

        property.isExpanded = ResponsiveUiDetectionDrawer.ShouldExpandState(property);
        if (!property.isExpanded)
            return line;

        float h = line + s + line; // foldout + enabled
        if (property.FindPropertyRelative("enabled")?.boolValue != true)
            return h;

        h += ResponsiveUiInspectorFoldouts.SlotSectionHeight(
            property, "ScrollSlot", "Metrics", true, line * 2f + s);
        return h;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        float line = EditorGUIUtility.singleLineHeight;
        float s = EditorGUIUtility.standardVerticalSpacing;

        Rect row = new Rect(position.x, position.y, position.width, line);
        bool expanded = ResponsiveUiSlotClipboard.DrawFoldoutRow(
            row, property, label, ResponsiveUiSlotClipboard.Kind.Scroll);
        if (!expanded)
        {
            EditorGUI.EndProperty();
            return;
        }

        ResponsiveUiInspectorFoldouts.DrawNestedContent(position, content =>
        {
            float y = position.y + line + s;

            SerializedProperty enabled = property.FindPropertyRelative("enabled");
            if (enabled != null)
            {
                using (new EditorGUI.DisabledScope(ResponsiveUiEditorSettings.LockEnabledToggles))
                {
                    bool wasEnabled = enabled.boolValue;
                    EditorGUI.PropertyField(new Rect(content.x, y, content.width, line), enabled);
                    if (!wasEnabled && enabled.boolValue)
                    {
                        ResponsiveScrollEditorUtil.CopyFallback(property);
                        ResponsiveScrollEditorUtil.LivePushIfActive(property);
                    }
                }
                y += line;
            }

            if (enabled == null || !enabled.boolValue)
                return;

            ResponsiveUiInspectorFoldouts.DrawSlotSection(
                content,
                y,
                line * 2f + s,
                property,
                "ScrollSlot",
                "Metrics",
                "Metrics",
                MetricsSummary(property),
                ResponsiveUiInspectorFoldouts.TintBlue,
                true,
                body =>
                {
                    float by = body.y;
                    DrawClampedSlider(
                        new Rect(body.x, by, body.width, line),
                        property,
                        property.FindPropertyRelative("cellInterval"),
                        "Cell Interval",
                        ResponsiveScrollState.MinCellInterval,
                        1f);
                    DrawClampedSlider(
                        new Rect(body.x, by + line + s, body.width, line),
                        property,
                        property.FindPropertyRelative("scrollOffset"),
                        "Scroll Offset",
                        0f,
                        1f);
                });
        });
        EditorGUI.EndProperty();
    }

    #endregion

    #region Helpers

    static string MetricsSummary(SerializedProperty property)
    {
        float interval = property.FindPropertyRelative("cellInterval")?.floatValue ?? 0f;
        float offset = property.FindPropertyRelative("scrollOffset")?.floatValue ?? 0f;
        return $"{interval:0.##}  ·  {offset:0.##}";
    }

    static void DrawClampedSlider(
        Rect rect,
        SerializedProperty stateProp,
        SerializedProperty prop,
        string label,
        float min,
        float max)
    {
        if (prop == null)
            return;

        EditorGUI.BeginChangeCheck();
        float value = prop.floatValue;
        if (min > 0f && value <= 0f)
            value = ResponsiveScrollState.DefaultCellInterval;
        value = EditorGUI.Slider(rect, new GUIContent(label), value, min, max);
        if (EditorGUI.EndChangeCheck())
        {
            prop.floatValue = Mathf.Clamp(value, min, max);
            ResponsiveScrollEditorUtil.LivePushIfActive(stateProp);
        }
    }

    #endregion
}
}

#endif
