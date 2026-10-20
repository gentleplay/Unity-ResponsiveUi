using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using FDPlus.ResponsiveUI.Shared.Editor;
using UnityEditor;
using UnityEngine;

namespace FDPlus.PhonePad.Editor
{
    /// <summary>
    /// Enables / disables Unity Device Simulator skins.
    /// Enabled = <c>.device</c> on disk (imported). Disabled = file deleted
    /// (regenerated on next enable). Do not keep <c>.device.off</c> with a
    /// Device ScriptedImporter meta — Unity still lists those as empty devices.
    /// </summary>
    public static partial class PhonePadDeviceCatalog
    {
        #region Constants

        const string PhoneSimulatorPrefix = ResponsiveUiSimulatorName.PhoneListPrefix;
        const string PadSimulatorPrefix = ResponsiveUiSimulatorName.PadListPrefix;
        /// <summary>
        /// ASCII SOH — sorts before "Apple…" in Device Simulator, does not render in the label.
        /// </summary>
        const string DesignRefSortPrefix = "\u0001";
        const string DisabledDeviceSuffix = ".off";
        public const string DesignReferenceMarker = ResponsiveUiSimulatorName.DesignReferenceMarker;

        #endregion

        #region Simulator

        public static bool IsSimulatorSkinActive(string relativeDeviceFile)
        {
            if (string.IsNullOrEmpty(relativeDeviceFile))
                return false;
            if (!ResponsiveUiEditorPath.CombineContained(GetPackageRoot(), relativeDeviceFile, out string abs))
                return false;
            return File.Exists(abs);
        }

        public static string ResolveDeviceFileRelative(DeviceProfile profile)
        {
            if (profile == null)
                return "";
            if (!string.IsNullOrEmpty(profile.DeviceFile))
                return profile.DeviceFile;
            return DefaultDeviceFileRelative(profile.Id);
        }

        static string DefaultDeviceFileRelative(string id) =>
            ResponsiveUiEditorPath.ToAssetSeparators(
                "Editor/Devices/" + SanitizeDeviceFileStem(id) + ".device");

        /// <summary>
        /// Enable or disable Unity Device Simulator skins for every catalog profile.
        /// One AssetDatabase refresh / reload at the end.
        /// </summary>
        public static bool SetAllSimulatorEnabled(bool enabled, out string error, out int changedCount)
        {
            error = null;
            changedCount = 0;
            var devices = Devices;
            if (devices == null || devices.Length == 0)
            {
                error = "No device profiles loaded.";
                return false;
            }

            var ids = new string[devices.Length];
            for (int i = 0; i < devices.Length; i++)
                ids[i] = devices[i].Id;

            bool ok = true;
            try
            {
                AssetDatabase.StartAssetEditing();
                for (int i = 0; i < ids.Length; i++)
                {
                    string id = ids[i];
                    // Design Canvas references always stay visible in Device Simulator.
                    if (!enabled && IsDesignCanvasReference(id))
                        continue;

                    EditorUtility.DisplayProgressBar(
                        enabled ? "Enable All in Simulator" : "Disable All in Simulator",
                        id,
                        (i + 1f) / ids.Length);

                    var before = FindById(id);
                    bool wasOn = before != null && before.SimulatorEnabled;
                    if (!SetSimulatorEnabledCore(id, enabled, out error))
                    {
                        ok = false;
                        break;
                    }

                    var after = FindById(id);
                    bool nowOn = after != null && after.SimulatorEnabled;
                    if (wasOn != nowOn)
                        changedCount++;
                }

                if (!enabled)
                    EnsureDesignReferenceSkinsEnabled(out _);

                // Remove any leftover .device.off ghosts that Unity still imports as empty devices.
                CleanupDisabledDeviceGhosts();
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                EditorUtility.ClearProgressBar();
                AssetDatabase.Refresh();
                Reload();
                // Import hooks mark the Simulator list dirty; deletes do not — force a reload.
                PhonePadSimulatorListUtil.ForceReloadDeviceLists();
            }

            return ok;
        }

        public static bool EnsureDesignReferenceSkinsEnabled(out string error) =>
            EnsureDesignReferenceSkinsEnabled(out error, out _);

