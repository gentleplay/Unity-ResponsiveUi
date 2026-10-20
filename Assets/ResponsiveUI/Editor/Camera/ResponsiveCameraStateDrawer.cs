#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

using FDPlus.ResponsiveUI;

namespace FDPlus.ResponsiveUI.Editor
{
/// <summary>
/// Draws <see cref="ResponsiveCameraState"/>: per-slot viewport, local position / rotation,
/// UI-size scale, and Orthographic / Perspective projection. Apply Local decides whether
/// this slot writes its stored Local Position / Local Euler Angles; those fields stay
/// visible so each device profile keeps its own pose like a widget slot.
/// </summary>
[CustomPropertyDrawer(typeof(ResponsiveCameraState))]
public class ResponsiveCameraStateDrawer : PropertyDrawer
{
    static readonly string[] ProjectionLabels = { "Perspective", "Orthographic" };

    static readonly GUIContent LocalPositionLabel = new GUIContent(
        "Local Position",
        "Stored on this device slot (like a widget Pos). Written to the Target when Apply Local is on.");
    static readonly GUIContent LocalEulerLabel = new GUIContent(
        "Local Euler Angles",
        "Stored on this device slot. Written to the Target when Apply Local is on.");
    static readonly GUIContent DesignUiSizeLabel = new GUIContent(
        "Design UI Size",
        "Locked to Project Settings → Responsive UI Design Canvas for this form factor.");
    static readonly GUIContent ScaleFromUiLabel = new GUIContent(
        "Scale From UI Size",
        "Scales authored Local Position by live display size / Design UI Size " +
        "(same canvas ratio as widget Design Scale). Turns on Apply Local so there is a pose to scale.");
    static readonly GUIContent FocusBoundsLabel = new GUIContent(
        "Focus Bounds",
        "HUD rect to frame for Auto / Bounds. Bounds Layout stores this slot's " +
        "widget anchors / Pos / Size. Does not write Camera.rect.");
    static readonly GUIContent ScaleProjectionLabel = new GUIContent(
        "Scale Projection",
        "Orthographic Size and Field Of View use Scale Mode aspect framing " +
        "(Width keeps horizontal world span; Height keeps vertical span; " +
        "Auto fits like UniformMin: wider → Height, taller → Width). " +
        "Not pixel canvas ratio.");

    const string SectionView = "View";
    const string SectionFocus = "Focus";
    const string SectionBoundsLayout = "Bounds Layout";
    const string SectionTransform = "Transform";
    const string SectionScale = "Scale";
    const string SlotKind = "CamSlot";

    static readonly Color ViewTint = ResponsiveUiInspectorFoldouts.TintBlue;
    static readonly Color FocusTint = ResponsiveUiInspectorFoldouts.TintTeal;
    static readonly Color LayoutTint = ResponsiveUiInspectorFoldouts.TintGreen;
    static readonly Color TransformTint = ResponsiveUiInspectorFoldouts.TintAmber;
    static readonly Color ScaleTint = ResponsiveUiInspectorFoldouts.TintPurple;

    #region Inspector

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float line = EditorGUIUtility.singleLineHeight;
        float s = EditorGUIUtility.standardVerticalSpacing;

        property.isExpanded = ResponsiveUiDetectionDrawer.ShouldExpandState(property);
        if (!property.isExpanded)
            return line;

        float h = line + s;
        SerializedProperty enabled = property.FindPropertyRelative("enabled");
        h += line + s;
        if (enabled == null || !enabled.boolValue)
            return h;

        bool wide = EditorGUIUtility.wideMode;
        EditorGUIUtility.wideMode = true;
        h += SlotHeight(property, "View", AtZeroIndent(() => ViewBodyHeight(property)));
        h += SlotHeight(property, "Focus", AtZeroIndent(() => FocusBodyHeight(property)));
        h += SlotHeight(property, "BoundsLayout", AtZeroIndent(() => BoundsLayoutBodyHeight(property)));
        h += SlotHeight(property, "Transform", TransformFieldsHeight());
        h += SlotHeight(property, "Scale", AtZeroIndent(() => ScaleBodyHeight(property)));
        EditorGUIUtility.wideMode = wide;
        h += line + s; // Capture From Camera
        return h;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        float line = EditorGUIUtility.singleLineHeight;
        float s = EditorGUIUtility.standardVerticalSpacing;

        Rect row = new Rect(position.x, position.y, position.width, line);
        bool expanded = ResponsiveUiSlotClipboard.DrawFoldoutRow(
            row, property, label, ResponsiveUiSlotClipboard.Kind.Camera);
        if (!expanded)
        {
            EditorGUI.EndProperty();
            return;
        }

