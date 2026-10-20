using UnityEngine;

namespace FDPlus.IphoneDuo
{
    /// <summary>
    /// Hold state for <see cref="IphoneDuoHoldOrientation"/>, pure so Editor tests cover it: feeds the
    /// posture, device orientation and the project's autorotation flags through
    /// <see cref="IphoneDuoHoldOrientation.Resolve"/> and returns the <see cref="Writes"/> to apply.
    /// </summary>
    internal sealed class IphoneDuoHoldLock
    {
        #region Types

        /// <summary>What to change this frame.</summary>
        internal struct Writes
        {
            /// <summary>Write <see cref="flags"/> as the autorotation flags.</summary>
            public bool writeFlags;
            public IphoneDuoAutorotateFlags flags;
            /// <summary>
            /// Start turning the view with the hold (<see cref="turn"/>, see
            /// <see cref="IphoneDuoHoldOrientation.ViewTurns"/>) or put it straight.
            /// </summary>
            public bool writeTurn;
            public bool turn;
        }

        #endregion

        #region State

        IphoneDuoAutorotateFlags project;
        IphoneDuoAutorotateFlags written;
        bool overriding;
        bool sideways;
        bool turning;
        IphoneDuoPosture applied = IphoneDuoPosture.Unknown;
        DeviceOrientation lastDevice = DeviceOrientation.Unknown;
        // The hold the rules apply: its device orientation (Seated without one assumes the laptop
        // hold) and flags (default without a hold).
        DeviceOrientation holdDevice = DeviceOrientation.Unknown;
        IphoneDuoAutorotateFlags holdFlags;
        float leftSeatedAt = -1f;
        // This Seated period: the hold it was entered with (known once a device orientation was
        // reported) and the device orientation of the hold the rules keep.
        bool seatedEntryTall;
        bool seatedEntryKnown;
        DeviceOrientation seatedDevice = DeviceOrientation.Unknown;
        // The same for this Flat period (a Locked Flat, IphoneDuoRules.FlatKeepsHold).
        bool flatEntryWide;
        bool flatEntryKnown;
        DeviceOrientation flatDevice = DeviceOrientation.Unknown;

        #endregion

        /// <summary>True while the rules own the autorotation flags or the view turn.</summary>
        public bool IsActive => overriding || turning;

        /// <summary>True while the game is turned sideways.</summary>
        public bool IsSideways => sideways;

        /// <summary>True while the view turn follows the hold (<see cref="IphoneDuoHoldOrientation.TurnsView"/>).</summary>
        public bool IsTurning => turning;

        /// <summary>
        /// Device orientation of the hold the rules apply: the last one reported on this display, the
        /// laptop hold for Seated before any; Unknown otherwise (e.g. flat face up at launch).
        /// </summary>
        public DeviceOrientation HoldDevice => holdDevice;

        public IphoneDuoScreenMode Mode =>
            sideways ? IphoneDuoScreenMode.Sideways
            : overriding ? IphoneDuoScreenMode.HoldAxis
            : IphoneDuoScreenMode.Native;

        /// <summary>
        /// Game-facing state for <paramref name="posture"/> from what the rules apply now;
        /// <paramref name="flatLocked"/> is the flat lock (<see cref="IphoneDuoScreenMode.Locked"/>).
        /// </summary>
        public IphoneDuoState State(IphoneDuoPosture posture, bool flatLocked) =>
            IphoneDuoHoldOrientation.BuildState(posture, holdFlags, project, Mode, flatLocked);

        #region Steps

        /// <summary>
        /// One posture frame. Face up / down / unknown keep the last hold of the same display (Seated
        /// starts in the laptop hold); without one, the cover and a display switch keep the project's
        /// flags. Seated applies the hold the rules keep
        /// (<see cref="IphoneDuoRules.SeatedHoldTall(IphoneDuoAutorotateFlags, bool, bool)"/>): Locked keeps
        /// one for the whole Seated period, Follows hold the current one. A Locked Flat that keeps a hold
        /// of its own (<see cref="IphoneDuoRules.FlatKeepsHold"/>) keeps one for the whole Flat period
        /// the same way. Leaving Seated for the cover
        /// waits <see cref="IphoneDuoRules.leaveSeatedDelaySeconds"/>:
        /// iOS resizes the scene in steps while it rotates, which can read as Closed for a frame.
        /// </summary>
        public Writes Step(
            IphoneDuoPosture posture,
            DeviceOrientation device,
            IphoneDuoAutorotateFlags current,
            bool flatLocked,
            float now,
            IphoneDuoRules rules)
        {
            if (!IsActive)
                project = current;

            if (applied == IphoneDuoPosture.Seated && posture == IphoneDuoPosture.Cover && IsActive)
            {
                if (leftSeatedAt < 0f)
                    leftSeatedAt = now;
                if (now - leftSeatedAt < rules.leaveSeatedDelaySeconds)
                    return default;
            }
            leftSeatedAt = -1f;

            // The cover and the inner display name device orientations differently.
            bool displayChanged = (posture == IphoneDuoPosture.Cover) != (applied == IphoneDuoPosture.Cover);
            if (displayChanged)
                ForgetHold();
            if (posture == IphoneDuoPosture.Seated && applied != IphoneDuoPosture.Seated)
            {
                seatedEntryKnown = false;
                seatedDevice = DeviceOrientation.Unknown;
            }
            if (posture == IphoneDuoPosture.Flat && applied != IphoneDuoPosture.Flat)
            {
                flatEntryKnown = false;
                flatDevice = DeviceOrientation.Unknown;
            }
            applied = posture;

            if (posture == IphoneDuoPosture.Unknown)
                return Apply(false, default, IphoneDuoScreenMode.Native, false);

            if (IphoneDuoHoldOrientation.HoldFlags(posture, device, out _))
                lastDevice = device;
            DeviceOrientation effective = lastDevice == DeviceOrientation.Unknown && posture == IphoneDuoPosture.Seated
                ? DeviceOrientation.LandscapeLeft
                : lastDevice;
            if (posture == IphoneDuoPosture.Seated)
                effective = SeatedHold(effective, rules);
            else if (posture == IphoneDuoPosture.Flat && effective != DeviceOrientation.Unknown)
                effective = FlatHold(effective, rules);
            if (!IphoneDuoHoldOrientation.HoldFlags(posture, effective, out var hold))
            {
                // No hold yet. On the inner display the last one stays (a flat launch keeps the native
                // launch hold); on another display it no longer applies (e.g. folded to the cover face up).
                return displayChanged || posture == IphoneDuoPosture.Cover
                    ? Apply(false, default, IphoneDuoScreenMode.Native, false)
                    : default;
            }
            holdDevice = effective;
            holdFlags = hold;

            IphoneDuoScreenMode mode = IphoneDuoHoldOrientation.Resolve(posture, hold, project, flatLocked, rules);
            return Apply(mode != IphoneDuoScreenMode.Native, hold, mode,
                IphoneDuoHoldOrientation.TurnsView(posture, mode, hold));
        }

