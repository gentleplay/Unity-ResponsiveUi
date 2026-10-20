using System.IO;
using FDPlus.ResponsiveUI.Shared.Editor;
using UnityEditor;
using UnityEngine;

namespace FDPlus.PhonePad.Editor
{
    /// <summary>
    /// PhonePad Simulator skins enable API.
    /// First-run UX is owned by <c>ResponsiveUiPackageSetup</c>.
    /// </summary>
    public static class PhonePadPackageSetup
    {
        #region Constants

        /// <summary>Module display name inside the Responsive UI kit.</summary>
        public const string DisplayName = "Responsive UI — PhonePad";
        /// <summary>Path under Assets/ for this module.</summary>
        public const string ModuleRelativePath = "ResponsiveUI/PhonePad";
        /// <summary>Module version (aligned with the unified Responsive UI kit).</summary>
        public const string Version = "1.0.0";

        #endregion

        #region Package setup

        [MenuItem(ResponsiveUiEditorMenus.EnablePhonePadSimulator, false,
            ResponsiveUiEditorMenus.PriorityDevices + ResponsiveUiEditorMenus.PriorityGroupGap + 2)]
        public static void EnableDefaultSimulatorDevicesMenu()
        {
            if (EnableDefaultSimulatorDevices(out string message))
            {
                Debug.Log($"[Responsive UI] {message}");
                EditorUtility.DisplayDialog("Responsive UI — Simulator", message, "OK");
            }
            else
            {
                Debug.LogWarning($"[Responsive UI] {message}");
                EditorUtility.DisplayDialog("Responsive UI — Simulator", message, "OK");
            }
        }

        [MenuItem(ResponsiveUiEditorMenus.RepairPhonePadSimulator, false,
            ResponsiveUiEditorMenus.PriorityDevices + ResponsiveUiEditorMenus.PriorityGroupGap + 3)]
        public static void RepairSimulatorDevicesMenu()
        {
            PhonePadDeviceCatalog.Reload();
            int repaired = PhonePadDeviceCatalog.RepairActiveDeviceSkins();
            string message = repaired > 0
                ? $"Repaired {repaired} Device Simulator skin artifact(s)."
                : "Phone/Pad Device Simulator skins are valid.";
            Debug.Log($"[Responsive UI] {message}");
            EditorUtility.DisplayDialog("Responsive UI — Simulator", message, "OK");
        }

        /// <summary>
        /// Writes Unity Device Simulator skins for every catalog profile.
        /// </summary>
        public static bool EnableDefaultSimulatorDevices(out string message)
        {
            string root = PhonePadDeviceCatalog.GetPackageRoot();
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
            {
                message = $"{DisplayName} module root not found.";
                return false;
            }

            PhonePadDeviceCatalog.Reload();

            if (!PhonePadDeviceCatalog.SetAllSimulatorEnabled(true, out string error, out int changed))
            {
                message = string.IsNullOrEmpty(error)
                    ? "Failed to enable Device Simulator skins."
                    : error;
                return false;
            }

            int total = PhonePadDeviceCatalog.Devices != null ? PhonePadDeviceCatalog.Devices.Length : 0;
            message = changed > 0
                ? $"Enabled {changed} Device Simulator skin(s) ({total} profile(s))."
                : $"All {total} profile skin(s) already enabled.";
            return true;
        }

        /// <summary>
        /// Enables only the profiles selected as project Design Canvas references.
        /// Intended for explicit first-run setup without populating the full Simulator list.
        /// </summary>
        public static bool EnableDesignReferenceDevices(out string message)
        {
            PhonePadDeviceCatalog.Reload();
            if (!PhonePadDeviceCatalog.EnsureDesignReferenceSkinsEnabled(out string error))
            {
                message = string.IsNullOrEmpty(error)
                    ? "Failed to enable Design Canvas reference skins."
                    : error;
                return false;
            }

            message = "Enabled Device Simulator skins for the Design Canvas reference profiles.";
            return true;
        }

        #endregion
    }
}
