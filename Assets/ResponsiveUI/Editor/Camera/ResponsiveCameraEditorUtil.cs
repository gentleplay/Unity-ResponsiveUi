#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

using FDPlus.ResponsiveUI;
using FDPlus.ResponsiveUI.Shared.Editor;

namespace FDPlus.ResponsiveUI.Editor
{
/// <summary>
/// Shared Edit Mode helpers for <see cref="ResponsiveCamera"/> drawers and Scene→slot sync.
/// Mirrors <see cref="ResponsiveRectStateEditorUtil"/> for widgets.
/// </summary>
public static class ResponsiveCameraEditorUtil
{
    #region Constants

    const float PositionEpsilon = 5e-4f;
    const float AngleEpsilon = 0.05f;
    const float ProjectionEpsilon = 5e-4f;

    #endregion

    #region Serialized

    public static ResponsiveCameraState ReadState(SerializedProperty state)
    {
        if (state == null)
            return ResponsiveCameraState.CreateDefault();

        ResponsiveCameraState result = ResponsiveCameraState.CreateDefault();

        SerializedProperty enabled = state.FindPropertyRelative("enabled");
        if (enabled != null)
            result.enabled = enabled.boolValue;

        SerializedProperty viewport = state.FindPropertyRelative("viewport");
        if (viewport != null)
            result.viewport = viewport.rectValue;

        SerializedProperty applyLocalPosition = state.FindPropertyRelative("applyLocalPosition");
        if (applyLocalPosition != null)
            result.applyLocalPosition = applyLocalPosition.boolValue;

        SerializedProperty localPosition = state.FindPropertyRelative("localPosition");
        if (localPosition != null)
            result.localPosition = localPosition.vector3Value;

        SerializedProperty applyLocalRotation = state.FindPropertyRelative("applyLocalRotation");
        if (applyLocalRotation != null)
            result.applyLocalRotation = applyLocalRotation.boolValue;

        SerializedProperty localEulerAngles = state.FindPropertyRelative("localEulerAngles");
        if (localEulerAngles != null)
            result.localEulerAngles = localEulerAngles.vector3Value;

        SerializedProperty scaleFromUiSize = state.FindPropertyRelative("scaleFromUiSize");
        if (scaleFromUiSize != null)
            result.scaleFromUiSize = scaleFromUiSize.boolValue;

        SerializedProperty scaleProjection = state.FindPropertyRelative("scaleProjection");
        if (scaleProjection != null)
            result.scaleProjection = scaleProjection.boolValue;

        SerializedProperty uiScaleMode = state.FindPropertyRelative("uiScaleMode");
        if (uiScaleMode != null)
            result.uiScaleMode = (ResponsiveDesignScaleMode)uiScaleMode.intValue;

        SerializedProperty orthographic = state.FindPropertyRelative("orthographic");
        if (orthographic != null)
            result.orthographic = orthographic.boolValue;

        SerializedProperty applyFieldOfView = state.FindPropertyRelative("applyFieldOfView");
        if (applyFieldOfView != null)
            result.applyFieldOfView = applyFieldOfView.boolValue;

        SerializedProperty fieldOfView = state.FindPropertyRelative("fieldOfView");
        if (fieldOfView != null)
            result.fieldOfView = fieldOfView.floatValue;

        SerializedProperty applyOrthographicSize = state.FindPropertyRelative("applyOrthographicSize");
        if (applyOrthographicSize != null)
            result.applyOrthographicSize = applyOrthographicSize.boolValue;

        SerializedProperty orthographicSize = state.FindPropertyRelative("orthographicSize");
        if (orthographicSize != null)
            result.orthographicSize = orthographicSize.floatValue;

        SerializedProperty focusTarget = state.FindPropertyRelative("focusTarget");
        if (focusTarget != null)
            result.focusTarget = focusTarget.objectReferenceValue as Transform;

        SerializedProperty focusScreen = state.FindPropertyRelative("focusScreen");
        if (focusScreen != null)
            result.focusScreen = (ResponsiveCameraFocusScreen)focusScreen.intValue;

        SerializedProperty focusFit = state.FindPropertyRelative("focusFit");
        if (focusFit != null)
            result.focusFit = (ResponsiveCameraFocusFit)focusFit.intValue;

        SerializedProperty focusPadding = state.FindPropertyRelative("focusPadding");
        if (focusPadding != null)
            result.focusPadding = focusPadding.floatValue;

        SerializedProperty focusBounds = state.FindPropertyRelative("focusBounds");
        if (focusBounds != null)
            result.focusBounds = focusBounds.objectReferenceValue as RectTransform;

        SerializedProperty focusBoundsLayout = state.FindPropertyRelative("focusBoundsLayout");
        if (focusBoundsLayout != null)
            result.focusBoundsLayout = ResponsiveRectStateEditorUtil.ReadState(focusBoundsLayout);

        return result;
    }

