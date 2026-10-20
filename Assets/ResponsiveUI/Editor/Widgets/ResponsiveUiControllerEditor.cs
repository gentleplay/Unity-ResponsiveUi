#if UNITY_EDITOR
using System.Collections.Generic;

using UnityEditor;
using UnityEngine;

using FDPlus.ResponsiveUI;

namespace FDPlus.ResponsiveUI.Editor
{
/// <summary>
/// Inspector for <see cref="ResponsiveUiController"/>: hub reference, Active Objects,
/// backgrounds, widget slots, and exact-slot copy across every widget row.
/// </summary>
[CustomEditor(typeof(ResponsiveUiController))]
public class ResponsiveUiControllerEditor : UnityEditor.Editor
{
    #region Inspector

    int _lastOrientationRevision = -1;
    int _lastWidgetCount = -1;
    int _lastActiveCount = -1;
    int _lastBackgroundCount = -1;
    readonly ResponsiveUiBulkSlotCopyPanel _bulkCopy = new ResponsiveUiBulkSlotCopyPanel();

    void OnEnable()
    {
        _lastOrientationRevision = -1;
        _lastWidgetCount = -1;
        _lastActiveCount = -1;
        _lastBackgroundCount = -1;
    }

    public override void OnInspectorGUI()
    {
        var controller = target as ResponsiveUiController;
        serializedObject.Update();
        ResponsiveUiInspectorFoldouts.EnsureHubField(serializedObject, controller);

        int orientationRevision = ResponsiveUiOrientationWatch.Revision;
        if (_lastOrientationRevision != orientationRevision &&
            ResponsiveUiDetectionDrawer.CanMutateInspectorLayout())
        {
            ResponsiveRectStateEditorUtil.SyncWidgets(serializedObject);
            _lastOrientationRevision = orientationRevision;
            serializedObject.Update();
        }

        ResponsiveUI hub = controller != null ? controller.Hub : null;
        if (hub == null)
        {
            EditorGUILayout.HelpBox(
                "No ResponsiveUI hub. Add ResponsiveUI to the Main Canvas, or assign Hub.",
                MessageType.Warning);
        }
        else if (!hub.enabled)
        {
            EditorGUILayout.HelpBox(
                "The ResponsiveUI hub is disabled — widget / background / Active Object Apply will not run. " +
                "Bulk copy still saves slot data; its scene preview waits until the hub is enabled.",
                MessageType.Warning);
        }

        ResponsiveUiInspectorFoldouts.DrawHubField(serializedObject);

        SerializedProperty widgets = serializedObject.FindProperty("widgets");
        if (ResponsiveUiDetectionDrawer.CanMutateInspectorLayout())
            EnsureDefaultWidget(widgets);

        bool backgroundsReset = ResetGrownListRows(ref widgets);
        if (ResponsiveUiDetectionDrawer.CanMutateInspectorLayout())
            ResponsiveUiDetectionDrawer.ApplyListFoldouts(widgets, hub);

        bool backgroundsChanged = false;
        ResponsiveActiveObjectEntryDrawer.DrawSection(serializedObject);

        EditorGUILayout.Space(4f);
        ResponsiveUiInspectorFoldouts.ColoredFoldoutSection(
            InspectorKey("Layout"),
            "Layout",
            null,
            ResponsiveUiInspectorFoldouts.TintGreen,
            () =>
            {
                EditorGUI.BeginChangeCheck();
                ResponsiveUiInspectorFoldouts.Property(serializedObject, "backgrounds");
                backgroundsChanged = EditorGUI.EndChangeCheck();
            },
            defaultOpen: true);

        ResponsiveUiInspectorFoldouts.BoxedFoldoutSection(
            InspectorKey("Widgets"),
            "Widgets",
            null,
            ResponsiveUiInspectorFoldouts.TintBlue,
            () =>
            {
                DrawBulkWidgetCopy(controller, widgets);
                EditorGUILayout.PropertyField(
                    widgets,
                    new GUIContent("Widget Items"),
                    includeChildren: true);
            });

        backgroundsReset |= ResetGrownListRows(ref widgets, immediate: true);
        serializedObject.ApplyModifiedProperties();
        if (backgroundsChanged && controller != null)
            controller.EditorApplyBackgrounds();

        serializedObject.Update();
        widgets = serializedObject.FindProperty("widgets");
        ResponsiveUiInspectorFoldouts.DrawDesignScaleHints(
            hub, ResponsiveRectStateEditorUtil.AnySlotUsesDesignScale(widgets));
        backgroundsReset |= ResetGrownListRows(ref widgets, immediate: true);
        if (backgroundsReset && controller != null)
            controller.EditorApplyBackgrounds();
    }

