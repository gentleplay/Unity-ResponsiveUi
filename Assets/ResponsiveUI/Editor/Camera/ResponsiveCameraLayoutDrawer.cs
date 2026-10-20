#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

using FDPlus.ResponsiveUI;

namespace FDPlus.ResponsiveUI.Editor
{
/// <summary>
/// Draws <see cref="ResponsiveCameraLayout"/>: Target assign seeds slots; active-slot edits
/// LivePush to the Camera; other slots only save. Clearing Target to None resets every slot. Orientation gating,
/// auto-expand, and copy-Phone-on-enable match widget layouts.
/// </summary>
[CustomPropertyDrawer(typeof(ResponsiveCameraLayout))]
public class ResponsiveCameraLayoutDrawer : PropertyDrawer
{
    #region Inspector

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        ResponsiveUiDetectionDrawer.ApplyDetectedFoldouts(property);

        if (!property.isExpanded)
            return EditorGUIUtility.singleLineHeight;

        float s = EditorGUIUtility.standardVerticalSpacing;
        float h = EditorGUIUtility.singleLineHeight + s
            + EditorGUI.GetPropertyHeight(property.FindPropertyRelative("target"), true) + s
            + EditorGUI.GetPropertyHeight(property.FindPropertyRelative("spriteFitMode"), true) + s
            + EditorGUI.GetPropertyHeight(property.FindPropertyRelative("background"),
                new GUIContent("Camera Background"), true) + s;

        string[] states = ResponsiveCameraLayout.StatePropertyNames;
        string group = "";
        for (int i = 0; i < states.Length; i++)
        {
            if (!ResponsiveUiDetectionDrawer.ShouldDrawState(property.serializedObject, states[i]))
                continue;

            h += ResponsiveSlotGroupUi.Advance(ref group, i, default, false);
            SerializedProperty stateProp = property.FindPropertyRelative(states[i]);
            h += EditorGUI.GetPropertyHeight(stateProp, true) + s;
        }

        return h;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);
        ResponsiveUiDetectionDrawer.ApplyDetectedFoldouts(property);

        SerializedProperty targetProp = property.FindPropertyRelative("target");

        float line = EditorGUIUtility.singleLineHeight;
        float s = EditorGUIUtility.standardVerticalSpacing;

