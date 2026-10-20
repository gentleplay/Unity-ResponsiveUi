using System;
using FDPlus.ResponsiveUI.Shared.Editor;
using UnityEditor;
using UnityEngine;

namespace FDPlus.Foldable.Editor
{
    /// <summary>
    /// Shared Device Simulator bezel overlay loading for Catalog / Posture Simulator. Reads the
    /// overlay paths stored in the profile (<c>skins.overlays</c>), never guesses file names.
    /// </summary>
    static class FoldableDeviceOverlayUtil
    {
        #region Skins / overlays

        public static bool Load(
            FoldableDeviceCatalog.DeviceProfile device,
            string screenName,
            int screenW,
            int screenH,
            out Texture2D texture,
            out Vector2 border)
        {
            string path = null;
            if (device?.Overlays != null)
            {
                foreach (var overlay in device.Overlays)
                {
                    if (overlay != null && ScreenMatches(overlay.Screen, screenName))
                    {
                        path = overlay.Path;
                        break;
                    }
                }
            }

            return LoadPath(path, screenW, screenH, out texture, out border);
        }

        public static bool Load(
            FoldableDeviceCatalog.EditableProfile draft,
            string screenName,
            int screenW,
            int screenH,
            out Texture2D texture,
            out Vector2 border)
        {
            string path = null;
            if (draft != null)
            {
                foreach (var overlay in draft.Overlays)
                {
                    if (overlay != null && ScreenMatches(overlay.Screen, screenName))
                    {
                        path = overlay.Path;
                        break;
                    }
                }
            }

            return LoadPath(path, screenW, screenH, out texture, out border);
        }

        #endregion

        #region Helpers

        static bool ScreenMatches(string overlayScreen, string screenName) =>
            string.Equals(overlayScreen ?? "", screenName ?? "", StringComparison.OrdinalIgnoreCase);

        /// <summary>Loads a package-relative overlay path (as stored in the profile JSON).</summary>
        static bool LoadPath(
            string packageRelativePath,
            int screenW,
            int screenH,
            out Texture2D texture,
            out Vector2 border)
        {
            texture = null;
            border = Vector2.zero;
            if (string.IsNullOrEmpty(packageRelativePath))
                return false;

            if (!ResponsiveUiEditorPath.CombineContained(
                    FoldableDeviceCatalog.GetPackageRoot(), packageRelativePath, out string absolute))
                return false;

            string assetPath = ResponsiveUiEditorPath.ToProjectRelativePath(absolute);
            if (string.IsNullOrEmpty(assetPath))
                return false;

            texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (texture == null)
                return false;

            border = new Vector2(
                Mathf.Max(0, (texture.width - screenW) * 0.5f),
                Mathf.Max(0, (texture.height - screenH) * 0.5f));
            return true;
        }

        #endregion
    }
}
