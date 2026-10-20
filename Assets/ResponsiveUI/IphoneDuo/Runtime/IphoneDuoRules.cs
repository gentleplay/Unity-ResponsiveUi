using System;
using UnityEngine;

namespace FDPlus.IphoneDuo
{
    /// <summary>
    /// How the game behaves on each iPhone Duo posture. Edited in Project Settings › Responsive UI ›
    /// iPhone Duo (saved to <see cref="IphoneDuoSettings.ResourcesPath"/>); read and changed at runtime
    /// through <see cref="IphoneDuoDevice.Rules"/>. The "across lock" rules apply to a one-axis Player lock
    /// (Default Orientation = Auto Rotation with only landscape or only portrait allowed) when the
    /// display is held the other way, e.g. a landscape game held tall.
    /// </summary>
    [Serializable]
    public sealed class IphoneDuoRules
    {
        #region Defaults

        public const IphoneDuoAcrossHold DefaultCoverAcrossLock = IphoneDuoAcrossHold.SystemDefault;
        public const IphoneDuoAcrossHold DefaultSeatedAcrossLock = IphoneDuoAcrossHold.Rotate;
        public const IphoneDuoAcrossHold DefaultFlatAcrossLock = IphoneDuoAcrossHold.Sideways;
        public const IphoneDuoSeatedRotation DefaultSeatedRotation = IphoneDuoSeatedRotation.Locked;
        public const IphoneDuoSeatedPose DefaultSeatedLockedPose = IphoneDuoSeatedPose.Auto;
        public const IphoneDuoFlatRotation DefaultFlatRotation = IphoneDuoFlatRotation.FollowDevice;
        public const IphoneDuoFlatPose DefaultFlatLockedPose = IphoneDuoFlatPose.Auto;
        public const float DefaultLeaveSeatedDelaySeconds = 0.3f;
        public const float DefaultLockSettleSeconds = 0.5f;
        public const float DefaultRequestRetrySeconds = 0.5f;

        #endregion

        #region Rules

        [Tooltip("Closed (cover display) held across the Player lock. System Default: iOS shows the game sideways, like any iPhone. Rotate: the game rotates to the hold.")]
        public IphoneDuoAcrossHold coverAcrossLock = DefaultCoverAcrossLock;

        [Tooltip("Seated held across the Player lock (the laptop hold for a landscape game). Rotate: tall full screen, Seated Portrait slots. Sideways: the game keeps its orientation, turned a quarter. System Default: iOS letterboxes it.")]
        public IphoneDuoAcrossHold seatedAcrossLock = DefaultSeatedAcrossLock;

        [Tooltip("Flat held across the Player lock (a landscape game held tall). Sideways: the game stays as it is, full screen (Android Fold style), also after a cold start or unfolding tall. Rotate: the game rotates to the hold. System Default: iOS letterboxes it.")]
        public IphoneDuoAcrossHold flatAcrossLock = DefaultFlatAcrossLock;

        [Tooltip("Flat: turning the device to the opposite orientation of the game's axis flips the game 180° upright. Off: the game keeps its orientation (upside down).")]
        public bool flatFlip180 = true;

        [Tooltip("Which way the game is turned in Sideways mode, where iOS keeps the interface of the hold's axis and the view is turned (from Seated, a cold start or an unfold while held across the lock).")]
        public IphoneDuoSidewaysTurn sidewaysTurn = IphoneDuoSidewaysTurn.Left90;

        #endregion

        #region Seated

        [Tooltip("Turning the device while Seated. Locked: Seated keeps one pose (Locked pose); only flattening or closing ends it. Follows hold: laptop hold → Seated Portrait, book hold → Seated Landscape.")]
        public IphoneDuoSeatedRotation seatedRotation = DefaultSeatedRotation;

        [Tooltip("The pose a Locked Seated keeps. Auto: a landscape-only game is Seated Portrait (tall laptop), a portrait-only game Seated Landscape (wide book); with both axes allowed, the hold Seated was entered with.")]
        public IphoneDuoSeatedPose seatedLockedPose = DefaultSeatedLockedPose;

