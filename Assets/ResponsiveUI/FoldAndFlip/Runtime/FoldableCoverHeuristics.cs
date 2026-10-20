using UnityEngine;

namespace FDPlus.Foldable
{
    /// <summary>
    /// Best-effort cover / Closed detection from window sizes when Jetpack has no Closed enum.
    /// See Docs/DETECTION.md.
    /// </summary>
    public static class FoldableCoverHeuristics
    {
        #region Constants

        /// <summary>Current window below this share of the reference area counts as the cover.</summary>
        public const float CoverAreaRatio = 0.70f;
        const float CoverAreaSoftRatio = 0.85f;
        const float CoverEdgeRatio = 0.75f;
        const int FlipCoverMaxLongEdge = 1200;
        const int FlipCoverMaxShortEdge = 900;
        const float FlipCoverMaxAspect = 1.35f;

        #endregion

        #region Detection

        /// <summary>
        /// Uses ratios against window max, session peak, largest display, and Device Kind open size.
        /// Does not treat “no FoldingFeature” alone as Closed.
        /// </summary>
        public static bool InferCoverScreen(FoldableLayoutState layout)
        {
            if (layout == null)
                return false;
            if (layout.isInMultiWindowMode || layout.isInPictureInPictureMode)
                return false;
            if (layout.isCoverScreen)
                return true;

            if (IsCurrentMuchSmallerThanReference(layout))
                return true;

            if (IsSmallerThanKindOpenMetrics(layout))
                return true;

            return IsLikelyFlipCoverPanel(layout);
        }

        /// <summary>
        /// Remember the largest inner/main size while a FoldingFeature is present (or Simulator Opened).
        /// </summary>
        public static void NoteInnerPeak(FoldableLayoutState layout)
        {
            if (layout == null)
                return;

            // Cover / Normal must not replace the remembered inner peak (Expand→Normal, peak wipe).
            if (layout.isCoverScreen || layout.productState == FoldableProductState.Closed)
                return;

            // Only Jetpack Flat / HalfOpened (or Simulator Opened Flat). Do not use productState
            // Opened alone — a stale Opened on cover would grow peak to cover WxH and hide Closed.
            bool onInner =
                layout.state == FoldableState.Flat ||
                layout.state == FoldableState.HalfOpened;

            if (!onInner)
                return;

            GrowPeak(ref layout.peakMaxMetrics, layout.currentMetrics);
            GrowPeak(ref layout.peakMaxMetrics, layout.maxMetrics);
        }

        /// <summary>
        /// Mirror of Java <c>FoldableHelper.isStaleFoldingFeatureForCover</c>. A live
        /// FLAT / HALF_OPENED is stale only when BOTH the window is cover-sized versus the session
        /// peak / built-in display AND the hinge no longer spans the current window along its axis.
        /// Jetpack always reports a live feature spanning the window it was computed for, so a
        /// letterboxed / size-compat window keeps its posture; area alone is never enough.
        /// </summary>
        public static bool IsStaleFoldingFeature(FoldableLayoutState layout) =>
            layout != null && IsStaleFoldingFeature(layout, layout.peakMaxMetrics);

        /// <inheritdoc cref="IsStaleFoldingFeature(FoldableLayoutState)"/>
        /// <param name="rememberedPeak">Extra session peak (e.g. the live state's peak during a native merge).</param>
        public static bool IsStaleFoldingFeature(FoldableLayoutState layout, RectInt rememberedPeak)
        {
            if (layout == null)
                return false;
            if (layout.state != FoldableState.Flat && layout.state != FoldableState.HalfOpened)
                return false;
            if (layout.isInMultiWindowMode || layout.isInPictureInPictureMode)
                return false;

            long currentArea = Area(layout.currentMetrics);
            if (currentArea <= 0)
                return false;

            long refArea = Area(layout.peakMaxMetrics);
            refArea = System.Math.Max(refArea, Area(rememberedPeak));
            refArea = System.Math.Max(refArea, Area(layout.largestDisplayMetrics));
            if (refArea <= 0)
                return false;

            // Same integer 70% rule as Java.
            if (currentArea * 10 >= refArea * 7)
                return false;

            return !FeatureSpansWindow(layout.orientation, layout.foldBounds, layout.currentMetrics);
        }

        /// <summary>
        /// True when a hinge spans the window along its axis (horizontal → full width,
        /// vertical → full height) AND its centre lies inside the window (window-relative bounds).
        /// Unknown geometry counts as spanning (never discard on doubt).
        /// </summary>
        public static bool FeatureSpansWindow(
            FoldableOrientation orientation,
            RectInt feature,
            RectInt window)
        {
            bool horizontal = orientation == FoldableOrientation.Horizontal;
            if (!horizontal && orientation != FoldableOrientation.Vertical)
                return true;

            int featureLength = horizontal ? feature.width : feature.height;
            int windowLength = horizontal ? window.width : window.height;
            if (featureLength <= 0 || windowLength <= 0)
                return true;
            if (featureLength != windowLength)
                return false;

            // Same length is not enough (Razr+: main short side 1080 == cover side 1080). Bounds are
            // window-relative; a live feature's centre lies inside the window, a stale main-display
            // hinge (centre ~1320) falls beyond a 1272 px cover edge. The test is symmetric, so it
            // holds for bottom-origin (converted) and top-origin bounds alike.
            return IsCentreInside(feature.x, feature.xMax, window.width) &&
                   IsCentreInside(feature.y, feature.yMax, window.height);
        }

        static bool IsCentreInside(int start, int end, int windowLength)
        {
            long doubledCentre = (long)start + end;
            return doubledCentre >= 0 && doubledCentre <= 2L * windowLength;
        }

