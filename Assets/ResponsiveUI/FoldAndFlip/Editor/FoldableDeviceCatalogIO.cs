using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using FDPlus.ResponsiveUI.Shared.Editor;
using UnityEditor;
using UnityEngine;

namespace FDPlus.Foldable.Editor
{
    /// <summary>
    /// Persist / duplicate / delete profile JSON and sync matching .device screen sizes.
    /// </summary>
    public static partial class FoldableDeviceCatalog
    {
        #region Types

        /// <summary>Mutable editor working copy for the Device Catalog window.</summary>
        public sealed class EditableProfile
        {
            public string Id;
            /// <summary>Id when the draft was loaded (for rename cleanup).</summary>
            public string SourceId;
            public string ShortName;
            public string ListCategory = "Fold";
            public string DeviceModel;
            public FoldableDeviceKind Kind = FoldableDeviceKind.FoldablePhone;
            public FoldableOrientation PreferredHinge = FoldableOrientation.Horizontal;
            public bool PreferSeparating;
            public bool InnerLandscapeNative;
            public string BasedOn = "";
            public string Notes = "";
            public string VerifiedDate = "";
            public string VerifiedStatus = "approx";
            public string DeviceFile = "";
            public readonly List<EditableScreen> Screens = new List<EditableScreen>();
            public readonly List<EditableOverlay> Overlays = new List<EditableOverlay>();
            public readonly List<EditableSource> Sources = new List<EditableSource>();

            public string FriendlyName => BuildFriendlyName(
                ListCategory, ShortName, Id, ToScreenProfiles(),
                IsLandscapeNativeInner(Kind, InnerLandscapeNative));

            ScreenProfile[] ToScreenProfiles()
            {
                if (Screens == null || Screens.Count == 0)
                    return null;
                var screens = new ScreenProfile[Screens.Count];
                for (int i = 0; i < Screens.Count; i++)
                {
                    EditableScreen s = Screens[i];
                    screens[i] = new ScreenProfile
                    {
                        Name = s.Name,
                        PortraitWidth = s.PortraitWidth,
                        PortraitHeight = s.PortraitHeight,
                        LandscapeWidth = s.LandscapeWidth,
                        LandscapeHeight = s.LandscapeHeight,
                        CoverOrOuter = s.CoverOrOuter,
                    };
                }
                return screens;
            }

