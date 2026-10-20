#if UNITY_EDITOR
using System.Collections.Generic;

using UnityEditor;
using UnityEngine;

using FDPlus.ResponsiveUI;

namespace FDPlus.ResponsiveUI.Editor
{
/// <summary>
/// Shared per-slot clipboard and exact-slot bulk-copy controls for widget,
/// two-side tween, camera, and scroll Inspectors.
/// </summary>
public static class ResponsiveUiSlotClipboard
{
    #region Constants

    public enum Kind
    {
        Widget,
        Camera,
        Scroll,
    }

    const float ButtonWidth = 44f;
    const float ButtonGap = 2f;
    const int ButtonCount = 3;
    const string BulkCopyDialogOptOutKeyPrefix =
        "FDPlus.ResponsiveUI.BulkCopy.";
    public const string WidgetSideBulkCopyOptOutKey =
        BulkCopyDialogOptOutKeyPrefix + "WidgetSide";
    public const string WidgetRowBulkCopyOptOutKey =
        BulkCopyDialogOptOutKeyPrefix + "WidgetRow";
    /// <summary>Bulk copy From default: Fold Cover Portrait.</summary>
    public static readonly int DefaultBulkSourceSlot = ResponsiveWidgetLayout.StatePropertyIndex(
        new ResponsiveDevice.LayoutKey(
            ResponsiveDevice.FormFactor.FoldPhoneCover,
            ResponsiveDevice.Orientation.Portrait));
    /// <summary>Bulk copy To default: Fold Half Opened Portrait.</summary>
    public static readonly int DefaultBulkDestinationSlot = ResponsiveWidgetLayout.StatePropertyIndex(
        new ResponsiveDevice.LayoutKey(
            ResponsiveDevice.FormFactor.FoldHalfOpened,
            ResponsiveDevice.Orientation.Portrait));

    static readonly GUIContent CopyLabel = new GUIContent(
        "Copy",
        "Copy all values from this orientation slot.");
    static readonly GUIContent PasteLabel = new GUIContent(
        "Paste",
        "Paste copied values onto this orientation slot.");
    static readonly GUIContent ResetLabel = new GUIContent(
        "Reset",
        "Reset to type defaults. Widgets keep Size Delta; cameras keep viewport " +
        "size and orthographic size; scroll uses 0.2 interval / 0.5 offset.");

    #endregion

    #region Inspector

    static float ToolbarWidth =>
        ButtonWidth * ButtonCount + ButtonGap * (ButtonCount - 1);

    public static bool DrawFoldoutRow(
        Rect row,
        SerializedProperty state,
        GUIContent label,
        Kind kind)
    {
        Rect foldout = new Rect(
            row.x,
            row.y,
            Mathf.Max(16f, row.width - ToolbarWidth - 4f),
            row.height);
        bool expanded = ResponsiveUiDetectionDrawer.DrawStateFoldout(foldout, state, label);

        Rect bar = new Rect(row.xMax - ToolbarWidth, row.y, ToolbarWidth, row.height);
        float x = bar.x;
        if (GUI.Button(new Rect(x, bar.y, ButtonWidth, bar.height), CopyLabel, EditorStyles.miniButton))
            Copy(state, kind);

        x += ButtonWidth + ButtonGap;
        using (new EditorGUI.DisabledScope(!CanPaste(kind)))
        {
            if (GUI.Button(new Rect(x, bar.y, ButtonWidth, bar.height), PasteLabel, EditorStyles.miniButton))
            {
                Record(state, "Paste Slot");
                Paste(state, kind);
                GUI.changed = true;
                if (kind == Kind.Scroll)
                    ResponsiveScrollEditorUtil.LivePushIfActive(state);
            }
        }

        x += ButtonWidth + ButtonGap;
        if (GUI.Button(new Rect(x, bar.y, ButtonWidth, bar.height), ResetLabel, EditorStyles.miniButton))
        {
            Record(state, "Reset Slot");
            Reset(state, kind);
            GUI.changed = true;
            if (kind == Kind.Scroll)
                ResponsiveScrollEditorUtil.LivePushIfActive(state);
        }

        return expanded;
    }

    #endregion

    #region Bulk copy

    /// <summary>Human-readable label for a shared slot index.</summary>
    public static string SlotLabel(int slotIndex) =>
        ResponsiveWidgetLayout.StatePropertyLabel(slotIndex);

