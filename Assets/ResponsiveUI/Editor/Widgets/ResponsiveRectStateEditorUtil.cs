#if UNITY_EDITOR
using System;
using System.Collections.Generic;

using UnityEditor;
using UnityEngine;

using FDPlus.ResponsiveUI;
using FDPlus.ResponsiveUI.Shared.Editor;

namespace FDPlus.ResponsiveUI.Editor
{
/// <summary>
/// Shared SerializedProperty helpers for <see cref="ResponsiveRectState"/> editor capture / preview.
/// </summary>
public static class ResponsiveRectStateEditorUtil
{
    #region Constants

    public const string EnabledProp = "enabled";
    public const string ScaleFromDesignCanvasProp = "scaleFromDesignCanvas";
    public const string DesignScaleModeProp = "designScaleMode";
    public const string DesignCanvasSizeProp = "designCanvasSize";
    public const string DefaultScaleProp = "defaultScale";
    public const string AnchorMinProp = "anchorMin";
    public const string AnchorMaxProp = "anchorMax";
    public const string AnchoredPositionProp = "anchoredPosition";
    public const string SizeDeltaProp = "sizeDelta";
    public const string PivotProp = "pivot";

    #endregion

    #region Slot / Auto hint

    /// <summary>
    /// Widget slot index from the property name, or from a nested path
    /// (<c>phoneLandscape.focusBoundsLayout</c>).
    /// </summary>
    public static int SlotIndexOf(SerializedProperty property)
    {
        if (property == null)
            return 0;

        if (ResponsiveWidgetLayout.GetStatePropertyIndex(property.name, out int index))
            return index;

        string path = property.propertyPath ?? "";
        string[] names = ResponsiveWidgetLayout.StatePropertyNames;
        for (int i = 0; i < names.Length; i++)
        {
            string token = "." + names[i];
            if (path.Contains(token + ".", StringComparison.Ordinal) ||
                path.EndsWith(token, StringComparison.Ordinal))
                return i;
        }

        return 0;
    }

    /// <summary>
    /// Inspector line: Auto → Height when wider than Design Canvas, Width when taller.
    /// </summary>
    public static string FormatAutoScaleHint(
        ResponsiveDevice.FormFactor form,
        ResponsiveDevice.Orientation orientation,
        Vector2 liveSize)
    {
        Vector2 design = ResponsiveUI.OrientSizeForSlot(
            ResponsiveRectState.GetDefaultDesignCanvasSize(form),
            form,
            orientation);
        Vector2 live = ResponsiveDesignScale.ResolveIdentityLiveSize(
            ResponsiveUI.OrientSizeForSlot(liveSize, form, orientation),
            form,
            orientation,
            design);
        ResponsiveDesignScaleMode resolved = ResponsiveRectState.ResolveAutoScaleMode(
            ResponsiveDesignScaleMode.Auto, live, design, matchCurrentOrientation: false);
        string axis = resolved == ResponsiveDesignScaleMode.Height ? "Height" : "Width";
        string formName = ResponsiveDevice.GetFormDisplayName(form);
        if (live.x <= 0f || live.y <= 0f || design.x <= 0f || design.y <= 0f)
            return $"Auto → {axis}";

        float liveAspect = live.x / live.y;
        float designAspect = design.x / design.y;
        string cmp = Mathf.Abs(liveAspect - designAspect) < 1e-4f
            ? "matches"
            : liveAspect > designAspect ? "wider than" : "taller than";
        return $"Auto → {axis} ({cmp} {formName} {design.x:0}×{design.y:0})";
    }

    public static string FormatAutoScaleHint(SerializedProperty stateProp, Vector2 liveSize)
    {
        int slotIndex = SlotIndexOf(stateProp);
        return FormatAutoScaleHint(
            ResponsiveWidgetLayout.SlotFormFactor(slotIndex),
            ResponsiveWidgetLayout.SlotOrientation(slotIndex),
            liveSize);
    }

    #endregion

    #region Serialized

    public static void WriteDesignScale(
        SerializedProperty stateProp,
        ResponsiveRectState state)
    {
        if (stateProp == null)
            return;

        SerializedProperty scaleOn = stateProp.FindPropertyRelative(ScaleFromDesignCanvasProp);
        if (scaleOn != null)
            scaleOn.boolValue = state.scaleFromDesignCanvas;

        SerializedProperty mode = stateProp.FindPropertyRelative(DesignScaleModeProp);
        if (mode != null)
            mode.intValue = (int)state.designScaleMode;

        // Persist authored sentinels. (0,0) Design Canvas Size follows Project Settings
        // in the Editor and built-in form sizes in the player — do not bake resolved pixels.
        SerializedProperty size = stateProp.FindPropertyRelative(DesignCanvasSizeProp);
        if (size != null)
            size.vector2Value = state.designCanvasSize;

        SerializedProperty defaultScale = stateProp.FindPropertyRelative(DefaultScaleProp);
        if (defaultScale != null)
            defaultScale.vector2Value = state.defaultScale;
    }

    public static void WriteState(
        SerializedProperty stateProp,
        ResponsiveRectState state)
    {
        if (stateProp == null)
            return;

        SerializedProperty enabled = stateProp.FindPropertyRelative(EnabledProp);
        if (enabled != null)
            enabled.boolValue = state.enabled;

        WriteDesignScale(stateProp, state);
        WriteLayout(stateProp, state);
    }

    /// <summary>
    /// Unity RectTransform Reset values. Keeps Enabled and Size Delta
    /// (Width / Height on fixed axes, combined edge offsets on stretched axes).
    /// </summary>
    public static void ResetSlot(SerializedProperty stateProp)
    {
        Vector2 size = stateProp.FindPropertyRelative(SizeDeltaProp).vector2Value;
        ResponsiveRectState reset = ResponsiveRectState.CreateDefault(IsEnabled(stateProp));
        reset.sizeDelta = size;
        WriteState(stateProp, reset);
    }

    /// <summary>
    /// Clears every slot to Unity RectTransform defaults and re-applies new-widget
    /// Enabled / Design Scale policy. Used when the Inspector Target is set to None.
    /// </summary>
    public static void ResetSlots(SerializedProperty widgetProp)
    {
        if (widgetProp == null)
            return;

        string[] states = ResponsiveWidgetLayout.StatePropertyNames;
        for (int i = 0; i < states.Length; i++)
        {
            SerializedProperty slot = widgetProp.FindPropertyRelative(states[i]);
            if (slot == null)
                continue;
            WriteState(slot, ResponsiveRectState.CreateDefault(enabled: false));
        }

        SeedSlots(widgetProp);
    }