            public static EditableProfile FromProfile(DeviceProfile p)
            {
                var e = new EditableProfile
                {
                    Id = p.Id ?? "",
                    SourceId = p.Id ?? "",
                    ShortName = p.ShortName ?? "",
                    ListCategory = string.IsNullOrEmpty(p.ListCategory)
                        ? CategoryFromKind(p.Kind)
                        : p.ListCategory,
                    DeviceModel = p.DeviceModel ?? "",
                    Kind = p.Kind,
                    PreferredHinge = p.PreferredHinge,
                    PreferSeparating = p.PreferSeparating,
                    InnerLandscapeNative = p.InnerLandscapeNative,
                    BasedOn = p.BasedOn ?? "",
                    Notes = p.Notes ?? "",
                    VerifiedDate = p.VerifiedDate ?? "",
                    VerifiedStatus = string.IsNullOrEmpty(p.VerifiedStatus) ? "approx" : p.VerifiedStatus,
                    DeviceFile = p.DeviceFile ?? ""
                };

                if (p.Screens != null)
                {
                    foreach (var s in p.Screens)
                    {
                        e.Screens.Add(new EditableScreen
                        {
                            Name = s.Name ?? "",
                            PortraitWidth = s.PortraitWidth,
                            PortraitHeight = s.PortraitHeight,
                            LandscapeWidth = s.LandscapeWidth > 0 ? s.LandscapeWidth : s.PortraitHeight,
                            LandscapeHeight = s.LandscapeHeight > 0 ? s.LandscapeHeight : s.PortraitWidth,
                            CoverOrOuter = s.CoverOrOuter
                        });
                    }
                }

                if (p.Overlays != null)
                {
                    foreach (var o in p.Overlays)
                    {
                        e.Overlays.Add(new EditableOverlay
                        {
                            Screen = o.Screen ?? "",
                            Path = o.Path ?? ""
                        });
                    }
                }

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

            public void NormalizeScreenPairs()
            {
                foreach (var s in Screens)
                    s.NormalizePairs();
            }

            public string Validate(out string error)
            {
                error = null;
                if (string.IsNullOrWhiteSpace(Id))
                {
                    error = "Id is required (kebab-case, e.g. my-fold-like).";
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

                if (Screens.Count == 0)
                {
                    error = "Add at least one screen.";
                    return error;
                }

                foreach (var s in Screens)
                {
                    if (string.IsNullOrWhiteSpace(s.Name))
                    {
                        error = "Every screen needs a name.";
                        return error;
                    }

                    if (s.PortraitWidth < 8 || s.PortraitHeight < 8)
                    {
                        error = $"Screen '{s.Name}' portrait size is too small.";
                        return error;
                    }
                }

                return null;
            }
        }

        public sealed class EditableScreen
        {
            public string Name = "Main";
            public int PortraitWidth = 1080;
            public int PortraitHeight = 2316;
            public int LandscapeWidth = 2316;
            public int LandscapeHeight = 1080;
            public bool CoverOrOuter;

            public void NormalizePairs()
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
        }

        public sealed class EditableOverlay
        {
            public string Screen = "";
            public string Path = "";
        }

        public sealed class EditableSource
        {
            public string Title = "";
            public string Url = "";
            public string Quote = "";
        }

        #endregion

        #region Catalog

        public static bool SaveProfile(EditableProfile edit, bool syncDeviceFile, out string error)
        {
            error = null;
            if (edit == null)
            {
                error = "No profile.";
                return false;
            }

            edit.NormalizeScreenPairs();
            if (edit.Validate(out error) != null)
                return false;

            string root = GetPackageRoot();
            if (!ValidateCatalogRelativePath(root, edit.DeviceFile, ".device", out error))
                return false;
            foreach (var overlay in edit.Overlays)
            {
                if (!ValidateCatalogRelativePath(root, overlay.Path, null, out error))
                    return false;
            }

            // Keep list category aligned with kind when kind changes.
            if (edit.Kind == FoldableDeviceKind.FlipablePhone)
                edit.ListCategory = "Flip";
            else if (edit.Kind == FoldableDeviceKind.DualScreen)
                edit.ListCategory = "Dual";
            else if (string.IsNullOrEmpty(edit.ListCategory) ||
                     edit.ListCategory.Equals("Flip", StringComparison.OrdinalIgnoreCase) ||
                     edit.ListCategory.Equals("Dual", StringComparison.OrdinalIgnoreCase))
                edit.ListCategory = "Fold";

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

            // Id rename: do not leave the previous Profiles/*.json behind.
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

            var dto = ToDto(edit);
            string json = JsonUtility.ToJson(dto, true);
            File.WriteAllText(path, json, new UTF8Encoding(false));

            if (syncDeviceFile && !string.IsNullOrEmpty(edit.DeviceFile))
            {
                if (!SyncDeviceFile(edit, out string syncErr))
                    Debug.LogWarning("[FoldAndFlip] Profile saved but .device sync failed: " + syncErr);
            }

            AssetDatabase.Refresh();
            Reload();
            edit.SourceId = edit.Id;
            return true;
        }

        public static bool DeleteProfile(string id, bool deleteSkins, out string error)
        {
            error = null;
            var profile = FindById(id);
            if (profile == null)
            {
                error = "Profile not found: " + id;
                return false;
            }

            if (Devices.Length <= 1)
            {
                error = "Cannot delete the last device profile.";
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

            if (deleteSkins)
            {
                DeleteSkinFiles(profile);
            }

            AssetDatabase.Refresh();
            Reload();
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
            edit.SourceId = newId; // new file — do not delete the source profile on save
            edit.ShortName = string.IsNullOrWhiteSpace(newShortName)
                ? source.ShortName + " Copy"
                : newShortName;
            edit.DeviceModel = "gamedev " + newId;
            edit.BasedOn = string.IsNullOrEmpty(edit.BasedOn)
                ? "Copy of " + source.ShortName
                : edit.BasedOn;
            if (edit.Validate(out error) != null)
                return false;

            string root = GetPackageRoot();
            if (!string.IsNullOrEmpty(source.DeviceFile))
            {
                if (!ResponsiveUiEditorPath.CombineContained(root, source.DeviceFile, out string srcDevice))
                {
                    error = "Source .device path resolves outside the package root.";
                    return false;
                }
                string newDeviceRel = BuildUniqueRelativePath(
                    root,
                    "Editor/Devices",
                    SanitizeFileStem(edit.Id, "device"),
                    ".device");
                if (!ResponsiveUiEditorPath.CombineContained(root, newDeviceRel, out string dstDevice))
                {
                    error = "Duplicate .device path resolves outside the package root.";
                    return false;
                }
                if (File.Exists(srcDevice))
                {
                    CopyFileWithNewMeta(srcDevice, dstDevice);
                    edit.DeviceFile = ResponsiveUiEditorPath.ToAssetSeparators(newDeviceRel);
                    PatchDeviceFriendlyName(dstDevice, edit.FriendlyName);
                }
            }

            for (int i = 0; i < edit.Overlays.Count; i++)
            {
                var ov = edit.Overlays[i];
                if (string.IsNullOrEmpty(ov.Path))
                    continue;
                if (!ResponsiveUiEditorPath.CombineContained(root, ov.Path, out string srcOv))
                {
                    error = "Source overlay path resolves outside the package root.";
                    return false;
                }
                string fileName = Path.GetFileName(ov.Path);
                string newFile = SanitizeOverlayFileName(edit.ShortName, ov.Screen) + Path.GetExtension(fileName);
                string newRel = BuildUniqueRelativePath(
                    root,
                    "Editor/Devices/DeviceOverlays",
                    Path.GetFileNameWithoutExtension(newFile),
                    Path.GetExtension(newFile));
                if (!ResponsiveUiEditorPath.CombineContained(root, newRel, out string dstOv))
                {
                    error = "Duplicate overlay path resolves outside the package root.";
                    return false;
                }
                if (File.Exists(srcOv))
                {
                    CopyFileWithNewMeta(srcOv, dstOv);
                    ov.Path = ResponsiveUiEditorPath.ToAssetSeparators(newRel);
                }
            }

            return SaveProfile(edit, syncDeviceFile: true, out error);
        }

        public static EditableProfile CreateBlank(FoldableDeviceKind kind)
        {
            string category = CategoryFromKind(kind);
            string id = kind == FoldableDeviceKind.FlipablePhone
                ? "custom-flip-like"
                : kind == FoldableDeviceKind.DualScreen
                    ? "custom-dual-like"
                    : "custom-fold-like";

            // Unique id if taken.
            string baseId = id;
            int n = 2;
            while (FindById(id) != null)
            {
                id = baseId + "-" + n;
                n++;
            }

            string shortName = kind == FoldableDeviceKind.FlipablePhone
                ? "Custom Flip-like"
                : kind == FoldableDeviceKind.DualScreen
                    ? "Custom Dual-like"
                    : "Custom Fold-like";

            var edit = new EditableProfile
            {
                Id = id,
                SourceId = "",
                ShortName = shortName,
                ListCategory = category,
                DeviceModel = "gamedev " + id,
                Kind = kind,
                PreferredHinge = FoldableOrientation.Horizontal,
                PreferSeparating = kind == FoldableDeviceKind.DualScreen,
                BasedOn = "Custom",
                Notes = "Created in Device Catalog.",
                VerifiedDate = DateTime.UtcNow.ToString("yyyy-MM-dd"),
                VerifiedStatus = "approx",
                DeviceFile = ""
            };

            if (kind == FoldableDeviceKind.FlipablePhone)
            {
                edit.Screens.Add(new EditableScreen
                {
                    Name = "Cover", PortraitWidth = 720, PortraitHeight = 748, CoverOrOuter = true
                });
                edit.Screens.Add(new EditableScreen
                {
                    Name = "Main", PortraitWidth = 1080, PortraitHeight = 2640, CoverOrOuter = false
                });
            }
            else if (kind == FoldableDeviceKind.DualScreen)
            {
                edit.Screens.Add(new EditableScreen
                {
                    Name = "Single", PortraitWidth = 675, PortraitHeight = 1800, CoverOrOuter = true
                });
                edit.Screens.Add(new EditableScreen
                {
                    Name = "Spanned", PortraitWidth = 1350, PortraitHeight = 1800, CoverOrOuter = false
                });
            }
            else
            {
                edit.Screens.Add(new EditableScreen
                {
                    Name = "Cover", PortraitWidth = 1080, PortraitHeight = 2424, CoverOrOuter = true
                });
                edit.Screens.Add(new EditableScreen
                {
                    Name = "Inner", PortraitWidth = 1812, PortraitHeight = 2176, CoverOrOuter = false
                });
            }

            edit.NormalizeScreenPairs();
            return edit;
        }

        public static bool SelectInSimulator(string friendlyName, int screenIndex, out string error)
        {
            error = null;
            if (string.IsNullOrEmpty(friendlyName))
            {
                error = "No device selected.";
                return false;
            }

            bool ok = SimulatorDeviceBridge.SelectToolkitDevice(friendlyName, Mathf.Max(0, screenIndex));
            FoldableEditorSessionWatcher.RefreshNow(forceRebind: true);
            if (!ok)
            {
                error =
                    "Could not select device in Device Simulator. Open Simulator view and ensure the .device skin is imported.";
                return false;
            }

            return true;
        }

        static DeviceProfileDto ToDto(EditableProfile edit)
        {
            var screens = new ScreenDto[edit.Screens.Count];
            for (int i = 0; i < edit.Screens.Count; i++)
            {
                var s = edit.Screens[i];
                s.NormalizePairs();
                screens[i] = new ScreenDto
                {
                    name = s.Name,
                    width = s.PortraitWidth,
                    height = s.PortraitHeight,
                    portraitWidth = s.PortraitWidth,
                    portraitHeight = s.PortraitHeight,
                    landscapeWidth = s.LandscapeWidth,
                    landscapeHeight = s.LandscapeHeight,
                    coverOrOuter = s.CoverOrOuter
                };
            }

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

            var overlays = new OverlayDto[edit.Overlays.Count];
            for (int i = 0; i < edit.Overlays.Count; i++)
            {
                overlays[i] = new OverlayDto
                {
                    screen = edit.Overlays[i].Screen,
                    path = edit.Overlays[i].Path
                };
            }

            return new DeviceProfileDto
            {
                id = edit.Id,
                shortName = edit.ShortName,
                listCategory = edit.ListCategory,
                deviceModel = edit.DeviceModel,
                kind = KindToJson(edit.Kind),
                preferredHinge = edit.PreferredHinge == FoldableOrientation.Vertical ? "Vertical" : "Horizontal",
                preferSeparating = edit.PreferSeparating,
                innerLandscapeNative = IsLandscapeNativeInner(edit.Kind, edit.InnerLandscapeNative),
                basedOn = edit.BasedOn,
                notes = edit.Notes,
                verifiedDate = edit.VerifiedDate,
                verifiedStatus = edit.VerifiedStatus,
                sources = sources,
                skins = new SkinsDto
                {
                    deviceFile = edit.DeviceFile,
                    overlays = overlays
                },
                screens = screens
            };
        }

        #endregion

        #region Skins / overlays

        static bool SyncDeviceFile(EditableProfile edit, out string error)
        {
            error = null;
            string root = GetPackageRoot();
            if (!ResponsiveUiEditorPath.CombineContained(root, edit.DeviceFile, out string abs))
            {
                error = ".device path resolves outside the package root.";
                return false;
            }
            if (!File.Exists(abs))
            {
                error = "Missing .device: " + edit.DeviceFile;
                return false;
            }

            // Surgical text updates — avoid JsonUtility round-trip (drops unknown fields).
            string json = File.ReadAllText(abs);
            json = PatchJsonStringField(json, "friendlyName", edit.FriendlyName);

            for (int i = 0; i < edit.Screens.Count; i++)
            {
                var src = edit.Screens[i];
                src.NormalizePairs();
                if (!PatchScreenBlock(ref json, src.Name, src.PortraitWidth, src.PortraitHeight, out error))
                {
                    // Fall back to index-ordered patch when presentation name is missing/mismatched.
                    if (!PatchScreenBlockByIndex(ref json, i, src.PortraitWidth, src.PortraitHeight, out error))
                        return false;
                }
            }

            File.WriteAllText(abs, json, new UTF8Encoding(false));
            return true;
        }

        static string PatchJsonStringField(string json, string field, string value)
        {
            string escaped = EscapeJson(value);
            string pattern = "\"" + field + "\"\\s*:\\s*\"(?:\\\\.|[^\"\\\\])*\"";
            string replacement = "\"" + field + "\": \"" + escaped + "\"";
            var replaced = Regex.Replace(json, pattern, replacement, RegexOptions.CultureInvariant);
            return replaced;
        }

        static string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "";

            var sb = new StringBuilder(value.Length + 8);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '"': sb.Append("\\\""); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20)
                            sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else
                            sb.Append(c);
                        break;
                }
            }