        ResponsiveUiInspectorFoldouts.DrawNestedContent(position, content =>
        {
            float y = position.y + line + s;

            SerializedProperty enabled = property.FindPropertyRelative("enabled");
            if (enabled != null)
            {
                using (new EditorGUI.DisabledScope(ResponsiveUiEditorSettings.LockEnabledToggles))
                {
                    bool wasEnabled = enabled.boolValue;
                    EditorGUI.PropertyField(new Rect(content.x, y, content.width, line), enabled);
                    if (!wasEnabled && enabled.boolValue)
                        ResponsiveCameraEditorUtil.CopyFallback(property);
                }
                y += line + s;
            }

            if (enabled == null || !enabled.boolValue)
                return;

            bool wide = EditorGUIUtility.wideMode;
            EditorGUIUtility.wideMode = true;
            y = DrawSlot(
                content, y, property, "View", SectionView, ViewSummary(property),
                ViewTint, AtZeroIndent(() => ViewBodyHeight(property)),
                body => DrawViewBody(body, property));
            y = DrawSlot(
                content, y, property, "Focus", SectionFocus, FocusSummary(property),
                FocusTint, AtZeroIndent(() => FocusBodyHeight(property)),
                body => DrawFocusFields(body, body.y, line, s, property));
            y = DrawSlot(
                content, y, property, "BoundsLayout", SectionBoundsLayout,
                BoundsLayoutSummary(property), LayoutTint,
                AtZeroIndent(() => BoundsLayoutBodyHeight(property)),
                body => DrawFocusBoundsLayout(body, body.y, line, s, property));
            y = DrawSlot(
                content, y, property, "Transform", SectionTransform,
                TransformSummary(property), TransformTint,
                TransformFieldsHeight(),
                body => DrawLocalTransformFields(body, body.y, line, s, property));
            y = DrawSlot(
                content, y, property, "Scale", SectionScale, ScaleSummary(property),
                ScaleTint, AtZeroIndent(() => ScaleBodyHeight(property)),
                body => DrawScaleFields(body, body.y, line, s, property));
            EditorGUIUtility.wideMode = wide;

            if (GUI.Button(
                    new Rect(content.x, y, content.width, line),
                    new GUIContent(
                        "Capture From Camera",
                        "Copy the Target camera’s live viewport, Projection, and projection values into this slot. " +
                        "Copies the live transform and projection values so the slot and Camera stay synchronized.")))
                CaptureFromTargetCamera(property);
        });
        EditorGUI.EndProperty();
    }

    #endregion

    #region Helpers

    static float AtZeroIndent(Func<float> measure) =>
        ResponsiveUiInspectorFoldouts.AtZeroIndent(measure);

    static float SlotHeight(
        SerializedProperty property,
        string section,
        float bodyHeight) =>
        ResponsiveUiInspectorFoldouts.SlotSectionHeight(
            property, SlotKind, section, true, bodyHeight);

    static float DrawSlot(
        Rect position,
        float y,
        SerializedProperty property,
        string section,
        string title,
        string summary,
        Color tint,
        float bodyHeight,
        Action<Rect> drawBody) =>
        ResponsiveUiInspectorFoldouts.DrawSlotSection(
            position,
            y,
            bodyHeight,
            property,
            SlotKind,
            section,
            title,
            summary,
            tint,
            true,
            drawBody);

    static float ViewBodyHeight(SerializedProperty property)
    {
        float s = EditorGUIUtility.standardVerticalSpacing;
        float h = 0f;
        SerializedProperty viewport = property.FindPropertyRelative("viewport");
        if (viewport != null)
            h += EditorGUI.GetPropertyHeight(viewport, true) + s;
        return h + ProjectionHeight(property);
    }

    static void DrawViewBody(Rect position, SerializedProperty property)
    {
        float line = EditorGUIUtility.singleLineHeight;
        float s = EditorGUIUtility.standardVerticalSpacing;
        float y = position.y;
        SerializedProperty viewport = property.FindPropertyRelative("viewport");
        if (viewport != null)
        {
            float vh = EditorGUI.GetPropertyHeight(viewport, true);
            EditorGUI.PropertyField(new Rect(position.x, y, position.width, vh), viewport, true);
            y += vh + s;
        }

        DrawProjection(position, y, line, s, property);
    }

    static float FocusBodyHeight(SerializedProperty property)
    {
        float line = EditorGUIUtility.singleLineHeight;
        float s = EditorGUIUtility.standardVerticalSpacing;
        float h = (line + s) * 2f; // target + screen
        if (property.FindPropertyRelative("focusBounds") != null)
            h += line + s;
        h += line + s; // fit
        h += line + s; // padding
        if (GetFocusMeasureHint(property, out _))
            h += line * 2f + s;
        return h;
    }

