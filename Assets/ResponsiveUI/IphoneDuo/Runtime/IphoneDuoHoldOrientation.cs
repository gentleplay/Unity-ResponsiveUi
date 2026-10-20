using UnityEngine;

namespace FDPlus.IphoneDuo
{
    /// <summary>
    /// Applies the <see cref="IphoneDuoRules"/> "across lock" rules for every posture: which axis iOS
    /// may use and whether the game is turned sideways. Seated allows only the hold's axis by default
    /// (the laptop hold of a landscape game gets the tall screen, Seated Portrait), because on the inner
    /// display iOS reports the laptop hold as a landscape device orientation and keeps a game that still
    /// allows landscape letterboxed. With <see cref="IphoneDuoSeatedRotation.Locked"/> the hold is the
    /// one the rules keep for the whole Seated period (<see cref="SeatedHoldDevice"/>), so turning the
    /// device does not re-orient the game; a Locked Flat does the same (<see cref="FlatHoldDevice"/>,
    /// <see cref="IphoneDuoRules.FlatKeepsHold"/>). Sideways keeps the game's orientation: iOS gets the interface of the
    /// hold's axis (an interface across the hold is letterboxed, with or without the interface
    /// orientation lock) and the Unity view is turned a quarter
    /// (<see cref="IphoneDuoHinge.SetContentQuarterTurns"/>); a landscape game held tall and a portrait
    /// game held wide are mirror cases. A tall hold always gets Portrait (iOS gives an iPhone app no
    /// Portrait Upside Down window), so on the side where Portrait Upside Down is upright the view turn
    /// makes up for it (<see cref="ViewTurns"/>). Unity's iOS view controller applies runtime
    /// <see cref="Screen"/> autorotation flags (iOS 16+), so the rules need no Info.plist change once
    /// C# runs (iOS builds still write the launch keys for the splash, <c>IphoneDuoBuildPlist</c>); a
    /// fixed Default Orientation ignores them. The decisions are pure (<see cref="Resolve"/>, <see cref="BuildState"/>,
    /// <see cref="IphoneDuoHoldLock"/>) so Editor tests and the Simulator preview share them; the
    /// Screen I/O runs on iOS players only.
    /// </summary>
    public static class IphoneDuoHoldOrientation
    {
        #region Decision

