#if UNITY_EDITOR
using System;
using UnityEditor;

namespace FDPlus.ResponsiveUI.Editor
{
    /// <summary>
    /// Per-Inspector state for the exact-slot bulk-copy panel shared by widget, two-side,
    /// camera, and scroll Inspectors: From / To selection, foldout, and the one-copy-in-flight
    /// guard. A confirmed copy runs on the next editor delayCall so it never mutates the
    /// SerializedObject mid-OnGUI.
    /// </summary>
    public sealed class ResponsiveUiBulkSlotCopyPanel
    {
        #region Runtime state

        int _source = ResponsiveUiSlotClipboard.DefaultBulkSourceSlot;
        int _destination = ResponsiveUiSlotClipboard.DefaultBulkDestinationSlot;
        bool _copyQueued;
        bool _expanded;

        #endregion

        #region Inspector

        /// <summary>
        /// Draws <see cref="ResponsiveUiSlotClipboard.DrawBulkCopyPanel"/> and, after the
        /// developer confirms, calls <paramref name="execute"/>(source, destination) on the
        /// next editor delayCall.
        /// </summary>
        public void Draw(
            SerializedObject so,
            string title,
            string buttonLabel,
            string itemName,
            ResponsiveUiSlotClipboard.Kind kind,
            int itemCount,
            Action<int, int> execute,
            string extraHelp = null)
        {
            if (!ResponsiveUiSlotClipboard.DrawBulkCopyPanel(
                    so,
                    title,
                    buttonLabel,
                    itemName,
                    kind,
                    itemCount,
                    _copyQueued,
                    ref _expanded,
                    ref _source,
                    ref _destination,
                    out int source,
                    out int destination,
                    extraHelp))
                return;

            _copyQueued = true;
            EditorApplication.delayCall += () =>
            {
                _copyQueued = false;
                execute(source, destination);
            };
        }

        #endregion
    }
}

#endif
