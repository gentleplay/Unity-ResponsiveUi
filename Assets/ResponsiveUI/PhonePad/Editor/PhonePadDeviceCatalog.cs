using System;
using System.Collections.Generic;
using System.IO;
using FDPlus.ResponsiveUI.Shared.Editor;
using UnityEditor;
using UnityEngine;

namespace FDPlus.PhonePad.Editor
{
    #region Types

    public enum PhonePadDeviceKind
    {
        Phone = 0,
        Pad = 1,
    }

    #endregion

    /// <summary>
    /// Catalog of non-foldable phone / pad profiles stored as JSON under
    /// <c>Editor/Devices/Profiles/*.json</c>.
    /// Optional Unity Device Simulator skins use <c>.device</c> files (created on enable,
    /// deleted on disable).
    /// </summary>
    public static partial class PhonePadDeviceCatalog
    {
        #region Constants

        const string ProfilesRelativePath = "Editor/Devices/Profiles";

        #endregion

        #region Catalog

        /// <summary>
        /// Optional hook from Responsive UI Project Settings: true when <paramref name="profileId"/>
        /// is a Design Canvas reference device.
        /// </summary>
        public static Func<string, bool> IsDesignCanvasReferenceId;

        static DeviceProfile[] s_Devices;
        static string s_PackageRoot;

        public static bool IsDesignCanvasReference(string profileId) =>
            !string.IsNullOrEmpty(profileId) &&
            IsDesignCanvasReferenceId != null &&
            IsDesignCanvasReferenceId(profileId);

        #endregion

        #region Types

        public sealed class DeviceProfile
        {
            public string Id;
            public string ShortName;
            public string Brand;
            public string DeviceModel;
            public PhonePadDeviceKind Kind = PhonePadDeviceKind.Phone;
            public int PortraitWidth;
            public int PortraitHeight;
            public int LandscapeWidth;
            public int LandscapeHeight;
            public float Dpi;
            public float DiagonalInches;
            public string BasedOn;
            public string Notes;
            public string VerifiedDate;
            public string VerifiedStatus;
            public SourceLink[] Sources;
            public string DeviceFile;
            public bool SimulatorEnabled;
            public string ProfilePath;

            public string DisplayLabel =>
                string.IsNullOrEmpty(Brand) ? ShortName : $"{Brand} / {ShortName}";
        }

        public sealed class SourceLink
        {
            public string Title;
            public string Url;
            public string Quote;
        }

        [Serializable]
        class SourceDto
        {
            public string title;
            public string url;
            public string quote;
        }

        [Serializable]
        class DeviceProfileDto
        {
            public string id;
            public string shortName;
            public string brand;
            public string deviceModel;
            public string kind;
            public int portraitWidth;
            public int portraitHeight;
            public int landscapeWidth;
            public int landscapeHeight;
            public float dpi;
            public float diagonalInches;
            public string basedOn;
            public string notes;
            public string verifiedDate;
            public string verifiedStatus;
            public string deviceFile;
            public SourceDto[] sources;
        }

        #endregion

        #region Catalog

        public static event Action CatalogChanged;

        public static DeviceProfile[] Devices
        {
            get
            {
                if (s_Devices == null)
                    Reload();
                return s_Devices;
            }
        }

        public static void Reload()
        {
            s_PackageRoot = null;
            var list = new List<DeviceProfile>();
            string dir = GetProfilesDirectory();
            if (!Directory.Exists(dir))
            {
                Debug.LogWarning($"[PhonePad] Device profiles folder missing: {dir}");
                bool firstLoadMissing = s_Devices == null;
                s_Devices = Array.Empty<DeviceProfile>();
                if (!firstLoadMissing)
                    CatalogChanged?.Invoke();
                return;
            }

            foreach (string path in Directory.GetFiles(dir, "*.json", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    string json = File.ReadAllText(path);
                    var dto = JsonUtility.FromJson<DeviceProfileDto>(json);
                    if (dto == null || string.IsNullOrEmpty(dto.shortName))
                    {
                        Debug.LogError($"[PhonePad] Invalid device profile JSON: {path}");
                        continue;
                    }

                    if (dto.portraitWidth < 1 || dto.portraitHeight < 1)
                    {
                        Debug.LogError($"[PhonePad] Profile missing portrait size: {path}");
                        continue;
                    }

                    var profile = MapDto(dto);
                    profile.ProfilePath = path;
                    profile.SimulatorEnabled = IsSimulatorSkinActive(ResolveDeviceFileRelative(profile));
                    list.Add(profile);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[PhonePad] Failed to load device profile {path}: {ex.Message}");
                }
            }

            list.Sort((a, b) =>
            {
                int brand = string.Compare(a.Brand, b.Brand, StringComparison.OrdinalIgnoreCase);
                if (brand != 0)
                    return brand;
                return string.Compare(a.ShortName, b.ShortName, StringComparison.OrdinalIgnoreCase);
            });

            bool firstLoad = s_Devices == null;
            s_Devices = list.ToArray();
            if (!firstLoad)
                CatalogChanged?.Invoke();
        }