        /// <summary>
        /// Ensures Design Canvas reference profiles have Simulator skins on disk.
        /// Does not rewrite friendly names — call <see cref="SyncAllActiveSimulatorFriendlyNames"/>
        /// when a Project Settings reference actually changes.
        /// <paramref name="filesChanged"/> is true when a skin file was created.
        /// </summary>
        public static bool EnsureDesignReferenceSkinsEnabled(out string error, out bool filesChanged)
        {
            error = null;
            filesChanged = false;
            var devices = Devices;
            if (devices == null)
                return true;

            for (int i = 0; i < devices.Length; i++)
            {
                if (!IsDesignCanvasReference(devices[i].Id))
                    continue;
                if (devices[i].SimulatorEnabled)
                    continue;
                if (!SetSimulatorEnabledCore(devices[i].Id, true, out error))
                    return false;
                filesChanged = true;
            }

            // Label rewrite is a separate pass (Project Settings reference change).
            // Running it here on every Ensure regenerated overlays / .device files.
            return true;
        }

        /// <summary>
        /// Rewrites friendlyName on every active <c>.device</c> from current catalog + reference flags.
        /// </summary>
        public static int SyncAllActiveSimulatorFriendlyNames()
        {
            int written = 0;
            var devices = Devices;
            if (devices == null)
                return 0;

            string root = GetPackageRoot();
            for (int i = 0; i < devices.Length; i++)
            {
                var profile = devices[i];
                if (profile == null)
                    continue;

                string relative = ResolveDeviceFileRelative(profile);
                if (!ResponsiveUiEditorPath.CombineContained(root, relative, out string abs) ||
                    !File.Exists(abs))
                    continue;

                if (WriteDeviceSkin(profile, abs))
                    written++;
            }

            return written;
        }

