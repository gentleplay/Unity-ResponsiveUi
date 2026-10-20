using UnityEditor;

namespace FDPlus.Foldable.Editor
{
    /// <summary>
    /// Player Settings helpers.
    /// Single source for Default Orientation / Allowed Orientations → portrait &amp; landscape axes.
    /// </summary>
    public static class FoldablePlayerOrientation
    {
        #region Detection

        /// <summary>True when Player Settings allow portrait (or default orientation is portrait).</summary>
        public static bool IsPortraitAllowed
        {
            get
            {
                GetAllowedAxes(out bool portrait, out _);
                return portrait;
            }
        }

        /// <summary>True when Player Settings allow landscape (or default orientation is landscape).</summary>
        public static bool IsLandscapeAllowed
        {
            get
            {
                GetAllowedAxes(out _, out bool landscape);
                return landscape;
            }
        }

        /// <summary>
        /// Landscape value forced by Player Settings, or null when Auto Rotation allows
        /// both portrait and landscape (developer may choose in Fold &amp; Flip UI).
        /// </summary>
        public static bool? GetLockedLandscape()
        {
            GetAllowedAxes(out bool portrait, out bool landscape);
            if (landscape && !portrait)
                return true;
            if (portrait && !landscape)
                return false;
            return null;
        }

        /// <summary>
        /// True when the Default Orientation is Auto Rotation. Only then does the runtime honour
        /// <c>Screen.autorotateTo*</c>, so only then can iPhone Duo Seated switch to the hold's axis.
        /// </summary>
        public static bool IsAutoRotation => GetDefaultOrientation() == UIOrientation.AutoRotation;

        /// <summary>
        /// True when the Android half-open cards can act: Auto Rotation with more than one orientation
        /// allowed (<see cref="FoldableProductStateResolver.AllowsRuntimeHold"/>). Auto Rotation with one
        /// orientation is fixed on the phone, so the cards keep their defaults. iPhone Duo does not use it.
        /// </summary>
        public static bool AllowsAndroidHalfOpenHold =>
            FoldableProductStateResolver.AllowsRuntimeHold(
                IsAutoRotation,
                PlayerSettings.allowedAutorotateToPortrait,
                PlayerSettings.allowedAutorotateToPortraitUpsideDown,
                PlayerSettings.allowedAutorotateToLandscapeLeft,
                PlayerSettings.allowedAutorotateToLandscapeRight);

        /// <summary>
        /// Pose landscape forced by Player Settings for <paramref name="product"/>, or null when
        /// Auto Rotation allows both axes. iPhone Duo Seated is not forced by Auto Rotation with
        /// one axis: the device allows only the hold's axis while Seated.
        /// </summary>
        public static bool? GetLockedLandscapeForProduct(
            FoldableProductState product,
            bool innerLandscapeNative) =>
            LockedPoseLandscape(GetLockedLandscape(), product, innerLandscapeNative, IsAutoRotation);

        /// <summary>
        /// Pure form of <see cref="GetLockedLandscapeForProduct"/> for a given screen-axis lock
        /// (<paramref name="lockedLandscape"/>: true = landscape-only, false = portrait-only,
        /// null = both axes). <paramref name="autoRotation"/>: the lock is Auto Rotation with one
        /// axis (false = a fixed Default Orientation).
        /// </summary>
        public static bool? LockedPoseLandscape(
            bool? lockedLandscape,
            FoldableProductState product,
            bool innerLandscapeNative,
            bool autoRotation = true)
        {
            // iPhone Duo Seated: the device allows only the hold's axis while Seated, so an Auto Rotation
            // lock never forces its pose (laptop by default, book on request). A fixed Default
            // Orientation ignores the runtime flags and keeps its lock.
            if (autoRotation &&
                FoldableProductStateResolver.IsLandscapeNativeHalfOpen(product, innerLandscapeNative))
                return null;
            return lockedLandscape;
        }

        /// <summary>
        /// Half-open hinge forced by Player Settings, or null when Auto Rotation allows both axes.
        /// Landscape → laptop fold (Horizontal). Portrait → book fold (Vertical); iPhone Duo the
        /// other way. iPhone Duo Seated under Auto Rotation is never forced (see
        /// <see cref="LockedPoseLandscape"/>).
        /// </summary>
        public static FoldableOrientation? GetLockedHalfOpenHinge(bool innerLandscapeNative) =>
            LockedHalfOpenHinge(GetLockedLandscape(), innerLandscapeNative, IsAutoRotation);