        #endregion

        #region Flat

        [Tooltip("Turning the device while Flat. Follow device: today's rules (the across-lock rule and flip 180°). Locked: Flat keeps one screen (Locked pose) until folded or closed; the flipped side of that hold still shows it upright.")]
        public IphoneDuoFlatRotation flatRotation = DefaultFlatRotation;

        [Tooltip("The screen a Locked Flat keeps. Auto: a landscape-only game is wide (Pad · Landscape), a portrait-only game tall (Pad · Portrait); with both axes allowed, the hold Flat was entered with.")]
        public IphoneDuoFlatPose flatLockedPose = DefaultFlatLockedPose;

        #endregion

        #region Launch (iOS build)

        [Tooltip("iOS builds: iOS's launch screen fills the display when started held across the Player lock (Info.plist lists the other axis for iPhones). Side effect on every iPhone: launched held that way, the launch screen shows on the other axis, then the game rotates to its own. Off: the launch screen is letterboxed on the iPhone Duo inner display until the game starts.")]
        public bool launchScreenFillsAcrossHold;

        #endregion

        #region Timings

        [Tooltip("Seconds the posture must stay out of Seated before the cover rules apply. iOS resizes the scene in steps while it rotates, which can read as Closed for a frame.")]
        [Min(0f)] public float leaveSeatedDelaySeconds = DefaultLeaveSeatedDelaySeconds;

        [Tooltip("Flat: seconds the game must stay upright before the orientation lock returns (covers the rotation animation).")]
        [Min(0f)] public float lockSettleSeconds = DefaultLockSettleSeconds;

        [Tooltip("Flat: seconds before a flip request that iOS has not carried out is sent again.")]
        [Min(0.05f)] public float requestRetrySeconds = DefaultRequestRetrySeconds;

        #endregion

        #region Diagnostics

        [Tooltip("Development builds: log one line per posture / orientation change (Xcode console).")]
        public bool traceLog = true;

        [Tooltip("Development builds and the Editor: show the posture, hold and screen mode in a corner of the screen.")]
        public bool debugOverlay;

        #endregion

        /// <summary>The across-lock rule for <paramref name="posture"/> (System Default when unknown).</summary>
        public IphoneDuoAcrossHold AcrossLock(IphoneDuoPosture posture)
        {
            switch (posture)
            {
                case IphoneDuoPosture.Cover: return coverAcrossLock;
                case IphoneDuoPosture.Seated: return seatedAcrossLock;
                case IphoneDuoPosture.Flat: return flatAcrossLock;
                default: return IphoneDuoAcrossHold.SystemDefault;
            }
        }

        /// <summary>
        /// The Seated hold under these rules (true: tall laptop = Seated Portrait), see
        /// <see cref="SeatedHoldTall(IphoneDuoSeatedRotation, IphoneDuoSeatedPose, IphoneDuoAutorotateFlags, bool, bool)"/>.
        /// </summary>
        public bool SeatedHoldTall(IphoneDuoAutorotateFlags project, bool entryTall, bool holdTall) =>
            SeatedHoldTall(seatedRotation, seatedLockedPose, project, entryTall, holdTall);

        /// <summary>
        /// The Seated hold (true: tall laptop = Seated Portrait; false: wide book = Seated Landscape).
        /// <see cref="IphoneDuoSeatedRotation.FollowsHold"/>: the current hold <paramref name="holdTall"/>.
        /// Locked: <paramref name="lockedPose"/>; Auto from the Player flags <paramref name="project"/>
        /// (landscape only → tall, portrait only → wide, both axes → the hold Seated was entered with,
        /// <paramref name="entryTall"/>).
        /// </summary>
        public static bool SeatedHoldTall(
            IphoneDuoSeatedRotation rotation,
            IphoneDuoSeatedPose lockedPose,
            IphoneDuoAutorotateFlags project,
            bool entryTall,
            bool holdTall)
        {
            if (rotation == IphoneDuoSeatedRotation.FollowsHold)
                return holdTall;
            switch (lockedPose)
            {
                case IphoneDuoSeatedPose.SeatedPortrait: return true;
                case IphoneDuoSeatedPose.SeatedLandscape: return false;
                default: return project.IsOneAxis ? project.AllowsLandscapeAxis : entryTall;
            }
        }