    static float BoundsLayoutBodyHeight(SerializedProperty property)
    {
        SerializedProperty layout = property.FindPropertyRelative("focusBoundsLayout");
        if (layout == null)
            return 0f;

        float line = EditorGUIUtility.singleLineHeight;
        float s = EditorGUIUtility.standardVerticalSpacing;
        return line + s + ResponsiveRectStateDrawer.LayoutFieldsHeight(layout);
    }

    static float ProjectionHeight(SerializedProperty property)
    {
        float line = EditorGUIUtility.singleLineHeight;
        float s = EditorGUIUtility.standardVerticalSpacing;
        float h = line + s;
        bool orthographic = property.FindPropertyRelative("orthographic")?.boolValue != false;
        if (orthographic)
        {
            h += line + s;
            if (property.FindPropertyRelative("applyOrthographicSize")?.boolValue == true)
            {
                h += line + s;
                if (GetLiveProjectionHint(property, out _))
                    h += line + s;
            }
        }
        else
        {
            h += line + s;
            if (property.FindPropertyRelative("applyFieldOfView")?.boolValue == true)
            {
                h += line + s;
                if (GetLiveProjectionHint(property, out _))
                    h += line + s;
            }
        }

        return h;
    }

    static float ScaleBodyHeight(SerializedProperty property)
    {
        float line = EditorGUIUtility.singleLineHeight;
        float s = EditorGUIUtility.standardVerticalSpacing;
        float h = line + s; // scaleFromUiSize
        h += line + s; // scaleProjection
        bool showScaleMode =
            property.FindPropertyRelative("scaleFromUiSize")?.boolValue == true ||
            FindScaleProjection(property)?.boolValue == true;
        if (!showScaleMode)
            return h;

        h += line + s; // uiScaleMode
        var mode = (ResponsiveDesignScaleMode)
            (property.FindPropertyRelative("uiScaleMode")?.intValue ?? 0);
        if (mode == ResponsiveDesignScaleMode.Auto)
            h += line + s;
        h += Vector2RowHeight(DesignUiSizeLabel) + s;
        h += line + s;
        return h;
    }

    static float DrawScaleFields(
        Rect position,
        float y,
        float line,
        float s,
        SerializedProperty property)
    {
        SerializedProperty scaleFromUi = property.FindPropertyRelative("scaleFromUiSize");
        if (scaleFromUi != null)
        {
            EditorGUI.BeginChangeCheck();
            EditorGUI.PropertyField(
                new Rect(position.x, y, position.width, line),
                scaleFromUi,
                ScaleFromUiLabel);
            if (EditorGUI.EndChangeCheck() && scaleFromUi.boolValue)
                EnableApplyLocalForScale(property);
            y += line + s;
        }

        SerializedProperty scaleProjection = FindScaleProjection(property);
        if (scaleProjection != null)
        {
            EditorGUI.BeginChangeCheck();
            EditorGUI.PropertyField(
                new Rect(position.x, y, position.width, line),
                scaleProjection,
                ScaleProjectionLabel);
            if (EditorGUI.EndChangeCheck() && scaleProjection.boolValue)
            {
                bool orthographic =
                    property.FindPropertyRelative("orthographic")?.boolValue != false;
                if (orthographic)
                {
                    SerializedProperty applyOrtho =
                        property.FindPropertyRelative("applyOrthographicSize");
                    if (applyOrtho != null)
                        applyOrtho.boolValue = true;
                }
                else
                {
                    SerializedProperty applyFov =
                        property.FindPropertyRelative("applyFieldOfView");
                    if (applyFov != null)
                        applyFov.boolValue = true;
                }
            }
            y += line + s;
        }

        bool showScaleMode =
            scaleFromUi != null && scaleFromUi.boolValue ||
            scaleProjection != null && scaleProjection.boolValue;
        if (!showScaleMode)
            return y;

        SerializedProperty uiScaleMode = property.FindPropertyRelative("uiScaleMode");
        if (uiScaleMode != null)
        {
            EditorGUI.PropertyField(
                new Rect(position.x, y, position.width, line),
                uiScaleMode,
                new GUIContent(
                    "Scale Mode",
                    "Width keeps horizontal world span. Height keeps vertical span. " +
                    "Auto fits like UniformMin: wider than Design Canvas → Height, taller → Width."));
            y += line + s;
            if ((ResponsiveDesignScaleMode)uiScaleMode.intValue ==
                ResponsiveDesignScaleMode.Auto)
            {
                EditorGUI.LabelField(
                    new Rect(position.x, y, position.width, line),
                    " ",
                    ResponsiveRectStateEditorUtil.FormatAutoScaleHint(
                        property, GetSlotUiSize(property)));
                y += line + s;
            }
        }

        int slotIndex = ResponsiveWidgetLayout.StatePropertyIndex(property.name);
        ResponsiveDevice.FormFactor form = ResponsiveWidgetLayout.SlotFormFactor(slotIndex);
        Vector2 resolved = ResponsiveRectState.GetDefaultDesignCanvasSize(form);
        resolved = ResponsiveUI.OrientSizeForSlot(
            resolved, form, ResponsiveWidgetLayout.SlotOrientation(slotIndex));
        float designH = Vector2RowHeight(DesignUiSizeLabel);
        using (new EditorGUI.DisabledScope(true))
        {
            var values = new[] { resolved.x, resolved.y };
            EditorGUI.MultiFloatField(
                new Rect(position.x, y, position.width, designH),
                DesignUiSizeLabel,
                XyLabels,
                values);
        }
        y += designH + s;
        EditorGUI.LabelField(
            new Rect(position.x, y, position.width, line),
            " ",
            $"Locked to Responsive UI settings: {resolved.x:0}×{resolved.y:0}");
        return y + line + s;
    }

