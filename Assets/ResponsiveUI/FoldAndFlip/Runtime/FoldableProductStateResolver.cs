using System;
using UnityEngine;

namespace FDPlus.Foldable
{
    /// <summary>
    /// Maps WindowManager signals (+ cover/inner) to product states, and the reverse for simulators.
    /// Cover size heuristics live in <see cref="FoldableCoverHeuristics"/>.
    /// </summary>
    public static class FoldableProductStateResolver
    {
        #region Detection

        public static FoldableProductState Resolve(FoldableLayoutState layout)
        {
            if (layout == null)
                return FoldableProductState.Undefined;

            if (layout.isCoverScreen &&
                !layout.isInMultiWindowMode &&
                !layout.isInPictureInPictureMode)
                return FoldableProductState.Closed;

            if (layout.state == FoldableState.HalfOpened)
                return FoldableProductState.HalfOpened;

            // Dual-screen: single pane has no separating hinge (Flat or no FoldingFeature).
            // Spanned sets isSeparating.
            if (layout.deviceKind == FoldableDeviceKind.DualScreen && !layout.isSeparating)
                return FoldableProductState.Closed;

            if (layout.state == FoldableState.Flat)
                return FoldableProductState.Opened;

            // A configured detector on an ordinary Android device also receives an
            // empty WindowLayoutInfo. No FoldingFeature is not evidence of Opened.
            return FoldableProductState.Undefined;
        }

        /// <inheritdoc cref="FoldableCoverHeuristics.InferCoverScreen"/>
        public static bool InferCoverScreen(FoldableLayoutState layout) =>
            FoldableCoverHeuristics.InferCoverScreen(layout);

        public static void Enrich(FoldableLayoutState layout, FoldableDeviceKind kindHint)
        {
            if (layout == null)
                return;

            if (layout.deviceKind == FoldableDeviceKind.Unknown && kindHint != FoldableDeviceKind.Unknown)
                layout.deviceKind = kindHint;

            FoldableCoverHeuristics.NoteInnerPeak(layout);
            FoldableCoverHeuristics.ApplyCoverFlags(layout);

            if (layout.productState == FoldableProductState.Undefined)
                layout.productState = Resolve(layout);

            // Google: HALF_OPENED always separates the window into two logical areas.
            if (layout.productState == FoldableProductState.HalfOpened)
            {
                layout.isSeparating = true;
                layout.state = FoldableState.HalfOpened;
            }
        }

        /// <summary>
        /// Same as <see cref="Enrich(FoldableLayoutState, FoldableDeviceKind)"/>, then keeps a
        /// live HalfOpened across a missing FoldingFeature. Jetpack FLAT and cover/Closed still win.
        /// Native snapshots always arrive with <see cref="FoldableProductState.Undefined"/>.
        /// </summary>
        public static void Enrich(
            FoldableLayoutState layout,
            FoldableDeviceKind kindHint,
            FoldableProductState previousProduct,
            FoldableOrientation previousOrientation = FoldableOrientation.Undefined)
        {
            Enrich(layout, kindHint);
            if (!ShouldPreserveHalfOpened(layout, previousProduct))
                return;

            layout.productState = FoldableProductState.HalfOpened;
            layout.state = FoldableState.HalfOpened;
            layout.isCoverScreen = false;
            layout.isSeparating = true;
            if (layout.orientation == FoldableOrientation.Undefined &&
                previousOrientation != FoldableOrientation.Undefined)
                layout.orientation = previousOrientation;
        }

        /// <summary>
        /// Missing FoldingFeature is not proof of Expand. Keep HalfOpened until FLAT or Closed
        /// (never while in multi-window / picture-in-picture).
        /// </summary>
        public static bool ShouldPreserveHalfOpened(
            FoldableLayoutState layout,
            FoldableProductState previousProduct)
        {
            if (layout == null || previousProduct != FoldableProductState.HalfOpened)
                return false;
            // Split-screen / PiP windows that do not cross the hinge get no FoldingFeature.
            // That is not evidence of Flex: drop to the non-posture state instead of holding it.
            if (layout.isInMultiWindowMode || layout.isInPictureInPictureMode)
                return false;
            // Live Jetpack HALF_OPENED / FLAT already decided — do not override.
            if (layout.state == FoldableState.Flat || layout.state == FoldableState.HalfOpened)
                return false;
            if (layout.isCoverScreen || layout.productState == FoldableProductState.Closed)
                return false;
            if (layout.productState != FoldableProductState.Opened &&
                layout.productState != FoldableProductState.Undefined)
                return false;
            return layout.state == FoldableState.Undefined;
        }

