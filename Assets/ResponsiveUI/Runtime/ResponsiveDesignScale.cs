using UnityEngine;

namespace FDPlus.ResponsiveUI
{
    /// <summary>
    /// Design Scale identity: when the live watch is a slot's default-profile reference
    /// (catalog Design Canvas, or a posture-owned Fold / Flip / iPhone Duo screen), live size
    /// becomes the Design Canvas so ratio is 1 and Inspector values copy 1:1. Widgets
    /// (<see cref="ResponsiveUI.GetLayoutRatio"/>), cameras, tutorial offsets, and text banners
    /// share these rules.
    /// </summary>
    public static class ResponsiveDesignScale
    {
        #region Identity

        /// <summary>
        /// Watch matches this slot's Design Canvas, or a product-state screen that
        /// shares the slot. Fold/Flip <b>Opened (Flat)</b> and <b>HalfOpened</b> are
        /// posture-owned: identity follows the detector / Simulator product state
        /// without Screen WxH ≈ catalog. Cover Closed still uses size helpers
        /// (Jetpack has no CLOSED). Widgets, cameras, tutorial offsets, and text
        /// banners all use this so default-profile Inspector values copy 1:1.
        /// <paramref name="orientation"/> is the slot orientation (the screen shape it shows is
        /// <see cref="ResponsiveDevice.OrientationForForm"/>);
        /// <paramref name="orientedDesignSize"/> is already in that shape.
        /// </summary>
        public static bool IsDesignCanvasReferenceWatch(
            ResponsiveDevice.FormFactor deviceForm,
            Vector2 watch,
            ResponsiveDevice.Orientation orientation,
            Vector2 orientedDesignSize)
        {
            Vector2 orientedWatch = ResponsiveUI.OrientSizeForSlot(watch, deviceForm, orientation);
            if (ResponsiveRectState.ApproximatelyMatchesSize(
                    orientedWatch, orientedDesignSize))
                return true;

            // Flat / HalfOpened already selected this slot — do not require catalog WxH.
            if (ResponsiveDevice.IsPostureOwnedDesignIdentity(
                    deviceForm, watch.x, watch.y))
                return true;

            if (deviceForm == ResponsiveDevice.FormFactor.FoldPhoneCover &&
                ResponsiveDevice.IsKnownFoldCoverPhoneSize(watch.x, watch.y))
                return true;

            if (deviceForm == ResponsiveDevice.FormFactor.FlipPhoneCover)
            {
                if (ResponsiveDevice.IsKnownFlipCoverPhoneSize(watch.x, watch.y))
                    return true;

                Vector2 flipCover = ResponsiveUI.OrientSize(
                    ResponsiveRectState.GetDefaultFlipCoverSize(),
                    orientation);
                if (ResponsiveRectState.ApproximatelyMatchesSize(
                        orientedWatch, flipCover))
                    return true;
            }

            // Fold Expand / Dual Opened map to Pad slots; Design Canvas is iPad,
            // but the foldable default profile is Fold inner (Fold7 or Fold8),
            // including Simulator / OEM safe-area when posture is not yet live.
            if (deviceForm == ResponsiveDevice.FormFactor.Pad)
            {
                Vector2 foldInner = ResponsiveUI.OrientSize(
                    ResponsiveRectState.GetDefaultDesignCanvasSize(
                        ResponsiveDevice.FormFactor.FoldHalfOpened),
                    orientation);
                if (ResponsiveRectState.ApproximatelyMatchesSize(
                        orientedWatch, foldInner))
                    return true;

                Vector2 foldCoverInner = ResponsiveUI.OrientSize(
                    ResponsiveRectState.GetDefaultDesignCanvasSize(
                        ResponsiveDevice.FormFactor.FoldCoverHalfOpened),
                    orientation);
                if (ResponsiveRectState.ApproximatelyMatchesSize(
                        orientedWatch, foldCoverInner))
                    return true;

                // Fold inner, and the iPhone Duo inner (flat, before the first hinge report,
                // and as the Disabled iPhone Duo Seated fallback: same panel → same Pad scale).
                if (ResponsiveDevice.IsKnownInnerPadSize(watch.x, watch.y))
                    return true;
            }

            if (deviceForm == ResponsiveDevice.FormFactor.FoldHalfOpened &&
                ResponsiveDevice.IsKnownFoldPhoneInnerSize(watch.x, watch.y))
                return true;

            if (deviceForm == ResponsiveDevice.FormFactor.FoldCoverHalfOpened &&
                ResponsiveDevice.IsKnownFoldCoverHalfOpenedSize(watch.x, watch.y))
                return true;

            // The Duo form already implies a player that matches Duo sizes (never Android).
            if (deviceForm == ResponsiveDevice.FormFactor.iPhoneDuoSeated &&
                ResponsiveDevice.IsKnownIphoneDuoInnerSize(watch.x, watch.y))
                return true;

            // Flip Opened maps to Phone slots. Design Canvas is iPhone; default-profile
            // watch is Flip main. Flip Closed uses Flip Cover slots.
            // Size-only fallback: when posture is live it already decided above. A Phone
            // slot reached while posture says HalfOpened / Expand is a Disabled-slot Phone
            // fallback, which keeps the Phone Design Canvas (not identity).
            if (deviceForm == ResponsiveDevice.FormFactor.Phone &&
                !ResponsiveDevice.ResolveOpenedOrHalfOpenedForm(out _, out _))
            {
                Vector2 flipMain = ResponsiveUI.OrientSize(
                    ResponsiveRectState.GetDefaultDesignCanvasSize(
                        ResponsiveDevice.FormFactor.FlipHalfOpened),
                    orientation);
                if (ResponsiveRectState.ApproximatelyMatchesSize(
                        orientedWatch, flipMain))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Live size for Design Scale / camera UI scale. On a default-profile
        /// watch the live size becomes the slot Design Canvas so ratio is 1.
        /// </summary>
        public static Vector2 ResolveIdentityLiveSize(
            Vector2 liveSize,
            ResponsiveDevice.FormFactor deviceForm,
            ResponsiveDevice.Orientation orientation,
            Vector2 orientedDesignSize)
        {
            if (liveSize.x <= 0f || liveSize.y <= 0f ||
                orientedDesignSize.x <= 0f || orientedDesignSize.y <= 0f)
                return liveSize;

            return IsDesignCanvasReferenceWatch(
                    deviceForm, liveSize, orientation, orientedDesignSize)
                ? orientedDesignSize
                : liveSize;
        }

        /// <summary>
        /// Target-aware identity resolution. A constrained nested Canvas uses its
        /// pane rect instead of the full default-profile watch.
        /// </summary>
        public static Vector2 ResolveIdentityLiveSize(
            Vector2 liveSize,
            ResponsiveDevice.FormFactor deviceForm,
            ResponsiveDevice.Orientation orientation,
            Vector2 orientedDesignSize,
            RectTransform target)
        {
            if (GetConstrainedCanvasSize(target, out Vector2 paneSize))
                return paneSize;

            return ResolveIdentityLiveSize(
                liveSize, deviceForm, orientation, orientedDesignSize);
        }

        /// <summary>
        /// True when <paramref name="target"/> sits in a sized non-root Canvas pane.
        /// The lookup starts at the Target's parent, so a Canvas on the Target itself
        /// (override sorting / batching) is not mistaken for the pane.
        /// </summary>
        internal static bool GetConstrainedCanvasSize(
            RectTransform target,
            out Vector2 size)
        {
            if (target != null)
            {
                Canvas targetCanvas = ResponsiveRectState.GetParentCanvas(target);
                if (targetCanvas != null && !targetCanvas.isRootCanvas &&
                    targetCanvas.transform is RectTransform targetCanvasRt)
                {
                    size = targetCanvasRt.rect.size;
                    if (size.x > 0f && size.y > 0f)
                        return true;
                }
            }

            size = default;
            return false;
        }

        #endregion
    }
}
