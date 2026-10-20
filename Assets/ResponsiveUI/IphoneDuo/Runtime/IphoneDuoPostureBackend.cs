#if UNITY_IOS && !UNITY_EDITOR
using FDPlus.Foldable;
using UnityEngine;

namespace FDPlus.IphoneDuo
{
    /// <summary>
    /// iPhone Duo posture for <see cref="FoldablePostureDetector"/>: UIKit hinge status plus the
    /// Unity view size (<see cref="IphoneDuoHinge"/>), the posture rules
    /// (<see cref="IphoneDuoHoldOrientation"/>, <see cref="IphoneDuoFlatOrientation"/>) and the
    /// game-facing <see cref="IphoneDuoDevice.State"/>. Registered before the first scene loads.
    /// </summary>
    sealed class IphoneDuoPostureBackend : IFoldablePostureBackend
    {
        static bool s_HingeWarned;
        bool hingeAttached;
        int lastVersion = -1;
        int lastScreenW;
        int lastScreenH;
        bool launchHoldAdopted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Register() => FoldablePostureBackend.Register(new IphoneDuoPostureBackend());

        public bool IsSupportedDevice => IphoneDuoHinge.IsSupportedDevice;

        public void Attach()
        {
            if (hingeAttached)
                return;

            hingeAttached = IphoneDuoHinge.Start();
            if (!hingeAttached && !s_HingeWarned)
            {
                // No resolvable UIHingeInteraction: keep size-only Closed / Opened from the view.
                s_HingeWarned = true;
                Debug.LogWarning(
                    "iPhone Duo: UIHingeInteraction is unavailable on this device; " +
                    "posture is size-only (Closed / Opened, no Seated).");
            }
        }

        public void Detach()
        {
            if (hingeAttached)
                IphoneDuoHinge.Stop();
            // Detached before the first posture frame: take the native launch hold over so it ends,
            // then put the project's flags back and the view straight below.
            if (!launchHoldAdopted)
            {
                launchHoldAdopted = true;
                IphoneDuoHoldOrientation.AdoptLaunchHold(IphoneDuoDevice.Rules);
            }
            IphoneDuoFlatOrientation.Restore();
            IphoneDuoHoldOrientation.Restore();
            IphoneDuoDevice.Publish(default);
            hingeAttached = false;
            lastVersion = -1;
        }

        public void SetPaused(bool paused)
        {
            // Resuming re-enables delivery, so UIKit reports a fold made while paused.
            if (hingeAttached)
                IphoneDuoHinge.SetEnabled(!paused);
        }

        public bool Poll(bool force, FoldableLayoutState layout)
        {
            int version = IphoneDuoHinge.Version;
            int w = Screen.width;
            int h = Screen.height;
            if (!force &&
                version == lastVersion &&
                w == lastScreenW &&
                h == lastScreenH)
                return false;

            lastVersion = version;
            lastScreenW = w;
            lastScreenH = h;
            layout.CopyExact(IphoneDuoHinge.ReadLayout());
            return true;
        }

        public void Tick(FoldableLayoutState committed)
        {
            IphoneDuoRules rules = IphoneDuoDevice.Rules;
            IphoneDuoPosture posture = IphoneDuoHinge.ToPosture(committed.productState);
            // The splash may already show the game sideways (native launch hold): continue from it.
            if (!launchHoldAdopted)
            {
                launchHoldAdopted = true;
                IphoneDuoHoldOrientation.AdoptLaunchHold(rules);
            }
            // Flat inner: Android-style lock (stays when held across the lock, flips 180°). It waits
            // while the hold rules own the flags or the view turn, and releases before they take them.
            IphoneDuoFlatOrientation.Tick(
                posture == IphoneDuoPosture.Flat && !IphoneDuoHoldOrientation.IsActive, rules);
            // Seated allows only the hold's axis; held across the Player lock each posture follows its
            // rule (flat from Seated, a cold start or an unfold: the game turns sideways).
            bool flatLocked = IphoneDuoFlatOrientation.IsLocked;
            IphoneDuoHoldOrientation.Tick(posture, flatLocked, rules);
            // The hold, across-lock and mode the rules apply (one source with the flags and view turn).
            IphoneDuoDevice.Publish(IphoneDuoHoldOrientation.CurrentState(posture, flatLocked));
        }
    }
}
#endif