    /// <summary>
    /// Unity list <c>+</c> duplicates the previous row. Clear Target and reset slots
    /// so the new element is empty, with the same Enabled / Design Scale defaults
    /// as Add Component.
    /// </summary>
    public static void ResetNewListRow(SerializedProperty widgetProp)
    {
        if (widgetProp == null)
            return;

        SerializedProperty target = widgetProp.FindPropertyRelative("target");
        if (target != null)
            target.objectReferenceValue = null;
        ResetSlots(widgetProp);
    }

    /// <summary>
    /// After a widget Target ObjectField edit: first assign / Missing→new seeds every
    /// slot; live retarget writes the active slot only; clear to None resets the row.
    /// Call while <paramref name="widgetProp"/> already holds the new Target value.
    /// </summary>
    public static void ApplyInspectorTargetChange(
        SerializedProperty widgetProp,
        UnityEngine.Object previousTarget,
        int previousInstanceId,
        ResponsiveUI ui)
    {
        if (widgetProp == null)
            return;

        SerializedProperty targetProp = widgetProp.FindPropertyRelative("target");
        UnityEngine.Object next = targetProp != null ? targetProp.objectReferenceValue : null;

        if (next is RectTransform rt)
        {
            WriteCaptureToSlots(
                widgetProp,
                rt,
                ui,
                applyEnabledDefaults: ResponsiveUiObjectReference.IsFirstAssign(previousTarget));
            return;
        }

        if (ResponsiveUiObjectReference.WasClearedToNone(previousInstanceId, next))
            ResetSlots(widgetProp);
    }

    /// <summary>Writes layout vectors only — leaves enabled / design-scale fields unchanged.</summary>
    public static void WriteLayout(SerializedProperty stateProp, ResponsiveRectState state)
    {
        if (stateProp == null)
            return;

        SerializedProperty anchorMin = stateProp.FindPropertyRelative(AnchorMinProp);
        if (anchorMin != null)
            anchorMin.vector2Value = state.anchorMin;

        SerializedProperty anchorMax = stateProp.FindPropertyRelative(AnchorMaxProp);
        if (anchorMax != null)
            anchorMax.vector2Value = state.anchorMax;

        SerializedProperty anchoredPosition = stateProp.FindPropertyRelative(AnchoredPositionProp);
        if (anchoredPosition != null)
            anchoredPosition.vector2Value = state.anchoredPosition;

        SerializedProperty sizeDelta = stateProp.FindPropertyRelative(SizeDeltaProp);
        if (sizeDelta != null)
            sizeDelta.vector2Value = state.sizeDelta;

        SerializedProperty pivot = stateProp.FindPropertyRelative(PivotProp);
        if (pivot != null)
            pivot.vector2Value = state.pivot;
    }

    public static ResponsiveRectState ReadState(SerializedProperty stateProp)
    {
        if (stateProp == null)
            return default;

        ResponsiveRectState state = new ResponsiveRectState
        {
            enabled = IsEnabled(stateProp),
        };

        SerializedProperty anchorMin = stateProp.FindPropertyRelative(AnchorMinProp);
        if (anchorMin != null)
            state.anchorMin = anchorMin.vector2Value;

        SerializedProperty anchorMax = stateProp.FindPropertyRelative(AnchorMaxProp);
        if (anchorMax != null)
            state.anchorMax = anchorMax.vector2Value;

        SerializedProperty anchoredPosition = stateProp.FindPropertyRelative(AnchoredPositionProp);
        if (anchoredPosition != null)
            state.anchoredPosition = anchoredPosition.vector2Value;

        SerializedProperty sizeDelta = stateProp.FindPropertyRelative(SizeDeltaProp);
        if (sizeDelta != null)
            state.sizeDelta = sizeDelta.vector2Value;

        SerializedProperty pivot = stateProp.FindPropertyRelative(PivotProp);
        if (pivot != null)
            state.pivot = pivot.vector2Value;

        SerializedProperty scaleOn = stateProp.FindPropertyRelative(ScaleFromDesignCanvasProp);
        if (scaleOn != null)
            state.scaleFromDesignCanvas = scaleOn.boolValue;

        SerializedProperty mode = stateProp.FindPropertyRelative(DesignScaleModeProp);
        if (mode != null)
            state.designScaleMode = (ResponsiveDesignScaleMode)mode.intValue;

        SerializedProperty size = stateProp.FindPropertyRelative(DesignCanvasSizeProp);
        if (size != null)
            state.designCanvasSize = size.vector2Value;

        SerializedProperty defaultScale = stateProp.FindPropertyRelative(DefaultScaleProp);
        if (defaultScale != null)
            state.defaultScale = defaultScale.vector2Value;

        return state;
    }

    public static bool SameAuthoring(in ResponsiveRectState a, in ResponsiveRectState b) =>
        a.HasSameLayout(b);

    /// <summary>
    /// Copies every serialized RectTransform and design-scale value from one
    /// exact slot to another on each widget row. Targets and other slots stay
    /// unchanged.
    /// </summary>
    /// <returns>The number of widget rows copied.</returns>
    public static int CopySlotAcrossWidgets(
        SerializedProperty widgets,
        int sourceSlotIndex,
        int destinationSlotIndex)
    {
        string[] states = ResponsiveWidgetLayout.StatePropertyNames;
        if (!CanCopySlot(widgets, sourceSlotIndex, destinationSlotIndex, states))
            return 0;

        string sourceName = states[sourceSlotIndex];
        string destinationName = states[destinationSlotIndex];
        for (int i = 0; i < widgets.arraySize; i++)
            CopySlotOnLayout(
                widgets.GetArrayElementAtIndex(i),
                sourceName,
                destinationName);

        return widgets.arraySize;
    }

    /// <summary>
    /// Copies one exact slot to another on the currently shown Side + Preview
    /// layout of each two-side widget row. Other layouts and slots stay unchanged.
    /// </summary>
    public static int CopySlotAcrossTwoSideWidgets(
        SerializedProperty widgets,
        int sourceSlotIndex,
        int destinationSlotIndex,
        ResponsiveUiSide side,
        bool previewAnimationStart)
    {
        string[] states = ResponsiveWidgetLayout.StatePropertyNames;
        if (!CanCopySlot(widgets, sourceSlotIndex, destinationSlotIndex, states))
            return 0;

        string sourceName = states[sourceSlotIndex];
        string destinationName = states[destinationSlotIndex];
        int copied = 0;
        for (int i = 0; i < widgets.arraySize; i++)
        {
            SerializedProperty widget = widgets.GetArrayElementAtIndex(i);
            SerializedProperty layout = widget.FindPropertyRelative(
                TwoSideLayoutPropertyName(widget, side, previewAnimationStart));
            if (layout == null)
                continue;
            CopySlotOnLayout(layout, sourceName, destinationName);
            copied++;
        }

        return copied;
    }

