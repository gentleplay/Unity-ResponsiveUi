using FDPlus.ResponsiveUI.Editor;
using NUnit.Framework;
using FDPlus.ResponsiveUI.Shared.Editor;

namespace FDPlus.ResponsiveUI.Tests
{
    /// <summary>
    /// Production export include/exclude contract.
    /// </summary>
    public class ResponsiveUiPackageExportTests
    {
        [Test]
        public void IsExcludedExportPath_RejectsTestsAndGeneratedPhonePadSkins()
        {
            Assert.IsTrue(
                ResponsiveUiPackageExport.IsExcludedExportPath(
                    "Assets/ResponsiveUI/Tests/Editor/ResponsiveUiHubTests.cs"));
            Assert.IsTrue(
                ResponsiveUiPackageExport.IsExcludedExportPath(
                    "Assets/ResponsiveUI/FoldAndFlip/Tests/validate-package.ps1"));
            Assert.IsTrue(
                ResponsiveUiPackageExport.IsExcludedExportPath(
                    "Assets/ResponsiveUI/PhonePad/Editor/Devices/iPhone 17 Pro Max.device"));
            Assert.IsTrue(
                ResponsiveUiPackageExport.IsExcludedExportPath(
                    "Assets/ResponsiveUI/PhonePad/Editor/Devices/iPhone 17 Pro Max.device.off"));
            Assert.IsTrue(
                ResponsiveUiPackageExport.IsExcludedExportPath(
                    "Assets/ResponsiveUI/PhonePad/Editor/Devices/DeviceOverlays/frame.png"));
            Assert.IsTrue(
                ResponsiveUiPackageExport.IsExcludedExportPath(
                    "Assets/ResponsiveUI/PhonePad/Editor/Devices/DeviceOverlays/frame.borders.json"));
        }

        [Test]
        public void IsExcludedExportPath_KeepsRuntimeEditorAndFoldSkins()
        {
            Assert.IsFalse(
                ResponsiveUiPackageExport.IsExcludedExportPath(
                    "Assets/ResponsiveUI/Runtime/ResponsiveUI.cs"));
            Assert.IsFalse(
                ResponsiveUiPackageExport.IsExcludedExportPath(
                    "Assets/ResponsiveUI/Editor/Simulator/ResponsiveUiSimulatorSlotFocus.cs"));
            Assert.IsFalse(
                ResponsiveUiPackageExport.IsExcludedExportPath(
                    "Assets/ResponsiveUI/package.json"));
            Assert.IsFalse(
                ResponsiveUiPackageExport.IsExcludedExportPath(
                    "Assets/ResponsiveUI/SharedEditor/ResponsiveUiInspectorRepaint.cs"));
            Assert.IsFalse(
                ResponsiveUiPackageExport.IsExcludedExportPath(
                    "Assets/ResponsiveUI/FoldAndFlip/Editor/Devices/Galaxy Z Fold8-like.device"));
        }

        [Test]
        public void CollectProductionAssetPaths_OmitsTestsAndPhonePadSkins()
        {
            string[] paths = ResponsiveUiPackageExport.CollectProductionAssetPaths(
                "Assets/ResponsiveUI");
            Assert.Greater(paths.Length, 20);

            bool sawPackageJson = false;
            bool sawSharedEditor = false;
            for (int i = 0; i < paths.Length; i++)
            {
                string path = paths[i].Replace('\\', '/');
                Assert.IsFalse(
                    path.IndexOf("/Tests/", System.StringComparison.OrdinalIgnoreCase) >= 0,
                    path);
                if (path.IndexOf("/PhonePad/", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Assert.IsFalse(path.EndsWith(".device", System.StringComparison.OrdinalIgnoreCase), path);
                    Assert.IsFalse(path.EndsWith(".device.off", System.StringComparison.OrdinalIgnoreCase), path);
                    Assert.IsFalse(
                        path.IndexOf("/DeviceOverlays/", System.StringComparison.OrdinalIgnoreCase) >= 0,
                        path);
                }

                if (path.EndsWith("/package.json", System.StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith("package.json", System.StringComparison.OrdinalIgnoreCase))
                    sawPackageJson = true;
                if (path.IndexOf("/SharedEditor/", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    sawSharedEditor = true;
            }

            Assert.IsTrue(sawPackageJson);
            Assert.IsTrue(sawSharedEditor);
        }
    }
}
