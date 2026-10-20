using System;
using UnityEngine;

namespace FDPlus.IphoneDuo
{
    /// <summary>
    /// iPhone Duo entry point for game code: the current <see cref="State"/> (posture, hold, screen
    /// mode), <see cref="StateChanged"/>, and the <see cref="Rules"/> in use. The rules start as the
    /// project's (Project Settings › Responsive UI › iPhone Duo); change their fields or assign new
    /// rules at runtime, or assign null to reload the project's. The iOS posture owner publishes the
    /// state on device; in the Editor the iPhone Duo Device Simulator panel publishes a preview.
    /// Designers can use the <see cref="IphoneDuoEvents"/> component instead of code.
    /// </summary>
    public static class IphoneDuoDevice
    {
        static IphoneDuoRules s_Rules;

        /// <summary>
        /// Rules in use; assign to change them at runtime, null to reload the project's. Turning
        /// <see cref="IphoneDuoRules.debugOverlay"/> on shows the overlay at the next assignment or state change.
        /// </summary>
        public static IphoneDuoRules Rules
        {
            get => s_Rules ??= IphoneDuoSettings.Load();
            set
            {
                s_Rules = value;
                IphoneDuoDebugOverlay.Ensure();
            }
        }

        /// <summary>Current posture, hold and screen mode (<see cref="IphoneDuoPosture.Unknown"/> off a Duo).</summary>
        public static IphoneDuoState State { get; private set; }

        /// <summary>Raised on the main thread whenever <see cref="State"/> changes.</summary>
        public static event Action<IphoneDuoState> StateChanged;

        /// <summary>True on iPhone Duo hardware and the Xcode iPhone Duo simulator (false in the Editor).</summary>
        public static bool IsSupported
        {
            get
            {
#if UNITY_IOS && !UNITY_EDITOR
                return IphoneDuoHinge.IsSupportedDevice;
#else
                return false;
#endif
            }
        }

        /// <summary>Sets <see cref="State"/> and raises <see cref="StateChanged"/> when it differs.</summary>
        internal static void Publish(IphoneDuoState state)
        {
            if (state.Equals(State))
                return;
            State = state;
            IphoneDuoDebugOverlay.Ensure();
            StateChanged?.Invoke(state);
        }

        // Enter Play Mode without a domain reload keeps statics: start each session clean.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetSession()
        {
            s_Rules = null;
            State = default;
            StateChanged = null;
        }
    }
}