        public static bool AreAnySimulatorSkinsEnabled()
        {
            var devices = Devices;
            if (devices == null || devices.Length == 0)
                return false;
            for (int i = 0; i < devices.Length; i++)
            {
                if (devices[i].SimulatorEnabled)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Rewrites every active <c>.device</c> skin from its profile and the current overlays (photoreal
        /// import / clear, missing Device Simulator fields such as <c>systemInfo.operatingSystem</c>),
        /// and deletes leftover <c>.device.off</c> ghosts. Unchanged skins are not rewritten.
        /// </summary>
        public static int RepairActiveDeviceSkins()
        {
            int repaired = 0;
            var devices = Devices;
            if (devices != null)
            {
                string root = GetPackageRoot();
                for (int i = 0; i < devices.Length; i++)
                {
                    var profile = devices[i];
                    if (!profile.SimulatorEnabled)
                        continue;

                    string relative = ResolveDeviceFileRelative(profile);
                    if (!ResponsiveUiEditorPath.CombineContained(root, relative, out string abs) ||
                        !File.Exists(abs))
                        continue;

                    if (WriteDeviceSkin(profile, abs))
                        repaired++;
                }
            }

            repaired += CleanupDisabledDeviceGhosts();
            if (repaired > 0)
            {
                AssetDatabase.Refresh();
                PhonePadSimulatorListUtil.ForceReloadDeviceLists();
            }

            return repaired;
        }

        /// <summary>
        /// Deletes <c>*.device.off</c> (+ meta). Those files keep Device ScriptedImporter metas
        /// after a rename and show up as blank entries in the Simulator list.
        /// </summary>
        static int CleanupDisabledDeviceGhosts()
        {
            string dir = ResponsiveUiEditorPath.CombineRoot(GetPackageRoot(), "Editor/Devices");
            if (!Directory.Exists(dir))
                return 0;

            int removed = 0;
            foreach (string path in Directory.GetFiles(dir, "*.device.off", SearchOption.TopDirectoryOnly))
            {
                DeleteWithMeta(path);
                removed++;
            }

            return removed;
        }

        /// <summary>
        /// Per-profile skin toggle used by <see cref="SetAllSimulatorEnabled"/>.
        /// Does not refresh AssetDatabase or reload the catalog — caller owns that.
        /// </summary>
        static bool SetSimulatorEnabledCore(string id, bool enabled, out string error)
        {
            error = null;
            var profile = FindById(id);
            if (profile == null)
            {
                error = "Profile not found: " + id;
                return false;
            }

            string root = GetPackageRoot();
            string relative = ResolveDeviceFileRelative(profile);

            if (!ResponsiveUiEditorPath.CombineContained(root, relative, out string activeAbs))
            {
                error = "Device file path resolves outside the package root.";
                return false;
            }

            string disabledAbs = activeAbs + DisabledDeviceSuffix;
            bool activeExists = File.Exists(activeAbs);
            bool pathNeedsWrite = !string.Equals(profile.DeviceFile, relative, System.StringComparison.Ordinal);

            if (enabled)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(activeAbs) ?? root);
                // Drop any previous .device.off leftover from rename-based disables.
                if (File.Exists(disabledAbs))
                    DeleteWithMeta(disabledAbs);

                WriteDeviceSkin(profile, activeAbs);

                if (pathNeedsWrite && !WriteProfileDeviceFile(profile.Id, relative, out error))
                    return false;

                profile.DeviceFile = relative;
                profile.SimulatorEnabled = true;
                return true;
            }

            if (IsDesignCanvasReference(id))
            {
                error =
                    "Design Canvas reference devices stay enabled in Device Simulator. " +
                    "Change Edit → Project Settings → Responsive UI first.";
                return false;
            }

            // Disable: delete the .device (+ meta). Do NOT rename to .device.off —
            // Unity keeps importing those via ScriptedImporter as empty Simulator entries.
            if (activeExists)
                DeleteWithMeta(activeAbs);
            if (File.Exists(disabledAbs))
                DeleteWithMeta(disabledAbs);
            DeleteOverlayForProfile(profile);

            if (pathNeedsWrite && !WriteProfileDeviceFile(profile.Id, relative, out error))
                return false;

            profile.DeviceFile = relative;
            profile.SimulatorEnabled = false;
            return true;
        }

        static bool WriteProfileDeviceFile(string id, string relativeDeviceFile, out string error)
        {
            error = null;
            var live = FindById(id);
            if (live == null)
            {
                error = "Profile not found while updating device file.";
                return false;
            }

            var edit = EditableProfile.FromProfile(live);
            edit.DeviceFile = relativeDeviceFile;
            return SaveProfile(edit, syncDeviceFile: false, refreshAndReload: false, out error);
        }

        #endregion

        #region Skins / overlays

        static bool WriteDeviceSkin(DeviceProfile profile, string absolutePath)
        {
            int w = Mathf.Max(1, profile.PortraitWidth);
            int h = Mathf.Max(1, profile.PortraitHeight);
            int dpi = profile.Dpi > 0 ? Mathf.RoundToInt(profile.Dpi) : 400;
            int nav = Mathf.Clamp(Mathf.RoundToInt(h * 0.05f), 48, 140);
            string friendly = BuildSimulatorFriendlyName(profile);
            bool apple = IsAppleProfile(profile);
            bool phone = profile.Kind != PhonePadDeviceKind.Pad;
            int borderX;
            int borderY;
            int borderZ;
            int borderW;
            int corner;
            string overlayRel;

            if (PhonePadPhotorealFrameImport.GetPresentation(
                    profile,
                    out overlayRel,
                    out borderX,
                    out borderY,
                    out borderZ,
                    out borderW,
                    out corner))
            {
                // Local photoreal overlay already on disk.
            }
            else
            {
                borderX = borderY = borderZ = borderW = apple
                    ? (phone ? 52 : 36)
                    : (phone ? 18 : 28);
                corner = apple
                    ? (phone
                        ? Mathf.Clamp(Mathf.RoundToInt(Mathf.Min(w, h) * 0.12f), 72, 200)
                        : Mathf.Clamp(Mathf.RoundToInt(Mathf.Min(w, h) * 0.06f), 36, 120))
                    : (phone ? 36 : 18);
                overlayRel = EnsureBezelOverlay(profile, w, h, borderX, corner, apple, phone);
            }

            string os = BuildOperatingSystem(profile);
            string model = string.IsNullOrEmpty(profile.DeviceModel)
                ? (string.IsNullOrEmpty(profile.ShortName) ? profile.Id : profile.ShortName)
                : profile.DeviceModel;

            var sb = new StringBuilder(2048);
            sb.Append("{\n");
            sb.Append("    \"friendlyName\": \"").Append(EscapeJson(friendly)).Append("\",\n");
            sb.Append("    \"version\": 1,\n");
            sb.Append("    \"screens\": [\n");
            sb.Append("        {\n");
            sb.Append("            \"width\": ").Append(w).Append(",\n");
            sb.Append("            \"height\": ").Append(h).Append(",\n");
            sb.Append("            \"dpi\": ").Append(dpi).Append(",\n");
            sb.Append("            \"navigationBarHeight\": ").Append(nav).Append(",\n");
            sb.Append("            \"presentation\": {\n");
            sb.Append("                \"name\": \"Main\",\n");
            sb.Append("                \"borderSize\": { \"x\": ").Append(borderX)
                .Append(", \"y\": ").Append(borderY)
                .Append(", \"z\": ").Append(borderZ)
                .Append(", \"w\": ").Append(borderW).Append(" },\n");
            sb.Append("                \"cornerRadius\": ").Append(corner).Append(",\n");
            sb.Append("                \"overlayPath\": \"").Append(EscapeJson(overlayRel)).Append("\"\n");
            sb.Append("            },\n");
            sb.Append("            \"orientations\": ").Append(BuildOrientationsJson(w, h, nav)).Append("\n");
            sb.Append("        }\n");
            sb.Append("    ],\n");
            sb.Append("    \"systemInfo\": {\n");
            sb.Append("        \"deviceModel\": \"").Append(EscapeJson(model)).Append("\",\n");
            sb.Append("        \"deviceType\": 1,\n");
            sb.Append("        \"operatingSystem\": \"").Append(EscapeJson(os)).Append("\",\n");
            sb.Append("        \"operatingSystemFamily\": 0,\n");
            sb.Append("        \"supportsAccelerometer\": true,\n");
            sb.Append("        \"supportsAudio\": true,\n");
            sb.Append("        \"supportsGyroscope\": true,\n");
            sb.Append("        \"supportsLocationService\": true,\n");
            sb.Append("        \"supportsVibration\": true\n");
            sb.Append("    }\n");
            sb.Append("}\n");

            string next = sb.ToString();
            if (File.Exists(absolutePath) && File.ReadAllText(absolutePath) == next)
                return false;

            File.WriteAllText(absolutePath, next, new UTF8Encoding(false));
            return true;
        }

        /// <summary>
        /// Kit-generated bezel PNG (not OEM / Apple Design Resources / Unity Companion artwork).
        /// Apple phones get a Dynamic Island–style pill cut-in; other brands get a generic frame.
        /// </summary>
        static string EnsureBezelOverlay(
            DeviceProfile profile,
            int screenW,
            int screenH,
            int border,
            int radius,
            bool appleStyle,
            bool phoneStyle)
        {
            if (PhonePadPhotorealFrameImport.GetPresentation(
                    profile, out string existing, out _, out _, out _, out _, out _))
                return existing;

            string fileName = OverlayStem(profile) + "_Main_Overlay.png";
            string relative = "DeviceOverlays/" + fileName;
            string absDir = ResponsiveUiEditorPath.CombineRoot(GetPackageRoot(), "Editor/Devices/DeviceOverlays");
            Directory.CreateDirectory(absDir);
            string abs = Path.Combine(absDir, fileName);
            if (!File.Exists(abs))
                WriteBezelOverlayPng(abs, screenW, screenH, border, radius, appleStyle, phoneStyle);
            EnsureOverlayPngMeta(abs);
            return relative;
        }

        static bool IsAppleProfile(DeviceProfile profile)
        {
            if (profile == null)
                return false;
            if (!string.IsNullOrEmpty(profile.Brand) &&
                profile.Brand.Equals("Apple", System.StringComparison.OrdinalIgnoreCase))
                return true;
            string name = (profile.ShortName ?? "") + " " + (profile.Id ?? "");
            return name.IndexOf("iphone", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("ipad", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        static void WriteBezelOverlayPng(
            string absolutePath,
            int screenW,
            int screenH,
            int border,
            int radius,
            bool appleStyle,
            bool phoneStyle)
        {
            int ow = Mathf.Max(2, screenW + border * 2);
            int oh = Mathf.Max(2, screenH + border * 2);
            const int maxDim = 4096;
            if (ow > maxDim || oh > maxDim)
            {
                float scale = maxDim / (float)Mathf.Max(ow, oh);
                ow = Mathf.Max(2, Mathf.RoundToInt(ow * scale));
                oh = Mathf.Max(2, Mathf.RoundToInt(oh * scale));
                screenW = Mathf.Max(1, Mathf.RoundToInt(screenW * scale));
                screenH = Mathf.Max(1, Mathf.RoundToInt(screenH * scale));
                border = Mathf.Max(1, Mathf.RoundToInt(border * scale));
                radius = Mathf.Max(1, Mathf.RoundToInt(radius * scale));
            }

            var tex = new Texture2D(ow, oh, TextureFormat.RGBA32, false);
            var pixels = new Color32[ow * oh];
            var bezel = appleStyle
                ? new Color32(22, 22, 24, 255)
                : new Color32(28, 30, 34, 230);
            var clear = new Color32(0, 0, 0, 0);
            var rim = appleStyle
                ? new Color32(180, 180, 185, 140)
                : new Color32(255, 255, 255, 90);
            int holeRadius = Mathf.Max(1, Mathf.RoundToInt(radius * (appleStyle ? 0.85f : 0.75f)));

            // Dynamic Island approx (opaque over the screen hole).
            int islandW = 0, islandH = 0, islandX = 0, islandY = 0, islandR = 0;
            if (appleStyle && phoneStyle)
            {
                islandW = Mathf.Clamp(screenW / 3, 120, screenW / 2);
                islandH = Mathf.Clamp(screenH / 42, 28, 56);
                islandX = border + (screenW - islandW) / 2;
                islandY = border + Mathf.Max(14, screenH / 70);
                islandR = islandH / 2;
            }

            for (int y = 0; y < oh; y++)
            {
                for (int x = 0; x < ow; x++)
                {
                    bool inBezel = InsideRoundedRect(x, y, 0, 0, ow, oh, radius);
                    bool inHole = InsideRoundedRect(
                        x, y, border, border, screenW, screenH, holeRadius);
                    bool inIsland = islandW > 0 &&
                                    InsideRoundedRect(x, y, islandX, islandY, islandW, islandH, islandR);

                    if (inBezel && (!inHole || inIsland))
                        pixels[y * ow + x] = bezel;
                    else
                        pixels[y * ow + x] = clear;
                }
            }

            for (int y = 0; y < oh; y++)
            {
                for (int x = 0; x < ow; x++)
                {
                    if (!InsideRoundedRect(x, y, 0, 0, ow, oh, radius))
                        continue;
                    if (InsideRoundedRect(x, y, 1, 1, ow - 2, oh - 2, Mathf.Max(1, radius - 1)))
                        continue;
                    pixels[y * ow + x] = rim;
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            File.WriteAllBytes(absolutePath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        static bool InsideRoundedRect(
            int px, int py, int rx, int ry, int rw, int rh, int radius)
        {
            if (px < rx || py < ry || px >= rx + rw || py >= ry + rh)
                return false;

            int r = Mathf.Clamp(radius, 0, Mathf.Min(rw, rh) / 2);
            if (r <= 0)
                return true;

            int lx = px - rx;
            int ly = py - ry;
            if (lx >= r && lx < rw - r)
                return true;
            if (ly >= r && ly < rh - r)
                return true;

            int cx = lx < r ? r : rw - r - 1;
            int cy = ly < r ? r : rh - r - 1;
            int dx = lx - cx;
            int dy = ly - cy;
            return dx * dx + dy * dy <= r * r;
        }

        /// <summary>
        /// Texture importer meta for a Simulator overlay PNG (kit bezel or photoreal import): default
        /// texture, no mipmaps, no power-of-two scaling, alpha is transparency. Kept when present.
        /// </summary>
        internal static void EnsureOverlayPngMeta(string pngAbs)
        {
            string metaPath = pngAbs + ".meta";
            if (File.Exists(metaPath))
                return;

            string guid = System.Guid.NewGuid().ToString("N");
            var sb = new StringBuilder(512);
            sb.Append("fileFormatVersion: 2\n");
            sb.Append("guid: ").Append(guid).Append('\n');
            sb.Append("TextureImporter:\n");
            sb.Append("  internalIDToNameTable: []\n");
            sb.Append("  externalObjects: {}\n");
            sb.Append("  serializedVersion: 12\n");
            sb.Append("  mipmaps:\n");
            sb.Append("    mipMapMode: 0\n");
            sb.Append("    enableMipMap: 0\n");
            sb.Append("    sRGBTexture: 1\n");
            sb.Append("    linearTexture: 0\n");
            sb.Append("    fadeOut: 0\n");
            sb.Append("    borderMipMap: 0\n");
            sb.Append("    mipMapsPreserveCoverage: 0\n");
            sb.Append("    alphaTestReferenceValue: 0.5\n");
            sb.Append("    mipMapFadeDistanceStart: 1\n");
            sb.Append("    mipMapFadeDistanceEnd: 3\n");
            sb.Append("  bumpmap:\n");
            sb.Append("    convertToNormalMap: 0\n");
            sb.Append("    externalNormalMap: 0\n");
            sb.Append("    heightScale: 0.25\n");
            sb.Append("    normalMapFilter: 0\n");
            sb.Append("  isReadable: 0\n");
            sb.Append("  streamingMipmaps: 0\n");
            sb.Append("  streamingMipmapsPriority: 0\n");
            sb.Append("  vTOnly: 0\n");
            sb.Append("  ignoreMasterTextureLimit: 0\n");
            sb.Append("  grayScaleToAlpha: 0\n");
            sb.Append("  generateCubemap: 6\n");
            sb.Append("  cubemapConvolution: 0\n");
            sb.Append("  seamlessCubemap: 0\n");
            sb.Append("  textureFormat: 1\n");
            sb.Append("  maxTextureSize: 8192\n");
            sb.Append("  textureSettings:\n");
            sb.Append("    serializedVersion: 2\n");
            sb.Append("    filterMode: 1\n");
            sb.Append("    aniso: 1\n");
            sb.Append("    mipBias: 0\n");
            sb.Append("    wrapU: 1\n");
            sb.Append("    wrapV: 1\n");
            sb.Append("    wrapW: 1\n");
            sb.Append("  nPOTScale: 0\n");
            sb.Append("  lightmap: 0\n");
            sb.Append("  compressionQuality: 50\n");
            sb.Append("  spriteMode: 0\n");
            sb.Append("  spriteExtrude: 1\n");
            sb.Append("  spriteMeshType: 1\n");
            sb.Append("  alignment: 0\n");
            sb.Append("  spritePivot: {x: 0.5, y: 0.5}\n");
            sb.Append("  spritePixelsToUnits: 100\n");
            sb.Append("  spriteBorder: {x: 0, y: 0, z: 0, w: 0}\n");
            sb.Append("  spriteGenerateFallbackPhysicsShape: 0\n");
            sb.Append("  alphaUsage: 1\n");
            sb.Append("  alphaIsTransparency: 1\n");
            sb.Append("  spriteTessellationDetail: -1\n");
            sb.Append("  textureType: 0\n");
            sb.Append("  textureShape: 1\n");
            sb.Append("  singleChannelComponent: 0\n");
            sb.Append("  flipbookRows: 1\n");
            sb.Append("  flipbookColumns: 1\n");
            sb.Append("  maxTextureSizeSet: 0\n");
            sb.Append("  compressionQualitySet: 0\n");
            sb.Append("  textureFormatSet: 0\n");
            sb.Append("  ignorePngGamma: 0\n");
            sb.Append("  applyGammaDecoding: 0\n");
            sb.Append("  swizzle: 50462976\n");
            sb.Append("  platformSettings: []\n");
            sb.Append("  spriteSheet:\n");
            sb.Append("    serializedVersion: 2\n");
            sb.Append("    sprites: []\n");
            sb.Append("    outline: []\n");
            sb.Append("    physicsShape: []\n");
            sb.Append("    bones: []\n");
            sb.Append("    spriteID: \n");
            sb.Append("    internalID: 0\n");
            sb.Append("    vertices: []\n");
            sb.Append("    indices: \n");
            sb.Append("    edges: []\n");
            sb.Append("    weights: []\n");
            sb.Append("  spritePackingTag: \n");
            sb.Append("  pSDRemoveMatte: 0\n");
            sb.Append("  userData: \n");
            sb.Append("  assetBundleName: \n");
            sb.Append("  assetBundleVariant: \n");
            File.WriteAllText(metaPath, sb.ToString(), new UTF8Encoding(false));
        }

        static void DeleteOverlayForProfile(DeviceProfile profile)
        {
            if (profile == null)
                return;
            // Keep local photoreal imports; Clear Local Photoreal Frames removes those.
            if (PhonePadPhotorealFrameImport.HasPhotorealOverlay(profile))
                return;

            string abs = OverlayPathForProfile(profile, "_Main_Overlay.png");
            if (File.Exists(abs))
                DeleteWithMeta(abs);
        }

        /// <summary>Deletes every overlay of a removed profile: kit bezel or photoreal PNG and its borders sidecar.</summary>
        static void DeleteAllOverlaysForProfile(DeviceProfile profile)
        {
            foreach (string suffix in new[] { "_Main_Overlay.png", "_Main_Overlay.borders.json" })
            {
                string abs = OverlayPathForProfile(profile, suffix);
                if (File.Exists(abs))
                    DeleteWithMeta(abs);
            }
        }

        static string OverlayPathForProfile(DeviceProfile profile, string suffix) =>
            ResponsiveUiEditorPath.CombineRoot(
                GetPackageRoot(),
                "Editor/Devices/DeviceOverlays/" + OverlayStem(profile) + suffix);

        #endregion

        #region Helpers

        static string BuildOperatingSystem(DeviceProfile profile)
        {
            bool apple = !string.IsNullOrEmpty(profile.Brand) &&
                         profile.Brand.Equals("Apple", System.StringComparison.OrdinalIgnoreCase);
            if (apple)
                return "iOS 17.0";
            return "Android OS 14 / API-34";
        }

        static string BuildSimulatorFriendlyName(DeviceProfile profile)
        {
            string name = string.IsNullOrEmpty(profile.ShortName) ? "Device" : profile.ShortName;
            // Design Canvas references: plain ShortName, plus invisible sort-to-top prefix.
            if (IsDesignCanvasReference(profile.Id))
                return DesignRefSortPrefix + name;
            if (profile.Kind == PhonePadDeviceKind.Pad)
                return PadSimulatorPrefix + name;
            return PhoneSimulatorPrefix + name;
        }

        static string BuildOrientationsJson(int w, int h, int nav)
        {
            int safeH = Mathf.Max(1, h - nav);
            int safeWLand = Mathf.Max(1, h - nav);
            return
                "[\n" +
                "                { \"orientation\": 1, \"safeArea\": { \"serializedVersion\": \"2\", \"x\": 0, \"y\": 0, \"width\": " + w + ", \"height\": " + safeH + " }, \"cutouts\": [] },\n" +
                "                { \"orientation\": 2, \"safeArea\": { \"serializedVersion\": \"2\", \"x\": 0, \"y\": " + nav + ", \"width\": " + w + ", \"height\": " + safeH + " }, \"cutouts\": [] },\n" +
                "                { \"orientation\": 3, \"safeArea\": { \"serializedVersion\": \"2\", \"x\": " + nav + ", \"y\": 0, \"width\": " + safeWLand + ", \"height\": " + w + " }, \"cutouts\": [] },\n" +
                "                { \"orientation\": 4, \"safeArea\": { \"serializedVersion\": \"2\", \"x\": 0, \"y\": 0, \"width\": " + h + ", \"height\": " + w + " }, \"cutouts\": [] }\n" +
                "            ]";
        }

        internal static string EscapeJson(string value)
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

        /// <summary>File stem of a profile's overlays under <c>DeviceOverlays/</c> (kit bezel and photoreal).</summary>
        internal static string OverlayStem(DeviceProfile profile) =>
            SanitizeDeviceFileStem(string.IsNullOrEmpty(profile.Id) ? profile.ShortName : profile.Id);

        static string SanitizeDeviceFileStem(string value)
        {
            string stem = string.IsNullOrWhiteSpace(value) ? "device" : value;
            foreach (char invalid in Path.GetInvalidFileNameChars())
                stem = stem.Replace(invalid.ToString(), "");
            stem = Regex.Replace(stem, @"[^A-Za-z0-9._-]+", "-");
            stem = stem.Trim('.', ' ', '-', '_');
            return string.IsNullOrEmpty(stem) ? "device" : stem;
        }

        internal static void DeleteWithMeta(string abs)
        {
            string assetPath = ResponsiveUiEditorPath.ToAssetPath(abs);
            if (!string.IsNullOrEmpty(assetPath) && AssetDatabase.LoadMainAssetAtPath(assetPath) != null)
            {
                AssetDatabase.DeleteAsset(assetPath);
                return;
            }

            if (File.Exists(abs))
                File.Delete(abs);
            string meta = abs + ".meta";
            if (File.Exists(meta))
                File.Delete(meta);
        }

        #endregion
    }
}
