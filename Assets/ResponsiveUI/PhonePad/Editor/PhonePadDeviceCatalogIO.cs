using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using FDPlus.ResponsiveUI.Shared.Editor;
using UnityEditor;
using UnityEngine;

namespace FDPlus.PhonePad.Editor
{
    /// <summary>
    /// Persist / duplicate / delete phone &amp; pad profile JSON.
    /// Optional Device Simulator skins are managed separately via
    /// <see cref="SetAllSimulatorEnabled"/>.
    /// </summary>
    public static partial class PhonePadDeviceCatalog
    {
        #region Types

        /// <summary>Profile fields being edited (serializable so the catalog window keeps a draft across domain reloads).</summary>
        [Serializable]
        public sealed class EditableProfile
        {
            public string Id;
            public string SourceId;
            public string ShortName;
            public string Brand = "Other";
            public string DeviceModel = "";
            public PhonePadDeviceKind Kind = PhonePadDeviceKind.Phone;
            public int PortraitWidth = 1080;
            public int PortraitHeight = 2400;
            public int LandscapeWidth = 2400;
            public int LandscapeHeight = 1080;
            public float Dpi = 400f;
            public float DiagonalInches = 6.1f;
            public string BasedOn = "";
            public string Notes = "";
            public string VerifiedDate = "";
            public string VerifiedStatus = "approx";
            public string DeviceFile = "";
            public List<EditableSource> Sources = new List<EditableSource>();

            public string DisplayLabel =>
                string.IsNullOrEmpty(Brand) ? ShortName : $"{Brand} / {ShortName}";

            public static EditableProfile FromProfile(DeviceProfile p)
            {
                var e = new EditableProfile
                {
                    Id = p.Id ?? "",
                    SourceId = p.Id ?? "",
                    ShortName = p.ShortName ?? "",
                    Brand = string.IsNullOrEmpty(p.Brand) ? "Other" : p.Brand,
                    DeviceModel = p.DeviceModel ?? "",
                    Kind = p.Kind,
                    PortraitWidth = p.PortraitWidth,
                    PortraitHeight = p.PortraitHeight,
                    LandscapeWidth = p.LandscapeWidth > 0 ? p.LandscapeWidth : p.PortraitHeight,
                    LandscapeHeight = p.LandscapeHeight > 0 ? p.LandscapeHeight : p.PortraitWidth,
                    Dpi = p.Dpi,
                    DiagonalInches = p.DiagonalInches,
                    BasedOn = p.BasedOn ?? "",
                    Notes = p.Notes ?? "",
                    VerifiedDate = p.VerifiedDate ?? "",
                    VerifiedStatus = string.IsNullOrEmpty(p.VerifiedStatus) ? "approx" : p.VerifiedStatus,
                    DeviceFile = p.DeviceFile ?? ""
                };

                if (p.Sources != null)
                {
                    foreach (var s in p.Sources)
                    {
                        e.Sources.Add(new EditableSource
                        {
                            Title = s.Title ?? "",
                            Url = s.Url ?? "",
                            Quote = s.Quote ?? ""
                        });
                    }
                }

                return e;
            }

            public void NormalizeSizePairs()
            {
                if (PortraitWidth <= 0) PortraitWidth = 1;
                if (PortraitHeight <= 0) PortraitHeight = 1;
                if (PortraitWidth > PortraitHeight)
                {
                    int t = PortraitWidth;
                    PortraitWidth = PortraitHeight;
                    PortraitHeight = t;
                }

                LandscapeWidth = PortraitHeight;
                LandscapeHeight = PortraitWidth;
            }

            public string Validate(out string error)
            {
                error = null;
                if (string.IsNullOrWhiteSpace(Id))
                {
                    error = "Id is required (kebab-case, e.g. pixel-8).";
                    return error;
                }

                if (!Regex.IsMatch(Id, @"^[a-z0-9]+(?:-[a-z0-9]+)*$"))
                {
                    error = "Id must be lowercase kebab-case (a-z, 0-9, hyphens).";
                    return error;
                }

                if (string.IsNullOrWhiteSpace(ShortName))
                {
                    error = "Short name is required.";
                    return error;
                }

                if (PortraitWidth < 8 || PortraitHeight < 8)
                {
                    error = "Portrait size is too small.";
                    return error;
                }

                return null;
            }
        }

        [Serializable]
        public sealed class EditableSource
        {
            public string Title = "";
            public string Url = "";
            public string Quote = "";
        }