    /// <summary>
    /// Horizontal mirror used when copying a two-side layout from Left to Right
    /// or Right to Left. Anchor Min/Max X swap through 1 − x, Pivot X becomes
    /// 1 − x (0.5 stays 0.5), and Pos X is negated. Y, size, Enabled, and
    /// Design Scale stay the same.
    /// </summary>
    public static ResponsiveRectState MirrorHorizontally(in ResponsiveRectState state)
    {
        ResponsiveRectState mirrored = state;
        mirrored.anchorMin = new Vector2(1f - state.anchorMax.x, state.anchorMin.y);
        mirrored.anchorMax = new Vector2(1f - state.anchorMin.x, state.anchorMax.y);
        mirrored.pivot = new Vector2(1f - state.pivot.x, state.pivot.y);
        mirrored.anchoredPosition = new Vector2(
            -state.anchoredPosition.x,
            state.anchoredPosition.y);
        return mirrored;
    }

    /// <summary>
    /// Copies every slot on Normal and Animation Start from one side onto the
    /// other for each two-side widget, mirroring X so a right-edge panel
    /// becomes a left-edge panel (and vice versa). Targets stay unchanged.
    /// Copies Animation Start even when Animation Enabled is off.
    /// </summary>
    /// <returns>The number of widget rows copied.</returns>
    public static int CopySideAcrossTwoSideWidgets(
        SerializedProperty widgets,
        ResponsiveUiSide sourceSide,
        ResponsiveUiSide destinationSide)
    {
        if (widgets == null ||
            !widgets.isArray ||
            sourceSide == destinationSide)
            return 0;

        int copied = 0;
        for (int i = 0; i < widgets.arraySize; i++)
        {
            SerializedProperty widget = widgets.GetArrayElementAtIndex(i);
            SerializedProperty sourceNormal = widget.FindPropertyRelative(
                TwoSideLayoutPropertyName(sourceSide, animationStart: false));
            SerializedProperty destinationNormal = widget.FindPropertyRelative(
                TwoSideLayoutPropertyName(destinationSide, animationStart: false));
            SerializedProperty sourceStart = widget.FindPropertyRelative(
                TwoSideLayoutPropertyName(sourceSide, animationStart: true));
            SerializedProperty destinationStart = widget.FindPropertyRelative(
                TwoSideLayoutPropertyName(destinationSide, animationStart: true));
            if (sourceNormal == null || destinationNormal == null)
                continue;

            CopyLayout(sourceNormal, destinationNormal, mirror: true);
            CopyLayout(sourceStart, destinationStart, mirror: true);
            copied++;
        }

        return copied;
    }

    /// <summary>
    /// Copies every slot on Normal and Animation Start for one side from one
    /// two-side widget onto another. Targets, animation settings, and the
    /// other side stay unchanged. Copies Animation Start even when Animation
    /// Enabled is off.
    /// </summary>
    /// <returns>1 when copied, otherwise 0.</returns>
    public static int CopyWidgetSideAcrossTwoSideWidgets(
        SerializedProperty widgets,
        int sourceWidgetIndex,
        int destinationWidgetIndex,
        ResponsiveUiSide side)
    {
        if (!CanCopyWidgetSide(widgets, sourceWidgetIndex, destinationWidgetIndex))
            return 0;

        SerializedProperty source = widgets.GetArrayElementAtIndex(sourceWidgetIndex);
        SerializedProperty destination =
            widgets.GetArrayElementAtIndex(destinationWidgetIndex);
        CopyLayout(
            source.FindPropertyRelative(TwoSideLayoutPropertyName(side, false)),
            destination.FindPropertyRelative(TwoSideLayoutPropertyName(side, false)));
        CopyLayout(
            source.FindPropertyRelative(TwoSideLayoutPropertyName(side, true)),
            destination.FindPropertyRelative(TwoSideLayoutPropertyName(side, true)));
        return 1;
    }

    /// <summary>
    /// Swaps every slot on Normal and Animation Start for one side between two
    /// two-side widgets. Targets, animation settings, and the other side stay
    /// unchanged.
    /// </summary>
    /// <returns>2 when swapped, otherwise 0.</returns>
    public static int SwapWidgetSideAcrossTwoSideWidgets(
        SerializedProperty widgets,
        int sourceWidgetIndex,
        int destinationWidgetIndex,
        ResponsiveUiSide side)
    {
        if (!CanCopyWidgetSide(widgets, sourceWidgetIndex, destinationWidgetIndex))
            return 0;

        SerializedProperty source = widgets.GetArrayElementAtIndex(sourceWidgetIndex);
        SerializedProperty destination =
            widgets.GetArrayElementAtIndex(destinationWidgetIndex);
        SwapLayout(
            source.FindPropertyRelative(TwoSideLayoutPropertyName(side, false)),
            destination.FindPropertyRelative(TwoSideLayoutPropertyName(side, false)));
        SwapLayout(
            source.FindPropertyRelative(TwoSideLayoutPropertyName(side, true)),
            destination.FindPropertyRelative(TwoSideLayoutPropertyName(side, true)));
        return 2;
    }

    static bool CanCopyWidgetSide(
        SerializedProperty widgets,
        int sourceWidgetIndex,
        int destinationWidgetIndex) =>
        widgets != null &&
        widgets.isArray &&
        sourceWidgetIndex >= 0 &&
        destinationWidgetIndex >= 0 &&
        sourceWidgetIndex < widgets.arraySize &&
        destinationWidgetIndex < widgets.arraySize &&
        sourceWidgetIndex != destinationWidgetIndex;

    static void CopyLayout(
        SerializedProperty sourceLayout,
        SerializedProperty destinationLayout,
        bool mirror = false)
    {
        if (sourceLayout == null || destinationLayout == null)
            return;

        string[] states = ResponsiveWidgetLayout.StatePropertyNames;
        for (int i = 0; i < states.Length; i++)
        {
            ResponsiveRectState source =
                ReadState(sourceLayout.FindPropertyRelative(states[i]));
            WriteState(
                destinationLayout.FindPropertyRelative(states[i]),
                mirror ? MirrorHorizontally(source) : source);
        }
    }

    static void SwapLayout(
        SerializedProperty firstLayout,
        SerializedProperty secondLayout)
    {
        if (firstLayout == null || secondLayout == null)
            return;

        string[] states = ResponsiveWidgetLayout.StatePropertyNames;
        for (int i = 0; i < states.Length; i++)
        {
            SerializedProperty first = firstLayout.FindPropertyRelative(states[i]);
            SerializedProperty second = secondLayout.FindPropertyRelative(states[i]);
            ResponsiveRectState firstState = ReadState(first);
            WriteState(first, ReadState(second));
            WriteState(second, firstState);
        }
    }

