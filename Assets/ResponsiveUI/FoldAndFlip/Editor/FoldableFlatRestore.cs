using UnityEngine;

namespace FDPlus.Foldable.Editor
{
    /// <summary>
    /// Restores Flat posture on detectors when the Device Simulator leaves Fold/Flip devices.
    /// Owned by <see cref="FoldableEditorSessionWatcher"/> (single call site) to avoid Undo spam.
    /// </summary>
    public static class FoldableFlatRestore
    {
        #region Apply

        public static void RestoreAllInLoadedScenes()
        {
            var detectors = Object.FindObjectsByType<FoldablePostureDetector>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < detectors.Length; i++)
            {
                FoldablePostureDetector detector = detectors[i];
                if (detector == null)
                    continue;

                detector.ClearEditorDeviceKind();
                if (Application.isPlaying)
                    detector.ApplyFlatLayout();
                else
                    detector.SyncSimulatedLayoutState(FoldableLayoutState.CreateFlat(
                        Screen.width, Screen.height, detector.DeviceKind));
                detector.ClearEditorDeviceKind();
            }
        }

        #endregion
    }
}
