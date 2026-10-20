#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

using FDPlus.ResponsiveUI;

namespace FDPlus.ResponsiveUI.Editor
{
/// <summary>
/// Draws <see cref="ResponsiveWidgetLayout"/> with live preview:
/// changing the <b>active</b> slot writes straight to the target RectTransform.
/// On <see cref="ResponsiveUiController"/> and
/// <see cref="ResponsiveTwoSideUiTweenController"/>, toggling Enabled on the live
/// slot Applies the resolved layout (Cover off → Phone). The Target GameObject stays
/// as authored. Other slots save without touching the Target (avoids capture
/// writing them into the active slot).
/// Assigning a Target seeds layout from the rect; Enabled defaults apply only on first assign
/// (including replace of a Missing ref). Clearing Target to None resets every slot.
/// Slots blocked by Player Allowed Orientations are hidden and Disabled.
/// Allowed-orientation slots are shown; the detected Simulator slot auto-expands.
/// FoldPhoneCover / Fold HalfOpened / Flip HalfOpened: new widgets start Enabled;
/// Flip Cover Landscape starts Disabled.
/// Enabling a slot copies <see cref="ResponsiveRectStateEditorUtil.FindFallbackSource"/>.
/// </summary>
[CustomPropertyDrawer(typeof(ResponsiveWidgetLayout))]
public class ResponsiveWidgetLayoutDrawer : PropertyDrawer
{
    #region Inspector

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        => GetPropertyHeight(property, true);

    internal static float GetPropertyHeightWithoutTarget(
        SerializedProperty property) =>
        GetPropertyHeight(property, false);

