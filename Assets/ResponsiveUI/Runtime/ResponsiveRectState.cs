using System;
using UnityEngine;

namespace FDPlus.ResponsiveUI
{
    #region Types

    /// <summary>
    /// How design-space widget pixels map onto the live canvas size.
    /// </summary>
    public enum ResponsiveDesignScaleMode
    {
        /// <summary>Uniform scale from width ratio (currentWidth / designWidth).</summary>
        Width = 0,
        /// <summary>Uniform scale from height ratio (currentHeight / designHeight).</summary>
        Height = 1,
        /// <summary>Uniform scale using min(widthRatio, heightRatio) — fit inside.</summary>
        UniformMin = 2,
        /// <summary>Uniform scale using max(widthRatio, heightRatio) — cover / fill.</summary>
        UniformMax = 3,
        /// <summary>Independent X/Y from width and height ratios.</summary>
        IndependentXY = 4,
        /// <summary>
        /// Pick Width or Height from live aspect vs this form's Design Canvas
        /// (default device). Wider than default → Height; taller → Width.
        /// Same fit as UniformMin.
        /// </summary>
        Auto = 5,
    }

    #endregion

    /// <summary>
    /// Snapshot of a <see cref="RectTransform"/> layout for one device + orientation slot.
    /// </summary>
    [Serializable]
    public struct ResponsiveRectState
    {
        #region Built-in defaults

        /// <summary>iPhone 17 Pro Max portrait — built-in Phone fallback.</summary>
        public static readonly Vector2 BuiltinPhoneDesignCanvasSize = new Vector2(1320f, 2868f);

        /// <summary>Galaxy Z Fold8 Cover portrait — built-in Fold Cover fallback.</summary>
        public static readonly Vector2 BuiltinFoldPhoneCoverDesignCanvasSize = new Vector2(1248f, 1972f);

        /// <summary>iPad Pro 13" (M5) portrait — built-in Pad fallback.</summary>
        public static readonly Vector2 BuiltinPadDesignCanvasSize = new Vector2(2064f, 2752f);

        /// <summary>Galaxy Z Fold7 main portrait — built-in Fold HalfOpened fallback.</summary>
        public static readonly Vector2 BuiltinFoldDesignCanvasSize = new Vector2(1968f, 2184f);

        /// <summary>Galaxy Z Fold8 main portrait — built-in Fold Cover HalfOpened fallback.</summary>
        public static readonly Vector2 BuiltinFoldCoverHalfOpenedDesignCanvasSize =
            new Vector2(1848f, 2448f);

        /// <summary>
        /// iPhone Duo inner portrait (2007×2853) — built-in iPhone Duo Seated fallback.
        /// Oriented per slot by screen shape: Seated Portrait (laptop) is tall, Seated
        /// Landscape (book) is wide.
        /// </summary>
        public static readonly Vector2 BuiltinIphoneDuoSeatedDesignCanvasSize =
            new Vector2(2007f, 2853f);

        /// <summary>iPhone Duo cover portrait (1398×2034) — built-in iPhone Duo Cover fallback.</summary>
        public static readonly Vector2 BuiltinIphoneDuoCoverDesignCanvasSize =
            new Vector2(1398f, 2034f);

        /// <summary>
        /// Wide Screen design canvas, stored portrait like every other form (1080×1920); the
        /// Landscape slot orients it to 1920×1080. Design Scale follows the live window size.
        /// </summary>
        public static readonly Vector2 BuiltinWideScreenDesignCanvasSize =
            new Vector2(1080f, 1920f);

        /// <summary>Galaxy Z Flip8 main portrait — built-in Flip HalfOpened fallback.</summary>
        public static readonly Vector2 BuiltinFlipDesignCanvasSize = new Vector2(1080f, 2520f);

        /// <summary>Galaxy Z Flip8 Cover portrait — built-in Flip Cover fallback.</summary>
        public static readonly Vector2 BuiltinFlipCoverSize = new Vector2(948f, 1048f);

        public static readonly Vector2 BuiltinDefaultScale = Vector2.one;

