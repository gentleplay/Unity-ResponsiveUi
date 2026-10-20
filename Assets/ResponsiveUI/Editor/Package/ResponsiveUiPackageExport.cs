using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using FDPlus.ResponsiveUI.Shared.Editor;

namespace FDPlus.ResponsiveUI.Editor
{
/// <summary>
/// Export the unified ResponsiveUI kit (layout + FoldAndFlip + IphoneDuo + PhonePad) as one
/// <c>.unitypackage</c>. Excludes Tests/ and generated PhonePad <c>.device</c> skins.
/// </summary>
public static class ResponsiveUiPackageExport
{
    #region Constants

    const string PrefLastDir = "com.futuredigitalplus.responsiveui.export.lastDir";

    #endregion

    #region Export

    [MenuItem(ResponsiveUiEditorMenus.ExportUnityPackage, false, ResponsiveUiEditorMenus.PriorityProject + 2)]
    public static void ExportUnityPackage()
    {
        if (!ResolvePackageFolder(out string assetPath, out string rootAbs))
        {
            EditorUtility.DisplayDialog(
                "Responsive UI — Export Failed",
                "Could not resolve the package folder under Assets or Packages.\n\n" +
                $"Expected a valid {ResponsiveUiPathUtil.FolderName} folder in this project.",
                "OK");
            return;
        }

        string version = ResolvePackageVersion(rootAbs);
        string defaultName = $"{ResponsiveUiPathUtil.FolderName}-{version}.unitypackage";
        string lastDir = ResolveDefaultSaveDirectory();

        string savePath = EditorUtility.SaveFilePanel(
            "Export Responsive UI Unity Package",
            lastDir,
            defaultName,
            "unitypackage");
        if (string.IsNullOrEmpty(savePath))
            return;

        string saveDir = Path.GetDirectoryName(savePath);
        if (!string.IsNullOrEmpty(saveDir))
            EditorPrefs.SetString(PrefLastDir, saveDir);

        if (!ExportTo(savePath, out string error))
        {
            EditorUtility.DisplayDialog("Responsive UI — Export Failed", error, "OK");
            return;
        }

        Debug.Log($"[Responsive UI] Exported Unity package:\n{savePath}");
        EditorUtility.RevealInFinder(savePath);
        EditorUtility.DisplayDialog(
            "Responsive UI — Export Complete",
            $"Exported:\n{savePath}\n\n" +
            "Import into another project via Assets > Import Package > Custom Package…\n\n" +
            "Includes layout Runtime/Editor, FoldAndFlip (fold/flip + Android bridge), IphoneDuo (hinge bridge + Simulator panel), " +
            "and PhonePad (phone/pad profiles).",
            "OK");
    }

    /// <summary>Non-interactive export for tests / batchmode (Tests/ excluded).</summary>
    public static bool ExportTo(string savePath, out string error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(savePath))
        {
            error = "Save path is empty.";
            return false;
        }

        if (!ResolvePackageFolder(out string assetPath, out _))
        {
            error =
                $"Could not resolve {ResponsiveUiPathUtil.FolderName} under Assets or Packages.";
            return false;
        }

        string[] exportAssets = CollectProductionAssetPaths(assetPath);
        if (exportAssets.Length == 0)
        {
            error = "No exportable assets found (Tests are excluded from production packages).";
            return false;
        }

        savePath = Path.GetFullPath(savePath);
        string saveDir = Path.GetDirectoryName(savePath);
        if (string.IsNullOrEmpty(saveDir))
        {
            error = "Save directory is invalid.";
            return false;
        }

        try
        {
            Directory.CreateDirectory(saveDir);
            if (File.Exists(savePath))
                File.Delete(savePath);

            EditorUtility.DisplayProgressBar(
                "Responsive UI",
                $"Exporting {exportAssets.Length} assets…",
                0.35f);
            // Explicit path list — do not Recurse (would pull Tests / generated .device).
            AssetDatabase.ExportPackage(exportAssets, savePath, ExportPackageOptions.Default);
            EditorUtility.DisplayProgressBar("Responsive UI", "Finishing export…", 1f);
        }
        catch (Exception ex)
        {
            error = ex.Message;
            Debug.LogError("[Responsive UI] Export failed: " + ex.Message);
            return false;
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        if (!File.Exists(savePath))
        {
            error = "Unity did not write the .unitypackage file.";
            return false;
        }

        return true;
    }

    /// <summary>Batchmode entry: exports under Temp/, then exits.</summary>
    public static void ExportUnityPackageBatch()
    {
        try
        {
            string version = ResolvePackageVersion(ResponsiveUiPathUtil.GetPackageRoot());
            string outDir = Path.Combine(
                Directory.GetParent(Application.dataPath)?.FullName ?? ".",
                "Temp");
            Directory.CreateDirectory(outDir);
            string savePath = Path.Combine(
                outDir,
                $"{ResponsiveUiPathUtil.FolderName}-{version}.unitypackage");

            if (!ExportTo(savePath, out string error))
                throw new InvalidOperationException("Responsive UI export failed: " + error);

            Debug.Log("[Responsive UI] Batch export OK: " + savePath);
            if (Application.isBatchMode)
                EditorApplication.Exit(0);
        }
        catch (Exception ex)
        {
            Debug.LogError("[Responsive UI] Batch export failed: " + ex.Message);
            if (Application.isBatchMode)
                EditorApplication.Exit(1);
            throw;
        }
    }

