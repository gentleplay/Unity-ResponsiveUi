using System;
using System.Collections.Generic;
using System.IO;
using FDPlus.ResponsiveUI.Shared.Editor;
using UnityEditor;
using UnityEngine;

namespace FDPlus.Foldable.Editor
{
    /// <summary>
    /// Catalog of Device Simulator entries. Device data lives in
    /// <c>Editor/Devices/Profiles/*.json</c> (screens, skins, source links).
    /// </summary>
    public static partial class FoldableDeviceCatalog
    {
        #region Constants

        public const string FoldPrefix = ResponsiveUiSimulatorName.ToolkitFoldPrefix;
        public const string FlipPrefix = ResponsiveUiSimulatorName.ToolkitFlipPrefix;
        public const string DualPrefix = ResponsiveUiSimulatorName.ToolkitDualPrefix;
        public const string DesignReferenceMarker = ResponsiveUiSimulatorName.DesignReferenceMarker;
        /// <summary>ASCII SOH — sorts Design Canvas references above Unity built-ins; not shown in the label.</summary>
        public const string SimulatorListTopPrefix = "\u0001";

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

        public sealed class ScreenProfile
        {
            public string Name;
            /// <summary>Portrait width (canonical; same as <see cref="Width"/>).</summary>
            public int PortraitWidth;
            /// <summary>Portrait height (canonical; same as <see cref="Height"/>).</summary>
            public int PortraitHeight;
            public int LandscapeWidth;
            public int LandscapeHeight;
            public bool CoverOrOuter;

            /// <summary>Portrait width alias used by Device Simulator data.</summary>
            public int Width
            {
                get => PortraitWidth;
                set => PortraitWidth = value;
            }

            /// <summary>Portrait height alias used by Device Simulator data.</summary>
            public int Height
            {
                get => PortraitHeight;
                set => PortraitHeight = value;
            }

            public void GetOrientedSize(bool landscape, out int width, out int height)
            {
                if (landscape)
                {
                    width = LandscapeWidth > 0 ? LandscapeWidth : PortraitHeight;
                    height = LandscapeHeight > 0 ? LandscapeHeight : PortraitWidth;
                }
                else
                {
                    width = PortraitWidth;
                    height = PortraitHeight;
                }
            }
        }

        public sealed class SourceLink
        {
            public string Title;
            public string Url;
            public string Quote;
        }

        public sealed class OverlayLink
        {
            public string Screen;
            public string Path;
        }

        public sealed class DeviceProfile
        {
            public string Id;
            public string FriendlyName;
            public string ShortName;
            /// <summary>Fold / Flip / Dual — drives Simulator list prefix.</summary>
            public string ListCategory;
            public string DeviceModel;
            public FoldableDeviceKind Kind = FoldableDeviceKind.FoldablePhone;
            public ScreenProfile[] Screens;
            public FoldableOrientation PreferredHinge = FoldableOrientation.Horizontal;
            public bool PreferSeparating;
            /// <summary>
            /// Inner panel is landscape-native (iPhone Duo). Half-open Portrait is the tall
            /// horizontal laptop fold; Landscape is the wide vertical book fold.
            /// Galaxy-style Folds (portrait-native inner) leave this false. Read
            /// <see cref="HasLandscapeNativeInner"/>: the flag only applies to a Fold.
            /// </summary>
            public bool InnerLandscapeNative;

            /// <summary>Effective <see cref="InnerLandscapeNative"/> (Fold kind only).</summary>
            public bool HasLandscapeNativeInner =>
                IsLandscapeNativeInner(Kind, InnerLandscapeNative);
            public string BasedOn;
            public string Notes;
            public string VerifiedDate;
            public string VerifiedStatus;
            public SourceLink[] Sources;
            public string DeviceFile;
            public OverlayLink[] Overlays;
            /// <summary>True when the linked <c>.device</c> skin is present (not <c>.device.off</c>).</summary>
            public bool SimulatorEnabled;
            /// <summary>Absolute path of the JSON this profile was loaded from (may be empty).</summary>
            public string ProfilePath;
        }