        public const string BuiltinPhoneModelName = "iPhone 17 Pro Max";
        public const string BuiltinFoldPhoneCoverModelName = "Galaxy Z Fold8 Cover";
        public const string BuiltinPadModelName = "iPad Pro 13 M5";
        public const string BuiltinFoldModelName = "Galaxy Z Fold7";
        public const string BuiltinFoldCoverHalfOpenedModelName = "Galaxy Z Fold8";
        public const string BuiltinIphoneDuoCoverModelName = "iPhone Duo Cover";
        public const string BuiltinIphoneDuoSeatedModelName = "iPhone Duo";
        public const string BuiltinWideScreenModelName = "Wide Screen 1920×1080";
        public const string BuiltinFlipModelName = "Galaxy Z Flip8";
        public const string BuiltinFlipCoverModelName = "Galaxy Z Flip8 Cover";

        /// <summary>
        /// Optional override (Editor Project Settings). When null, built-in sizes are used.
        /// </summary>
        /// <remarks>
        /// The provider fields are configuration hooks installed by Editor
        /// <c>[InitializeOnLoad]</c> code, not play-session state. They are intentionally
        /// not cleared on <c>SubsystemRegistration</c>: with domain reload disabled the
        /// installer does not run again, so a reset would drop Project Settings in Play Mode.
        /// </remarks>
        public static Func<ResponsiveDevice.FormFactor, Vector2> DesignCanvasSizeProvider;

        /// <summary>
        /// Optional Flip reference-profile Cover size. Flip Closed maps to Flip Cover
        /// slots; this cover watch is identity on those slots.
        /// </summary>
        public static Func<Vector2> FlipCoverSizeProvider;

        /// <summary>
        /// Optional override (Editor Project Settings). When null, built-in scale is used.
        /// </summary>
        public static Func<ResponsiveDevice.FormFactor, Vector2> DefaultScaleProvider;

        #endregion

        #region Serialized

        [Tooltip("When off, runtime Apply ignores this state and the Inspector hides layout fields.")]
        public bool enabled;

        [Tooltip(
            "When on: anchoredPosition and localScale scale by the canvas ratio. " +
            "Stretched axes compensate their parent anchor span through sizeDelta, so edge offsets and child content " +
            "scale without applying the parent stretch twice. " +
            "Under a Scale With Screen Size CanvasScaler the ratio is canvas-local residual against this slot's Design Canvas.")]
        public bool scaleFromDesignCanvas;

        [Tooltip(
            "Width/Height = uniform axis scale. " +
            "Auto = fit: wider than Design Canvas → Height, taller → Width. " +
            "UniformMin/Max = fit/cover. IndependentXY = separate axes.")]
        public ResponsiveDesignScaleMode designScaleMode;

        [Tooltip(
            "Serialized (0,0) follows Project Settings Design Canvas for this form. " +
            "The Inspector shows that size oriented to the slot; per-slot overrides are not used.")]
        public Vector2 designCanvasSize;

        [Tooltip(
            "Origin localScale XY (default 1,1). " +
            "Design Scale on → final = this × canvas ratio. " +
            "Design Scale off → Apply restores this value.")]
        public Vector2 defaultScale;

        public Vector2 anchorMin;
        public Vector2 anchorMax;
        public Vector2 anchoredPosition;
        public Vector2 sizeDelta;
        public Vector2 pivot;

        #endregion

        public bool HasSameLayout(in ResponsiveRectState other) =>
            enabled == other.enabled &&
            scaleFromDesignCanvas == other.scaleFromDesignCanvas &&
            designScaleMode == other.designScaleMode &&
            designCanvasSize == other.designCanvasSize &&
            defaultScale == other.defaultScale &&
            anchorMin == other.anchorMin &&
            anchorMax == other.anchorMax &&
            anchoredPosition == other.anchoredPosition &&
            sizeDelta == other.sizeDelta &&
            pivot == other.pivot;

