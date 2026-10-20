#if UNITY_EDITOR
using System.Collections.Generic;

using UnityEditor;
using UnityEngine;

using FDPlus.ResponsiveUI;

namespace FDPlus.ResponsiveUI.Editor
{
    /// <summary>
    /// Inspector for <see cref="ResponsiveCamera"/>: hub reference, camera slots,
    /// and exact-slot copy across every camera row.
    /// Detection / Events / Scaler live on the Canvas ResponsiveUI hub.
    /// </summary>
    [CustomEditor(typeof(ResponsiveCamera))]
    public class ResponsiveCameraEditor : UnityEditor.Editor
    {
        #region Inspector

        int _lastOrientationRevision = -1;
        int _lastCameraCount = -1;
        readonly ResponsiveUiBulkSlotCopyPanel _bulkCopy = new ResponsiveUiBulkSlotCopyPanel();

        void OnEnable()
        {
            _lastOrientationRevision = -1;
            _lastCameraCount = -1;
        }

        public override void OnInspectorGUI()
        {
            var component = target as ResponsiveCamera;
            serializedObject.Update();
            ResponsiveUiInspectorFoldouts.EnsureHubField(serializedObject, component);

            int orientationRevision = ResponsiveUiOrientationWatch.Revision;
            if (_lastOrientationRevision != orientationRevision &&
                ResponsiveUiDetectionDrawer.CanMutateInspectorLayout())
            {
                ResponsiveRectStateEditorUtil.SyncCameras(serializedObject);
                _lastOrientationRevision = orientationRevision;
                serializedObject.Update();
            }

            ResponsiveUI hub = component != null ? component.Hub : null;
            if (component != null && !component.enabled)
            {
                EditorGUILayout.HelpBox(
                    "ResponsiveCamera is disabled — device / Simulator Apply will not run until you enable it.\n\n" +
                    "Direct slot edits still LivePush to the Target Camera. Bulk copy saves its slots, " +
                    "then previews after this component and its hub are enabled.",
                    MessageType.Warning);
            }
            else if (hub == null)
            {
                EditorGUILayout.HelpBox(
                    "No ResponsiveUI hub. Add ResponsiveUI to the Main Canvas, or assign Hub. " +
                    "Bulk copy saves slot data but cannot preview it without a hub.",
                    MessageType.Warning);
            }
            else if (!hub.enabled)
            {
                EditorGUILayout.HelpBox(
                    "The ResponsiveUI hub is disabled — camera Apply will not run. " +
                    "Bulk copy still saves slot data; its scene preview waits until the hub is enabled.",
                    MessageType.Warning);
            }

            ResponsiveUiInspectorFoldouts.ColoredFoldoutSection(
                InspectorKey("Help"),
                "Usage notes",
                null,
                ResponsiveUiInspectorFoldouts.TintSlate,
                () =>
                {
                    EditorGUILayout.HelpBox(
                        "Production (real devices): layout comes from the Canvas ResponsiveUI hub " +
                        "(Screen size, FoldablePostureDetector / FoldablePostureSource LayoutChanged, app focus/resume).\n" +
                        "HalfOpened slots need an active FoldablePostureDetector.\n" +
                        "Per slot (" + ResponsiveUiEditorSettings.FormDisplayNameList +
                        " × Portrait/Landscape):\n" +
                        "• Apply Local — each device slot stores its own Local Position / Local Euler Angles " +
                        "(same idea as a widget Pos). When on, this slot writes those values to the Target. " +
                        "When off, the stored pose stays on this slot and is not written (Focus can own the live camera). " +
                        "Switching Device Simulator profiles does not rewrite other slots. " +
                        "Turn Apply Local on to capture the current camera into this slot, or edit Local Position. " +
                        "Capture From Camera copies the live Target into this slot. " +
                        "Moving or editing the Target Camera writes the visible authoring slot; " +
                        "responsive runtime / Simulator applies are suppressed and never feed back into slot data.\n" +
                        "• Fold Cover (Fold8 Cover ~1.58) and Flip Cover (Flip Closed): " +
                        "new cameras start Enabled for allowed orientations; " +
                        "existing Cover stays authored. Leave them off to reuse Phone ortho/position. " +
                        "Enabling Cover / Pad / Fold / Flip copies Phone Landscape or Phone Portrait; " +
                        "iPhone Duo Seated and Wide Screen copy Pad first (same shape), then Phone.\n" +
                        "• Scale From UI Size — local position × live Screen / canvas size ÷ Design UI Size " +
                        "(locked to Project Settings Design Canvas for that form factor and oriented to the slot; " +
                        "catalog changes re-Apply loaded cameras).\n" +
                        "• Projection — Orthographic or Perspective (auto-detected from Target on Capture / first assign). " +
                        "Apply writes Projection plus Orthographic Size or Field Of View.\n" +
                        "• Scale Projection — Orthographic Size and Field Of View use Scale Mode " +
                        "aspect framing (Width keeps horizontal world span; Height keeps vertical span; " +
                        "Auto fits like UniformMin: wider → Height, taller → Width).\n" +
                        "• Camera Background — one SpriteRenderer fitted to the camera view " +
                        "(Stretch / Cover / Fit), centered, and placed behind all camera content.\n" +
                        "• Focus Target / Focus Screen / Focus Bounds (per slot) — keep a world object " +
                        "in this slot's Focus Bounds HUD (Auto / Bounds) or the whole camera (Full). " +
                        "Focus Bounds authors like a widget item (anchors, Pos / Size) on that slot. " +
                        "Does not write Camera.rect. Slot Viewport still owns the camera rectangle. " +
                        "Center slides the camera; Cover fills the pane (may crop); Fit shows the whole object.",
                        MessageType.Info);
                });

            ResponsiveUiInspectorFoldouts.DrawHubField(serializedObject);

            SerializedProperty cameras = serializedObject.FindProperty("cameras");
            if (ResponsiveUiDetectionDrawer.CanMutateInspectorLayout())
                EnsureDefaultCamera(cameras);

            ResetGrownCameraRows(ref cameras);
            if (ResponsiveUiDetectionDrawer.CanMutateInspectorLayout())
                ResponsiveUiDetectionDrawer.ApplyListFoldouts(cameras, hub);

            EditorGUILayout.Space(4f);
            ResponsiveUiInspectorFoldouts.BoxedFoldoutSection(
                InspectorKey("Cameras"),
                "Cameras",
                null,
                ResponsiveUiInspectorFoldouts.TintBlue,
                () =>
                {
                    EditorGUILayout.HelpBox(
                        "Scale From UI Size and Scale Projection lock Design UI Size to Project Settings → Responsive UI " +
                        "Design Canvas for that form factor (Portrait/Landscape oriented to the slot). " +
                        "Projection (Orthographic / Perspective) is auto-detected from the Target on Capture. " +
                        "Changing the catalog device there re-Applies loaded cameras immediately.",
                        MessageType.None);
                    ResponsiveUiInspectorFoldouts.Property(serializedObject, "uiSizeRect");
                    ResponsiveUiInspectorFoldouts.Property(serializedObject, "uiSizeCanvas");
                    DrawBulkCameraCopy(component, cameras);
                    EditorGUILayout.PropertyField(
                        cameras,
                        new GUIContent("Camera Items"),
                        includeChildren: true);
                });

            ResetGrownCameraRows(ref cameras, immediate: true);
            serializedObject.ApplyModifiedProperties();

            serializedObject.Update();
            cameras = serializedObject.FindProperty("cameras");
            ResetGrownCameraRows(ref cameras, immediate: true);
        }