    static string ViewSummary(SerializedProperty property)
    {
        Rect viewport = property.FindPropertyRelative("viewport")?.rectValue ?? new Rect(0, 0, 1, 1);
        bool orthographic = property.FindPropertyRelative("orthographic")?.boolValue != false;
        if (orthographic)
        {
            float size = property.FindPropertyRelative("orthographicSize")?.floatValue ?? 0f;
            return $"{viewport.width:0.##}×{viewport.height:0.##}  Ortho {size:0.##}";
        }

        float fov = property.FindPropertyRelative("fieldOfView")?.floatValue ?? 0f;
        return $"{viewport.width:0.##}×{viewport.height:0.##}  {fov:0.#}°";
    }

    static string FocusSummary(SerializedProperty property)
    {
        var screen = (ResponsiveCameraFocusScreen)
            (property.FindPropertyRelative("focusScreen")?.intValue ?? 0);
        var target = property.FindPropertyRelative("focusTarget")?.objectReferenceValue as Transform;
        string targetName = target != null ? target.name : "No target";
        return screen == ResponsiveCameraFocusScreen.None
            ? "Off"
            : $"{screen}  {targetName}";
    }

    static string BoundsLayoutSummary(SerializedProperty property)
    {
        var bounds = property.FindPropertyRelative("focusBounds")?.objectReferenceValue as RectTransform;
        SerializedProperty layout = property.FindPropertyRelative("focusBoundsLayout");
        if (layout == null)
            return bounds != null ? bounds.name : "None";

        Vector2 min = layout.FindPropertyRelative(
            ResponsiveRectStateEditorUtil.AnchorMinProp)?.vector2Value ?? Vector2.zero;
        Vector2 max = layout.FindPropertyRelative(
            ResponsiveRectStateEditorUtil.AnchorMaxProp)?.vector2Value ?? Vector2.one;
        Vector2 pos = layout.FindPropertyRelative(
            ResponsiveRectStateEditorUtil.AnchoredPositionProp)?.vector2Value ?? Vector2.zero;
        Vector2 size = layout.FindPropertyRelative(
            ResponsiveRectStateEditorUtil.SizeDeltaProp)?.vector2Value ?? Vector2.zero;
        Vector2 pivot = layout.FindPropertyRelative(
            ResponsiveRectStateEditorUtil.PivotProp)?.vector2Value ?? new Vector2(0.5f, 0.5f);
        bool stretchX = ResponsiveRectState.IsAxisStretched(min.x, max.x);
        bool stretchY = ResponsiveRectState.IsAxisStretched(min.y, max.y);
        if (stretchX && stretchY)
        {
            float left = pos.x - size.x * pivot.x;
            float right = -(pos.x + size.x * (1f - pivot.x));
            float top = -(pos.y + size.y * (1f - pivot.y));
            float bottom = pos.y - size.y * pivot.y;
            return $"L{left:0}  R{right:0}  T{top:0}  B{bottom:0}";
        }

        return $"{pos.x:0}×{pos.y:0}  {size.x:0}×{size.y:0}";
    }

    static string TransformSummary(SerializedProperty property)
    {
        bool apply =
            property.FindPropertyRelative("applyLocalPosition")?.boolValue == true ||
            property.FindPropertyRelative("applyLocalRotation")?.boolValue == true;
        Vector3 pos = property.FindPropertyRelative("localPosition")?.vector3Value ?? Vector3.zero;
        return apply
            ? $"On  {pos.x:0.##}, {pos.y:0.##}, {pos.z:0.##}"
            : "Off";
    }

