#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;

namespace FDPlus.ResponsiveUI.Editor
{
    /// <summary>
    /// Shared Scene→slot capture suppress / ignore window for widget, camera, and scroll sync.
    /// Each owner keeps its own instance so LivePush on one domain does not block the other.
    /// </summary>
    public sealed class ResponsiveUiEditorCaptureGuard
    {
        #region Runtime state

        readonly HashSet<int> _ignoredTargetIds = new HashSet<int>();
        int _suppressCapture;

        #endregion

        #region Suppress

        public bool IsSuppressed => _suppressCapture > 0;

        public bool IsIgnored(int instanceId) => _ignoredTargetIds.Contains(instanceId);

        public Scope Suppress()
        {
            _suppressCapture++;
            return new Scope(this);
        }

        public void Begin() => _suppressCapture++;

        public void EndDeferred() =>
            EditorApplication.delayCall += Release;

        public void IgnoreUntilDelayCall(int instanceId)
        {
            _ignoredTargetIds.Add(instanceId);
            EditorApplication.delayCall += () => _ignoredTargetIds.Remove(instanceId);
        }

        void Release()
        {
            if (_suppressCapture > 0)
                _suppressCapture--;
        }

        public readonly struct Scope : IDisposable
        {
            readonly ResponsiveUiEditorCaptureGuard _guard;

            internal Scope(ResponsiveUiEditorCaptureGuard guard) => _guard = guard;

            public void Dispose()
            {
                if (_guard == null)
                    return;
                _guard.EndDeferred();
            }
        }

        #endregion
    }
}

#endif
