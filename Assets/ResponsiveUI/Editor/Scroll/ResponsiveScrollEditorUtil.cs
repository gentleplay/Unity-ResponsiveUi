#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

using FDPlus.ResponsiveUI;

namespace FDPlus.ResponsiveUI.Editor
{
/// <summary>
/// Shared Inspector helpers for <see cref="ResponsiveScrollLayout"/> hosts
/// (serialized FancyScrollView <c>cellInterval</c> / <c>scrollOffset</c>).
/// </summary>
public static class ResponsiveScrollEditorUtil
{
    #region Constants

    public const string HostIntervalProp = "cellInterval";
    public const string HostOffsetProp = "scrollOffset";

    public static bool GetHostMetrics(
        SerializedObject so,
        out SerializedProperty interval,
        out SerializedProperty offset)
    {
        interval = so?.FindProperty(HostIntervalProp);
        offset = so?.FindProperty(HostOffsetProp);
        return interval != null && offset != null;
    }

    #endregion

    #region Slot enabled

    /// <summary>
    /// When Enabled turns on, copy Cell Interval / Scroll Offset from
    /// <see cref="ResponsiveRectStateEditorUtil.FindFallbackSource"/>.
    /// </summary>
    public static bool CopyFallback(SerializedProperty stateProp)
    {
        SerializedProperty source = ResponsiveRectStateEditorUtil.FindFallbackSource(stateProp);
        if (source == null)
            return false;

        SerializedProperty interval = stateProp.FindPropertyRelative("cellInterval");
        SerializedProperty offset = stateProp.FindPropertyRelative("scrollOffset");
        SerializedProperty sourceInterval = source.FindPropertyRelative("cellInterval");
        SerializedProperty sourceOffset = source.FindPropertyRelative("scrollOffset");
        if (interval != null && sourceInterval != null)
            interval.floatValue = sourceInterval.floatValue;
        if (offset != null && sourceOffset != null)
            offset.floatValue = sourceOffset.floatValue;
        return true;
    }

    #endregion

    #region Serialized

    public static ResponsiveScrollState ReadState(SerializedProperty stateProp) =>
        new ResponsiveScrollState
        {
            enabled = stateProp.FindPropertyRelative("enabled")?.boolValue == true,
            cellInterval = stateProp.FindPropertyRelative("cellInterval")?.floatValue ?? 0f,
            scrollOffset = stateProp.FindPropertyRelative("scrollOffset")?.floatValue ?? 0f,
        };

    public static void WriteState(SerializedProperty stateProp, ResponsiveScrollState state)
    {
        SerializedProperty enabled = stateProp.FindPropertyRelative("enabled");
        if (enabled != null)
            enabled.boolValue = state.enabled;
        SerializedProperty interval = stateProp.FindPropertyRelative("cellInterval");
        if (interval != null)
            interval.floatValue = state.cellInterval;
        SerializedProperty offset = stateProp.FindPropertyRelative("scrollOffset");
        if (offset != null)
            offset.floatValue = state.scrollOffset;
    }

    public static bool SameAuthoring(in ResponsiveScrollState a, in ResponsiveScrollState b) =>
        a.enabled == b.enabled &&
        Mathf.Approximately(a.cellInterval, b.cellInterval) &&
        Mathf.Approximately(a.scrollOffset, b.scrollOffset);

    /// <summary>
    /// Copies Enabled, Cell Interval, and Scroll Offset from one exact layout
    /// slot to another. Other slots are unchanged.
    /// </summary>
    public static bool CopySlot(
        SerializedProperty layout,
        int sourceSlotIndex,
        int destinationSlotIndex)
    {
        string[] states = ResponsiveScrollLayout.StatePropertyNames;
        if (layout == null ||
            sourceSlotIndex < 0 ||
            sourceSlotIndex >= states.Length ||
            destinationSlotIndex < 0 ||
            destinationSlotIndex >= states.Length ||
            sourceSlotIndex == destinationSlotIndex)
            return false;

        ResponsiveScrollState source =
            ReadState(layout.FindPropertyRelative(states[sourceSlotIndex]));
        WriteState(
            layout.FindPropertyRelative(states[destinationSlotIndex]),
            source);
        return true;
    }

    public static void ResetSlot(SerializedProperty stateProp)
    {
        bool enabled = stateProp.FindPropertyRelative("enabled")?.boolValue == true;
        WriteState(stateProp, ResponsiveScrollState.CreateDefault(enabled));
    }

    #endregion

    #region LivePush / Capture

    public static bool CanLivePush(ResponsiveScrollView scroll)
    {
        if (scroll == null || !scroll.isActiveAndEnabled)
            return false;
        ResponsiveUI hub = scroll.Hub;
        return hub == null || hub.enabled;
    }

