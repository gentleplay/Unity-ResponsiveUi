using System.IO;
using System.Text;
using FDPlus.Foldable.Editor;
using FDPlus.PhonePad.Editor;
using UnityEditor;
using UnityEngine;
using FDPlus.ResponsiveUI.Shared.Editor;

namespace FDPlus.ResponsiveUI.Editor
{
/// <summary>
/// First-run setup + unified Project → Validate Setup for the ResponsiveUI kit.
/// </summary>
[InitializeOnLoad]
public static class ResponsiveUiPackageSetup
{
    #region Constants

    const string PrefKeyPrefix = "com.futuredigitalplus.responsiveui.setup.v2";

    /// <summary>Stable package id used by validation and tooling.</summary>
    public const string PackageId = "com.futuredigitalplus.responsiveui";
    /// <summary>Human-readable kit name.</summary>
    public const string DisplayName = "Responsive UI";
    /// <summary>Ship version (also parsed from root README when present).</summary>
    public const string Version = "1.0.0";
    const int MinimumUnityMajor = 2023;
    const int MinimumUnityMinor = 1;

    #endregion

    #region First run

    /// <summary>
    /// EditorPrefs are machine-wide; key the first-run answer per project so each project
    /// that imports the kit is asked once.
    /// </summary>
    static string PrefKey => PrefKeyPrefix + "." + PlayerSettings.productGUID.ToString("N");

    static ResponsiveUiPackageSetup()
    {
        EditorApplication.delayCall += OfferFirstRunSetup;
    }

    static void OfferFirstRunSetup()
    {
        if (Application.isBatchMode || EditorPrefs.GetBool(PrefKey, false))
            return;

        int choice = EditorUtility.DisplayDialogComplex(
            "Responsive UI — First-time setup",
            "Apply recommended Android Fold & Flip Player Settings and enable only the Phone/Pad Design Canvas reference skins?\n\n" +
            "You can change these later under Responsive UI:\n" +
            "• Project → Apply Android Fold & Flip Recommended Settings\n" +
            "• Devices → Enable Phone & Pad Simulator Skins\n\n" +
            "Nothing is changed unless you choose Apply.",
            "Apply recommended",
            "Not Now",
            "Android settings only");

        // Record after the user answers so a dismissed / crashed dialog can show again.
        EditorPrefs.SetBool(PrefKey, true);

        if (choice == 1)
            return;

        if (choice == 0 || choice == 2)
            FoldablePackageSetup.ApplyRecommendedSettings(log: true, forceAutoRotation: false);

        if (choice == 0)
        {
            if (PhonePadPackageSetup.EnableDesignReferenceDevices(out string message))
                Debug.Log($"[Responsive UI] {message}");
            else
                Debug.LogWarning($"[Responsive UI] {message}");
        }
    }

    #endregion

    #region Validate