    /// <summary>
    /// True when two slot snapshots match for Inspector LivePush. Foldout
    /// expand/collapse is not part of authoring and must not push the Target.
    /// </summary>
    public static bool SameAuthoring(in ResponsiveCameraState a, in ResponsiveCameraState b) =>
        a.enabled == b.enabled &&
        a.viewport == b.viewport &&
        a.applyLocalPosition == b.applyLocalPosition &&
        a.applyLocalRotation == b.applyLocalRotation &&
        a.localPosition == b.localPosition &&
        a.localEulerAngles == b.localEulerAngles &&
        a.scaleFromUiSize == b.scaleFromUiSize &&
        a.scaleProjection == b.scaleProjection &&
        a.uiScaleMode == b.uiScaleMode &&
        a.orthographic == b.orthographic &&
        a.applyFieldOfView == b.applyFieldOfView &&
        a.fieldOfView == b.fieldOfView &&
        a.applyOrthographicSize == b.applyOrthographicSize &&
        a.orthographicSize == b.orthographicSize &&
        a.focusTarget == b.focusTarget &&
        a.focusScreen == b.focusScreen &&
        a.focusFit == b.focusFit &&
        a.focusBounds == b.focusBounds &&
        a.focusBoundsLayout.HasSameLayout(b.focusBoundsLayout) &&
        Mathf.Approximately(a.focusPadding, b.focusPadding);

    public static void WriteState(SerializedProperty state, ResponsiveCameraState value)
    {
        if (state == null)
            return;

        SerializedProperty enabled = state.FindPropertyRelative("enabled");
        if (enabled != null)
            enabled.boolValue = value.enabled;

        SerializedProperty viewport = state.FindPropertyRelative("viewport");
        if (viewport != null)
            viewport.rectValue = value.viewport;

        SerializedProperty applyLocalPosition = state.FindPropertyRelative("applyLocalPosition");
        if (applyLocalPosition != null)
            applyLocalPosition.boolValue = value.applyLocalPosition;

        SerializedProperty localPosition = state.FindPropertyRelative("localPosition");
        if (localPosition != null)
            localPosition.vector3Value = value.localPosition;

        SerializedProperty applyLocalRotation = state.FindPropertyRelative("applyLocalRotation");
        if (applyLocalRotation != null)
            applyLocalRotation.boolValue = value.applyLocalRotation;

        SerializedProperty localEulerAngles = state.FindPropertyRelative("localEulerAngles");
        if (localEulerAngles != null)
            localEulerAngles.vector3Value = value.localEulerAngles;

        SerializedProperty scaleFromUiSize = state.FindPropertyRelative("scaleFromUiSize");
        if (scaleFromUiSize != null)
            scaleFromUiSize.boolValue = value.scaleFromUiSize;

        SerializedProperty scaleProjection = state.FindPropertyRelative("scaleProjection");
        if (scaleProjection != null)
            scaleProjection.boolValue = value.scaleProjection;

        SerializedProperty uiScaleMode = state.FindPropertyRelative("uiScaleMode");
        if (uiScaleMode != null)
            uiScaleMode.intValue = (int)value.uiScaleMode;

        SerializedProperty orthographic = state.FindPropertyRelative("orthographic");
        if (orthographic != null)
            orthographic.boolValue = value.orthographic;

        SerializedProperty applyFieldOfView = state.FindPropertyRelative("applyFieldOfView");
        if (applyFieldOfView != null)
            applyFieldOfView.boolValue = value.applyFieldOfView;

        SerializedProperty fieldOfView = state.FindPropertyRelative("fieldOfView");
        if (fieldOfView != null)
            fieldOfView.floatValue = value.fieldOfView;

        SerializedProperty applyOrthographicSize = state.FindPropertyRelative("applyOrthographicSize");
        if (applyOrthographicSize != null)
            applyOrthographicSize.boolValue = value.applyOrthographicSize;

        SerializedProperty orthographicSize = state.FindPropertyRelative("orthographicSize");
        if (orthographicSize != null)
            orthographicSize.floatValue = value.orthographicSize;

        SerializedProperty focusTarget = state.FindPropertyRelative("focusTarget");
        if (focusTarget != null)
            focusTarget.objectReferenceValue = value.focusTarget;

        SerializedProperty focusScreen = state.FindPropertyRelative("focusScreen");
        if (focusScreen != null)
            focusScreen.intValue = (int)value.focusScreen;

        SerializedProperty focusFit = state.FindPropertyRelative("focusFit");
        if (focusFit != null)
            focusFit.intValue = (int)value.focusFit;

        SerializedProperty focusPadding = state.FindPropertyRelative("focusPadding");
        if (focusPadding != null)
            focusPadding.floatValue = value.focusPadding > 0f ? value.focusPadding : 1f;

        SerializedProperty focusBounds = state.FindPropertyRelative("focusBounds");
        if (focusBounds != null)
            focusBounds.objectReferenceValue = value.focusBounds;

        SerializedProperty focusBoundsLayout = state.FindPropertyRelative("focusBoundsLayout");
        if (focusBoundsLayout != null)
            ResponsiveRectStateEditorUtil.WriteState(focusBoundsLayout, value.focusBoundsLayout);
    }

