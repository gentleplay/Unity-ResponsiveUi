#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

using FDPlus.ResponsiveUI;

namespace FDPlus.ResponsiveUI.Editor
{
/// <summary>
/// Shared Inspector block for <see cref="ResponsiveUiHandler"/> Detection (Editor).
/// When Override is off, shows live Device Simulator / Screen form + orientation
/// (the serialized Editor Form Factor / Orientation fields are override-only).
/// </summary>
public static class ResponsiveUiDetectionDrawer
{
    #region Draw

    public static void Draw(SerializedObject so, ResponsiveUiHandler handler)
    {
        if (so == null)
            return;

        SerializedProperty overrideProp = so.FindProperty("overrideInEditor");
        SerializedProperty formProp = so.FindProperty("editorFormFactor");
        SerializedProperty orientProp = so.FindProperty("editorOrientation");
        SerializedProperty padMaxProp = so.FindProperty("editorPadMaxAspect");
        SerializedProperty hideOtherProp = so.FindProperty("hideOtherSlots");
        if (overrideProp == null || formProp == null || orientProp == null || padMaxProp == null)
            return;

        EditorGUILayout.PropertyField(overrideProp, new GUIContent("Override In Editor"));

        bool useOverride = overrideProp.boolValue;
        if (useOverride)
        {
            DrawFormFactorPopup(
                new GUIContent(
                    "Editor Form Factor",
                    "Form laid out while Override In Editor is on (the build target's forms). " +
                    "A selected Device Simulator profile drives detection when Override is off, " +
                    "on any build target. A stored form of another platform shows and applies as " +
                    "its build-target equivalent."),
                formProp);
            EditorGUILayout.PropertyField(orientProp, new GUIContent("Editor Orientation"));
        }
        else
        {
            ResponsiveDevice.LayoutKey live = ResolveLiveLayout(handler);
            using (new EditorGUI.DisabledScope(true))
            {
                DrawFormFactorPopup(
                    new GUIContent(
                        "Detected Form Factor",
                        "Live Device Simulator / Screen classification. " +
                        "A selected Device Simulator profile uses that device's slots on any build target. " +
                        "With no profile: Android Phone / Pad / Fold / Flip; " +
                        "iOS Phone / Pad / iPhone Duo Cover / Seated; " +
                        "Windows / macOS / Linux / WebGL Wide Screen only, sized from the window. " +
                        "Enable Override In Editor to lock a form factor."),
                    live.Form);
                EditorGUILayout.EnumPopup(
                    new GUIContent(
                        "Detected Orientation",
                        "Live Device Simulator / Screen orientation. " +
                        "Enable Override In Editor to lock Portrait or Landscape."),
                    live.Orientation);
            }
        }

        EditorGUILayout.PropertyField(
            padMaxProp,
            new GUIContent(
                "Editor Pad Max Aspect",
                "Game View / Simulator aspect (max/min) below this counts as Pad when " +
                "no Foldable/Simulator pin applies. Fold8 Cover (~1.58) is Fold Cover; " +
                "Flip Cover (~1.10) is Flip Cover — neither is Pad by aspect."));

        if (hideOtherProp != null)
        {
            bool showOthers = !hideOtherProp.boolValue;
            EditorGUI.BeginChangeCheck();
            showOthers = EditorGUILayout.Toggle(
                new GUIContent(
                    "Show Other Slots",
                    "On: list " + ResponsiveUiEditorSettings.VisibleFormDisplayNameList +
                    " so you can expand them to edit. " +
                    "Auto-expand opens the detected slot and re-collapses the others when Simulator / product state changes. " +
                    "Off: hide the other form foldouts and show only the detected slot."),
                showOthers);
            if (EditorGUI.EndChangeCheck())
                hideOtherProp.boolValue = !showOthers;
        }
    }

    public static ResponsiveUiHandler HandlerOf(SerializedObject so)
    {
        if (so?.targetObject is ResponsiveUiHandler handler)
            return handler;
        if (so?.targetObject is IResponsiveUiClient client)
            return client.Hub;
        return null;
    }

    public static SerializedObject HubSerializedObject(SerializedObject so)
    {
        ResponsiveUiHandler handler = HandlerOf(so);
        if (handler == null)
            return so;
        if (so != null && so.targetObject == handler)
            return so;
        return new SerializedObject(handler);
    }

    public static string SectionTitle(SerializedObject so, ResponsiveUiHandler handler)
    {
        ResponsiveDevice.LayoutKey live = ResolveInspectorLayout(so, handler);
        return "Detection (Editor)  — " +
            ResponsiveDevice.GetFormDisplayName(live.Form) + " / " + live.Orientation;
    }

