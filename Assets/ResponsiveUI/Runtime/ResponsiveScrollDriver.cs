using System;
using System.Collections;
using FDPlus.Foldable;
using UnityEngine;

namespace FDPlus.ResponsiveUI
{
    /// <summary>
    /// Fold / Simulator / Screen-size lifecycle for a FancyScrollView host that serializes
    /// <see cref="ResponsiveScrollLayout"/>. The host applies metrics in the callback
    /// (typically <see cref="ResponsiveScrollLayout.Apply"/> + Relayout).
    /// Forms follow the build target (<see cref="ResponsivePlatforms"/>): Android Phone / Pad /
    /// Fold / Flip; iOS Phone / Pad / iPhone Duo Cover / Seated; Windows / macOS / Linux / WebGL
    /// Wide Screen only, sized from the window, with no mobile detection.
    /// Do not add <c>LateUpdate</c> on FancyScrollView subclasses — it hides the base editor LateUpdate.
    /// </summary>
    public sealed class ResponsiveScrollDriver
    {
        #region Runtime state

        MonoBehaviour _host;
        Action _apply;
        Vector2 _lastWatchSize;
        bool _hasWatchSize;
        bool _bound;
        bool _postLoadQueued;
        Coroutine _postLoadRoutine;
#if UNITY_EDITOR
        bool _validateQueued;
#endif

        #endregion

        #region Bind

        public void Bind(MonoBehaviour host, Action apply)
        {
            Unbind();
            _host = host;
            _apply = apply;
            if (_host == null || _apply == null)
                return;

            FoldablePostureSource.LayoutChanged += OnFoldableLayoutChanged;
            FoldablePostureSource.ActiveInstanceChanged += OnActiveFoldableChanged;
#if UNITY_EDITOR
            ResponsiveDevice.SimulatorLayoutChanged += OnSimulatorLayoutChanged;
#endif
            CaptureWatchSize();
            _bound = true;
            InvokeApply();
            // ExecuteAlways: Start() may already have run in Edit Mode and will not run again
            // when entering Play Mode. Queue the post-load pass from Bind instead.
            QueuePlayModePostLoad();
        }

        public void Unbind()
        {
            // A deactivated host kills the post-load coroutine without running its tail;
            // clear the queue so the next Bind (re-enable) queues it again.
            CancelPlayModePostLoad();
            if (!_bound)
                return;

            _bound = false;
            FoldablePostureSource.LayoutChanged -= OnFoldableLayoutChanged;
            FoldablePostureSource.ActiveInstanceChanged -= OnActiveFoldableChanged;
#if UNITY_EDITOR
            ResponsiveDevice.SimulatorLayoutChanged -= OnSimulatorLayoutChanged;
#endif
        }

        #endregion

        #region Play Mode

        public void StartPlayMode()
        {
            if (!Application.isPlaying || _host == null)
                return;

            NotifyAndApply();
            QueuePlayModePostLoad();
        }

        public void TickPlayMode()
        {
            if (!Application.isPlaying)
                return;

            Vector2 watch = ResponsiveDevice.GetWatchScreenSize();
            bool sizeChanged = !_hasWatchSize ||
                Mathf.Abs(watch.x - _lastWatchSize.x) >= ResponsiveDevice.SizeHysteresisPixels ||
                Mathf.Abs(watch.y - _lastWatchSize.y) >= ResponsiveDevice.SizeHysteresisPixels;
            if (!sizeChanged)
                return;

            NotifyAndApply();
        }

        public void OnPlayModeResume(bool becameActive)
        {
            if (!becameActive || !Application.isPlaying)
                return;
            NotifyAndApply();
        }

        #endregion

        #region Editor

        public void QueueEditorApply()
        {
#if UNITY_EDITOR
            if (_host == null || !_host.isActiveAndEnabled)
                return;
            if (_validateQueued)
                return;

            _validateQueued = true;
            UnityEditor.EditorApplication.delayCall += () =>
            {
                _validateQueued = false;
                InvokeApply();
            };
#endif
        }

        #endregion

        #region Layout

        void QueuePlayModePostLoad()
        {
            if (!Application.isPlaying || _host == null || _postLoadQueued ||
                !_host.gameObject.activeInHierarchy)
                return;

            _postLoadQueued = true;
            _postLoadRoutine = _host.StartCoroutine(PlayModePostLoadApply());
        }

        void CancelPlayModePostLoad()
        {
            if (_postLoadRoutine != null && _host != null)
                _host.StopCoroutine(_postLoadRoutine);
            _postLoadRoutine = null;
            _postLoadQueued = false;
        }

        IEnumerator PlayModePostLoadApply()
        {
            yield return null;
            _postLoadRoutine = null;
            _postLoadQueued = false;
            if (_host == null || !_host.isActiveAndEnabled || !Application.isPlaying)
                yield break;

            NotifyAndApply();
        }

        void NotifyAndApply()
        {
            ResponsiveDevice.NotifyDisplayChanged();
            CaptureWatchSize();
            InvokeApply();
        }

        void InvokeApply()
        {
            if (_host == null || !_host.isActiveAndEnabled || _apply == null)
                return;
            _apply();
        }

        #endregion

        #region Size watch

        void CaptureWatchSize()
        {
            _lastWatchSize = ResponsiveDevice.GetWatchScreenSize();
            _hasWatchSize = _lastWatchSize.x >= ResponsiveDevice.SizeHysteresisPixels &&
                            _lastWatchSize.y >= ResponsiveDevice.SizeHysteresisPixels;
        }

        #endregion

        #region Foldable

        void OnActiveFoldableChanged()
        {
            if (_host != null && _host.isActiveAndEnabled)
                NotifyAndApply();
        }

        void OnFoldableLayoutChanged(FoldableLayoutState _)
        {
            if (_host == null || !_host.isActiveAndEnabled)
                return;
            NotifyAndApply();
        }

#if UNITY_EDITOR
        void OnSimulatorLayoutChanged()
        {
            if (_host == null || !_host.isActiveAndEnabled)
                return;
            NotifyAndApply();
        }
#endif

        #endregion
    }

}