        #endregion

        #region Catalog

        /// <summary>
        /// Catalog Cover vs Main/Inner sizes used by Play Mode simulator when product state changes.
        /// </summary>
        public static bool GetProductMetrics(
            FoldableDeviceKind kind,
            FoldableProductState product,
            out int width,
            out int height)
        {
            width = 0;
            height = 0;
            bool cover = product == FoldableProductState.Closed;

            switch (kind)
            {
                case FoldableDeviceKind.FlipablePhone:
                    width = cover ? 720 : 1080;
                    height = cover ? 748 : 2640;
                    return true;
                case FoldableDeviceKind.DualScreen:
                    width = cover ? 675 : 1350;
                    height = 1800;
                    return true;
                case FoldableDeviceKind.FoldablePhone:
                    // Inner open reference for cover heuristics (Fold8-class). Cover catalog
                    // stays Fold7-like for Simulator CreateFromProductState sizes.
                    width = cover ? 832 : 1848;
                    height = cover ? 2268 : 2448;
                    return true;
                default:
                    return false;
            }
        }

        #endregion

        #region Apply

        /// <summary>
        /// Ensures width×height match the requested orientation (portrait upright, then landscape swap).
        /// </summary>
        public static void ApplyOrientedMetrics(bool landscape, ref int width, ref int height)
        {
            if (width <= 0 || height <= 0)
                return;

            if (width > height)
            {
                int t = width;
                width = height;
                height = t;
            }

            if (!landscape || width == height)
                return;

            int swap = width;
            width = height;
            height = swap;
        }

        #endregion

        #region Helpers

        public static int ToIndex(FoldableProductState state)
        {
            switch (state)
            {
                case FoldableProductState.Closed: return 0;
                case FoldableProductState.HalfOpened: return 2;
                default: return 1;
            }
        }

        public static FoldableProductState FromIndex(int index)
        {
            switch (index)
            {
                case 0: return FoldableProductState.Closed;
                case 2: return FoldableProductState.HalfOpened;
                default: return FoldableProductState.Opened;
            }
        }

        public static string[] GetDisplayNames(FoldableDeviceKind kind) =>
            new[]
            {
                GetDisplayName(kind, FoldableProductState.Closed),
                GetDisplayName(kind, FoldableProductState.Opened),
                GetDisplayName(kind, FoldableProductState.HalfOpened)
            };

        public static string GetDisplayName(FoldableDeviceKind kind, FoldableProductState state)
        {
            switch (kind)
            {
                case FoldableDeviceKind.FoldablePhone:
                    return FoldDisplayName(state);
                case FoldableDeviceKind.FlipablePhone:
                    return FlipDisplayName(state);
                case FoldableDeviceKind.DualScreen:
                    return DualDisplayName(state);
                default:
                    return GenericDisplayName(state);
            }
        }

        public static string GetKindDisplayName(FoldableDeviceKind kind)
        {
            switch (kind)
            {
                case FoldableDeviceKind.FoldablePhone: return "Foldable phone";
                case FoldableDeviceKind.FlipablePhone: return "Flipable phone";
                case FoldableDeviceKind.DualScreen: return "Dual-screen";
                default: return "Unknown";
            }
        }

        #endregion

        #region Simulator

        /// <summary>
        /// Half-open hinge when nothing picks one: the horizontal fold (Fold / Flip Flex laptop,
        /// iPhone Duo Seated laptop).
        /// </summary>
        public static FoldableOrientation DefaultHalfOpenHinge() => FoldableOrientation.Horizontal;

        /// <summary>
        /// Half-open pose to crease for portrait-native inners (Galaxy Fold / Flip): a Landscape
        /// pose is the horizontal laptop fold, a Portrait pose the vertical book fold. iPhone Duo
        /// runs the other way, see <see cref="LandscapeNativeHalfOpenHinge"/>.
        /// </summary>
        public static FoldableOrientation HalfOpenHingeForPose(bool landscape) =>
            landscape ? FoldableOrientation.Horizontal : FoldableOrientation.Vertical;

        /// <summary>
        /// iPhone Duo Seated (landscape-native inner, half open). Its pose names the screen shape
        /// like every other device, but its fold runs the other way: see
        /// <see cref="LandscapeNativeHalfOpenHinge"/>.
        /// </summary>
        public static bool IsLandscapeNativeHalfOpen(
            FoldableProductState product,
            bool innerLandscapeNative) =>
            product == FoldableProductState.HalfOpened && innerLandscapeNative;