    /// <summary>
    /// When true (default), inactive form-factor foldouts stay listed.
    /// When false, Inspector draws only the detected / override slot.
    /// </summary>
    public static bool ShowOtherSlots(SerializedObject so)
    {
        // Hub Inspector: read its own SerializedObject so unapplied edits count.
        if (so?.targetObject is ResponsiveUiHandler)
        {
            SerializedProperty hide = so.FindProperty("hideOtherSlots");
            return hide == null || !hide.boolValue;
        }

        // Client Inspectors call this for every slot on every GUI event; read the
        // hub field instead of building a SerializedObject each time.
        ResponsiveUiHandler handler = HandlerOf(so);
        return handler == null || !handler.EditorHideOtherSlots;
    }

    public static bool ShouldDrawState(SerializedObject so, string stateName)
    {
        if (string.IsNullOrEmpty(stateName) ||
            !ResponsiveUiEditorSettings.SlotVisible(stateName))
            return false;

        if (ShowOtherSlots(so))
            return true;

        ResponsiveUiHandler handler = HandlerOf(so);
        return stateName == ResolveFoldoutSlotName(so, handler);
    }

    #endregion

    #region Helpers

    static readonly ResponsiveDevice.FormFactor[] FormFactorValues =
        ResponsiveWidgetLayout.FormFactors;

    static void DrawFormFactorPopup(GUIContent label, SerializedProperty formProp)
    {
        // Show what Override applies (ResponsiveUiHandler.EditorFormFactor); the stored value
        // changes only when a form is picked.
        var current = ResponsivePlatforms.ConstrainDetected(
            (ResponsiveDevice.FormFactor)formProp.intValue);
        var next = DrawFormFactorPopup(label, current);
        if (next != current)
            formProp.intValue = (int)next;
    }

    static ResponsiveDevice.FormFactor DrawFormFactorPopup(
        GUIContent label,
        ResponsiveDevice.FormFactor current)
    {
        // The build target's forms only (plus the current value, so it never jumps).
        var forms = new List<ResponsiveDevice.FormFactor>();
        var labels = new List<string>();
        int selected = 0;
        for (int i = 0; i < FormFactorValues.Length; i++)
        {
            ResponsiveDevice.FormFactor form = FormFactorValues[i];
            // Override stays on the build target. The detected value is always listed, including
            // a Simulator profile from another platform.
            bool onTarget = !ResponsivePlatforms.ConstrainsForms ||
                            ResponsivePlatforms.IsActiveForm(form);
            if (form != current && !onTarget)
                continue;
            if (form == current)
                selected = forms.Count;
            forms.Add(form);
            labels.Add(
                ResponsiveWidgetLayout.SlotGroupLabel(form) + " / " +
                ResponsiveDevice.GetFormDisplayName(form));
        }

        int next = EditorGUILayout.Popup(label, selected, labels.ToArray());
        return forms[next];
    }

    /// <summary>
    /// Override In Editor when on; otherwise live Simulator / Play Mode layout.
    /// A null <paramref name="handler"/> uses the hub of <paramref name="so"/>.
    /// </summary>
    public static ResponsiveDevice.LayoutKey ResolveInspectorLayout(
        SerializedObject so,
        ResponsiveUiHandler handler = null)
    {
        ResponsiveUiHandler owner = HandlerOf(so);
        bool overrideOn;
        if (so?.targetObject is ResponsiveUiHandler)
        {
            // Hub Inspector: read its own SerializedObject so unapplied edits count.
            SerializedProperty overrideProp = so.FindProperty("overrideInEditor");
            overrideOn = overrideProp != null && overrideProp.boolValue;
        }
        else
        {
            overrideOn = owner != null && owner.EditorOverrideInEditor;
        }

        handler ??= owner;
        if (overrideOn && handler != null)
            return handler.GetEditorCaptureLayout();

        return ResolveLiveLayout(handler);
    }

    /// <summary>
    /// State property to expand (detected form × orientation). Opt-in FoldPhoneCover /
    /// HalfOpened slots expand even when disabled so they can be authored.
    /// </summary>
    public static string ResolveFoldoutSlotName(
        SerializedObject so,
        ResponsiveUiHandler handler = null)
    {
        ResponsiveDevice.LayoutKey layout = ResolveInspectorLayout(so, handler);
        string slot = ResponsiveUiSimulatorSlotFocus.ResolveFoldoutSlotName(layout);
        return string.IsNullOrEmpty(slot)
            ? ResponsiveWidgetLayout.StatePropertyName(layout)
            : slot;
    }

    static string _foldoutSlot;
    static readonly HashSet<string> _syncedLayoutKeys = new HashSet<string>();

    /// <summary>
    /// Safe to expand/collapse SerializedProperty foldouts (not during MouseDown).
    /// </summary>
    public static bool CanMutateInspectorLayout()
    {
        Event e = Event.current;
        if (e == null)
            return true;
        return e.type == EventType.Layout || e.type == EventType.Repaint;
    }

