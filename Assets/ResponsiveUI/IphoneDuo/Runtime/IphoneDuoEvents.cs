using System;
using UnityEngine;
using UnityEngine.Events;

namespace FDPlus.IphoneDuo
{
    /// <summary>
    /// UnityEvents for iPhone Duo posture, hold and screen mode changes (<see cref="IphoneDuoDevice.StateChanged"/>),
    /// for wiring HUD or gameplay reactions without code. Events fire on change; with
    /// <see cref="invokeOnEnable"/> the current posture, hold and mode also fire when enabled.
    /// </summary>
    [AddComponentMenu("Responsive UI/iPhone Duo Events")]
    public sealed class IphoneDuoEvents : MonoBehaviour
    {
        /// <summary>Posture, hold and screen mode.</summary>
        [Serializable]
        public sealed class StateEvent : UnityEvent<IphoneDuoPosture, IphoneDuoHold, IphoneDuoScreenMode> { }

        [Tooltip("Also fire the current posture, hold and mode when this component is enabled.")]
        public bool invokeOnEnable = true;

        [Header("Posture")]
        public UnityEvent onCover = new UnityEvent();
        public UnityEvent onSeated = new UnityEvent();
        public UnityEvent onFlat = new UnityEvent();

        [Header("Hold")]
        public UnityEvent onWideHold = new UnityEvent();
        public UnityEvent onTallHold = new UnityEvent();

        [Header("Screen mode")]
        [Tooltip("The game started showing sideways (Android Fold style), as a Unity view turn or the flat lock.")]
        public UnityEvent onSidewaysStarted = new UnityEvent();
        public UnityEvent onSidewaysEnded = new UnityEvent();

        [Header("Any change")]
        public StateEvent onStateChanged = new StateEvent();

        IphoneDuoState last;
        bool hasLast;

        void OnEnable()
        {
            IphoneDuoDevice.StateChanged += Raise;
            hasLast = false;
            if (invokeOnEnable && IphoneDuoDevice.State.posture != IphoneDuoPosture.Unknown)
                Raise(IphoneDuoDevice.State);
            else
            {
                last = IphoneDuoDevice.State;
                hasLast = true;
            }
        }

        void OnDisable() => IphoneDuoDevice.StateChanged -= Raise;

        /// <summary>True when the game shows sideways: turned view, or locked flat across the Player lock.</summary>
        public static bool IsSideways(IphoneDuoState state) =>
            state.mode == IphoneDuoScreenMode.Sideways ||
            (state.mode == IphoneDuoScreenMode.Locked && state.acrossLock);

        /// <summary>Fires the events for a change to <paramref name="state"/> (internal for EditMode tests).</summary>
        internal void Raise(IphoneDuoState state)
        {
            if (!hasLast || state.posture != last.posture)
            {
                switch (state.posture)
                {
                    case IphoneDuoPosture.Cover: onCover.Invoke(); break;
                    case IphoneDuoPosture.Seated: onSeated.Invoke(); break;
                    case IphoneDuoPosture.Flat: onFlat.Invoke(); break;
                }
            }
            if (!hasLast || state.hold != last.hold)
            {
                if (state.hold == IphoneDuoHold.Wide)
                    onWideHold.Invoke();
                else if (state.hold == IphoneDuoHold.Tall)
                    onTallHold.Invoke();
            }
            bool sideways = IsSideways(state);
            if (hasLast ? sideways != IsSideways(last) : sideways)
            {
                if (sideways)
                    onSidewaysStarted.Invoke();
                else
                    onSidewaysEnded.Invoke();
            }
            onStateChanged.Invoke(state.posture, state.hold, state.mode);
            last = state;
            hasLast = true;
        }
    }
}