        #endregion

        #region IO

        public static bool SaveProfile(EditableProfile edit, out string error) =>
            SaveProfile(edit, syncDeviceFile: true, refreshAndReload: true, out error);

        public static bool SaveProfile(
            EditableProfile edit,
            bool syncDeviceFile,
            bool refreshAndReload,
            out string error)
        {
            error = null;
            if (edit == null)
            {
                error = "No profile.";
                return false;
            }

            edit.NormalizeSizePairs();
            if (edit.Validate(out error) != null)
                return false;

            if (string.IsNullOrWhiteSpace(edit.Brand))
                edit.Brand = "Other";

            string root = GetPackageRoot();
            string dir = GetProfilesDirectory();
            if (!ResponsiveUiEditorPath.IsContained(root, dir))
            {
                error = "Profiles directory resolves outside the package root.";
                return false;
            }

            Directory.CreateDirectory(dir);
            string path = ProfilePathForId(edit.Id);
            if (!ResponsiveUiEditorPath.IsContained(dir, path))
            {
                error = "Profile path resolves outside the Profiles directory.";
                return false;
            }

            var dto = ToDto(edit);
            string json = JsonUtility.ToJson(dto, true);
            try
            {
                File.WriteAllText(path, json, new UTF8Encoding(false));
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                error = $"Could not write {path}: {ex.Message}";
                return false;
            }

            // Renamed: the old profile goes only once the new one is on disk.
            if (!string.IsNullOrEmpty(edit.SourceId) &&
                !string.Equals(edit.SourceId, edit.Id, StringComparison.OrdinalIgnoreCase))
            {
                string oldPath = ProfilePathForId(edit.SourceId);
                if (ResponsiveUiEditorPath.IsContained(dir, oldPath) &&
                    File.Exists(oldPath) &&
                    !ResponsiveUiEditorPath.PathsEqual(oldPath, path))
                {
                    DeleteAssetOrFile(oldPath);
                }
            }

            if (syncDeviceFile &&
                !string.IsNullOrEmpty(edit.DeviceFile) &&
                IsSimulatorSkinActive(edit.DeviceFile))
            {
                // The saved profile as Reload reads it (brand, device model, sizes), not a partial copy.
                var saved = MapDto(dto);
                saved.ProfilePath = path;
                SyncActiveDeviceSkin(saved);
            }

            edit.SourceId = edit.Id;
            if (refreshAndReload)
            {
                AssetDatabase.Refresh();
                Reload();
            }

            return true;
        }

        /// <summary>Rewrites the active skin of a saved profile from all of its fields.</summary>
        static void SyncActiveDeviceSkin(DeviceProfile profile)
        {
            if (!ResponsiveUiEditorPath.CombineContained(GetPackageRoot(), profile.DeviceFile, out string abs))
                return;
            if (!File.Exists(abs))
                return;

            WriteDeviceSkin(profile, abs);
        }

        public static bool DeleteProfile(string id, out string error)
        {
            error = null;
            var profile = FindById(id);
            if (profile == null)
            {
                error = "Profile not found: " + id;
                return false;
            }

            string jsonPath = !string.IsNullOrEmpty(profile.ProfilePath)
                ? profile.ProfilePath
                : ProfilePathForId(id);
            if (!ResponsiveUiEditorPath.IsContained(GetProfilesDirectory(), jsonPath))
            {
                error = "Refusing to delete a profile outside the Profiles directory.";
                return false;
            }

            if (File.Exists(jsonPath))
            {
                string assetPath = ResponsiveUiEditorPath.ToAssetPath(jsonPath);
                if (!string.IsNullOrEmpty(assetPath))
                    AssetDatabase.DeleteAsset(assetPath);
                if (File.Exists(jsonPath))
                    File.Delete(jsonPath);
                string meta = jsonPath + ".meta";
                if (File.Exists(meta))
                    File.Delete(meta);
            }

            if (!string.IsNullOrEmpty(profile.DeviceFile) &&
                ResponsiveUiEditorPath.CombineContained(GetPackageRoot(), profile.DeviceFile, out string deviceAbs))
            {
                DeleteWithMeta(deviceAbs);
                DeleteWithMeta(deviceAbs + DisabledDeviceSuffix);
            }
            DeleteAllOverlaysForProfile(profile);

            AssetDatabase.Refresh();
            Reload();
            // Deleting a .device does not mark the Simulator list dirty.
            PhonePadSimulatorListUtil.ForceReloadDeviceLists();
            return true;
        }