    /// <summary>
    /// Copies every serialized value from one exact slot to another on each
    /// camera layout. Other slots and camera-row references are unchanged.
    /// </summary>
    /// <returns>The number of camera layouts copied.</returns>
    public static int CopySlotAcrossLayouts(
        SerializedProperty layouts,
        int sourceSlotIndex,
        int destinationSlotIndex)
    {
        string[] states = ResponsiveCameraLayout.StatePropertyNames;
        if (layouts == null ||
            !layouts.isArray ||
            sourceSlotIndex < 0 ||
            sourceSlotIndex >= states.Length ||
            destinationSlotIndex < 0 ||
            destinationSlotIndex >= states.Length ||
            sourceSlotIndex == destinationSlotIndex)
            return 0;

        string sourceName = states[sourceSlotIndex];
        string destinationName = states[destinationSlotIndex];
        for (int i = 0; i < layouts.arraySize; i++)
        {
            SerializedProperty layout = layouts.GetArrayElementAtIndex(i);
            ResponsiveCameraState source =
                ReadState(layout.FindPropertyRelative(sourceName));
            WriteState(layout.FindPropertyRelative(destinationName), source);
        }

        return layouts.arraySize;
    }

    /// <summary>
    /// Camera CreateDefault values. Keeps Enabled, viewport width/height, projection,
    /// Orthographic Size, and Field Of View.
    /// </summary>
    public static void ResetSlot(SerializedProperty stateProp)
    {
        ResponsiveCameraState current = ReadState(stateProp);
        ResponsiveCameraState reset = ResponsiveCameraState.CreateDefault(current.enabled);
        float width = current.viewport.width;
        float height = current.viewport.height;
        reset.viewport = width > 0f && height > 0f
            ? new Rect(0f, 0f, width, height)
            : ResponsiveCameraState.FullViewport;
        reset.orthographic = current.orthographic;
        reset.orthographicSize = current.orthographicSize;
        reset.fieldOfView = current.fieldOfView;
        WriteState(stateProp, reset);
    }

    /// <summary>
    /// Clears every camera slot to CreateDefault and re-applies new-row Enabled policy.
    /// Used when the Inspector Target is set to None.
    /// </summary>
    public static void ResetSlots(SerializedProperty layoutProp)
    {
        if (layoutProp == null)
            return;

        string[] states = ResponsiveCameraLayout.StatePropertyNames;
        for (int i = 0; i < states.Length; i++)
        {
            SerializedProperty slot = layoutProp.FindPropertyRelative(states[i]);
            if (slot == null)
                continue;
            WriteState(slot, ResponsiveCameraState.CreateDefault(enabled: false));
        }

        SeedSlots(layoutProp);
    }

    /// <summary>
    /// Unity list <c>+</c> duplicates the previous row. Clear Target, Camera Background,
    /// and slots so the new camera item is empty, with the same Enabled defaults as
    /// Add Component.
    /// </summary>
    public static void ResetNewListRow(SerializedProperty layoutProp)
    {
        if (layoutProp == null)
            return;

        SerializedProperty target = layoutProp.FindPropertyRelative("target");
        if (target != null)
            target.objectReferenceValue = null;

        SerializedProperty background = layoutProp.FindPropertyRelative("background");
        if (background != null)
            background.objectReferenceValue = null;

        SerializedProperty fit = layoutProp.FindPropertyRelative("spriteFitMode");
        if (fit != null)
            fit.intValue = (int)ResponsiveCameraSpriteFitMode.Cover;

        ResetSlots(layoutProp);
    }