            return sb.ToString();
        }

        static bool PatchScreenBlock(
            ref string json,
            string screenName,
            int width,
            int height,
            out string error)
        {
            error = null;
            if (string.IsNullOrEmpty(screenName))
            {
                error = "Screen name empty.";
                return false;
            }

            // Locate presentation name, then expand to enclosing screen object braces.
            var nameMatch = Regex.Match(
                json,
                "\"name\"\\s*:\\s*\"" + Regex.Escape(screenName) + "\"",
                RegexOptions.CultureInvariant);
            if (!nameMatch.Success)
            {
                error = "Screen '" + screenName + "' not found in .device.";
                return false;
            }

            if (!GetEnclosingObject(json, nameMatch.Index, out int start, out int end))
            {
                error = "Could not parse screen object for '" + screenName + "'.";
                return false;
            }

            // presentation is nested — enclosing once is presentation; enclose again for screen.
            if (!GetEnclosingObject(json, start - 1, out int screenStart, out int screenEnd))
            {
                error = "Could not parse parent screen for '" + screenName + "'.";
                return false;
            }

            string block = json.Substring(screenStart, screenEnd - screenStart + 1);
            string patched = PatchScreenSizeAndOrientations(block, width, height);
            json = json.Substring(0, screenStart) + patched + json.Substring(screenEnd + 1);
            return true;
        }

