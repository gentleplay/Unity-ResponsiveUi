using FDPlus.ResponsiveUI.Shared.Editor;
using UnityEngine;

namespace FDPlus.PhonePad.Editor
{
    /// <summary>
    /// Device Simulator only marks its device dropdown dirty on <c>.device</c> import.
    /// Deletes (Disable All) do not trigger that, so open Simulator windows keep stale entries
    /// until we call <c>SimulatorWindow.MarkAllDeviceListsDirty</c> / <c>UpdateDeviceList</c>.
    /// </summary>
    public static class PhonePadSimulatorListUtil
    {
        public static void ForceReloadDeviceLists()
        {
            if (!ResponsiveUiSimulatorList.Refresh(out string error))
            {
                Debug.LogWarning(
                    "[PhonePad] Could not refresh Device Simulator device list: " + error +
                    " Close and reopen the Simulator window.");
            }
        }
    }
}
