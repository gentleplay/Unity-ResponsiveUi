using UnityEngine;

namespace FDPlus.IphoneDuo
{
    /// <summary>
    /// Flat (Opened) inner display with a one-axis Player lock, Android Fold style by default: turning
    /// the device across the lock keeps the game as it is (full screen, sideways) instead of iOS
    /// letterboxing it, and turning it to the other orientation of the game's axis flips the game 180°
    /// (<see cref="IphoneDuoRules.flatAcrossLock"/>, <see cref="IphoneDuoRules.flatFlip180"/>). iOS keeps
    /// the interface orientation while <c>prefersInterfaceOrientationLocked</c> is set
    /// (<see cref="IphoneDuoHinge"/>); the flip releases the lock, requests the upright interface and
    /// locks again once iOS is there, because a 180° rotation requested while locked is reported but
    /// not shown. The decisions are pure (<see cref="IphoneDuoFlatLock"/>); the UIKit I/O runs on iOS
    /// only. The other postures follow <see cref="IphoneDuoHoldOrientation"/>.
    /// </summary>
    public static class IphoneDuoFlatOrientation
    {
        #region Decision

        /// <summary>
        /// The interface orientation that is upright for <paramref name="device"/> on the iPhone Duo
        /// inner display, whose device orientation axes are turned 90° from the panel: the wide hold
        /// reports Portrait (upright interface Landscape Left) and the tall hold reports a landscape
        /// device orientation (upright interface Portrait / Upside Down). <c>Unknown</c> for face up /
        /// down / unknown.
        /// </summary>
        public static IphoneDuoInterfaceOrientation UprightInterface(DeviceOrientation device)
        {
            switch (device)
            {
                case DeviceOrientation.Portrait: return IphoneDuoInterfaceOrientation.LandscapeLeft;
                case DeviceOrientation.PortraitUpsideDown: return IphoneDuoInterfaceOrientation.LandscapeRight;
                case DeviceOrientation.LandscapeLeft: return IphoneDuoInterfaceOrientation.Portrait;
                case DeviceOrientation.LandscapeRight: return IphoneDuoInterfaceOrientation.PortraitUpsideDown;
                default: return IphoneDuoInterfaceOrientation.Unknown;
            }
        }

        /// <summary>True when <paramref name="flags"/> allow <paramref name="orientation"/>.</summary>
        public static bool Allows(
            IphoneDuoAutorotateFlags flags,
            IphoneDuoInterfaceOrientation orientation)
        {
            switch (orientation)
            {
                case IphoneDuoInterfaceOrientation.Portrait: return flags.portrait;
                case IphoneDuoInterfaceOrientation.PortraitUpsideDown: return flags.portraitUpsideDown;
                // UIKit Landscape Right is Unity's Landscape Left and the reverse.
                case IphoneDuoInterfaceOrientation.LandscapeRight: return flags.landscapeLeft;
                case IphoneDuoInterfaceOrientation.LandscapeLeft: return flags.landscapeRight;
                default: return false;
            }
        }

        #endregion

        #region Player

#if UNITY_IOS && !UNITY_EDITOR
        static readonly IphoneDuoFlatLock s_Lock = new IphoneDuoFlatLock();
#if DEVELOPMENT_BUILD
        static string s_LastTrace;
#endif

        /// <summary>Called every frame by the iOS posture owner; <paramref name="opened"/> is the flat inner display.</summary>
        internal static void Tick(bool opened, IphoneDuoRules rules)
        {
            DeviceOrientation device = Input.deviceOrientation;
            IphoneDuoInterfaceOrientation current = IphoneDuoHinge.InterfaceOrientation;
            IphoneDuoFlatLock.Writes writes = s_Lock.Step(
                opened, Time.unscaledTime, IphoneDuoHoldOrientation.ReadFlags(), device, current, rules);
#if DEVELOPMENT_BUILD
            // Development players: one line per change, for simulator / hardware QA.
            if (rules.traceLog)
                Trace(opened, device, current, writes);
#endif
            Apply(writes);
        }

#if DEVELOPMENT_BUILD
        static void Trace(
            bool opened,
            DeviceOrientation device,
            IphoneDuoInterfaceOrientation current,
            IphoneDuoFlatLock.Writes writes)
        {
            string trace = $"opened {opened} device {device} interface {current} " +
                           $"locked {s_Lock.IsLocked}/{IphoneDuoHinge.IsInterfaceOrientationLocked}";
            if (trace != s_LastTrace || writes.writeLock || writes.request != IphoneDuoInterfaceOrientation.Unknown)
            {
                s_LastTrace = trace;
                Debug.Log($"iPhone Duo flat: {trace}" +
                          (writes.writeLock ? $" -> lock {writes.locked}" : "") +
                          (writes.request != IphoneDuoInterfaceOrientation.Unknown ? $" -> request {writes.request}" : ""));
            }
        }
#endif

        /// <summary>True while the flat lock keeps the interface orientation.</summary>
        internal static bool IsLocked => s_Lock.IsLocked;

        /// <summary>Releases the lock now (posture owner detached).</summary>
        internal static void Restore() => Apply(s_Lock.Unlock());

        static void Apply(IphoneDuoFlatLock.Writes writes)
        {
            if (writes.writeLock)
                IphoneDuoHinge.SetInterfaceOrientationLocked(writes.locked);
            if (writes.request != IphoneDuoInterfaceOrientation.Unknown)
                IphoneDuoHinge.RequestInterfaceOrientation(writes.request);
        }
#endif

        #endregion
    }
}
