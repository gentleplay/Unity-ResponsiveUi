using System;
using System.IO;
using UnityEngine;

namespace FDPlus.ResponsiveUI.Shared.Editor
{
    /// <summary>Cross-platform, containment-safe paths shared by ResponsiveUI editor modules.</summary>
    public static class ResponsiveUiEditorPath
    {
        #region Combine

        public static string ToAssetSeparators(string path) =>
            string.IsNullOrEmpty(path) ? path : path.Replace('\\', '/');

        public static string CombineRoot(string root, string relative)
        {
            if (string.IsNullOrEmpty(relative))
                return root ?? "";

            string absolute = root ?? "";
            string[] parts = relative.Replace('\\', '/').TrimStart('/').Split('/');
            for (int i = 0; i < parts.Length; i++)
            {
                if (string.IsNullOrEmpty(parts[i]) || parts[i] == ".")
                    continue;
                absolute = Path.Combine(absolute, parts[i]);
            }
            return Path.GetFullPath(absolute);
        }

        public static bool CombineContained(string root, string relative, out string absolute)
        {
            absolute = null;
            if (string.IsNullOrEmpty(root) ||
                string.IsNullOrEmpty(relative) ||
                Path.IsPathRooted(relative))
                return false;

            string rootFull = Path.GetFullPath(root);
            string candidate = CombineRoot(rootFull, relative);
            if (!IsContained(rootFull, candidate))
                return false;

            absolute = candidate;
            return true;
        }

        public static bool IsContained(string root, string path)
        {
            if (string.IsNullOrEmpty(root) || string.IsNullOrEmpty(path))
                return false;

            string rootFull = Path.GetFullPath(root);
            string candidate = Path.GetFullPath(path);
            if (PathsEqual(rootFull, candidate))
                return true;

            string prefix = rootFull.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            return candidate.StartsWith(prefix, PathComparison);
        }

        #endregion

        #region Project relative

        public static string ToAssetPath(string absolutePath)
        {
            string relative = ToProjectRelativePath(absolutePath);
            if (string.IsNullOrEmpty(relative))
                return null;
            return string.Equals(relative, "Assets", PathComparison) ||
                   relative.StartsWith("Assets/", PathComparison)
                ? relative
                : null;
        }

        public static string ToProjectRelativePath(string absolutePath)
        {
            if (string.IsNullOrEmpty(absolutePath))
                return null;

            string full = ToAssetSeparators(Path.GetFullPath(absolutePath));
            string data = ToAssetSeparators(Path.GetFullPath(Application.dataPath));
            string projectRoot = ToAssetSeparators(
                Path.GetFullPath(Path.GetDirectoryName(Application.dataPath) ?? ""));
            if (string.Equals(full, data, PathComparison))
                return "Assets";

            string prefix = projectRoot.TrimEnd('/') + "/";
            if (!full.StartsWith(prefix, PathComparison))
                return null;

            string relative = full.Substring(prefix.Length);
            return string.Equals(relative, "Assets", PathComparison) ||
                   string.Equals(relative, "Packages", PathComparison) ||
                   relative.StartsWith("Assets/", PathComparison) ||
                   relative.StartsWith("Packages/", PathComparison)
                ? relative
                : null;
        }

        public static bool PathsEqual(string first, string second)
        {
            if (string.IsNullOrEmpty(first) || string.IsNullOrEmpty(second))
                return string.IsNullOrEmpty(first) && string.IsNullOrEmpty(second);
            return string.Equals(
                Path.GetFullPath(first),
                Path.GetFullPath(second),
                PathComparison);
        }

        #endregion

        #region Platform

        public static string SaveShortcutHint
        {
            get
            {
#if UNITY_EDITOR_OSX
                return "Cmd+S";
#else
                return "Ctrl+S";
#endif
            }
        }

        static StringComparison PathComparison
        {
            get
            {
#if UNITY_EDITOR_WIN
                return StringComparison.OrdinalIgnoreCase;
#else
                return StringComparison.Ordinal;
#endif
            }
        }

        #endregion
    }
}