        #endregion

        #region Helpers

        string InspectorKey(string section) =>
            ResponsiveUiInspectorFoldouts.InspectorSectionKey(target, "Camera", section);

        void ResetGrownCameraRows(ref SerializedProperty cameras, bool immediate = false)
        {
            bool canMutate = immediate || ResponsiveUiDetectionDrawer.CanMutateInspectorLayout();
            if (!ResponsiveRectStateEditorUtil.ForEachNewRow(
                    cameras,
                    ref _lastCameraCount,
                    ResponsiveCameraEditorUtil.ResetNewListRow,
                    canMutate))
                return;

            serializedObject.ApplyModifiedProperties();
            serializedObject.Update();
            cameras = serializedObject.FindProperty("cameras");
        }

        void DrawBulkCameraCopy(
            ResponsiveCamera component,
            SerializedProperty cameras) =>
            _bulkCopy.Draw(
                cameras.serializedObject,
                "Bulk Copy Camera Slots",
                "Copy Across All Cameras",
                "camera",
                ResponsiveUiSlotClipboard.Kind.Camera,
                cameras.arraySize,
                (source, destination) => ExecuteBulkCameraCopy(component, source, destination));

        static void ExecuteBulkCameraCopy(
            ResponsiveCamera component,
            int sourceSlotIndex,
            int destinationSlotIndex)
        {
            if (component == null ||
                !ResponsiveUiSlotClipboard.IsValidBulkCopy(
                    sourceSlotIndex,
                    destinationSlotIndex))
                return;

            var so = new SerializedObject(component);
            so.Update();
            SerializedProperty cameras = so.FindProperty("cameras");
            if (cameras.arraySize == 0)
                return;

            string undoName = ResponsiveUiBulkCopyUtil.SlotCopyUndoName(
                sourceSlotIndex, destinationSlotIndex);
            int copied = 0;
            if (!ResponsiveUiBulkCopyUtil.Execute(
                    undoName,
                    GetBulkCopyUndoTargets(component),
                    () =>
                    {
                        copied = ResponsiveCameraEditorUtil.CopySlotAcrossLayouts(
                            cameras,
                            sourceSlotIndex,
                            destinationSlotIndex);
                        return copied;
                    },
                    () =>
                    {
                        component.NotifyWidgetLivePreview();
                        so.ApplyModifiedProperties();
                        EditorUtility.SetDirty(component);

                        ResponsiveUI hub = component.Hub;
                        if (component.isActiveAndEnabled && hub != null && hub.enabled)
                        {
                            ResponsiveDevice.LayoutKey layout =
                                ResponsiveUiDetectionDrawer.ResolveInspectorLayout(so, hub);
                            using (ResponsiveCameraSync.SuppressCapture())
                                component.EditorApplyCameras(layout);
                        }
                    }))
                return;

            ResponsiveUiSlotClipboard.LogBulkCopyResult(
                ResponsiveUiSlotClipboard.Kind.Camera,
                component,
                sourceSlotIndex,
                destinationSlotIndex,
                copied);
            ResponsiveUiBulkCopyUtil.RepaintPreview();
        }

