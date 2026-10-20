#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

using FDPlus.ResponsiveUI;

namespace FDPlus.ResponsiveUI.Editor
{
    /// <summary>
    /// Android / iOS / Wide Screen headers above slot lists. Phone and Pad are in the Android
    /// group; they are the iPhone and iPad slots as well.
    /// </summary>
    public static class ResponsiveSlotGroupUi
    {
        public static float HeaderSpan =>
            EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

        /// <summary>
        /// When <paramref name="slotIndex"/> starts a new group, advances past a bold header.
        /// <paramref name="group"/> is the last header drawn (empty before the first).
        /// </summary>
        public static float Advance(ref string group, int slotIndex, Rect row, bool draw)
        {
            string next = ResponsiveWidgetLayout.SlotGroupLabel(
                ResponsiveWidgetLayout.SlotFormFactor(slotIndex));
            if (next == group)
                return 0f;

            group = next;
            if (draw)
            {
                Rect labelRow = row;
                labelRow.height = EditorGUIUtility.singleLineHeight;
                EditorGUI.LabelField(labelRow, next, EditorStyles.boldLabel);
            }

            return HeaderSpan;
        }

        /// <summary>GUILayout header when <paramref name="form"/> starts a new group.</summary>
        public static void LayoutHeader(ref string group, ResponsiveDevice.FormFactor form)
        {
            string next = ResponsiveWidgetLayout.SlotGroupLabel(form);
            if (next == group)
                return;

            group = next;
            EditorGUILayout.LabelField(next, EditorStyles.boldLabel);
        }
    }
}
#endif
