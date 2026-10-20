#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

using FDPlus.ResponsiveUI;

namespace FDPlus.ResponsiveUI.Editor
{
/// <summary>
/// Edit Mode Scene → slot sync for <see cref="ResponsiveCamera"/> Targets and Focus Bounds HUDs.
/// Same automatic detection model as <see cref="ResponsiveUiWidgetSync"/>:
/// <see cref="Undo.postprocessModifications"/> only.
/// Hub / Simulator Apply does not recapture Local Position / Euler or Bounds Layout.
/// Scene capture preserves slot apply/scale settings, matching widget capture.
/// Inspector → Scene is handled by <see cref="ResponsiveCameraLayoutDrawer"/> (active slot only).
/// </summary>
[InitializeOnLoad]
public static class ResponsiveCameraSync
{
    #region Runtime state

    static readonly List<Camera> ScratchTargets = new List<Camera>(8);
    static readonly List<RectTransform> ScratchBounds = new List<RectTransform>(8);
    static readonly List<ResponsiveCamera> OwnerBuffer = new List<ResponsiveCamera>(8);
    static readonly ResponsiveUiEditorCaptureGuard Capture = new ResponsiveUiEditorCaptureGuard();

    static readonly string[] CameraPropertyMarkers =
    {
        "m_LocalPosition",
        "m_LocalRotation",
        "m_LocalEulerAngles",
        "m_LocalEulerAnglesHint",
        "m_OrthographicSize",
        "orthographic size",
        "m_Orthographic",
        "m_FieldOfView",
        "field of view",
        "m_NormalizedViewPortRect",
    };

    static bool _bound;
    static bool _capturing;

    #endregion

    #region Lifecycle

    static ResponsiveCameraSync()
    {
        BindApplyCallbacks();
        EditorApplication.delayCall += Bind;
    }

    static void Bind()
    {
        if (_bound)
            return;
        _bound = true;

        BindApplyCallbacks();
        Undo.postprocessModifications -= OnPostprocessModifications;
        Undo.postprocessModifications += OnPostprocessModifications;
    }

    static void BindApplyCallbacks()
    {
        ResponsiveCamera.EditorBeginApply = BeginApplySuppress;
        ResponsiveCamera.EditorEndApply = EndApplySuppress;
        ResponsiveCamera.EditorIgnoreTargetCapture = IgnoreTargetCapture;
    }

    #endregion

    #region Suppress capture

    public static ResponsiveUiEditorCaptureGuard.Scope SuppressCapture() =>
        Capture.Suppress();

    static void BeginApplySuppress() => Capture.Begin();

    static void EndApplySuppress() => Capture.EndDeferred();

    /// <summary>
    /// Ignore capture for this Target until the next editor delayCall
    /// (covers LivePush Undo that arrives after <see cref="SuppressCapture"/> ends).
    /// </summary>
    public static void IgnoreTargetCapture(Camera camera)
    {
        if (camera == null)
            return;
        Capture.IgnoreUntilDelayCall(camera.GetInstanceID());
    }

    #endregion

    #region Capture

    static bool CannotCapture() =>
        Capture.IsSuppressed ||
        _capturing ||
        Application.isPlaying ||
        EditorApplication.isPlayingOrWillChangePlaymode;

    static UndoPropertyModification[] OnPostprocessModifications(
        UndoPropertyModification[] modifications)
    {
        if (CannotCapture())
            return modifications;
        if (modifications == null || modifications.Length == 0)
            return modifications;

        ScratchTargets.Clear();
        ScratchBounds.Clear();
        for (int i = 0; i < modifications.Length; i++)
        {
            PropertyModification current = modifications[i].currentValue;
            if (current.target == null)
                continue;
            if (IsCameraPropertyPath(current.propertyPath) &&
                ResponsiveCameraEditorUtil.GetCamera(current.target, out Camera camera) &&
                camera != null &&
                !Capture.IsIgnored(camera.GetInstanceID()) &&
                !ScratchTargets.Contains(camera))
                ScratchTargets.Add(camera);

            if (ResponsiveUiWidgetSync.IsLayoutPropertyPath(current.propertyPath) &&
                ResponsiveUI.GetEditorRectTransform(current.target, out RectTransform bounds) &&
                bounds != null &&
                !ScratchBounds.Contains(bounds))
                ScratchBounds.Add(bounds);
        }

        if (ScratchTargets.Count > 0)
            CaptureChangedTargets(ScratchTargets);
        if (ScratchBounds.Count > 0)
            CaptureFocusBoundsOwners(ScratchBounds);
        return modifications;
    }