    /// <summary>Two-side layout property for one side: Normal or Animation Start.</summary>
    public static string TwoSideLayoutPropertyName(ResponsiveUiSide side, bool animationStart)
    {
        bool left = side == ResponsiveUiSide.Left;
        return left
            ? (animationStart ? "leftAnimationStart" : "leftNormal")
            : (animationStart ? "rightAnimationStart" : "rightNormal");
    }

    /// <summary>
    /// The layout a two-side widget row shows / previews for <paramref name="side"/>:
    /// Animation Start only while Preview Animation Start is on and the row animates.
    /// </summary>
    public static string TwoSideLayoutPropertyName(
        SerializedProperty widget,
        ResponsiveUiSide side,
        bool previewAnimationStart)
    {
        bool animationStart = previewAnimationStart &&
            widget.FindPropertyRelative("animationEnabled").boolValue;
        return TwoSideLayoutPropertyName(side, animationStart);
    }

    public static void SeedTwoSideWidget(SerializedProperty widgetProp)
    {
        if (widgetProp == null)
            return;

        widgetProp.FindPropertyRelative("name").stringValue = string.Empty;
        widgetProp.FindPropertyRelative("animationEnabled").boolValue = true;
        widgetProp.FindPropertyRelative("duration").floatValue = 0.25f;
        widgetProp.FindPropertyRelative("delay").floatValue = 0f;
        widgetProp.FindPropertyRelative("curve").animationCurveValue =
            AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        widgetProp.FindPropertyRelative("initialized").boolValue = true;

        string[] layouts = ResponsiveTwoSideWidget.LayoutPropertyNames;
        for (int i = 0; i < layouts.Length; i++)
            ResetNewListRow(widgetProp.FindPropertyRelative(layouts[i]));
    }

    static bool CanCopySlot(
        SerializedProperty widgets,
        int sourceSlotIndex,
        int destinationSlotIndex,
        string[] states) =>
        widgets != null &&
        widgets.isArray &&
        sourceSlotIndex >= 0 &&
        sourceSlotIndex < states.Length &&
        destinationSlotIndex >= 0 &&
        destinationSlotIndex < states.Length &&
        sourceSlotIndex != destinationSlotIndex;

    static void CopySlotOnLayout(
        SerializedProperty layout,
        string sourceName,
        string destinationName)
    {
        ResponsiveRectState source =
            ReadState(layout.FindPropertyRelative(sourceName));
        WriteState(layout.FindPropertyRelative(destinationName), source);
    }

    public static bool IsScaleFromDesignCanvas(SerializedProperty stateProp)
    {
        if (stateProp == null)
            return false;
        SerializedProperty scaleOn = stateProp.FindPropertyRelative(ScaleFromDesignCanvasProp);
        return scaleOn != null && scaleOn.boolValue;
    }

    /// <summary>
    /// Compares live rect to stored design-space layout + expected localScale.
    /// Position is compared after × <paramref name="layoutRatio"/>.
    /// </summary>
    public static bool MatchesLayout(
        SerializedProperty stateProp,
        RectTransform rt,
        Vector2 layoutRatio,
        Vector2 expectedLocalScaleXy)
    {
        if (stateProp == null || rt == null)
            return false;

        return ReadState(stateProp).MatchesApplied(rt, layoutRatio, expectedLocalScaleXy);
    }

    #endregion

    #region Slot resolve

    /// <summary>
    /// Walks the Apply fallback chain (<see cref="ResponsiveWidgetSlotResolver.GetCandidate"/>)
    /// for <paramref name="layout"/> and returns the first slot <paramref name="match"/> accepts.
    /// Shared by widget, camera, and scroll slot resolve.
    /// </summary>
    public static bool FindFallbackChainSlot(
        ResponsiveDevice.LayoutKey layout,
        Func<string, bool> match,
        out string propertyName,
        out ResponsiveDevice.FormFactor form)
    {
        for (int i = 0; i < ResponsiveWidgetSlotResolver.MaxCandidates; i++)
        {
            if (!ResponsiveWidgetSlotResolver.GetCandidate(
                    layout, i, out form, out var orientation))
                continue;
            propertyName = ResponsiveWidgetLayout.StatePropertyName(form, orientation);
            if (match(propertyName))
                return true;
        }

        propertyName = null;
        form = layout.Form;
        return false;
    }

    /// <summary>
    /// Serialized-slot resolve matching <see cref="ResponsiveWidgetLayout.ResolveActiveSlot"/>:
    /// the first Enabled slot in the fallback chain. Works for widget, camera, and scroll
    /// layouts (every slot serializes <c>enabled</c>). When none is enabled, returns false
    /// with the exact layout-key slot.
    /// </summary>
    public static bool ResolveActiveSlot(
        SerializedProperty layoutProp,
        ResponsiveDevice.LayoutKey key,
        out string propertyName,
        out ResponsiveDevice.FormFactor sourceForm)
    {
        if (layoutProp != null &&
            FindFallbackChainSlot(
                key,
                name => IsEnabled(layoutProp.FindPropertyRelative(name)),
                out propertyName,
                out sourceForm))
            return true;

        propertyName = ResponsiveWidgetLayout.StatePropertyName(key);
        sourceForm = key.Form;
        return false;
    }

    /// <summary>
    /// Scene → slot capture target: <see cref="ResolveActiveSlot"/>, else the exact layout-key
    /// slot so Scene edits are not discarded while authoring disabled slots.
    /// </summary>
    public static bool ResolveCaptureSlot(
        SerializedProperty layoutProp,
        ResponsiveDevice.LayoutKey layout,
        out string stateName,
        out ResponsiveDevice.FormFactor slotForm) =>
        ResolveActiveSlot(layoutProp, layout, out stateName, out slotForm) ||
        (layoutProp != null && layoutProp.FindPropertyRelative(stateName) != null);

    /// <summary>
    /// True when <paramref name="statePropertyName"/> is the slot Apply/capture uses for
    /// <paramref name="layout"/> (including Landscape→Portrait / HalfOpened→Phone fallback).
    /// </summary>
    public static bool IsActiveEditorSlot(
        SerializedProperty widgetProp,
        ResponsiveDevice.LayoutKey layout,
        string statePropertyName)
    {
        if (widgetProp == null || string.IsNullOrEmpty(statePropertyName))
            return false;

        ResolveActiveSlot(widgetProp, layout, out string activeName, out _);
        return activeName == statePropertyName;
    }