        #endregion

        #region Catalog

        /// <summary>Fired after <see cref="Reload"/> (Save / Delete / Duplicate / manual reload).</summary>
        public static event Action CatalogChanged;

        #endregion

        #region Types

        public sealed class Resolution
        {
            public DeviceProfile Device;
            public ScreenProfile Screen;
            public bool IsKnownFoldable;
        }

        [Serializable]
        class ScreenDto
        {
            public string name;
            public int width;
            public int height;
            public int portraitWidth;
            public int portraitHeight;
            public int landscapeWidth;
            public int landscapeHeight;
            public bool coverOrOuter;
        }

        [Serializable]
        class SourceDto
        {
            public string title;
            public string url;
            public string quote;
        }

        [Serializable]
        class OverlayDto
        {
            public string screen;
            public string path;
        }

        [Serializable]
        class SkinsDto
        {
            public string deviceFile;
            public OverlayDto[] overlays;
        }

        [Serializable]
        class DeviceProfileDto
        {
            public string id;
            public string shortName;
            public string listCategory;
            public string deviceModel;
            public string kind;
            public string preferredHinge;
            public bool preferSeparating;
            public bool innerLandscapeNative;
            public string basedOn;
            public string notes;
            public string verifiedDate;
            public string verifiedStatus;
            public SourceDto[] sources;
            public SkinsDto skins;
            public ScreenDto[] screens;
        }

        #endregion

        #region Catalog

        /// <summary>Loaded device profiles (lazy). Call <see cref="Reload"/> after editing JSON.</summary>
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
            string root = GetPackageRoot();
            string dir = GetProfilesDirectory();
            if (!Directory.Exists(dir))
            {
                Debug.LogError($"[FoldAndFlip] Device profiles folder missing: {dir}");
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
                        Debug.LogError($"[FoldAndFlip] Invalid device profile JSON: {path}");
                        continue;
                    }

                    if (dto.screens == null || dto.screens.Length == 0)
                    {
                        Debug.LogError($"[FoldAndFlip] Profile has no screens: {path}");
                        continue;
                    }

