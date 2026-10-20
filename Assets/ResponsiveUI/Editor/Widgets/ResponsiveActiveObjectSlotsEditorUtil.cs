#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

using FDPlus.ResponsiveUI;

namespace FDPlus.ResponsiveUI.Editor
{
/// <summary>
/// Shared <see cref="ResponsiveActiveObjectSlots"/> toggle grid (one row per form, P / L toggles)
/// for the Active Objects row drawer and Form Transition Loading Show On Slots.
/// </summary>
public static class ResponsiveActiveObjectSlotsEditorUtil
{
    #region Constants

    const float ToggleWidth = 28f;

    static readonly GUIContent PortraitLabel = new GUIContent("P", "Portrait");
    static readonly GUIContent LandscapeLabel = new GUIContent("L", "Landscape");

    static readonly ResponsiveDevice.FormFactor[] FormFactors =
        ResponsiveWidgetLayout.FormFactors;

    #endregion

    #region Slots

    public static bool ReadSlot(SerializedProperty slots, int slotIndex) =>
        slots.FindPropertyRelative(
            ResponsiveWidgetLayout.StatePropertyNames[slotIndex]).boolValue;

    public static void WriteSlot(SerializedProperty slots, int slotIndex, bool value) =>
        slots.FindPropertyRelative(
            ResponsiveWidgetLayout.StatePropertyNames[slotIndex]).boolValue = value;

    public static bool AllOff(SerializedProperty slots)
    {
        string[] names = ResponsiveWidgetLayout.StatePropertyNames;
        for (int i = 0; i < names.Length; i++)
        {
            if (ReadSlot(slots, i))
                return false;
        }
        return true;
    }

    #endregion

    #region Grid

    /// <summary>Height of the drawn form rows (spacing + line each), excluding any header.</summary>
    public static float FormRowsHeight(SerializedProperty property)
    {
        float line = EditorGUIUtility.singleLineHeight;
        float s = EditorGUIUtility.standardVerticalSpacing;
        float h = 0f;
        string group = "";
        for (int form = 0; form < FormFactors.Length; form++)
        {
            if (!ShouldDrawForm(property, form, out _, out _))
                continue;
            h += ResponsiveSlotGroupUi.Advance(ref group, form * 2, default, false);
            h += s + line;
        }
        return h;
    }

    /// <summary>
    /// Draws every visible form row below <paramref name="y"/> (spacing before each row).
    /// <paramref name="property"/> decides visibility (Show Other Slots / Allowed Orientations).
    /// </summary>
    public static void DrawFormRows(
        Rect area,
        float y,
        SerializedProperty property,
        SerializedProperty slots)
    {
        float line = EditorGUIUtility.singleLineHeight;
        float s = EditorGUIUtility.standardVerticalSpacing;
        string detected = ResponsiveUiDetectionDrawer.ResolveFoldoutSlotName(
            property.serializedObject,
            ResponsiveUiDetectionDrawer.HandlerOf(property.serializedObject));
        string group = "";
        for (int form = 0; form < FormFactors.Length; form++)
        {
            if (!ShouldDrawForm(property, form, out bool drawP, out bool drawL))
                continue;
            y += ResponsiveSlotGroupUi.Advance(
                ref group, form * 2, new Rect(area.x, y, area.width, line), true);
            y += s;
            DrawFormRow(
                new Rect(area.x, y, area.width, line),
                slots,
                form,
                detected,
                drawP,
                drawL);
            y += line;
        }
    }

    static bool ShouldDrawForm(
        SerializedProperty property,
        int form,
        out bool drawP,
        out bool drawL)
    {
        int portrait = form * 2;
        string[] names = ResponsiveWidgetLayout.StatePropertyNames;
        drawP = ResponsiveUiDetectionDrawer.ShouldDrawState(
            property.serializedObject, names[portrait]);
        drawL = ResponsiveUiDetectionDrawer.ShouldDrawState(
            property.serializedObject, names[portrait + 1]);
        return drawP || drawL;
    }

    static void DrawFormRow(
        Rect row,
        SerializedProperty slots,
        int form,
        string detected,
        bool drawP,
        bool drawL)
    {
        int portrait = form * 2;
        string[] names = ResponsiveWidgetLayout.StatePropertyNames;
        float toggles = 0f;
        if (drawP)
            toggles += ToggleWidth;
        if (drawL)
            toggles += ToggleWidth;
        Rect labelRect = new Rect(row.x, row.y, Mathf.Max(16f, row.width - toggles), row.height);
        bool isDetected =
            names[portrait] == detected || names[portrait + 1] == detected;
        EditorGUI.LabelField(
            labelRect,
            ResponsiveDevice.GetFormDisplayName(FormFactors[form]),
            isDetected ? EditorStyles.boldLabel : EditorStyles.label);

        float x = row.xMax;
        if (drawL)
        {
            x -= ToggleWidth;
            ToggleSlot(
                new Rect(x, row.y, ToggleWidth, row.height),
                slots,
                portrait + 1,
                LandscapeLabel);
        }
        if (drawP)
        {
            x -= ToggleWidth;
            ToggleSlot(
                new Rect(x, row.y, ToggleWidth, row.height),
                slots,
                portrait,
                PortraitLabel);
        }
    }

    static void ToggleSlot(Rect rect, SerializedProperty slots, int slotIndex, GUIContent label)
    {
        bool current = ReadSlot(slots, slotIndex);
        EditorGUI.BeginChangeCheck();
        bool next = EditorGUI.ToggleLeft(rect, label, current);
        if (EditorGUI.EndChangeCheck())
            WriteSlot(slots, slotIndex, next);
    }

    #endregion
}
}

#endif
