using System;
using UnityEditor;
using UnityEngine;

namespace FDPlus.ResponsiveUI.Shared.Editor
{
    /// <summary>Soft-repaints Inspector windows without forcing editor reconstruction.</summary>
    public static class ResponsiveUiInspectorRepaint
    {
        #region Runtime state

        static bool _queued;

        #endregion

        #region Repaint

        public static void All()
        {
            // Play Mode already redraws Inspectors; soft-repaint here causes visible thrash
            // when Device Simulator emits resolution/orientation spam.
            if (Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            // Never Repaint while IMGUI is mid-click (OnGUIDepth mismatches).
            // GUIUtility.guiDepth is not a public API in Unity 6 — use Event.type instead.
            if (Event.current != null &&
                (Event.current.type == EventType.MouseDown ||
                 Event.current.type == EventType.MouseUp ||
                 Event.current.type == EventType.Used))
            {
                Queue();
                return;
            }

            RepaintNow();
        }

        public static void Queue()
        {
            if (_queued)
                return;
            _queued = true;
            EditorApplication.delayCall += () =>
            {
                _queued = false;
                if (Application.isBatchMode)
                    return;
                RepaintNow();
            };
        }

        static void RepaintNow()
        {
            Type inspectorType = typeof(UnityEditor.Editor).Assembly.GetType(
                "UnityEditor.InspectorWindow");
            if (inspectorType == null)
                return;

            UnityEngine.Object[] windows = Resources.FindObjectsOfTypeAll(inspectorType);
            for (int i = 0; i < windows.Length; i++)
            {
                if (windows[i] is EditorWindow window)
                    window.Repaint();
            }
        }

        #endregion
    }
}