                    var profile = MapDto(dto);
                    profile.ProfilePath = path;
                    profile.SimulatorEnabled = IsSimulatorSkinActive(profile.DeviceFile);
                    ValidateProfileSkins(profile, root, path);
                    list.Add(profile);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[FoldAndFlip] Failed to load device profile {path}: {ex.Message}");
                }
            }

            list.Sort((a, b) => string.Compare(a.ShortName, b.ShortName, StringComparison.OrdinalIgnoreCase));
            bool firstLoad = s_Devices == null;
            s_Devices = list.ToArray();
            if (s_Devices.Length == 0)
                Debug.LogError("[FoldAndFlip] No device profiles loaded from " + dir);

            if (!firstLoad)
                CatalogChanged?.Invoke();
        }

        static DeviceProfile MapDto(DeviceProfileDto dto)
        {
            var screens = new ScreenProfile[dto.screens.Length];
            for (int i = 0; i < dto.screens.Length; i++)
            {
                var s = dto.screens[i];
                int pw = s.portraitWidth > 0 ? s.portraitWidth : s.width;
                int ph = s.portraitHeight > 0 ? s.portraitHeight : s.height;
                if (pw <= 0 || ph <= 0)
                {
                    Debug.LogError($"[FoldAndFlip] Screen '{s.name}' missing size in profile {dto.id}");
                    pw = Math.Max(1, pw);
                    ph = Math.Max(1, ph);
                }

                // Portrait must be the upright pair (width <= height when non-square).
                if (pw > ph)
                {
                    int t = pw;
                    pw = ph;
                    ph = t;
                }

                int lw = s.landscapeWidth > 0 ? s.landscapeWidth : ph;
                int lh = s.landscapeHeight > 0 ? s.landscapeHeight : pw;
                if (lw < lh && pw != ph)
                {
                    // Landscape pair should be width >= height for non-square panels.
                    int t = lw;
                    lw = lh;
                    lh = t;
                }

                screens[i] = new ScreenProfile
                {
                    Name = s.name,
                    PortraitWidth = pw,
                    PortraitHeight = ph,
                    LandscapeWidth = lw,
                    LandscapeHeight = lh,
                    CoverOrOuter = s.coverOrOuter
                };
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

            OverlayLink[] overlays = Array.Empty<OverlayLink>();
            string deviceFile = null;
            if (dto.skins != null)
            {
                deviceFile = dto.skins.deviceFile;
                if (dto.skins.overlays != null && dto.skins.overlays.Length > 0)
                {
                    overlays = new OverlayLink[dto.skins.overlays.Length];
                    for (int i = 0; i < dto.skins.overlays.Length; i++)
                    {
                        overlays[i] = new OverlayLink
                        {
                            Screen = dto.skins.overlays[i].screen,
                            Path = dto.skins.overlays[i].path
                        };
                    }
                }
            }

            string category = string.IsNullOrEmpty(dto.listCategory)
                ? CategoryFromKind(ParseKind(dto.kind))
                : dto.listCategory;

            return new DeviceProfile
            {
                Id = dto.id,
                ShortName = dto.shortName,
                ListCategory = category,
                FriendlyName = BuildFriendlyName(
                    category, dto.shortName, dto.id, screens,
                    IsLandscapeNativeInner(ParseKind(dto.kind), dto.innerLandscapeNative)),
                DeviceModel = dto.deviceModel,
                Kind = ParseKind(dto.kind),
                PreferredHinge = ParseHinge(dto.preferredHinge),
                PreferSeparating = dto.preferSeparating,
                InnerLandscapeNative = dto.innerLandscapeNative,
                BasedOn = dto.basedOn,
                Notes = dto.notes,
                VerifiedDate = dto.verifiedDate,
                VerifiedStatus = dto.verifiedStatus,
                Sources = sources,
                DeviceFile = deviceFile,
                Overlays = overlays,
                Screens = screens
            };
        }

        /// <summary>
        /// The landscape-native inner flag only applies to a Fold. The runtime hinge rule
        /// (<c>ToScreenSpaceHalfOpenHinge</c>) ignores it for Flip and Dual.
        /// </summary>
        public static bool IsLandscapeNativeInner(FoldableDeviceKind kind, bool innerLandscapeNative) =>
            kind == FoldableDeviceKind.FoldablePhone && innerLandscapeNative;

        public static string CategoryFromKind(FoldableDeviceKind kind)
        {
            switch (kind)
            {
                case FoldableDeviceKind.FlipablePhone: return "Flip";
                case FoldableDeviceKind.DualScreen: return "Dual";
                default: return "Fold";
            }
        }

        public static string KindToJson(FoldableDeviceKind kind)
        {
            switch (kind)
            {
                case FoldableDeviceKind.FlipablePhone: return "FlipablePhone";
                case FoldableDeviceKind.DualScreen: return "DualScreen";
                default: return "FoldablePhone";
            }
        }

        public static string GetProfilesDirectory()
        {
            return ResponsiveUiEditorPath.CombineRoot(GetPackageRoot(), ProfilesRelativePath);
        }

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

        static string BuildFriendlyName(
            string listCategory,
            string shortName,
            string profileId,
            ScreenProfile[] screens,
            bool landscapeNativeInner)
        {
            string name = string.IsNullOrEmpty(shortName) ? "Device" : shortName;

            // Design Canvas references: role prefix + ShortName + invisible sort-to-top
            // prefix (Flip / Fold Cover / iPhone Duo / Fold). Keep ★ Fold/Flip/Dual only on
            // non-reference toolkit skins. IsToolkitListName still matches ShortName.
            // iPhone Duo is the landscape-native inner profile, not an id match.
            if (IsDesignCanvasReference(profileId))
            {
                if (string.Equals(listCategory, "Flip", StringComparison.OrdinalIgnoreCase))
                    return SimulatorListTopPrefix + ResponsiveUiSimulatorName.DesignRoleFlip + name;
                if (IsPassportFoldCover(profileId, screens))
                    return SimulatorListTopPrefix + ResponsiveUiSimulatorName.DesignRoleFoldCover + name;
                if (landscapeNativeInner)
                    return SimulatorListTopPrefix + ResponsiveUiSimulatorName.DesignRoleIphoneDuo + name;
                return SimulatorListTopPrefix + ResponsiveUiSimulatorName.DesignRoleFold + name;
            }

            if (string.Equals(listCategory, "Flip", StringComparison.OrdinalIgnoreCase))
                return FlipPrefix + name;
            if (string.Equals(listCategory, "Dual", StringComparison.OrdinalIgnoreCase))
                return DualPrefix + name;
            return FoldPrefix + name;
        }

        /// <summary>
        /// Fold8-style passport Cover (same conceptual bands as
        /// <c>ResponsiveDevice.IsKnownFoldCoverPhoneSize</c>: short 1248 / long 1800–2000).
        /// Falls back to profile id containing <c>fold8</c>.
        /// </summary>
        static bool IsPassportFoldCover(string profileId, ScreenProfile[] screens)
        {
            if (screens != null)
            {
                for (int i = 0; i < screens.Length; i++)
                {
                    ScreenProfile s = screens[i];
                    if (s == null || !s.CoverOrOuter)
                        continue;
                    int shortSide = Math.Min(Math.Abs(s.PortraitWidth), Math.Abs(s.PortraitHeight));
                    int longSide = Math.Max(Math.Abs(s.PortraitWidth), Math.Abs(s.PortraitHeight));
                    if (shortSide == 1248 && longSide >= 1800 && longSide <= 2000)
                        return true;
                }
            }

            return !string.IsNullOrEmpty(profileId) &&
                   profileId.IndexOf("fold8", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        static FoldableDeviceKind ParseKind(string kind)
        {
            if (string.Equals(kind, "FlipablePhone", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(kind, "Flip", StringComparison.OrdinalIgnoreCase))
                return FoldableDeviceKind.FlipablePhone;
            if (string.Equals(kind, "DualScreen", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(kind, "Dual", StringComparison.OrdinalIgnoreCase))
                return FoldableDeviceKind.DualScreen;
            return FoldableDeviceKind.FoldablePhone;
        }

        static FoldableOrientation ParseHinge(string hinge)
        {
            if (string.Equals(hinge, "Vertical", StringComparison.OrdinalIgnoreCase))
                return FoldableOrientation.Vertical;
            return FoldableOrientation.Horizontal;
        }

        #endregion

        #region Skins / overlays

        static void ValidateProfileSkins(DeviceProfile profile, string packageRoot, string profilePath)
        {
            if (!string.IsNullOrEmpty(profile.DeviceFile))
            {
                string deviceAbs = ResponsiveUiEditorPath.CombineRoot(packageRoot, profile.DeviceFile);
                string disabledAbs = deviceAbs + DisabledDeviceSuffix;
                if (!File.Exists(deviceAbs) && !File.Exists(disabledAbs))
                    Debug.LogWarning($"[FoldAndFlip] Missing .device for {profile.ShortName}: {profile.DeviceFile} (from {Path.GetFileName(profilePath)})");
            }

            if (profile.Overlays == null)
                return;

            foreach (var overlay in profile.Overlays)
            {
                if (string.IsNullOrEmpty(overlay.Path))
                    continue;
                string abs = ResponsiveUiEditorPath.CombineRoot(packageRoot, overlay.Path);
                if (!File.Exists(abs))
                    Debug.LogWarning($"[FoldAndFlip] Missing overlay for {profile.ShortName}/{overlay.Screen}: {overlay.Path}");
            }
        }

        #endregion

        #region Helpers

        public static string GetPackageRoot()
        {
            if (!string.IsNullOrEmpty(s_PackageRoot) && Directory.Exists(s_PackageRoot))
                return s_PackageRoot;

            string[] guids = AssetDatabase.FindAssets("FoldableDeviceCatalog t:MonoScript");
            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(assetPath) ||
                    !assetPath.EndsWith("FoldableDeviceCatalog.cs", StringComparison.OrdinalIgnoreCase))
                    continue;

                // .../Editor/FoldableDeviceCatalog.cs → package root
                string editorDir = Path.GetDirectoryName(assetPath);
                string root = Path.GetDirectoryName(editorDir);
                if (!string.IsNullOrEmpty(root))
                {
                    root = ResponsiveUiEditorPath.ToAssetSeparators(root);
                    string dataParent = Path.GetDirectoryName(Application.dataPath);
                    if (!string.IsNullOrEmpty(dataParent) && root.StartsWith("Assets/", StringComparison.Ordinal))
                        s_PackageRoot = ResponsiveUiEditorPath.CombineRoot(dataParent, root);
                    else if (root.StartsWith("Packages/", StringComparison.Ordinal))
                        s_PackageRoot = ResponsiveUiEditorPath.CombineRoot(dataParent ?? ".", root);
                    else
                        s_PackageRoot = Path.GetFullPath(root);
                    return s_PackageRoot;
                }
            }

            // Fallback: nested under Assets/ResponsiveUI/FoldAndFlip
            s_PackageRoot = Path.GetFullPath(
                Path.Combine(
                    Application.dataPath,
                    FoldablePackageSetup.ModuleRelativePath.Replace('/', Path.DirectorySeparatorChar)));
            return s_PackageRoot;
        }

        public static string StripHighlightPrefix(string name)
            => ResponsiveUiSimulatorName.Normalize(name);

        public static bool NamesMatch(string a, string b)
            => ResponsiveUiSimulatorName.Match(a, b);

        #endregion

        #region Catalog

        public static Resolution Resolve(string deviceModel, int width, int height)
        {
            DeviceProfile device = null;
            if (!string.IsNullOrEmpty(deviceModel))
            {
                foreach (var candidate in Devices)
                {
                    if (string.Equals(candidate.DeviceModel, deviceModel, StringComparison.OrdinalIgnoreCase) ||
                        deviceModel.IndexOf(candidate.DeviceModel, StringComparison.OrdinalIgnoreCase) >= 0 ||
                        candidate.DeviceModel.IndexOf(deviceModel, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        device = candidate;
                        break;
                    }
                }
            }

            if (device == null)
            {
                foreach (var candidate in Devices)
                {
                    foreach (var screen in candidate.Screens)
                    {
                        if (ScreenMatches(screen, width, height))
                        {
                            return new Resolution
                            {
                                Device = candidate,
                                Screen = screen,
                                IsKnownFoldable = true
                            };
                        }
                    }
                }

                return new Resolution { IsKnownFoldable = false };
            }

            ScreenProfile matchedScreen = null;
            foreach (var screen in device.Screens)
            {
                if (ScreenMatches(screen, width, height))
                {
                    matchedScreen = screen;
                    break;
                }
            }

            if (matchedScreen == null && device.Screens.Length > 0)
                matchedScreen = device.Screens[device.Screens.Length - 1];

            return new Resolution
            {
                Device = device,
                Screen = matchedScreen,
                IsKnownFoldable = true
            };
        }

        public static Resolution ResolveFromSimulator()
        {
            if (SimulatorDeviceBridge.GetActiveSelection(out var selection) && selection.IsValid)
            {
                var byName = ResolveByFriendlyName(
                    selection.FriendlyName,
                    selection.DeviceModel,
                    selection.ScreenWidth,
                    selection.ScreenHeight,
                    selection.ScreenIndex);
                if (byName.IsKnownFoldable)
                    return byName;
            }

            return Resolve(SystemInfo.deviceModel, Screen.width, Screen.height);
        }

        public static Resolution ResolveByFriendlyName(
            string friendlyName,
            string deviceModel,
            int width,
            int height,
            int screenIndex = -1)
        {
            if (IsToolkitListName(friendlyName))
            {
                foreach (var candidate in Devices)
                {
                    if (NamesMatch(candidate.FriendlyName, friendlyName) ||
                        NamesMatch(candidate.ShortName, friendlyName) ||
                        (!string.IsNullOrEmpty(candidate.ShortName) &&
                         friendlyName.IndexOf(candidate.ShortName, StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        return BuildResolution(candidate, width, height, screenIndex);
                    }
                }
            }

            if (!string.IsNullOrEmpty(deviceModel))
            {
                var byModel = Resolve(deviceModel, width, height);
                if (byModel.IsKnownFoldable)
                    return byModel;
            }

            return Resolve(deviceModel, width, height);
        }

        public static bool IsToolkitListName(string friendlyName)
        {
            if (string.IsNullOrEmpty(friendlyName))
                return false;

            string normalized = StripHighlightPrefix(friendlyName);
            foreach (var device in Devices)
            {
                if (NamesMatch(device.FriendlyName, friendlyName) ||
                    NamesMatch(device.ShortName, friendlyName) ||
                    NamesMatch(device.FriendlyName, normalized) ||
                    NamesMatch(device.ShortName, normalized))
                    return true;
            }

            return false;
        }

        static Resolution BuildResolution(DeviceProfile device, int width, int height, int screenIndex)
        {
            ScreenProfile matched = null;

            // Prefer live WxH over ScreenIndex — index can still point at Inner while
            // Simulator has already switched to Cover (Fold8 Cover→Pad landmine).
            foreach (var screen in device.Screens)
            {
                if (ScreenMatches(screen, width, height))
                {
                    matched = screen;
                    break;
                }
            }

            if (matched == null && screenIndex >= 0 && screenIndex < device.Screens.Length)
                matched = device.Screens[screenIndex];

            if (matched == null && device.Screens.Length > 0)
                matched = device.Screens[Mathf.Clamp(screenIndex, 0, device.Screens.Length - 1)];

            return new Resolution
            {
                Device = device,
                Screen = matched,
                IsKnownFoldable = true
            };
        }

        /// <summary>True when WxH matches portrait or landscape of this catalog screen.</summary>
        public static bool ScreenMatchesPublic(ScreenProfile screen, int width, int height) =>
            ScreenMatches(screen, width, height);

        static bool ScreenMatches(ScreenProfile screen, int width, int height)
        {
            if (screen == null)
                return false;
            if ((screen.PortraitWidth == width && screen.PortraitHeight == height) ||
                (screen.LandscapeWidth == width && screen.LandscapeHeight == height) ||
                (screen.PortraitWidth == height && screen.PortraitHeight == width))
                return true;

            // Fold8 Cover safe-area (nav bar 120px): 1248×1852 / 1852×1248.
            if (screen.CoverOrOuter &&
                screen.PortraitWidth == 1248 && screen.PortraitHeight == 1972 &&
                ((width == 1248 && height == 1852) || (width == 1852 && height == 1248)))
                return true;

            return false;
        }

        #endregion

        #region Apply

        public static void ApplyDeviceDefaults(Resolution resolution, ref FoldableState state,
            ref FoldableOrientation orientation, ref float hingeNormalized, ref int hingeThicknessPx,
            ref bool isSeparating)
        {
            if (resolution?.Device == null)
                return;

            var device = resolution.Device;
            var screen = resolution.Screen;

            orientation = device.PreferredHinge;
            hingeNormalized = 0.5f;
            hingeThicknessPx = 24;
            isSeparating = device.PreferSeparating;

            if (screen != null && screen.CoverOrOuter)
            {
                state = FoldableState.Flat;
                isSeparating = false;
            }
            else if (device.PreferSeparating)
            {
                state = FoldableState.Flat;
                isSeparating = true;
            }
            else
            {
                state = FoldableState.Flat;
            }
        }

        #endregion
    }
}
