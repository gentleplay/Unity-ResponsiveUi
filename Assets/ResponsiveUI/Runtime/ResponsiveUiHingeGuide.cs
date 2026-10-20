using FDPlus.Foldable;
using UnityEngine;

namespace FDPlus.ResponsiveUI
{
    /// <summary>
    /// Half-open hinge / dual-pane border math for the Editor Device Simulator guide.
    /// </summary>
    public static class ResponsiveUiHingeGuide
    {
        #region Constants

        /// <summary>Default line color (Project Settings → Responsive UI seeds from it).</summary>
        public static readonly Color DefaultGuideColor = new Color(0.25f, 0.25f, 0.25f, 0.4f);
        /// <summary>
        /// Default line thickness in pixels (Project Settings → Responsive UI seeds from it), and
        /// the minimum width <see cref="ToGuiRect"/> draws from a normalized thickness.
        /// </summary>
        public const float DefaultThicknessPx = 5f;
        public const float MinThicknessPx = 1f;
        public const float MaxThicknessPx = 10f;

        #endregion

        #region Editor session

#if UNITY_EDITOR
        static bool _editorHintActive;
        static bool _editorHintVertical;
        static float _editorHintNormalized = 0.5f;
        static float _editorHintThicknessNorm = 24f / 2176f;

        /// <summary>
        /// Editor Simulator Fold session hinge (overlay / slot focus).
        /// </summary>
        public static void SetEditorSessionHint(FoldableLayoutState layout)
        {
            if (!ComputeFromLayout(layout, out bool vertical, out float normalized, out float thicknessNorm))
            {
                ClearEditorSessionHint();
                return;
            }

            _editorHintActive = true;
            _editorHintVertical = vertical;
            _editorHintNormalized = normalized;
            _editorHintThicknessNorm = thicknessNorm;
        }

        public static void ClearEditorSessionHint() => _editorHintActive = false;
#endif

        #endregion

        #region Layout math

        /// <summary>
        /// True for a separating Half Opened inner layout (not Cover / Normal / Expand).
        /// </summary>
        public static bool CanDrawFromLayout(FoldableLayoutState layout)
        {
            if (layout == null || layout.isCoverScreen || !layout.isSeparating)
                return false;
            if (layout.productState == FoldableProductState.Opened ||
                layout.productState == FoldableProductState.Closed)
                return false;
            if (layout.productState != FoldableProductState.HalfOpened &&
                layout.state != FoldableState.HalfOpened)
                return false;
            return layout.ResolvePosture() != FoldablePosture.Flat;
        }

        /// <summary>
        /// Computes hinge line in full-screen normalized space (0–1, bottom-left origin).
        /// </summary>
        public static bool ComputeFromLayout(
            FoldableLayoutState layout,
            out bool vertical,
            out float normalized,
            out float thicknessNormalized)
        {
            vertical = false;
            normalized = 0.5f;
            thicknessNormalized = 0.01f;

            if (!CanDrawFromLayout(layout))
                return false;

            FoldablePosture posture = layout.ResolvePosture();
            int metricsW = Mathf.Max(1, layout.currentMetrics.width);
            int metricsH = Mathf.Max(1, layout.currentMetrics.height);
            // foldBounds is already Unity bottom-left origin: Android top-origin hinge bounds are
            // converted once in FoldableProxy (FoldableLayoutState.FromAndroidTopOrigin) and
            // simulated layouts are authored bottom-origin. Do not flip again here.
            RectInt bounds = layout.foldBounds;
            if (bounds.width <= 0 || bounds.height <= 0)
                return false;

            vertical = posture ==FoldablePosture.Book ||
                       (posture == FoldablePosture.DualPane &&
                        layout.orientation == FoldableOrientation.Vertical) ||
                       bounds.height >= bounds.width;

            if (vertical)
            {
                normalized = (bounds.x + bounds.width * 0.5f) / metricsW;
                thicknessNormalized = bounds.width / (float)metricsW;
            }
            else
            {
                normalized = (bounds.y + bounds.height * 0.5f) / metricsH;
                thicknessNormalized = bounds.height / (float)metricsH;
            }

            normalized = Mathf.Clamp01(normalized);
            thicknessNormalized = Mathf.Clamp(thicknessNormalized, 0.002f, 0.2f);
            return true;
        }

        /// <summary>
        /// Maps a full-screen normalized hinge into a canvas that may only cover one pane.
        /// </summary>
        public static float MapFullScreenNormToCanvasLocal(
            float fullScreenNormalized,
            float canvasAnchorMin,
            float canvasAnchorMax)
        {
            float span = canvasAnchorMax - canvasAnchorMin;
            if (Mathf.Abs(span) < 1e-4f)
                return Mathf.Clamp01(fullScreenNormalized);

            return Mathf.Clamp01((fullScreenNormalized - canvasAnchorMin) / span);
        }

        #endregion

        #region Resolve

        /// <summary>
        /// Resolves hinge: manager layout → Editor session → centered horizontal fallback.
        /// </summary>
        public static bool ResolveHinge(
            out bool vertical,
            out float normalized,
            out float thicknessNormalized)
        {
            if (FoldablePostureSource.IsRuntimeSupported)
            {
                FoldableLayoutState layout = FoldablePostureSource.FindActiveLayoutState();
                if (layout != null &&
                    ComputeFromLayout(layout, out vertical, out normalized, out thicknessNormalized))
                    return true;
            }

#if UNITY_EDITOR
            if (_editorHintActive)
            {
                vertical = _editorHintVertical;
                normalized = _editorHintNormalized;
                thicknessNormalized = _editorHintThicknessNorm;
                return true;
            }
#endif

            vertical = false;
            normalized = 0.5f;
            thicknessNormalized = 0.012f;
            return false;
        }

        #endregion

        #region GUI

        /// <summary>
        /// Pixel rect in a top-left GUI / Game View (y-down) coordinate space.
        /// When <paramref name="thicknessPixels"/> &gt; 0, that absolute width is used;
        /// otherwise thickness comes from <paramref name="thicknessNormalized"/>.
        /// </summary>
        public static Rect ToGuiRect(
            Rect view,
            bool vertical,
            float normalizedFromBottomLeft,
            float thicknessNormalized,
            float thicknessPixels = 0f)
        {
            float thickness;
            if (thicknessPixels > 0f)
                thickness = thicknessPixels;
            else
            {
                thickness = vertical
                    ? Mathf.Max(DefaultThicknessPx, view.width * thicknessNormalized)
                    : Mathf.Max(DefaultThicknessPx, view.height * thicknessNormalized);
            }

            if (vertical)
            {
                float x = view.x + view.width * normalizedFromBottomLeft - thickness * 0.5f;
                return new Rect(x, view.y, thickness, view.height);
            }

            float yFromTop = view.height * (1f - normalizedFromBottomLeft);
            float y = view.y + yFromTop - thickness * 0.5f;
            return new Rect(view.x, y, view.width, thickness);
        }

        #endregion
    }

}