        /// <summary>
        /// The Flat hold under these rules (true: wide = Pad · Landscape), see
        /// <see cref="FlatHoldWide(IphoneDuoFlatRotation, IphoneDuoFlatPose, IphoneDuoAutorotateFlags, bool, bool)"/>.
        /// </summary>
        public bool FlatHoldWide(IphoneDuoAutorotateFlags project, bool entryWide, bool holdWide) =>
            FlatHoldWide(flatRotation, flatLockedPose, project, entryWide, holdWide);

        /// <summary>
        /// The Flat hold (true: wide = Pad · Landscape; false: tall = Pad · Portrait).
        /// <see cref="IphoneDuoFlatRotation.FollowDevice"/>: the current hold <paramref name="holdWide"/>.
        /// Locked: <paramref name="lockedPose"/>; Auto from the Player flags <paramref name="project"/>
        /// (landscape only → wide, portrait only → tall, both axes → the hold Flat was entered with,
        /// <paramref name="entryWide"/>).
        /// </summary>
        public static bool FlatHoldWide(
            IphoneDuoFlatRotation rotation,
            IphoneDuoFlatPose lockedPose,
            IphoneDuoAutorotateFlags project,
            bool entryWide,
            bool holdWide)
        {
            if (rotation == IphoneDuoFlatRotation.FollowDevice)
                return holdWide;
            switch (lockedPose)
            {
                case IphoneDuoFlatPose.Wide: return true;
                case IphoneDuoFlatPose.Tall: return false;
                default: return project.IsOneAxis ? project.AllowsLandscapeAxis : entryWide;
            }
        }

        /// <summary>
        /// True when a Locked Flat keeps a hold of its own against the device
        /// (<see cref="FlatHoldWide(IphoneDuoAutorotateFlags, bool, bool)"/>): with both Player axes, or a
        /// one-axis game whose Locked pose is across its lock. A one-axis game locked on its own axis
        /// keeps that screen anyway: the real hold stays and held across it the game turns sideways
        /// (<see cref="FlatAcrossLockFor"/>).
        /// </summary>
        public bool FlatKeepsHold(IphoneDuoAutorotateFlags project) =>
            flatRotation == IphoneDuoFlatRotation.Locked &&
            (!project.IsOneAxis || FlatHoldWide(project, false, false) != project.AllowsLandscapeAxis);

        /// <summary>
        /// The Flat across-lock rule for <paramref name="project"/>: <see cref="flatAcrossLock"/>, except
        /// that a Locked Flat on a one-axis game's own axis never rotates, so Rotate keeps the game
        /// sideways (full screen) like <see cref="IphoneDuoAcrossHold.Sideways"/>.
        /// </summary>
        public IphoneDuoAcrossHold FlatAcrossLockFor(IphoneDuoAutorotateFlags project) =>
            flatAcrossLock == IphoneDuoAcrossHold.Rotate &&
            flatRotation == IphoneDuoFlatRotation.Locked &&
            project.IsOneAxis &&
            !FlatKeepsHold(project)
                ? IphoneDuoAcrossHold.Sideways
                : flatAcrossLock;

        /// <summary>
        /// Quarter turns of the Unity view in Sideways mode, from the interface upright for the hold
        /// (+1 = Left 90°, the turn verified on the Xcode iPhone Duo simulator; −1 = Right 90°).
        /// </summary>
        public int SidewaysQuarterTurns => sidewaysTurn == IphoneDuoSidewaysTurn.Right90 ? -1 : 1;

        public IphoneDuoRules Clone() => (IphoneDuoRules)MemberwiseClone();
    }
}
