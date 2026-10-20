#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FDPlus.ResponsiveUI.Editor
{
    /// <summary>
    /// Inspector for <see cref="ResponsiveTwoSideUiTweenController"/>: Side / Preview authoring,
    /// widget rows, slot / side / widget-row bulk copy, and tween preview controls.
    /// </summary>
    [CustomEditor(typeof(ResponsiveTwoSideUiTweenController))]
    public sealed class ResponsiveTwoSideUiTweenControllerEditor :
        UnityEditor.Editor
    {
        #region Inspector

        const string WidgetsSection = "Widgets";

        static readonly string[] PreviewLabels =
        {
            "Preview Normal",
            "Preview Animation Start",
        };

        int _lastOrientationRevision = -1;
        int _lastWidgetCount = -1;
        readonly ResponsiveUiBulkSlotCopyPanel _bulkCopy = new ResponsiveUiBulkSlotCopyPanel();
        bool _bulkSideCopyQueued;
        bool _bulkSideCopyExpanded;
        bool _bulkWidgetRowQueued;
        bool _bulkWidgetRowExpanded;
        int _bulkWidgetRowSource;
        int _bulkWidgetRowDestination = 1;
        int _bulkWidgetRowSide = (int)ResponsiveUiSide.Right;

        void OnEnable()
        {
            _lastOrientationRevision = -1;
            _lastWidgetCount = -1;
        }

        string InspectorKey(string section) =>
            ResponsiveUiInspectorFoldouts.InspectorSectionKey(target, "TwoSide", section);

        public override void OnInspectorGUI()
        {
            var controller = target as ResponsiveTwoSideUiTweenController;
            serializedObject.Update();
            ResponsiveUiInspectorFoldouts.EnsureHubField(
                serializedObject,
                controller);

            int orientationRevision = ResponsiveUiOrientationWatch.Revision;
            if (_lastOrientationRevision != orientationRevision &&
                ResponsiveUiDetectionDrawer.CanMutateInspectorLayout())
            {
                ResponsiveRectStateEditorUtil.SyncTwoSideWidgets(
                    serializedObject);
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
                    "The ResponsiveUI hub is disabled — two-side Apply will not run. " +
                    "Bulk copy still saves slot data; its scene preview waits until the hub is enabled.",
                    MessageType.Warning);
            }

            ResponsiveUiInspectorFoldouts.DrawHubField(serializedObject);

            EditorGUILayout.Space();
            ResponsiveUiInspectorFoldouts.ColoredFoldoutSection(
                InspectorKey("Authoring"),
                "Layout Authoring",
                null,
                ResponsiveUiInspectorFoldouts.TintAmber,
                () =>
                {
                    EditorGUILayout.PropertyField(
                        serializedObject.FindProperty("side"),
                        new GUIContent("Side"));
                    SerializedProperty previewStart =
                        serializedObject.FindProperty("previewAnimationStart");
                    int previewIndex = GUILayout.Toolbar(
                        previewStart.boolValue ? 1 : 0,
                        PreviewLabels);
                    previewStart.boolValue = previewIndex == 1;
                    EditorGUILayout.PropertyField(
                        serializedObject.FindProperty("useUnscaledTime"));
                },
                defaultOpen: true);

            SerializedProperty widgets = serializedObject.FindProperty("widgets");
            if (ResponsiveUiDetectionDrawer.CanMutateInspectorLayout())
                EnsureDefaultWidget(widgets);

            ResetGrownTwoSideRows(ref widgets);

            ResponsiveUiInspectorFoldouts.BoxedFoldoutSection(
                InspectorKey(WidgetsSection),
                "Widgets",
                null,
                ResponsiveUiInspectorFoldouts.TintBlue,
                () =>
                {
                    DrawBulkWidgetCopy(controller, widgets);
                    DrawBulkSideCopy(controller, widgets);
                    DrawBulkWidgetRowCopy(controller, widgets);
                    EditorGUILayout.PropertyField(
                        widgets,
                        new GUIContent("Widget Items"),
                        includeChildren: true);
                });

            bool grown = ResetGrownTwoSideRows(ref widgets, immediate: true);
            if (serializedObject.ApplyModifiedProperties() || grown)
                PreviewWithUndo();

            serializedObject.Update();
            widgets = serializedObject.FindProperty("widgets");
            ResetGrownTwoSideRows(ref widgets, immediate: true);
            ResponsiveUiInspectorFoldouts.DrawDesignScaleHints(
                hub, ResponsiveRectStateEditorUtil.AnyTwoSideSlotUsesDesignScale(widgets));

            EditorGUILayout.Space();
            ResponsiveUiInspectorFoldouts.ColoredFoldoutSection(
                InspectorKey("Preview"),
                "Preview",
                null,
                ResponsiveUiInspectorFoldouts.TintPurple,
                () =>
                {
                    DrawAnimationControls();
                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button("Refresh Preview"))
                        PreviewWithUndo();
                    if (GUILayout.Button("Hide"))
                        HideWithUndo();
                    EditorGUILayout.EndHorizontal();
                },
                defaultOpen: true);
        }

        bool ResetGrownTwoSideRows(ref SerializedProperty widgets, bool immediate = false)
        {
            bool canMutate = immediate || ResponsiveUiDetectionDrawer.CanMutateInspectorLayout();
            if (!ResponsiveRectStateEditorUtil.ForEachNewRow(
                    widgets,
                    ref _lastWidgetCount,
                    ResponsiveRectStateEditorUtil.SeedTwoSideWidget,
                    canMutate))
                return false;

            serializedObject.ApplyModifiedProperties();
            serializedObject.Update();
            widgets = serializedObject.FindProperty("widgets");
            return true;
        }

        #endregion

        #region Bulk copy

        void DrawBulkWidgetCopy(
            ResponsiveTwoSideUiTweenController controller,
            SerializedProperty widgets) =>
            _bulkCopy.Draw(
                widgets.serializedObject,
                "Bulk Copy Widget Slots",
                "Copy Across All Widgets",
                "widget",
                ResponsiveUiSlotClipboard.Kind.Widget,
                widgets.arraySize,
                (source, destination) => ExecuteBulkWidgetCopy(controller, source, destination),
                extraHelp: "Writes the currently shown Side and Preview layout only.");

        void DrawBulkSideCopy(
            ResponsiveTwoSideUiTweenController controller,
            SerializedProperty widgets)
        {
            _bulkSideCopyExpanded = ResponsiveUiInspectorFoldouts.DrawGuiFoldout(
                "Bulk Copy Side",
                _bulkSideCopyExpanded,
                () =>
                {
                    EditorGUILayout.HelpBox(
                        "Copies every form-factor slot on Normal and Animation Start " +
                        "from one side to the other. Mirrors Anchor Min/Max X (1 − x) " +
                        "and Pos X sign so a right-edge panel becomes a left-edge panel. " +
                        "Pivot X becomes 1 − x (0.5 stays 0.5). Y, size, Enabled, and " +
                        "Design Scale are unchanged. Targets stay on each widget. " +
                        "After copy, Side switches to the destination so the preview " +
                        "shows the mirrored result.",
                        MessageType.None);

                    bool canCopy = !_bulkSideCopyQueued && widgets.arraySize > 0;
                    using (new EditorGUI.DisabledScope(!canCopy))
                    {
                        if (GUILayout.Button("Copy Right → Left") &&
                            ConfirmBulkSideCopy(
                                widgets.arraySize,
                                ResponsiveUiSide.Right,
                                ResponsiveUiSide.Left))
                        {
                            QueueBulkSideCopy(
                                controller,
                                ResponsiveUiSide.Right,
                                ResponsiveUiSide.Left);
                        }

                        if (GUILayout.Button("Copy Left → Right") &&
                            ConfirmBulkSideCopy(
                                widgets.arraySize,
                                ResponsiveUiSide.Left,
                                ResponsiveUiSide.Right))
                        {
                            QueueBulkSideCopy(
                                controller,
                                ResponsiveUiSide.Left,
                                ResponsiveUiSide.Right);
                        }
                    }
                });
        }

        void DrawBulkWidgetRowCopy(
            ResponsiveTwoSideUiTweenController controller,
            SerializedProperty widgets)
        {
            if (!_bulkWidgetRowExpanded)
                _bulkWidgetRowSide =
                    serializedObject.FindProperty("side").intValue;

            _bulkWidgetRowExpanded = ResponsiveUiInspectorFoldouts.DrawGuiFoldout(
                "Bulk Copy Widget",
                _bulkWidgetRowExpanded,
                () =>
                {
                    EditorGUILayout.HelpBox(
                        "Copies or swaps every form-factor slot on Normal and Animation " +
                        "Start between two widgets on one side. Targets, animation " +
                        "settings, and the other side stay unchanged.",
                        MessageType.None);

                    int widgetCount = widgets.arraySize;
                    if (widgetCount < 2)
                    {
                        EditorGUILayout.HelpBox(
                            "Add at least two widgets to copy or swap one onto another.",
                            MessageType.Info);
                        return;
                    }

                    string[] labels = WidgetRowLabels(widgets);
                    _bulkWidgetRowSource = Mathf.Clamp(_bulkWidgetRowSource, 0, widgetCount - 1);
                    _bulkWidgetRowDestination =
                        Mathf.Clamp(_bulkWidgetRowDestination, 0, widgetCount - 1);
                    if (_bulkWidgetRowDestination == _bulkWidgetRowSource)
                        _bulkWidgetRowDestination =
                            (_bulkWidgetRowSource + 1) % widgetCount;

                    _bulkWidgetRowSource = EditorGUILayout.Popup(
                        "From Widget",
                        _bulkWidgetRowSource,
                        labels);
                    _bulkWidgetRowDestination = EditorGUILayout.Popup(
                        "To Widget",
                        _bulkWidgetRowDestination,
                        labels);
                    _bulkWidgetRowSide = (int)(ResponsiveUiSide)EditorGUILayout.EnumPopup(
                        "Side",
                        (ResponsiveUiSide)_bulkWidgetRowSide);

                    bool canCopy =
                        !_bulkWidgetRowQueued &&
                        _bulkWidgetRowSource != _bulkWidgetRowDestination;
                    using (new EditorGUI.DisabledScope(!canCopy))
                    {
                        if (GUILayout.Button("Copy From → To") &&
                            ConfirmBulkWidgetRowCopy(
                                labels[_bulkWidgetRowSource],
                                labels[_bulkWidgetRowDestination],
                                (ResponsiveUiSide)_bulkWidgetRowSide,
                                swap: false))
                        {
                            QueueBulkWidgetRowCopy(controller, swap: false);
                        }

                        if (GUILayout.Button("Swap From ↔ To") &&
                            ConfirmBulkWidgetRowCopy(
                                labels[_bulkWidgetRowSource],
                                labels[_bulkWidgetRowDestination],
                                (ResponsiveUiSide)_bulkWidgetRowSide,
                                swap: true))
                        {
                            QueueBulkWidgetRowCopy(controller, swap: true);
                        }
                    }
                });
        }

        static string[] WidgetRowLabels(SerializedProperty widgets)
        {
            var labels = new string[widgets.arraySize];
            for (int i = 0; i < widgets.arraySize; i++)
            {
                string name = widgets
                    .GetArrayElementAtIndex(i)
                    .FindPropertyRelative("name")
                    .stringValue;
                labels[i] = string.IsNullOrWhiteSpace(name)
                    ? $"Widget {i}"
                    : $"{name} ({i})";
            }

            return labels;
        }

        static bool ConfirmBulkWidgetRowCopy(
            string sourceWidget,
            string destinationWidget,
            ResponsiveUiSide side,
            bool swap)
        {
            string title = swap ? "Bulk Swap Widget" : "Bulk Copy Widget";
            string action = swap
                ? $"Swap {side} Normal and Animation Start between {sourceWidget} " +
                  $"and {destinationWidget}?"
                : $"Overwrite {destinationWidget} {side} Normal and Animation Start " +
                  $"with {sourceWidget} {side} values?";
            return EditorUtility.DisplayDialog(
                title,
                action + " Targets stay unchanged.",
                swap ? "Swap" : "Copy",
                "Cancel",
                DialogOptOutDecisionType.ForThisSession,
                ResponsiveUiSlotClipboard.WidgetRowBulkCopyOptOutKey);
        }

        void QueueBulkWidgetRowCopy(
            ResponsiveTwoSideUiTweenController controller,
            bool swap)
        {
            int source = _bulkWidgetRowSource;
            int destination = _bulkWidgetRowDestination;
            ResponsiveUiSide side = (ResponsiveUiSide)_bulkWidgetRowSide;
            _bulkWidgetRowQueued = true;
            EditorApplication.delayCall += () =>
            {
                _bulkWidgetRowQueued = false;
                ExecuteBulkWidgetRowCopy(controller, source, destination, side, swap);
            };
        }

        static void ExecuteBulkWidgetRowCopy(
            ResponsiveTwoSideUiTweenController controller,
            int sourceWidgetIndex,
            int destinationWidgetIndex,
            ResponsiveUiSide side,
            bool swap)
        {
            if (controller == null || sourceWidgetIndex == destinationWidgetIndex)
                return;

            var so = new SerializedObject(controller);
            so.Update();
            SerializedProperty widgets = so.FindProperty("widgets");
            if (widgets.arraySize < 2)
                return;

            string[] labels = WidgetRowLabels(widgets);
            if (sourceWidgetIndex >= labels.Length ||
                destinationWidgetIndex >= labels.Length)
                return;

            string undoName = swap
                ? $"Swap {labels[sourceWidgetIndex]} with {labels[destinationWidgetIndex]} on {side}"
                : $"Copy {labels[sourceWidgetIndex]} to {labels[destinationWidgetIndex]} on {side}";
            ResponsiveUiBulkCopyUtil.Execute(
                undoName,
                GetBulkCopyUndoTargets(controller),
                () => swap
                    ? ResponsiveRectStateEditorUtil.SwapWidgetSideAcrossTwoSideWidgets(
                        widgets,
                        sourceWidgetIndex,
                        destinationWidgetIndex,
                        side)
                    : ResponsiveRectStateEditorUtil.CopyWidgetSideAcrossTwoSideWidgets(
                        widgets,
                        sourceWidgetIndex,
                        destinationWidgetIndex,
                        side),
                () => CompleteBulkCopy(
                    controller,
                    so,
                    () => ResponsiveUiSlotClipboard.LogBulkWidgetRowCopyResult(
                        controller,
                        labels[sourceWidgetIndex],
                        labels[destinationWidgetIndex],
                        side,
                        swap)));
        }

        static bool ConfirmBulkSideCopy(
            int widgetCount,
            ResponsiveUiSide sourceSide,
            ResponsiveUiSide destinationSide) =>
            EditorUtility.DisplayDialog(
                "Bulk Copy Side",
                $"Overwrite {destinationSide} Normal and Animation Start on " +
                $"{widgetCount} widget{(widgetCount == 1 ? string.Empty : "s")} " +
                $"with a horizontal mirror of {sourceSide}?",
                widgetCount == 1 ? "Copy" : "Copy All",
                "Cancel",
                DialogOptOutDecisionType.ForThisSession,
                ResponsiveUiSlotClipboard.WidgetSideBulkCopyOptOutKey);

        void QueueBulkSideCopy(
            ResponsiveTwoSideUiTweenController controller,
            ResponsiveUiSide sourceSide,
            ResponsiveUiSide destinationSide)
        {
            _bulkSideCopyQueued = true;
            EditorApplication.delayCall += () =>
            {
                _bulkSideCopyQueued = false;
                ExecuteBulkSideCopy(controller, sourceSide, destinationSide);
            };
        }

        static void ExecuteBulkSideCopy(
            ResponsiveTwoSideUiTweenController controller,
            ResponsiveUiSide sourceSide,
            ResponsiveUiSide destinationSide)
        {
            if (controller == null || sourceSide == destinationSide)
                return;

            var so = new SerializedObject(controller);
            so.Update();
            SerializedProperty widgets = so.FindProperty("widgets");
            if (widgets.arraySize == 0)
                return;

            string undoName = $"Copy {sourceSide} to {destinationSide}";
            int copied = 0;
            ResponsiveUiBulkCopyUtil.Execute(
                undoName,
                GetBulkCopyUndoTargets(controller),
                () =>
                {
                    copied = ResponsiveRectStateEditorUtil.CopySideAcrossTwoSideWidgets(
                        widgets,
                        sourceSide,
                        destinationSide);
                    if (copied > 0)
                        so.FindProperty("side").intValue = (int)destinationSide;
                    return copied;
                },
                () => CompleteBulkCopy(
                    controller,
                    so,
                    () => ResponsiveUiSlotClipboard.LogBulkSideCopyResult(
                        controller,
                        sourceSide,
                        destinationSide,
                        copied)));
        }

        static void ExecuteBulkWidgetCopy(
            ResponsiveTwoSideUiTweenController controller,
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

            ResponsiveUiSide side =
                (ResponsiveUiSide)so.FindProperty("side").intValue;
            bool previewAnimationStart =
                so.FindProperty("previewAnimationStart").boolValue;
            string undoName = ResponsiveUiBulkCopyUtil.SlotCopyUndoName(
                sourceSlotIndex, destinationSlotIndex);
            int copied = 0;
            ResponsiveUiBulkCopyUtil.Execute(
                undoName,
                GetBulkCopyUndoTargets(controller),
                () =>
                {
                    copied = ResponsiveRectStateEditorUtil.CopySlotAcrossTwoSideWidgets(
                        widgets,
                        sourceSlotIndex,
                        destinationSlotIndex,
                        side,
                        previewAnimationStart);
                    return copied;
                },
                () => CompleteBulkCopy(
                    controller,
                    so,
                    () => ResponsiveUiSlotClipboard.LogBulkCopyResult(
                        ResponsiveUiSlotClipboard.Kind.Widget,
                        controller,
                        sourceSlotIndex,
                        destinationSlotIndex,
                        copied)));
        }

        static void CompleteBulkCopy(
            ResponsiveTwoSideUiTweenController controller,
            SerializedObject so,
            Action log)
        {
            ResponsiveUI hub = controller.Hub;
            hub?.NotifyWidgetLivePreview();
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(controller);

            if (controller.isActiveAndEnabled && hub != null && hub.enabled)
            {
                using (ResponsiveUiWidgetSync.SuppressCapture())
                    controller.Preview();
            }

            log();
            ResponsiveUiBulkCopyUtil.RepaintPreview();
        }

        static Object[] GetBulkCopyUndoTargets(
            ResponsiveTwoSideUiTweenController controller)
        {
            var targets = new List<Object> { controller };
            ResponsiveTwoSideWidget[] widgets = controller.Widgets;
            if (widgets == null)
                return targets.ToArray();

            for (int i = 0; i < widgets.Length; i++)
            {
                ResponsiveUiBulkCopyUtil.AddUndoTarget(targets, widgets[i].leftNormal.target);
                ResponsiveUiBulkCopyUtil.AddUndoTarget(targets, widgets[i].leftAnimationStart.target);
                ResponsiveUiBulkCopyUtil.AddUndoTarget(targets, widgets[i].rightNormal.target);
                ResponsiveUiBulkCopyUtil.AddUndoTarget(targets, widgets[i].rightAnimationStart.target);
            }

            return targets.ToArray();
        }

        #endregion

        #region Helpers

        static void EnsureDefaultWidget(SerializedProperty widgets)
        {
            var controller =
                widgets?.serializedObject.targetObject as
                    ResponsiveTwoSideUiTweenController;
            if (controller == null || !controller.ConsumeSeedDefaultLayout())
                return;
            if (!widgets.isArray || widgets.arraySize > 0)
                return;

            widgets.arraySize = 1;
            ResponsiveRectStateEditorUtil.SeedTwoSideWidget(
                widgets.GetArrayElementAtIndex(0));
            widgets.serializedObject.ApplyModifiedProperties();
            widgets.serializedObject.Update();
        }

        void OnDisable()
        {
            EditorApplication.update -= RepaintAnimationControls;
            if (target is ResponsiveTwoSideUiTweenController controller &&
                controller.EditorPreviewPlaying)
            {
                IgnorePreviewTargetCapture();
                controller.EditorStopPreview();
            }
        }

        void DrawAnimationControls()
        {
            ResponsiveTwoSideUiTweenController controller =
                (ResponsiveTwoSideUiTweenController)target;
            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(Application.isPlaying))
            {
                using (new EditorGUI.DisabledScope(
                           !controller.EditorCanPlayPreview))
                {
                    if (GUILayout.Button("Play"))
                    {
                        RecordPreviewTargets(
                            "Play Responsive UI Animation");
                        controller.EditorPlayPreview(
                            ResponsiveUiWidgetSync.IgnoreTargetCapture);
                        EditorApplication.update -=
                            RepaintAnimationControls;
                        EditorApplication.update +=
                            RepaintAnimationControls;
                    }
                }

                using (new EditorGUI.DisabledScope(
                           !controller.EditorPreviewPlaying))
                {
                    string pauseLabel = controller.EditorPreviewPaused
                        ? "Resume"
                        : "Pause";
                    if (GUILayout.Button(pauseLabel))
                        controller.EditorPausePreview();
                    if (GUILayout.Button("Stop"))
                    {
                        RecordPreviewTargets("Stop Responsive UI Animation");
                        IgnorePreviewTargetCapture();
                        controller.EditorStopPreview();
                        EditorApplication.update -=
                            RepaintAnimationControls;
                    }
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        void RepaintAnimationControls()
        {
            if (target is ResponsiveTwoSideUiTweenController controller &&
                controller.EditorPreviewPlaying)
            {
                Repaint();
                return;
            }

            EditorApplication.update -= RepaintAnimationControls;
            Repaint();
        }

        void IgnorePreviewTargetCapture()
        {
            List<Object> targets = CollectPreviewTargets();
            for (int i = 0; i < targets.Count; i++)
            {
                if (targets[i] is RectTransform rectTransform)
                    ResponsiveUiWidgetSync.IgnoreTargetCapture(rectTransform);
            }
        }

        void PreviewWithUndo()
        {
            List<Object> targets =
                RecordPreviewTargets("Preview Responsive UI State");
            ((ResponsiveTwoSideUiTweenController)target).Preview();
            for (int i = 0; i < targets.Count; i++)
                EditorUtility.SetDirty(targets[i]);
            SceneView.RepaintAll();
            EditorApplication.QueuePlayerLoopUpdate();
        }

        void HideWithUndo()
        {
            List<Object> targets =
                RecordPreviewTargets("Hide Responsive UI");
            IgnorePreviewTargetCapture();
            var controller = (ResponsiveTwoSideUiTweenController)target;
            controller.EditorStopPreview();
            controller.SetVisibleImmediate(false);
            for (int i = 0; i < targets.Count; i++)
                EditorUtility.SetDirty(targets[i]);
            SceneView.RepaintAll();
            EditorApplication.QueuePlayerLoopUpdate();
        }

        List<Object> RecordPreviewTargets(string undoName)
        {
            List<Object> targets = CollectPreviewTargets();
            if (targets.Count > 0)
                Undo.RegisterCompleteObjectUndo(
                    targets.ToArray(),
                    undoName);
            return targets;
        }

        List<Object> CollectPreviewTargets()
        {
            SerializedProperty widgets =
                serializedObject.FindProperty("widgets");
            List<Object> targets = new List<Object>(widgets.arraySize);
            for (int i = 0; i < widgets.arraySize; i++)
            {
                SerializedProperty widget = widgets.GetArrayElementAtIndex(i);
                Object target = widget
                    .FindPropertyRelative(SelectedLayoutName(widget))
                    .FindPropertyRelative("target")
                    .objectReferenceValue;
                if (target != null && !targets.Contains(target))
                    targets.Add(target);
            }

            return targets;
        }

        string SelectedLayoutName(SerializedProperty widget) =>
            ResponsiveRectStateEditorUtil.TwoSideLayoutPropertyName(
                widget,
                (ResponsiveUiSide)serializedObject.FindProperty("side").intValue,
                serializedObject.FindProperty("previewAnimationStart").boolValue);

        #endregion
    }
}
#endif