        /// <summary>
        /// Crease of a landscape-native inner (iPhone Duo) half open: a Landscape (wide) screen is
        /// the book with a vertical fold, a Portrait (tall) screen the laptop with a horizontal fold.
        /// </summary>
        public static FoldableOrientation LandscapeNativeHalfOpenHinge(bool landscape) =>
            landscape ? FoldableOrientation.Vertical : FoldableOrientation.Horizontal;

        /// <summary>
        /// Android Fold / Fold Cover half open (portrait-native inner). Its crease follows the
        /// screen, as Jetpack reports it on a device: see <see cref="FoldHalfOpenHingeForScreen"/>.
        /// </summary>
        public static bool IsFoldHalfOpen(
            FoldableDeviceKind kind,
            FoldableProductState product,
            bool innerLandscapeNative) =>
            kind == FoldableDeviceKind.FoldablePhone &&
            product == FoldableProductState.HalfOpened &&
            !innerLandscapeNative;

        /// <summary>
        /// Crease of an Android Fold / Fold Cover half open for a screen shape: the flat screen
        /// (wide, Fold8 2448×1848, Portrait pose) folds top to bottom (vertical); the flat screen
        /// turned 90° (tall, 1848×2448, Landscape pose) folds left to right (horizontal).
        /// </summary>
        public static FoldableOrientation FoldHalfOpenHingeForScreen(bool wideScreen) =>
            wideScreen ? FoldableOrientation.Vertical : FoldableOrientation.Horizontal;

        /// <summary>
        /// Screen shape (true = wide) a Flex hinge picks when nothing else does. Android Fold:
        /// the vertical book fold is the wide screen, the horizontal tabletop fold the tall one
        /// (<see cref="FoldHalfOpenHingeForScreen"/>). Flip Flex stays portrait. Dual-screen:
        /// the horizontal fold is wide.
        /// </summary>
        public static bool DefaultLandscapeForHalfOpen(
            FoldableDeviceKind kind,
            FoldableOrientation hinge)
        {
            if (kind == FoldableDeviceKind.FlipablePhone)
                return false;
            if (kind == FoldableDeviceKind.FoldablePhone)
                return hinge == FoldHalfOpenHingeForScreen(wideScreen: true);

            return hinge == HalfOpenHingeForPose(landscape: true);
        }

        /// <summary>
        /// Maps the Simulator Tabletop/Book hinge into current screen space.
        /// Flip's clamshell hinge is horizontal in portrait; landscape rotation makes it vertical.
        /// Android Fold follows the screen (<see cref="FoldHalfOpenHingeForScreen"/>), so a wide
        /// screen never carries a horizontal fold. iPhone Duo also follows the screen:
        /// 2853×2007 is the wide book (vertical fold), 2007×2853 the tall laptop (horizontal fold).
        /// </summary>
        public static FoldableOrientation ToScreenSpaceHalfOpenHinge(
            FoldableDeviceKind kind,
            FoldableProductState product,
            FoldableOrientation postureHinge,
            int width,
            int height,
            bool innerLandscapeNative = false)
        {
            if (product != FoldableProductState.HalfOpened)
                return postureHinge;
            if (kind == FoldableDeviceKind.FoldablePhone && innerLandscapeNative)
            {
                if (width <= 0 || height <= 0 || width == height)
                    return postureHinge;
                // Wide screen = book (vertical fold), tall = laptop (horizontal fold).
                return LandscapeNativeHalfOpenHinge(landscape: width > height);
            }
            if (IsFoldHalfOpen(kind, product, innerLandscapeNative))
            {
                if (width <= 0 || height <= 0 || width == height)
                    return postureHinge;
                return FoldHalfOpenHingeForScreen(wideScreen: width > height);
            }
            if (kind != FoldableDeviceKind.FlipablePhone)
                return postureHinge;
            if (width <= 0 || height <= 0 || width <= height)
                return postureHinge;

            return postureHinge == FoldableOrientation.Horizontal
                ? FoldableOrientation.Vertical
                : FoldableOrientation.Horizontal;
        }

        /// <summary>
        /// Pose landscape: the project lock when set, else the Flex default for Half Opened,
        /// else portrait.
        /// </summary>
        public static bool ResolveDefaultLandscape(
            FoldableDeviceKind kind,
            FoldableOrientation hinge,
            FoldableProductState product,
            bool? projectLockedLandscape)
        {
            if (projectLockedLandscape.HasValue)
                return projectLockedLandscape.Value;

            if (product == FoldableProductState.HalfOpened)
                return DefaultLandscapeForHalfOpen(kind, hinge);

            return false;
        }