    #endregion

    #region Helpers

    string InspectorKey(string section) =>
        ResponsiveUiInspectorFoldouts.InspectorSectionKey(target, "Controller", section);

    bool ResetGrownListRows(ref SerializedProperty widgets, bool immediate = false)
    {
        bool canMutate = immediate || ResponsiveUiDetectionDrawer.CanMutateInspectorLayout();
        bool seeded = ResponsiveRectStateEditorUtil.ForEachNewRow(
            widgets,
            ref _lastWidgetCount,
            ResponsiveRectStateEditorUtil.ResetNewListRow,
            canMutate);
        seeded |= ResponsiveActiveObjectEntryDrawer.SeedNewRows(
            serializedObject.FindProperty("activeObjects"),
            ref _lastActiveCount,
            canMutate);
        bool backgroundsReset = ResponsiveRectStateEditorUtil.ForEachNewRow(
            serializedObject.FindProperty("backgrounds"),
            ref _lastBackgroundCount,
            ResponsiveBackgroundDrawer.ResetNewListRow,
            canMutate);
        seeded |= backgroundsReset;
        if (!seeded)
            return false;

        serializedObject.ApplyModifiedProperties();
        serializedObject.Update();
        widgets = serializedObject.FindProperty("widgets");
        return backgroundsReset;
    }

    void DrawBulkWidgetCopy(
        ResponsiveUiController controller,
        SerializedProperty widgets) =>
        _bulkCopy.Draw(
            widgets.serializedObject,
            "Bulk Copy Widget Slots",
            "Copy Across All Widgets",
            "widget",
            ResponsiveUiSlotClipboard.Kind.Widget,
            widgets.arraySize,
            (source, destination) => ExecuteBulkWidgetCopy(controller, source, destination));

    static void ExecuteBulkWidgetCopy(
        ResponsiveUiController controller,
        int sourceSlotIndex,
        int destinationSlotIndex)
    {
        if (controller == null ||
            !ResponsiveUiSlotClipboard.IsValidBulkCopy(
                sourceSlotIndex,
                destinationSlotIndex))
            return;

        var so = new SerializedObject(controller);
        so.Update();
        SerializedProperty widgets = so.FindProperty("widgets");
        if (widgets.arraySize == 0)
            return;

        string undoName = ResponsiveUiBulkCopyUtil.SlotCopyUndoName(
            sourceSlotIndex, destinationSlotIndex);
        ResponsiveUI hub = controller.Hub;
        int copied = 0;
        if (!ResponsiveUiBulkCopyUtil.Execute(
                undoName,
                GetBulkCopyUndoTargets(controller),
                () =>
                {
                    copied = ResponsiveRectStateEditorUtil.CopySlotAcrossWidgets(
                        widgets,
                        sourceSlotIndex,
                        destinationSlotIndex);
                    return copied;
                },
                () =>
                {
                    hub?.NotifyWidgetLivePreview();
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(controller);

                    if (controller.isActiveAndEnabled && hub != null && hub.enabled)
                    {
                        ResponsiveDevice.LayoutKey layout =
                            ResponsiveUiDetectionDrawer.ResolveInspectorLayout(so, hub);
                        using (ResponsiveUiWidgetSync.SuppressCapture())
                            controller.EditorApplyWidgets(layout);
                    }
                }))
            return;

        ResponsiveUiSlotClipboard.LogBulkCopyResult(
            ResponsiveUiSlotClipboard.Kind.Widget,
            controller,
            sourceSlotIndex,
            destinationSlotIndex,
            copied);
        ResponsiveUiBulkCopyUtil.RepaintPreview();
    }

    static Object[] GetBulkCopyUndoTargets(ResponsiveUiController controller)
    {
        var targets = new List<Object> { controller };
        ResponsiveWidgetLayout[] widgets = controller.Widgets;
        if (widgets == null)
            return targets.ToArray();

        for (int i = 0; i < widgets.Length; i++)
            ResponsiveUiBulkCopyUtil.AddUndoTarget(targets, widgets[i].target);

        return targets.ToArray();
    }

    static void EnsureDefaultWidget(SerializedProperty widgets)
    {
        var controller = widgets?.serializedObject.targetObject as ResponsiveUiController;
        if (controller == null || !controller.ConsumeSeedDefaultLayout())
            return;
        if (!widgets.isArray || widgets.arraySize > 0)
            return;

        widgets.arraySize = 1;
        ResponsiveRectStateEditorUtil.SeedSlots(widgets.GetArrayElementAtIndex(0));
        widgets.serializedObject.ApplyModifiedProperties();
        widgets.serializedObject.Update();
    }

    #endregion
}
}

#endif