        static bool PatchScreenBlockByIndex(
            ref string json,
            int screenIndex,
            int width,
            int height,
            out string error)
        {
            error = null;
            int screensKey = json.IndexOf("\"screens\"", StringComparison.Ordinal);
            if (screensKey < 0)
            {
                error = ".device has no screens array.";
                return false;
            }

            int arrayStart = json.IndexOf('[', screensKey);
            if (arrayStart < 0)
            {
                error = ".device screens array malformed.";
                return false;
            }

            if (!GetArrayElementObject(json, arrayStart, screenIndex, out int start, out int end))
            {
                error = "Screen index " + screenIndex + " not found in .device.";
                return false;
            }

            string block = json.Substring(start, end - start + 1);
            string patched = PatchScreenSizeAndOrientations(block, width, height);
            json = json.Substring(0, start) + patched + json.Substring(end + 1);
            return true;
        }

        static string PatchScreenSizeAndOrientations(string screenBlock, int width, int height)
        {
            // Same size: keep the authored safe areas (iOS insets, zero-nav Flip covers).
            // The rebuild below is the Android navigation-bar template.
            if (ScreenBlockHasSize(screenBlock, width, height))
                return screenBlock;

            int nav = 120;
            var navMatch = Regex.Match(screenBlock, "\"navigationBarHeight\"\\s*:\\s*(\\d+)");
            if (navMatch.Success)
                int.TryParse(navMatch.Groups[1].Value, out nav);
            if (nav <= 0)
                nav = 120;

            // .device layouts vary: width/height may be before and/or after orientations.
            int orientKey = screenBlock.IndexOf("\"orientations\"", StringComparison.Ordinal);
            string head = orientKey >= 0 ? screenBlock.Substring(0, orientKey) : screenBlock;
            string tail = orientKey >= 0 ? screenBlock.Substring(orientKey) : "";

            head = PatchFirstIntField(head, "width", width);
            head = PatchFirstIntField(head, "height", height);

            string orientationsJson = BuildOrientationsJson(width, height, nav);
            if (orientKey >= 0)
            {
                var orientMatch = Regex.Match(tail, "\"orientations\"\\s*:\\s*\\[");
                if (orientMatch.Success)
                {
                    int absStart = orientMatch.Index + orientMatch.Length - 1; // '['
                    if (GetMatchingBracket(tail, absStart, out int absEnd))
                    {
                        string after = tail.Substring(absEnd + 1);
                        after = PatchFirstIntField(after, "width", width);
                        after = PatchFirstIntField(after, "height", height);
                        tail = tail.Substring(0, orientMatch.Index) +
                               "\"orientations\": " + orientationsJson +
                               after;
                    }
                }
            }

            return head + tail;
        }

