#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

using FDPlus.ResponsiveUI;

namespace FDPlus.ResponsiveUI.Editor
{
/// <summary>
/// Draws <see cref="ResponsiveRectState"/>: layout + design-scale fields hide when Enabled is off.
/// Default Scale (origin) stays visible whenever Enabled; Mode / Design Canvas Size only when Scale is on.
/// Camera Focus Bounds reuses <see cref="DrawLayoutFields"/> without the slot foldout.
/// </summary>
[CustomPropertyDrawer(typeof(ResponsiveRectState))]
public class ResponsiveRectStateDrawer : PropertyDrawer
{
    #region Inspector

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float line = EditorGUIUtility.singleLineHeight;
        float s = EditorGUIUtility.standardVerticalSpacing;

        property.isExpanded = ResponsiveWidgetLayout.GetStatePropertyIndex(property.name, out _)
            ? ResponsiveUiDetectionDrawer.ShouldExpandState(property)
            : true;
        if (!property.isExpanded)
            return line;

        float h = line + s + line; // foldout + enabled
        if (!ResponsiveRectStateEditorUtil.IsEnabled(property))
            return h;

        bool wide = EditorGUIUtility.wideMode;
        EditorGUIUtility.wideMode = true;
        h += ResponsiveUiInspectorFoldouts.SlotSectionHeight(
            property, "WidgetSlot", "Layout", true, line + GeometryFieldsHeight(property));
        h += ResponsiveUiInspectorFoldouts.SlotSectionHeight(
            property, "WidgetSlot", "Scale", true, ScaleFieldsHeight(property));
        EditorGUIUtility.wideMode = wide;
        return h;
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        float line = EditorGUIUtility.singleLineHeight;
        float s = EditorGUIUtility.standardVerticalSpacing;

        Rect row = new Rect(position.x, position.y, position.width, line);
        bool expanded = ResponsiveUiSlotClipboard.DrawFoldoutRow(
            row, property, label, ResponsiveUiSlotClipboard.Kind.Widget);
        if (!expanded)
        {
            EditorGUI.EndProperty();
            return;
        }

        ResponsiveUiInspectorFoldouts.DrawNestedContent(position, content =>
        {
            float y = position.y + line + s;

            SerializedProperty enabledProp = property.FindPropertyRelative(
                ResponsiveRectStateEditorUtil.EnabledProp);
            if (enabledProp == null)
                return;

            using (new EditorGUI.DisabledScope(ResponsiveUiEditorSettings.LockEnabledToggles))
            {
                bool wasEnabled = enabledProp.boolValue;
                EditorGUI.PropertyField(new Rect(content.x, y, content.width, line), enabledProp);
                if (!wasEnabled && enabledProp.boolValue)
                    ResponsiveRectStateEditorUtil.CopyFallback(property);
            }
            y += line;

            if (!enabledProp.boolValue)
                return;

            y = ResponsiveUiInspectorFoldouts.DrawSlotSection(
                content, y, line + GeometryFieldsHeight(property),
                property, "WidgetSlot", "Layout", "Layout", GeometrySummary(property),
                ResponsiveUiInspectorFoldouts.TintGreen, true,
                body =>
                {
                    float by = body.y;
                    EditorGUI.LabelField(
                        new Rect(body.x, by, body.width, line),
                        "Reference Model",
                        ResponsiveUiEditorSettings.GetModelName(
                            ResponsiveWidgetLayout.SlotFormFactor(property.name)));
                    DrawGeometryFields(body, by + line, property);
                });
            ResponsiveUiInspectorFoldouts.DrawSlotSection(
                content, y, ScaleFieldsHeight(property),
                property, "WidgetSlot", "Scale", "Scale", ScaleSummary(property),
                ResponsiveUiInspectorFoldouts.TintPurple, true,
                body => DrawScaleFields(body, body.y, property));
        });
        EditorGUI.EndProperty();
    }

    /// <summary>
    /// Design Scale, anchors, and Pos / Size — shared by widget slots and camera Focus Bounds.
    /// </summary>
    internal static float LayoutFieldsHeight(SerializedProperty property)
    {
        if (property == null)
            return 0f;

        bool wide = EditorGUIUtility.wideMode;
        EditorGUIUtility.wideMode = true;
        float h = ScaleFieldsHeight(property) + GeometryFieldsHeight(property);
        EditorGUIUtility.wideMode = wide;
        return h;
    }

    internal static float DrawLayoutFields(
        Rect position,
        float y,
        SerializedProperty property)
    {
        if (property == null)
            return y;

        bool wide = EditorGUIUtility.wideMode;
        EditorGUIUtility.wideMode = true;
        y = DrawScaleFields(position, y, property);
        y = DrawGeometryFields(position, y, property);
        EditorGUIUtility.wideMode = wide;
        return y;
    }