    public static bool AnySlotUsesDesignScale(SerializedProperty widgets)
    {
        if (widgets == null || !widgets.isArray)
            return false;

        for (int i = 0; i < widgets.arraySize; i++)
        {
            if (LayoutUsesDesignScale(widgets.GetArrayElementAtIndex(i)))
                return true;
        }

        return false;
    }

    public static bool AnyTwoSideSlotUsesDesignScale(SerializedProperty widgets)
    {
        if (widgets == null || !widgets.isArray)
            return false;

        string[] layouts = ResponsiveTwoSideWidget.LayoutPropertyNames;
        for (int i = 0; i < widgets.arraySize; i++)
        {
            SerializedProperty widget = widgets.GetArrayElementAtIndex(i);
            for (int l = 0; l < layouts.Length; l++)
            {
                if (LayoutUsesDesignScale(widget.FindPropertyRelative(layouts[l])))
                    return true;
            }
        }

        return false;
    }

    static bool LayoutUsesDesignScale(SerializedProperty layout)
    {
        if (layout == null)
            return false;

        string[] states = ResponsiveWidgetLayout.StatePropertyNames;
        for (int s = 0; s < states.Length; s++)
        {
            if (IsScaleFromDesignCanvas(layout.FindPropertyRelative(states[s])))
                return true;
        }

        return false;
    }

    #endregion

    #region Slot enabled

    public static bool IsEnabled(SerializedProperty stateProp)
    {
        if (stateProp == null)
            return false;
        SerializedProperty enabled = stateProp.FindPropertyRelative(EnabledProp);
        return enabled != null && enabled.boolValue;
    }

    /// <summary>
    /// Fills unset (0,0) Default Scale from Project Settings / built-ins.
    /// Leaves Design Canvas Size (0,0) so the slot keeps following catalog / player builtins.
    /// </summary>
    public static void EnsureDesignScaleDefaults(SerializedProperty stateProp, int slotIndex)
    {
        if (stateProp == null)
            return;

        ResponsiveDevice.FormFactor slotForm = ResponsiveWidgetLayout.SlotFormFactor(slotIndex);

        SerializedProperty scaleProp = stateProp.FindPropertyRelative(DefaultScaleProp);
        if (scaleProp != null &&
            ResponsiveRectState.IsUnsetDefaultScale(scaleProp.vector2Value))
            scaleProp.vector2Value = ResponsiveRectState.GetDefaultScale(slotForm);
    }

    /// <summary>
    /// New / enabled slots only: an unset Design Canvas Size takes the Project Settings Landscape
    /// row when that row is set (<see cref="ResponsiveUiEditorSettings.GetDesignCanvasSize(ResponsiveDevice.FormFactor, ResponsiveDevice.Orientation)"/>).
    /// Otherwise it stays (0,0) and keeps following the per-form default, as before.
    /// </summary>
    public static bool SeedDesignCanvasFromPoseRow(SerializedProperty stateProp, int slotIndex)
    {
        SerializedProperty size = stateProp?.FindPropertyRelative(DesignCanvasSizeProp);
        if (size == null || !ResponsiveRectState.IsUnsetDesignCanvasSize(size.vector2Value))
            return false;

        ResponsiveDevice.FormFactor form = ResponsiveWidgetLayout.SlotFormFactor(slotIndex);
        ResponsiveDevice.Orientation orientation = ResponsiveWidgetLayout.SlotOrientation(slotIndex);
        if (orientation != ResponsiveDevice.Orientation.Landscape ||
            ResponsiveRectState.IsUnsetDesignCanvasSize(
                ResponsiveUiEditorSettings.GetFormDefaults(form).landscapeDesignCanvasSize))
            return false;

        size.vector2Value = ResponsiveUiEditorSettings.GetDesignCanvasSize(form, orientation);
        return true;
    }

    public static SerializedProperty FindSibling(SerializedProperty stateProp, string slotName)
    {
        if (stateProp == null || string.IsNullOrEmpty(slotName))
            return null;

        string path = stateProp.propertyPath;
        int dot = path.LastIndexOf('.');
        SerializedObject so = stateProp.serializedObject;
        if (dot < 0)
            return so.FindProperty(slotName);

        SerializedProperty parent = so.FindProperty(path.Substring(0, dot));
        return parent != null
            ? parent.FindPropertyRelative(slotName)
            : so.FindProperty(path.Substring(0, dot + 1) + slotName);
    }

    /// <summary>
    /// Seed source for a slot that turns Enabled: the first other-form candidate in the Editor
    /// seed chain (<see cref="ResponsiveWidgetSlotResolver.GetSeedCandidate"/>) that is enabled,
    /// or a Phone slot (always a usable source). Cover / Pad / Fold / Flip copy Phone in the
    /// same orientation. iPhone Duo Seated and Wide Screen copy Pad by screen shape (same, then
    /// other), then Phone. For Wide Screen this is an authoring-only copy (at runtime it falls
    /// back only to its other orientation).
    /// Null for Phone slots (they are sources, not destinations).
    /// </summary>
    public static SerializedProperty FindFallbackSource(SerializedProperty stateProp)
    {
        if (stateProp == null)
            return null;

        int slotIndex = ResponsiveWidgetLayout.StatePropertyIndex(stateProp.name);
        if (ResponsiveWidgetLayout.SlotFormFactor(slotIndex) == ResponsiveDevice.FormFactor.Phone)
            return null;

        var key = new ResponsiveDevice.LayoutKey(
            ResponsiveWidgetLayout.SlotFormFactor(slotIndex),
            ResponsiveWidgetLayout.SlotOrientation(slotIndex));
        for (int i = 1; i < ResponsiveWidgetSlotResolver.MaxCandidates; i++)
        {
            if (!ResponsiveWidgetSlotResolver.GetSeedCandidate(
                    key, i, out var form, out var orientation) ||
                form == key.Form)
                continue;

            SerializedProperty candidate = FindSibling(
                stateProp, ResponsiveWidgetLayout.StatePropertyName(form, orientation));
            if (candidate == null ||
                candidate.propertyPath == stateProp.propertyPath)
                continue;
            if (form == ResponsiveDevice.FormFactor.Phone || IsEnabled(candidate))
                return candidate;
        }

        return null;
    }