        /// <summary>True when the screen object's own width / height (outside orientations) match.</summary>
        static bool ScreenBlockHasSize(string screenBlock, int width, int height)
        {
            string own = screenBlock;
            var orientMatch = Regex.Match(screenBlock, "\"orientations\"\\s*:\\s*\\[");
            if (orientMatch.Success)
            {
                int open = orientMatch.Index + orientMatch.Length - 1; // '['
                if (!GetMatchingBracket(screenBlock, open, out int close))
                    return false;
                own = screenBlock.Substring(0, orientMatch.Index) + screenBlock.Substring(close + 1);
            }

            var widthMatch = Regex.Match(own, "\"width\"\\s*:\\s*(\\d+)");
            var heightMatch = Regex.Match(own, "\"height\"\\s*:\\s*(\\d+)");
            return widthMatch.Success &&
                   heightMatch.Success &&
                   int.TryParse(widthMatch.Groups[1].Value, out int w) &&
                   int.TryParse(heightMatch.Groups[1].Value, out int h) &&
                   w == width &&
                   h == height;
        }

        static string PatchFirstIntField(string json, string field, int value)
        {
            return new Regex("\"" + field + "\"\\s*:\\s*\\d+")
                .Replace(json, "\"" + field + "\": " + value, 1);
        }