    [MenuItem(ResponsiveUiEditorMenus.ValidateSetup, false, ResponsiveUiEditorMenus.PriorityProject + 1)]
    public static void ValidateSetup()
    {
        var report = new StringBuilder();
        bool ok = true;

        ok &= RequireSupportedUnityVersion(report);

        string root = ResponsiveUiPathUtil.GetPackageRoot();
        if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
        {
            report.AppendLine($"FAIL  {DisplayName} ({PackageId}) package root not found under Assets/.");
            ok = false;
        }
        else
        {
            report.AppendLine($"PASS  Package root: {root}");
            report.AppendLine($"INFO  Version {ResolveVersion(root)}");
            ok &= RequireFile(report, root, "LICENSE.md");
            ok &= RequireFile(report, root, "README.md");
            ok &= RequireFile(report, root, "Runtime/com.futuredigitalplus.responsiveui.asmdef");
            ok &= RequireFile(report, root, "Editor/com.futuredigitalplus.responsiveui.editor.asmdef");
            ok &= RequireFile(report, root, "Runtime/ResponsiveUI.cs");
            ok &= RequireFile(report, root, "Runtime/ResponsiveDevice.cs");
            ok &= RequireFile(report, root, "Editor/Package/ResponsiveUiPackageExport.cs");
            ok &= RequireDir(report, root, "FoldAndFlip");
            ok &= RequireDir(report, root, "IphoneDuo");
            ok &= RequireDir(report, root, "PhonePad");
            ok &= RequireFile(report, root, "FoldAndFlip/Runtime/com.futuredigitalplus.foldandflip.asmdef");
            ok &= RequireFile(report, root, "IphoneDuo/Runtime/com.futuredigitalplus.iphoneduo.asmdef");
            ok &= RequireFile(report, root, "PhonePad/Editor/com.futuredigitalplus.phonepad.simulator.editor.asmdef");
        }

        report.AppendLine();
        report.AppendLine("--- FoldAndFlip ---");
        var foldReport = FoldablePackageSetup.BuildValidationReport();
        report.Append(foldReport.message);
        if (!foldReport.ok)
            ok = false;

        report.AppendLine();
        report.AppendLine("--- PhonePad ---");
        string phoneRoot = PhonePadDeviceCatalog.GetPackageRoot();
        if (!string.IsNullOrEmpty(phoneRoot) && Directory.Exists(phoneRoot))
        {
            report.AppendLine($"PASS  PhonePad module: {phoneRoot}");
            PhonePadDeviceCatalog.Reload();
            var devices = PhonePadDeviceCatalog.Devices;
            int profiles = devices != null ? devices.Length : 0;
            int skins = 0;
            if (devices != null)
            {
                for (int i = 0; i < devices.Length; i++)
                {
                    if (devices[i].SimulatorEnabled)
                        skins++;
                }
            }

            report.AppendLine($"INFO  {profiles} phone/pad profile(s)");
            if (skins == 0)
            {
                report.AppendLine(
                    "WARN  No PhonePad Device Simulator skins — Responsive UI → Devices → Enable Phone & Pad Simulator Skins");
            }
            else
            {
                report.AppendLine($"PASS  {skins} PhonePad Device Simulator skin(s) enabled");
            }
        }
        else
        {
            ok = false;
            report.AppendLine("FAIL  PhonePad module not resolved");
        }

        report.AppendLine();
        report.AppendLine($"INFO  {DisplayName} ({PackageId}) — owner: futuredigitalplus");
        report.AppendLine("INFO  Design canvas + Background Cover Size: Edit → Project Settings → Responsive UI");

        string title = ok ? "Responsive UI — Validate Setup (OK)" : "Responsive UI — Validate Setup (Issues)";
        Debug.Log($"[Responsive UI] Validate Setup\n{report}");
        EditorUtility.DisplayDialog(title, report.ToString(), "OK");
    }

    static string ResolveVersion(string root)
    {
        string fromReadme = ResponsiveUiPackageExport.ResolvePackageVersion(root);
        return string.IsNullOrEmpty(fromReadme) ? Version : fromReadme;
    }

    static bool RequireSupportedUnityVersion(StringBuilder report)
    {
        ParseUnityVersion(Application.unityVersion, out int major, out int minor);
        bool supported = major > MinimumUnityMajor ||
                         (major == MinimumUnityMajor && minor >= MinimumUnityMinor);
        if (supported)
        {
            report.AppendLine(
                $"PASS  Unity {Application.unityVersion} (minimum {MinimumUnityMajor}.{MinimumUnityMinor})");
            return true;
        }

        report.AppendLine(
            $"FAIL  Unity {Application.unityVersion} is below required {MinimumUnityMajor}.{MinimumUnityMinor}+");
        return false;
    }

    static void ParseUnityVersion(string version, out int major, out int minor)
    {
        string[] parts = version.Split('.');
        int.TryParse(parts[0], out major);
        minor = 0;
        if (parts.Length > 1)
            int.TryParse(parts[1], out minor);
    }

    static bool RequireFile(StringBuilder report, string root, string relative)
    {
        string path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(path))
        {
            report.AppendLine($"PASS  {relative}");
            return true;
        }

        report.AppendLine($"FAIL  missing {relative}");
        return false;
    }

    static bool RequireDir(StringBuilder report, string root, string relative)
    {
        string path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        if (Directory.Exists(path))
        {
            report.AppendLine($"PASS  {relative}/");
            return true;
        }

        report.AppendLine($"FAIL  missing {relative}/");
        return false;
    }

    #endregion
}

}
