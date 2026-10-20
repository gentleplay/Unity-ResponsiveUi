#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

using FDPlus.ResponsiveUI;

namespace FDPlus.ResponsiveUI.Editor
{
/// <summary>
/// Draws <see cref="ResponsiveScrollLayout"/> slots. Detected Simulator slot auto-expands;
/// allowed-orientation siblings stay listed. Enabling a slot copies
/// <see cref="ResponsiveRectStateEditorUtil.FindFallbackSource"/>.
/// Active-slot edits LivePush to the host FancyScrollView <c>cellInterval</c> / <c>scrollOffset</c>.
/// </summary>
[CustomPropertyDrawer(typeof(ResponsiveScrollLayout))]
public class ResponsiveScrollLayoutDrawer : PropertyDrawer
{
    #region Inspector

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        ResponsiveUiDetectionDrawer.ApplyDetectedFoldouts(property);

        float line = EditorGUIUtility.singleLineHeight;
        float s = EditorGUIUtility.standardVerticalSpacing;
        float h = line + s; // detected slot
        string[] states = ResponsiveScrollLayout.StatePropertyNames;
        string group = "";
        for (int i = 0; i < states.Length; i++)
        {
            if (!ResponsiveUiDetectionDrawer.ShouldDrawState(property.serializedObject, states[i]))
                continue;

            h += ResponsiveSlotGroupUi.Advance(ref group, i, default, false);
            h += EditorGUI.GetPropertyHeight(property.FindPropertyRelative(states[i]), true) + s;
        }

        return h;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        ResponsiveUiDetectionDrawer.ApplyDetectedFoldouts(property);

        ResponsiveDevice.LayoutKey layout =
            ResponsiveUiDetectionDrawer.ResolveInspectorLayout(property.serializedObject);

        float line = EditorGUIUtility.singleLineHeight;
        float s = EditorGUIUtility.standardVerticalSpacing;
        float y = position.y;

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUI.LabelField(
                new Rect(position.x, y, position.width, line),
                new GUIContent(
                    "Detected Slot",
                    "Device Simulator / fold product state / Screen. That slot stays expanded."),
                new GUIContent(
                    ObjectNames.NicifyVariableName(
                        ResponsiveUiDetectionDrawer.ResolveFoldoutSlotName(
                            property.serializedObject))));
        }

        y += line + s;
        string[] states = ResponsiveScrollLayout.StatePropertyNames;
        string group = "";
        for (int i = 0; i < states.Length; i++)
        {
            if (!ResponsiveUiDetectionDrawer.ShouldDrawState(property.serializedObject, states[i]))
                continue;

            y += ResponsiveSlotGroupUi.Advance(
                ref group, i, new Rect(position.x, y, position.width, line), true);

            SerializedProperty stateProp = property.FindPropertyRelative(states[i]);
            float sh = EditorGUI.GetPropertyHeight(stateProp, true);
            ResponsiveScrollState before = ResponsiveScrollEditorUtil.ReadState(stateProp);
            EditorGUI.BeginChangeCheck();
            EditorGUI.PropertyField(
                new Rect(position.x, y, position.width, sh),
                stateProp,
                new GUIContent(ResponsiveWidgetLayout.StatePropertyLabel(i)),
                true);
            bool changed = EditorGUI.EndChangeCheck() ||
                property.serializedObject.hasModifiedProperties;
            if (changed)
            {
                ResponsiveScrollState after = ResponsiveScrollEditorUtil.ReadState(stateProp);
                if (!ResponsiveScrollEditorUtil.SameAuthoring(before, after))
                {
                    // Enabled flip can move the Apply slot (disable → fallback,
                    // enable → this slot); push whichever slot now resolves.
                    if (before.enabled != after.enabled)
                        ResponsiveScrollEditorUtil.PushResolvedSlotToHost(property);
                    else if (after.enabled &&
                        ResponsiveRectStateEditorUtil.IsActiveEditorSlot(property, layout, states[i]))
                        ResponsiveScrollEditorUtil.CopySlotToHost(property, stateProp);
                }
            }

            y += sh + s;
        }

        EditorGUI.EndProperty();
    }

    #endregion
}
}

#endif