        static Object[] GetBulkCopyUndoTargets(ResponsiveCamera component)
        {
            var targets = new List<Object> { component };
            ResponsiveCameraLayout[] layouts = component.Cameras;
            if (layouts == null)
                return targets.ToArray();

            for (int i = 0; i < layouts.Length; i++)
            {
                Camera camera = layouts[i].target;
                ResponsiveUiBulkCopyUtil.AddUndoTarget(targets, camera);
                if (camera != null)
                    ResponsiveUiBulkCopyUtil.AddUndoTarget(targets, camera.transform);

                SpriteRenderer background = layouts[i].background;
                ResponsiveUiBulkCopyUtil.AddUndoTarget(targets, background);
                if (background != null)
                    ResponsiveUiBulkCopyUtil.AddUndoTarget(targets, background.transform);
            }

            return targets.ToArray();
        }

        static void EnsureDefaultCamera(SerializedProperty cameras)
        {
            if (cameras == null)
                return;

            var component = cameras.serializedObject.targetObject as ResponsiveCamera;
            if (component == null || !component.ConsumeSeedDefaultLayout())
                return;
            if (!cameras.isArray || cameras.arraySize > 0)
                return;

            cameras.arraySize = 1;
            SerializedProperty layout = cameras.GetArrayElementAtIndex(0);
            SerializedProperty fitMode = layout.FindPropertyRelative("spriteFitMode");
            if (fitMode != null)
                fitMode.intValue = (int)ResponsiveCameraSpriteFitMode.Cover;
            ResponsiveCameraEditorUtil.SeedSlots(layout);
            cameras.serializedObject.ApplyModifiedProperties();
            cameras.serializedObject.Update();
        }

        #endregion
    }
}

#endif
