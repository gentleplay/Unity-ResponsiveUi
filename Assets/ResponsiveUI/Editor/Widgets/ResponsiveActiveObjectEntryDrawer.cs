#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

using FDPlus.ResponsiveUI;

namespace FDPlus.ResponsiveUI.Editor
{
/// <summary>
/// One Active Object row: Target plus Portrait / Landscape toggles per form factor.
/// Show Other Slots / Allowed Orientations match widget foldouts.
/// </summary>
[CustomPropertyDrawer(typeof(ResponsiveActiveObjectEntry))]
public class ResponsiveActiveObjectEntryDrawer : PropertyDrawer
{
    #region Constants

    static readonly GUIContent ObjectsLabel = new GUIContent("Objects");
    static readonly GUIContent TargetLabel = new GUIContent(
        "Target",
        "Shown when a checked slot is the live layout; hidden when that slot is off.");

    #endregion

    #region Inspector

    public static void DrawSection(SerializedObject so)
    {
        ResponsiveUiInspectorFoldouts.ColoredFoldoutSection(
            ResponsiveUiInspectorFoldouts.InspectorSectionKey(
                so.targetObject, "Controller", "ActiveObjects"),
            "Active Objects",
            null,
            ResponsiveUiInspectorFoldouts.TintTeal,
            () =>
            {
                EditorGUILayout.HelpBox(
                    "Each object is shown when its current form × orientation slot is on, hidden when off. " +
                    "Unlisted objects are left alone. Show Other Slots (Detection) lists every allowed form.",
                    MessageType.None);
                SerializedProperty list = so.FindProperty("activeObjects");
                list.isExpanded = true;
                EditorGUILayout.PropertyField(list, ObjectsLabel, true);
            },
            defaultOpen: true);
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        return EditorGUIUtility.singleLineHeight +
               ResponsiveActiveObjectSlotsEditorUtil.FormRowsHeight(property);
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        float line = EditorGUIUtility.singleLineHeight;
        SerializedProperty targetProp = property.FindPropertyRelative("target");
        SerializedProperty slots = property.FindPropertyRelative("slots");

        Rect row = new Rect(position.x, position.y, position.width, line);
        Object previous = targetProp.objectReferenceValue;
        EditorGUI.BeginChangeCheck();
        EditorGUI.PropertyField(row, targetProp, TargetLabel);
        if (EditorGUI.EndChangeCheck() &&
            previous == null &&
            targetProp.objectReferenceValue != null &&
            ResponsiveActiveObjectSlotsEditorUtil.AllOff(slots))
            SeedAllowed(property);

        float y = position.y + line;
        ResponsiveUiInspectorFoldouts.DrawNestedContent(position, forms =>
            ResponsiveActiveObjectSlotsEditorUtil.DrawFormRows(forms, y, property, slots));

        EditorGUI.EndProperty();
    }

    #endregion

    #region Seed

    public static void SeedAllowed(SerializedProperty entry)
    {
        SerializedProperty slots = entry.FindPropertyRelative("slots");
        string[] names = ResponsiveWidgetLayout.StatePropertyNames;
        for (int i = 0; i < names.Length; i++)
            ResponsiveActiveObjectSlotsEditorUtil.WriteSlot(
                slots, i, ResponsiveUiEditorSettings.SlotAllowed(i));
    }

    public static void ResetNewListRow(SerializedProperty entry)
    {
        entry.FindPropertyRelative("target").objectReferenceValue = null;
        SeedAllowed(entry);
    }

    public static bool SeedNewRows(SerializedProperty list, ref int lastCount) =>
        SeedNewRows(
            list,
            ref lastCount,
            ResponsiveUiDetectionDrawer.CanMutateInspectorLayout());

    public static bool SeedNewRows(
        SerializedProperty list,
        ref int lastCount,
        bool canMutate) =>
        ResponsiveRectStateEditorUtil.ForEachNewRow(
            list, ref lastCount, ResetNewListRow, canMutate);

    #endregion
}
}

#endif