    /// <summary>
    /// When Enabled turns on, copy <see cref="FindFallbackSource"/>. Design Canvas Size stays
    /// this slot's form default (or its set Landscape pose row, <see cref="SeedDesignCanvasFromPoseRow"/>). A Wide Screen slot seeded from Pad also copies the hub's Pad
    /// Canvas Scaler refs while its Wide Screen refs are still the built-in default
    /// (<see cref="ResponsiveUiHubEditorUtil.CopyPadScalerRefsToWideScreen"/>).
    /// </summary>
    public static bool CopyFallback(SerializedProperty stateProp)
    {
        SerializedProperty source = FindFallbackSource(stateProp);
        if (source == null)
            return false;

        ResponsiveRectState copied = ReadState(source);
        copied.enabled = IsEnabled(stateProp);
        copied.designCanvasSize = Vector2.zero;
        WriteState(stateProp, copied);
        SeedDesignCanvasFromPoseRow(
            stateProp, ResponsiveWidgetLayout.StatePropertyIndex(stateProp.name));
        if (ResponsiveWidgetLayout.SlotFormFactor(stateProp.name) == ResponsiveDevice.FormFactor.WideScreen &&
            ResponsiveWidgetLayout.SlotFormFactor(source.name) == ResponsiveDevice.FormFactor.Pad)
            ResponsiveUiHubEditorUtil.CopyPadScalerRefsToWideScreen(stateProp.serializedObject);
        return true;
    }

    /// <summary>
    /// Survives <c>Editor.OnEnable</c> after Unity list <c>+</c> rebuilds the Inspector.
    /// Instance <c>lastCount</c> is <c>-1</c> on that rebuild and would otherwise treat
    /// Unity's duplicated row as already-authored.
    /// </summary>
    static readonly Dictionary<string, int> ListSizeCache = new Dictionary<string, int>();

    /// <summary>
    /// Unity list <c>+</c> grows the array on MouseDown, when foldout mutation is blocked.
    /// Keep the last seen size across Inspector events and seed on the next Layout/Repaint.
    /// </summary>
    public static bool GetNewRows(
        SerializedProperty list,
        ref int lastCount,
        out int startInclusive,
        out int endExclusive) =>
        GetNewRows(
            list,
            ref lastCount,
            out startInclusive,
            out endExclusive,
            ResponsiveUiDetectionDrawer.CanMutateInspectorLayout());

    /// <param name="canMutate">
    /// Inspector Layout/Repaint, or after the list has already drawn. MouseDown growth
    /// before the list draws is deferred so IMGUI control heights stay stable.
    /// </param>
    public static bool GetNewRows(
        SerializedProperty list,
        ref int lastCount,
        out int startInclusive,
        out int endExclusive,
        bool canMutate)
    {
        startInclusive = 0;
        endExclusive = 0;
        if (list == null || !list.isArray)
            return false;

        int current = list.arraySize;
        int remembered = RememberedListSize(list, lastCount);
        if (remembered < 0)
        {
            StoreListSize(list, ref lastCount, current);
            return false;
        }

        if (current <= remembered)
        {
            StoreListSize(list, ref lastCount, current);
            return false;
        }

        if (!canMutate)
            return false;

        startInclusive = remembered;
        endExclusive = current;
        lastCount = current;
        return true;
    }

    /// <summary>
    /// Unity list <c>+</c> duplicates the previous row. Call from Inspector Layout/Repaint
    /// (including before the list is drawn) so new elements start empty.
    /// </summary>
    public static bool ForEachNewRow(
        SerializedProperty list,
        ref int lastCount,
        Action<SerializedProperty> resetRow) =>
        ForEachNewRow(
            list,
            ref lastCount,
            resetRow,
            ResponsiveUiDetectionDrawer.CanMutateInspectorLayout());

    /// <param name="canMutate">
    /// Pass true after the list <c>PropertyField</c> so MouseDown <c>+</c> is cleared
    /// in the same Inspector pass.
    /// </param>
    public static bool ForEachNewRow(
        SerializedProperty list,
        ref int lastCount,
        Action<SerializedProperty> resetRow,
        bool canMutate)
    {
        if (!GetNewRows(list, ref lastCount, out int start, out int end, canMutate))
            return false;

        for (int i = start; i < end; i++)
            resetRow(list.GetArrayElementAtIndex(i));
        // Keep ListSizeCache on the pre-add size until the next equal-size
        // Inspector pass (after ApplyModifiedProperties). If the Editor rebuilds
        // before Apply, OnEnable lastCount=-1 still sees growth and clears again.
        lastCount = list.arraySize;
        return true;
    }

    static int RememberedListSize(SerializedProperty list, int lastCount)
    {
        if (lastCount >= 0)
            return lastCount;
        return ListSizeCache.TryGetValue(ListSizeKey(list), out int cached) ? cached : -1;
    }

    static void StoreListSize(SerializedProperty list, ref int lastCount, int size)
    {
        lastCount = size;
        ListSizeCache[ListSizeKey(list)] = size;
    }

    static string ListSizeKey(SerializedProperty list)
    {
        UnityEngine.Object host = list.serializedObject.targetObject;
        int id = host != null ? host.GetInstanceID() : 0;
        return id.ToString() + "\n" + list.propertyPath;
    }

    /// <summary>
    /// Applies Enabled defaults from Player Allowed Orientations to a new widget element,
    /// and ensures each slot has Design Canvas Size / Default Scale defaults.
    /// New widgets start with Scale From Design Canvas on. Allowed axes start Enabled
    /// except Flip Cover Landscape, which stays opt-in.
    /// Existing FoldPhoneCover / FlipPhoneCover / HalfOpened keep authored Enabled
    /// (empty Cover must not auto-enable and apply zero layouts on device).
    /// </summary>
    public static void SeedSlots(SerializedProperty widgetProp) =>
        SyncSlotEnabled(widgetProp, seedNewWidget: true);

    /// <summary>
    /// Keeps slot Enabled in sync with Player Allowed Orientations:
    /// disallowed axis → Disabled; Phone/Pad allowed → Enabled;
    /// FoldPhoneCover / FlipPhoneCover / Fold/Flip HalfOpened stay authored except when
    /// <paramref name="seedNewWidget"/>. Flip Cover Landscape starts Disabled on new rows.
    /// A slot that turns Enabled copies its fallback source through
    /// <paramref name="copyFallback"/> (widget <see cref="CopyFallback"/> when null;
    /// cameras pass <see cref="ResponsiveCameraEditorUtil.CopyFallback"/>).
    /// Returns true when any serialized slot value changed.
    /// </summary>
    public static bool SyncSlotEnabled(
        SerializedProperty widgetProp,
        bool seedNewWidget = false,
        Func<SerializedProperty, bool> copyFallback = null)
    {
        if (widgetProp == null)
            return false;

        copyFallback ??= CopyFallback;

        string[] states = ResponsiveWidgetLayout.StatePropertyNames;
        bool changed = false;
        for (int i = 0; i < states.Length; i++)
        {
            SerializedProperty slot = widgetProp.FindPropertyRelative(states[i]);
            if (slot == null)
                continue;

            if (seedNewWidget)
            {
                EnsureDesignScaleDefaults(slot, i);
                changed |= SeedDesignCanvasFromPoseRow(slot, i);
                SerializedProperty scaleProp =
                    slot.FindPropertyRelative(ScaleFromDesignCanvasProp);
                if (!scaleProp.boolValue)
                {
                    scaleProp.boolValue = true;
                    changed = true;
                }
            }

            SerializedProperty enabledProp = slot.FindPropertyRelative(EnabledProp);
            if (enabledProp == null)
                continue;

            bool allowed = ResponsiveUiEditorSettings.SlotAllowed(i);
            bool targetEnabled;
            if (ResponsiveWidgetLayout.IsSeedDisabledSlot(i))
            {
                if (!seedNewWidget)
                    continue;
                targetEnabled = false;
            }
            else if (ResponsiveWidgetLayout.IsOptInSlot(i))
            {
                // New widgets enable allowed Cover/Fold slots. Existing opt-in slots stay
                // authored so empty serialized slots do not Apply zeros on device.
                if (!seedNewWidget)
                    continue;
                targetEnabled = allowed;
            }
            else if (!allowed)
            {
                targetEnabled = false;
            }
            else
            {
                targetEnabled = true;
            }

            bool wasEnabled = enabledProp.boolValue;
            if (wasEnabled != targetEnabled)
            {
                enabledProp.boolValue = targetEnabled;
                changed = true;
            }

            if (targetEnabled && (seedNewWidget || !wasEnabled))
                changed |= copyFallback(slot);
        }

        return changed;
    }