        /// <summary>
        /// True when the live rect already matches this design-space layout after Apply
        /// (position × ratio, stretch sizeDelta compensation, expected localScale).
        /// Scene capture uses this so an Apply-scaled HUD is not written back as
        /// Left / Right / Top / Bottom or origin Default Scale.
        /// </summary>
        public bool MatchesApplied(
            RectTransform rt,
            Vector2 layoutRatio,
            Vector2 expectedLocalScaleXy)
        {
            if (rt == null)
                return false;

            Vector2 pos = ScaleDesignPixels(anchoredPosition, layoutRatio);
            Vector2 size = ComputeAppliedSizeDelta(rt, layoutRatio);
            return ApproxLayout(anchorMin, rt.anchorMin)
                && ApproxLayout(anchorMax, rt.anchorMax)
                && ApproxLayout(pos, rt.anchoredPosition)
                && ApproxLayout(size, rt.sizeDelta)
                && ApproxLayout(pivot, rt.pivot)
                && ApproxScale(expectedLocalScaleXy.x, rt.localScale.x)
                && ApproxScale(expectedLocalScaleXy.y, rt.localScale.y);
        }

        static bool ApproxLayout(Vector2 a, Vector2 b) =>
            Mathf.Abs(a.x - b.x) <= 0.05f && Mathf.Abs(a.y - b.y) <= 0.05f;

        static bool ApproxScale(float a, float b) => Mathf.Abs(a - b) <= 1e-3f;

        #region Factory

        /// <summary>
        /// Unity UI RectTransform Reset: center anchors / pivot, position 0, scale 1, Design Scale off.
        /// <see cref="sizeDelta"/> is (0,0) here — slot Reset keeps the authored
        /// Width / Height or combined stretched-axis offsets.
        /// </summary>
        public static ResponsiveRectState CreateDefault(bool enabled = true) =>
            new ResponsiveRectState
            {
                enabled = enabled,
                scaleFromDesignCanvas = false,
                designScaleMode = ResponsiveDesignScaleMode.Auto,
                designCanvasSize = Vector2.zero,
                defaultScale = Vector2.one,
                anchorMin = new Vector2(0.5f, 0.5f),
                anchorMax = new Vector2(0.5f, 0.5f),
                anchoredPosition = Vector2.zero,
                sizeDelta = Vector2.zero,
                pivot = new Vector2(0.5f, 0.5f),
            };

        #endregion

        #region Design canvas defaults

        public static bool IsUnsetDesignCanvasSize(Vector2 size) =>
            size.x <= 0f || size.y <= 0f;

        public static bool IsUnsetDefaultScale(Vector2 scale) =>
            Mathf.Approximately(scale.x, 0f) && Mathf.Approximately(scale.y, 0f);

        public static string GetBuiltinModelName(ResponsiveDevice.FormFactor slotForm) =>
            slotForm switch
            {
                ResponsiveDevice.FormFactor.Pad => BuiltinPadModelName,
                ResponsiveDevice.FormFactor.FoldHalfOpened => BuiltinFoldModelName,
                ResponsiveDevice.FormFactor.FoldCoverHalfOpened => BuiltinFoldCoverHalfOpenedModelName,
                ResponsiveDevice.FormFactor.iPhoneDuoCover => BuiltinIphoneDuoCoverModelName,
                ResponsiveDevice.FormFactor.iPhoneDuoSeated => BuiltinIphoneDuoSeatedModelName,
                ResponsiveDevice.FormFactor.WideScreen => BuiltinWideScreenModelName,
                ResponsiveDevice.FormFactor.FlipHalfOpened => BuiltinFlipModelName,
                ResponsiveDevice.FormFactor.FoldPhoneCover => BuiltinFoldPhoneCoverModelName,
                ResponsiveDevice.FormFactor.FlipPhoneCover => BuiltinFlipCoverModelName,
                _ => BuiltinPhoneModelName,
            };

        public static Vector2 GetBuiltinDesignCanvasSize(ResponsiveDevice.FormFactor slotForm) =>
            slotForm switch
            {
                ResponsiveDevice.FormFactor.Pad => BuiltinPadDesignCanvasSize,
                ResponsiveDevice.FormFactor.FoldHalfOpened => BuiltinFoldDesignCanvasSize,
                ResponsiveDevice.FormFactor.FoldCoverHalfOpened =>
                    BuiltinFoldCoverHalfOpenedDesignCanvasSize,
                ResponsiveDevice.FormFactor.iPhoneDuoCover => BuiltinIphoneDuoCoverDesignCanvasSize,
                ResponsiveDevice.FormFactor.iPhoneDuoSeated =>
                    BuiltinIphoneDuoSeatedDesignCanvasSize,
                ResponsiveDevice.FormFactor.WideScreen => BuiltinWideScreenDesignCanvasSize,
                ResponsiveDevice.FormFactor.FlipHalfOpened => BuiltinFlipDesignCanvasSize,
                ResponsiveDevice.FormFactor.FoldPhoneCover => BuiltinFoldPhoneCoverDesignCanvasSize,
                ResponsiveDevice.FormFactor.FlipPhoneCover => BuiltinFlipCoverSize,
                _ => BuiltinPhoneDesignCanvasSize,
            };