    /// <summary>True when two different shared slot indexes can be copied.</summary>
    public static bool IsValidBulkCopy(
        int sourceSlotIndex,
        int destinationSlotIndex) =>
        IsValidSlotIndex(sourceSlotIndex) &&
        IsValidSlotIndex(destinationSlotIndex) &&
        sourceSlotIndex != destinationSlotIndex;

    /// <summary>Reports a completed bulk-copy operation with clickable Inspector context.</summary>
    public static void LogBulkCopyResult(
        Kind kind,
        Object context,
        int sourceSlotIndex,
        int destinationSlotIndex,
        int copiedCount)
    {
        string item = kind switch
        {
            Kind.Widget => copiedCount == 1 ? "widget" : "widgets",
            Kind.Camera => copiedCount == 1 ? "camera" : "cameras",
            _ => "scroll layout",
        };
        Debug.Log(
            $"[ResponsiveUI] Bulk Copy {kind}: " +
            $"{SlotLabel(sourceSlotIndex)} → {SlotLabel(destinationSlotIndex)}; " +
            $"copied {copiedCount} {item} on '{context.name}'.",
            context);
    }

    /// <summary>
    /// Reports a completed two-side Left ↔ Right bulk copy with clickable
    /// Inspector context.
    /// </summary>
    public static void LogBulkSideCopyResult(
        Object context,
        ResponsiveUiSide sourceSide,
        ResponsiveUiSide destinationSide,
        int copiedCount)
    {
        string item = copiedCount == 1 ? "widget" : "widgets";
        Debug.Log(
            $"[ResponsiveUI] Bulk Copy Side: {sourceSide} → {destinationSide}; " +
            $"copied {copiedCount} {item} on '{context.name}'.",
            context);
    }

    /// <summary>
    /// Reports a completed two-side widget-to-widget copy or swap on one side
    /// with clickable Inspector context.
    /// </summary>
    public static void LogBulkWidgetRowCopyResult(
        Object context,
        string sourceWidget,
        string destinationWidget,
        ResponsiveUiSide side,
        bool swap)
    {
        string verb = swap ? "swapped" : "copied";
        string arrow = swap ? "↔" : "→";
        Debug.Log(
            $"[ResponsiveUI] Bulk Copy Widget Row: {sourceWidget} {arrow} " +
            $"{destinationWidget} on {side}; {verb} Normal and Animation Start " +
            $"on '{context.name}'.",
            context);
    }

    /// <summary>
    /// Draws a collapsed-by-default bulk-copy panel whose From / To menus use
    /// the same currently available slots as the owning Inspector.
    /// </summary>
    /// <returns>True only after the developer confirms an enabled copy.</returns>
    public static bool DrawBulkCopyPanel(
        SerializedObject so,
        string title,
        string buttonLabel,
        string itemName,
        Kind kind,
        int itemCount,
        bool copyPending,
        ref bool expanded,
        ref int sourceSlot,
        ref int destinationSlot,
        out int requestedSource,
        out int requestedDestination,
        string extraHelp = null)
    {
        requestedSource = -1;
        requestedDestination = -1;

        bool confirmed = false;
        int nextSource = sourceSlot;
        int nextDestination = destinationSlot;
        expanded = ResponsiveUiInspectorFoldouts.DrawGuiFoldout(title, expanded, () =>
        {
            int[] availableSlots = GetAvailableSlots(so);
            if (availableSlots.Length == 0)
            {
                EditorGUILayout.HelpBox(
                    "No slots are available for the current orientation settings.",
                    MessageType.Info);
                return;
            }

            nextSource = ResolveAvailableSlot(nextSource, availableSlots);
            nextDestination = ResolveAvailableSlot(nextDestination, availableSlots);
            string[] availableLabels = GetAvailableLabels(availableSlots);

            int sourceOption = System.Array.IndexOf(availableSlots, nextSource);
            sourceOption = EditorGUILayout.Popup("From", sourceOption, availableLabels);
            nextSource = availableSlots[sourceOption];

            int destinationOption =
                System.Array.IndexOf(availableSlots, nextDestination);
            destinationOption = EditorGUILayout.Popup(
                "To",
                destinationOption,
                availableLabels);
            nextDestination = availableSlots[destinationOption];

            string help =
                "Copies every value, including Enabled, from the selected source " +
                "slot to the destination slot. Other slots are unchanged.";
            if (!string.IsNullOrEmpty(extraHelp))
                help += " " + extraHelp;
            EditorGUILayout.HelpBox(help, MessageType.None);

            bool canCopy =
                !copyPending &&
                itemCount > 0 &&
                nextSource != nextDestination;
            using (new EditorGUI.DisabledScope(!canCopy))
            {
                if (GUILayout.Button(buttonLabel))
                {
                    confirmed = EditorUtility.DisplayDialog(
                        title,
                        $"Overwrite {SlotLabel(nextDestination)} on {itemCount} " +
                        $"{itemName}{(itemCount == 1 ? string.Empty : "s")} " +
                        $"with each {itemName}'s " +
                        $"{SlotLabel(nextSource)} values?",
                        itemCount == 1 ? "Copy" : "Copy All",
                        "Cancel",
                        DialogOptOutDecisionType.ForThisSession,
                        BulkCopyDialogOptOutKeyPrefix + kind);
                }
            }
        });
        sourceSlot = nextSource;
        destinationSlot = nextDestination;
        if (!confirmed)
            return false;

        requestedSource = sourceSlot;
        requestedDestination = destinationSlot;
        return true;
    }