    /// <summary>
    /// Syncs every Widgets element on a <see cref="ResponsiveUI"/> SerializedObject.
    /// </summary>
    public static bool SyncWidgets(SerializedObject so) =>
        SyncList(so, "widgets", CopyFallback);

    public static bool SyncTwoSideWidgets(SerializedObject so)
    {
        SerializedProperty list = so?.FindProperty("widgets");
        if (list == null || !list.isArray)
            return false;

        string[] layouts = ResponsiveTwoSideWidget.LayoutPropertyNames;
        bool changed = false;
        for (int i = 0; i < list.arraySize; i++)
        {
            SerializedProperty widget = list.GetArrayElementAtIndex(i);
            for (int l = 0; l < layouts.Length; l++)
                changed |= SyncSlotEnabled(widget.FindPropertyRelative(layouts[l]));
        }

        if (changed)
            so.ApplyModifiedProperties();
        return changed;
    }

    /// <summary>
    /// Syncs every Cameras element on a <see cref="ResponsiveCamera"/> SerializedObject.
    /// </summary>
    public static bool SyncCameras(SerializedObject so) =>
        SyncList(so, "cameras", ResponsiveCameraEditorUtil.CopyFallback);

    static bool SyncList(
        SerializedObject so,
        string listPropertyName,
        Func<SerializedProperty, bool> copyFallback)
    {
        if (so == null || string.IsNullOrEmpty(listPropertyName))
            return false;

        SerializedProperty list = so.FindProperty(listPropertyName);
        if (list == null || !list.isArray)
            return false;

        bool changed = false;
        for (int i = 0; i < list.arraySize; i++)
            changed |= SyncSlotEnabled(
                list.GetArrayElementAtIndex(i), copyFallback: copyFallback);

        if (changed)
            so.ApplyModifiedProperties();
        return changed;
    }

    /// <summary>
    /// Syncs all loaded scene controllers and cameras to Player Allowed Orientations.
    /// </summary>
    public static void SyncLoaded()
    {
        ResponsiveUiController[] controllers = FindLoadedControllers();
        for (int i = 0; i < controllers.Length; i++)
        {
            ResponsiveUiController controller = controllers[i];
            if (controller == null || !controller.gameObject.scene.IsValid())
                continue;

            var so = new SerializedObject(controller);
            if (SyncWidgets(so))
                EditorUtility.SetDirty(controller);
        }

        ResponsiveCamera[] cameras = ResponsiveCameraEditorUtil.FindLoaded();
        for (int i = 0; i < cameras.Length; i++)
        {
            ResponsiveCamera camera = cameras[i];
            if (camera == null || !camera.gameObject.scene.IsValid())
                continue;

            var so = new SerializedObject(camera);
            if (SyncCameras(so))
                EditorUtility.SetDirty(camera);
        }

        ResponsiveTwoSideUiTweenController[] twoSides = FindLoadedTwoSideControllers();
        for (int i = 0; i < twoSides.Length; i++)
        {
            ResponsiveTwoSideUiTweenController twoSide = twoSides[i];
            if (twoSide == null || !twoSide.gameObject.scene.IsValid())
                continue;

            var so = new SerializedObject(twoSide);
            if (SyncTwoSideWidgets(so))
                EditorUtility.SetDirty(twoSide);
        }
    }

    public static ResponsiveUI[] FindLoaded()
    {
        return UnityEngine.Object.FindObjectsByType<ResponsiveUI>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
    }

    public static ResponsiveUiController[] FindLoadedControllers()
    {
        return UnityEngine.Object.FindObjectsByType<ResponsiveUiController>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
    }

    public static ResponsiveTwoSideUiTweenController[] FindLoadedTwoSideControllers()
    {
        return UnityEngine.Object.FindObjectsByType<ResponsiveTwoSideUiTweenController>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
    }

    #endregion

    #region Capture / LivePush

    /// <summary>Writes layout vectors + origin Default Scale (from live capture).</summary>
    public static void WriteCapturedLive(
        SerializedProperty stateProp,
        ResponsiveRectState captured)
    {
        if (stateProp == null)
            return;

        WriteLayout(stateProp, captured);
        SerializedProperty defaultScale = stateProp.FindPropertyRelative(DefaultScaleProp);
        if (defaultScale != null)
            defaultScale.vector2Value = captured.defaultScale;
        SerializedProperty canvasSize = stateProp.FindPropertyRelative(DesignCanvasSizeProp);
        if (canvasSize != null)
            canvasSize.vector2Value = Vector2.zero;
    }

