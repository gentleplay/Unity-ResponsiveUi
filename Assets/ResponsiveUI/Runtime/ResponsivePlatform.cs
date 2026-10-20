using UnityEngine;

namespace FDPlus.ResponsiveUI
{
    /// <summary>
    /// Slot family of a build. Android: Phone / Pad / Fold / Flip. iOS: Phone / Pad / iPhone Duo.
    /// Wide Screen (Windows / macOS / Linux / WebGL): Wide Screen only.
    /// </summary>
    public enum ResponsivePlatform
    {
        Android = 0,
        Ios = 1,
        WideScreen = 2,
    }

    /// <summary>
    /// Active <see cref="ResponsivePlatform"/> and the forms it owns. Players follow the build
    /// target (Unity defines <c>UNITY_ANDROID</c> / <c>UNITY_IOS</c> for the active target in the
    /// Editor too). In the Editor, Android and iOS slots are both listed, and a selected Device
    /// Simulator profile drives detection on any build target. Without that profile, detection
    /// stays on the build target's forms.
    /// </summary>
    public static class ResponsivePlatforms
    {
        #region Runtime state

        static ResponsivePlatform? s_Override;

        #endregion

        #region Active platform

        /// <summary>Platform of the build target (or the <see cref="SetOverride"/> pin).</summary>
        public static ResponsivePlatform Active => s_Override ?? BuildPlatform;

        /// <summary>Platform from the compile-time build target.</summary>
        public static ResponsivePlatform BuildPlatform =>
#if UNITY_ANDROID
            ResponsivePlatform.Android;
#elif UNITY_IOS
            ResponsivePlatform.Ios;
#else
            ResponsivePlatform.WideScreen;
#endif

        /// <summary>
        /// When false, detection returns every form whatever the platform (Editor tests and tools
        /// that author all slots). Players keep the default (true).
        /// </summary>
        public static bool ConstrainsForms { get; set; } = true;

        /// <summary>Pins <see cref="Active"/> (null = build target). Editor tests and tools only.</summary>
        public static void SetOverride(ResponsivePlatform? platform) => s_Override = platform;

        /// <summary>
        /// Play-session reset (also when Enter Play Mode skips domain reload): drops a test /
        /// tool pin so Play Mode always follows the build target with constrained forms.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetPlaySessionState()
        {
            s_Override = null;
            ConstrainsForms = true;
        }

        #endregion

        #region Forms

        /// <summary>True when <paramref name="platform"/> can reach <paramref name="form"/>.</summary>
        public static bool Owns(ResponsivePlatform platform, ResponsiveDevice.FormFactor form)
        {
            switch (form)
            {
                case ResponsiveDevice.FormFactor.Phone:
                case ResponsiveDevice.FormFactor.Pad:
                    return platform != ResponsivePlatform.WideScreen;
                case ResponsiveDevice.FormFactor.iPhoneDuoCover:
                case ResponsiveDevice.FormFactor.iPhoneDuoSeated:
                    return platform == ResponsivePlatform.Ios;
                case ResponsiveDevice.FormFactor.WideScreen:
                    return platform == ResponsivePlatform.WideScreen;
                default:
                    // Fold / Flip forms.
                    return platform == ResponsivePlatform.Android;
            }
        }

        /// <summary><see cref="Owns"/> for the <see cref="Active"/> platform.</summary>
        public static bool IsActiveForm(ResponsiveDevice.FormFactor form) => Owns(Active, form);

        /// <summary>
        /// Whether a newly seeded slot of <paramref name="form"/> starts Enabled. Seeding is
        /// platform-neutral except Wide Screen, which seeds Enabled only on Wide Screen targets
        /// (always when <see cref="ConstrainsForms"/> is false).
        /// </summary>
        public static bool SeedsEnabled(ResponsiveDevice.FormFactor form) =>
            form != ResponsiveDevice.FormFactor.WideScreen ||
            !ConstrainsForms ||
            Active == ResponsivePlatform.WideScreen;

        /// <summary>
        /// <paramref name="form"/> on <paramref name="platform"/>: owned forms pass through; Wide
        /// Screen builds always use <see cref="ResponsiveDevice.FormFactor.WideScreen"/>. Otherwise
        /// Fold / Fold Cover HalfOpened, iPhone Duo Seated and Wide Screen become Pad; covers and
        /// Flip HalfOpened become Phone.
        /// </summary>
        public static ResponsiveDevice.FormFactor Constrain(
            ResponsivePlatform platform,
            ResponsiveDevice.FormFactor form)
        {
            if (platform == ResponsivePlatform.WideScreen)
                return ResponsiveDevice.FormFactor.WideScreen;
            if (Owns(platform, form))
                return form;

            switch (form)
            {
                case ResponsiveDevice.FormFactor.FoldHalfOpened:
                case ResponsiveDevice.FormFactor.FoldCoverHalfOpened:
                case ResponsiveDevice.FormFactor.iPhoneDuoSeated:
                case ResponsiveDevice.FormFactor.WideScreen:
                    return ResponsiveDevice.FormFactor.Pad;
                default:
                    return ResponsiveDevice.FormFactor.Phone;
            }
        }

        /// <summary>
        /// Detection result for the <see cref="Active"/> platform (unchanged when
        /// <see cref="ConstrainsForms"/> is false).
        /// </summary>
        public static ResponsiveDevice.FormFactor ConstrainDetected(ResponsiveDevice.FormFactor form) =>
            ConstrainsForms ? Constrain(Active, form) : form;

        #endregion
    }
}