    /// <summary>
    /// Seeds layout from the live camera.
    /// First Target assign (or Missing→new): copy into every slot and set Enabled from
    /// Player Allowed Orientations. Retarget: write only the active slot.
    /// </summary>
    public static void WriteCaptureToSlots(
        SerializedProperty layoutProp,
        Camera camera,
        bool applyEnabledDefaults)
    {
        if (layoutProp == null || camera == null)
            return;

        ResponsiveCameraState live = ResponsiveCameraState.From(camera);
        ResponsiveCamera responsiveCamera =
            layoutProp.serializedObject.targetObject as ResponsiveCamera;
        Vector2 uiSize = responsiveCamera != null
            ? responsiveCamera.GetUiLayoutSize()
            : Vector2.zero;

        if (!applyEnabledDefaults)
        {
            ResponsiveDevice.LayoutKey layout =
                ResponsiveUiDetectionDrawer.ResolveInspectorLayout(
                    layoutProp.serializedObject,
                    ResponsiveUiDetectionDrawer.HandlerOf(layoutProp.serializedObject));
            ResponsiveRectStateEditorUtil.ResolveCaptureSlot(
                layoutProp, layout, out string active, out _);

            SerializedProperty activeState = layoutProp.FindPropertyRelative(active);
            if (activeState == null)
                return;

            bool currentlyEnabled =
                activeState.FindPropertyRelative("enabled")?.boolValue ?? false;
            int slotIndex = ResponsiveWidgetLayout.StatePropertyIndex(active);
            WriteCapturedState(
                activeState,
                live,
                currentlyEnabled,
                uiSize,
                ResponsiveWidgetLayout.SlotFormFactor(slotIndex));
            return;
        }

        string[] states = ResponsiveCameraLayout.StatePropertyNames;
        for (int i = 0; i < states.Length; i++)
        {
            SerializedProperty state = layoutProp.FindPropertyRelative(states[i]);
            if (state == null)
                continue;

            bool allowed = ResponsiveUiEditorSettings.SlotAllowed(states[i]);
            bool currentlyEnabled =
                state.FindPropertyRelative("enabled")?.boolValue ?? false;
            bool enabled =
                !ResponsiveWidgetLayout.IsSeedDisabledSlot(i) &&
                (currentlyEnabled || allowed);

            WriteCapturedState(
                state,
                live,
                enabled,
                uiSize,
                ResponsiveWidgetLayout.SlotFormFactor(i));
        }
    }

    /// <summary>
    /// After a camera Target ObjectField edit: first assign / Missing→new seeds every
    /// slot; live retarget writes the active slot only; clear to None resets the row.
    /// Returns the newly assigned Camera when LivePush should run; otherwise null.
    /// Call while <paramref name="layoutProp"/> already holds the new Target value.
    /// </summary>
    public static Camera ApplyInspectorTargetChange(
        SerializedProperty layoutProp,
        Object previousTarget,
        int previousInstanceId)
    {
        if (layoutProp == null)
            return null;

        SerializedProperty targetProp = layoutProp.FindPropertyRelative("target");
        Object next = targetProp != null ? targetProp.objectReferenceValue : null;

        if (next is Camera cam)
        {
            WriteCaptureToSlots(
                layoutProp,
                cam,
                applyEnabledDefaults: ResponsiveUiObjectReference.IsFirstAssign(previousTarget));
            return cam;
        }

        if (ResponsiveUiObjectReference.WasClearedToNone(previousInstanceId, next))
            ResetSlots(layoutProp);

        return null;
    }