    static string ScaleSummary(SerializedProperty property)
    {
        bool ui = property.FindPropertyRelative("scaleFromUiSize")?.boolValue == true;
        bool projection = FindScaleProjection(property)?.boolValue == true;
        if (!ui && !projection)
            return "Off";

        var mode = (ResponsiveDesignScaleMode)
            (property.FindPropertyRelative("uiScaleMode")?.intValue ?? 0);
        string which = ui && projection ? "UI + Projection" : ui ? "UI Size" : "Projection";
        return $"{which}  {mode}";
    }

    static float DrawFocusBoundsObject(
        Rect position,
        float y,
        float line,
        float s,
        SerializedProperty property)
    {
        SerializedProperty boundsProp = property.FindPropertyRelative("focusBounds");
        SerializedProperty layoutProp = property.FindPropertyRelative("focusBoundsLayout");
        if (boundsProp == null)
            return y;

        EditorGUI.BeginChangeCheck();
        EditorGUI.PropertyField(
            new Rect(position.x, y, position.width, line),
            boundsProp,
            FocusBoundsLabel,
            false);
        bool boundsChanged = EditorGUI.EndChangeCheck();
        y += line + s;
        if (boundsChanged)
            ResponsiveCameraEditorUtil.CaptureFocusBoundsLayoutInto(property);
        else if (
            boundsProp.objectReferenceValue != null &&
            layoutProp?.FindPropertyRelative(
                ResponsiveRectStateEditorUtil.EnabledProp)?.boolValue == false)
            ResponsiveCameraEditorUtil.CaptureFocusBoundsLayoutInto(property);
        return y;
    }

    static float DrawFocusBoundsLayout(
        Rect position,
        float y,
        float line,
        float s,
        SerializedProperty property)
    {
        SerializedProperty layoutProp = property.FindPropertyRelative("focusBoundsLayout");
        if (layoutProp == null)
            return y;

        EditorGUI.LabelField(
            new Rect(position.x, y, position.width, line),
            "Reference Model",
            ResponsiveUiEditorSettings.GetModelName(
                ResponsiveWidgetLayout.SlotFormFactor(property.name)));
        y += line + s;

        EditorGUI.BeginChangeCheck();
        y = ResponsiveRectStateDrawer.DrawLayoutFields(position, y, layoutProp);
        if (EditorGUI.EndChangeCheck())
        {
            SerializedProperty enabled = layoutProp.FindPropertyRelative(
                ResponsiveRectStateEditorUtil.EnabledProp);
            if (enabled != null)
                enabled.boolValue = true;
        }

        return y;
    }

    static SerializedProperty FindScaleProjection(SerializedProperty property) =>
        property.FindPropertyRelative("scaleProjection");

    static readonly GUIContent[] XyLabels =
    {
        new GUIContent("X"),
        new GUIContent("Y"),
    };
    static readonly GUIContent[] XyzLabels =
    {
        new GUIContent("X"),
        new GUIContent("Y"),
        new GUIContent("Z"),
    };

    static float Vector2RowHeight(GUIContent label) =>
        EditorGUIUtility.singleLineHeight;

    static float TransformFieldsHeight()
    {
        float line = EditorGUIUtility.singleLineHeight;
        float s = EditorGUIUtility.standardVerticalSpacing;
        return 3f * (line + s);
    }

    static float DrawVector3Field(Rect position, float y, float line, float s, GUIContent label, SerializedProperty prop)
    {
        Vector3 was = prop.vector3Value;
        var values = new[] { was.x, was.y, was.z };
        EditorGUI.MultiFloatField(
            new Rect(position.x, y, position.width, line),
            label,
            XyzLabels,
            values);
        var next = new Vector3(values[0], values[1], values[2]);
        if (next != was)
            prop.vector3Value = next;
        return y + line + s;
    }

    static void EnableApplyLocalForScale(SerializedProperty property)
    {
        if (ResponsiveCameraEditorUtil.SlotHasFocus(property))
            return;

        SerializedProperty applyPos = property.FindPropertyRelative("applyLocalPosition");
        SerializedProperty applyRot = property.FindPropertyRelative("applyLocalRotation");
        if (applyPos == null || applyRot == null)
            return;

        bool wasOff = !applyPos.boolValue && !applyRot.boolValue;
        applyPos.boolValue = true;
        applyRot.boolValue = true;
        if (!wasOff)
            return;

        Camera camera = FindTargetCamera(property);
        if (camera == null)
            return;
        CaptureLiveLocalPosition(property, camera);
        SerializedProperty localEuler = property.FindPropertyRelative("localEulerAngles");
        if (localEuler != null)
            localEuler.vector3Value = camera.transform.localEulerAngles;
    }