    static float GetPropertyHeight(
        SerializedProperty property,
        bool drawTarget)
    {
        ResponsiveUiDetectionDrawer.ApplyDetectedFoldouts(property);

        if (!property.isExpanded)
            return EditorGUIUtility.singleLineHeight;

        float s = EditorGUIUtility.standardVerticalSpacing;
        float h = EditorGUIUtility.singleLineHeight + s;
        if (drawTarget)
        {
            h += EditorGUI.GetPropertyHeight(
                property.FindPropertyRelative("target"),
                true) + s;
        }

        string[] states = ResponsiveWidgetLayout.StatePropertyNames;
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
        => OnGUI(position, property, label, true);

    internal static void OnGUIWithoutTarget(
        Rect position,
        SerializedProperty property,
        GUIContent label) =>
        OnGUI(position, property, label, false);

    static void OnGUI(
        Rect position,
        SerializedProperty property,
        GUIContent label,
        bool drawTarget)
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

                float h;
                Rect r;
                if (drawTarget)
                {
                    h = EditorGUI.GetPropertyHeight(targetProp, true);
                    r = new Rect(content.x, y, content.width, h);
                    Object previousTarget = targetProp.objectReferenceValue;
                    int previousTargetId = targetProp.objectReferenceInstanceIDValue;
                    EditorGUI.BeginChangeCheck();
                    EditorGUI.PropertyField(r, targetProp, true);
                    if (EditorGUI.EndChangeCheck())
                    {
                        ResponsiveRectStateEditorUtil.ApplyInspectorTargetChange(
                            property,
                            previousTarget,
                            previousTargetId,
                            HubOf(property.serializedObject));
                        PreviewControllerWidgets(property);
                    }
                    y += h + s;
                }

                string[] stateProps = ResponsiveWidgetLayout.StatePropertyNames;
                string group = "";
                for (int i = 0; i < stateProps.Length; i++)
                {
                    if (!ResponsiveUiDetectionDrawer.ShouldDrawState(property.serializedObject, stateProps[i]))
                        continue;

                    y += ResponsiveSlotGroupUi.Advance(
                        ref group, i, new Rect(content.x, y, content.width, line), true);

                    SerializedProperty stateProp = property.FindPropertyRelative(stateProps[i]);
                    h = EditorGUI.GetPropertyHeight(stateProp, true);
                    r = new Rect(content.x, y, content.width, h);
                    ResponsiveRectState before = ResponsiveRectStateEditorUtil.ReadState(stateProp);
                    EditorGUI.BeginChangeCheck();
                    EditorGUI.PropertyField(
                        r,
                        stateProp,
                        new GUIContent(ResponsiveWidgetLayout.StatePropertyLabel(i)),
                        true);
                    if (EditorGUI.EndChangeCheck())
                    {
                        ResponsiveRectState after = ResponsiveRectStateEditorUtil.ReadState(stateProp);
                        // Foldout expand/collapse sets GUI.changed without authoring edits.
                        // LivePush in that case would Apply stored pixels × ratio and jump the Target.
                        if (!ResponsiveRectStateEditorUtil.SameAuthoring(before, after))
                        {
                            bool scaleToggle = before.scaleFromDesignCanvas != after.scaleFromDesignCanvas;
                            bool modeChanged = after.scaleFromDesignCanvas &&
                                before.designScaleMode != after.designScaleMode;
            if (scaleToggle || modeChanged)
                                RecaptureLiveForDesignScaleToggle(property, targetProp, stateProp);
                            else if (ResponsiveRectStateEditorUtil.IsEnabled(stateProp) &&
                                (before.enabled == after.enabled ||
                                 !PreviewResolvedLiveSlot(property, stateProp)))
                                LivePush(property, targetProp, stateProp);
                            else if (before.enabled != after.enabled)
                                PreviewResolvedLiveSlot(property, stateProp);
                        }
                    }
                    y += h + s;
                }
            });
        EditorGUI.EndProperty();
    }

    static ResponsiveUI HubOf(SerializedObject so) =>
        ResponsiveUiDetectionDrawer.HandlerOf(so) as ResponsiveUI;

    #endregion

    #region Capture

    /// <summary>
    /// Scale From Design Canvas stores anchoredPosition in design-space and Apply
    /// multiplies by canvas ratio. Toggling it, or changing Scale Mode while it is
    /// on, must recapture the live Target so the object does not jump.
    /// </summary>
    static void RecaptureLiveForDesignScaleToggle(
        SerializedProperty widgetProp,
        SerializedProperty targetProp,
        SerializedProperty stateProp)
    {
        ResponsiveUI responsiveUi = HubOf(widgetProp.serializedObject);
        if (responsiveUi != null)
            responsiveUi.NotifyWidgetLivePreview();

        widgetProp.serializedObject.ApplyModifiedProperties();

        if (responsiveUi == null ||
            targetProp.objectReferenceValue is not RectTransform rt)
            return;

        ResponsiveDevice.LayoutKey inspectorLayout =
            ResponsiveUiDetectionDrawer.ResolveInspectorLayout(
                widgetProp.serializedObject, responsiveUi);
        if (!ResponsiveRectStateEditorUtil.IsActiveEditorSlot(
                widgetProp,
                inspectorLayout,
                stateProp.name))
            return;

        using (ResponsiveUiWidgetSync.SuppressCapture())
        {
            ResponsiveRectStateEditorUtil.RecaptureSlotFromLive(stateProp, rt, responsiveUi);
            if (responsiveUi != null)
                responsiveUi.NotifyWidgetLivePreview();
            widgetProp.serializedObject.ApplyModifiedProperties();
            ResponsiveUiWidgetSync.IgnoreTargetCapture(rt);
        }
    }

    #endregion

    #region LivePush

    static void LivePush(
        SerializedProperty widgetProp,
        SerializedProperty targetProp,
        SerializedProperty stateProp)
    {
        Object targetRef = targetProp.objectReferenceValue;
        ResponsiveUI responsiveUi = HubOf(widgetProp.serializedObject);

        int slotIndex = ResponsiveWidgetLayout.StatePropertyIndex(stateProp.name);
        ResponsiveRectStateEditorUtil.EnsureDesignScaleDefaults(stateProp, slotIndex);

        // Skip OnValidate Apply so it does not overwrite the rect we are about to push.
        // Must run immediately before ApplyModifiedProperties (not in a later delayCall).
        if (responsiveUi != null)
            responsiveUi.NotifyWidgetLivePreview();

        widgetProp.serializedObject.ApplyModifiedProperties();

        // Serialized edits still save. Preview needs an enabled hub; otherwise leave Target.
        if (responsiveUi == null || !responsiveUi.enabled)
            return;

        ResponsiveDevice.LayoutKey inspectorLayout =
            ResponsiveUiDetectionDrawer.ResolveInspectorLayout(
                widgetProp.serializedObject, responsiveUi);
        if (!ResponsiveRectStateEditorUtil.IsActiveEditorSlot(
                widgetProp,
                inspectorLayout,
                stateProp.name))
            return;

        if (targetRef is not RectTransform rt)
            return;

        ResponsiveDevice.FormFactor slotForm =
            ResponsiveWidgetLayout.SlotFormFactor(slotIndex);
        ResponsiveDevice.Orientation slotOrientation =
            ResponsiveWidgetLayout.SlotOrientation(slotIndex);

        // Hold suppress past this call — Undo.postprocessModifications can run after Dispose.
        using (ResponsiveUiWidgetSync.SuppressCapture())
        {
            Canvas.ForceUpdateCanvases();
            Undo.RecordObject(rt, "Responsive Widget Preview");
            ResponsiveRectState state = ResponsiveRectStateEditorUtil.ReadState(stateProp);
            state.enabled = true;
            responsiveUi.ApplyWidget(
                rt,
                state,
                slotForm,
                slotOrientation);
            EditorUtility.SetDirty(rt);
            ResponsiveUiWidgetSync.IgnoreTargetCapture(rt);
        }

        // Defer view refresh — SceneView.RepaintAll during MouseDown/PropertyField
        // can trip Unity's OnGUIDepth warning.
        EditorApplication.delayCall += () =>
        {
            SceneView.RepaintAll();
            EditorApplication.QueuePlayerLoopUpdate();
        };
    }

    /// <summary>
    /// Enabled toggle on the live slot Applies the resolved layout for both
    /// <see cref="ResponsiveUiController"/> and
    /// <see cref="ResponsiveTwoSideUiTweenController"/> (Cover off → Phone).
    /// The Target GameObject stays as authored. Two-side visibility stays on
    /// <c>SetVisible</c>.
    /// </summary>
    static bool PreviewResolvedLiveSlot(
        SerializedProperty widgetProp,
        SerializedProperty stateProp)
    {
        UnityEngine.Object owner = widgetProp.serializedObject.targetObject;
        ResponsiveUI hub = HubOf(widgetProp.serializedObject);
        ResponsiveDevice.LayoutKey layout =
            ResponsiveUiDetectionDrawer.ResolveInspectorLayout(
                widgetProp.serializedObject, hub);
        // Any slot in the live fallback chain can change the resolved layout
        // (Pad off → Phone Landscape active; then Phone Landscape off → Phone Portrait).
        if (!IsInLiveFallbackChain(layout, stateProp.name))
            return false;

        if (owner is ResponsiveUiController)
        {
            if (!CanPreviewControllerWidgets(widgetProp, out _, out _))
                return false;
            PreviewControllerWidgets(widgetProp);
            return true;
        }

        if (owner is ResponsiveTwoSideUiTweenController twoSide)
        {
            if (!twoSide.isActiveAndEnabled || hub == null || !hub.enabled)
                return false;
            PreviewTwoSideWidgets(widgetProp, twoSide, hub, layout);
            return true;
        }

        return false;
    }

    static bool IsInLiveFallbackChain(
        in ResponsiveDevice.LayoutKey layout,
        string slotName) =>
        ResponsiveRectStateEditorUtil.FindFallbackChainSlot(
            layout, name => name == slotName, out _, out _);

    static void PreviewTwoSideWidgets(
        SerializedProperty widgetProp,
        ResponsiveTwoSideUiTweenController twoSide,
        ResponsiveUI hub,
        in ResponsiveDevice.LayoutKey layout)
    {
        hub.NotifyWidgetLivePreview();
        widgetProp.serializedObject.ApplyModifiedProperties();

        using (ResponsiveUiWidgetSync.SuppressCapture())
        {
            RecordTwoSideWidgetPreviewUndo(twoSide);
            twoSide.EditorApplyLayouts(layout);
        }

        EditorApplication.delayCall += () =>
        {
            SceneView.RepaintAll();
            EditorApplication.QueuePlayerLoopUpdate();
        };
    }

    static void PreviewControllerWidgets(SerializedProperty widgetProp)
    {
        if (!CanPreviewControllerWidgets(
                widgetProp,
                out ResponsiveUiController controller,
                out ResponsiveUI hub))
        {
            widgetProp.serializedObject.ApplyModifiedProperties();
            return;
        }

        hub.NotifyWidgetLivePreview();
        widgetProp.serializedObject.ApplyModifiedProperties();

        ResponsiveDevice.LayoutKey layout =
            ResponsiveUiDetectionDrawer.ResolveInspectorLayout(
                widgetProp.serializedObject, hub);
        using (ResponsiveUiWidgetSync.SuppressCapture())
        {
            RecordControllerWidgetPreviewUndo(controller);
            controller.EditorApplyWidgets(layout);
        }

        EditorApplication.delayCall += () =>
        {
            SceneView.RepaintAll();
            EditorApplication.QueuePlayerLoopUpdate();
        };
    }

    static bool CanPreviewControllerWidgets(
        SerializedProperty widgetProp,
        out ResponsiveUiController controller,
        out ResponsiveUI hub)
    {
        controller = widgetProp.serializedObject.targetObject as ResponsiveUiController;
        hub = HubOf(widgetProp.serializedObject);
        return controller != null &&
            controller.isActiveAndEnabled &&
            hub != null &&
            hub.enabled;
    }

    static void RecordControllerWidgetPreviewUndo(ResponsiveUiController controller)
    {
        ResponsiveWidgetLayout[] widgets = controller.Widgets;
        if (widgets == null)
            return;

        for (int i = 0; i < widgets.Length; i++)
            RecordWidgetTargetUndo(widgets[i].target);
    }

    static void RecordTwoSideWidgetPreviewUndo(
        ResponsiveTwoSideUiTweenController twoSide)
    {
        ResponsiveTwoSideWidget[] widgets = twoSide.Widgets;
        if (widgets == null)
            return;

        for (int i = 0; i < widgets.Length; i++)
        {
            RecordWidgetTargetUndo(widgets[i].leftNormal.target);
            RecordWidgetTargetUndo(widgets[i].leftAnimationStart.target);
            RecordWidgetTargetUndo(widgets[i].rightNormal.target);
            RecordWidgetTargetUndo(widgets[i].rightAnimationStart.target);
        }
    }

    static void RecordWidgetTargetUndo(RectTransform target)
    {
        if (target == null)
            return;
        Undo.RecordObject(target, "Responsive Widget Preview");
    }

    #endregion
}
}

#endif
