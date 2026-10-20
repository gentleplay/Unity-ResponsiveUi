using NUnit.Framework;
using UnityEngine;
using FDPlus.Foldable.Editor;
using FDPlus.PhonePad.Editor;
using FDPlus.ResponsiveUI;

namespace FDPlus.ResponsiveUI.Tests
{
    /// <summary>
    /// Device pixels for EditMode tests come from catalog JSON, not invented
    /// resolutions. IDs match Project Settings Design Canvas references.
    /// </summary>
    public static class ResponsiveCatalogDevices
    {
        public const string PhoneId = "iphone-17-pro-max";
        public const string CompactPhoneId = "iphone-16e";
        public const string PadId = "ipad-pro-13-m5";
        public const string FoldId = "galaxy-z-fold8-like";
        public const string FoldPhoneId = "galaxy-z-fold7-like";
        public const string FlipId = "galaxy-z-flip8-like";
        public const string IphoneDuoId = "iphone-duo-like";

        /// <summary>Galaxy Z Fold8 Cover Simulator nav inset (see fold catalog notes).</summary>
        public const int FoldCoverNavInsetPx = 120;

        public static Vector2 PhonePortrait => Portrait(RequirePhone(PhoneId));
        public static Vector2 PhoneLandscape => Landscape(RequirePhone(PhoneId));
        public static Vector2 CompactPhoneLandscape => Landscape(RequirePhone(CompactPhoneId));
        public static Vector2 PadPortrait => Portrait(RequirePhone(PadId));
        public static Vector2 PadLandscape => Landscape(RequirePhone(PadId));
        public static Vector2 FoldCoverPortrait => Size(RequireFoldScreen(cover: true), landscape: false);
        public static Vector2 FoldCoverLandscape => Size(RequireFoldScreen(cover: true), landscape: true);
        public static Vector2 FoldInnerPortrait => Size(RequireFoldScreen(cover: false), landscape: false);
        public static Vector2 FoldInnerLandscape => Size(RequireFoldScreen(cover: false), landscape: true);
        public static Vector2 FoldPhoneInnerPortrait =>
            Size(RequireScreen(FoldPhoneId, cover: false), landscape: false);
        public static Vector2 FoldPhoneInnerLandscape =>
            Size(RequireScreen(FoldPhoneId, cover: false), landscape: true);
        public static Vector2 FlipCoverPortrait => Size(RequireFlipScreen(cover: true), landscape: false);
        public static Vector2 FlipCoverLandscape => Size(RequireFlipScreen(cover: true), landscape: true);
        public static Vector2 FlipMainPortrait => Size(RequireFlipScreen(cover: false), landscape: false);
        public static Vector2 FlipMainLandscape => Size(RequireFlipScreen(cover: false), landscape: true);
        public static Vector2 IphoneDuoInnerPortrait =>
            Size(RequireScreen(IphoneDuoId, cover: false), landscape: false);
        public static Vector2 IphoneDuoInnerLandscape =>
            Size(RequireScreen(IphoneDuoId, cover: false), landscape: true);

        public static Vector2 FoldCoverSafeAreaPortrait =>
            new Vector2(FoldCoverPortrait.x, FoldCoverPortrait.y - FoldCoverNavInsetPx);

        public static Vector2 FoldCoverSafeAreaLandscape =>
            new Vector2(FoldCoverPortrait.y - FoldCoverNavInsetPx, FoldCoverPortrait.x);

        /// <summary>
        /// Fold7-like Inner landscape with Simulator Safe Area long-side inset
        /// (2184→2154), matching Device Simulator Fold panel readouts.
        /// </summary>
        public const int FoldPhoneInnerSafeAreaLongInsetPx = 30;

        public static Vector2 FoldPhoneInnerSafeAreaLandscape =>
            new Vector2(
                FoldPhoneInnerLandscape.x - FoldPhoneInnerSafeAreaLongInsetPx,
                FoldPhoneInnerLandscape.y);

        public static Vector2 FoldPhoneInnerSafeAreaPortrait =>
            new Vector2(
                FoldPhoneInnerPortrait.x,
                FoldPhoneInnerPortrait.y - FoldPhoneInnerSafeAreaLongInsetPx);

        static PhonePadDeviceCatalog.DeviceProfile RequirePhone(string id)
        {
            PhonePadDeviceCatalog.DeviceProfile profile = PhonePadDeviceCatalog.FindById(id);
            Assert.NotNull(profile, "missing PhonePad profile '" + id + "'");
            Assert.Greater(profile.PortraitWidth, 0, id);
            Assert.Greater(profile.PortraitHeight, 0, id);
            Assert.Greater(profile.LandscapeWidth, 0, id);
            Assert.Greater(profile.LandscapeHeight, 0, id);
            return profile;
        }

        static FoldableDeviceCatalog.ScreenProfile RequireFoldScreen(bool cover) =>
            RequireScreen(FoldId, cover);

        static FoldableDeviceCatalog.ScreenProfile RequireFlipScreen(bool cover) =>
            RequireScreen(FlipId, cover);

        static FoldableDeviceCatalog.ScreenProfile RequireScreen(string id, bool cover)
        {
            FoldableDeviceCatalog.DeviceProfile device = FoldableDeviceCatalog.FindById(id);
            Assert.NotNull(device, "missing Fold/Flip profile '" + id + "'");
            Assert.NotNull(device.Screens, id);
            for (int i = 0; i < device.Screens.Length; i++)
            {
                FoldableDeviceCatalog.ScreenProfile screen = device.Screens[i];
                if (screen != null && screen.CoverOrOuter == cover)
                    return screen;
            }

            Assert.Fail(id + (cover ? " has no Cover screen" : " has no inner/main screen"));
            return null;
        }

        static Vector2 Portrait(PhonePadDeviceCatalog.DeviceProfile profile) =>
            new Vector2(profile.PortraitWidth, profile.PortraitHeight);

        static Vector2 Landscape(PhonePadDeviceCatalog.DeviceProfile profile) =>
            new Vector2(profile.LandscapeWidth, profile.LandscapeHeight);

        static Vector2 Size(FoldableDeviceCatalog.ScreenProfile screen, bool landscape) =>
            landscape
                ? new Vector2(screen.LandscapeWidth, screen.LandscapeHeight)
                : new Vector2(screen.PortraitWidth, screen.PortraitHeight);
    }
}