        static string BuildOrientationsJson(int w, int h, int nav)
        {
            int safeH = Mathf.Max(1, h - nav);
            int safeWLand = Mathf.Max(1, h - nav);
            return
                "[\n" +
                "  { \"orientation\": 1, \"safeArea\": { \"serializedVersion\": \"2\", \"x\": 0, \"y\": 0, \"width\": " + w + ", \"height\": " + safeH + " }, \"cutouts\": [] },\n" +
                "  { \"orientation\": 2, \"safeArea\": { \"serializedVersion\": \"2\", \"x\": 0, \"y\": " + nav + ", \"width\": " + w + ", \"height\": " + safeH + " }, \"cutouts\": [] },\n" +
                "  { \"orientation\": 3, \"safeArea\": { \"serializedVersion\": \"2\", \"x\": " + nav + ", \"y\": 0, \"width\": " + safeWLand + ", \"height\": " + w + " }, \"cutouts\": [] },\n" +
                "  { \"orientation\": 4, \"safeArea\": { \"serializedVersion\": \"2\", \"x\": 0, \"y\": 0, \"width\": " + h + ", \"height\": " + w + " }, \"cutouts\": [] }\n" +
                "]";
        }

        static bool GetEnclosingObject(string json, int innerIndex, out int start, out int end)
        {
            start = -1;
            end = -1;
            int depth = 0;
            for (int i = innerIndex; i >= 0; i--)
            {
                char c = json[i];
                if (c == '}')
                    depth++;
                else if (c == '{')
                {
                    if (depth == 0)
                    {
                        start = i;
                        break;
                    }

                    depth--;
                }
            }

            if (start < 0)
                return false;
            return GetMatchingBracket(json, start, out end);
        }

