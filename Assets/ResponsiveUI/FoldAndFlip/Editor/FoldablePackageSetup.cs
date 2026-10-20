using System.IO;
using System.Text;
using FDPlus.ResponsiveUI.Shared.Editor;
using UnityEditor;
using UnityEngine;

namespace FDPlus.Foldable.Editor
{
    /// <summary>
    /// FoldAndFlip module setup: recommended Player Settings + validation report.
    /// First-run UX is owned by <c>ResponsiveUiPackageSetup</c>.
    /// </summary>
    public static class FoldablePackageSetup
    {
        #region Constants

        const string JavaPackagePath = "com/futuredigitalplus/foldandflip";
        const int MinAndroidTarget = 34;
        const float MinAndroidAspect = 3f;
        const float ValidateAndroidAspect = 2.8f;

        /// <summary>Stable package id used by validation and tooling.</summary>
        public const string PackageId = "com.futuredigitalplus.foldandflip";
        /// <summary>Module display name inside the Responsive UI kit.</summary>
        public const string DisplayName = "Responsive UI — FoldAndFlip";
        /// <summary>Module folder name under Assets/ResponsiveUI/.</summary>
        public const string FolderName = "FoldAndFlip";
        /// <summary>Path under Assets/ for this module.</summary>
        public const string ModuleRelativePath = "ResponsiveUI/FoldAndFlip";
        /// <summary>Module version (aligned with the unified Responsive UI kit).</summary>
        public const string Version = "1.0.0";

        #endregion

        #region Editor

        [MenuItem(ResponsiveUiEditorMenus.ApplyFoldableRecommended, false, ResponsiveUiEditorMenus.PriorityProject)]
        public static void ApplyRecommendedSettingsMenu()
        {
            // Default Orientation is one Player setting for every platform: ask before widening it.
            bool forceAutoRotation = EditorUtility.DisplayDialog(
                "Apply Android Fold & Flip Recommended Settings",
                "Also switch Default Orientation to Auto Rotation with portrait + landscape?\n\n" +
                "Default Orientation is shared: this changes it for iOS, Android and desktop, not only " +
                "Android. Keep it to leave a one-axis lock as is (iPhone Duo Seated allows the hold's " +
                "axis at runtime).",
                "Switch to Auto Rotation",
                "Keep Orientation");
            ApplyRecommendedSettings(log: true, forceAutoRotation);
        }

        public static void ApplyRecommendedSettings(bool log, bool forceAutoRotation)
        {
            var changes = new StringBuilder();

            if ((int)PlayerSettings.Android.minSdkVersion < 25)
            {
                PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)25;
                changes.AppendLine("- Android minSdk set to API 25");
            }
            else
            {
                changes.AppendLine($"- Android minSdk already OK ({(int)PlayerSettings.Android.minSdkVersion})");
            }

            var targetSdk = PlayerSettings.Android.targetSdkVersion;
            if (targetSdk != AndroidSdkVersions.AndroidApiLevelAuto &&
                (int)targetSdk < MinAndroidTarget)
            {
                PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)MinAndroidTarget;
                changes.AppendLine($"- Android targetSdk set to API {MinAndroidTarget}");
            }
            else
            {
                changes.AppendLine(targetSdk == AndroidSdkVersions.AndroidApiLevelAuto
                    ? "- Android targetSdk uses highest installed API (must resolve to 34+)"
                    : $"- Android targetSdk already OK ({(int)targetSdk})");
            }

            // Tall Fold cover / Flip main exceed Android's 2.1 default aspect cap.
            if (PlayerSettings.Android.maxAspectRatio < MinAndroidAspect)
            {
                PlayerSettings.Android.maxAspectRatio = MinAndroidAspect;
                changes.AppendLine($"- Android maxAspectRatio set to {MinAndroidAspect}");
            }
            else
            {
                changes.AppendLine($"- Android maxAspectRatio already OK ({PlayerSettings.Android.maxAspectRatio})");
            }

            ApplyOrientationRecommendation(changes, forceAutoRotation);

            if (!FoldablePlayerOrientation.GetAndroidResizable())
            {
                FoldablePlayerOrientation.SetAndroidResizable(true);
                changes.AppendLine("- Android Resizable Activity / Window enabled (fold / multi-window)");
            }
            else
            {
                changes.AppendLine("- Android Resizable Activity / Window already enabled");
            }

