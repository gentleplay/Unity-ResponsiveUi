using System.IO;
using UnityEngine;
using FDPlus.ResponsiveUI.Shared.Editor;

namespace FDPlus.ResponsiveUI.Editor
{
/// <summary>
/// Locates the ResponsiveUI folder under Assets/ or Packages/ (Windows / macOS / Linux Editors).
/// Project-relative mapping is <see cref="ResponsiveUiEditorPath.ToProjectRelativePath"/>.
/// </summary>
public static class ResponsiveUiPathUtil
{
    #region Constants

    public const string FolderName = "ResponsiveUI";

    #endregion

    #region Paths

    /// <summary>Resolve package root under Assets/ or Packages/.</summary>
    public static string GetPackageRoot()
    {
        string assets = Application.dataPath;
        string underAssets = Path.Combine(assets, FolderName);
        if (Directory.Exists(underAssets))
            return Path.GetFullPath(underAssets);

        string packages = Path.Combine(Directory.GetParent(assets)?.FullName ?? "", "Packages");
        if (Directory.Exists(packages))
        {
            foreach (string dir in Directory.GetDirectories(packages))
            {
                if (string.Equals(Path.GetFileName(dir), FolderName, System.StringComparison.OrdinalIgnoreCase))
                    return Path.GetFullPath(dir);
                string nested = Path.Combine(dir, FolderName);
                if (Directory.Exists(nested))
                    return Path.GetFullPath(nested);
            }
        }

        return null;
    }

    #endregion
}

}