        /// <summary>
        /// Design canvas for <paramref name="slotForm"/> when this slot's size is unset (0,0).
        /// Editor: Project Settings Design Canvas provider when registered.
        /// Player: built-in form-factor sizes (provider is Editor-only).
        /// </summary>
        public static Vector2 GetDefaultDesignCanvasSize(ResponsiveDevice.FormFactor slotForm)
        {
            if (DesignCanvasSizeProvider != null)
            {
                Vector2 size = DesignCanvasSizeProvider(slotForm);
                if (!IsUnsetDesignCanvasSize(size))
                    return size;
            }

            return GetBuiltinDesignCanvasSize(slotForm);
        }

        public static Vector2 GetDefaultFlipCoverSize()
        {
            if (FlipCoverSizeProvider != null)
            {
                Vector2 size = FlipCoverSizeProvider();
                if (size.x > 0f && size.y > 0f)
                    return size;
            }

            return BuiltinFlipCoverSize;
        }

        public static Vector2 GetDefaultScale(ResponsiveDevice.FormFactor slotForm)
        {
            if (DefaultScaleProvider != null)
            {
                Vector2 scale = DefaultScaleProvider(slotForm);
                if (!IsUnsetDefaultScale(scale))
                    return scale;
            }

            return BuiltinDefaultScale;
        }

        /// <summary>Design canvas size, or the default for <paramref name="slotForm"/> when unset.</summary>
        public Vector2 GetResolvedDesignCanvasSize(ResponsiveDevice.FormFactor slotForm) =>
            IsUnsetDesignCanvasSize(designCanvasSize)
                ? GetDefaultDesignCanvasSize(slotForm)
                : designCanvasSize;

        /// <summary>Default scale, or Project / built-in default when unset (0,0).</summary>
        public Vector2 GetResolvedDefaultScale(ResponsiveDevice.FormFactor slotForm) =>
            IsUnsetDefaultScale(defaultScale) ? GetDefaultScale(slotForm) : defaultScale;

        #endregion

        #region Apply

        /// <summary>
        /// Applies layout. <paramref name="layoutRatio"/> scales design-space
        /// <see cref="anchoredPosition"/> and localScale on every axis. Stretched axes compensate
        /// the live parent anchor span through <see cref="sizeDelta"/> so transformed edges still
        /// resolve to their authored design-space offsets.
        /// Always writes XY <see cref="Transform.localScale"/> from <paramref name="localScaleXy"/> (Z preserved).
        /// </summary>
        public void Apply(
            RectTransform rt,
            Vector2 layoutRatio,
            Vector2 localScaleXy)
        {
            if (!enabled)
                return;

            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = ScaleDesignPixels(anchoredPosition, layoutRatio);
            rt.sizeDelta = ComputeAppliedSizeDelta(rt, layoutRatio);

            Vector3 ls = rt.localScale;
            rt.localScale = new Vector3(localScaleXy.x, localScaleXy.y, ls.z);
        }

        /// <summary>
        /// Design Scale on: <c>defaultScale × canvasRatio</c>. Stretched axes compensate
        /// their parent-driven size in <see cref="sizeDelta"/> before this transform scale.
        /// Design Scale off: layout as authored, <c>localScale</c> restored to <c>defaultScale</c> (origin).
        /// </summary>
        public void Apply(
            RectTransform rt,
            Vector2 currentCanvasSize,
            ResponsiveDevice.FormFactor slotForm) =>
            ApplyDirect(rt, ComputeLayoutRatio(currentCanvasSize, slotForm), slotForm);

