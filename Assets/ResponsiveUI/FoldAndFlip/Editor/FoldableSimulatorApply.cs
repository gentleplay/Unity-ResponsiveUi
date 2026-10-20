using UnityEditor;
using UnityEngine;

namespace FDPlus.Foldable.Editor
{
    /// <summary>
    /// Pushes the Simulator session posture to the active <see cref="FoldablePostureDetector"/>.
    /// Shared by the Android Fold &amp; Flip and iPhone Duo Device Simulator panels.
    /// </summary>
    public static class FoldableSimulatorApply
    {
        /// <summary>
        /// Play Mode: <see cref="FoldablePostureSource.ApplySimulatedLayout"/> (LayoutChanged).
        /// Edit Mode: <see cref="FoldablePostureSource.SyncSimulatedLayoutState"/> only.
        /// <paramref name="panelTitle"/> names the panel in dialogs and logs;
        /// <paramref name="selectDeviceHint"/> is shown when no matching device is bound.
        /// </summary>
        public static void Apply(bool showDialogs, string panelTitle, string selectDeviceHint)
        {
            if (!FoldableEditorSession.IsFoldableDeviceBound)
            {
                if (showDialogs)
                    EditorUtility.DisplayDialog(panelTitle, selectDeviceHint, "OK");
                return;
            }

            FoldableEditorSessionWatcher.SuppressNextDeviceDefaults();
            FoldableEditorSessionWatcher.RefreshNow();

            if (!FoldablePostureSource.HasActiveOwner())
            {
                if (showDialogs)
                {
                    EditorUtility.DisplayDialog(
                        panelTitle,
                        "No active FoldablePostureDetector in loaded scenes.\n\n" +
                        "Open the scene that hosts the persistent detector, or " +
                        "Responsive UI → Scene → Create Fold & Flip Setup.",
                        "OK");
                }
                return;
            }

            var layout = FoldableEditorSession.BuildLayout();

            if (Application.isPlaying)
            {
                FoldablePostureSource.ApplySimulatedLayout(layout);
                if (showDialogs)
                {
                    Debug.Log(
                        $"[{panelTitle}] Applied {layout.GetProductStateDisplayName()} / {layout.ResolvePosture()} " +
                        $"({layout.currentMetrics.width}×{layout.currentMetrics.height})");
                }
                return;
            }

            // Keep LayoutState aligned with the panel (Cover vs Half Opened) so ResponsiveDevice /
            // ResponsiveCamera map the same product in Edit Mode.
            FoldablePostureSource.SyncSimulatedLayoutState(layout);
            if (showDialogs)
            {
                Debug.Log(
                    $"[{panelTitle}] Edit Mode synced {layout.GetProductStateDisplayName()} posture " +
                    "(FoldablePostureDetector — SyncSimulatedLayoutState only).");
            }
        }
    }
}
