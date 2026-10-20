using UnityEngine;

namespace FDPlus.IphoneDuo
{
    /// <summary>
    /// Flat (Opened) inner display orientation state, pure so Editor tests cover it.
    /// <see cref="IphoneDuoFlatOrientation"/> feeds it the posture, project flags, device and interface
    /// orientation and applies the <see cref="Writes"/> it returns.
    /// </summary>
    internal sealed class IphoneDuoFlatLock
    {
        #region Types

        /// <summary>What to change this frame.</summary>
        internal struct Writes
        {
            /// <summary>Set the interface orientation lock to <see cref="locked"/>.</summary>
            public bool writeLock;
            public bool locked;
            /// <summary>Interface orientation to rotate to; <c>Unknown</c> for none.</summary>
            public IphoneDuoInterfaceOrientation request;
        }

        #endregion

        #region State

        bool locked;
        // The game has been upright and full screen since entering Opened (safe to lock the tall hold).
        bool synced;
        DeviceOrientation hold = DeviceOrientation.Unknown;
        float uprightSince = -1f;
        IphoneDuoInterfaceOrientation requested;
        float requestedAt;

        #endregion

        public bool IsLocked => locked;

        #region Steps

        /// <summary>
        /// One frame. While <paramref name="opened"/> with a one-axis project the lock keeps the game as
        /// it is when the device turns across the lock (<see cref="IphoneDuoRules.flatAcrossLock"/> =
        /// Sideways, also for Rotate under a Locked Flat on the game's own axis,
        /// <see cref="IphoneDuoRules.FlatAcrossLockFor"/>; once it has been upright, so a letterboxed start is never locked), and each new hold
        /// on the game's axis releases the lock, requests the upright interface (the 180° flip,
        /// <see cref="IphoneDuoRules.flatFlip180"/>) and locks again once iOS has stayed there for
        /// <see cref="IphoneDuoRules.lockSettleSeconds"/>. The lock must be off for the flip: while it
        /// is on, iOS reports a requested 180° rotation but keeps showing the old one. Without the flip
        /// the lock stays on in every hold of the game's axis. With neither, iOS rotates natively. A
        /// Locked Flat with a hold of its own (<see cref="IphoneDuoRules.FlatKeepsHold"/>) leaves the
        /// screen to <see cref="IphoneDuoHoldLock"/>: the lock stays off.
        /// </summary>
        public Writes Step(
            bool opened,
            float now,
            IphoneDuoAutorotateFlags project,
            DeviceOrientation device,
            IphoneDuoInterfaceOrientation current,
            IphoneDuoRules rules)
        {
            bool lockAcross = rules.FlatAcrossLockFor(project) == IphoneDuoAcrossHold.Sideways;
            if (!opened || !project.IsOneAxis || (!lockAcross && rules.flatFlip180) || rules.FlatKeepsHold(project))
                return Unlock();

            IphoneDuoInterfaceOrientation upright = IphoneDuoFlatOrientation.UprightInterface(device);
            // Face up / down / unknown: keep the last hold.
            if (upright == IphoneDuoInterfaceOrientation.Unknown)
                return default;

            if (!IphoneDuoFlatOrientation.Allows(project, upright))
            {
                // Across the lock (tall hold for a landscape game): keep the game as it is, or leave the
                // hold to the Rotate / System Default rule.
                hold = device;
                uprightSince = -1f;
                if (!lockAcross)
                    return locked ? WriteLock(false) : default;
                return locked || !synced ? default : WriteLock(true);
            }

            if (device != hold)
            {
                // New hold on the game's axis: let iOS rotate to it (the flip), or keep it locked.
                hold = device;
                uprightSince = -1f;
                requested = IphoneDuoInterfaceOrientation.Unknown;
                if (locked && rules.flatFlip180)
                    return WriteLock(false);
            }
            if (locked)
                return default;

            // Without the flip any orientation of the game's axis is fine to lock.
            bool settled = rules.flatFlip180 ? current == upright : IphoneDuoFlatOrientation.Allows(project, current);
            if (!settled)
            {
                uprightSince = -1f;
                // One request per hold, repeated when iOS has not reached it (e.g. it arrived mid-rotation).
                if (requested == upright && now - requestedAt < rules.requestRetrySeconds)
                    return default;
                requested = upright;
                requestedAt = now;
                return new Writes { request = upright };
            }

            synced = true;
            if (uprightSince < 0f)
                uprightSince = now;
            return now - uprightSince >= rules.lockSettleSeconds
                ? WriteLock(true)
                : default;
        }

        /// <summary>Releases the lock now (left Opened, or posture owner detached).</summary>
        public Writes Unlock()
        {
            synced = false;
            hold = DeviceOrientation.Unknown;
            uprightSince = -1f;
            requested = IphoneDuoInterfaceOrientation.Unknown;
            return locked ? WriteLock(false) : default;
        }

        Writes WriteLock(bool value)
        {
            locked = value;
            return new Writes { writeLock = true, locked = value };
        }

        #endregion
    }
}
