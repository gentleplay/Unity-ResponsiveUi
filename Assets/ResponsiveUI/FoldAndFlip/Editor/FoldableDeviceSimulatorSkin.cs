using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using FDPlus.ResponsiveUI.Shared.Editor;
using UnityEditor;

namespace FDPlus.Foldable.Editor
{
    /// <summary>
    /// Show / hide toolkit <c>.device</c> skins in Unity Device Simulator by renaming
    /// <c>.device</c> ↔ <c>.device.off</c>.
    /// Disabled files must NOT keep the Device ScriptedImporter meta — Unity still
    /// imports those as blank Simulator entries and can throw IndexOutOfRangeException.
    /// </summary>
    public static partial class FoldableDeviceCatalog
    {
        #region Constants

        public const string DisabledDeviceSuffix = ".off";

        #endregion

        #region Skins / overlays

        public static bool IsSimulatorSkinActive(string relativeDeviceFile)
        {
            if (string.IsNullOrEmpty(relativeDeviceFile))
                return false;
            if (!ResponsiveUiEditorPath.CombineContained(GetPackageRoot(), relativeDeviceFile, out string abs))
                return false;
            return File.Exists(abs);
        }

        public static bool SetSimulatorEnabled(string id, bool enabled, out string error)
        {
            error = null;
            var profile = FindById(id);
            if (profile == null)
            {
                error = "Profile not found: " + id;
                return false;
            }

            if (string.IsNullOrEmpty(profile.DeviceFile))
            {
                error = "This profile has no Device Simulator .device skin path.";
                return false;
            }

            if (!enabled && IsDesignCanvasReference(id))
            {
                error =
                    "Design Canvas reference devices stay enabled in Device Simulator. " +
                    "Change Edit → Project Settings → Responsive UI first.";
                return false;
            }

            string root = GetPackageRoot();
            if (!ResponsiveUiEditorPath.CombineContained(root, profile.DeviceFile, out string activeAbs))
            {
                error = "Device file path resolves outside the package root.";
                return false;
            }

            string disabledAbs = activeAbs + DisabledDeviceSuffix;
            bool activeExists = File.Exists(activeAbs);
            bool disabledExists = File.Exists(disabledAbs);

            if (enabled)
            {
                if (activeExists)
                {
                    EnsureDeviceImporterMeta(activeAbs);
                    return true;
                }

                if (!disabledExists)
                {
                    error =
                        "Missing .device skin (and no .device.off). Re-import the toolkit skins or restore the file.";
                    return false;
                }

                EnableFromDisabled(disabledAbs, activeAbs);
            }
            else
            {
                if (!activeExists)
                {
                    // Still scrub any ghost .device.off that carries a Device importer meta.
                    if (disabledExists)
                        EnsureDisabledPlainMeta(disabledAbs);
                    return true;
                }

                DisableToOff(activeAbs, disabledAbs);
            }

            AssetDatabase.Refresh();
            Reload();
            SimulatorDeviceBridge.ForceReloadDeviceLists();
            return true;
        }

        /// <summary>
        /// Rewrites active <c>.device</c> friendlyName from current catalog flags
        /// (Flip / Fold Cover / iPhone Duo / Fold role labels for Design Canvas references;
        /// ★ Fold/Flip/Dual otherwise). Returns how many files were patched.
        /// Device Simulator rebuilds from imported assets — always Refresh after patches.
        /// </summary>
        public static int SyncDesignReferenceSimulatorLabels()
        {
            var devices = Devices;
            if (devices == null || devices.Length == 0)
                return 0;

            string root = GetPackageRoot();
            int written = 0;
            var patchedAbs = new List<string>(4);
            for (int i = 0; i < devices.Length; i++)
            {
                var profile = devices[i];
                if (profile == null || string.IsNullOrEmpty(profile.DeviceFile))
                    continue;
                if (!IsSimulatorSkinActive(profile.DeviceFile))
                    continue;
                if (!ResponsiveUiEditorPath.CombineContained(root, profile.DeviceFile, out string abs))
                    continue;

                string friendly = BuildFriendlyName(
                    profile.ListCategory, profile.ShortName, profile.Id, profile.Screens,
                    profile.HasLandscapeNativeInner);
                if (PatchDeviceFriendlyName(abs, friendly))
                {
                    written++;
                    patchedAbs.Add(abs);
                }
                profile.FriendlyName = friendly;
            }

            if (written > 0)
            {
                // Force-update patched skins before list rebuild — Device Simulator reads
                // imported .device assets; ForceReload alone can race a deferred Refresh.
                for (int i = 0; i < patchedAbs.Count; i++)
                {
                    string assetPath = ResponsiveUiEditorPath.ToAssetPath(patchedAbs[i]);
                    if (!string.IsNullOrEmpty(assetPath))
                        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
                }

                AssetDatabase.Refresh();
                Reload();
                SimulatorDeviceBridge.ForceReloadDeviceLists();
            }

            return written;
        }