    static float DrawFocusFields(
        Rect position,
        float y,
        float line,
        float s,
        SerializedProperty property)
    {
        SerializedProperty targetProp = property.FindPropertyRelative("focusTarget");
        SerializedProperty screenProp = property.FindPropertyRelative("focusScreen");
        SerializedProperty fitProp = property.FindPropertyRelative("focusFit");
        SerializedProperty paddingProp = property.FindPropertyRelative("focusPadding");
        if (targetProp == null || screenProp == null)
            return y;

        EditorGUI.PropertyField(
            new Rect(position.x, y, position.width, line),
            targetProp,
            new GUIContent(
                "Focus Target",
                "World object to keep in this slot (pool table, board)."));
        y += line + s;

        EditorGUI.PropertyField(
            new Rect(position.x, y, position.width, line),
            screenProp,
            new GUIContent(
                "Focus Screen",
                "None off. Auto uses this slot's Focus Bounds when assigned, otherwise the whole camera. " +
                "Bounds uses Focus Bounds (Full if unset). Full ignores Focus Bounds. " +
                "Does not write Camera.rect."));
        y += line + s;

        y = DrawFocusBoundsObject(position, y, line, s, property);

        bool focusing = (ResponsiveCameraFocusScreen)screenProp.intValue !=
            ResponsiveCameraFocusScreen.None;
        using (new EditorGUI.DisabledScope(!focusing))
        {
            if (fitProp != null)
            {
                EditorGUI.PropertyField(
                    new Rect(position.x, y, position.width, line),
                    fitProp,
                    new GUIContent(
                        "Focus Fit",
                        "Cover fills the pane (may crop). Fit shows the whole object (may letterbox). " +
                        "The slot Orthographic Size / FOV is applied first as the seed, then Focus Fit adjusts it."));
                y += line + s;
            }

            if (paddingProp != null)
            {
                EditorGUI.PropertyField(
                    new Rect(position.x, y, position.width, line),
                    paddingProp,
                    new GUIContent("Focus Padding", "Cover / Fit margin. 1 = bounds only."));
                y += line + s;
            }
        }

        if (GetFocusMeasureHint(property, out string hint))
        {
            EditorGUI.HelpBox(
                new Rect(position.x, y, position.width, line * 2f),
                hint,
                MessageType.Warning);
            y += line * 2f + s;
        }

        return y;
    }

    static bool GetFocusMeasureHint(SerializedProperty property, out string hint)
    {
        hint = null;
        SerializedProperty screenProp = property.FindPropertyRelative("focusScreen");
        if (screenProp == null ||
            (ResponsiveCameraFocusScreen)screenProp.intValue ==
            ResponsiveCameraFocusScreen.None)
            return false;

        SerializedProperty fitProp = property.FindPropertyRelative("focusFit");
        if (fitProp == null ||
            fitProp.intValue == (int)ResponsiveCameraFocusFit.Center)
            return false;

        Transform target =
            property.FindPropertyRelative("focusTarget")?.objectReferenceValue as Transform;
        if (target == null)
        {
            hint = "Cover / Fit need a Focus Target with a Renderer or Collider.";
            return true;
        }

        if (target.GetComponentInChildren<Renderer>() == null &&
            target.GetComponentInChildren<Collider>() == null &&
            target.GetComponentInChildren<Collider2D>() == null)
        {
            hint = "Cover / Fit cannot measure this Focus Target (no Renderer or Collider).";
            return true;
        }

        return false;
    }

    static float DrawProjection(
        Rect position,
        float y,
        float line,
        float s,
        SerializedProperty property)
    {
        SerializedProperty orthographic = property.FindPropertyRelative("orthographic");
        if (orthographic == null)
            return y;

        int index = orthographic.boolValue ? 1 : 0;
        float labelW = EditorGUIUtility.labelWidth;
        EditorGUI.BeginChangeCheck();
        EditorGUI.LabelField(
            new Rect(position.x, y, labelW, line),
            new GUIContent(
                "Projection",
                "Orthographic or Perspective. Auto-detected from the Target on Capture / first assign; Apply writes this to the Target."));
        int next = EditorGUI.Popup(
            new Rect(position.x + labelW, y, position.width - labelW, line),
            index,
            ProjectionLabels);
        if (EditorGUI.EndChangeCheck())
        {
            bool becameOrtho = next == 1;
            orthographic.boolValue = becameOrtho;
            if (becameOrtho)
            {
                SerializedProperty applyOrtho = property.FindPropertyRelative("applyOrthographicSize");
                if (applyOrtho != null)
                    applyOrtho.boolValue = true;
            }
            else
            {
                SerializedProperty applyFov = property.FindPropertyRelative("applyFieldOfView");
                if (applyFov != null)
                    applyFov.boolValue = true;
            }
        }
        y += line + s;

        if (orthographic.boolValue)
        {
            y = DrawToggleField(
                position, y, line, s,
                property, "applyOrthographicSize", "orthographicSize");
            return DrawLiveProjectionHint(position, y, line, s, property);
        }

        y = DrawToggleField(
            position, y, line, s,
            property, "applyFieldOfView", "fieldOfView");
        return DrawLiveProjectionHint(position, y, line, s, property);
    }

