#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using FDPlus.ResponsiveUI.Shared.Editor;

namespace FDPlus.ResponsiveUI.Editor
{
    /// <summary>
    /// Synchronizes slot availability after Player Allowed Orientations change.
    /// </summary>
    [InitializeOnLoad]
    static class ResponsiveUiOrientationWatch
    {
        static bool _initialized;
        static bool _syncQueued;
        static bool _portraitAllowed;
        static bool _landscapeAllowed;
        static bool _syncAfterPlayMode;
        static int _revision;

        public static int Revision => _revision;

        static ResponsiveUiOrientationWatch()
        {
            EditorApplication.projectChanged -= QueueSync;
            EditorApplication.projectChanged += QueueSync;
            Undo.undoRedoPerformed -= QueueSync;
            Undo.undoRedoPerformed += QueueSync;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            QueueSync();
        }

        sealed class ActiveBuildTargetChangedHandler : IActiveBuildTargetChanged
        {
            public int callbackOrder => 0;

            public void OnActiveBuildTargetChanged(
                BuildTarget previousTarget,
                BuildTarget newTarget) =>
                QueueSync();
        }

        static void QueueSync()
        {
            if (_syncQueued)
                return;
            _syncQueued = true;
            EditorApplication.delayCall += Sync;
        }

        static void Sync()
        {
            _syncQueued = false;
            if (Application.isBatchMode)
                return;
            if (Application.isPlaying ||
                EditorApplication.isPlayingOrWillChangePlaymode)
            {
                _syncAfterPlayMode = true;
                return;
            }

            bool portrait = ResponsiveUiEditorSettings.IsPortraitAllowedByPlayer();
            bool landscape = ResponsiveUiEditorSettings.IsLandscapeAllowedByPlayer();
            if (_initialized &&
                portrait == _portraitAllowed &&
                landscape == _landscapeAllowed)
                return;

            _initialized = true;
            _portraitAllowed = portrait;
            _landscapeAllowed = landscape;
            _revision++;

            ResponsiveRectStateEditorUtil.SyncLoaded();
            ResponsiveUiSimulatorSlotFocus.RefreshNow();
            ResponsiveUiInspectorRepaint.All();
        }

        static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode ||
                !_syncAfterPlayMode)
                return;

            _syncAfterPlayMode = false;
            QueueSync();
        }
    }

    /// <summary>
    /// Bakes project design/background settings into loaded scene hubs outside
    /// Play Mode, then refreshes their clients.
    /// </summary>
    static class ResponsiveUiBackgroundSizeWatch
    {
        static bool _syncQueued;
        static bool _syncAfterPlayMode;

        static ResponsiveUiBackgroundSizeWatch()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        public static void NotifyChanged()
        {
            if (Application.isBatchMode || _syncQueued)
                return;

            _syncQueued = true;
            EditorApplication.delayCall += Sync;
        }

        static void Sync()
        {
            _syncQueued = false;
            if (Application.isBatchMode)
                return;
            if (Application.isPlaying ||
                EditorApplication.isPlayingOrWillChangePlaymode)
            {
                _syncAfterPlayMode = true;
                return;
            }

            ResponsiveUI[] uis = ResponsiveRectStateEditorUtil.FindLoaded();
            for (int i = 0; i < uis.Length; i++)
            {
                ResponsiveUI ui = uis[i];
                if (ui == null || !ui.gameObject.scene.IsValid())
                    continue;

                if (ui.EditorBakeProjectBackgroundSize())
                    EditorUtility.SetDirty(ui);

                if (ui.isActiveAndEnabled)
                    ui.Apply();
            }

            ResponsiveUiInspectorRepaint.All();
        }

        static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode ||
                !_syncAfterPlayMode)
                return;

            _syncAfterPlayMode = false;
            if (_syncQueued)
                return;
            _syncQueued = true;
            EditorApplication.delayCall += Sync;
        }
    }
}
#endif
