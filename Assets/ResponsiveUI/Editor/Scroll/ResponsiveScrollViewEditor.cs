#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

using FDPlus.ResponsiveUI;

namespace FDPlus.ResponsiveUI.Editor
{
/// <summary>
/// Inspector for <see cref="ResponsiveScrollView"/>: hub reference, scroll slots,
/// and exact-slot copy within the scroll layout.
/// </summary>
[CustomEditor(typeof(ResponsiveScrollView))]
public class ResponsiveScrollViewEditor : UnityEditor.Editor
{
    #region Inspector

    readonly ResponsiveUiBulkSlotCopyPanel _bulkCopy = new ResponsiveUiBulkSlotCopyPanel();

    public override void OnInspectorGUI()
    {
        var scroll = target as ResponsiveScrollView;
        // Seed on the component before Update so the SerializedObject reads the seeded slots.
        if (ResponsiveUiDetectionDrawer.CanMutateInspectorLayout())
            EnsureDefaultSlots(scroll);
        serializedObject.Update();
        ResponsiveUiInspectorFoldouts.EnsureHubField(serializedObject, scroll);

        ResponsiveUI hub = scroll != null ? scroll.Hub : null;
        if (scroll != null && !scroll.enabled)
        {
            EditorGUILayout.HelpBox(
                "ResponsiveScrollView is disabled — bulk copy still saves slot data, " +
                "but LivePush waits until the component is enabled.",
                MessageType.Warning);
        }
        else if (hub == null)
        {
            EditorGUILayout.HelpBox(
                "No ResponsiveUI hub. Add ResponsiveUI to the Main Canvas, or assign Hub. " +
                "Without a hub, this component detects layout on its own.",
                MessageType.Info);
        }
        else if (!hub.enabled)
        {
            EditorGUILayout.HelpBox(
                "The ResponsiveUI hub is disabled — scroll Apply waits until the hub is enabled. " +
                "Bulk copy still saves slot data without LivePush.",
                MessageType.Warning);
        }

        if (scroll != null && !HasHost(scroll))
        {
            EditorGUILayout.HelpBox(
                "No FancyScrollView on this GameObject. Add ResponsiveScrollView next to the " +
                "scroll view that serializes Cell Interval / Scroll Offset — slots apply to " +
                "that sibling only.",
                MessageType.Warning);
        }

        ResponsiveUiInspectorFoldouts.DrawHubField(serializedObject);

        SerializedProperty metrics = serializedObject.FindProperty("scrollMetrics");

        ResponsiveUiInspectorFoldouts.BoxedFoldoutSection(
            InspectorKey("Layout"),
            "Scroll Layout",
            null,
            ResponsiveUiInspectorFoldouts.TintGreen,
            () =>
            {
                DrawBulkScrollCopy(scroll, metrics);
                EditorGUILayout.PropertyField(
                    metrics,
                    new GUIContent("Scroll Slots"),
                    includeChildren: true);
            });
        serializedObject.ApplyModifiedProperties();
    }

    #endregion

    #region Helpers

    bool _hostChecked;
    bool _hasHost;
    int _hostCheckComponentCount;

    /// <summary>
    /// FindHost builds a SerializedObject per sibling; re-check only when the
    /// GameObject's component count changes, not on every repaint.
    /// </summary>
    bool HasHost(ResponsiveScrollView scroll)
    {
        int count = scroll.gameObject.GetComponentCount();
        if (_hostChecked && count == _hostCheckComponentCount)
            return _hasHost;

        _hostChecked = true;
        _hostCheckComponentCount = count;
        _hasHost = ResponsiveScrollEditorUtil.FindHost(scroll) != null;
        return _hasHost;
    }

    string InspectorKey(string section) =>
        ResponsiveUiInspectorFoldouts.InspectorSectionKey(target, "Scroll", section);

    void DrawBulkScrollCopy(
        ResponsiveScrollView scroll,
        SerializedProperty metrics) =>
        _bulkCopy.Draw(
            metrics.serializedObject,
            "Bulk Copy Scroll Slots",
            "Copy Scroll Slot",
            "scroll layout",
            ResponsiveUiSlotClipboard.Kind.Scroll,
            1,
            (source, destination) => ExecuteBulkScrollCopy(scroll, source, destination));

    static void ExecuteBulkScrollCopy(
        ResponsiveScrollView scroll,
        int sourceSlotIndex,
        int destinationSlotIndex)
    {
        if (scroll == null ||
            !ResponsiveUiSlotClipboard.IsValidBulkCopy(
                sourceSlotIndex,
                destinationSlotIndex))
            return;

        var so = new SerializedObject(scroll);
        so.Update();
        SerializedProperty metrics = so.FindProperty("scrollMetrics");

        string undoName = ResponsiveUiBulkCopyUtil.SlotCopyUndoName(
            sourceSlotIndex, destinationSlotIndex);
        Component host = ResponsiveScrollEditorUtil.FindHost(scroll);
        Object applyTarget = host != null ? host : scroll.EditorApplyTarget;
        Object[] undoTargets =
            applyTarget != null && applyTarget != scroll
                ? new[] { scroll, applyTarget }
                : new Object[] { scroll };
        if (!ResponsiveUiBulkCopyUtil.Execute(
                undoName,
                undoTargets,
                () => ResponsiveScrollEditorUtil.CopySlot(
                    metrics,
                    sourceSlotIndex,
                    destinationSlotIndex)
                    ? 1
                    : 0,
                () =>
                {
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(scroll);
                    if (ResponsiveScrollEditorUtil.CanLivePush(scroll))
                        scroll.EditorLivePush();
                }))
            return;

        ResponsiveUiSlotClipboard.LogBulkCopyResult(
            ResponsiveUiSlotClipboard.Kind.Scroll,
            scroll,
            sourceSlotIndex,
            destinationSlotIndex,
            copiedCount: 1);
        ResponsiveUiBulkCopyUtil.RepaintPreview();
    }

    static void EnsureDefaultSlots(ResponsiveScrollView scroll)
    {
        if (scroll == null || !scroll.ConsumeSeedDefaultLayout())
            return;

        float interval = ResponsiveScrollState.DefaultCellInterval;
        float offset = ResponsiveScrollState.DefaultScrollOffset;
        MonoBehaviour[] behaviours = scroll.GetComponents<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] == null || behaviours[i] == scroll)
                continue;
            var so = new SerializedObject(behaviours[i]);
            SerializedProperty intervalProp = so.FindProperty("cellInterval");
            SerializedProperty offsetProp = so.FindProperty("scrollOffset");
            if (intervalProp == null || offsetProp == null)
                continue;
            interval = intervalProp.floatValue;
            offset = offsetProp.floatValue;
            break;
        }

        bool portrait = ResponsiveUiEditorSettings.IsPortraitAllowedByPlayer();
        bool landscape = ResponsiveUiEditorSettings.IsLandscapeAllowedByPlayer();
        Undo.RecordObject(scroll, "Seed Scroll Slots");
        scroll.SeedAll(interval, offset, portrait, landscape);
        EditorUtility.SetDirty(scroll);
    }

    #endregion
}
}

#endif
