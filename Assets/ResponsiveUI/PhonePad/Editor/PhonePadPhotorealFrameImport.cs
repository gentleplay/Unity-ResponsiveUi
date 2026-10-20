using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using FDPlus.ResponsiveUI.Shared.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace FDPlus.PhonePad.Editor
{
    /// <summary>
    /// Downloads Device Frames Media PNGs (Editor/local only), converts them to
    /// Unity Device Simulator overlays, and stores them under DeviceOverlays/.
    /// These assets are excluded from the MIT .unitypackage export — check upstream
    /// licensing before redistributing the PNGs yourself.
    /// Source: https://github.com/jonnyjackson26/device-frames-media
    /// </summary>
    public static partial class PhonePadPhotorealFrameImport
    {
        #region Constants

        const string BordersSuffix = "_Main_Overlay.borders.json";
        const string OverlaySuffix = "_Main_Overlay.png";
        const string ProgressTitle = "Import Photoreal Frames";
        /// <summary>Seconds one frame download may take before it is aborted.</summary>
        const double DownloadTimeoutSeconds = 60;

        #endregion

        #region Types

        struct FrameSource
        {
            public readonly string ProfileId;
            public readonly string FrameUrl;
            public readonly int ScreenX;
            public readonly int ScreenY;
            public readonly int ScreenW;
            public readonly int ScreenH;
            public readonly int FrameW;
            public readonly int FrameH;
            public readonly string Label;

            public FrameSource(
                string profileId,
                string frameUrl,
                int screenX,
                int screenY,
                int screenW,
                int screenH,
                int frameW,
                int frameH,
                string label)
            {
                ProfileId = profileId;
                FrameUrl = frameUrl;
                ScreenX = screenX;
                ScreenY = screenY;
                ScreenW = screenW;
                ScreenH = screenH;
                FrameW = frameW;
                FrameH = frameH;
                Label = label;
            }
        }

        #endregion

        #region Photoreal import

        [MenuItem(ResponsiveUiEditorMenus.ImportPhotorealFrames, false,
            ResponsiveUiEditorMenus.PriorityDevices + 2 * ResponsiveUiEditorMenus.PriorityGroupGap + 3)]
        public static void ImportPhotorealFramesMenu()
        {
            if (!EditorUtility.DisplayDialog(
                    "Import Photoreal Frames (Local)",
                    "Download matched frames from Device Frames Media and write Unity Device Simulator overlays under PhonePad/Editor/Devices/DeviceOverlays/.\n\n" +
                    "Local / Editor use only — not included in the MIT package export. Review upstream licensing before redistributing PNGs.\n\n" +
                    "Continue?",
                    "Import",
                    "Cancel"))
                return;

            if (!ImportAll(out string message, out int imported, out int skipped, out int failed))
            {
                Debug.LogWarning("[Responsive UI] " + message);
                EditorUtility.DisplayDialog("Photoreal Frames", message, "OK");
                return;
            }

            PhonePadDeviceCatalog.Reload();
            PhonePadDeviceCatalog.RepairActiveDeviceSkins();
            PhonePadSimulatorListUtil.ForceReloadDeviceLists();

            string summary =
                $"Imported {imported} photoreal overlay(s). Skipped {skipped}. Failed {failed}.\n\n{message}";
            Debug.Log("[Responsive UI] " + summary.Replace('\n', ' '));
            EditorUtility.DisplayDialog("Photoreal Frames", summary, "OK");
        }

        [MenuItem(ResponsiveUiEditorMenus.ClearPhotorealFrames, false,
            ResponsiveUiEditorMenus.PriorityDevices + 2 * ResponsiveUiEditorMenus.PriorityGroupGap + 4)]
        public static void ClearPhotorealFramesMenu()
        {
            if (!EditorUtility.DisplayDialog(
                    "Clear Local Photoreal Frames",
                    "Delete imported photoreal overlays (*.borders.json + matching PNGs) under DeviceOverlays?\n\n" +
                    "Procedural bezels will be regenerated the next time skins are enabled.",
                    "Clear",
                    "Cancel"))
                return;

            int removed = ClearImportedOverlays();
            PhonePadDeviceCatalog.RepairActiveDeviceSkins();
            PhonePadSimulatorListUtil.ForceReloadDeviceLists();
            EditorUtility.DisplayDialog(
                "Photoreal Frames",
                $"Removed {removed} imported overlay set(s).",
                "OK");
        }

        public static bool ImportAll(out string message, out int imported, out int skipped, out int failed)
        {
            imported = 0;
            skipped = 0;
            failed = 0;
            var failNotes = new List<string>();

            string root = PhonePadDeviceCatalog.GetPackageRoot();
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
            {
                message = "PhonePad module root not found.";
                return false;
            }

            string overlayDir = ResponsiveUiEditorPath.CombineRoot(root, "Editor/Devices/DeviceOverlays");
            Directory.CreateDirectory(overlayDir);

            PhonePadDeviceCatalog.Reload();
            var sources = BuiltInSources;
            bool cancelled = false;
            try
            {
                for (int i = 0; i < sources.Length && !cancelled; i++)
                {
                    var src = sources[i];
                    float progress = (i + 1f) / sources.Length;
                    if (EditorUtility.DisplayCancelableProgressBar(ProgressTitle, src.ProfileId, progress))
                    {
                        cancelled = true;
                        break;
                    }

                    var profile = PhonePadDeviceCatalog.FindById(src.ProfileId);
                    if (profile == null)
                    {
                        skipped++;
                        continue;
                    }

                    string stem = PhonePadDeviceCatalog.OverlayStem(profile);
                    string pngAbs = Path.Combine(overlayDir, stem + OverlaySuffix);
                    string bordersAbs = Path.Combine(overlayDir, stem + BordersSuffix);

                    if (File.Exists(pngAbs) && File.Exists(bordersAbs) && !IsStale(bordersAbs, src))
                    {
                        skipped++;
                        continue;
                    }

                    if (!Download(src.FrameUrl, src.ProfileId, progress, out byte[] pngBytes, out string err, out cancelled))
                    {
                        if (cancelled)
                            break;
                        failed++;
                        failNotes.Add(src.ProfileId + ": " + err);
                        continue;
                    }

                    int targetW = Mathf.Max(1, profile.PortraitWidth);
                    int targetH = Mathf.Max(1, profile.PortraitHeight);
                    if (!ConvertFrameToOverlay(
                            pngBytes,
                            src,
                            targetW,
                            targetH,
                            pngAbs,
                            out int bl,
                            out int bt,
                            out int br,
                            out int bb,
                            out int corner,
                            out err))
                    {
                        failed++;
                        failNotes.Add(src.ProfileId + ": " + err);
                        continue;
                    }

                    WriteBordersSidecar(bordersAbs, bl, bt, br, bb, corner, src);
                    PhonePadDeviceCatalog.EnsureOverlayPngMeta(pngAbs);
                    imported++;
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                AssetDatabase.Refresh();
            }

            if (cancelled)
                failNotes.Insert(0, "Cancelled: the remaining frames were not imported.");
            message = failNotes.Count == 0
                ? "Done. Enabled Simulator skins now use the imported frames; skins enabled later pick them up too."
                : "Not all frames were imported:\n- " + string.Join("\n- ", failNotes);
            return (failed == 0 && !cancelled) || imported > 0;
        }

        public static bool GetPresentation(
            PhonePadDeviceCatalog.DeviceProfile profile,
            out string overlayRel,
            out int borderX,
            out int borderY,
            out int borderZ,
            out int borderW,
            out int cornerRadius)
        {
            overlayRel = null;
            borderX = borderY = borderZ = borderW = cornerRadius = 0;
            if (profile == null)
                return false;

            string root = PhonePadDeviceCatalog.GetPackageRoot();
            string stem = PhonePadDeviceCatalog.OverlayStem(profile);
            string pngAbs = ResponsiveUiEditorPath.CombineRoot(
                root, "Editor/Devices/DeviceOverlays/" + stem + OverlaySuffix);
            string bordersAbs = ResponsiveUiEditorPath.CombineRoot(
                root, "Editor/Devices/DeviceOverlays/" + stem + BordersSuffix);
            if (!File.Exists(pngAbs) || !File.Exists(bordersAbs))
                return false;

            if (!ReadBorders(bordersAbs, out borderX, out borderY, out borderZ, out borderW, out cornerRadius))
                return false;

            overlayRel = "DeviceOverlays/" + stem + OverlaySuffix;
            return true;
        }

        public static bool HasPhotorealOverlay(PhonePadDeviceCatalog.DeviceProfile profile)
        {
            return GetPresentation(profile, out _, out _, out _, out _, out _, out _);
        }

        static int ClearImportedOverlays()
        {
            string root = PhonePadDeviceCatalog.GetPackageRoot();
            string dir = ResponsiveUiEditorPath.CombineRoot(root, "Editor/Devices/DeviceOverlays");
            if (!Directory.Exists(dir))
                return 0;

            int removed = 0;
            foreach (string borders in Directory.GetFiles(dir, "*" + BordersSuffix))
            {
                string stem = Path.GetFileName(borders);
                int cut = stem.IndexOf(BordersSuffix, StringComparison.Ordinal);
                if (cut <= 0)
                    continue;
                stem = stem.Substring(0, cut);
                string png = Path.Combine(dir, stem + OverlaySuffix);
                PhonePadDeviceCatalog.DeleteWithMeta(borders);
                if (File.Exists(png))
                    PhonePadDeviceCatalog.DeleteWithMeta(png);
                removed++;
            }

            AssetDatabase.Refresh();
            return removed;
        }

        static bool IsStale(string bordersAbs, FrameSource src)
        {
            try
            {
                string text = File.ReadAllText(bordersAbs);
                return text.IndexOf(src.Label, StringComparison.Ordinal) < 0;
            }
            catch
            {
                return true;
            }
        }

        /// <summary>
        /// Downloads <paramref name="url"/> while the progress bar stays cancelable
        /// (<paramref name="cancelled"/>), aborting after <see cref="DownloadTimeoutSeconds"/>.
        /// </summary>
        static bool Download(
            string url,
            string label,
            float progress,
            out byte[] bytes,
            out string error,
            out bool cancelled)
        {
            bytes = null;
            error = null;
            cancelled = false;
            using (var req = UnityWebRequest.Get(url))
            {
                var op = req.SendWebRequest();
                double start = EditorApplication.timeSinceStartup;
                while (!op.isDone)
                {
                    if (EditorUtility.DisplayCancelableProgressBar(ProgressTitle, label + " (downloading)", progress))
                    {
                        cancelled = true;
                        error = "Cancelled.";
                        req.Abort();
                        return false;
                    }
                    if (EditorApplication.timeSinceStartup - start > DownloadTimeoutSeconds)
                    {
                        error = "Timed out downloading frame.";
                        req.Abort();
                        return false;
                    }
                    System.Threading.Thread.Sleep(20);
                }

                if (req.result != UnityWebRequest.Result.Success)
                {
                    error = req.error ?? ("HTTP " + req.responseCode);
                    return false;
                }

                bytes = req.downloadHandler.data;
                if (bytes == null || bytes.Length < 64)
                {
                    error = "Empty download.";
                    return false;
                }

                return true;
            }
        }

        static bool ConvertFrameToOverlay(
            byte[] framePng,
            FrameSource src,
            int targetW,
            int targetH,
            string outAbs,
            out int borderL,
            out int borderT,
            out int borderR,
            out int borderB,
            out int corner,
            out string error)
        {
            borderL = borderT = borderR = borderB = corner = 0;
            error = null;

            var srcTex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!srcTex.LoadImage(framePng, false))
            {
                UnityEngine.Object.DestroyImmediate(srcTex);
                error = "PNG decode failed.";
                return false;
            }

            int screenX = src.ScreenX;
            int screenY = src.ScreenY;
            int screenW = src.ScreenW;
            int screenH = src.ScreenH;
            int frameW = src.FrameW;
            int frameH = src.FrameH;

            // Device Simulator skins are authored in portrait; rotate landscape mockups.
            bool srcLandscape = screenW > screenH;
            bool dstPortrait = targetW <= targetH;
            if (srcLandscape && dstPortrait)
            {
                var rotated = Rotate90Clockwise(srcTex);
                UnityEngine.Object.DestroyImmediate(srcTex);
                srcTex = rotated;
                // 90° CW: (x,y) → (h-1-y, x). Screen bbox + frame size follow.
                int newScreenX = frameH - (screenY + screenH);
                int newScreenY = screenX;
                int newScreenW = screenH;
                int newScreenH = screenW;
                screenX = Mathf.Max(0, newScreenX);
                screenY = Mathf.Max(0, newScreenY);
                screenW = newScreenW;
                screenH = newScreenH;
                int tmp = frameW;
                frameW = frameH;
                frameH = tmp;
            }

            float scaleX = targetW / (float)Mathf.Max(1, screenW);
            float scaleY = targetH / (float)Mathf.Max(1, screenH);
            borderL = Mathf.Max(1, Mathf.RoundToInt(screenX * scaleX));
            borderT = Mathf.Max(1, Mathf.RoundToInt(screenY * scaleY));
            borderR = Mathf.Max(1, Mathf.RoundToInt((frameW - screenX - screenW) * scaleX));
            borderB = Mathf.Max(1, Mathf.RoundToInt((frameH - screenY - screenH) * scaleY));
            int outW = borderL + targetW + borderR;
            int outH = borderT + targetH + borderB;

            // Cap extreme pad overlays so Editor stays responsive.
            const int maxDim = 4096;
            if (outW > maxDim || outH > maxDim)
            {
                float shrink = maxDim / (float)Mathf.Max(outW, outH);
                outW = Mathf.Max(2, Mathf.RoundToInt(outW * shrink));
                outH = Mathf.Max(2, Mathf.RoundToInt(outH * shrink));
                borderL = Mathf.Max(1, Mathf.RoundToInt(borderL * shrink));
                borderT = Mathf.Max(1, Mathf.RoundToInt(borderT * shrink));
                borderR = Mathf.Max(1, Mathf.RoundToInt(borderR * shrink));
                borderB = Mathf.Max(1, Mathf.RoundToInt(borderB * shrink));
                targetW = Mathf.Max(1, outW - borderL - borderR);
                targetH = Mathf.Max(1, outH - borderT - borderB);
            }

            var dst = ScaleBilinear(srcTex, outW, outH);
            UnityEngine.Object.DestroyImmediate(srcTex);

            // Soften residual screen pixels that should stay see-through for Simulator.
            var pixels = dst.GetPixels32();
            for (int i = 0; i < pixels.Length; i++)
            {
                if (pixels[i].a <= 12)
                    pixels[i] = new Color32(0, 0, 0, 0);
            }

            dst.SetPixels32(pixels);
            dst.Apply(false, false);
            File.WriteAllBytes(outAbs, dst.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(dst);

            corner = Mathf.Clamp(
                Mathf.RoundToInt(Mathf.Min(targetW, targetH) * 0.11f),
                48,
                220);
            return true;
        }

        #endregion

        #region Helpers

        static Texture2D Rotate90Clockwise(Texture2D src)
        {
            int sw = src.width;
            int sh = src.height;
            var dst = new Texture2D(sh, sw, TextureFormat.RGBA32, false);
            var srcPixels = src.GetPixels32();
            var dstPixels = new Color32[sw * sh];
            for (int y = 0; y < sh; y++)
            {
                for (int x = 0; x < sw; x++)
                {
                    int nx = sh - 1 - y;
                    int ny = x;
                    dstPixels[ny * sh + nx] = srcPixels[y * sw + x];
                }
            }

            dst.SetPixels32(dstPixels);
            dst.Apply(false, false);
            return dst;
        }

        static Texture2D ScaleBilinear(Texture2D src, int w, int h)
        {
            var dst = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var srcPixels = src.GetPixels();
            int sw = src.width;
            int sh = src.height;
            var dstPixels = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                float v = (y + 0.5f) / h;
                float sy = v * sh - 0.5f;
                int y0 = Mathf.Clamp(Mathf.FloorToInt(sy), 0, sh - 1);
                int y1 = Mathf.Min(y0 + 1, sh - 1);
                float fy = sy - y0;
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w;
                    float sx = u * sw - 0.5f;
                    int x0 = Mathf.Clamp(Mathf.FloorToInt(sx), 0, sw - 1);
                    int x1 = Mathf.Min(x0 + 1, sw - 1);
                    float fx = sx - x0;
                    Color c00 = srcPixels[y0 * sw + x0];
                    Color c10 = srcPixels[y0 * sw + x1];
                    Color c01 = srcPixels[y1 * sw + x0];
                    Color c11 = srcPixels[y1 * sw + x1];
                    Color c0 = Color.Lerp(c00, c10, fx);
                    Color c1 = Color.Lerp(c01, c11, fx);
                    dstPixels[y * w + x] = Color.Lerp(c0, c1, fy);
                }
            }

            dst.SetPixels(dstPixels);
            dst.Apply(false, false);
            return dst;
        }

        static void WriteBordersSidecar(
            string abs,
            int bl,
            int bt,
            int br,
            int bb,
            int corner,
            FrameSource src)
        {
            var sb = new StringBuilder(256);
            sb.Append("{\n");
            sb.Append("  \"x\": ").Append(bl).Append(",\n");
            sb.Append("  \"y\": ").Append(bt).Append(",\n");
            sb.Append("  \"z\": ").Append(br).Append(",\n");
            sb.Append("  \"w\": ").Append(bb).Append(",\n");
            sb.Append("  \"cornerRadius\": ").Append(corner).Append(",\n");
            sb.Append("  \"source\": \"device-frames-media\",\n");
            sb.Append("  \"label\": \"").Append(PhonePadDeviceCatalog.EscapeJson(src.Label)).Append("\",\n");
            sb.Append("  \"url\": \"").Append(PhonePadDeviceCatalog.EscapeJson(src.FrameUrl)).Append("\"\n");
            sb.Append("}\n");
            File.WriteAllText(abs, sb.ToString(), new UTF8Encoding(false));
        }

        static bool ReadBorders(
            string abs,
            out int x,
            out int y,
            out int z,
            out int w,
            out int corner)
        {
            x = y = z = w = corner = 0;
            string text = File.ReadAllText(abs);
            if (!ReadInt(text, "x", out x) ||
                !ReadInt(text, "y", out y) ||
                !ReadInt(text, "z", out z) ||
                !ReadInt(text, "w", out w))
                return false;
            ReadInt(text, "cornerRadius", out corner);
            if (corner < 1)
                corner = 80;
            return x > 0 && y > 0 && z > 0 && w > 0;
        }

        static bool ReadInt(string json, string key, out int value)
        {
            value = 0;
            var m = Regex.Match(json, "\"" + key + "\"\\s*:\\s*(-?\\d+)");
            if (!m.Success)
                return false;
            return int.TryParse(m.Groups[1].Value, out value);
        }

        #endregion
    }
}