        static DeviceProfile MapDto(DeviceProfileDto dto)
        {
            int pw = dto.portraitWidth;
            int ph = dto.portraitHeight;
            if (pw > ph)
            {
                int t = pw;
                pw = ph;
                ph = t;
            }

            int lw = dto.landscapeWidth > 0 ? dto.landscapeWidth : ph;
            int lh = dto.landscapeHeight > 0 ? dto.landscapeHeight : pw;
            if (lw < lh && pw != ph)
            {
                int t = lw;
                lw = lh;
                lh = t;
            }

            SourceLink[] sources = Array.Empty<SourceLink>();
            if (dto.sources != null && dto.sources.Length > 0)
            {
                sources = new SourceLink[dto.sources.Length];
                for (int i = 0; i < dto.sources.Length; i++)
                {
                    sources[i] = new SourceLink
                    {
                        Title = dto.sources[i].title,
                        Url = dto.sources[i].url,
                        Quote = dto.sources[i].quote
                    };
                }
            }

            return new DeviceProfile
            {
                Id = dto.id,
                ShortName = dto.shortName,
                Brand = string.IsNullOrEmpty(dto.brand) ? "Other" : dto.brand,
                DeviceModel = dto.deviceModel,
                Kind = ParseKind(dto.kind),
                PortraitWidth = pw,
                PortraitHeight = ph,
                LandscapeWidth = lw,
                LandscapeHeight = lh,
                Dpi = dto.dpi,
                DiagonalInches = dto.diagonalInches,
                BasedOn = dto.basedOn,
                Notes = dto.notes,
                VerifiedDate = dto.verifiedDate,
                VerifiedStatus = dto.verifiedStatus,
                DeviceFile = dto.deviceFile,
                Sources = sources
            };
        }

        static string KindToJson(PhonePadDeviceKind kind) =>
            kind == PhonePadDeviceKind.Pad ? "Pad" : "Phone";

        static PhonePadDeviceKind ParseKind(string kind)
        {
            if (string.Equals(kind, "Pad", StringComparison.OrdinalIgnoreCase))
                return PhonePadDeviceKind.Pad;
            return PhonePadDeviceKind.Phone;
        }

        public static string GetProfilesDirectory() =>
            ResponsiveUiEditorPath.CombineRoot(GetPackageRoot(), ProfilesRelativePath);

        public static string ProfilePathForId(string id) =>
            Path.Combine(GetProfilesDirectory(), id + ".json");

        public static DeviceProfile FindById(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            foreach (var d in Devices)
            {
                if (string.Equals(d.Id, id, StringComparison.OrdinalIgnoreCase))
                    return d;
            }
            return null;
        }

        #endregion

        #region Helpers

        public static string GetPackageRoot()
        {
            if (!string.IsNullOrEmpty(s_PackageRoot) && Directory.Exists(s_PackageRoot))
                return s_PackageRoot;

            string[] guids = AssetDatabase.FindAssets("PhonePadDeviceCatalog t:MonoScript");
            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(assetPath) ||
                    !assetPath.EndsWith("PhonePadDeviceCatalog.cs", StringComparison.OrdinalIgnoreCase))
                    continue;

                string editorDir = Path.GetDirectoryName(assetPath);
                string root = Path.GetDirectoryName(editorDir);
                if (!string.IsNullOrEmpty(root))
                {
                    root = ResponsiveUiEditorPath.ToAssetSeparators(root);
                    string dataParent = Path.GetDirectoryName(Application.dataPath);
                    if (!string.IsNullOrEmpty(dataParent) && root.StartsWith("Assets/", StringComparison.Ordinal))
                        s_PackageRoot = ResponsiveUiEditorPath.CombineRoot(dataParent, root);
                    else
                        s_PackageRoot = Path.GetFullPath(root);
                    return s_PackageRoot;
                }
            }

            // Fallback: nested under Assets/ResponsiveUI/PhonePad
            s_PackageRoot = Path.GetFullPath(
                Path.Combine(
                    Application.dataPath,
                    PhonePadPackageSetup.ModuleRelativePath.Replace('/', Path.DirectorySeparatorChar)));
            return s_PackageRoot;
        }

        #endregion
    }
}
