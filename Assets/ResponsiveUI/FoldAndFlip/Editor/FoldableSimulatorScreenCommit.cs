using UnityEditor;
using UnityEngine;

namespace FDPlus.Foldable.Editor
{
    /// <summary>
    /// Switches Device Simulator Cover/Inner to the bound Fold session, then notifies
    /// layout only after Unity Screen matches that product (or on the last catch-up).
    /// ★ Flip lists Cover first; Opened catalog metrics are already Main, so
    /// <see cref="FoldableEditorSession.Changed"/> does not fire again after first-bind.
    /// </summary>
    public static class FoldableSimulatorScreenCommit
    {
        #region Runtime state

        public static bool InFlight { get; private set; }

        static int _generation;

        #endregion

        #region Lifecycle

        static FoldableSimulatorScreenCommit()
        {
            // Only cancel across assembly reload. Play-mode exit already tears down
            // delayCalls with the domain; avoid extra playModeStateChanged work.
            AssemblyReloadEvents.beforeAssemblyReload += CancelPending;
        }

        /// <summary>Drop in-flight commits across domain reload.</summary>
        public static void CancelPending()
        {
            _generation++;
            InFlight = false;
        }

        #endregion

        #region Commit

        /// <summary>
        /// Phone → Flip / Fold profile bind: wait one tick, commit, wait one more tick.
        /// Unity Screen often lags the session product by a frame.
        /// </summary>
        public static void QueueDeferred()
        {
            InFlight = true;
            Schedule(++_generation, commitsRemaining: 2);
        }

        /// <summary>
        /// Simulator panel posture change (Android Fold &amp; Flip or iPhone Duo): commit now, then
        /// one catch-up tick.
        /// </summary>
        public static void CommitNowThenCatchup()
        {
            InFlight = true;
            int generation = ++_generation;
            RunCommit(generation, lastCommit: false);
            Schedule(generation, commitsRemaining: 1);
        }

        /// <summary>One chrome sync + layout notify (Force Apply).</summary>
        public static void CommitNow()
        {
            int generation = ++_generation;
            InFlight = true;
            RunCommit(generation, lastCommit: true);
            if (generation == _generation)
                InFlight = false;
        }

        static void Schedule(int generation, int commitsRemaining)
        {
            EditorApplication.delayCall += () =>
            {
                if (generation != _generation)
                    return;

                // Never spin delayCall across compile / domain reload — that can
                // deadlock MonoManager ReloadAssembly (isUpdating stays true).
                if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                {
                    InFlight = false;
                    return;
                }

                RunCommit(generation, lastCommit: commitsRemaining <= 1);
                int remaining = commitsRemaining - 1;
                if (remaining > 0)
                    Schedule(generation, remaining);
                else if (generation == _generation)
                    InFlight = false;
            };
        }

        static void RunCommit(int generation, bool lastCommit)
        {
            if (generation != _generation)
                return;
            if (!FoldableEditorSession.IsFoldableDeviceBound)
            {
                InFlight = false;
                return;
            }

            int width;
            int height;
            if (!SimulatorDeviceBridge.SyncFromSession(out width, out height, out _))
            {
                // No catalog screen for the bound device: sync chrome only, no metrics.
                SimulatorDeviceBridge.SyncScreenForFoldablePosture(
                    FoldableEditorSession.NeedsInnerScreen);
                if (lastCommit)
                    FoldableEditorSession.PublishScreenSynced();
                return;
            }

            // Cover chrome can report a successful screen-index write while Unity
            // Screen is still the previous size. Applying then locks widgets to that
            // Cover canvas. Wait for Screen to match, or force on the last tick.
            if (!lastCommit && !UnityScreenMatchesProduct(width, height))
                return;

            FoldableEditorSession.PublishFold(
                FoldableEditorSession.State,
                FoldableEditorSession.Orientation,
                FoldableEditorSession.HingeNormalized,
                FoldableEditorSession.HingeThicknessPx,
                FoldableEditorSession.IsSeparating,
                width,
                height,
                FoldableEditorSession.Source.Bridge,
                FoldableEditorSession.ProductState);
            FoldableEditorSession.PublishScreenSynced();
        }

        static bool UnityScreenMatchesProduct(int width, int height) =>
            width > 0 &&
            height > 0 &&
            Mathf.Abs(Screen.width - width) < 2 &&
            Mathf.Abs(Screen.height - height) < 2;

        #endregion
    }
}