        /// <summary>
        /// Scrub any <c>*.device.off</c> that still use the Device ScriptedImporter
        /// (blank Simulator list entries / IndexOutOfRangeException on select).
        /// </summary>
        public static int CleanupDisabledDeviceGhostMetas()
        {
            string dir = ResponsiveUiEditorPath.CombineRoot(GetPackageRoot(), "Editor/Devices");
            if (!Directory.Exists(dir))
                return 0;

            int fixedCount = 0;
            foreach (string path in Directory.GetFiles(dir, "*.device.off", SearchOption.TopDirectoryOnly))
            {
                if (EnsureDisabledPlainMeta(path))
                    fixedCount++;
            }

            if (fixedCount > 0)
                AssetDatabase.Refresh();
            return fixedCount;
        }

        static void EnableFromDisabled(string disabledAbs, string activeAbs)
        {
            if (File.Exists(activeAbs))
                DeleteWithMeta(activeAbs);

            File.Move(disabledAbs, activeAbs);
            // Drop whatever meta the .off had — let Unity assign the Device importer for .device.
            string disabledMeta = disabledAbs + ".meta";
            if (File.Exists(disabledMeta))
                File.Delete(disabledMeta);
            string activeMeta = activeAbs + ".meta";
            if (File.Exists(activeMeta))
                File.Delete(activeMeta);

            EnsureDeviceImporterMeta(activeAbs);
        }

        static void DisableToOff(string activeAbs, string disabledAbs)
        {
            if (File.Exists(disabledAbs))
                DeleteWithMeta(disabledAbs);

            File.Move(activeAbs, disabledAbs);
            // Do NOT move the Device ScriptedImporter .meta onto .device.off.
            string activeMeta = activeAbs + ".meta";
            if (File.Exists(activeMeta))
                File.Delete(activeMeta);

            EnsureDisabledPlainMeta(disabledAbs);
        }

        static void EnsureDeviceImporterMeta(string deviceAbs)
        {
            // Matching Unity's built-in Device Simulator ScriptedImporter binding.
            string metaPath = deviceAbs + ".meta";
            string guid = File.Exists(metaPath)
                ? ReadGuidFromMeta(metaPath) ?? Guid.NewGuid().ToString("N")
                : Guid.NewGuid().ToString("N");

            var sb = new StringBuilder(256);
            sb.Append("fileFormatVersion: 2\n");
            sb.Append("guid: ").Append(guid).Append('\n');
            sb.Append("ScriptedImporter:\n");
            sb.Append("  internalIDToNameTable: []\n");
            sb.Append("  externalObjects: {}\n");
            sb.Append("  serializedVersion: 2\n");
            sb.Append("  userData: \n");
            sb.Append("  assetBundleName: \n");
            sb.Append("  assetBundleVariant: \n");
            sb.Append("  script: {fileID: 13973, guid: 0000000000000000e000000000000000, type: 0}\n");
            string next = sb.ToString();
            if (File.Exists(metaPath) && File.ReadAllText(metaPath) == next)
                return;
            File.WriteAllText(metaPath, next, new UTF8Encoding(false));
        }

        /// <returns>True if the meta was created or replaced.</returns>
        static bool EnsureDisabledPlainMeta(string disabledAbs)
        {
            string metaPath = disabledAbs + ".meta";
            bool needsRewrite = !File.Exists(metaPath);
            if (!needsRewrite)
            {
                string existing = File.ReadAllText(metaPath);
                // Device ScriptedImporter on a .off file → blank Simulator entry.
                if (existing.IndexOf("ScriptedImporter", StringComparison.Ordinal) >= 0 ||
                    existing.IndexOf("fileID: 13973", StringComparison.Ordinal) >= 0)
                    needsRewrite = true;
            }

            if (!needsRewrite)
                return false;

            string guid = File.Exists(metaPath)
                ? ReadGuidFromMeta(metaPath) ?? Guid.NewGuid().ToString("N")
                : Guid.NewGuid().ToString("N");

            var sb = new StringBuilder(192);
            sb.Append("fileFormatVersion: 2\n");
            sb.Append("guid: ").Append(guid).Append('\n');
            sb.Append("DefaultImporter:\n");
            sb.Append("  externalObjects: {}\n");
            sb.Append("  userData: \n");
            sb.Append("  assetBundleName: \n");
            sb.Append("  assetBundleVariant: \n");
            File.WriteAllText(metaPath, sb.ToString(), new UTF8Encoding(false));
            return true;
        }

        #endregion

        #region Helpers

        static string ReadGuidFromMeta(string metaPath)
        {
            foreach (string line in File.ReadAllLines(metaPath))
            {
                const string prefix = "guid: ";
                if (line.StartsWith(prefix, StringComparison.Ordinal))
                    return line.Substring(prefix.Length).Trim();
            }

            return null;
        }

        static void DeleteWithMeta(string abs)
        {
            if (File.Exists(abs))
                File.Delete(abs);
            string meta = abs + ".meta";
            if (File.Exists(meta))
                File.Delete(meta);
        }

        #endregion
    }
}
