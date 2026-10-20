#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

using FDPlus.ResponsiveUI;

namespace FDPlus.ResponsiveUI.Editor
{
/// <summary>
/// Edit Mode host → slot sync for <see cref="ResponsiveScrollView"/>.
/// Same automatic detection model as <see cref="ResponsiveUiWidgetSync"/>:
/// <see cref="Undo.postprocessModifications"/> only.
/// Hub / Simulator Apply does not recapture FancyScrollView Cell Interval /
/// Scroll Offset. Inspector → host is handled by
/// <see cref="ResponsiveScrollStateDrawer"/> sliders and
/// <see cref="ResponsiveScrollLayoutDrawer"/> (active slot only).
/// </summary>
[InitializeOnLoad]
public static class ResponsiveScrollSync
{
    #region Runtime state

    static readonly List<ResponsiveScrollView> ChangedOwners = new List<ResponsiveScrollView>(4);
    static readonly ResponsiveUiEditorCaptureGuard Capture = new ResponsiveUiEditorCaptureGuard();

    static bool _bound;
    static bool _capturing;

    #endregion

    #region Lifecycle

    static ResponsiveScrollSync()
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
        ResponsiveScrollView.EditorBeginApply = BeginApplySuppress;
        ResponsiveScrollView.EditorEndApply = EndApplySuppress;
        ResponsiveScrollView.EditorPushHost = scroll =>
            ResponsiveScrollEditorUtil.ApplyLayoutToHost(scroll);
    }

    #endregion

    #region Suppress capture

    public static ResponsiveUiEditorCaptureGuard.Scope SuppressCapture() =>
        Capture.Suppress();

    static void BeginApplySuppress() => Capture.Begin();

    static void EndApplySuppress() => Capture.EndDeferred();

    /// <summary>
    /// Ignore capture for this host until the next editor delayCall
    /// (covers LivePush Undo that arrives after <see cref="SuppressCapture"/> ends).
    /// </summary>
    public static void IgnoreHostCapture(Component host)
    {
        if (host == null)
            return;
        Capture.IgnoreUntilDelayCall(host.GetInstanceID());
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

        ChangedOwners.Clear();
        for (int i = 0; i < modifications.Length; i++)
        {
            PropertyModification current = modifications[i].currentValue;
            if (current.target == null)
                continue;
            if (!IsHostMetricPath(current.propertyPath))
                continue;
            if (current.target is not Component component)
                continue;
            if (Capture.IsIgnored(component.GetInstanceID()))
                continue;

            ResponsiveScrollView scroll = component.GetComponent<ResponsiveScrollView>();
            if (scroll == null || ChangedOwners.Contains(scroll))
                continue;
            ChangedOwners.Add(scroll);
        }

        if (ChangedOwners.Count > 0)
            CaptureChangedHosts(ChangedOwners);
        return modifications;
    }

    static bool IsHostMetricPath(string propertyPath)
    {
        if (string.IsNullOrEmpty(propertyPath))
            return false;
        if (propertyPath.IndexOf("scrollMetrics", StringComparison.Ordinal) >= 0)
            return false;
        return propertyPath == ResponsiveScrollEditorUtil.HostIntervalProp ||
            propertyPath == ResponsiveScrollEditorUtil.HostOffsetProp ||
            propertyPath.EndsWith("." + ResponsiveScrollEditorUtil.HostIntervalProp, StringComparison.Ordinal) ||
            propertyPath.EndsWith("." + ResponsiveScrollEditorUtil.HostOffsetProp, StringComparison.Ordinal);
    }

    static void CaptureChangedHosts(List<ResponsiveScrollView> owners)
    {
        if (CannotCapture() || owners == null || owners.Count == 0)
            return;

        _capturing = true;
        try
        {
            for (int i = 0; i < owners.Count; i++)
            {
                ResponsiveScrollView scroll = owners[i];
                if (scroll == null || !scroll.gameObject.scene.IsValid())
                    continue;
                // Same gate as LivePush: a disabled component or hub missed the
                // current layout, so its host still shows an older slot.
                if (!ResponsiveScrollEditorUtil.CanLivePush(scroll))
                    continue;

                Component host = ResponsiveScrollEditorUtil.FindHost(scroll);
                if (host == null || Capture.IsIgnored(host.GetInstanceID()))
                    continue;

                scroll.Hub?.NotifyWidgetLivePreview();
                ResponsiveScrollEditorUtil.CaptureHostToActiveSlot(scroll);
            }
        }
        finally
        {
            _capturing = false;
        }

        EditorApplication.QueuePlayerLoopUpdate();
        SceneView.RepaintAll();
    }

    #endregion
}
}

#endif
