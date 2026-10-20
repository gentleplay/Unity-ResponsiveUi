using System;
using UnityEngine;
using UnityEngine.UI;

namespace FDPlus.ResponsiveUI
{
    #region Types

    /// <summary>
    /// How a background Image fills its parent. Serialized as <c>coverMode</c> ints:
    /// append new modes only — existing values stay stable.
    /// </summary>
    public enum ResponsiveBgCoverMode
    {
        /// <summary>Keep art aspect; scale to fill the parent (may crop).</summary>
        Cover = 0,
        /// <summary>
        /// Same vertical cover as Cover. Full-width stretch anchors clear Left/Right
        /// (<c>sizeDelta.x = 0</c>); otherwise keeps the current <c>sizeDelta.x</c>.
        /// </summary>
        HeightOnly = 1,
        /// <summary>Keep art aspect; scale to fit inside the parent (may letterbox).</summary>
        Fit = 2,
        /// <summary>Ignore art aspect; stretch both axes to the parent.</summary>
        Stretch = 3,
        /// <summary>
        /// Same horizontal cover as Cover. Full-height stretch anchors clear Top/Bottom
        /// (<c>sizeDelta.y = 0</c>); otherwise keeps the current <c>sizeDelta.y</c>.
        /// </summary>
        WidthOnly = 4,
        /// <summary>Keep art aspect; scale so width matches the parent (height may overflow).</summary>
        FitWidth = 5,
        /// <summary>Keep art aspect; scale so height matches the parent (width may overflow).</summary>
        FitHeight = 6,
        /// <summary>Leave sizeDelta alone (authored / hand-tuned).</summary>
        Manual = 7,
    }

    [Serializable]
    public struct ResponsiveBackground
    {
        public Image image;
        [Tooltip(
            "Match Mode (Inspector order): Cover → Fit → Stretch → Fit Width → Fit Height → " +
            "Width Only → Height Only → Manual. Cover / Fit / Fit Width / Fit Height keep sprite " +
            "aspect (Project Settings size if no sprite). Manual leaves sizeDelta alone.")]
        public ResponsiveBgCoverMode coverMode;
    }

    #endregion

    /// <summary>
    /// Background Image Match Mode math: sizes an Image against its parent rect (Cover / Fit /
    /// Stretch / Fit Width / Fit Height / Width Only / Height Only; Manual leaves it alone).
    /// <see cref="ResponsiveUiController"/> backgrounds use it with the hub's art size
    /// (<see cref="ResponsiveUI.GetOriginalBgSize"/>).
    /// </summary>
    public static class ResponsiveBackgroundCover
    {
        #region Cover

        public static bool GetCoverParentSize(Image bg, out Vector2 parentSize)
        {
            parentSize = default;
            if (bg == null)
                return false;
            if (bg.rectTransform.parent is not RectTransform parent)
                return false;

            parentSize = parent.rect.size;
            if ((parentSize.x <= 0f || parentSize.y <= 0f) &&
                !parent.gameObject.activeInHierarchy)
            {
                parentSize = EstimateInactiveRectSize(parent, 0);
            }
            return parentSize.x > 0f && parentSize.y > 0f;
        }

        const int MaxInactiveRectWalk = 8;

        /// <summary>
        /// Edit Mode size estimate for an inactive rect whose <c>rect</c> is not laid out.
        /// <c>sizeDelta</c> is the size only on a non-stretched axis; a stretched axis is
        /// the parent's size × anchor span + <c>sizeDelta</c> (walks up).
        /// </summary>
        static Vector2 EstimateInactiveRectSize(RectTransform rt, int depth)
        {
            Vector2 size = rt.rect.size;
            if (size.x > 0f && size.y > 0f)
                return size;

            Vector2 span = rt.anchorMax - rt.anchorMin;
            bool stretchX = ResponsiveRectState.IsAxisStretched(rt.anchorMin.x, rt.anchorMax.x);
            bool stretchY = ResponsiveRectState.IsAxisStretched(rt.anchorMin.y, rt.anchorMax.y);
            Vector2 parentSize = Vector2.zero;
            if (((stretchX && size.x <= 0f) || (stretchY && size.y <= 0f)) &&
                depth < MaxInactiveRectWalk &&
                rt.parent is RectTransform parent)
            {
                parentSize = EstimateInactiveRectSize(parent, depth + 1);
            }

            if (size.x <= 0f)
                size.x = EstimateAxis(stretchX, span.x, parentSize.x, rt.sizeDelta.x);
            if (size.y <= 0f)
                size.y = EstimateAxis(stretchY, span.y, parentSize.y, rt.sizeDelta.y);
            return size;
        }

        static float EstimateAxis(bool stretched, float span, float parentAxis, float sizeDelta)
        {
            if (!stretched)
                return sizeDelta;
            return parentAxis > 0f ? parentAxis * span + sizeDelta : 0f;
        }

        /// <summary>
        /// Art size for Match Mode ratio. Prefers the Image sprite pixels so Cover / Fit
        /// keep the image aspect; falls back to Project Settings / Override size.
        /// </summary>
        public static Vector2 ResolveArtSize(Image bg, Vector2 fallback)
        {
            if (bg != null && bg.sprite != null)
            {
                Rect rect = bg.sprite.rect;
                if (rect.width > 0f && rect.height > 0f)
                    return new Vector2(rect.width, rect.height);
            }

            return fallback;
        }