    /// <summary>
    /// Auto-expand the detected slot. Collapse the others only when the detected
    /// form changes, per layout path so widget / camera / scroll arrays all follow
    /// Simulator. Do not key generation on the inspected host — Canvas Inspectors
    /// draw several clients in one frame, and a host switch must not re-collapse
    /// siblings the user just opened. Show Other Slots is visibility only —
    /// listed siblings stay user-expandable.
    /// </summary>
    public static void ApplyLayoutFoldouts(SerializedProperty layoutProp, string foldoutSlot)
    {
        if (layoutProp == null || string.IsNullOrEmpty(foldoutSlot))
            return;

        UnityEngine.Object host = layoutProp.serializedObject.targetObject;
        int hostId = host != null ? host.GetInstanceID() : 0;
        bool canMutate = CanMutateInspectorLayout();
        bool generationChanged = foldoutSlot != _foldoutSlot;
        if (generationChanged && canMutate)
        {
            _foldoutSlot = foldoutSlot;
            _syncedLayoutKeys.Clear();
        }

        string layoutKey = hostId.ToString() + "\n" + layoutProp.propertyPath;
        bool collapseOthers = canMutate && !_syncedLayoutKeys.Contains(layoutKey);
        if (collapseOthers)
            _syncedLayoutKeys.Add(layoutKey);

        string[] names = ResponsiveWidgetLayout.StatePropertyNames;
        for (int i = 0; i < names.Length; i++)
        {
            SerializedProperty state = layoutProp.FindPropertyRelative(names[i]);
            if (state == null)
                continue;

            bool isDetected = state.name == foldoutSlot;
            if (isDetected)
                state.isExpanded = true;
            else if (collapseOthers)
                state.isExpanded = false;
        }
    }

    /// <summary>
    /// <see cref="ApplyLayoutFoldouts"/> for the Inspector's detected slot. Shared by the
    /// widget, camera, and scroll layout drawers.
    /// </summary>
    public static void ApplyDetectedFoldouts(SerializedProperty layoutProp)
    {
        if (layoutProp == null)
            return;
        ApplyLayoutFoldouts(layoutProp, ResolveFoldoutSlotName(layoutProp.serializedObject));
    }

    public static void ApplyListFoldouts(SerializedProperty list, ResponsiveUiHandler handler)
    {
        if (list == null || !list.isArray)
            return;

        handler ??= HandlerOf(list.serializedObject);
        string slot = ResolveFoldoutSlotName(list.serializedObject, handler);
        for (int i = 0; i < list.arraySize; i++)
            ApplyLayoutFoldouts(list.GetArrayElementAtIndex(i), slot);
    }

    public static bool ShouldExpandState(SerializedProperty stateProp)
    {
        if (stateProp == null)
            return false;
        return ShouldExpandState(
            stateProp,
            ResolveFoldoutSlotName(stateProp.serializedObject));
    }

    public static bool ShouldExpandState(SerializedProperty stateProp, string detectedSlot)
    {
        if (stateProp == null)
            return false;
        if (stateProp.name == detectedSlot)
            return true;
        return stateProp.isExpanded;
    }

    public static bool DrawStateFoldout(Rect rect, SerializedProperty stateProp, GUIContent label)
    {
        if (stateProp == null)
            return false;
        return DrawStateFoldout(
            rect,
            stateProp,
            label,
            ResolveFoldoutSlotName(stateProp.serializedObject));
    }

    public static bool DrawStateFoldout(
        Rect rect,
        SerializedProperty stateProp,
        GUIContent label,
        string detectedSlot)
    {
        if (stateProp == null)
            return false;

        bool isDetected = stateProp.name == detectedSlot;
        if (isDetected)
        {
            stateProp.isExpanded = true;
            ResponsiveUiInspectorFoldouts.DrawFoldout(rect, true, label, true);
            return true;
        }

        stateProp.isExpanded = ResponsiveUiInspectorFoldouts.DrawFoldout(
            rect, stateProp.isExpanded, label, true);
        return stateProp.isExpanded;
    }

    /// <summary>
    /// Play Mode uses the hub layout. Edit Mode uses the selected Device Simulator profile
    /// when one is set — the same layout Apply writes — then Simulator focus, then the handler.
    /// </summary>
    public static ResponsiveDevice.LayoutKey ResolveLiveLayout(ResponsiveUiHandler handler)
    {
        if (handler != null && Application.isPlaying)
            return handler.CurrentLayout;

        if (!Application.isPlaying &&
            ResponsiveDevice.EditorPreviewLayout(out ResponsiveDevice.LayoutKey preview))
            return preview;

        if (ResponsiveUiSimulatorSlotFocus.GetFocusedLayout(out ResponsiveDevice.LayoutKey focused))
            return focused;

        if (handler != null)
            return handler.GetEditorCaptureLayout();

        return ResponsiveDevice.ResolveLiveLayout(ResponsiveDevice.GetWatchScreenSize());
    }

    #endregion
}
}

#endif