    static void CaptureChangedTargets(List<Camera> changedTargets)
    {
        if (CannotCapture() || changedTargets == null || changedTargets.Count == 0)
            return;

        _capturing = true;
        try
        {
            FindOwners(changedTargets, OwnerBuffer);
            for (int i = 0; i < OwnerBuffer.Count; i++)
                CaptureOwner(OwnerBuffer[i], changedTargets);
        }
        finally
        {
            _capturing = false;
        }
    }

    static void FindOwners(List<Camera> targets, List<ResponsiveCamera> into)
    {
        into.Clear();
        ResponsiveCamera[] cams = ResponsiveCameraEditorUtil.FindLoaded();
        for (int i = 0; i < cams.Length; i++)
        {
            ResponsiveCamera owner = cams[i];
            if (owner == null || !owner.gameObject.scene.IsValid())
                continue;
            // Cheap ownership test first; Hub may walk parents when unassigned.
            if (!OwnsAnyTarget(owner, targets))
                continue;
            if (!FollowsHubLayout(owner))
                continue;
            into.Add(owner);
        }
    }

    /// <summary>
    /// Capture writes the live Camera into the Inspector's detected slot, which is only
    /// correct when the Camera actually received the current layout. A disabled / inactive
    /// component, or its assigned hub disabled, misses Simulator / fold-state switches, so
    /// the Camera still shows an older slot. A hub-less camera keeps its standalone capture.
    /// </summary>
    static bool FollowsHubLayout(ResponsiveCamera owner)
    {
        if (!owner.isActiveAndEnabled)
            return false;
        ResponsiveUI hub = owner.Hub;
        return hub == null || hub.isActiveAndEnabled;
    }

    static bool OwnsAnyTarget(ResponsiveCamera owner, List<Camera> targets)
    {
        for (int t = 0; t < targets.Count; t++)
        {
            if (owner.EditorOwnsCameraTarget(targets[t]))
                return true;
        }

        return false;
    }

    static void CaptureOwner(ResponsiveCamera owner, List<Camera> changedTargets)
    {
        var so = new SerializedObject(owner);
        so.Update();

        SerializedProperty cameras = so.FindProperty("cameras");
        if (cameras == null || !cameras.isArray || cameras.arraySize == 0)
            return;

        ResponsiveUI hub = owner.Hub;
        ResponsiveDevice.LayoutKey layout = hub != null
            ? ResponsiveUiDetectionDrawer.ResolveInspectorLayout(so, hub)
            : owner.GetEditorCaptureLayout();
        Vector2 uiSize = owner.GetUiLayoutSize();
        bool changed = false;

        for (int i = 0; i < cameras.arraySize; i++)
        {
            SerializedProperty entry = cameras.GetArrayElementAtIndex(i);
            SerializedProperty targetProp = entry.FindPropertyRelative("target");
            if (targetProp?.objectReferenceValue is not Camera camera)
                continue;
            if (!changedTargets.Contains(camera))
                continue;
            if (Capture.IsIgnored(camera.GetInstanceID()))
                continue;

            if (!ResponsiveRectStateEditorUtil.ResolveCaptureSlot(
                    entry, layout, out string stateName, out ResponsiveDevice.FormFactor slotForm))
                continue;
            if (!ResponsiveUiEditorSettings.SlotAllowed(stateName))
                continue;

            SerializedProperty stateProp = entry.FindPropertyRelative(stateName);
            if (stateProp == null)
                continue;

            bool focusOwnsPosition =
                ResponsiveCameraEditorUtil.SlotHasFocus(stateProp);
            bool focusOwnsProjection =
                ResponsiveCameraEditorUtil.SlotFocusOwnsProjection(stateProp);
            if (ResponsiveCameraEditorUtil.MatchesLive(
                    stateProp,
                    camera,
                    uiSize,
                    slotForm,
                    ignoreFocusPosition: focusOwnsPosition,
                    ignoreFocusProjection: focusOwnsProjection))
                continue;

            bool currentlyEnabled =
                stateProp.FindPropertyRelative("enabled")?.boolValue ?? false;
            ResponsiveCameraEditorUtil.WriteCapturedState(
                stateProp,
                ResponsiveCameraState.From(camera),
                currentlyEnabled,
                uiSize,
                slotForm,
                seedTransform: false,
                // Focus always pans and Fit/Cover also zoom. Do not bake those
                // derived values back into the slot's authored seed.
                captureLiveValues: true,
                preserveFocusPosition: focusOwnsPosition,
                preserveFocusProjection: focusOwnsProjection);
            ResponsiveCameraEditorUtil.FitBackground(entry, camera, recordUndo: false);
            changed = true;
        }

        if (!changed)
            return;

        owner.NotifyWidgetLivePreview();
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(owner);
        EditorApplication.QueuePlayerLoopUpdate();
        SceneView.RepaintAll();
    }