    /// <summary>
    /// Writes live camera values into a slot. Preserves Scale From UI Size /
    /// Scale Projection and stores design-space position / projection when those
    /// toggles are on. Auto-detects Orthographic vs Perspective from the live camera.
    /// <paramref name="seedTransform"/> enables Apply Local and projection flags for
    /// first assign / explicit Capture. <paramref name="captureLiveValues"/> writes all
    /// live values while preserving those authored flags, matching widget Scene capture.
    /// <paramref name="preserveFocusPosition"/> and
    /// <paramref name="preserveFocusProjection"/> keep focus-derived values while
    /// still capturing viewport, rotation, and projection mode.
    /// </summary>
    public static void WriteCapturedState(
        SerializedProperty state,
        ResponsiveCameraState live,
        bool enabled,
        Vector2 currentUiSize,
        ResponsiveDevice.FormFactor slotForm,
        bool seedTransform = true,
        bool captureLiveValues = false,
        bool preserveFocusPosition = false,
        bool preserveFocusProjection = false)
    {
        if (state == null)
            return;

        SerializedProperty scaleProp = state.FindPropertyRelative("scaleFromUiSize");
        SerializedProperty projectionScaleProp = state.FindPropertyRelative("scaleProjection");
        SerializedProperty modeProp = state.FindPropertyRelative("uiScaleMode");

        bool scaleFromUi = scaleProp != null && scaleProp.boolValue;
        bool scaleProjection = projectionScaleProp != null && projectionScaleProp.boolValue;
        var mode = modeProp != null
            ? (ResponsiveDesignScaleMode)modeProp.intValue
            : ResponsiveDesignScaleMode.Width;
        Vector3 localPosition = live.localPosition;
        float orthographicSize = live.orthographicSize;
        float fieldOfView = live.fieldOfView;
        UnscaleLiveToDesign(
            slotForm,
            ResponsiveWidgetLayout.SlotOrientation(
                ResponsiveWidgetLayout.StatePropertyIndex(state.name)),
            currentUiSize,
            mode,
            scaleFromUi,
            scaleProjection,
            ref localPosition,
            ref orthographicSize,
            ref fieldOfView);

        SerializedProperty enabledProp = state.FindPropertyRelative("enabled");
        if (enabledProp != null)
            enabledProp.boolValue = enabled;

        SerializedProperty viewport = state.FindPropertyRelative("viewport");
        if (viewport != null)
            viewport.rectValue = live.viewport;

        SerializedProperty applyLocalPosition = state.FindPropertyRelative("applyLocalPosition");
        SerializedProperty applyLocalRotation = state.FindPropertyRelative("applyLocalRotation");
        SerializedProperty localPositionProp = state.FindPropertyRelative("localPosition");
        SerializedProperty localEulerAngles = state.FindPropertyRelative("localEulerAngles");
        if (seedTransform)
        {
            bool applyLocal =
                (applyLocalPosition != null && applyLocalPosition.boolValue) ||
                (applyLocalRotation != null && applyLocalRotation.boolValue);
            if (applyLocal || !SlotHasFocus(state))
            {
                if (applyLocalPosition != null)
                    applyLocalPosition.boolValue = true;
                if (applyLocalRotation != null)
                    applyLocalRotation.boolValue = true;
            }
        }
        if ((seedTransform || captureLiveValues) &&
            !preserveFocusPosition &&
            localPositionProp != null)
            localPositionProp.vector3Value = localPosition;
        if ((seedTransform || captureLiveValues) && localEulerAngles != null)
            localEulerAngles.vector3Value = live.localEulerAngles;

        if (scaleProp != null)
            scaleProp.boolValue = scaleFromUi;
        if (projectionScaleProp != null)
            projectionScaleProp.boolValue = scaleProjection;
        if (modeProp != null)
            modeProp.intValue = (int)mode;

        SerializedProperty orthographic = state.FindPropertyRelative("orthographic");
        if (orthographic != null)
            orthographic.boolValue = live.orthographic;

        SerializedProperty applyFieldOfView = state.FindPropertyRelative("applyFieldOfView");
        SerializedProperty fieldOfViewProp = state.FindPropertyRelative("fieldOfView");
        SerializedProperty applyOrthographicSize = state.FindPropertyRelative("applyOrthographicSize");
        SerializedProperty orthographicSizeProp = state.FindPropertyRelative("orthographicSize");
        if (seedTransform)
        {
            if (applyFieldOfView != null)
                applyFieldOfView.boolValue = live.applyFieldOfView;
            if (applyOrthographicSize != null)
                applyOrthographicSize.boolValue = live.applyOrthographicSize;
        }

        bool writeFov = !preserveFocusProjection &&
            (seedTransform || captureLiveValues ||
             (applyFieldOfView != null && applyFieldOfView.boolValue));
        bool writeOrtho = !preserveFocusProjection &&
            (seedTransform || captureLiveValues ||
             (applyOrthographicSize != null && applyOrthographicSize.boolValue));
        if (!seedTransform && !captureLiveValues && SlotHasFocus(state))
        {
            writeFov = false;
            writeOrtho = false;
        }
        if (writeFov && fieldOfViewProp != null)
            fieldOfViewProp.floatValue = fieldOfView;
        if (writeOrtho && orthographicSizeProp != null)
            orthographicSizeProp.floatValue = orthographicSize;
    }

    /// <summary>
    /// Inverse of the slot's UI-size scaling on Apply: converts live Local Position
    /// (<paramref name="scalePosition"/>, Scale From UI Size) and Orthographic Size /
    /// Field Of View (<paramref name="scaleProjection"/>) to the design-space values the
    /// slot stores. Design UI Size is the slot form's Design Canvas oriented to the slot.
    /// </summary>
    public static void UnscaleLiveToDesign(
        ResponsiveDevice.FormFactor slotForm,
        ResponsiveDevice.Orientation slotOrientation,
        Vector2 currentUiSize,
        ResponsiveDesignScaleMode mode,
        bool scalePosition,
        bool scaleProjection,
        ref Vector3 localPosition,
        ref float orthographicSize,
        ref float fieldOfView)
    {
        if (!scalePosition && !scaleProjection)
            return;

        Vector2 design = ResponsiveUI.OrientSizeForSlot(
            ResponsiveRectState.GetDefaultDesignCanvasSize(slotForm), slotForm, slotOrientation);
        Vector2 liveUi = ResponsiveDesignScale.ResolveIdentityLiveSize(
            currentUiSize, slotForm, slotOrientation, design);
        if (scalePosition)
        {
            Vector2 ratio = ResponsiveRectState.ComputeCanvasRatio(liveUi, design, mode);
            localPosition = ResponsiveCameraState.UnscaleDesignPosition(localPosition, ratio);
        }
        if (scaleProjection)
        {
            orthographicSize = ResponsiveCameraState.UnscaleOrthographicSize(
                orthographicSize, liveUi, design, mode);
            fieldOfView = ResponsiveCameraState.UnscaleFieldOfView(
                fieldOfView, liveUi, design, mode);
        }
    }

    #endregion

    #region Slot enabled