        /// <summary>
        /// Pure form of <see cref="GetLockedHalfOpenHinge"/> for a given screen-axis lock
        /// (see <see cref="LockedPoseLandscape"/>).
        /// </summary>
        public static FoldableOrientation? LockedHalfOpenHinge(
            bool? lockedLandscape,
            bool innerLandscapeNative,
            bool autoRotation = true)
        {
            var locked = LockedPoseLandscape(
                lockedLandscape, FoldableProductState.HalfOpened, innerLandscapeNative, autoRotation);
            if (!locked.HasValue)
                return null;

            return innerLandscapeNative
                ? FoldableProductStateResolver.LandscapeNativeHalfOpenHinge(locked.Value)
                : FoldableProductStateResolver.HalfOpenHingeForPose(locked.Value);
        }

        public static string DescribeProjectOrientation()
        {
            GetAllowedAxes(out bool portrait, out bool landscape);
            if (portrait && landscape)
                return "Auto Rotation (Portrait + Landscape)";
            if (portrait)
                return "Portrait (Player Settings)";
            if (landscape)
                return "Landscape (Player Settings)";
            return "No Allowed Orientations (Player Settings)";
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Resolves Player Default Orientation + Allowed Orientations into portrait/landscape axes.
        /// </summary>
        public static void GetAllowedAxes(out bool portrait, out bool landscape)
        {
            switch (GetDefaultOrientation())
            {
                case UIOrientation.Portrait:
                case UIOrientation.PortraitUpsideDown:
                    portrait = true;
                    landscape = false;
                    return;
                case UIOrientation.LandscapeLeft:
                case UIOrientation.LandscapeRight:
                    portrait = false;
                    landscape = true;
                    return;
                case UIOrientation.AutoRotation:
                    portrait =
                        PlayerSettings.allowedAutorotateToPortrait ||
                        PlayerSettings.allowedAutorotateToPortraitUpsideDown;
                    landscape =
                        PlayerSettings.allowedAutorotateToLandscapeLeft ||
                        PlayerSettings.allowedAutorotateToLandscapeRight;
                    return;
                default:
                    portrait = true;
                    landscape = true;
                    return;
            }
        }

        /// <summary>
        /// Pose landscape for a product state: the Player Settings lock when it applies,
        /// else the Flex hinge default. iPhone Duo Seated ignores an Auto Rotation lock and follows
        /// its hold: laptop (horizontal) → Portrait, book (vertical) → Landscape
        /// (<see cref="FoldableProductStateResolver.LandscapeNativeHalfOpenHinge"/>).
        /// </summary>
        public static bool ResolveForProduct(
            FoldableDeviceKind kind,
            FoldableOrientation hinge,
            FoldableProductState product,
            bool innerLandscapeNative = false)
        {
            bool? locked = GetLockedLandscapeForProduct(product, innerLandscapeNative);
            if (!locked.HasValue &&
                FoldableProductStateResolver.IsLandscapeNativeHalfOpen(product, innerLandscapeNative))
                return hinge == FoldableProductStateResolver.LandscapeNativeHalfOpenHinge(landscape: true);

            return FoldableProductStateResolver.ResolveDefaultLandscape(
                kind,
                hinge,
                product,
                locked);
        }

        #endregion

        #region Editor

        /// <summary>Player Settings Default Orientation. Kept in one place for clarity.</summary>
        public static UIOrientation GetDefaultOrientation() =>
            PlayerSettings.defaultInterfaceOrientation;

        /// <summary>Runtime <see cref="UnityEngine.ScreenOrientation"/> for a Player Settings orientation.</summary>
        public static UnityEngine.ScreenOrientation ToScreenOrientation(UIOrientation orientation)
        {
            switch (orientation)
            {
                case UIOrientation.Portrait: return UnityEngine.ScreenOrientation.Portrait;
                case UIOrientation.PortraitUpsideDown: return UnityEngine.ScreenOrientation.PortraitUpsideDown;
                case UIOrientation.LandscapeLeft: return UnityEngine.ScreenOrientation.LandscapeLeft;
                case UIOrientation.LandscapeRight: return UnityEngine.ScreenOrientation.LandscapeRight;
                default: return UnityEngine.ScreenOrientation.AutoRotation;
            }
        }

        public static void SetDefaultOrientation(UIOrientation orientation) =>
            PlayerSettings.defaultInterfaceOrientation = orientation;

        public static void EnableAllAutorotations()
        {
            SetDefaultOrientation(UIOrientation.AutoRotation);
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = true;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
        }

        /// <summary>
        /// Android multi-window / resizable activity (not standalone <c>PlayerSettings.resizableWindow</c>).
        /// </summary>
        public static bool GetAndroidResizable() =>
            PlayerSettings.Android.resizeableActivity;

        public static void SetAndroidResizable(bool enabled) =>
            PlayerSettings.Android.resizeableActivity = enabled;

        #endregion
    }
}
