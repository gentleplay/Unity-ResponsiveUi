using System;
using UnityEngine;

namespace FDPlus.Foldable
{
    /// <summary>
    /// Snapshot of fold / flip layout signals (WM + product-layer fields).
    /// </summary>
    [Serializable]
    public class FoldableLayoutState
    {
        #region Serialized

        public FoldableState state = FoldableState.Undefined;
        public FoldableOrientation orientation = FoldableOrientation.Undefined;
        public FoldableOcclusionType occlusionType = FoldableOcclusionType.Undefined;
        public bool isSeparating;
        /// <summary>
        /// Hinge bounds in window pixels, Unity bottom-left origin (y grows upward). Android
        /// top-origin bounds are converted once in <see cref="FoldableProxy"/>
        /// (<see cref="FromAndroidTopOrigin"/>); simulated layouts are authored bottom-origin.
        /// </summary>
        public RectInt foldBounds;
        public RectInt currentMetrics = new RectInt(0, 0, 1080, 2316);
        public RectInt maxMetrics;
        /// <summary>Largest window metrics seen while a FoldingFeature was present (session peak).</summary>
        public RectInt peakMaxMetrics;
        /// <summary>Largest physical display reported by the device (may include inner while on cover).</summary>
        public RectInt largestDisplayMetrics;
        public bool needsUpdate;
        /// <summary>True while Android reports the activity in multi-window mode.</summary>
        public bool isInMultiWindowMode;
        /// <summary>True while Android reports the activity in picture-in-picture mode.</summary>
        public bool isInPictureInPictureMode;

        public FoldableDeviceKind deviceKind = FoldableDeviceKind.Unknown;
        public bool isCoverScreen;
        public FoldableProductState productState = FoldableProductState.Undefined;

        #endregion

        #region Detection

        public FoldablePosture ResolvePosture()
        {
            if (isCoverScreen)
                return FoldablePosture.Flat;

            // Spanned / Expand / Opened = use the full surface.
            // WindowManager isSeparating means a physical hinge exists; it must NOT
            // force Book/DualPane UI (that is Flex / HalfOpened only).
            if (productState == FoldableProductState.Opened ||
                productState == FoldableProductState.Closed)
                return FoldablePosture.Flat;

            if (state == FoldableState.HalfOpened ||
                productState == FoldableProductState.HalfOpened)
            {
                return orientation == FoldableOrientation.Vertical
                    ? FoldablePosture.Book
                    : FoldablePosture.Tabletop;
            }

            // Raw WM path when product state is unset: separating + hinge axis.
            if (isSeparating && state == FoldableState.Flat)
            {
                return orientation == FoldableOrientation.Vertical
                    ? FoldablePosture.Book
                    : FoldablePosture.DualPane;
            }

            return FoldablePosture.Flat;
        }

        public FoldableProductState ResolveProductState() =>
            FoldableProductStateResolver.Resolve(this);

        public string GetProductStateDisplayName() =>
            FoldableProductStateResolver.GetDisplayName(deviceKind, ResolveProductState());

        #endregion

        #region Helpers

        public void CopyExact(FoldableLayoutState other)
        {
            if (other == null)
                return;

            state = other.state;
            orientation = other.orientation;
            occlusionType = other.occlusionType;
            isSeparating = other.isSeparating;
            foldBounds = other.foldBounds;
            currentMetrics = other.currentMetrics;
            maxMetrics = other.maxMetrics;
            peakMaxMetrics = other.peakMaxMetrics;
            largestDisplayMetrics = other.largestDisplayMetrics;
            needsUpdate = other.needsUpdate;
            isInMultiWindowMode = other.isInMultiWindowMode;
            isInPictureInPictureMode = other.isInPictureInPictureMode;
            deviceKind = other.deviceKind;
            isCoverScreen = other.isCoverScreen;
            productState = other.productState;
        }

        /// <summary>
        /// Merges a transient Android snapshot while preserving live hinge/device history
        /// omitted when WindowManager temporarily reports no FoldingFeature.
        /// </summary>
        public void MergeNativeSnapshot(FoldableLayoutState other)
        {
            if (other == null)
                return;

            // Native snapshots use Undefined / empty hinge when FoldingFeature is missing
            // (Unity/Jetpack: empty on closed foldables AND non-foldables). Keep hinge bounds,
            // orientation, Device Kind, and session peak so Flex can be restored — but clear
            // Flat and HalfOpened. Stale Flat → false Expand/Pad on cover; stale HalfOpened →
            // false Flex on cover (kind open-size cover detection requires Undefined).
            // ShouldPreserveHalfOpened restores Flex when not Closed; cover heuristics win Normal.
            // Defense in depth with Java: a lagging FoldingFeature on a cover-sized window that the
            // hinge no longer spans is treated as missing (letterboxed live postures are kept).
            bool liveFeature =
                other.state != FoldableState.Undefined &&
                !FoldableCoverHeuristics.IsStaleFoldingFeature(other, peakMaxMetrics);
            bool windowModeSurface = other.isInMultiWindowMode || other.isInPictureInPictureMode;

            if (liveFeature)
            {
                state = other.state;
                isSeparating = other.isSeparating;
            }
            else if (state == FoldableState.Flat || state == FoldableState.HalfOpened)
            {
                state = FoldableState.Undefined;
                isSeparating = false;
            }

            if (liveFeature && other.orientation != FoldableOrientation.Undefined)
                orientation = other.orientation;
            if (liveFeature && other.occlusionType != FoldableOcclusionType.Undefined)
                occlusionType = other.occlusionType;

            if (!liveFeature && windowModeSurface)
            {
                // Split-screen / PiP without a feature: the old hinge is not in this window.
                foldBounds = default;
                orientation = FoldableOrientation.Undefined;
                occlusionType = FoldableOcclusionType.Undefined;
            }
            else if (liveFeature || other.state == FoldableState.Undefined)
            {
                // A stale (discarded) feature keeps the remembered hinge, like a missing one.
                if (HasArea(other.foldBounds) || !HasArea(foldBounds))
                    foldBounds = other.foldBounds;
            }
            currentMetrics = other.currentMetrics;
            maxMetrics = other.maxMetrics;
            // Keep the larger session peak across updates (cover launches may report peak=0 briefly).
            if (FoldableCoverHeuristics.Area(other.peakMaxMetrics) >=
                FoldableCoverHeuristics.Area(peakMaxMetrics))
                peakMaxMetrics = other.peakMaxMetrics;
            largestDisplayMetrics = other.largestDisplayMetrics;
            needsUpdate = other.needsUpdate;
            isInMultiWindowMode = other.isInMultiWindowMode;
            isInPictureInPictureMode = other.isInPictureInPictureMode;
            if (other.deviceKind != FoldableDeviceKind.Unknown)
                deviceKind = other.deviceKind;
            isCoverScreen = other.isCoverScreen;
            productState = other.productState;
        }