        /// <summary>
        /// True when the Android half-open hold can act on the phone: Default Orientation is Auto Rotation
        /// and more than one of the four orientations is allowed. Android requests a fixed orientation for
        /// Auto Rotation with one allowed, so the hold stays off and the cards keep their defaults
        /// (<see cref="FoldableOrientationHold"/>). Example: Auto Rotation with only Portrait ticked → false.
        /// </summary>
        public static bool AllowsRuntimeHold(
            bool autoRotation,
            bool portrait,
            bool portraitUpsideDown,
            bool landscapeLeft,
            bool landscapeRight)
        {
            if (!autoRotation)
                return false;
            int allowed = (portrait ? 1 : 0) + (portraitUpsideDown ? 1 : 0) +
                          (landscapeLeft ? 1 : 0) + (landscapeRight ? 1 : 0);
            return allowed != 1;
        }

        /// <summary><see cref="AllowsRuntimeHold(bool, bool, bool, bool, bool)"/> from <see cref="Screen"/>.</summary>
        public static bool AllowsRuntimeHoldFromScreen() =>
            AllowsRuntimeHold(
                Screen.orientation == ScreenOrientation.AutoRotation,
                Screen.autorotateToPortrait,
                Screen.autorotateToPortraitUpsideDown,
                Screen.autorotateToLandscapeLeft,
                Screen.autorotateToLandscapeRight);

        public static bool? GetLockedLandscapeFromScreen()
        {
            bool portraitAllowed = Screen.autorotateToPortrait || Screen.autorotateToPortraitUpsideDown;
            bool landscapeAllowed = Screen.autorotateToLandscapeLeft || Screen.autorotateToLandscapeRight;

            if (Screen.orientation == ScreenOrientation.AutoRotation)
            {
                if (landscapeAllowed && !portraitAllowed)
                    return true;
                if (portraitAllowed && !landscapeAllowed)
                    return false;
                return null;
            }

            return Screen.orientation == ScreenOrientation.LandscapeLeft ||
                   Screen.orientation == ScreenOrientation.LandscapeRight;
        }

        /// <summary>
        /// Screen shape (true = wide) a one-axis project lock gives for <paramref name="product"/>,
        /// or null when both axes are allowed. Android Fold / Fold Cover Half Opened names the
        /// posture, so landscape-only is the flat screen turned 90° (tall, Fold8 1848×2448,
        /// horizontal fold) and portrait-only the flat screen (wide, 2448×1848, vertical fold).
        /// Every other device and posture keeps the lock's shape.
        /// </summary>
        public static bool? LockedScreenLandscape(
            FoldableDeviceKind kind,
            FoldableProductState product,
            bool? lockedLandscape)
        {
            if (!lockedLandscape.HasValue)
                return null;
            return IsFoldHalfOpen(kind, product, innerLandscapeNative: false)
                ? !lockedLandscape.Value
                : lockedLandscape.Value;
        }

        /// <summary>
        /// Set by Responsive UI in the Editor: true for a half-open inner WxH it maps to Fold
        /// Cover Half Opened (Fold8-style 1848×2448). Unset: every Fold uses the Fold card.
        /// </summary>
        public static Func<int, int, bool> IsFoldCoverHalfOpenedSize;

        static readonly FoldableHalfOpenPose DefaultHalfOpenCard = new FoldableHalfOpenPose();

        /// <summary>
        /// Screen shape (true = wide) an Android Fold / Fold Cover / Flip Half Opened holds while
        /// it is rotated, as its card in <see cref="FoldableSettings.Rules"/> decides
        /// (<see cref="FoldableHalfOpenRules.ScreenLandscape"/>): by default a Fold holds the
        /// one-axis lock's pose and follows rotation with both axes allowed;
        /// <paramref name="entryLandscape"/> is the screen it entered Half Opened with. A Flip is
        /// held only when its card is changed. Null for Dual and other postures, which rotate freely.
        /// Cards apply only while the runtime hold can act (<paramref name="autoRotation"/>, see
        /// <see cref="AllowsRuntimeHold"/>): a fixed orientation, or Auto Rotation with one orientation
        /// allowed, keeps the platform default, as the runtime hold never overrides it.
        /// </summary>
        public static bool? HeldHalfOpenScreenLandscape(
            FoldableDeviceKind kind,
            FoldableProductState product,
            bool? lockedLandscape,
            bool entryLandscape,
            bool foldCover = false,
            bool autoRotation = true)
        {
            if (product != FoldableProductState.HalfOpened)
                return null;
            bool isFold = kind == FoldableDeviceKind.FoldablePhone;
            if (!isFold && kind != FoldableDeviceKind.FlipablePhone)
                return null;

            var pose = autoRotation ? FoldableSettings.Rules.For(kind, foldCover) : DefaultHalfOpenCard;
            if (!isFold && pose.IsDefault)
                return null;
            return FoldableHalfOpenRules.ScreenLandscape(pose, isFold, lockedLandscape, entryLandscape);
        }