    /// <summary>
    /// Scene / assign capture for a widget slot or camera Focus Bounds layout.
    /// Stores design-space Left/Right/Top/Bottom and origin Default Scale using
    /// the same <see cref="ResponsiveUI.GetLayoutRatio"/> Apply uses. Keeps
    /// serialized (0,0) Design Canvas Size. No-op when the live rect already matches
    /// unless <paramref name="skipIfMatches"/> is false (Design Scale toggle).
    /// <paramref name="slotIndex"/> is Phone/Pad/... (not a nested property name).
    /// When Hub is null, <paramref name="currentUiSize"/> is the Apply fallback size.
    /// </summary>
    public static bool WriteCapturedLive(
        SerializedProperty stateProp,
        RectTransform rt,
        ResponsiveUI hub,
        int slotIndex,
        Vector2 currentUiSize = default,
        bool skipIfMatches = true)
    {
        if (stateProp == null || rt == null)
            return false;

        EnsureDesignScaleDefaults(stateProp, slotIndex);
        ResponsiveDevice.FormFactor form = ResponsiveWidgetLayout.SlotFormFactor(slotIndex);
        ResponsiveDevice.Orientation orientation =
            ResponsiveWidgetLayout.SlotOrientation(slotIndex);
        ResponsiveRectState existing = ReadState(stateProp);
        Vector2 identityLive = ResponsiveDesignScale.ResolveIdentityLiveSize(
            currentUiSize,
            form,
            orientation,
            ResponsiveUI.OrientSizeForSlot(
                existing.GetResolvedDesignCanvasSize(form), form, orientation),
            rt);
        Vector2 layoutRatio = hub != null
            ? hub.GetLayoutRatio(existing, form, rt, orientation)
            : existing.ComputeLayoutRatio(identityLive, form, orientation);
        Vector2 expectedLocal = hub != null
            ? hub.GetAppliedWidgetScale(existing, form, layoutRatio)
            : existing.ComputeAppliedLocalScale(identityLive, form, orientation);

        if (skipIfMatches &&
            existing.enabled &&
            MatchesLayout(stateProp, rt, layoutRatio, expectedLocal))
            return false;

        ResponsiveRectState captured = ResponsiveRectState.From(rt, layoutRatio, form);
        captured.CopyDesignScaleSettingsFrom(existing);
        WriteCapturedLive(stateProp, captured);
        return true;
    }

    /// <summary>
    /// Seeds layout from the live rect.
    /// First Target assign: copy live pixels into every slot and set Enabled from
    /// Player Allowed Orientations. The active Simulator / override slot is unscaled
    /// with the live canvas ratio so Apply keeps authored localScale (including when
    /// the new row had Enabled off). Other slots stay ratio 1.
    /// Retarget: write only the active slot.
    /// </summary>
    public static void WriteCaptureToSlots(
        SerializedProperty widgetProp,
        RectTransform rt,
        ResponsiveUI ui,
        bool applyEnabledDefaults)
    {
        if (widgetProp == null || rt == null)
            return;

        if (!applyEnabledDefaults)
        {
            WriteCaptureToActiveSlot(widgetProp, rt, ui);
            return;
        }

        string[] states = ResponsiveWidgetLayout.StatePropertyNames;
        string activeName = null;
        ResponsiveDevice.FormFactor activeSourceForm = ResponsiveDevice.FormFactor.Phone;
        if (ui != null)
        {
            ResponsiveDevice.LayoutKey layout =
                ResponsiveUiDetectionDrawer.ResolveInspectorLayout(widgetProp.serializedObject, ui);
            // New array rows start with Enabled off; resolve then falls back to the exact
            // layout-key slot, so the slot Apply will use is always unscaled (same as retarget).
            ResolveActiveSlot(widgetProp, layout, out activeName, out activeSourceForm);
        }

        for (int i = 0; i < states.Length; i++)
        {
            SerializedProperty slot = widgetProp.FindPropertyRelative(states[i]);
            EnsureDesignScaleDefaults(slot, i);

            ResponsiveDevice.FormFactor slotForm = ResponsiveWidgetLayout.SlotFormFactor(i);
            ResponsiveRectState existing = ReadState(slot);
            // Active slot: unscale with the live canvas ratio so Apply keeps the authored
            // localScale (duplicating a Design Scale widget used to jump scale on enable).
            // Other slots stay ratio 1 so they are not unscaled against today's canvas.
            Vector2 ratio = Vector2.one;
            if (ui != null && states[i] == activeName)
            {
                ratio = ui.GetLayoutRatio(
                    existing,
                    activeSourceForm,
                    rt,
                    ResponsiveWidgetLayout.SlotOrientation(activeName));
            }
            ResponsiveRectState captured = ResponsiveRectState.From(rt, ratio, slotForm);
            captured.CopyDesignScaleSettingsFrom(existing);
            captured.enabled =
                ResponsiveUiEditorSettings.SlotAllowed(i) &&
                !ResponsiveWidgetLayout.IsSeedDisabledSlot(i);
            WriteState(slot, captured);
        }
    }

    /// <summary>
    /// Re-reads the live Target into <paramref name="stateProp"/> using that slot's
    /// current Design Scale ratio so toggling the feature does not move the object.
    /// </summary>
    public static void RecaptureSlotFromLive(
        SerializedProperty stateProp,
        RectTransform rt,
        ResponsiveUI ui)
    {
        if (stateProp == null || rt == null)
            return;

        int slotIndex = ResponsiveWidgetLayout.StatePropertyIndex(stateProp.name);
        EnsureDesignScaleDefaults(stateProp, slotIndex);

        ResponsiveDevice.FormFactor slotForm = ResponsiveWidgetLayout.SlotFormFactor(slotIndex);
        ResponsiveRectState existing = ReadState(stateProp);
        Vector2 layoutRatio = ui != null
            ? ui.GetLayoutRatio(
                existing,
                slotForm,
                rt,
                ResponsiveWidgetLayout.SlotOrientation(slotIndex))
            : Vector2.one;
        ResponsiveRectState captured = ResponsiveRectState.From(rt, layoutRatio, slotForm);
        captured.CopyDesignScaleSettingsFrom(existing);
        WriteCapturedLive(stateProp, captured);
    }

    static void WriteCaptureToActiveSlot(
        SerializedProperty widgetProp,
        RectTransform rt,
        ResponsiveUI ui)
    {
        ResponsiveDevice.LayoutKey layout = ui != null
            ? ResponsiveUiDetectionDrawer.ResolveInspectorLayout(widgetProp.serializedObject, ui)
            : default;
        ResolveActiveSlot(
            widgetProp,
            layout,
            out string activeName,
            out ResponsiveDevice.FormFactor sourceForm);

        SerializedProperty slot = widgetProp.FindPropertyRelative(activeName);
        if (slot == null)
            return;

        int slotIndex = ResponsiveWidgetLayout.StatePropertyIndex(activeName);
        EnsureDesignScaleDefaults(slot, slotIndex);

        ResponsiveRectState existing = ReadState(slot);
        Vector2 layoutRatio = ui != null
            ? ui.GetLayoutRatio(
                existing,
                sourceForm,
                rt,
                ResponsiveWidgetLayout.SlotOrientation(slotIndex))
            : Vector2.one;
        ResponsiveRectState captured = ResponsiveRectState.From(rt, layoutRatio, sourceForm);
        captured.CopyDesignScaleSettingsFrom(existing);
        WriteCapturedLive(slot, captured);
    }

    #endregion
}
}

#endif