        public FoldableLayoutState CreateSnapshot()
        {
            var snapshot = new FoldableLayoutState();
            snapshot.CopyExact(this);
            return snapshot;
        }

        /// <summary>
        /// Compares layout content while ignoring the transient <see cref="needsUpdate"/> flag.
        /// </summary>
        public bool ContentEquals(FoldableLayoutState other) =>
            other != null &&
            state == other.state &&
            orientation == other.orientation &&
            occlusionType == other.occlusionType &&
            isSeparating == other.isSeparating &&
            foldBounds == other.foldBounds &&
            currentMetrics == other.currentMetrics &&
            maxMetrics == other.maxMetrics &&
            peakMaxMetrics == other.peakMaxMetrics &&
            largestDisplayMetrics == other.largestDisplayMetrics &&
            isInMultiWindowMode == other.isInMultiWindowMode &&
            isInPictureInPictureMode == other.isInPictureInPictureMode &&
            deviceKind == other.deviceKind &&
            isCoverScreen == other.isCoverScreen &&
            productState == other.productState;

        /// <summary>
        /// Converts Android window bounds (top-left origin, y down) into Unity bottom-left origin:
        /// <c>y' = windowHeight - y - h</c>. Empty / invalid rects are returned unchanged.
        /// Pure integer math (safe on the Java callback thread).
        /// </summary>
        public static RectInt FromAndroidTopOrigin(RectInt topOrigin, int windowHeight)
        {
            if (windowHeight <= 0 || topOrigin.height < 0 || topOrigin.width < 0 ||
                (topOrigin.width == 0 && topOrigin.height == 0))
                return topOrigin;

            return new RectInt(
                topOrigin.x,
                windowHeight - topOrigin.y - topOrigin.height,
                topOrigin.width,
                topOrigin.height);
        }

        static bool HasArea(RectInt rect) =>
            rect.width > 0 && rect.height > 0;

        #endregion

        #region Simulator

        /// <summary>
        /// Full-window Flat / Opened layout (no hinge split) for <paramref name="kind"/>, used when
        /// leaving a Fold / Flip simulator device.
        /// </summary>
        public static FoldableLayoutState CreateFlat(int width, int height, FoldableDeviceKind kind)
        {
            var flat = CreateSimulated(
                FoldableState.Flat,
                FoldableOrientation.Horizontal,
                width,
                height,
                hingeNormalized: 0.5f,
                hingeThicknessPx: 24,
                isSeparating: false);
            flat.deviceKind = kind;
            flat.productState = FoldableProductState.Opened;
            return flat;
        }

        public static FoldableLayoutState CreateSimulated(
            FoldableState state,
            FoldableOrientation orientation,
            int width,
            int height,
            float hingeNormalized,
            int hingeThicknessPx,
            bool isSeparating = false)
        {
            if (state == FoldableState.Undefined)
                state = FoldableState.Flat;
            if (orientation == FoldableOrientation.Undefined)
                orientation = FoldableOrientation.Horizontal;

            width = Mathf.Max(1, width);
            height = Mathf.Max(1, height);
            hingeNormalized = Mathf.Clamp01(hingeNormalized);
            hingeThicknessPx = Mathf.Clamp(hingeThicknessPx, 1, Mathf.Min(width, height));

            var layout = new FoldableLayoutState
            {
                state = state,
                orientation = orientation,
                occlusionType = FoldableOcclusionType.None,
                isSeparating = isSeparating,
                currentMetrics = new RectInt(0, 0, width, height),
                maxMetrics = new RectInt(0, 0, width, height),
                needsUpdate = true
            };

            if (orientation == FoldableOrientation.Vertical)
            {
                int x = Mathf.Clamp(
                    Mathf.RoundToInt(width * hingeNormalized) - hingeThicknessPx / 2,
                    0,
                    width - hingeThicknessPx);
                layout.foldBounds = new RectInt(x, 0, hingeThicknessPx, height);
            }
            else
            {
                int y = Mathf.Clamp(
                    Mathf.RoundToInt(height * hingeNormalized) - hingeThicknessPx / 2,
                    0,
                    height - hingeThicknessPx);
                layout.foldBounds = new RectInt(0, y, width, hingeThicknessPx);
            }

            layout.productState = FoldableProductStateResolver.Resolve(layout);
            return layout;
        }

        #endregion
    }
}
