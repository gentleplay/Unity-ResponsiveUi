#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace FDPlus.ResponsiveUI.Editor
{
    /// <summary>
    /// Shared Undo group for exact-slot bulk copy. Reverts the group when nothing copied.
    /// </summary>
    public static class ResponsiveUiBulkCopyUtil
    {
        #region Execute

        public static bool Execute(
            string undoName,
            UnityEngine.Object[] recordTargets,
            Func<int> mutate,
            Action onCopied)
        {
            if (recordTargets == null || recordTargets.Length == 0 || mutate == null)
                return false;

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(undoName);
            Undo.RecordObjects(recordTargets, undoName);

            int copied = mutate();
            if (copied <= 0)
            {
                Undo.RevertAllDownToGroup(undoGroup);
                return false;
            }

            onCopied?.Invoke();
            Undo.CollapseUndoOperations(undoGroup);
            return true;
        }

        #endregion

        #region Helpers

        /// <summary>Undo name for an exact-slot copy, e.g. "Copy Phone Portrait to Pad Portrait".</summary>
        public static string SlotCopyUndoName(int sourceSlotIndex, int destinationSlotIndex) =>
            $"Copy {ResponsiveUiSlotClipboard.SlotLabel(sourceSlotIndex)} to " +
            ResponsiveUiSlotClipboard.SlotLabel(destinationSlotIndex);

        /// <summary>Adds a preview Undo target once; skips null and duplicates.</summary>
        public static void AddUndoTarget(
            List<UnityEngine.Object> targets,
            UnityEngine.Object target)
        {
            if (target != null && !targets.Contains(target))
                targets.Add(target);
        }

        /// <summary>Refreshes Scene views and the player loop after a bulk-copy preview.</summary>
        public static void RepaintPreview()
        {
            SceneView.RepaintAll();
            EditorApplication.QueuePlayerLoopUpdate();
        }

        #endregion
    }
}

#endif