    static int[] GetAvailableSlots(SerializedObject so)
    {
        string[] states = ResponsiveWidgetLayout.StatePropertyNames;
        var available = new List<int>(states.Length);
        for (int i = 0; i < states.Length; i++)
        {
            if (ResponsiveUiDetectionDrawer.ShouldDrawState(so, states[i]))
                available.Add(i);
        }

        return available.ToArray();
    }

    static string[] GetAvailableLabels(int[] availableSlots)
    {
        var labels = new string[availableSlots.Length];
        for (int i = 0; i < availableSlots.Length; i++)
        {
            labels[i] = ResponsiveWidgetLayout.SlotGroupLabel(
                    ResponsiveWidgetLayout.SlotFormFactor(availableSlots[i])) +
                " / " + SlotLabel(availableSlots[i]);
        }
        return labels;
    }

    static int ResolveAvailableSlot(int slotIndex, int[] availableSlots)
    {
        if (System.Array.IndexOf(availableSlots, slotIndex) >= 0)
            return slotIndex;

        int oppositeOrientation = slotIndex ^ 1;
        if (System.Array.IndexOf(availableSlots, oppositeOrientation) >= 0)
            return oppositeOrientation;

        return availableSlots[0];
    }

    static bool IsValidSlotIndex(int slotIndex) =>
        slotIndex >= 0 && slotIndex < ResponsiveWidgetLayout.StatePropertyNames.Length;

    #endregion

    #region Clipboard

    static ResponsiveRectState _widget;
    static bool _hasWidget;
    static ResponsiveCameraState _camera;
    static bool _hasCamera;
    static ResponsiveScrollState _scroll;
    static bool _hasScroll;

    public static void Copy(SerializedProperty state, Kind kind)
    {
        switch (kind)
        {
            case Kind.Camera:
                _camera = ResponsiveCameraEditorUtil.ReadState(state);
                _hasCamera = true;
                break;
            case Kind.Scroll:
                _scroll = ResponsiveScrollEditorUtil.ReadState(state);
                _hasScroll = true;
                break;
            default:
                _widget = ResponsiveRectStateEditorUtil.ReadState(state);
                _hasWidget = true;
                break;
        }
    }

    public static bool CanPaste(Kind kind) =>
        kind switch
        {
            Kind.Camera => _hasCamera,
            Kind.Scroll => _hasScroll,
            _ => _hasWidget,
        };

    public static bool Paste(SerializedProperty state, Kind kind)
    {
        if (!CanPaste(kind))
            return false;

        switch (kind)
        {
            case Kind.Camera:
                ResponsiveCameraEditorUtil.WriteState(state, _camera);
                break;
            case Kind.Scroll:
                ResponsiveScrollEditorUtil.WriteState(state, _scroll);
                break;
            default:
                ResponsiveRectStateEditorUtil.WriteState(state, _widget);
                break;
        }

        return true;
    }

    public static void Reset(SerializedProperty state, Kind kind)
    {
        switch (kind)
        {
            case Kind.Camera:
                ResponsiveCameraEditorUtil.ResetSlot(state);
                break;
            case Kind.Scroll:
                ResponsiveScrollEditorUtil.ResetSlot(state);
                break;
            default:
                ResponsiveRectStateEditorUtil.ResetSlot(state);
                break;
        }
    }

    public static void Clear()
    {
        _hasWidget = false;
        _hasCamera = false;
        _hasScroll = false;
    }

    #endregion

    #region Helpers

    static void Record(SerializedProperty state, string undo) =>
        Undo.RecordObject(state.serializedObject.targetObject, undo);

    #endregion
}
}

#endif