            changes.AppendLine("- Default Unity launcher remains supported (no custom activity required)");
            changes.AppendLine("- Android bridge + Jetpack WindowManager deps are provided by Foldable.androidlib");

            if (log)
                Debug.Log($"[Android Fold & Flip] Applied recommended settings:\n{changes}");
        }

        #endregion

        #region Helpers

        static void ApplyOrientationRecommendation(StringBuilder changes, bool forceAutoRotation)
        {
            bool portrait =
                PlayerSettings.allowedAutorotateToPortrait ||
                PlayerSettings.allowedAutorotateToPortraitUpsideDown;
            bool landscape =
                PlayerSettings.allowedAutorotateToLandscapeLeft ||
                PlayerSettings.allowedAutorotateToLandscapeRight;

            if (forceAutoRotation)
            {
                FoldablePlayerOrientation.EnableAllAutorotations();
                changes.AppendLine("- Default Orientation: Auto Rotation (portrait + landscape)");
                return;
            }

            var current = FoldablePlayerOrientation.GetDefaultOrientation();
            if (current == UIOrientation.AutoRotation)
            {
                // A one-axis lock is shared with iOS / desktop: never widen it here.
                if (portrait && landscape)
                {
                    changes.AppendLine("- Orientation already OK (Auto Rotation, both axes)");
                }
                else if (portrait || landscape)
                {
                    changes.AppendLine(
                        "- Kept orientation lock (shared with iOS); iPhone Duo Seated allows the hold's axis at runtime");
                }
                else
                {
                    FoldablePlayerOrientation.EnableAllAutorotations();
                    changes.AppendLine("- Auto Rotation axes enabled (portrait + landscape)");
                }
                return;
            }

            changes.AppendLine(
                $"- Left Default Orientation as {current} (use Apply Recommended to switch to Auto Rotation)");
        }

        #endregion

        #region Editor

        public static (bool ok, string message) BuildValidationReport()
        {
            var sb = new StringBuilder();
            bool ok = true;

            if ((int)PlayerSettings.Android.minSdkVersion < 25)
            {
                ok = false;
                sb.AppendLine("FAIL: Android minSdk < 25");
            }
            else
            {
                sb.AppendLine($"OK: minSdk {(int)PlayerSettings.Android.minSdkVersion}");
            }

            var targetSdk = PlayerSettings.Android.targetSdkVersion;
            if (targetSdk != AndroidSdkVersions.AndroidApiLevelAuto &&
                (int)targetSdk < MinAndroidTarget)
            {
                ok = false;
                sb.AppendLine($"FAIL: Android targetSdk {(int)targetSdk} < {MinAndroidTarget} (AndroidX Window compile requirement)");
            }
            else
            {
                sb.AppendLine(targetSdk == AndroidSdkVersions.AndroidApiLevelAuto
                    ? "OK: targetSdk uses highest installed API; ensure API 34+ is installed"
                    : $"OK: targetSdk {(int)targetSdk}");
            }

            if (PlayerSettings.Android.maxAspectRatio < ValidateAndroidAspect)
            {
                ok = false;
                sb.AppendLine(
                    $"FAIL: Android maxAspectRatio {PlayerSettings.Android.maxAspectRatio} < {ValidateAndroidAspect} (Fold cover needs ~2.8+)");
            }
            else
            {
                sb.AppendLine($"OK: maxAspectRatio {PlayerSettings.Android.maxAspectRatio}");
            }

            bool portrait =
                PlayerSettings.allowedAutorotateToPortrait ||
                PlayerSettings.allowedAutorotateToPortraitUpsideDown;
            bool landscape =
                PlayerSettings.allowedAutorotateToLandscapeLeft ||
                PlayerSettings.allowedAutorotateToLandscapeRight;
            if (!portrait && !landscape)
            {
                ok = false;
                sb.AppendLine(
                    "FAIL: Enable at least one autorotation axis (Responsive UI → Project → Apply Android Fold & Flip Recommended Settings)");
            }
            else if (!portrait || !landscape)
            {
                string enabled = portrait ? "portrait" : "landscape";
                sb.AppendLine(
                    $"INFO: Only {enabled} autorotation is enabled (shared with iOS). Fold/Flip hinge layouts " +
                    "keep that axis; iPhone Duo Seated allows the hold's axis at runtime.");
            }
            else
            {
                sb.AppendLine("OK: portrait + landscape autorotation");
            }

            if (!FoldablePlayerOrientation.GetAndroidResizable())
                sb.AppendLine("WARN: Android Resizable Activity/Window off (recommended for fold / multi-window)");
            else
                sb.AppendLine("OK: Android Resizable Activity/Window");

            string packageRoot = FindPackageRoot();
            if (string.IsNullOrEmpty(packageRoot))
            {
                ok = false;
                sb.AppendLine($"FAIL: {DisplayName} ({PackageId}) not found");
            }
            else
            {
                sb.AppendLine($"OK: Package root {packageRoot}");

                string androidLib = Path.Combine(packageRoot, "Runtime", "Plugins", "Android", "Foldable.androidlib");
                if (!Directory.Exists(androidLib))
                {
                    ok = false;
                    sb.AppendLine("FAIL: Foldable.androidlib missing");
                }
                else
                {
                    sb.AppendLine("OK: Foldable.androidlib present");
                }

                string javaRoot = Path.Combine(androidLib, "src", "main", "java");
                string helper = Path.Combine(javaRoot, JavaPackagePath.Replace('/', Path.DirectorySeparatorChar), "FoldableHelper.java");
                string tracker = Path.Combine(javaRoot, JavaPackagePath.Replace('/', Path.DirectorySeparatorChar), "FoldableWindowAreaTracker.java");
                if (!File.Exists(helper) || !File.Exists(tracker))
                {
                    ok = false;
                    sb.AppendLine("FAIL: Current Android bridge Java sources are missing inside Foldable.androidlib");
                }
                else
                {
                    sb.AppendLine("OK: Java FoldableHelper + FoldableWindowAreaTracker");
                }

                string manifest = Path.Combine(androidLib, "AndroidManifest.xml");
                if (!File.Exists(manifest))
                {
                    ok = false;
                    sb.AppendLine("FAIL: Foldable.androidlib/AndroidManifest.xml missing");
                }
                else
                {
                    string manifestText = File.ReadAllText(manifest);
                    bool declaresLauncher =
                        manifestText.IndexOf("android.intent.action.MAIN", System.StringComparison.Ordinal) >= 0 ||
                        manifestText.IndexOf("android.intent.category.LAUNCHER", System.StringComparison.Ordinal) >= 0;
                    if (declaresLauncher)
                    {
                        ok = false;
                        sb.AppendLine("FAIL: Android library still declares a custom launcher; the runtime bridge must merge into Unity's default activity");
                    }
                    else
                    {
                        sb.AppendLine("OK: Android manifest does not replace Unity's launcher activity");
                    }
                }
            }

            var detector = FoldablePostureDetector.FindActiveInstance();
            if (detector != null)
                sb.AppendLine("OK: active FoldablePostureDetector found in open scene(s)");
            else
                sb.AppendLine(
                    "WARN: No active FoldablePostureDetector in open scenes " +
                    "(Login detector, or Responsive UI → Scene → Create Fold & Flip Setup)");

            sb.AppendLine($"INFO: {DisplayName} ({PackageId})");
            sb.AppendLine("INFO: Runtime bridge uses Unity's default launcher activity");
            sb.AppendLine("INFO: Edit Mode tools: Responsive UI → Devices → Posture Simulator");
            sb.AppendLine("INFO: Device Simulator: pick Foldable *.device + Foldable control panel");
            sb.AppendLine("INFO: Play Mode overlay: FoldableSimulator on the setup object");

            if (!string.IsNullOrEmpty(packageRoot))
            {
                string devicesDir = Path.Combine(packageRoot, "Editor", "Devices");
                if (Directory.Exists(devicesDir))
                {
                    int deviceCount = Directory.GetFiles(devicesDir, "*.device").Length;
                    if (deviceCount == 0)
                    {
                        ok = false;
                        sb.AppendLine("FAIL: No .device files under Editor/Devices");
                    }
                    else
                    {
                        sb.AppendLine($"OK: {deviceCount} Device Simulator .device file(s)");
                    }
                }
                else
                {
                    ok = false;
                    sb.AppendLine("FAIL: Editor/Devices folder missing");
                }
            }

            return (ok, sb.ToString());
        }

        #endregion

        #region Helpers

        static string FindPackageRoot()
        {
            var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(FoldablePostureDetector).Assembly);
            if (packageInfo != null && !string.IsNullOrEmpty(packageInfo.resolvedPath))
                return packageInfo.resolvedPath;

            string root = FoldableDeviceCatalog.GetPackageRoot();
            return Directory.Exists(root) ? root : null;
        }

        #endregion
    }
}
