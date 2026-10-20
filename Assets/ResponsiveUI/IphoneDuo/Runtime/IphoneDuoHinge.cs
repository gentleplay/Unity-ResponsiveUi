using System;
using UnityEngine;
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

using FDPlus.Foldable;

namespace FDPlus.IphoneDuo
{
    /// <summary>
    /// iPhone Duo bridge: UIKit <c>UIHingeInteraction</c> (iOS 27.1+, <c>Plugins/iOS/IphoneDuoHinge.mm</c>)
    /// → <see cref="FoldableLayoutState"/>. Cover vs inner comes from the Unity view size in points
    /// (the hinge status can lag the display switch); the hinge only splits inner Opened vs HalfOpened.
    /// Mapping is pure so Editor tests cover it; native calls exist on iOS players only.
    /// </summary>
    public static class IphoneDuoHinge
    {
        #region Constants

        /// <summary>Apple division region: 40 pt wide, centred on the fold line.</summary>
        public const float FoldWidthPoints = 40f;

        /// <summary>iPhone Duo inner display in landscape points (2853×2007 px @3x).</summary>
        public const float InnerLongPoints = 951f;
        public const float InnerShortPoints = 669f;

        /// <summary>
        /// Xcode Simulator environment variable naming the simulated hardware
        /// (<see cref="SystemInfo.deviceModel"/> reports the host CPU there).
        /// </summary>
        const string SimulatorModelVariable = "SIMULATOR_MODEL_IDENTIFIER";

        /// <summary>
        /// Hardware identifiers with a hinge. The only model gate; add new Duo models here (iOS builds
        /// copy the list to Info.plist for the native launch hold).
        /// </summary>
        static readonly string[] FoldableModels = { "iPhone19,4" };

        /// <summary>Hardware identifiers with a hinge (see <see cref="FoldableModels"/>).</summary>
        internal static string[] Models => (string[])FoldableModels.Clone();

        #endregion

        #region Mapping