    /// <summary>
    /// When Enabled turns on, copy <see cref="ResponsiveRectStateEditorUtil.FindFallbackSource"/>.
    /// Design UI Size stays locked to this slot's Responsive UI form default.
    /// </summary>
    public static bool CopyFallback(SerializedProperty stateProp)
    {
        SerializedProperty source = ResponsiveRectStateEditorUtil.FindFallbackSource(stateProp);
        if (source == null)
            return false;

        bool enabled = stateProp.FindPropertyRelative("enabled")?.boolValue ?? false;
        ResponsiveCameraState copied = ReadState(source);
        copied.enabled = enabled;
        WriteState(stateProp, copied);
        return true;
    }

    /// <summary>
    /// Enables Player-allowed orientations except Flip Cover Landscape,
    /// which remains opt-in, and copies each enabled slot's fallback source into it.
    /// </summary>
    public static void SeedSlots(SerializedProperty layoutProp)
    {
        if (layoutProp == null)
            return;

        string[] states = ResponsiveCameraLayout.StatePropertyNames;
        for (int i = 0; i < states.Length; i++)
        {
            SerializedProperty state = layoutProp.FindPropertyRelative(states[i]);
            if (state == null)
                continue;

            SerializedProperty enabled = state.FindPropertyRelative("enabled");
            if (enabled == null)
                continue;

            bool enable =
                ResponsiveUiEditorSettings.SlotAllowed(states[i]) &&
                !ResponsiveWidgetLayout.IsSeedDisabledSlot(i);
            enabled.boolValue = enable;

            if (enable)
                CopyFallback(state);

            SerializedProperty viewport = state.FindPropertyRelative("viewport");
            if (viewport != null &&
                (viewport.rectValue.width <= 0f || viewport.rectValue.height <= 0f))
                viewport.rectValue = ResponsiveCameraState.FullViewport;
        }
    }

    #endregion

    #region Helpers

    /// <summary>
    /// True when this slot frames a world object (Focus owns live position).
    /// </summary>
    public static bool SlotHasFocus(SerializedProperty stateProp)
    {
        if (stateProp?.FindPropertyRelative("focusTarget")?.objectReferenceValue == null)
            return false;
        SerializedProperty screen = stateProp.FindPropertyRelative("focusScreen");
        return screen != null &&
               (ResponsiveCameraFocusScreen)screen.intValue !=
               ResponsiveCameraFocusScreen.None;
    }

    /// <summary>True when this slot's Fit/Cover focus owns live projection.</summary>
    public static bool SlotFocusOwnsProjection(SerializedProperty stateProp) =>
        SlotHasFocus(stateProp) &&
        (ResponsiveCameraFocusFit)stateProp.FindPropertyRelative("focusFit").intValue !=
        ResponsiveCameraFocusFit.Center;

    /// <summary>
    /// Inspector preview of one slot's Focus: applies its Bounds Layout to the Focus Bounds
    /// HUD, then frames the Focus Target like Apply.
    /// </summary>
    public static void ApplyFocus(
        SerializedProperty stateProp,
        SerializedProperty layoutProp,
        Camera camera)
    {
        if (stateProp == null || camera == null)
            return;

        ApplyFocusBoundsLayout(stateProp);

        if (stateProp.FindPropertyRelative("focusTarget")?.objectReferenceValue is not Transform target)
            return;
        SerializedProperty screenProp = stateProp.FindPropertyRelative("focusScreen");
        if (screenProp == null)
            return;

        var screen = (ResponsiveCameraFocusScreen)screenProp.intValue;
        var fit = (ResponsiveCameraFocusFit)
            (stateProp.FindPropertyRelative("focusFit")?.intValue ?? 0);
        float padding = stateProp.FindPropertyRelative("focusPadding")?.floatValue ?? 1f;
        RectTransform bounds = ReadFocusBounds(stateProp);

        ResponsiveCameraFocus.Apply(
            camera,
            target,
            screen,
            fit,
            padding,
            ReadBackground(layoutProp),
            bounds);
    }

    static void ApplyFocusBoundsLayout(SerializedProperty stateProp)
    {
        RectTransform bounds = ReadFocusBounds(stateProp);
        ResponsiveRectState boundsLayout = ReadFocusBoundsLayout(stateProp);
        if (bounds == null || !boundsLayout.enabled)
            return;

        int slotIndex = ResponsiveWidgetLayout.StatePropertyIndex(stateProp.name);
        var owner = stateProp.serializedObject.targetObject as ResponsiveCamera;
        ResponsiveUI hub = owner != null ? owner.Hub : null;
        Undo.RecordObject(bounds, "Responsive Camera Preview");
        if (hub != null)
        {
            hub.ApplyWidget(
                bounds,
                boundsLayout,
                ResponsiveWidgetLayout.SlotFormFactor(slotIndex),
                ResponsiveWidgetLayout.SlotOrientation(slotIndex));
            return;
        }

        Vector2 uiSize = owner != null
            ? owner.GetUiLayoutSize()
            : ResponsiveDevice.GetWatchScreenSize();
        boundsLayout.Apply(
            bounds,
            uiSize,
            ResponsiveWidgetLayout.SlotFormFactor(slotIndex),
            ResponsiveWidgetLayout.SlotOrientation(slotIndex));
    }