        static bool GetArrayElementObject(string json, int arrayStart, int index, out int start, out int end)
        {
            start = -1;
            end = -1;
            int depth = 0;
            int element = -1;
            for (int i = arrayStart; i < json.Length; i++)
            {
                char c = json[i];
                if (c == '[')
                {
                    depth++;
                }
                else if (c == ']')
                {
                    depth--;
                    if (depth == 0)
                        return false;
                }
                else if (c == '{' && depth == 1)
                {
                    element++;
                    if (element == index)
                    {
                        start = i;
                        return GetMatchingBracket(json, start, out end);
                    }
                }
            }

            return false;
        }

        static bool GetMatchingBracket(string json, int openIndex, out int closeIndex)
        {
            closeIndex = -1;
            if (openIndex < 0 || openIndex >= json.Length)
                return false;
            char open = json[openIndex];
            char close = open == '{' ? '}' : open == '[' ? ']' : '\0';
            if (close == '\0')
                return false;

            int depth = 0;
            bool inString = false;
            bool escape = false;
            for (int i = openIndex; i < json.Length; i++)
            {
                char c = json[i];
                if (inString)
                {
                    if (escape)
                        escape = false;
                    else if (c == '\\')
                        escape = true;
                    else if (c == '"')
                        inString = false;
                    continue;
                }

                if (c == '"')
                {
                    inString = true;
                    continue;
                }

                if (c == open)
                    depth++;
                else if (c == close)
                {
                    depth--;
                    if (depth == 0)
                    {
                        closeIndex = i;
                        return true;
                    }
                }
            }

            return false;
        }

        static bool PatchDeviceFriendlyName(string deviceAbs, string friendly)
        {
            try
            {
                string json = File.ReadAllText(deviceAbs);
                string next = PatchJsonStringField(json, "friendlyName", friendly);
                if (next == json)
                    return false;
                File.WriteAllText(deviceAbs, next, new UTF8Encoding(false));
                return true;
            }
            catch
            {
                return false;
            }
        }

        static void DeleteSkinFiles(DeviceProfile profile)
        {
            string root = GetPackageRoot();
            if (!string.IsNullOrEmpty(profile.DeviceFile))
            {
                if (ResponsiveUiEditorPath.CombineContained(root, profile.DeviceFile, out string abs))
                    DeleteAssetOrFile(root, abs);
                else
                    Debug.LogWarning("[FoldAndFlip] Refused skin delete outside package root: " + profile.DeviceFile);
            }

            if (profile.Overlays == null)
                return;

            foreach (var ov in profile.Overlays)
            {
                if (string.IsNullOrEmpty(ov.Path))
                    continue;
                if (ResponsiveUiEditorPath.CombineContained(root, ov.Path, out string abs))
                    DeleteAssetOrFile(root, abs);
                else
                    Debug.LogWarning("[FoldAndFlip] Refused overlay delete outside package root: " + ov.Path);
            }
        }