        /// <summary>
        /// Apply Match Mode to the Image for <paramref name="parentSize"/>.
        /// <paramref name="originalBgSize"/> is used when the Image has no sprite.
        /// Manual leaves sizeDelta unchanged.
        /// </summary>
        public static void ApplyBackground(
            Image bg,
            Vector2 originalBgSize,
            Vector2 parentSize,
            ResponsiveBgCoverMode mode)
        {
            if (bg == null)
                return;
            if (mode == ResponsiveBgCoverMode.Manual)
                return;
            if (parentSize.x <= 0f || parentSize.y <= 0f)
                return;

            Vector2 artSize = ResolveArtSize(bg, originalBgSize);
            if (artSize.x <= 0f || artSize.y <= 0f)
                return;

            switch (mode)
            {
                case ResponsiveBgCoverMode.HeightOnly:
                    ApplyCoverHeight(bg, artSize, parentSize);
                    return;
                case ResponsiveBgCoverMode.WidthOnly:
                    ApplyCoverWidth(bg, artSize, parentSize);
                    return;
            }

            RectTransform rt = bg.rectTransform;
            Vector2 displaySize = ComputeDisplaySize(artSize, parentSize, mode);
            Vector2 anchorSpan = rt.anchorMax - rt.anchorMin;
            rt.sizeDelta = displaySize - Vector2.Scale(anchorSpan, parentSize);
        }

        /// <summary>
        /// Same cover height as Cover. Stretch-width anchors reset Left/Right to fill;
        /// otherwise keeps the current <c>sizeDelta.x</c>.
        /// </summary>
        public static void ApplyCoverHeight(Image bg, Vector2 artSize, Vector2 parentSize)
        {
            if (bg == null || artSize.x <= 0f || artSize.y <= 0f)
                return;

            RectTransform rt = bg.rectTransform;
            float displayHeight = artSize.y * CoverScale(artSize, parentSize);
            float anchorSpanX = rt.anchorMax.x - rt.anchorMin.x;
            float anchorSpanY = rt.anchorMax.y - rt.anchorMin.y;
            float sizeDeltaY = displayHeight - anchorSpanY * parentSize.y;
            // Full-stretch width: clear leftover Fit letterbox X so HeightOnly fills width.
            float sizeDeltaX = Mathf.Abs(anchorSpanX - 1f) < 0.0001f
                ? 0f
                : rt.sizeDelta.x;
            rt.sizeDelta = new Vector2(sizeDeltaX, sizeDeltaY);
        }

        /// <summary>
        /// Same cover width as Cover. Stretch-height anchors reset Top/Bottom to fill;
        /// otherwise keeps the current <c>sizeDelta.y</c>.
        /// </summary>
        public static void ApplyCoverWidth(Image bg, Vector2 artSize, Vector2 parentSize)
        {
            if (bg == null || artSize.x <= 0f || artSize.y <= 0f)
                return;

            RectTransform rt = bg.rectTransform;
            float displayWidth = artSize.x * CoverScale(artSize, parentSize);
            float anchorSpanX = rt.anchorMax.x - rt.anchorMin.x;
            float anchorSpanY = rt.anchorMax.y - rt.anchorMin.y;
            float sizeDeltaX = displayWidth - anchorSpanX * parentSize.x;
            // Full-stretch height: clear leftover Fit letterbox Y so WidthOnly fills height.
            float sizeDeltaY = Mathf.Abs(anchorSpanY - 1f) < 0.0001f
                ? 0f
                : rt.sizeDelta.y;
            rt.sizeDelta = new Vector2(sizeDeltaX, sizeDeltaY);
        }

        public static Vector2 ComputeDisplaySize(
            Vector2 artSize,
            Vector2 parentSize,
            ResponsiveBgCoverMode mode)
        {
            switch (mode)
            {
                case ResponsiveBgCoverMode.Stretch:
                    return parentSize;
                case ResponsiveBgCoverMode.Fit:
                    return artSize * FitScale(artSize, parentSize);
                case ResponsiveBgCoverMode.FitWidth:
                    return artSize * (parentSize.x / artSize.x);
                case ResponsiveBgCoverMode.FitHeight:
                    return artSize * (parentSize.y / artSize.y);
                case ResponsiveBgCoverMode.Manual:
                    return Vector2.zero;
                // HeightOnly / WidthOnly Apply write one axis; shared math matches Cover.
                case ResponsiveBgCoverMode.HeightOnly:
                case ResponsiveBgCoverMode.WidthOnly:
                default:
                    return artSize * CoverScale(artSize, parentSize);
            }
        }

        static float CoverScale(Vector2 artSize, Vector2 parentSize) =>
            Mathf.Max(parentSize.x / artSize.x, parentSize.y / artSize.y);

        static float FitScale(Vector2 artSize, Vector2 parentSize) =>
            Mathf.Min(parentSize.x / artSize.x, parentSize.y / artSize.y);

        #endregion
    }
}
