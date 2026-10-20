#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

using FDPlus.ResponsiveUI;

namespace FDPlus.ResponsiveUI.Editor
{
/// <summary>
/// Edit Mode Scene → slot sync for responsive widget owners.
/// Fires only when a developer modifies a widget Target's layout properties
/// (<see cref="Undo.postprocessModifications"/>). No idle poll / selection sync.
/// Inspector → Scene is handled by <see cref="ResponsiveWidgetLayoutDrawer"/> (active slot only).
/// </summary>
[InitializeOnLoad]
public static class ResponsiveUiWidgetSync
{
    #region Runtime state

    static readonly List<RectTransform> ChangedTargets = new List<RectTransform>(8);
    static readonly List<ResponsiveUiController> OwnerBuffer = new List<ResponsiveUiController>(8);
    static readonly List<ResponsiveTwoSideUiTweenController> TwoSideOwnerBuffer =
        new List<ResponsiveTwoSideUiTweenController>(4);
    static readonly ResponsiveUiEditorCaptureGuard Capture = new ResponsiveUiEditorCaptureGuard();

    /// <summary>
    /// RectTransform property paths that move a widget Target or camera Focus Bounds HUD.
    /// </summary>
    static readonly string[] LayoutPropertyMarkers =
    {
        "m_AnchoredPosition",
        "m_SizeDelta",
        "m_AnchorMin",
        "m_AnchorMax",
        "m_Pivot",
        "m_LocalScale",
        "m_LocalPosition",
    };

    static bool _bound;
    static bool _capturing;

    #endregion

    #region Lifecycle

    static ResponsiveUiWidgetSync()
    {
        BindApplyCallbacks();
        EditorApplication.delayCall += Bind;
    }

    static void Bind()
    {
        if (_bound)
            return;
        _bound = true;

        Undo.postprocessModifications -= OnPostprocessModifications;
        Undo.postprocessModifications += OnPostprocessModifications;
        BindApplyCallbacks();
    }

    static void BindApplyCallbacks()
    {
        ResponsiveTextBanner.EditorIgnoreSlotCapture = IgnoreTargetCapture;
    }

    #endregion

    #region Suppress capture

    /// <summary>
    /// Suppresses Scene → slot capture while Inspector → Scene pushes run.
    /// Release is deferred one editor delayCall so late Undo echoes are ignored.
    /// </summary>
    public static ResponsiveUiEditorCaptureGuard.Scope SuppressCapture() =>
        Capture.Suppress();

    /// <summary>
    /// Ignore capture for this Target until the next editor delayCall
    /// (covers LivePush Undo that arrives after <see cref="SuppressCapture"/> ends).
    /// </summary>
    public static void IgnoreTargetCapture(RectTransform rt)
    {
        if (rt == null)
            return;
        Capture.IgnoreUntilDelayCall(rt.GetInstanceID());
    }

    #endregion

    #region Capture

    static UndoPropertyModification[] OnPostprocessModifications(
        UndoPropertyModification[] modifications)
    {
        if (Capture.IsSuppressed || _capturing)
            return modifications;
        if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
            return modifications;
        if (modifications == null || modifications.Length == 0)
            return modifications;

        ChangedTargets.Clear();
        for (int i = 0; i < modifications.Length; i++)
        {
            PropertyModification current = modifications[i].currentValue;
            if (!IsLayoutPropertyPath(current.propertyPath))
                continue;
            if (!ResponsiveUI.GetEditorRectTransform(current.target, out RectTransform rt))
                continue;
            if (Capture.IsIgnored(rt.GetInstanceID()))
                continue;
            if (ChangedTargets.Contains(rt))
                continue;
            ChangedTargets.Add(rt);
        }

        if (ChangedTargets.Count == 0)
            return modifications;

        CaptureChangedTargets(ChangedTargets);
        return modifications;
    }

    /// <summary>True when an Undo property path edits RectTransform layout.</summary>
    internal static bool IsLayoutPropertyPath(string propertyPath)
    {
        if (string.IsNullOrEmpty(propertyPath))
            return false;

        for (int i = 0; i < LayoutPropertyMarkers.Length; i++)
        {
            if (propertyPath.IndexOf(LayoutPropertyMarkers[i], StringComparison.Ordinal) >= 0)
                return true;
        }

        return false;
    }

    static void CaptureChangedTargets(List<RectTransform> changedTargets)
    {
        if (_capturing || changedTargets == null || changedTargets.Count == 0)
            return;

        _capturing = true;
        try
        {
            FindOwners(changedTargets, OwnerBuffer);
            for (int i = 0; i < OwnerBuffer.Count; i++)
                CaptureOwner(OwnerBuffer[i], changedTargets);
            FindTwoSideOwners(changedTargets, TwoSideOwnerBuffer);
            for (int i = 0; i < TwoSideOwnerBuffer.Count; i++)
                CaptureOwner(TwoSideOwnerBuffer[i], changedTargets);
        }
        finally
        {
            _capturing = false;
        }
    }

    static void FindOwners(List<RectTransform> targets, List<ResponsiveUiController> into)
    {
        into.Clear();
        ResponsiveUiController[] controllers = ResponsiveRectStateEditorUtil.FindLoadedControllers();
        for (int i = 0; i < controllers.Length; i++)
        {
            ResponsiveUiController controller = controllers[i];
            if (controller == null || !controller.gameObject.scene.IsValid())
                continue;
            // Cheap ownership test first; Hub may walk parents when unassigned.
            if (!OwnsAnyTarget(controller, targets))
                continue;
            if (!FollowsHubLayout(controller, controller.Hub))
                continue;
            into.Add(controller);
        }
    }