        /// <summary>
        /// Takes over the native launch hold: the splash already shows the game with only
        /// <paramref name="hold"/> allowed, the view turned by <paramref name="turns"/> quarter turns
        /// (odd: sideways). Returns the write that makes Unity's Screen flags match, so the native hold
        /// can end without iOS seeing the Player lock alone in between.
        /// </summary>
        public Writes AdoptHold(IphoneDuoAutorotateFlags current, IphoneDuoAutorotateFlags hold, int turns)
        {
            project = current;
            overriding = true;
            written = hold;
            holdFlags = hold;
            sideways = (turns & 1) != 0;
            turning = turns != 0;
            return new Writes { writeFlags = true, flags = hold };
        }

        /// <summary>Puts the project's flags back and the view straight (posture owner detached).</summary>
        public Writes Restore()
        {
            applied = IphoneDuoPosture.Unknown;
            ForgetHold();
            leftSeatedAt = -1f;
            return Apply(false, default, IphoneDuoScreenMode.Native, false);
        }

        /// <summary>
        /// Device orientation of the Seated hold the rules keep for the current hold
        /// <paramref name="effective"/>. Follows hold: <paramref name="effective"/> itself.
        /// </summary>
        DeviceOrientation SeatedHold(DeviceOrientation effective, IphoneDuoRules rules)
        {
            bool holdTall = IphoneDuoHoldOrientation.Hold(IphoneDuoPosture.Seated, effective) == IphoneDuoHold.Tall;
            // Seated without a hold assumes the laptop hold until the first one is reported.
            if (!seatedEntryKnown)
            {
                seatedEntryTall = holdTall;
                seatedEntryKnown = lastDevice != DeviceOrientation.Unknown;
            }
            seatedDevice = IphoneDuoHoldOrientation.SeatedHoldDevice(
                rules.SeatedHoldTall(project, seatedEntryTall, holdTall), effective, seatedDevice);
            return seatedDevice;
        }

        /// <summary>
        /// Device orientation of the Flat hold the rules keep for the current hold
        /// <paramref name="effective"/> (reported, not Unknown). Follow device, or a one-axis game
        /// locked on its own axis: <paramref name="effective"/> itself.
        /// </summary>
        DeviceOrientation FlatHold(DeviceOrientation effective, IphoneDuoRules rules)
        {
            bool holdWide = IphoneDuoHoldOrientation.Hold(IphoneDuoPosture.Flat, effective) == IphoneDuoHold.Wide;
            if (!flatEntryKnown)
            {
                flatEntryWide = holdWide;
                flatEntryKnown = true;
            }
            if (!rules.FlatKeepsHold(project))
                return effective;
            flatDevice = IphoneDuoHoldOrientation.FlatHoldDevice(
                rules.FlatHoldWide(project, flatEntryWide, holdWide), effective, flatDevice);
            return flatDevice;
        }

        void ForgetHold()
        {
            lastDevice = DeviceOrientation.Unknown;
            holdDevice = DeviceOrientation.Unknown;
            holdFlags = default;
        }

        Writes Apply(bool overrideFlags, IphoneDuoAutorotateFlags hold, IphoneDuoScreenMode mode, bool turn)
        {
            var writes = default(Writes);
            sideways = mode == IphoneDuoScreenMode.Sideways;
            if (turn != turning)
            {
                turning = turn;
                writes.writeTurn = true;
                writes.turn = turn;
            }

            if (overrideFlags)
            {
                if (!overriding || !written.Equals(hold))
                {
                    overriding = true;
                    written = hold;
                    writes.writeFlags = true;
                    writes.flags = hold;
                }
            }
            else if (overriding)
            {
                overriding = false;
                writes.writeFlags = true;
                writes.flags = project;
            }
            return writes;
        }

        #endregion
    }
}