    internal static float ScaleFieldsHeight(SerializedProperty property)
    {
        if (property == null)
            return 0f;

        float s = EditorGUIUtility.standardVerticalSpacing;
        float line = EditorGUIUtility.singleLineHeight;
        float h = LineFieldHeight(property, ResponsiveRectStateEditorUtil.ScaleFromDesignCanvasProp, s);
        h += Vector2FieldHeight(property, ResponsiveRectStateEditorUtil.DefaultScaleProp, s);
        if (!ResponsiveRectStateEditorUtil.IsScaleFromDesignCanvas(property))
            return h;

        h += LineFieldHeight(property, ResponsiveRectStateEditorUtil.DesignScaleModeProp, s);
        SerializedProperty mode = property.FindPropertyRelative(
            ResponsiveRectStateEditorUtil.DesignScaleModeProp);
        if (mode != null &&
            (ResponsiveDesignScaleMode)mode.intValue == ResponsiveDesignScaleMode.Auto)
            h += s + line;
        h += Vector2FieldHeight(property, ResponsiveRectStateEditorUtil.DesignCanvasSizeProp, s);
        h += s + line;
        return h;
    }

    internal static float GeometryFieldsHeight(SerializedProperty property)
    {
        if (property == null)
            return 0f;

        float s = EditorGUIUtility.standardVerticalSpacing;
        float line = EditorGUIUtility.singleLineHeight;
        float h = Vector2FieldHeight(property, ResponsiveRectStateEditorUtil.AnchorMinProp, s);
        h += Vector2FieldHeight(property, ResponsiveRectStateEditorUtil.AnchorMaxProp, s);
        h += Vector2FieldHeight(property, ResponsiveRectStateEditorUtil.PivotProp, s);
        h += 2f * (s + line);
        return h;
    }

    internal static float DrawScaleFields(
        Rect position,
        float y,
        SerializedProperty property)
    {
        if (property == null)
            return y;

        float s = EditorGUIUtility.standardVerticalSpacing;
        float line = EditorGUIUtility.singleLineHeight;
        y = DrawLineField(
            position, y, s, line, property, ResponsiveRectStateEditorUtil.ScaleFromDesignCanvasProp);
        y = DrawVector2Field(
            position, y, s, property, ResponsiveRectStateEditorUtil.DefaultScaleProp);
        if (!ResponsiveRectStateEditorUtil.IsScaleFromDesignCanvas(property))
            return y;

        y = DrawLineField(
            position, y, s, line, property, ResponsiveRectStateEditorUtil.DesignScaleModeProp);
        SerializedProperty mode = property.FindPropertyRelative(
            ResponsiveRectStateEditorUtil.DesignScaleModeProp);
        if (mode != null &&
            (ResponsiveDesignScaleMode)mode.intValue == ResponsiveDesignScaleMode.Auto)
        {
            y += s;
            EditorGUI.LabelField(
                new Rect(position.x, y, position.width, line),
                " ",
                ResponsiveRectStateEditorUtil.FormatAutoScaleHint(
                    property, ResponsiveDevice.GetWatchScreenSize()));
            y += line;
        }

        return DrawLockedDesignCanvasSize(position, y, s, line, property);
    }

    internal static float DrawGeometryFields(
        Rect position,
        float y,
        SerializedProperty property)
    {
        if (property == null)
            return y;

        float s = EditorGUIUtility.standardVerticalSpacing;
        float line = EditorGUIUtility.singleLineHeight;
        y = DrawVector2Field(
            position, y, s, property, ResponsiveRectStateEditorUtil.AnchorMinProp);
        y = DrawVector2Field(
            position, y, s, property, ResponsiveRectStateEditorUtil.AnchorMaxProp);
        y = DrawVector2Field(
            position, y, s, property, ResponsiveRectStateEditorUtil.PivotProp);
        return DrawPresetGeometry(position, y, line, s, property);
    }

    #endregion

    #region Helpers

    static string ScaleSummary(SerializedProperty property)
    {
        if (!ResponsiveRectStateEditorUtil.IsScaleFromDesignCanvas(property))
            return "Off";

        SerializedProperty mode = property.FindPropertyRelative(
            ResponsiveRectStateEditorUtil.DesignScaleModeProp);
        return mode != null
            ? ((ResponsiveDesignScaleMode)mode.intValue).ToString()
            : "On";
    }

