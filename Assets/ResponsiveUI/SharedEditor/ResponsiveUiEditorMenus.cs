namespace FDPlus.ResponsiveUI.Shared.Editor
{
    /// <summary>
    /// Editor menu paths and priorities for the unified Responsive UI kit. Lives in the shared
    /// editor assembly so the layout editor and the FoldAndFlip, PhonePad, and iPhone Duo editor
    /// modules register their <c>[MenuItem]</c>s from one list.
    /// </summary>
    public static class ResponsiveUiEditorMenus
    {
        #region Roots

        const string Root = "Responsive UI";
        const string WindowRoot = "Window/Responsive UI";

        #endregion

        #region Scene

        const string Scene = Root + "/Scene";
        public const string CreateFoldableSetup = Scene + "/Create Fold & Flip Setup";
        public const string CreateFoldableCompleteScene = Scene + "/Create Fold & Flip Complete Scene";
        public const string CreateSampleCanvas = Scene + "/Create Sample Canvas";
        public const int PriorityScene = 0;

        #endregion

        #region Devices

        const string Devices = Root + "/Devices";
        public const string PhonePadCatalog = Devices + "/Phone & Pad Catalog";
        public const string FoldFlipCatalog = Devices + "/Fold & Flip Catalog";
        public const string PostureSimulator = Devices + "/Posture Simulator";
        public const string EnablePhonePadSimulator = Devices + "/Enable Phone & Pad Simulator Skins";
        public const string RepairPhonePadSimulator = Devices + "/Repair Phone & Pad Simulator Skins";
        public const string ImportPhotorealFrames = Devices + "/Import Photoreal Frames (Local)";
        public const string ClearPhotorealFrames = Devices + "/Clear Local Photoreal Frames";
        public const string WindowPhonePadCatalog = WindowRoot + "/Phone & Pad Catalog";
        public const string WindowFoldFlipCatalog = WindowRoot + "/Fold & Flip Catalog";
        public const string WindowPostureSimulator = WindowRoot + "/Posture Simulator";
        public const int PriorityDevices = 20;
        public const int PriorityWindow = 2000;

        #endregion

        #region Project

        const string Project = Root + "/Project";
        public const string ApplyFoldableRecommended = Project + "/Apply Android Fold & Flip Recommended Settings";
        public const string ValidateSetup = Project + "/Validate Setup";
        public const string ExportUnityPackage = Project + "/Export Unity Package";
        public const string IphoneDuoSettings = Project + "/iPhone Duo Settings";
        public const int PriorityProject = 40;
        /// <summary>Unity draws a divider when neighbouring priorities differ by more than 10.</summary>
        public const int PriorityGroupGap = 11;

        #endregion

        #region Debug

        const string Debug = Root + "/Debug";
        public const string DiagnoseSimulatorBridge = Debug + "/Diagnose Simulator Bridge";
        public const int PriorityDebug = 60;

        #endregion
    }
}