    static float DrawLiveProjectionHint(
        Rect position,
        float y,
        float line,
        float s,
        SerializedProperty property)
    {
        if (!GetLiveProjectionHint(property, out string hint))
            return y;
        EditorGUI.LabelField(new Rect(position.x, y, position.width, line), " ", hint);
        return y + line + s;
    }

    static bool GetLiveProjectionHint(SerializedProperty property, out string hint)
    {
        hint = null;
        Camera camera = FindTargetCamera(property);
        if (camera == null)
            return false;

        bool scaleProjection = FindScaleProjection(property)?.boolValue == true;
        SerializedProperty fitProp = property.FindPropertyRelative("focusFit");
        bool focusOwnsProjection =
            ResponsiveCameraEditorUtil.SlotHasFocus(property) &&
            fitProp != null &&
            fitProp.intValue != (int)ResponsiveCameraFocusFit.Center;
        if (!scaleProjection && !focusOwnsProjection)
            return false;

        if (camera.orthographic)
        {
            float live = camera.orthographicSize;
            float authored = property.FindPropertyRelative("orthographicSize")?.floatValue ?? live;
            if (!focusOwnsProjection && Mathf.Abs(live - authored) < 1e-4f)
                return false;
            if (focusOwnsProjection)
            {
                hint = $"Live Size {live:0.###}. Focus Fit sets the Camera; this field is the slot seed.";
                return true;
            }

            hint = $"Live Size {live:0.###}. Scale Projection scales the slot seed for this display.";
            return true;
        }

        float liveFov = camera.fieldOfView;
        float authoredFov = property.FindPropertyRelative("fieldOfView")?.floatValue ?? liveFov;
        if (!focusOwnsProjection && Mathf.Abs(liveFov - authoredFov) < 1e-3f)
            return false;
        if (focusOwnsProjection)
        {
            hint = $"Live FOV {liveFov:0.#}. Focus Fit sets the Camera; this field is the slot seed.";
            return true;
        }

        hint = $"Live FOV {liveFov:0.#}. Scale Projection scales the slot seed for this display.";
        return true;
    }

    static float DrawLocalTransformFields(
        Rect position,
        float y,
        float line,
        float s,
        SerializedProperty property)
    {
        SerializedProperty applyPos = property.FindPropertyRelative("applyLocalPosition");
        SerializedProperty applyRot = property.FindPropertyRelative("applyLocalRotation");
        SerializedProperty localPos = property.FindPropertyRelative("localPosition");
        SerializedProperty localEuler = property.FindPropertyRelative("localEulerAngles");
        if (applyPos == null || applyRot == null || localPos == null || localEuler == null)
            return y;

        bool wasApply = applyPos.boolValue || applyRot.boolValue;
        EditorGUI.BeginChangeCheck();
        bool apply = EditorGUI.Toggle(
            new Rect(position.x, y, position.width, line),
            new GUIContent(
                "Apply Local",
                "When on, this slot writes its stored Local Position and Local Euler Angles. " +
                "When off, those values stay on this device slot and are not written " +
                "(Focus can own the live camera). Other profiles keep their own stored pose."),
            wasApply);
        if (EditorGUI.EndChangeCheck())
        {
            applyPos.boolValue = apply;
            applyRot.boolValue = apply;
            Camera camera = FindTargetCamera(property);
            if (apply && !wasApply && camera != null &&
                !ResponsiveCameraEditorUtil.SlotHasFocus(property))
            {
                CaptureLiveLocalPosition(property, camera);
                localEuler.vector3Value = camera.transform.localEulerAngles;
            }
        }
        y += line + s;

        EditorGUI.BeginChangeCheck();
        y = DrawVector3Field(position, y, line, s, LocalPositionLabel, localPos);
        y = DrawVector3Field(position, y, line, s, LocalEulerLabel, localEuler);
        if (EditorGUI.EndChangeCheck() &&
            !ResponsiveCameraEditorUtil.SlotHasFocus(property))
        {
            applyPos.boolValue = true;
            applyRot.boolValue = true;
        }

        return y;
    }