    static string GeometrySummary(SerializedProperty property)
    {
        Vector2 min = property.FindPropertyRelative(
            ResponsiveRectStateEditorUtil.AnchorMinProp)?.vector2Value ?? Vector2.zero;
        Vector2 max = property.FindPropertyRelative(
            ResponsiveRectStateEditorUtil.AnchorMaxProp)?.vector2Value ?? Vector2.one;
        Vector2 pos = property.FindPropertyRelative(
            ResponsiveRectStateEditorUtil.AnchoredPositionProp)?.vector2Value ?? Vector2.zero;
        Vector2 size = property.FindPropertyRelative(
            ResponsiveRectStateEditorUtil.SizeDeltaProp)?.vector2Value ?? Vector2.zero;
        Vector2 pivot = property.FindPropertyRelative(
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

    static readonly GUIContent[] XYLabels =
    {
        new GUIContent("X"),
        new GUIContent("Y"),
    };
    static readonly GUIContent LeftRightRowLabel = new GUIContent("Left / Right");
    static readonly GUIContent TopBottomRowLabel = new GUIContent("Top / Bottom");
    static readonly GUIContent PosXWidthRowLabel = new GUIContent("Pos X / Width");
    static readonly GUIContent PosYHeightRowLabel = new GUIContent("Pos Y / Height");
    static readonly GUIContent[] LeftRightLabels =
    {
        new GUIContent("Left"),
        new GUIContent("Right"),
    };
    static readonly GUIContent[] TopBottomLabels =
    {
        new GUIContent("Top"),
        new GUIContent("Bottom"),
    };
    static readonly GUIContent[] PosXWidthLabels =
    {
        new GUIContent("Pos X"),
        new GUIContent("Width"),
    };
    static readonly GUIContent[] PosYHeightLabels =
    {
        new GUIContent("Pos Y"),
        new GUIContent("Height"),
    };

    const float PairLabelWidth = 58f;
    const float PairGap = 4f;

    static GUIContent LabelOf(SerializedProperty prop) =>
        new GUIContent(prop.displayName, prop.tooltip);

    static float LineFieldHeight(SerializedProperty property, string name, float spacing)
    {
        SerializedProperty prop = property.FindPropertyRelative(name);
        return prop == null ? 0f : spacing + EditorGUIUtility.singleLineHeight;
    }

    static float Vector2FieldHeight(SerializedProperty property, string name, float spacing)
    {
        SerializedProperty prop = property.FindPropertyRelative(name);
        return prop == null ? 0f : spacing + EditorGUIUtility.singleLineHeight;
    }

    static float DrawLineField(
        Rect position,
        float y,
        float spacing,
        float line,
        SerializedProperty property,
        string name)
    {
        SerializedProperty prop = property.FindPropertyRelative(name);
        if (prop == null)
            return y;

        y += spacing;
        EditorGUI.PropertyField(new Rect(position.x, y, position.width, line), prop, LabelOf(prop));
        return y + line;
    }

    static float DrawVector2Field(
        Rect position,
        float y,
        float spacing,
        SerializedProperty property,
        string name)
    {
        SerializedProperty prop = property.FindPropertyRelative(name);
        if (prop == null)
            return y;

        float line = EditorGUIUtility.singleLineHeight;
        var values = new[] { prop.vector2Value.x, prop.vector2Value.y };
        y += spacing;
        EditorGUI.BeginChangeCheck();
        EditorGUI.MultiFloatField(
            new Rect(position.x, y, position.width, line),
            LabelOf(prop),
            XYLabels,
            values);
        if (EditorGUI.EndChangeCheck())
            prop.vector2Value = new Vector2(values[0], values[1]);
        return y + line;
    }

    static readonly GUIContent DesignCanvasSizeLabel = new GUIContent(
        "Design Canvas Size",
        "Locked to Project Settings → Responsive UI Design Canvas for this form factor.");

    static float DrawLockedDesignCanvasSize(
        Rect position,
        float y,
        float spacing,
        float line,
        SerializedProperty property)
    {
        SerializedProperty design = property.FindPropertyRelative(
            ResponsiveRectStateEditorUtil.DesignCanvasSizeProp);
        if (design == null)
            return y;

        int slotIndex = ResponsiveRectStateEditorUtil.SlotIndexOf(property);
        ResponsiveDevice.FormFactor form = ResponsiveWidgetLayout.SlotFormFactor(slotIndex);
        Vector2 resolved = ResponsiveUI.OrientSizeForSlot(
            ResponsiveRectState.GetDefaultDesignCanvasSize(form), form, ResponsiveWidgetLayout.SlotOrientation(slotIndex));

        y += spacing;
        using (new EditorGUI.DisabledScope(true))
        {
            var values = new[] { resolved.x, resolved.y };
            EditorGUI.MultiFloatField(
                new Rect(position.x, y, position.width, line),
                DesignCanvasSizeLabel,
                XYLabels,
                values);
        }
        y += line + spacing;
        EditorGUI.LabelField(
            new Rect(position.x, y, position.width, line),
            " ",
            $"Locked to Responsive UI settings: {resolved.x:0}×{resolved.y:0}");
        return y + line;
    }

    static float DrawPresetGeometry(
        Rect position,
        float y,
        float line,
        float spacing,
        SerializedProperty property)
    {
        SerializedProperty anchorsMin = property.FindPropertyRelative(
            ResponsiveRectStateEditorUtil.AnchorMinProp);
        SerializedProperty anchorsMax = property.FindPropertyRelative(
            ResponsiveRectStateEditorUtil.AnchorMaxProp);
        SerializedProperty anchoredPosition = property.FindPropertyRelative(
            ResponsiveRectStateEditorUtil.AnchoredPositionProp);
        SerializedProperty sizeDelta = property.FindPropertyRelative(
            ResponsiveRectStateEditorUtil.SizeDeltaProp);
        SerializedProperty pivotProp = property.FindPropertyRelative(
            ResponsiveRectStateEditorUtil.PivotProp);
        if (anchorsMin == null ||
            anchorsMax == null ||
            anchoredPosition == null ||
            sizeDelta == null ||
            pivotProp == null)
            return y;

        Vector2 min = anchorsMin.vector2Value;
        Vector2 max = anchorsMax.vector2Value;
        Vector2 pos = anchoredPosition.vector2Value;
        Vector2 size = sizeDelta.vector2Value;
        Vector2 pivot = pivotProp.vector2Value;
        bool stretchX = ResponsiveRectState.IsAxisStretched(min.x, max.x);
        bool stretchY = ResponsiveRectState.IsAxisStretched(min.y, max.y);
        float[] horizontal = stretchX
            ? new[]
            {
                pos.x - size.x * pivot.x,
                -(pos.x + size.x * (1f - pivot.x)),
            }
            : new[] { pos.x, size.x };
        float[] vertical = stretchY
            ? new[]
            {
                -(pos.y + size.y * (1f - pivot.y)),
                pos.y - size.y * pivot.y,
            }
            : new[] { pos.y, size.y };

        EditorGUI.BeginChangeCheck();
        y = DrawFloatPair(
            position,
            y,
            line,
            spacing,
            stretchX ? LeftRightRowLabel : PosXWidthRowLabel,
            stretchX ? LeftRightLabels : PosXWidthLabels,
            horizontal);
        bool horizontalChanged = EditorGUI.EndChangeCheck();

        EditorGUI.BeginChangeCheck();
        y = DrawFloatPair(
            position,
            y,
            line,
            spacing,
            stretchY ? TopBottomRowLabel : PosYHeightRowLabel,
            stretchY ? TopBottomLabels : PosYHeightLabels,
            vertical);
        bool verticalChanged = EditorGUI.EndChangeCheck();

        if (horizontalChanged)
        {
            if (stretchX)
            {
                size.x = -horizontal[1] - horizontal[0];
                pos.x = horizontal[0] + size.x * pivot.x;
            }
            else
            {
                pos.x = horizontal[0];
                size.x = horizontal[1];
            }
        }

        if (verticalChanged)
        {
            if (stretchY)
            {
                size.y = -vertical[0] - vertical[1];
                pos.y = vertical[1] + size.y * pivot.y;
            }
            else
            {
                pos.y = vertical[0];
                size.y = vertical[1];
            }
        }

        if (horizontalChanged || verticalChanged)
        {
            anchoredPosition.vector2Value = pos;
            sizeDelta.vector2Value = size;
        }

        return y;
    }

    static float DrawFloatPair(
        Rect position,
        float y,
        float line,
        float spacing,
        GUIContent rowLabel,
        GUIContent[] labels,
        float[] values)
    {
        y += spacing;
        Rect row = new Rect(position.x, y, position.width, line);
        ResponsiveUiInspectorFoldouts.AtZeroIndent(() =>
        {
            Rect fields = EditorGUI.PrefixLabel(row, rowLabel);
            float oldLabel = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = PairLabelWidth;
            float col = (fields.width - PairGap) * 0.5f;
            values[0] = EditorGUI.FloatField(
                new Rect(fields.x, fields.y, col, line),
                labels[0],
                values[0]);
            values[1] = EditorGUI.FloatField(
                new Rect(fields.x + col + PairGap, fields.y, col, line),
                labels[1],
                values[1]);
            EditorGUIUtility.labelWidth = oldLabel;
        });
        return y + line;
    }

    #endregion
}
}

#endif