        public static bool DuplicateProfile(
            string sourceId,
            string newId,
            string newShortName,
            out string error)
        {
            error = null;
            var source = FindById(sourceId);
            if (source == null)
            {
                error = "Source profile not found.";
                return false;
            }

            if (FindById(newId) != null)
            {
                error = "A profile with that id already exists.";
                return false;
            }

            var edit = EditableProfile.FromProfile(source);
            edit.Id = newId;
            edit.SourceId = newId;
            edit.ShortName = string.IsNullOrWhiteSpace(newShortName)
                ? source.ShortName + " Copy"
                : newShortName;
            edit.DeviceModel = string.IsNullOrEmpty(edit.DeviceModel)
                ? newId
                : edit.DeviceModel + " copy";
            edit.DeviceFile = "";
            edit.BasedOn = string.IsNullOrEmpty(edit.BasedOn)
                ? "Copy of " + source.ShortName
                : edit.BasedOn;
            if (edit.Validate(out error) != null)
                return false;

            return SaveProfile(edit, out error);
        }

        public static EditableProfile CreateBlank(PhonePadDeviceKind kind)
        {
            string id = kind == PhonePadDeviceKind.Pad ? "custom-pad" : "custom-phone";
            string baseId = id;
            int n = 2;
            while (FindById(id) != null)
            {
                id = baseId + "-" + n;
                n++;
            }

            var edit = new EditableProfile
            {
                Id = id,
                SourceId = "",
                ShortName = kind == PhonePadDeviceKind.Pad ? "Custom Pad" : "Custom Phone",
                Brand = "Other",
                DeviceModel = id,
                Kind = kind,
                PortraitWidth = kind == PhonePadDeviceKind.Pad ? 1600 : 1080,
                PortraitHeight = kind == PhonePadDeviceKind.Pad ? 2560 : 2400,
                Dpi = kind == PhonePadDeviceKind.Pad ? 280f : 400f,
                DiagonalInches = kind == PhonePadDeviceKind.Pad ? 11f : 6.1f,
                BasedOn = "Custom",
                Notes = "Created in Phone & Pad Device Catalog.",
                VerifiedDate = DateTime.UtcNow.ToString("yyyy-MM-dd"),
                VerifiedStatus = "approx"
            };
            edit.NormalizeSizePairs();
            return edit;
        }

        static DeviceProfileDto ToDto(EditableProfile edit)
        {
            edit.NormalizeSizePairs();
            var sources = new SourceDto[edit.Sources.Count];
            for (int i = 0; i < edit.Sources.Count; i++)
            {
                sources[i] = new SourceDto
                {
                    title = edit.Sources[i].Title,
                    url = edit.Sources[i].Url,
                    quote = edit.Sources[i].Quote
                };
            }

            return new DeviceProfileDto
            {
                id = edit.Id,
                shortName = edit.ShortName,
                brand = edit.Brand,
                deviceModel = edit.DeviceModel,
                kind = KindToJson(edit.Kind),
                portraitWidth = edit.PortraitWidth,
                portraitHeight = edit.PortraitHeight,
                landscapeWidth = edit.LandscapeWidth,
                landscapeHeight = edit.LandscapeHeight,
                dpi = edit.Dpi,
                diagonalInches = edit.DiagonalInches,
                basedOn = edit.BasedOn,
                notes = edit.Notes,
                verifiedDate = edit.VerifiedDate,
                verifiedStatus = edit.VerifiedStatus,
                deviceFile = edit.DeviceFile,
                sources = sources
            };
        }

        #endregion

        #region Helpers

        static void DeleteAssetOrFile(string abs)
        {
            string root = GetPackageRoot();
            if (!ResponsiveUiEditorPath.IsContained(root, abs))
            {
                Debug.LogWarning("[PhonePad] Refused delete outside package root: " + abs);
                return;
            }

            if (!File.Exists(abs))
                return;

            string asset = ResponsiveUiEditorPath.ToAssetPath(abs);
            if (!string.IsNullOrEmpty(asset) && AssetDatabase.LoadMainAssetAtPath(asset) != null)
                AssetDatabase.DeleteAsset(asset);
            else
            {
                File.Delete(abs);
                if (File.Exists(abs + ".meta"))
                    File.Delete(abs + ".meta");
            }
        }

        #endregion
    }
}
