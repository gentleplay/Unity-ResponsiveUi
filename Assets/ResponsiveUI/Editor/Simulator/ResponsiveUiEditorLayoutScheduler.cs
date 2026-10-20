#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace FDPlus.ResponsiveUI.Editor
{
    /// <summary>
    /// One editor layout pass: immediate focused Apply, then a coalesced
    /// canvas-ready layout-only notify-all plus Match Mode cover after
    /// Device Simulator / CanvasScaler geometry settles.
    /// </summary>
    public static class ResponsiveUiEditorLayoutScheduler
    {
        #region Runtime state

        static bool _canvasPassQueued;
        static bool _canvasPassAgain;

        #endregion

        #region Apply

        public static bool CanvasCatchupQueued => _canvasPassQueued;

        internal static void CancelPending()
        {
            _canvasPassQueued = false;
            _canvasPassAgain = false;
        }

        public static void Apply(bool scheduleCanvasCatchup)
        {
            if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            ResponsiveUiSimulatorSlotFocus.ApplyFocusedLayoutNow();
            if (scheduleCanvasCatchup)
                ScheduleCanvasCatchup();
        }

        public static void ScheduleCanvasCatchup()
        {
            if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            if (_canvasPassQueued)
            {
                // Cover Apply already queued a pass; Cover→Main sync still needs one
                // after Unity Screen commits (otherwise that pass never runs).
                _canvasPassAgain = true;
                return;
            }

            _canvasPassQueued = true;
            EditorApplication.delayCall += () =>
            {
                _canvasPassQueued = false;
                if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    _canvasPassAgain = false;
                    return;
                }

                Canvas.ForceUpdateCanvases();
                ResponsiveUiSimulatorSlotFocus.ApplyFocusedLayoutAfterCanvas();
                if (!_canvasPassAgain)
                    return;

                _canvasPassAgain = false;
                ScheduleCanvasCatchup();
            };
        }

        #endregion
    }
}

#endif