    static Camera FindTargetCamera(SerializedProperty stateProp)
    {
        SerializedProperty layout = ResponsiveCameraEditorUtil.FindLayoutProperty(stateProp);
        if (layout == null)
            return null;
        return layout.FindPropertyRelative("target")?.objectReferenceValue as Camera;
    }

    static void CaptureLiveLocalPosition(SerializedProperty stateProp, Camera camera)
    {
        SerializedProperty localPosition = stateProp.FindPropertyRelative("localPosition");
        if (localPosition == null)
            return;

        Vector3 live = camera.transform.localPosition;
        float orthographicSize = camera.orthographicSize;
        float fieldOfView = camera.fieldOfView;
        int slotIndex = ResponsiveWidgetLayout.StatePropertyIndex(stateProp.name);
        ResponsiveCameraEditorUtil.UnscaleLiveToDesign(
            ResponsiveWidgetLayout.SlotFormFactor(slotIndex),
            ResponsiveWidgetLayout.SlotOrientation(slotIndex),
            GetSlotUiSize(stateProp),
            (ResponsiveDesignScaleMode)(stateProp.FindPropertyRelative("uiScaleMode")?.intValue ?? 0),
            scalePosition: stateProp.FindPropertyRelative("scaleFromUiSize")?.boolValue == true,
            scaleProjection: false,
            ref live,
            ref orthographicSize,
            ref fieldOfView);

        localPosition.vector3Value = live;
    }

    static Vector2 GetSlotUiSize(SerializedProperty stateProp)
    {
        var responsiveCamera = stateProp.serializedObject.targetObject as ResponsiveCamera;
        return responsiveCamera != null
            ? responsiveCamera.GetUiLayoutSize()
            : ResponsiveDevice.GetWatchScreenSize();
    }

    static float DrawToggleField(
        Rect position,
        float y,
        float line,
        float s,
        SerializedProperty property,
        string toggleName,
        string valueName)
    {
        SerializedProperty toggle = property.FindPropertyRelative(toggleName);
        if (toggle == null)
            return y;

        EditorGUI.PropertyField(new Rect(position.x, y, position.width, line), toggle);
        y += line + s;
        if (!toggle.boolValue)
            return y;

        SerializedProperty value = property.FindPropertyRelative(valueName);
        if (value == null)
            return y;

        EditorGUI.PropertyField(
            new Rect(position.x, y, position.width, line),
            value);
        y += line + s;
        return y;
    }

    static void CaptureFromTargetCamera(SerializedProperty stateProp)
    {
        SerializedProperty layout = ResponsiveCameraEditorUtil.FindLayoutProperty(stateProp);
        if (layout == null)
            return;

        if (layout.FindPropertyRelative("target")?.objectReferenceValue is not Camera camera)
        {
            Debug.LogWarning("[ResponsiveCamera] Assign a Target camera before Capture From Camera.");
            return;
        }

        ResponsiveCamera responsiveCamera =
            stateProp.serializedObject.targetObject as ResponsiveCamera;
        Vector2 uiSize = responsiveCamera != null
            ? responsiveCamera.GetUiLayoutSize()
            : ResponsiveDevice.GetWatchScreenSize();

        int slotIndex = ResponsiveWidgetLayout.StatePropertyIndex(stateProp.name);
        ResponsiveDevice.FormFactor slotForm = ResponsiveWidgetLayout.SlotFormFactor(slotIndex);

        Undo.RecordObject(stateProp.serializedObject.targetObject, "Capture Camera Slot");
        ResponsiveCameraEditorUtil.WriteCapturedState(
            stateProp,
            ResponsiveCameraState.From(camera),
            enabled: true,
            uiSize,
            slotForm);
        stateProp.serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(stateProp.serializedObject.targetObject);

        if (responsiveCamera == null)
            return;

        bool applyViewport = camera != null;
        using (ResponsiveUiWidgetSync.SuppressCapture())
        using (ResponsiveCameraSync.SuppressCapture())
        {
            ResponsiveCameraSync.IgnoreTargetCapture(camera);
            Undo.RecordObject(camera.transform, "Capture Camera Slot Preview");
            Undo.RecordObject(camera, "Capture Camera Slot Preview");
            ResponsiveCameraEditorUtil.ReadState(stateProp)
                .Apply(
                    camera,
                    applyViewport,
                    uiSize,
                    slotForm,
                    ResponsiveWidgetLayout.SlotOrientation(slotIndex));
            ResponsiveCameraEditorUtil.ApplyFocus(stateProp, layout, camera);
            ResponsiveCameraEditorUtil.FitBackground(layout, camera);
        }
    }

    #endregion
}
}

#endif