        #endregion

        #region Helpers

        static void DeleteAssetOrFile(string abs)
        {
            DeleteAssetOrFile(GetPackageRoot(), abs);
        }

        static void DeleteAssetOrFile(string root, string abs)
        {
            if (!ResponsiveUiEditorPath.IsContained(root, abs))
            {
                Debug.LogWarning("[FoldAndFlip] Refused delete outside package root: " + abs);
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

        static void CopyFileWithNewMeta(string src, string dst)
        {
            string root = GetPackageRoot();
            if (!ResponsiveUiEditorPath.IsContained(root, src) || !ResponsiveUiEditorPath.IsContained(root, dst))
                throw new InvalidOperationException("Catalog copy must stay inside the package root.");
            Directory.CreateDirectory(Path.GetDirectoryName(dst) ?? ".");
            File.Copy(src, dst, true);
            string srcMeta = src + ".meta";
            string dstMeta = dst + ".meta";
            if (!File.Exists(srcMeta))
                return;

            string meta = File.ReadAllText(srcMeta);
            string guid = Guid.NewGuid().ToString("N");
            meta = Regex.Replace(meta, @"guid:\s*[a-f0-9]+", "guid: " + guid);
            File.WriteAllText(dstMeta, meta, new UTF8Encoding(false));
        }

        static string SanitizeOverlayFileName(string shortName, string screen)
        {
            string baseName = SanitizeFileStem(shortName, "Device");
            if (baseName.EndsWith("like", StringComparison.OrdinalIgnoreCase))
                baseName = baseName.Substring(0, baseName.Length - 4) + "Like";
            string pane = SanitizeFileStem(screen, "Main");
            return baseName + "_" + pane + "_Overlay";
        }

        static string SanitizeFileStem(string value, string fallback)
        {
            string stem = string.IsNullOrWhiteSpace(value) ? fallback : value;
            stem = stem.Replace("+", "Plus");
            foreach (char invalid in Path.GetInvalidFileNameChars())
                stem = stem.Replace(invalid.ToString(), "");
            stem = Regex.Replace(stem, @"[^A-Za-z0-9._-]+", "");
            stem = stem.Trim('.', ' ', '-', '_');
            return string.IsNullOrEmpty(stem) ? fallback : stem;
        }

        static string BuildUniqueRelativePath(
            string root,
            string directory,
            string stem,
            string extension)
        {
            stem = SanitizeFileStem(stem, "File");
            extension = string.IsNullOrEmpty(extension) ? "" : extension;
            string relative = directory + "/" + stem + extension;
            int suffix = 2;
            while (ResponsiveUiEditorPath.CombineContained(root, relative, out string absolute) &&
                   File.Exists(absolute))
            {
                relative = directory + "/" + stem + "-" + suffix + extension;
                suffix++;
            }
            return ResponsiveUiEditorPath.ToAssetSeparators(relative);
        }

        static bool ValidateCatalogRelativePath(
            string root,
            string relative,
            string requiredExtension,
            out string error)
        {
            error = null;
            if (string.IsNullOrEmpty(relative))
                return true;
            if (!ResponsiveUiEditorPath.CombineContained(root, relative, out _))
            {
                error = "Catalog path must be relative and remain inside the package root: " + relative;
                return false;
            }
            if (!string.IsNullOrEmpty(requiredExtension) &&
                !string.Equals(Path.GetExtension(relative), requiredExtension, StringComparison.OrdinalIgnoreCase))
            {
                error = "Catalog path must use " + requiredExtension + ": " + relative;
                return false;
            }
            return true;
        }

        #endregion
    }
}