        ResponsiveUiInspectorFoldouts.DrawPropertyFoldout(
            position,
            property,
            label,
            content =>
            {
                float y = position.y + line + s;

                float h = EditorGUI.GetPropertyHeight(targetProp, true);
                Rect r = new Rect(content.x, y, content.width, h);
                Object previousTarget = targetProp.objectReferenceValue;
                int previousTargetId = targetProp.objectReferenceInstanceIDValue;
                EditorGUI.BeginChangeCheck();
                EditorGUI.PropertyField(r, targetProp, true);
                if (EditorGUI.EndChangeCheck())
                {
                    Camera cam = ResponsiveCameraEditorUtil.ApplyInspectorTargetChange(
                        property,
                        previousTarget,
                        previousTargetId);
                    if (cam != null)
                        LivePushActiveSlot(property);
                    else
                        property.serializedObject.ApplyModifiedProperties();
                }

                y += h + s;

                SerializedProperty fitProp = property.FindPropertyRelative("spriteFitMode");
                float fitH = EditorGUI.GetPropertyHeight(fitProp, true);
                EditorGUI.BeginChangeCheck();
                EditorGUI.PropertyField(new Rect(content.x, y, content.width, fitH), fitProp, true);
                bool fitChanged = EditorGUI.EndChangeCheck();
                y += fitH + s;

                SerializedProperty backgroundProp = property.FindPropertyRelative("background");
                float backgroundH = EditorGUI.GetPropertyHeight(
                    backgroundProp, new GUIContent("Camera Background"), true);
                EditorGUI.BeginChangeCheck();
                EditorGUI.PropertyField(
                    new Rect(content.x, y, content.width, backgroundH),
                    backgroundProp,
                    new GUIContent(
                        "Camera Background",
                        "Single SpriteRenderer fitted behind all content visible to this camera."),
                    true);
                bool backgroundChanged = EditorGUI.EndChangeCheck();
                y += backgroundH + s;

                if ((fitChanged || backgroundChanged) &&
                    targetProp.objectReferenceValue is Camera)
                {
                    property.serializedObject.ApplyModifiedProperties();
                    ResponsiveCamera owner = property.serializedObject.targetObject as ResponsiveCamera;
                    if (owner != null && owner.isActiveAndEnabled)
                        PreviewCameras(owner, property);
                    else if (targetProp.objectReferenceValue is Camera backgroundCamera)
                        ResponsiveCameraEditorUtil.FitBackground(property, backgroundCamera);
                }

                string[] states = ResponsiveCameraLayout.StatePropertyNames;
                string group = "";
                for (int i = 0; i < states.Length; i++)
                {
                    if (!ResponsiveUiDetectionDrawer.ShouldDrawState(property.serializedObject, states[i]))
                        continue;

                    y += ResponsiveSlotGroupUi.Advance(
                        ref group, i, new Rect(content.x, y, content.width, line), true);

                    SerializedProperty stateProp = property.FindPropertyRelative(states[i]);
                    float sh = EditorGUI.GetPropertyHeight(stateProp, true);
                    bool scaleBefore =
                        stateProp.FindPropertyRelative("scaleFromUiSize")?.boolValue == true;
                    bool projectionScaleBefore = FindScaleProjection(stateProp)?.boolValue == true;
                    int modeBefore = stateProp.FindPropertyRelative("uiScaleMode")?.intValue ?? 0;
                    ResponsiveCameraState before = ResponsiveCameraEditorUtil.ReadState(stateProp);
                    EditorGUI.BeginChangeCheck();
                    EditorGUI.PropertyField(
                        new Rect(content.x, y, content.width, sh),
                        stateProp,
                        new GUIContent(ResponsiveWidgetLayout.StatePropertyLabel(i)),
                        true);
                    bool changed = EditorGUI.EndChangeCheck() ||
                        property.serializedObject.hasModifiedProperties;
                    if (changed &&
                        stateProp.FindPropertyRelative("enabled").boolValue)
                    {
                        ResponsiveCameraState after = ResponsiveCameraEditorUtil.ReadState(stateProp);
                        if (!ResponsiveCameraEditorUtil.SameAuthoring(before, after))
                        {
                            bool scaleAfter =
                                stateProp.FindPropertyRelative("scaleFromUiSize")?.boolValue == true;
                            bool projectionScaleAfter = FindScaleProjection(stateProp)?.boolValue == true;
                            int modeAfter = stateProp.FindPropertyRelative("uiScaleMode")?.intValue ?? 0;
                            bool flagsChanged =
                                scaleBefore != scaleAfter ||
                                projectionScaleBefore != projectionScaleAfter;
                            bool modeChanged =
                                modeBefore != modeAfter && (scaleAfter || projectionScaleAfter);
                            bool boundsScaleChanged =
                                before.focusBoundsLayout.scaleFromDesignCanvas !=
                                after.focusBoundsLayout.scaleFromDesignCanvas ||
                                before.focusBoundsLayout.designScaleMode !=
                                after.focusBoundsLayout.designScaleMode;
                            if (flagsChanged || modeChanged)
                                RecaptureLiveForScaleToggle(property, targetProp, stateProp);
                            else if (boundsScaleChanged)
                            {
                                if (IsActiveEditorSlot(property, stateProp))
                                    ResponsiveCameraEditorUtil.CaptureFocusBoundsLayoutInto(
                                        stateProp, skipIfMatches: false);
                                LivePush(property, targetProp, stateProp);
                            }
                            else
                                LivePush(property, targetProp, stateProp);
                        }
                    }
                    else if (changed && before.enabled)
                    {
                        // Enabled turned off: the Apply slot falls back (Portrait / Phone).
                        // Preview the resolved slot so the camera does not keep the
                        // disabled slot's pose until the next Simulator change.
                        ResponsiveCamera owner =
                            property.serializedObject.targetObject as ResponsiveCamera;
                        owner?.NotifyWidgetLivePreview();
                        property.serializedObject.ApplyModifiedProperties();
                        if (owner != null && owner.isActiveAndEnabled &&
                            targetProp.objectReferenceValue is Camera)
                            PreviewCameras(owner, property);
                    }

                    y += sh + s;
                }
            });
        EditorGUI.EndProperty();
    }

    #endregion

    #region Capture / LivePush

    static void RecaptureLiveForScaleToggle(
        SerializedProperty layoutProp,
        SerializedProperty targetProp,
        SerializedProperty stateProp)
    {
        ResponsiveCamera responsiveCamera =
            layoutProp.serializedObject.targetObject as ResponsiveCamera;
        responsiveCamera?.NotifyWidgetLivePreview();
        layoutProp.serializedObject.ApplyModifiedProperties();

        if (targetProp.objectReferenceValue is not Camera camera ||
            !IsActiveEditorSlot(layoutProp, stateProp))
            return;

        int slotIndex = ResponsiveWidgetLayout.StatePropertyIndex(stateProp.name);
        ResponsiveDevice.FormFactor slotForm =
            ResponsiveWidgetLayout.SlotFormFactor(slotIndex);
        ResponsiveDevice.Orientation slotOrientation =
            ResponsiveWidgetLayout.SlotOrientation(slotIndex);
        Vector2 uiSize = responsiveCamera != null
            ? responsiveCamera.GetUiLayoutSize()
            : ResponsiveDevice.GetWatchScreenSize();

        Vector3 localPosition = camera.transform.localPosition;
        float orthographicSize = camera.orthographicSize;
        float fieldOfView = camera.fieldOfView;
        bool scalePosition =
            stateProp.FindPropertyRelative("scaleFromUiSize")?.boolValue == true;
        bool scaleProjection = FindScaleProjection(stateProp)?.boolValue == true;
        SerializedProperty modeProp = stateProp.FindPropertyRelative("uiScaleMode");
        var mode = modeProp != null
            ? (ResponsiveDesignScaleMode)modeProp.intValue
            : ResponsiveDesignScaleMode.Width;
        ResponsiveCameraEditorUtil.UnscaleLiveToDesign(
            slotForm,
            slotOrientation,
            uiSize,
            mode,
            scalePosition,
            scaleProjection,
            ref localPosition,
            ref orthographicSize,
            ref fieldOfView);

        using (ResponsiveCameraSync.SuppressCapture())
        {
            ResponsiveCameraSync.IgnoreTargetCapture(camera);
            stateProp.FindPropertyRelative("orthographicSize").floatValue = orthographicSize;
            stateProp.FindPropertyRelative("fieldOfView").floatValue = fieldOfView;
            if (scalePosition ||
                stateProp.FindPropertyRelative("applyLocalPosition").boolValue ||
                stateProp.FindPropertyRelative("applyLocalRotation").boolValue)
            {
                stateProp.FindPropertyRelative("localPosition").vector3Value = localPosition;
            }
            layoutProp.serializedObject.ApplyModifiedProperties();
            responsiveCamera?.NotifyWidgetLivePreview();
        }
    }

    static SerializedProperty FindScaleProjection(SerializedProperty stateProp) =>
        stateProp.FindPropertyRelative("scaleProjection");

    static void LivePushActiveSlot(SerializedProperty layoutProp)
    {
        ResponsiveDevice.LayoutKey layout =
            ResponsiveUiDetectionDrawer.ResolveInspectorLayout(
                layoutProp.serializedObject,
                ResponsiveUiDetectionDrawer.HandlerOf(layoutProp.serializedObject));
        ResponsiveRectStateEditorUtil.ResolveCaptureSlot(
            layoutProp, layout, out string active, out _);

        SerializedProperty state = layoutProp.FindPropertyRelative(active);
        if (state == null || state.FindPropertyRelative("enabled")?.boolValue != true)
        {
            // Still commit first-assign / retarget capture written above.
            layoutProp.serializedObject.ApplyModifiedProperties();
            return;
        }

        LivePush(
            layoutProp,
            layoutProp.FindPropertyRelative("target"),
            state);
    }

    static void LivePush(
        SerializedProperty layoutProp,
        SerializedProperty targetProp,
        SerializedProperty stateProp)
    {
        ResponsiveCamera responsiveCamera =
            layoutProp.serializedObject.targetObject as ResponsiveCamera;
        responsiveCamera?.NotifyWidgetLivePreview();
        layoutProp.serializedObject.ApplyModifiedProperties();

        if (!IsActiveEditorSlot(layoutProp, stateProp) ||
            targetProp.objectReferenceValue is not Camera camera)
            return;

        if (responsiveCamera != null && responsiveCamera.isActiveAndEnabled)
            PreviewCameras(responsiveCamera, layoutProp);
        else
            PushStateToCamera(responsiveCamera, layoutProp, stateProp, camera);
    }

    static bool IsActiveEditorSlot(
        SerializedProperty layoutProp,
        SerializedProperty stateProp)
    {
        ResponsiveDevice.LayoutKey layout =
            ResponsiveUiDetectionDrawer.ResolveInspectorLayout(
                layoutProp.serializedObject,
                ResponsiveUiDetectionDrawer.HandlerOf(layoutProp.serializedObject));
        ResponsiveRectStateEditorUtil.ResolveCaptureSlot(
            layoutProp, layout, out string active, out _);
        return stateProp.name == active;
    }

    static void PreviewCameras(ResponsiveCamera owner, SerializedProperty layoutProp)
    {
        if (owner == null)
            return;

        // Apply after the Inspector finishes. Nested PropertyDrawers call this
        // mid-OnGUI, before the outer SerializedObject has flushed slot fields
        // onto the component — EditorApplyCameras would preview stale cameras[].
        // Disable/enable worked because OnEnable ApplyNow runs after that flush.
        ResponsiveDevice.LayoutKey layout = owner.Hub != null
            ? ResponsiveUiDetectionDrawer.ResolveInspectorLayout(
                layoutProp.serializedObject, owner.Hub)
            : owner.GetEditorCaptureLayout();
        EditorApplication.delayCall += () =>
        {
            if (owner == null || !owner.isActiveAndEnabled)
                return;

            using (ResponsiveUiWidgetSync.SuppressCapture())
            using (ResponsiveCameraSync.SuppressCapture())
            {
                ResponsiveCameraEditorUtil.RecordPreviewTargets(owner);
                owner.EditorApplyCameras(layout);
            }

            SceneView.RepaintAll();
            EditorApplication.QueuePlayerLoopUpdate();
        };
    }

    static void PushStateToCamera(
        ResponsiveCamera responsiveCamera,
        SerializedProperty layoutProp,
        SerializedProperty stateProp,
        Camera camera)
    {
        ResponsiveCameraState state = ResponsiveCameraEditorUtil.ReadState(stateProp);
        int slotIndex = ResponsiveWidgetLayout.StatePropertyIndex(stateProp.name);
        ResponsiveDevice.FormFactor slotForm = ResponsiveWidgetLayout.SlotFormFactor(slotIndex);
        Vector2 uiSize = responsiveCamera != null
            ? responsiveCamera.GetUiLayoutSize()
            : Vector2.zero;
        bool applyViewport = responsiveCamera == null ||
                             camera != null;
        using (ResponsiveUiWidgetSync.SuppressCapture())
        using (ResponsiveCameraSync.SuppressCapture())
        {
            ResponsiveCameraSync.IgnoreTargetCapture(camera);
            Undo.RecordObject(camera.transform, "Responsive Camera Preview");
            Undo.RecordObject(camera, "Responsive Camera Preview");
            state.Apply(
                camera,
                applyViewport,
                uiSize,
                slotForm,
                ResponsiveWidgetLayout.SlotOrientation(slotIndex));
            ResponsiveCameraEditorUtil.ApplyFocus(stateProp, layoutProp, camera);
            EditorUtility.SetDirty(camera);
            EditorUtility.SetDirty(camera.transform);
            ResponsiveCameraEditorUtil.FitBackground(layoutProp, camera);
        }

        EditorApplication.delayCall += () =>
        {
            SceneView.RepaintAll();
            EditorApplication.QueuePlayerLoopUpdate();
        };
    }

    #endregion
}
}

#endif