        /// <summary>True for iPhone hardware identifiers with a hinge (iPhone Duo).</summary>
        public static bool IsFoldableModel(string deviceModel)
        {
            for (int i = 0; i < FoldableModels.Length; i++)
            {
                if (string.Equals(deviceModel, FoldableModels[i], StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Hardware identifier to match: the simulated model in the Xcode Simulator,
        /// otherwise <paramref name="deviceModel"/>.
        /// </summary>
        internal static string ResolveModelIdentifier(string deviceModel, string simulatorModel) =>
            string.IsNullOrEmpty(simulatorModel) ? deviceModel : simulatorModel;

        /// <summary>
        /// True when the Unity view (points) is the inner display, not the cover. Same area rule
        /// as <see cref="FoldableCoverHeuristics.CoverAreaRatio"/>.
        /// </summary>
        internal static bool IsInnerViewSize(float pointWidth, float pointHeight) =>
            Mathf.Abs(pointWidth * pointHeight) >=
            InnerLongPoints * InnerShortPoints * FoldableCoverHeuristics.CoverAreaRatio;

        /// <summary>
        /// Product for the hinge status on the current view. A cover-size view is always
        /// <see cref="FoldableProductState.Closed"/>, including Apple Standing / StandBy (outer
        /// display propped in an inverted V, hinge partly open): only the inner display, partly
        /// open, is Seated.
        /// </summary>
        public static FoldableProductState MapProduct(
            IphoneDuoHingeStatus status,
            float pointWidth,
            float pointHeight)
        {
            if (!IsInnerViewSize(pointWidth, pointHeight))
                return FoldableProductState.Closed;

            return status == IphoneDuoHingeStatus.PartiallyOpen
                ? FoldableProductState.HalfOpened
                : FoldableProductState.Opened;
        }

        /// <summary>Game-facing posture for a committed product state (iOS player and Simulator preview).</summary>
        public static IphoneDuoPosture ToPosture(FoldableProductState product)
        {
            switch (product)
            {
                case FoldableProductState.Closed: return IphoneDuoPosture.Cover;
                case FoldableProductState.HalfOpened: return IphoneDuoPosture.Seated;
                case FoldableProductState.Opened: return IphoneDuoPosture.Flat;
                default: return IphoneDuoPosture.Unknown;
            }
        }

        /// <summary>
        /// Builds the posture snapshot for the current Unity view. iPhone Duo half-open:
        /// Portrait is 2007×2853 with a horizontal laptop fold, Landscape is 2853×2007
        /// with a vertical book fold (the resolver derives the axis from the screen shape).
        /// </summary>
        public static FoldableLayoutState BuildLayout(
            IphoneDuoHingeStatus status,
            float pointWidth,
            float pointHeight,
            int pixelWidth,
            int pixelHeight)
        {
            FoldableProductState product = MapProduct(status, pointWidth, pointHeight);
            float pixelsPerPoint = pixelWidth / Mathf.Max(1f, pointWidth);
            int foldPx = Mathf.Max(1, Mathf.RoundToInt(FoldWidthPoints * pixelsPerPoint));

            return FoldableProductStateResolver.CreateFromProductState(
                FoldableDeviceKind.FoldablePhone,
                product,
                pixelWidth,
                pixelHeight,
                hingeNormalized: 0.5f,
                hingeThicknessPx: foldPx,
                isSeparating: product == FoldableProductState.HalfOpened,
                innerLandscapeNative: true);
        }

        #endregion

        #region Native

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] static extern int FDPIphoneDuo_Start();
        [DllImport("__Internal")] static extern void FDPIphoneDuo_Stop();
        [DllImport("__Internal")] static extern void FDPIphoneDuo_SetEnabled(int enabled);
        [DllImport("__Internal")] static extern int FDPIphoneDuo_GetStatus();
        [DllImport("__Internal")] static extern int FDPIphoneDuo_GetVersion();
        [DllImport("__Internal")] static extern float FDPIphoneDuo_GetViewPointWidth();
        [DllImport("__Internal")] static extern float FDPIphoneDuo_GetViewPointHeight();
        [DllImport("__Internal")] static extern void FDPIphoneDuo_SetInterfaceOrientationLocked(int locked);
        [DllImport("__Internal")] static extern int FDPIphoneDuo_GetInterfaceOrientation();
        [DllImport("__Internal")] static extern int FDPIphoneDuo_IsInterfaceOrientationLocked();
        [DllImport("__Internal")] static extern void FDPIphoneDuo_RequestInterfaceOrientation(int orientation);
        [DllImport("__Internal")] static extern void FDPIphoneDuo_SetContentQuarterTurns(int turns);
        [DllImport("__Internal")] static extern int FDPIphoneDuo_GetLaunchHoldMask();
        [DllImport("__Internal")] static extern int FDPIphoneDuo_GetLaunchHoldTurns();
        [DllImport("__Internal")] static extern void FDPIphoneDuo_ReleaseLaunchHold();

        static readonly bool s_IsFoldableModel = IsFoldableModel(ResolveModelIdentifier(
            SystemInfo.deviceModel,
            Environment.GetEnvironmentVariable(SimulatorModelVariable)));

        /// <summary>iPhone Duo hardware (gates <see cref="FoldablePostureDetector.IsRuntimeSupported"/> through the backend).</summary>
        public static bool IsSupportedDevice => s_IsFoldableModel;

        /// <summary>
        /// Attaches the hinge interaction to the Unity view. False when UIKit has no resolvable
        /// <c>UIHingeInteraction</c> (iOS &lt; 27.1, or a renamed class / initializer).
        /// </summary>
        internal static bool Start() => FDPIphoneDuo_Start() != 0;

        internal static void Stop() => FDPIphoneDuo_Stop();

        /// <summary>
        /// Pauses / resumes hinge delivery. Resuming makes UIKit report the current hinge state,
        /// which bumps <see cref="Version"/> when it changed while paused.
        /// </summary>
        internal static void SetEnabled(bool enabled) => FDPIphoneDuo_SetEnabled(enabled ? 1 : 0);

        /// <summary>Increments on every hinge status change (and on <see cref="Stop"/>).</summary>
        internal static int Version => FDPIphoneDuo_GetVersion();

        /// <summary>
        /// Locks the scene's interface orientation (<c>prefersInterfaceOrientationLocked</c>): turning
        /// the device keeps it, see <see cref="IphoneDuoFlatOrientation"/>.
        /// </summary>
        internal static void SetInterfaceOrientationLocked(bool locked) =>
            FDPIphoneDuo_SetInterfaceOrientationLocked(locked ? 1 : 0);

        /// <summary>True while iOS reports the Unity scene's interface orientation as locked.</summary>
        internal static bool IsInterfaceOrientationLocked => FDPIphoneDuo_IsInterfaceOrientationLocked() != 0;

        /// <summary>The Unity scene's interface orientation (0 when unknown).</summary>
        internal static IphoneDuoInterfaceOrientation InterfaceOrientation =>
            (IphoneDuoInterfaceOrientation)FDPIphoneDuo_GetInterfaceOrientation();

        /// <summary>Rotates the Unity scene to <paramref name="orientation"/>, also while locked.</summary>
        internal static void RequestInterfaceOrientation(IphoneDuoInterfaceOrientation orientation) =>
            FDPIphoneDuo_RequestInterfaceOrientation((int)orientation);

        /// <summary>
        /// Turns the Unity view by quarter turns (bounds swapped on odd turns) while iOS keeps the
        /// interface; 0 restores it. See <see cref="IphoneDuoRules.SidewaysQuarterTurns"/>.
        /// </summary>
        internal static void SetContentQuarterTurns(int turns) => FDPIphoneDuo_SetContentQuarterTurns(turns);

        /// <summary>
        /// UIInterfaceOrientationMask of the native launch hold (0 when none): the splash ran held
        /// across the Player lock under the posture's rule (Info.plist from IphoneDuoBuildPlist).
        /// </summary>
        internal static int LaunchHoldMask => FDPIphoneDuo_GetLaunchHoldMask();

        /// <summary>Quarter turns the native launch hold gave the view (0 when straight).</summary>
        internal static int LaunchHoldTurns => FDPIphoneDuo_GetLaunchHoldTurns();

        /// <summary>Hands the launch hold to C# (ends it once Unity applied the Screen flags written).</summary>
        internal static void ReleaseLaunchHold() => FDPIphoneDuo_ReleaseLaunchHold();

        internal static FoldableLayoutState ReadLayout() =>
            BuildLayout(
                (IphoneDuoHingeStatus)FDPIphoneDuo_GetStatus(),
                FDPIphoneDuo_GetViewPointWidth(),
                FDPIphoneDuo_GetViewPointHeight(),
                Screen.width,
                Screen.height);
#endif

        #endregion
    }
}