        /// <summary>
        /// Pose name (true = Landscape) for a screen shape (true = wide). Android Fold / Fold Cover
        /// Half Opened names the posture, so its wide screen is Portrait and its tall one
        /// Landscape; every other device and posture names the shape. Its own inverse.
        /// </summary>
        public static bool PoseLandscape(
            FoldableDeviceKind kind,
            FoldableProductState product,
            bool screenLandscape) =>
            IsFoldHalfOpen(kind, product, innerLandscapeNative: false)
                ? !screenLandscape
                : screenLandscape;

        public static FoldableOrientation? GetLockedHalfOpenHingeFromScreen()
        {
            var locked = GetLockedLandscapeFromScreen();
            if (!locked.HasValue)
                return null;

            return HalfOpenHingeForPose(locked.Value);
        }

        /// <summary>
        /// Builds a posture snapshot for a product state. Android Fold and iPhone Duo
        /// (<paramref name="innerLandscapeNative"/>) derive the half-open fold axis from the
        /// screen shape, see
        /// <see cref="ToScreenSpaceHalfOpenHinge"/>.
        /// </summary>
        public static FoldableLayoutState CreateFromProductState(
            FoldableDeviceKind kind,
            FoldableProductState product,
            int width,
            int height,
            float hingeNormalized = 0.5f,
            int hingeThicknessPx = 24,
            FoldableOrientation? halfOpenHinge = null,
            bool isSeparating = false,
            bool innerLandscapeNative = false)
        {
            bool cover = product == FoldableProductState.Closed;
            var state = product == FoldableProductState.HalfOpened
                ? FoldableState.HalfOpened
                : FoldableState.Flat;

            var orientation = ToScreenSpaceHalfOpenHinge(
                kind,
                product,
                ResolveOrientation(kind, product, halfOpenHinge, isSeparating),
                width,
                height,
                innerLandscapeNative);

            if (kind == FoldableDeviceKind.DualScreen &&
                product == FoldableProductState.Opened)
            {
                isSeparating = true;
            }

            if (cover)
            {
                state = FoldableState.Flat;
                isSeparating = false;
            }

            var layout = FoldableLayoutState.CreateSimulated(
                state, orientation, width, height, hingeNormalized, hingeThicknessPx, isSeparating);

            layout.deviceKind = kind;
            layout.isCoverScreen = cover;
            layout.productState = Resolve(layout);
            return layout;
        }

        static FoldableOrientation ResolveOrientation(
            FoldableDeviceKind kind,
            FoldableProductState product,
            FoldableOrientation? halfOpenHinge,
            bool isSeparating)
        {
            if (product == FoldableProductState.HalfOpened)
                return halfOpenHinge ?? DefaultHalfOpenHinge();

            if (isSeparating || kind == FoldableDeviceKind.DualScreen)
                return FoldableOrientation.Vertical;

            return FoldableOrientation.Horizontal;
        }

        #endregion

        #region Helpers

        static string FoldDisplayName(FoldableProductState state)
        {
            switch (state)
            {
                case FoldableProductState.Closed: return "Normal";
                case FoldableProductState.Opened: return "Expand";
                case FoldableProductState.HalfOpened: return "Expand Half Opened";
                default: return "Undefined";
            }
        }

        static string FlipDisplayName(FoldableProductState state)
        {
            switch (state)
            {
                case FoldableProductState.Closed: return "Closed";
                case FoldableProductState.Opened: return "Opened";
                case FoldableProductState.HalfOpened: return "Half Opened";
                default: return "Undefined";
            }
        }

        static string DualDisplayName(FoldableProductState state)
        {
            switch (state)
            {
                case FoldableProductState.Closed: return "Single pane";
                case FoldableProductState.Opened: return "Spanned";
                case FoldableProductState.HalfOpened: return "Flex";
                default: return "Undefined";
            }
        }

        static string GenericDisplayName(FoldableProductState state)
        {
            switch (state)
            {
                case FoldableProductState.Closed: return "Closed / Normal";
                case FoldableProductState.Opened: return "Opened / Expand";
                case FoldableProductState.HalfOpened: return "Half Opened";
                default: return "Undefined";
            }
        }

        #endregion
    }
}