    /// <summary>
    /// FancyScrollView (or Bind target) that serializes <c>cellInterval</c> /
    /// <c>scrollOffset</c>. Prefers the bound callback owner, then a sibling.
    /// </summary>
    public static Component FindHost(ResponsiveScrollView scroll)
    {
        if (scroll == null)
            return null;

        if (IsMetricsHost(scroll.EditorApplyTarget as Component))
            return (Component)scroll.EditorApplyTarget;

        MonoBehaviour[] behaviours = scroll.GetComponents<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour == null || behaviour == scroll)
                continue;
            if (IsMetricsHost(behaviour))
                return behaviour;
        }

        return null;
    }

    public static bool WriteHostMetrics(Component host, float interval, float offset)
    {
        if (host == null)
            return false;

        interval = ResponsiveScrollState.ClampCellInterval(interval);
        offset = Mathf.Clamp01(offset);

        var so = new SerializedObject(host);
        so.Update();
        if (!GetHostMetrics(so, out SerializedProperty intervalProp, out SerializedProperty offsetProp))
            return false;
        if (Mathf.Approximately(intervalProp.floatValue, interval) &&
            Mathf.Approximately(offsetProp.floatValue, offset))
        {
            SyncOpenHostInspectors(host, interval, offset);
            return false;
        }

        intervalProp.floatValue = interval;
        offsetProp.floatValue = offset;
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(host);
        SyncOpenHostInspectors(host, interval, offset);
        return true;
    }

    /// <summary>
    /// LivePush when this state is the enabled Apply slot. Nested sliders swallow
    /// the layout drawer's <c>EndChangeCheck</c>, so the state drawer calls this
    /// after a real metrics edit.
    /// </summary>
    public static void LivePushIfActive(SerializedProperty stateProp)
    {
        SerializedProperty layout = stateProp.serializedObject.FindProperty("scrollMetrics");
        if (layout == null)
            return;
        if (stateProp.FindPropertyRelative("enabled")?.boolValue != true)
            return;

        ResponsiveDevice.LayoutKey key =
            ResponsiveUiDetectionDrawer.ResolveInspectorLayout(stateProp.serializedObject);
        if (!ResponsiveRectStateEditorUtil.IsActiveEditorSlot(layout, key, stateProp.name))
            return;

        CopySlotToHost(layout, stateProp);
    }

    /// <summary>
    /// Writes the resolved Apply slot onto the host SerializedObject.
    /// Hub / Simulator / bulk copy use this so Inspector YAML matches slots
    /// even when <c>Bind</c> has not run (host disabled in Edit Mode).
    /// </summary>
    public static bool ApplyLayoutToHost(ResponsiveScrollView scroll)
    {
        if (!CanLivePush(scroll))
            return false;

        Component host = FindHost(scroll);
        if (host == null)
            return false;

        float interval = 0f;
        float offset = 0f;
        if (!scroll.Apply(ref interval, ref offset))
            return false;

        bool wrote = WriteHostMetrics(host, interval, offset);
        ResponsiveScrollSync.IgnoreHostCapture(host);
        return wrote;
    }

    /// <summary>
    /// Writes live host Cell Interval / Scroll Offset into the enabled Apply
    /// slot (or the exact layout-key slot when none are enabled).
    /// </summary>
    public static bool CaptureHostToActiveSlot(ResponsiveScrollView scroll)
    {
        Component host = FindHost(scroll);
        if (host == null)
            return false;

        var hostSo = new SerializedObject(host);
        hostSo.Update();
        if (!GetHostMetrics(hostSo, out SerializedProperty hostInterval, out SerializedProperty hostOffset))
            return false;

        float interval = ResponsiveScrollState.ClampCellInterval(hostInterval.floatValue);
        float offset = Mathf.Clamp01(hostOffset.floatValue);

        var so = new SerializedObject(scroll);
        so.Update();
        SerializedProperty metrics = so.FindProperty("scrollMetrics");
        ResponsiveDevice.LayoutKey layout = ResponsiveUiDetectionDrawer.ResolveInspectorLayout(so);
        ResponsiveRectStateEditorUtil.ResolveActiveSlot(metrics, layout, out string slotName, out _);
        if (!ResponsiveUiEditorSettings.SlotAllowed(slotName))
            return false;

        SerializedProperty state = metrics.FindPropertyRelative(slotName);
        if (state == null)
            return false;

        SerializedProperty slotInterval = state.FindPropertyRelative("cellInterval");
        SerializedProperty slotOffset = state.FindPropertyRelative("scrollOffset");
        bool currentlyEnabled = state.FindPropertyRelative("enabled")?.boolValue == true;
        if (slotInterval != null &&
            slotOffset != null &&
            Mathf.Approximately(slotInterval.floatValue, interval) &&
            Mathf.Approximately(slotOffset.floatValue, offset))
            return false;

        WriteState(state, ResponsiveScrollState.From(interval, offset, currentlyEnabled));
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(scroll);
        return true;
    }

    public static void CopySlotToHost(SerializedProperty layoutProp, SerializedProperty stateProp)
    {
        layoutProp.serializedObject.ApplyModifiedProperties();
        if (layoutProp.serializedObject.targetObject is ResponsiveScrollView scroll)
        {
            if (CanLivePush(scroll))
            {
                scroll.Hub?.NotifyWidgetLivePreview();
                using (ResponsiveScrollSync.SuppressCapture())
                {
                    // InvokeApply first so Play Mode Relayout still sees a delta.
                    scroll.EditorInvokeApply();
                    WriteEditedSlotToHost(scroll, stateProp);
                }
            }

            EditorUtility.SetDirty(scroll);
            QueuePreviewRepaint();
            return;
        }

        if (!GetHostMetrics(layoutProp.serializedObject, out SerializedProperty interval, out SerializedProperty offset))
            return;

        SerializedProperty slotInterval = stateProp.FindPropertyRelative("cellInterval");
        SerializedProperty slotOffset = stateProp.FindPropertyRelative("scrollOffset");
        if (slotInterval != null)
            interval.floatValue = ResponsiveScrollState.ClampCellInterval(slotInterval.floatValue);
        if (slotOffset != null)
            offset.floatValue = Mathf.Clamp01(slotOffset.floatValue);

        layoutProp.serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(layoutProp.serializedObject.targetObject);
        QueuePreviewRepaint();
    }

    /// <summary>
    /// Enabled toggled on a slot: the Apply slot may now be a different one
    /// (disabled active slot falls back, or a newly enabled slot takes over).
    /// Re-resolves the enabled slot for the Inspector layout and pushes it to the
    /// host — same role as the widget drawer's resolved-live-slot preview.
    /// </summary>
    public static void PushResolvedSlotToHost(SerializedProperty layoutProp)
    {
        layoutProp.serializedObject.ApplyModifiedProperties();
        if (layoutProp.serializedObject.targetObject is not ResponsiveScrollView scroll)
            return;

        if (CanLivePush(scroll))
        {
            ResponsiveDevice.LayoutKey key =
                ResponsiveUiDetectionDrawer.ResolveInspectorLayout(layoutProp.serializedObject);
            // No enabled slot in the fallback chain: Apply keeps the host values too.
            if (ResponsiveRectStateEditorUtil.ResolveActiveSlot(
                    layoutProp, key, out string slotName, out _))
            {
                scroll.Hub?.NotifyWidgetLivePreview();
                using (ResponsiveScrollSync.SuppressCapture())
                {
                    // InvokeApply first so Play Mode Relayout still sees a delta.
                    scroll.EditorInvokeApply();
                    WriteEditedSlotToHost(scroll, layoutProp.FindPropertyRelative(slotName));
                }
            }
        }

        EditorUtility.SetDirty(scroll);
        QueuePreviewRepaint();
    }

    static bool IsMetricsHost(Component component)
    {
        if (component == null)
            return false;
        var so = new SerializedObject(component);
        return GetHostMetrics(so, out _, out _);
    }

    static void WriteEditedSlotToHost(ResponsiveScrollView scroll, SerializedProperty stateProp)
    {
        Component host = FindHost(scroll);
        if (host == null)
            return;

        SerializedProperty slotInterval = stateProp.FindPropertyRelative("cellInterval");
        SerializedProperty slotOffset = stateProp.FindPropertyRelative("scrollOffset");
        float interval = ResponsiveScrollState.ClampCellInterval(
            slotInterval != null ? slotInterval.floatValue : 0f);
        float offset = Mathf.Clamp01(slotOffset != null ? slotOffset.floatValue : 0f);
        WriteHostMetrics(host, interval, offset);
        ResponsiveScrollSync.IgnoreHostCapture(host);
    }

    /// <summary>
    /// The FancyScrollView Inspector keeps its own SerializedObject. Writing a
    /// second SO is not enough while that Inspector is open — sync its cache so
    /// the next Apply does not stomp slot LivePush.
    /// </summary>
    static void SyncOpenHostInspectors(Component host, float interval, float offset)
    {
        UnityEditor.Editor[] editors = ActiveEditorTracker.sharedTracker.activeEditors;
        for (int i = 0; i < editors.Length; i++)
        {
            UnityEditor.Editor editor = editors[i];
            if (editor == null || editor.target != host)
                continue;

            SerializedObject editorSo = editor.serializedObject;
            editorSo.Update();
            if (!GetHostMetrics(editorSo, out SerializedProperty intervalProp, out SerializedProperty offsetProp))
                continue;
            if (Mathf.Approximately(intervalProp.floatValue, interval) &&
                Mathf.Approximately(offsetProp.floatValue, offset))
                continue;

            intervalProp.floatValue = interval;
            offsetProp.floatValue = offset;
            editorSo.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    static void QueuePreviewRepaint()
    {
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