        public void Apply(
            RectTransform rt,
            Vector2 currentCanvasSize,
            ResponsiveDevice.FormFactor slotForm,
            ResponsiveDevice.Orientation slotOrientation) =>
            ApplyDirect(rt, ComputeLayoutRatio(currentCanvasSize, slotForm, slotOrientation), slotForm);

        void ApplyDirect(RectTransform rt, Vector2 desired, ResponsiveDevice.FormFactor slotForm)
        {
            Vector2 origin = GetResolvedDefaultScale(slotForm);
            if (!scaleFromDesignCanvas)
            {
                Apply(rt, Vector2.one, origin);
                return;
            }

            Vector2 ratio = ResidualRatio(desired, AncestorLocalScale(rt));
            Apply(rt, ratio, Vector2.Scale(origin, ratio));
        }

        /// <summary>
        /// Canvas ratio only (<see cref="Vector2.one"/> when Design Scale is off).
        /// Used for anchoredPosition. Infers design orientation from
        /// <paramref name="currentCanvasSize"/> except when that size is square.
        /// </summary>
        public Vector2 ComputeLayoutRatio(
            Vector2 currentCanvasSize,
            ResponsiveDevice.FormFactor slotForm)
        {
            if (!scaleFromDesignCanvas)
                return Vector2.one;

            return NormalizeLayoutRatio(
                ComputeCanvasRatio(
                    currentCanvasSize,
                    GetResolvedDesignCanvasSize(slotForm),
                    designScaleMode));
        }

        /// <summary>
        /// Canvas ratio using an explicit slot orientation. Square canvases keep that
        /// authored axis mapping instead of being treated as portrait.
        /// </summary>
        public Vector2 ComputeLayoutRatio(
            Vector2 currentCanvasSize,
            ResponsiveDevice.FormFactor slotForm,
            ResponsiveDevice.Orientation slotOrientation)
        {
            if (!scaleFromDesignCanvas)
                return Vector2.one;

            Vector2 design = ResponsiveUI.OrientSizeForSlot(
                GetResolvedDesignCanvasSize(slotForm), slotForm, slotOrientation);
            return NormalizeLayoutRatio(
                ComputeCanvasRatio(
                    currentCanvasSize,
                    design,
                    designScaleMode,
                    matchCurrentOrientation: false));
        }

        /// <summary>
        /// LocalScale XY written on Apply: origin when Design Scale is off;
        /// <c>origin × canvasRatio</c> when on.
        /// </summary>
        public Vector2 ComputeAppliedLocalScale(
            Vector2 currentCanvasSize,
            ResponsiveDevice.FormFactor slotForm)
        {
            Vector2 origin = GetResolvedDefaultScale(slotForm);
            if (!scaleFromDesignCanvas)
                return origin;

            return Vector2.Scale(origin, ComputeLayoutRatio(currentCanvasSize, slotForm));
        }

        public Vector2 ComputeAppliedLocalScale(
            Vector2 currentCanvasSize,
            ResponsiveDevice.FormFactor slotForm,
            ResponsiveDevice.Orientation slotOrientation)
        {
            Vector2 origin = GetResolvedDefaultScale(slotForm);
            if (!scaleFromDesignCanvas)
                return origin;

            return Vector2.Scale(
                origin,
                ComputeLayoutRatio(currentCanvasSize, slotForm, slotOrientation));
        }

        /// <summary>
        /// Compensates stretched anchor spans before localScale applies the design ratio.
        /// The transformed rect then follows the parent stretch while its authored edge
        /// offsets and child content scale in design space.
        /// </summary>
        public Vector2 ComputeAppliedSizeDelta(RectTransform rt, Vector2 layoutRatio)
        {
            Vector2 ratio = NormalizeLayoutRatio(layoutRatio);
            Vector2 applied = sizeDelta;
            Vector2 span = ParentAnchorSpan(rt);
            if (IsHorizontalStretch() && !Mathf.Approximately(ratio.x, 1f))
                applied.x += span.x * (1f / ratio.x - 1f);
            if (IsVerticalStretch() && !Mathf.Approximately(ratio.y, 1f))
                applied.y += span.y * (1f / ratio.y - 1f);
            return applied;
        }

