#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

using FDPlus.ResponsiveUI;

namespace FDPlus.ResponsiveUI.Editor
{
    /// <summary>
    /// Events section: one UnityEvent per <see cref="ResponsiveWidgetLayout.FormFactors"/>
    /// form (Phone … Wide Screen) × Portrait / Landscape slot.
    /// Show Other Slots / Allowed Orientations match widget slots.
    /// </summary>
    public static class ResponsiveLayoutEventsDrawer
    {
        #region Inspector

        public static void DrawSection(SerializedObject so)
        {
            ResponsiveUiInspectorFoldouts.ColoredFoldoutSection(
                ResponsiveUiInspectorFoldouts.InspectorSectionKey(
                    so.targetObject, "Hub", "Events"),
                "Events",
                null,
                ResponsiveUiInspectorFoldouts.TintTeal,
                () =>
                {
                    EditorGUILayout.HelpBox(
                        "Play Mode Apply fires the matching form × orientation event. " +
                        "Show Other Slots (Detection) lists every allowed slot.",
                        MessageType.None);
                    SerializedProperty events = so.FindProperty("layoutEvents");
                    string[] names = ResponsiveWidgetLayout.StatePropertyNames;
                    string group = "";
                    for (int i = 0; i < names.Length; i++)
                    {
                        if (!ResponsiveUiDetectionDrawer.ShouldDrawState(so, names[i]))
                            continue;
                        ResponsiveSlotGroupUi.LayoutHeader(
                            ref group, ResponsiveWidgetLayout.SlotFormFactor(i));
                        SerializedProperty slot = events.FindPropertyRelative(names[i]);
                        EditorGUILayout.PropertyField(
                            slot,
                            new GUIContent(ResponsiveWidgetLayout.StatePropertyLabel(i)),
                            true);
                    }
                });
        }

        #endregion
    }
}

#endif
