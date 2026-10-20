namespace FDPlus.Foldable.Editor
{
    /// <summary>
    /// Narrow seam around Unity Device Simulator's non-public selection model.
    /// The static bridge remains the package facade; tests can replace this adapter.
    /// </summary>
    internal interface IDeviceSimulatorSelectionAdapter
    {
        bool GetActiveSelection(out SimulatorDeviceBridge.Selection selection);
    }

    internal sealed class UnityDeviceSimulatorSelectionAdapter :
        IDeviceSimulatorSelectionAdapter
    {
        public bool GetActiveSelection(
            out SimulatorDeviceBridge.Selection selection) =>
            SimulatorDeviceBridge.ReadActiveSelection(out selection);
    }
}