        #endregion

        #region Capture

        public static ResponsiveRectState From(RectTransform rt) =>
            From(rt, ResponsiveDevice.FormFactor.Phone);

        public static ResponsiveRectState From(
            RectTransform rt,
            ResponsiveDevice.FormFactor slotForm) =>
            new ResponsiveRectState
            {
                enabled = true,
                scaleFromDesignCanvas = false,
                designScaleMode = ResponsiveDesignScaleMode.Auto,
                designCanvasSize = Vector2.zero,
                defaultScale = GetDefaultScale(slotForm),
                anchorMin = rt.anchorMin,
                anchorMax = rt.anchorMax,
                anchoredPosition = rt.anchoredPosition,
                sizeDelta = rt.sizeDelta,
                pivot = rt.pivot,
            };

        /// <summary>
        /// Captures a live rect into design-space: position ÷ <paramref name="layoutRatio"/>,
        /// origin <see cref="defaultScale"/> from live localScale ÷ ratio.
        /// </summary>
        public static ResponsiveRectState From(
            RectTransform rt,
            Vector2 layoutRatio,
            ResponsiveDevice.FormFactor slotForm)
        {
            ResponsiveRectState state = From(rt, slotForm);
            Vector2 ratio = NormalizeLayoutRatio(layoutRatio);
            Vector2 liveScale = new Vector2(rt.localScale.x, rt.localScale.y);
            state.defaultScale = UnscaleLivePixels(liveScale, ratio);

            if (!IsIdentityScale(ratio))
            {
                state.anchoredPosition = UnscaleLivePixels(state.anchoredPosition, ratio);
                Vector2 span = state.ParentAnchorSpan(rt);
                if (state.IsHorizontalStretch() && !Mathf.Approximately(ratio.x, 1f))
                    state.sizeDelta.x -= span.x * (1f / ratio.x - 1f);
                if (state.IsVerticalStretch() && !Mathf.Approximately(ratio.y, 1f))
                    state.sizeDelta.y -= span.y * (1f / ratio.y - 1f);
            }

            return state;
        }

        /// <summary>
        /// Copies design-scale toggle / mode from <paramref name="source"/>.
        /// Leaves <see cref="defaultScale"/> unchanged (caller sets origin from live capture).
        /// Design Canvas Size stays (0,0) so every slot follows the catalog default.
        /// </summary>
        public void CopyDesignScaleSettingsFrom(in ResponsiveRectState source)
        {
            scaleFromDesignCanvas = source.scaleFromDesignCanvas;
            designScaleMode = source.designScaleMode;
            designCanvasSize = Vector2.zero;
        }

        #endregion

        #region Design Scale math

        public static bool IsIdentityScale(Vector2 scale) =>
            Mathf.Approximately(scale.x, 1f) && Mathf.Approximately(scale.y, 1f);

        public static Vector2 ScaleDesignPixels(Vector2 designPixels, Vector2 scale)
        {
            Vector2 ratio = NormalizeLayoutRatio(scale);
            return IsIdentityScale(ratio) ? designPixels : Vector2.Scale(designPixels, ratio);
        }

        /// <summary>
        /// Maps invalid or near-identity ratio components to exactly 1 so Apply and
        /// capture use the same algebraic branch.
        /// </summary>
        public static Vector2 NormalizeLayoutRatio(Vector2 ratio) =>
            new Vector2(NormalizeRatioComponent(ratio.x), NormalizeRatioComponent(ratio.y));

        static float NormalizeRatioComponent(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || Mathf.Abs(value) < 1e-5f)
                return 1f;
            return Mathf.Approximately(value, 1f) ? 1f : value;
        }

        /// <summary>
        /// True when both axes of <paramref name="a"/> match <paramref name="b"/>
        /// within <paramref name="relativeTolerance"/> of <paramref name="b"/>.
        /// </summary>
        public static bool ApproximatelyMatchesSize(
            Vector2 a,
            Vector2 b,
            float relativeTolerance = 0.01f)
        {
            if (a.x <= 0f || a.y <= 0f || b.x <= 0f || b.y <= 0f)
                return false;

            float dx = Mathf.Abs(a.x - b.x) / b.x;
            float dy = Mathf.Abs(a.y - b.y) / b.y;
            return dx <= relativeTolerance && dy <= relativeTolerance;
        }