    /// <summary>
    /// Capture writes the live Target into the Inspector's detected slot, which is only
    /// correct when that Target actually received the current layout. A disabled / inactive
    /// owner or hub misses Simulator / fold-state switches, so its Target still shows an
    /// older slot — capturing would copy that whole rect (at the new device ratio) into
    /// the new slot. Widgets need a hub (same rule as widget LivePush);
    /// <see cref="ResponsiveCameraSync"/> and <see cref="ResponsiveScrollSync"/> apply the
    /// same enabled-hub rule but keep capturing a hub-less owner.
    /// </summary>
    static bool FollowsHubLayout(Behaviour owner, ResponsiveUI hub) =>
        owner.isActiveAndEnabled && hub != null && hub.isActiveAndEnabled;

    static bool OwnsAnyTarget(ResponsiveUiController controller, List<RectTransform> targets)
    {
        for (int t = 0; t < targets.Count; t++)
        {
            if (controller.EditorOwnsWidgetTarget(targets[t]))
                return true;
        }

        return false;
    }

    static void FindTwoSideOwners(
        List<RectTransform> targets,
        List<ResponsiveTwoSideUiTweenController> into)
    {
        into.Clear();
        ResponsiveTwoSideUiTweenController[] controllers =
            Resources.FindObjectsOfTypeAll<ResponsiveTwoSideUiTweenController>();
        for (int i = 0; i < controllers.Length; i++)
        {
            ResponsiveTwoSideUiTweenController controller = controllers[i];
            if (controller == null || !controller.gameObject.scene.IsValid())
                continue;
            if (!OwnsAnyTarget(controller, targets))
                continue;
            if (!FollowsHubLayout(controller, controller.Hub))
                continue;
            into.Add(controller);
        }
    }

    static bool OwnsAnyTarget(
        ResponsiveTwoSideUiTweenController controller,
        List<RectTransform> targets)
    {
        for (int i = 0; i < targets.Count; i++)
        {
            if (controller.EditorOwnsWidgetTarget(targets[i]))
                return true;
        }

        return false;
    }

    static void CaptureOwner(ResponsiveUiController controller, List<RectTransform> changedTargets)
    {
        var so = new SerializedObject(controller);
        so.Update();

        CaptureOwner(
            controller,
            controller.Hub,
            so,
            so.FindProperty("widgets"),
            null,
            changedTargets);
    }

    static void CaptureOwner(
        ResponsiveTwoSideUiTweenController controller,
        List<RectTransform> changedTargets)
    {
        var so = new SerializedObject(controller);
        so.Update();

        var side = (ResponsiveUiSide)so.FindProperty("side").intValue;
        bool previewAnimationStart = so.FindProperty("previewAnimationStart").boolValue;
        CaptureOwner(
            controller,
            controller.Hub,
            so,
            so.FindProperty("widgets"),
            row => row.FindPropertyRelative(
                ResponsiveRectStateEditorUtil.TwoSideLayoutPropertyName(
                    row, side, previewAnimationStart)),
            changedTargets);
    }

    static void CaptureOwner(
        UnityEngine.Object owner,
        ResponsiveUI hub,
        SerializedObject so,
        SerializedProperty widgets,
        Func<SerializedProperty, SerializedProperty> shownLayoutOf,
        List<RectTransform> changedTargets)
    {
        if (widgets == null || !widgets.isArray || widgets.arraySize == 0)
            return;

        // FindOwners / FindTwoSideOwners only pass owners with an enabled hub.
        ResponsiveDevice.LayoutKey layout =
            ResponsiveUiDetectionDrawer.ResolveInspectorLayout(so, hub);
        bool changed = false;

        for (int i = 0; i < widgets.arraySize; i++)
        {
            // Two-side rows capture into the layout the Inspector shows (Side + Preview).
            SerializedProperty widget = widgets.GetArrayElementAtIndex(i);
            if (shownLayoutOf != null)
                widget = shownLayoutOf(widget);
            if (widget == null)
                continue;
            SerializedProperty targetProp = widget.FindPropertyRelative("target");
            if (targetProp == null ||
                targetProp.objectReferenceValue is not RectTransform rt)
                continue;
            if (!changedTargets.Contains(rt))
                continue;
            if (Capture.IsIgnored(rt.GetInstanceID()))
                continue;

            if (!ResponsiveRectStateEditorUtil.ResolveCaptureSlot(
                    widget,
                    layout,
                    out string stateName,
                    out ResponsiveDevice.FormFactor sourceForm))
                continue;

            if (!ResponsiveUiEditorSettings.SlotAllowed(stateName))
                continue;

            SerializedProperty stateProp = widget.FindPropertyRelative(stateName);
            if (stateProp == null)
                continue;

            int slotIndex = ResponsiveWidgetLayout.StatePropertyIndex(stateName);
            ResponsiveRectStateEditorUtil.EnsureDesignScaleDefaults(stateProp, slotIndex);

            ResponsiveRectState state = ResponsiveRectStateEditorUtil.ReadState(stateProp);
            Vector2 layoutRatio = hub.GetLayoutRatio(
                state,
                sourceForm,
                rt,
                ResponsiveWidgetLayout.SlotOrientation(slotIndex));
            Vector2 expectedLocal = hub.GetAppliedWidgetScale(state, sourceForm, layoutRatio);

            if (ResponsiveRectStateEditorUtil.MatchesLayout(
                    stateProp, rt, layoutRatio, expectedLocal))
                continue;

            ResponsiveRectState captured = ResponsiveRectState.From(rt, layoutRatio, sourceForm);
            captured.CopyDesignScaleSettingsFrom(state);
            ResponsiveRectStateEditorUtil.WriteCapturedLive(stateProp, captured);
            changed = true;
        }

        if (!changed)
            return;

        hub.NotifyWidgetLivePreview();
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(owner);
    }

    #endregion
}
}

#endif