    #endregion

    #region Paths

    public static string ResolveDefaultSaveDirectory()
    {
        string lastDir = EditorPrefs.GetString(PrefLastDir, "");
        if (!string.IsNullOrEmpty(lastDir) && Directory.Exists(lastDir))
            return lastDir;

        string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        if (!string.IsNullOrEmpty(desktop) && Directory.Exists(desktop))
            return desktop;

        string personal = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
        if (!string.IsNullOrEmpty(personal) && Directory.Exists(personal))
            return personal;

        string home = Environment.GetEnvironmentVariable("HOME");
        if (string.IsNullOrEmpty(home))
            home = Environment.GetEnvironmentVariable("USERPROFILE");
        if (!string.IsNullOrEmpty(home) && Directory.Exists(home))
            return home;

        return Directory.GetParent(Application.dataPath)?.FullName ?? ".";
    }

    public static bool ResolvePackageFolder(out string assetPath, out string rootAbs)
    {
        assetPath = null;
        rootAbs = ResponsiveUiPathUtil.GetPackageRoot();
        if (string.IsNullOrEmpty(rootAbs) || !Directory.Exists(rootAbs))
            return false;

        // Assets/… or Packages/… — GetPackageRoot searches both.
        assetPath = ResponsiveUiEditorPath.ToProjectRelativePath(rootAbs);
        return !string.IsNullOrEmpty(assetPath) && AssetDatabase.IsValidFolder(assetPath);
    }

    /// <summary>
    /// Unified production payload: README + Runtime/Editor/SharedEditor/Docs/Samples + modules.
    /// Excludes any Tests/ tree and generated PhonePad <c>.device</c> / <c>.device.off</c> skins.
    /// FoldAndFlip shipped <c>.device</c> skins are included.
    /// </summary>
    public static string[] CollectProductionAssetPaths(string packageAssetFolder)
    {
        var paths = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string root = packageAssetFolder.Replace('\\', '/').TrimEnd('/');

        foreach (string file in new[] { "README.md", "LICENSE.md", "LICENSE", "package.json" })
            AddIfNew(paths, seen, root + "/" + file);

        foreach (string folder in new[]
                 {
                     "Runtime", "Editor", "SharedEditor", "Docs", "Samples", "FoldAndFlip", "IphoneDuo", "PhonePad"
                 })
            AddFolderAssets(paths, seen, root + "/" + folder);

        return paths.ToArray();
    }

    static void AddFolderAssets(List<string> paths, HashSet<string> seen, string folder)
    {
        if (!AssetDatabase.IsValidFolder(folder))
            return;

        string[] guids = AssetDatabase.FindAssets(string.Empty, new[] { folder });
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]).Replace('\\', '/');
            if (!seen.Add(path))
                continue;
            if (IsExcludedExportPath(path))
                continue;
            paths.Add(path);
        }
    }

    public static bool IsExcludedExportPath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return true;

        string p = path.Replace('\\', '/');
        if (p.IndexOf("/Tests/", StringComparison.OrdinalIgnoreCase) >= 0 ||
            p.EndsWith("/Tests", StringComparison.OrdinalIgnoreCase))
            return true;

        // PhonePad generates skins on Enable — do not ship them. FoldAndFlip ships authored skins.
        // Also exclude local photoreal imports (PNG + borders sidecars) under DeviceOverlays.
        if (p.IndexOf("/PhonePad/", StringComparison.OrdinalIgnoreCase) >= 0 &&
            (p.EndsWith(".device", StringComparison.OrdinalIgnoreCase) ||
             p.EndsWith(".device.off", StringComparison.OrdinalIgnoreCase) ||
             p.EndsWith(".borders.json", StringComparison.OrdinalIgnoreCase) ||
             p.IndexOf("/PhonePad/Editor/Devices/DeviceOverlays/", StringComparison.OrdinalIgnoreCase) >= 0 ||
             p.EndsWith("/PhonePad/Editor/Devices/DeviceOverlays", StringComparison.OrdinalIgnoreCase)))
            return true;

        return false;
    }

    static void AddIfNew(List<string> paths, HashSet<string> seen, string path)
    {
        if (string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path)))
            return;
        if (!seen.Add(path))
            return;
        paths.Add(path);
    }

    public static string ResolvePackageVersion(string packageRoot)
    {
        if (string.IsNullOrEmpty(packageRoot))
            return ResponsiveUiPackageSetup.Version;

        string readme = Path.Combine(packageRoot, "README.md");
        if (!File.Exists(readme))
            return ResponsiveUiPackageSetup.Version;

        try
        {
            string text = File.ReadAllText(readme);
            var match = Regex.Match(
                text,
                @"\*\*Version:\*\*\s*([0-9]+(?:\.[0-9]+){1,3})",
                RegexOptions.CultureInvariant);
            if (match.Success)
                return match.Groups[1].Value;
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[Responsive UI] Could not read package version from README: " + ex.Message);
        }

        return ResponsiveUiPackageSetup.Version;
    }

    #endregion
}

}