    static bool IsCameraPropertyPath(string propertyPath)
    {
        if (string.IsNullOrEmpty(propertyPath))
            return false;

        for (int i = 0; i < CameraPropertyMarkers.Length; i++)
        {
            if (propertyPath.IndexOf(CameraPropertyMarkers[i], StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }

        return false;
    }

    static void CaptureFocusBoundsOwners(List<RectTransform> changedBounds)
    {
        if (CannotCapture() || changedBounds == null || changedBounds.Count == 0)
            return;

        FindFocusBoundsOwners(changedBounds, OwnerBuffer);
        if (OwnerBuffer.Count == 0)
            return;

        _capturing = true;
        try
        {
            for (int i = 0; i < OwnerBuffer.Count; i++)
            {
                ResponsiveCamera owner = OwnerBuffer[i];
                if (owner == null || !owner.gameObject.scene.IsValid())
                    continue;

                var so = new SerializedObject(owner);
                so.Update();
                ResponsiveUI hub = owner.Hub;
                ResponsiveDevice.LayoutKey layout = hub != null
                    ? ResponsiveUiDetectionDrawer.ResolveInspectorLayout(so, hub)
                    : owner.GetEditorCaptureLayout();

                SerializedProperty cameras = so.FindProperty("cameras");
                if (cameras == null || !cameras.isArray || cameras.arraySize == 0)
                    continue;

                bool changed = false;
                for (int c = 0; c < cameras.arraySize; c++)
                {
                    SerializedProperty entry = cameras.GetArrayElementAtIndex(c);
                    if (!ResponsiveRectStateEditorUtil.ResolveCaptureSlot(
                            entry, layout, out string stateName, out _))
                        continue;
                    if (!ResponsiveUiEditorSettings.SlotAllowed(stateName))
                        continue;

                    SerializedProperty stateProp = entry.FindPropertyRelative(stateName);
                    if (stateProp == null)
                        continue;
                    if (stateProp.FindPropertyRelative("focusBounds")?.objectReferenceValue
                            is not RectTransform bounds ||
                        !changedBounds.Contains(bounds))
                        continue;

                    changed |= ResponsiveCameraEditorUtil.CaptureFocusBoundsLayoutInto(stateProp);
                }

                if (!changed)
                    continue;

                owner.NotifyWidgetLivePreview();
                so.ApplyModifiedProperties();

                using (SuppressCapture())
                {
                    ResponsiveCameraEditorUtil.RecordPreviewTargets(owner);
                    owner.EditorApplyCameras(layout, applyBoundsLayout: false);
                }

                EditorUtility.SetDirty(owner);
            }
        }
        finally
        {
            _capturing = false;
        }

        EditorApplication.QueuePlayerLoopUpdate();
        SceneView.RepaintAll();
    }

    static void FindFocusBoundsOwners(
        List<RectTransform> bounds,
        List<ResponsiveCamera> into)
    {
        into.Clear();
        ResponsiveCamera[] cams = ResponsiveCameraEditorUtil.FindLoaded();
        for (int i = 0; i < cams.Length; i++)
        {
            ResponsiveCamera owner = cams[i];
            if (owner == null || !owner.gameObject.scene.IsValid())
                continue;
            if (!UsesAnyFocusBounds(owner, bounds))
                continue;
            if (!FollowsHubLayout(owner))
                continue;
            into.Add(owner);
        }
    }

    static bool UsesAnyFocusBounds(ResponsiveCamera owner, List<RectTransform> bounds)
    {
        for (int i = 0; i < bounds.Count; i++)
        {
            if (owner.EditorUsesFocusBounds(bounds[i]))
                return true;
        }

        return false;
    }

    #endregion
}
}

#endif