        /// <summary>
        /// Converts Scale With Screen Size canvas-local units into the slot's design
        /// coordinate space. The scaler reference canvas is the identity point, so a
        /// widget authored at scale 1 on the reference device remains scale 1 even when
        /// CanvasScaler reference pixels differ from the Design Canvas profile.
        /// </summary>
        public static Vector2 MapScalerCanvasToDesignSpace(
            Vector2 canvasLocalSize,
            Vector2 scalerReferenceSize,
            Vector2 designSize)
        {
            if (canvasLocalSize.x <= 0f || canvasLocalSize.y <= 0f ||
                scalerReferenceSize.x <= 0f || scalerReferenceSize.y <= 0f ||
                designSize.x <= 0f || designSize.y <= 0f)
                return canvasLocalSize;

            if (ApproximatelyMatchesSize(canvasLocalSize, scalerReferenceSize))
                return designSize;

            return Vector2.Scale(
                canvasLocalSize,
                new Vector2(
                    designSize.x / scalerReferenceSize.x,
                    designSize.y / scalerReferenceSize.y));
        }

        /// <summary>
        /// Removes ancestor transform scale already applied in the same canvas so nested
        /// Design Scale widgets do not compound the canvas ratio. When the canvas ratio
        /// is identity, ancestor authored Default Scale is left alone so every widget
        /// in a list can copy Inspector Default Scale 1:1 onto its Target.
        /// </summary>
        public static Vector2 ResidualRatio(Vector2 desired, Vector2 ancestorScale)
        {
            Vector2 ratio = NormalizeLayoutRatio(desired);
            if (IsIdentityScale(ratio))
                return Vector2.one;

            return NormalizeLayoutRatio(new Vector2(
                Mathf.Abs(ancestorScale.x) < 1e-5f ? ratio.x : ratio.x / ancestorScale.x,
                Mathf.Abs(ancestorScale.y) < 1e-5f ? ratio.y : ratio.y / ancestorScale.y));
        }

        /// <summary>
        /// Nearest <see cref="Canvas"/> (active hierarchy) that contains <paramref name="target"/>,
        /// starting at the <b>parent</b>. A Canvas on the Target itself (override sorting /
        /// batching) is the widget, not the pane it is laid out in, so it is skipped.
        /// </summary>
        public static Canvas GetParentCanvas(Transform target)
        {
            if (target == null)
                return null;

            Transform parent = target.parent;
            return parent != null ? parent.GetComponentInParent<Canvas>() : null;
        }

        /// <summary>
        /// Product of parent <c>localScale</c> between <paramref name="target"/> and
        /// <paramref name="stopAt"/> (exclusive). Defaults to the nearest parent Canvas
        /// (<see cref="GetParentCanvas"/>). A <paramref name="stopAt"/> that is not a
        /// strict ancestor (for example the Target itself) also uses that parent Canvas,
        /// so the walk never multiplies in a root Canvas scale factor.
        /// </summary>
        public static Vector2 AncestorLocalScale(RectTransform target, Transform stopAt = null)
        {
            if (target == null)
                return Vector2.one;

            Transform stop = stopAt;
            if (stop == null || !IsStrictAncestor(target, stop))
            {
                Canvas canvas = GetParentCanvas(target);
                stop = canvas != null ? canvas.transform : null;
            }

            Vector2 scale = Vector2.one;
            for (Transform t = target.parent; t != null && t != stop; t = t.parent)
            {
                scale.x *= t.localScale.x;
                scale.y *= t.localScale.y;
            }

            return scale;
        }

        static bool IsStrictAncestor(Transform target, Transform ancestor)
        {
            for (Transform t = target.parent; t != null; t = t.parent)
            {
                if (t == ancestor)
                    return true;
            }

            return false;
        }

        public bool IsHorizontalStretch() => IsAxisStretched(anchorMin.x, anchorMax.x);

        public bool IsVerticalStretch() => IsAxisStretched(anchorMin.y, anchorMax.y);