        /// <summary>
        /// Flags for the hold given by <paramref name="device"/>. The cover is an iPhone: the device
        /// orientation names the hold. The inner display's axes are turned 90° from it: a landscape
        /// device orientation is the tall hold and a portrait one the wide hold (both landscapes).
        /// A tall hold is always Portrait: iOS gives an iPhone app no Portrait Upside Down window (the
        /// scene can report it, but the window stays landscape-sized, verified in the Xcode iPhone Duo
        /// simulator), so the tall hold whose upright interface is Portrait Upside Down (Landscape
        /// Right) shows Portrait with the view turned (<see cref="ViewTurns"/>). False for face up /
        /// down / unknown, which give no hold.
        /// </summary>
        public static bool HoldFlags(IphoneDuoPosture posture, DeviceOrientation device, out IphoneDuoAutorotateFlags flags)
        {
            flags = default;
            if (posture == IphoneDuoPosture.Cover)
            {
                switch (device)
                {
                    case DeviceOrientation.Portrait:
                    case DeviceOrientation.PortraitUpsideDown:
                        flags.portrait = true;
                        return true;
                    case DeviceOrientation.LandscapeLeft: flags.landscapeLeft = true; return true;
                    case DeviceOrientation.LandscapeRight: flags.landscapeRight = true; return true;
                    default: return false;
                }
            }

            switch (device)
            {
                case DeviceOrientation.LandscapeLeft:
                case DeviceOrientation.LandscapeRight:
                    flags.portrait = true;
                    return true;
                case DeviceOrientation.Portrait:
                case DeviceOrientation.PortraitUpsideDown:
                    flags.landscapeLeft = true;
                    flags.landscapeRight = true;
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>Physical shape of the hold (<see cref="IphoneDuoHold.Unknown"/> without one).</summary>
        public static IphoneDuoHold Hold(IphoneDuoPosture posture, DeviceOrientation device) =>
            HoldFlags(posture, device, out var flags)
                ? flags.AllowsPortraitAxis ? IphoneDuoHold.Tall : IphoneDuoHold.Wide
                : IphoneDuoHold.Unknown;

        /// <summary>
        /// Device orientation of the Seated hold the rules keep (<paramref name="tall"/>: the laptop
        /// hold, from <see cref="IphoneDuoRules.SeatedHoldTall(IphoneDuoAutorotateFlags, bool, bool)"/>):
        /// <paramref name="device"/> when it is that hold, else <paramref name="kept"/> (the last one
        /// kept this Seated) when it is, else the hold's default, Landscape Left (laptop) or Portrait
        /// (book). Turning the device to the other hold so keeps the game as it is; turning it to the
        /// hold's other side still shows it upright (<see cref="ViewTurns"/>).
        /// </summary>
        public static DeviceOrientation SeatedHoldDevice(bool tall, DeviceOrientation device, DeviceOrientation kept)
        {
            IphoneDuoHold hold = tall ? IphoneDuoHold.Tall : IphoneDuoHold.Wide;
            if (Hold(IphoneDuoPosture.Seated, device) == hold)
                return device;
            if (Hold(IphoneDuoPosture.Seated, kept) == hold)
                return kept;
            return tall ? DeviceOrientation.LandscapeLeft : DeviceOrientation.Portrait;
        }

        /// <summary>
        /// Device orientation of the Flat hold a Locked Flat keeps (<paramref name="wide"/>: the wide
        /// hold, from <see cref="IphoneDuoRules.FlatHoldWide(IphoneDuoAutorotateFlags, bool, bool)"/>):
        /// <paramref name="device"/> when it is that hold, else <paramref name="kept"/> (the last one
        /// kept this Flat period) when it is, else the hold's default, Portrait (wide) or Landscape Left
        /// (tall). Turning the device to the other hold so keeps the game as it is; turning it to the
        /// hold's other side still shows it upright (<see cref="ViewTurns"/>).
        /// </summary>
        public static DeviceOrientation FlatHoldDevice(bool wide, DeviceOrientation device, DeviceOrientation kept)
        {
            IphoneDuoHold hold = wide ? IphoneDuoHold.Wide : IphoneDuoHold.Tall;
            if (Hold(IphoneDuoPosture.Flat, device) == hold)
                return device;
            if (Hold(IphoneDuoPosture.Flat, kept) == hold)
                return kept;
            return wide ? DeviceOrientation.Portrait : DeviceOrientation.LandscapeLeft;
        }

        /// <summary>
        /// Flags for a UIKit <c>UIInterfaceOrientationMask</c> (bit = 1 &lt;&lt; UIInterfaceOrientation;
        /// UIKit Landscape Right is Unity's Landscape Left and the reverse).
        /// </summary>
        public static IphoneDuoAutorotateFlags FromInterfaceMask(int mask) => new IphoneDuoAutorotateFlags(
            (mask & (1 << (int)IphoneDuoInterfaceOrientation.Portrait)) != 0,
            (mask & (1 << (int)IphoneDuoInterfaceOrientation.PortraitUpsideDown)) != 0,
            (mask & (1 << (int)IphoneDuoInterfaceOrientation.LandscapeRight)) != 0,
            (mask & (1 << (int)IphoneDuoInterfaceOrientation.LandscapeLeft)) != 0);

        /// <summary>
        /// True when the rules turn the Unity view for <paramref name="mode"/>: always Sideways, and
        /// <see cref="IphoneDuoScreenMode.HoldAxis"/> on a tall hold of the inner display, which shows
        /// Portrait also where Portrait Upside Down is upright (<see cref="HoldFlags"/>).
        /// </summary>
        public static bool TurnsView(IphoneDuoPosture posture, IphoneDuoScreenMode mode, IphoneDuoAutorotateFlags hold) =>
            mode == IphoneDuoScreenMode.Sideways ||
            (mode == IphoneDuoScreenMode.HoldAxis && posture != IphoneDuoPosture.Cover && hold.AllowsPortraitAxis);

        /// <summary>
        /// Quarter turns of the Unity view so the game reads upright whichever orientation of the hold's
        /// axis iOS shows (<paramref name="holdDevice"/> on the inner display gives the upright one).
        /// Sideways: <see cref="IphoneDuoRules.SidewaysQuarterTurns"/> from the upright interface, the
        /// opposite turn from the upside-down one. Hold axis: 180° from the upside-down one.
        /// </summary>
        public static int ViewTurns(
            IphoneDuoScreenMode mode,
            IphoneDuoRules rules,
            DeviceOrientation holdDevice,
            IphoneDuoInterfaceOrientation current)
        {
            IphoneDuoInterfaceOrientation upright = IphoneDuoFlatOrientation.UprightInterface(holdDevice);
            bool upsideDown = upright != IphoneDuoInterfaceOrientation.Unknown && current == Opposite(upright);
            switch (mode)
            {
                case IphoneDuoScreenMode.Sideways:
                    return upsideDown ? -rules.SidewaysQuarterTurns : rules.SidewaysQuarterTurns;
                case IphoneDuoScreenMode.HoldAxis:
                    return upsideDown ? 2 : 0;
                default:
                    return 0;
            }
        }

        static IphoneDuoInterfaceOrientation Opposite(IphoneDuoInterfaceOrientation orientation)
        {
            switch (orientation)
            {
                case IphoneDuoInterfaceOrientation.Portrait: return IphoneDuoInterfaceOrientation.PortraitUpsideDown;
                case IphoneDuoInterfaceOrientation.PortraitUpsideDown: return IphoneDuoInterfaceOrientation.Portrait;
                case IphoneDuoInterfaceOrientation.LandscapeLeft: return IphoneDuoInterfaceOrientation.LandscapeRight;
                case IphoneDuoInterfaceOrientation.LandscapeRight: return IphoneDuoInterfaceOrientation.LandscapeLeft;
                default: return IphoneDuoInterfaceOrientation.Unknown;
            }
        }

        /// <summary>True when <paramref name="hold"/> is across a one-axis <paramref name="project"/> lock.</summary>
        public static bool IsAcrossLock(IphoneDuoAutorotateFlags hold, IphoneDuoAutorotateFlags project) =>
            project.IsOneAxis && !hold.SharesAxis(project);

        /// <summary>
        /// Screen mode for a posture and hold under <paramref name="rules"/>: <see cref="IphoneDuoScreenMode.HoldAxis"/>
        /// allows only <paramref name="hold"/>'s axis, <see cref="IphoneDuoScreenMode.Sideways"/> does too
        /// and turns the game, <see cref="IphoneDuoScreenMode.Native"/> keeps <paramref name="project"/>.
        /// Seated always rotates a two-axis game to the hold (iOS keeps the old axis otherwise), and so
        /// does a Locked Flat (<see cref="IphoneDuoRules.FlatKeepsHold"/>); Flat follows
        /// <see cref="IphoneDuoRules.FlatAcrossLockFor"/>. Sideways
        /// on the cover is what iOS shows natively, and flat with the interface locked
        /// (<paramref name="flatLocked"/>) iOS already keeps the game sideways.
        /// </summary>
        public static IphoneDuoScreenMode Resolve(
            IphoneDuoPosture posture,
            IphoneDuoAutorotateFlags hold,
            IphoneDuoAutorotateFlags project,
            bool flatLocked,
            IphoneDuoRules rules)
        {
            if (posture == IphoneDuoPosture.Unknown)
                return IphoneDuoScreenMode.Native;
            if (!IsAcrossLock(hold, project))
            {
                bool keepsHold = posture == IphoneDuoPosture.Seated ||
                                 (posture == IphoneDuoPosture.Flat && rules.FlatKeepsHold(project));
                return keepsHold && !project.IsOneAxis && !hold.Equals(project)
                    ? IphoneDuoScreenMode.HoldAxis
                    : IphoneDuoScreenMode.Native;
            }

            IphoneDuoAcrossHold rule = posture == IphoneDuoPosture.Flat
                ? rules.FlatAcrossLockFor(project)
                : rules.AcrossLock(posture);
            switch (rule)
            {
                case IphoneDuoAcrossHold.Rotate:
                    return IphoneDuoScreenMode.HoldAxis;
                case IphoneDuoAcrossHold.Sideways:
                    return posture == IphoneDuoPosture.Cover || (posture == IphoneDuoPosture.Flat && flatLocked)
                        ? IphoneDuoScreenMode.Native
                        : IphoneDuoScreenMode.Sideways;
                default:
                    return IphoneDuoScreenMode.Native;
            }
        }

        /// <summary>
        /// Game-facing state: <paramref name="hold"/> is the flags of the hold the rules apply (default
        /// without one), <paramref name="mode"/> the rules' mode; with <paramref name="flatLocked"/> a
        /// <see cref="IphoneDuoScreenMode.Native"/> mode is the flat lock's
        /// <see cref="IphoneDuoScreenMode.Locked"/>.
        /// </summary>
        public static IphoneDuoState BuildState(
            IphoneDuoPosture posture,
            IphoneDuoAutorotateFlags hold,
            IphoneDuoAutorotateFlags project,
            IphoneDuoScreenMode mode,
            bool flatLocked)
        {
            bool hasHold = hold.AllowsPortraitAxis || hold.AllowsLandscapeAxis;
            IphoneDuoHold shape = !hasHold ? IphoneDuoHold.Unknown
                : hold.AllowsPortraitAxis ? IphoneDuoHold.Tall
                : IphoneDuoHold.Wide;
            if (mode == IphoneDuoScreenMode.Native && flatLocked)
                mode = IphoneDuoScreenMode.Locked;
            return new IphoneDuoState(posture, shape, hasHold && IsAcrossLock(hold, project), mode);
        }

        #endregion

        #region Player

#if UNITY_IOS && !UNITY_EDITOR
        static readonly IphoneDuoHoldLock s_Lock = new IphoneDuoHoldLock();

        /// <summary>
        /// Called every frame by the iOS posture owner; <paramref name="flatLocked"/> is the flat lock
        /// keeping the interface orientation (<see cref="IphoneDuoFlatOrientation"/>).
        /// </summary>
        internal static void Tick(IphoneDuoPosture posture, bool flatLocked, IphoneDuoRules rules)
        {
            Apply(s_Lock.Step(posture, Input.deviceOrientation, ReadFlags(), flatLocked, Time.unscaledTime, rules), rules);
            // Every frame: the hold may change to the other side, and UIKit may lay the root view out
            // again (the native call is a no-op when already turned).
            if (s_Lock.IsTurning && s_Lock.HoldDevice != DeviceOrientation.Unknown)
                IphoneDuoHinge.SetContentQuarterTurns(Turns(rules));
        }

        /// <summary>
        /// First posture frame: adopts the native launch hold (the splash shown under the posture's
        /// rule) so the rules continue from it, then hands it back to C#. A no-op when there is none.
        /// </summary>
        internal static void AdoptLaunchHold(IphoneDuoRules rules)
        {
            int mask = IphoneDuoHinge.LaunchHoldMask;
            if (mask != 0)
                Apply(s_Lock.AdoptHold(ReadFlags(), FromInterfaceMask(mask), IphoneDuoHinge.LaunchHoldTurns), rules);
            IphoneDuoHinge.ReleaseLaunchHold();
        }

        /// <summary>Puts the project's flags back and the view straight now (posture owner detached).</summary>
        internal static void Restore() => Apply(s_Lock.Restore(), IphoneDuoDevice.Rules);

        /// <summary>True while the rules own the autorotation flags or the view turn.</summary>
        internal static bool IsActive => s_Lock.IsActive;

        /// <summary>Game-facing state for <paramref name="posture"/> from what the rules apply now.</summary>
        internal static IphoneDuoState CurrentState(IphoneDuoPosture posture, bool flatLocked) =>
            s_Lock.State(posture, flatLocked);

        static void Apply(IphoneDuoHoldLock.Writes writes, IphoneDuoRules rules)
        {
            if (writes.writeTurn)
            {
#if DEVELOPMENT_BUILD
                if (rules.traceLog)
                    Debug.Log($"iPhone Duo hold: turn {writes.turn} mode {s_Lock.Mode} device {Input.deviceOrientation}");
#endif
                // Before Unity reports a device orientation the turn already shown (launch hold) stays.
                if (!writes.turn)
                    IphoneDuoHinge.SetContentQuarterTurns(0);
                else if (s_Lock.HoldDevice != DeviceOrientation.Unknown)
                    IphoneDuoHinge.SetContentQuarterTurns(Turns(rules));
            }
            if (writes.writeFlags)
                WriteFlags(writes.flags);
        }

        static int Turns(IphoneDuoRules rules) =>
            ViewTurns(s_Lock.Mode, rules, s_Lock.HoldDevice, IphoneDuoHinge.InterfaceOrientation);

        internal static IphoneDuoAutorotateFlags ReadFlags() => new IphoneDuoAutorotateFlags(
            Screen.autorotateToPortrait,
            Screen.autorotateToPortraitUpsideDown,
            Screen.autorotateToLandscapeLeft,
            Screen.autorotateToLandscapeRight);

        static void WriteFlags(IphoneDuoAutorotateFlags flags)
        {
            // Enable before disable: Unity forces Portrait back on when every flag is off.
            if (flags.portrait)
                Screen.autorotateToPortrait = true;
            if (flags.portraitUpsideDown)
                Screen.autorotateToPortraitUpsideDown = true;
            if (flags.landscapeLeft)
                Screen.autorotateToLandscapeLeft = true;
            if (flags.landscapeRight)
                Screen.autorotateToLandscapeRight = true;
            if (!flags.portrait)
                Screen.autorotateToPortrait = false;
            if (!flags.portraitUpsideDown)
                Screen.autorotateToPortraitUpsideDown = false;
            if (!flags.landscapeLeft)
                Screen.autorotateToLandscapeLeft = false;
            if (!flags.landscapeRight)
                Screen.autorotateToLandscapeRight = false;
        }
#endif

        #endregion
    }
}