        public static void ApplyCoverFlags(FoldableLayoutState layout)
        {
            if (layout == null)
                return;

            if (layout.isInMultiWindowMode || layout.isInPictureInPictureMode)
            {
                layout.isCoverScreen = false;
                if (layout.productState == FoldableProductState.Closed)
                    layout.productState = FoldableProductState.Undefined;
                return;
            }

            if (layout.productState == FoldableProductState.Closed)
            {
                layout.isCoverScreen = true;
                return;
            }

            // Live Jetpack HALF_OPENED owns Flex — never size-probe into Closed.
            if (layout.state == FoldableState.HalfOpened)
            {
                layout.isCoverScreen = false;
                return;
            }

            // Live Jetpack FLAT is Expand/Opened, except rear-display ACTIVE: device can stay
            // open (FLAT) while the activity is on the cover (Samsung rear display mode).
            // Do not clear isCoverScreen in that case — size-probe is skipped when already cover.
            if (layout.state == FoldableState.Flat && !layout.isCoverScreen)
                return;

            if (layout.productState == FoldableProductState.HalfOpened ||
                layout.productState == FoldableProductState.Opened)
            {
                // Stale product after empty FoldingFeature (Merge cleared state to Undefined).
                layout.isCoverScreen = false;
                if (!InferCoverScreen(layout))
                    return;

                layout.isCoverScreen = true;
                layout.productState = FoldableProductState.Undefined;
                return;
            }

            if (!layout.isCoverScreen)
                layout.isCoverScreen = InferCoverScreen(layout);
        }

        #endregion

        #region Helpers

        /// <summary>Pixel area of <paramref name="rect"/> (negative sides count as 0). Shared session-peak math.</summary>
        internal static long Area(RectInt rect) =>
            (long)Mathf.Max(0, rect.width) * Mathf.Max(0, rect.height);

        static void GrowPeak(ref RectInt peak, RectInt candidate)
        {
            if (Area(candidate) > Area(peak))
                peak = new RectInt(0, 0, candidate.width, candidate.height);
        }

        static bool IsCurrentMuchSmallerThanReference(FoldableLayoutState layout)
        {
            if (!GetLargestReference(layout, out int rw, out int rh))
                return false;

            return IsMuchSmaller(layout.currentMetrics.width, layout.currentMetrics.height, rw, rh);
        }

        static bool IsSmallerThanKindOpenMetrics(FoldableLayoutState layout)
        {
            // Kind open-size is only for a missing FoldingFeature (cover / cold start).
            // Never run during live Flat / HalfOpened — that falsely Closed Flex/Expand.
            if (layout.state != FoldableState.Undefined)
                return false;

            var kind = layout.deviceKind;
            if (kind != FoldableDeviceKind.FoldablePhone && kind != FoldableDeviceKind.FlipablePhone)
                return false;

            if (!FoldableProductStateResolver.GetProductMetrics(
                    kind, FoldableProductState.Opened, out int ow, out int oh))
                return false;

            return IsMuchSmaller(layout.currentMetrics.width, layout.currentMetrics.height, ow, oh);
        }

        static bool GetLargestReference(FoldableLayoutState layout, out int width, out int height)
        {
            width = 0;
            height = 0;
            long best = 0;
            Consider(ref best, ref width, ref height, layout.maxMetrics);
            Consider(ref best, ref width, ref height, layout.peakMaxMetrics);
            Consider(ref best, ref width, ref height, layout.largestDisplayMetrics);
            return best > 0;
        }

        static void Consider(ref long bestArea, ref int width, ref int height, RectInt rect)
        {
            int w = Mathf.Max(0, rect.width);
            int h = Mathf.Max(0, rect.height);
            long area = (long)w * h;
            if (area > bestArea)
            {
                bestArea = area;
                width = w;
                height = h;
            }
        }

        static bool IsMuchSmaller(int cw, int ch, int rw, int rh)
        {
            cw = Mathf.Max(0, cw);
            ch = Mathf.Max(0, ch);
            rw = Mathf.Max(0, rw);
            rh = Mathf.Max(0, rh);
            if (cw <= 0 || ch <= 0 || rw <= 0 || rh <= 0)
                return false;

            long currentArea = (long)cw * ch;
            long refArea = (long)rw * rh;
            if (currentArea < refArea * CoverAreaRatio)
                return true;

            int cShort = Mathf.Min(cw, ch);
            int cLong = Mathf.Max(cw, ch);
            int rShort = Mathf.Min(rw, rh);
            int rLong = Mathf.Max(rw, rh);
            bool narrower = cShort < rShort * CoverEdgeRatio;
            bool notTallerThanRef = cLong <= rLong * 1.2f;
            if (narrower && notTallerThanRef && currentArea < refArea * CoverAreaSoftRatio)
                return true;

            return narrower && cLong < rLong * 0.9f;
        }

        static bool IsLikelyFlipCoverPanel(FoldableLayoutState layout)
        {
            if (layout.state != FoldableState.Undefined)
                return false;

            int cw = Mathf.Max(0, layout.currentMetrics.width);
            int ch = Mathf.Max(0, layout.currentMetrics.height);
            if (cw <= 0 || ch <= 0)
                return false;

            int longEdge = Mathf.Max(cw, ch);
            int shortEdge = Mathf.Min(cw, ch);
            if (longEdge > FlipCoverMaxLongEdge || shortEdge > FlipCoverMaxShortEdge)
                return false;

            float aspect = (float)longEdge / Mathf.Max(1, shortEdge);
            return aspect < FlipCoverMaxAspect;
        }

        #endregion
    }
}