    /// <summary>
    /// Captures this slot's live Focus Bounds HUD into its Bounds Layout (design space,
    /// same as widget capture) and marks the layout Enabled. Clearing Focus Bounds
    /// resets the layout to a disabled default. Returns true when anything was written.
    /// </summary>
    public static bool CaptureFocusBoundsLayoutInto(
        SerializedProperty state,
        bool skipIfMatches = true)
    {
        SerializedProperty layoutProp = state.FindPropertyRelative("focusBoundsLayout");
        if (layoutProp == null)
            return false;

        var bounds = state.FindPropertyRelative("focusBounds")?.objectReferenceValue as RectTransform;
        if (bounds == null)
        {
            ResponsiveRectStateEditorUtil.WriteState(
                layoutProp, ResponsiveRectState.CreateDefault(enabled: false));
            return true;
        }

        int slotIndex = ResponsiveWidgetLayout.StatePropertyIndex(state.name);
        var owner = state.serializedObject.targetObject as ResponsiveCamera;
        ResponsiveUI hub = owner != null ? owner.Hub : null;
        Vector2 uiSize = owner != null ? owner.GetUiLayoutSize() : Vector2.zero;
        if (!ResponsiveRectStateEditorUtil.WriteCapturedLive(
                layoutProp, bounds, hub, slotIndex, uiSize, skipIfMatches))
            return false;

        SerializedProperty enabled = layoutProp.FindPropertyRelative(
            ResponsiveRectStateEditorUtil.EnabledProp);
        if (enabled != null)
            enabled.boolValue = true;
        return true;
    }

    static RectTransform ReadFocusBounds(SerializedProperty stateProp)
    {
        SerializedProperty bounds = stateProp.FindPropertyRelative("focusBounds");
        return bounds != null ? bounds.objectReferenceValue as RectTransform : null;
    }

    static ResponsiveRectState ReadFocusBoundsLayout(SerializedProperty stateProp)
    {
        SerializedProperty layout = stateProp.FindPropertyRelative("focusBoundsLayout");
        return layout != null
            ? ResponsiveRectStateEditorUtil.ReadState(layout)
            : ResponsiveRectState.CreateDefault(enabled: false);
    }

    static SpriteRenderer ReadBackground(SerializedProperty layoutProp) =>
        layoutProp.FindPropertyRelative("background")?.objectReferenceValue as SpriteRenderer;

    public static bool MatchesLive(
        SerializedProperty stateProp,
        Camera camera,
        Vector2 uiSize,
        ResponsiveDevice.FormFactor slotForm) =>
        MatchesLive(
            stateProp,
            camera,
            uiSize,
            slotForm,
            ignoreFocusPosition: false,
            ignoreFocusProjection: false);

    public static bool MatchesLive(
        SerializedProperty stateProp,
        Camera camera,
        Vector2 uiSize,
        ResponsiveDevice.FormFactor slotForm,
        bool ignoreFraming) =>
        MatchesLive(
            stateProp,
            camera,
            uiSize,
            slotForm,
            ignoreFocusPosition: ignoreFraming,
            ignoreFocusProjection: ignoreFraming);

    public static bool MatchesLive(
        SerializedProperty stateProp,
        Camera camera,
        Vector2 uiSize,
        ResponsiveDevice.FormFactor slotForm,
        bool ignoreFocusPosition,
        bool ignoreFocusProjection)
    {
        if (stateProp == null || camera == null)
            return false;

        ResponsiveCameraState state = ReadState(stateProp);
        int slotIndex = ResponsiveWidgetLayout.StatePropertyIndex(stateProp.name);
        ResponsiveDevice.Orientation slotOrientation =
            ResponsiveWidgetLayout.SlotOrientation(slotIndex);
        Transform t = camera.transform;

        Vector2 liveUi = state.ResolveLiveUiSize(uiSize, slotForm, slotOrientation);

        if (!ignoreFocusPosition)
        {
            Vector3 expectedPos = state.scaleFromUiSize
                ? ResponsiveCameraState.ScaleDesignPosition(
                    state.localPosition,
                    ResponsiveRectState.ComputeCanvasRatio(
                        liveUi,
                        state.GetResolvedDesignUiSize(slotForm, slotOrientation),
                        state.uiScaleMode))
                : state.localPosition;
            if (!Approx(expectedPos, t.localPosition))
                return false;
        }
        if (!ApproxEuler(state.localEulerAngles, t.localEulerAngles))
            return false;

        if (camera.orthographic != state.orthographic)
            return false;

        if (!ignoreFocusProjection)
        {
            Vector2 designUi = state.scaleProjection
                ? state.GetResolvedDesignUiSize(slotForm, slotOrientation)
                : Vector2.zero;

            if (state.orthographic)
            {
                float expectedOrtho = state.orthographicSize;
                if (state.scaleProjection)
                {
                    expectedOrtho = ResponsiveCameraState.ScaleOrthographicSize(
                        expectedOrtho, liveUi, designUi, state.uiScaleMode);
                }
                if (Mathf.Abs(expectedOrtho - camera.orthographicSize) > ProjectionEpsilon)
                    return false;
            }
            else
            {
                float expectedFov = state.fieldOfView;
                if (state.scaleProjection)
                {
                    expectedFov = ResponsiveCameraState.ScaleFieldOfView(
                        expectedFov, liveUi, designUi, state.uiScaleMode);
                }
                if (Mathf.Abs(expectedFov - camera.fieldOfView) > AngleEpsilon)
                    return false;
            }

        }

        Rect expected = ResponsiveCameraState.ClampViewport(state.viewport);
        Rect live = camera.rect;
        return Mathf.Abs(expected.x - live.x) <= ProjectionEpsilon &&
               Mathf.Abs(expected.y - live.y) <= ProjectionEpsilon &&
               Mathf.Abs(expected.width - live.width) <= ProjectionEpsilon &&
               Mathf.Abs(expected.height - live.height) <= ProjectionEpsilon;
    }