        /// <summary>True when an anchor axis spans a non-zero portion of its parent.</summary>
        public static bool IsAxisStretched(float min, float max) => min != max;

        Vector2 ParentAnchorSpan(RectTransform rt)
        {
            if (!(rt.parent is RectTransform parent))
                return Vector2.zero;

            return Vector2.Scale(parent.rect.size, anchorMax - anchorMin);
        }

        public static Vector2 UnscaleLivePixels(Vector2 livePixels, Vector2 scale)
        {
            Vector2 ratio = NormalizeLayoutRatio(scale);
            if (IsIdentityScale(ratio))
                return livePixels;

            return new Vector2(livePixels.x / ratio.x, livePixels.y / ratio.y);
        }

        public static Vector2 ComputeCanvasRatio(
            Vector2 currentSize,
            Vector2 designSize,
            ResponsiveDesignScaleMode mode,
            bool matchCurrentOrientation = true)
        {
            if (IsUnsetDesignCanvasSize(designSize))
                return Vector2.one;
            if (currentSize.x <= 0f || currentSize.y <= 0f)
                return Vector2.one;

            Vector2 design = matchCurrentOrientation
                ? OrientSizeToMatch(designSize, currentSize)
                : designSize;
            float rx = currentSize.x / design.x;
            float ry = currentSize.y / design.y;
            mode = ResolveAutoScaleMode(
                mode, currentSize.x / currentSize.y, design.x / design.y);

            switch (mode)
            {
                case ResponsiveDesignScaleMode.Height:
                    return new Vector2(ry, ry);
                case ResponsiveDesignScaleMode.UniformMin:
                    {
                        float s = Mathf.Min(rx, ry);
                        return new Vector2(s, s);
                    }
                case ResponsiveDesignScaleMode.UniformMax:
                    {
                        float s = Mathf.Max(rx, ry);
                        return new Vector2(s, s);
                    }
                case ResponsiveDesignScaleMode.IndependentXY:
                    return new Vector2(rx, ry);
                case ResponsiveDesignScaleMode.Width:
                default:
                    return new Vector2(rx, rx);
            }
        }

        /// <summary>
        /// Auto compares live aspect to the Design Canvas (default device for that
        /// form). Wider than default → Height (keep vertical span). Taller → Width
        /// (keep horizontal span). Other modes pass through. Same fit as UniformMin.
        /// </summary>
        public static ResponsiveDesignScaleMode ResolveAutoScaleMode(
            ResponsiveDesignScaleMode mode,
            Vector2 currentSize,
            Vector2 designSize,
            bool matchCurrentOrientation = true)
        {
            if (mode != ResponsiveDesignScaleMode.Auto)
                return mode;
            if (currentSize.x <= 0f || currentSize.y <= 0f ||
                designSize.x <= 0f || designSize.y <= 0f)
                return ResponsiveDesignScaleMode.Width;

            Vector2 design = matchCurrentOrientation
                ? OrientSizeToMatch(designSize, currentSize)
                : designSize;
            return ResolveAutoScaleMode(
                mode, currentSize.x / currentSize.y, design.x / design.y);
        }

        public static ResponsiveDesignScaleMode ResolveAutoScaleMode(
            ResponsiveDesignScaleMode mode,
            float liveAspect,
            float designAspect)
        {
            if (mode != ResponsiveDesignScaleMode.Auto)
                return mode;
            if (liveAspect < 1e-5f || designAspect < 1e-5f)
                return ResponsiveDesignScaleMode.Width;

            return liveAspect > designAspect
                ? ResponsiveDesignScaleMode.Height
                : ResponsiveDesignScaleMode.Width;
        }

        /// <summary>
        /// Swap design WxH when it disagrees with the live size orientation. A square live
        /// size keeps the authored mapping. Shared by widget Design Scale and camera framing.
        /// </summary>
        internal static Vector2 OrientSizeToMatch(Vector2 design, Vector2 current)
        {
            if (Mathf.Approximately(current.x, current.y))
                return design;

            bool designLandscape = design.x > design.y;
            bool currentLandscape = current.x > current.y;
            if (designLandscape == currentLandscape)
                return design;
            return new Vector2(design.y, design.x);
        }

        #endregion
    }

}