    public static void RecordPreviewTargets(ResponsiveCamera owner)
    {
        ResponsiveCameraLayout[] layouts = owner != null ? owner.Cameras : null;
        if (layouts == null || layouts.Length == 0)
            return;

        for (int i = 0; i < layouts.Length; i++)
        {
            Camera camera = layouts[i].target;
            if (camera != null)
            {
                Undo.RecordObject(camera.transform, "Responsive Camera Preview");
                Undo.RecordObject(camera, "Responsive Camera Preview");
            }

            SpriteRenderer background = layouts[i].background;
            if (background != null)
            {
                Undo.RecordObject(background.transform, "Responsive Camera Preview");
                Undo.RecordObject(background, "Responsive Camera Preview");
            }

            for (int slot = 0; slot < ResponsiveCameraLayout.StatePropertyNames.Length; slot++)
                RecordFocusBounds(layouts[i].GetSlot(slot));
        }
    }

    static void RecordFocusBounds(in ResponsiveCameraState state)
    {
        if (state.focusBounds != null)
            Undo.RecordObject(state.focusBounds, "Responsive Camera Preview");
    }

    public static void FitBackground(
        SerializedProperty layoutProp,
        Camera camera,
        bool recordUndo = true)
    {
        if (layoutProp == null || camera == null)
            return;

        SerializedProperty fitProp = layoutProp.FindPropertyRelative("spriteFitMode");
        SerializedProperty backgroundProp = layoutProp.FindPropertyRelative("background");
        if (fitProp == null ||
            backgroundProp == null ||
            backgroundProp.objectReferenceValue is not SpriteRenderer background)
            return;

        var mode = (ResponsiveCameraSpriteFitMode)fitProp.intValue;
        if (recordUndo)
        {
            Undo.RecordObject(background.transform, "Responsive Camera Background Fit");
            Undo.RecordObject(background, "Responsive Camera Background Fit");
        }

        ResponsiveCameraSpriteFit.ApplyBackground(background, camera, mode);
        EditorUtility.SetDirty(background);
        EditorUtility.SetDirty(background.transform);
    }

    public static ResponsiveCamera[] FindLoaded() =>
        Object.FindObjectsByType<ResponsiveCamera>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

    public static bool GetCamera(Object obj, out Camera camera)
    {
        switch (obj)
        {
            case Camera cam:
                camera = cam;
                return true;
            case Transform t:
                camera = t.GetComponent<Camera>();
                return camera != null;
            case GameObject go:
                camera = go.GetComponent<Camera>();
                return camera != null;
            case Component c:
                camera = c as Camera ?? c.GetComponent<Camera>();
                return camera != null;
            default:
                camera = null;
                return false;
        }
    }

    public static SerializedProperty FindLayoutProperty(SerializedProperty stateProp)
    {
        if (stateProp == null)
            return null;

        string path = stateProp.propertyPath;
        int lastDot = path.LastIndexOf('.');
        if (lastDot < 0)
            return null;

        return stateProp.serializedObject.FindProperty(path.Substring(0, lastDot));
    }

    static bool Approx(Vector3 a, Vector3 b) =>
        Mathf.Abs(a.x - b.x) <= PositionEpsilon &&
        Mathf.Abs(a.y - b.y) <= PositionEpsilon &&
        Mathf.Abs(a.z - b.z) <= PositionEpsilon;

    static bool ApproxEuler(Vector3 a, Vector3 b) =>
        Mathf.Abs(Mathf.DeltaAngle(a.x, b.x)) <= AngleEpsilon &&
        Mathf.Abs(Mathf.DeltaAngle(a.y, b.y)) <= AngleEpsilon &&
        Mathf.Abs(Mathf.DeltaAngle(a.z, b.z)) <= AngleEpsilon;

    #endregion
}
}

#endif
