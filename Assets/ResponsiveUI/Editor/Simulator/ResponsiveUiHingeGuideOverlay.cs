#if UNITY_EDITOR
using System.Collections.Generic;
using FDPlus.Foldable.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

using FDPlus.ResponsiveUI;

namespace FDPlus.ResponsiveUI.Editor
{
/// <summary>
/// Draws the dual-pane hinge line only in the Unity Editor Device Simulator
/// while Fold/Flip Expand Half Opened is on Inner chrome. Not Phone/Pad,
/// Cover (Normal / Closed), Scene view, or builds.
/// One overlay per Simulator window — independent of how many <see cref="ResponsiveUI"/>
/// components are in the scene.
/// </summary>
[InitializeOnLoad]
public static class ResponsiveUiHingeGuideOverlay
{
    #region Lifecycle

    const string OverlayElementName = "ResponsiveUiHingeGuideOverlay";

    static bool s_LastDraw;
    static VisualElement s_Overlay;

    static ResponsiveUiHingeGuideOverlay()
    {
        FoldableEditorSession.Changed -= OnSessionChanged;
        FoldableEditorSession.Changed += OnSessionChanged;
        FoldableEditorSession.ScreenSynced -= RefreshOverlay;
        FoldableEditorSession.ScreenSynced += RefreshOverlay;
        SimulatorDeviceBridge.SelectionChanged -= RefreshOverlay;
        SimulatorDeviceBridge.SelectionChanged += RefreshOverlay;
        EditorApplication.delayCall += RefreshOverlay;
    }

    /// <summary>Re-hook after Project Settings hinge guide changes.</summary>
    public static void NotifySettingsChanged() => RefreshOverlay();

    internal static void Teardown()
    {
        FoldableEditorSession.Changed -= OnSessionChanged;
        FoldableEditorSession.ScreenSynced -= RefreshOverlay;
        SimulatorDeviceBridge.SelectionChanged -= RefreshOverlay;
        HookSimulatorWindows(draw: false);
        s_Overlay = null;
        s_LastDraw = false;
        ResponsiveUiHingeGuide.ClearEditorSessionHint();
    }

    static void OnSessionChanged(FoldableEditorSession.Source _) => RefreshOverlay();

    static void RefreshOverlay()
    {
        bool draw = ShouldDrawNow();
        bool drawChanged = draw != s_LastDraw;
        s_LastDraw = draw;

        if (draw)
            ResponsiveUiHingeGuide.SetEditorSessionHint(FoldableEditorSession.BuildLayout());
        else
            ResponsiveUiHingeGuide.ClearEditorSessionHint();

        if (!drawChanged && !draw)
            return;

        HookSimulatorWindows(draw);
        if (draw && s_Overlay != null && s_Overlay.panel != null)
            s_Overlay.MarkDirtyRepaint();
    }

    #endregion

    #region Simulator

    static void HookSimulatorWindows(bool draw)
    {
        object[] windows = SimulatorDeviceBridge.GetSimulatorWindowObjects();
        s_Overlay = null;
        for (int i = 0; i < windows.Length; i++)
        {
            if (windows[i] is not EditorWindow window || window == null)
                continue;

            VisualElement root = window.rootVisualElement;
            if (root == null)
                continue;

            if (!draw)
            {
                RemoveOverlays(QueryOverlays(root), start: 0);
                continue;
            }

            VisualElement overlay = FindUniqueOverlay(root);
            if (overlay == null)
            {
                overlay = new HingeGuideElement();
                root.Add(overlay);
            }

            s_Overlay = overlay;
        }
    }

    static VisualElement FindUniqueOverlay(VisualElement root)
    {
        List<VisualElement> found = QueryOverlays(root);
        if (found == null || found.Count == 0)
            return null;

        RemoveOverlays(found, start: 1);
        return found[0];
    }

    static List<VisualElement> QueryOverlays(VisualElement root) =>
        root.Query<VisualElement>(name: OverlayElementName).ToList();

    static void RemoveOverlays(List<VisualElement> found, int start)
    {
        if (found == null)
            return;

        for (int i = start; i < found.Count; i++)
        {
            if (found[i] != null)
                found[i].RemoveFromHierarchy();
        }
    }

    sealed class HingeGuideElement : VisualElement
    {
        public HingeGuideElement()
        {
            name = OverlayElementName;
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute;
            style.left = 0;
            style.top = 0;
            style.right = 0;
            style.bottom = 0;
            style.flexGrow = 1;
            generateVisualContent += Paint;
        }

        void Paint(MeshGenerationContext ctx)
        {
            if (!GetDraw(out bool vertical, out float normalized, out float thicknessNorm,
                    out Color color, out float thicknessPx))
                return;

            Rect rect = contentRect;
            if (rect.width < 8f || rect.height < 8f)
                return;

            Rect game = EstimateSimulatorGameRect(this, rect);
            Rect line = ResponsiveUiHingeGuide.ToGuiRect(
                game, vertical, normalized, thicknessNorm, thicknessPx);
            if (line.width < 1f && line.height < 1f)
                return;

            Painter2D painter = ctx.painter2D;
            painter.fillColor = color;
            painter.BeginPath();
            painter.MoveTo(new Vector2(line.xMin, line.yMin));
            painter.LineTo(new Vector2(line.xMax, line.yMin));
            painter.LineTo(new Vector2(line.xMax, line.yMax));
            painter.LineTo(new Vector2(line.xMin, line.yMax));
            painter.ClosePath();
            painter.Fill();
        }
    }

    /// <summary>
    /// Prefer the Device Simulator's game Image/rect when present; else letterbox estimate.
    /// </summary>
    static Rect EstimateSimulatorGameRect(VisualElement host, Rect windowContent)
    {
        VisualElement root = host?.panel?.visualTree;
        if (root != null && FindGameScreenRect(root, out Rect screen))
            return screen;

        float aspect = 1f;
        int mw = FoldableEditorSession.MetricsWidth;
        int mh = FoldableEditorSession.MetricsHeight;
        if (mw > 0 && mh > 0)
            aspect = mw / (float)mh;

        float padX = Mathf.Max(24f, windowContent.width * 0.04f);
        float padY = Mathf.Max(72f, windowContent.height * 0.1f);
        Rect inner = new Rect(
            windowContent.x + padX,
            windowContent.y + padY,
            windowContent.width - padX * 2f,
            windowContent.height - padY * 2f);

        float fitW = inner.height * aspect;
        float fitH = inner.width / Mathf.Max(0.01f, aspect);
        if (fitW <= inner.width)
        {
            float x = inner.x + (inner.width - fitW) * 0.5f;
            return new Rect(x, inner.y, fitW, inner.height);
        }

        float y = inner.y + (inner.height - fitH) * 0.5f;
        return new Rect(inner.x, y, inner.width, fitH);
    }

    static bool FindGameScreenRect(VisualElement root, out Rect screen)
    {
        screen = default;
        VisualElement best = null;
        float bestArea = 0f;
        FindLargestGameLikeElement(root, root.worldBound, ref best, ref bestArea);
        if (best == null || bestArea < 64f * 64f)
            return false;

        Rect world = best.worldBound;
        VisualElement overlay = root.Q(OverlayElementName);
        if (overlay == null)
            return false;

        Vector2 localMin = overlay.WorldToLocal(new Vector2(world.xMin, world.yMin));
        Vector2 localMax = overlay.WorldToLocal(new Vector2(world.xMax, world.yMax));
        screen = Rect.MinMaxRect(localMin.x, localMin.y, localMax.x, localMax.y);
        return screen.width > 8f && screen.height > 8f;
    }

    static void FindLargestGameLikeElement(
        VisualElement ve,
        Rect rootWorld,
        ref VisualElement best,
        ref float bestArea)
    {
        if (ve == null || ve.name == OverlayElementName)
            return;

        string name = ve.name ?? "";
        string typeName = ve.GetType().Name;
        bool candidate =
            ve is Image ||
            typeName.IndexOf("Game", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            typeName.IndexOf("Device", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("Game", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("Screen", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("Device", System.StringComparison.OrdinalIgnoreCase) >= 0;

        if (candidate)
        {
            Rect wb = ve.worldBound;
            float area = wb.width * wb.height;
            if (area > bestArea &&
                wb.width >= rootWorld.width * 0.25f &&
                wb.height >= rootWorld.height * 0.25f)
            {
                best = ve;
                bestArea = area;
            }
        }

        int count = ve.hierarchy.childCount;
        for (int i = 0; i < count; i++)
            FindLargestGameLikeElement(ve.hierarchy[i], rootWorld, ref best, ref bestArea);
    }

    #endregion

    #region Draw

    static bool ShouldDrawNow() =>
        GetDraw(out _, out _, out _, out _, out _);

    static bool GetDraw(
        out bool vertical,
        out float normalized,
        out float thicknessNorm,
        out Color color,
        out float thicknessPx)
    {
        vertical = false;
        normalized = 0.5f;
        thicknessNorm = 0.02f;
        ResolveLineStyle(out color, out thicknessPx);

        if (EditorApplication.isCompiling)
            return false;
        if (!ResponsiveUiEditorSettings.ShowHalfOpenedHingeGuide)
            return false;
        if (!FoldableEditorSession.IsFoldableDeviceBound)
            return false;
        if (!LiveSelectionAllowsHinge())
            return false;

        return ResponsiveUiHingeGuide.ComputeFromLayout(
            FoldableEditorSession.BuildLayout(),
            out vertical,
            out normalized,
            out thicknessNorm);
    }

    /// <summary>
    /// Phone/Pad skins and Cover (Normal / Closed) chrome must not keep the
    /// divide line. Invalid selection (HWND recreate) defers to the session gate.
    /// </summary>
    public static bool LiveSelectionAllowsHinge()
    {
        if (!SimulatorDeviceBridge.GetActiveSelection(out var selection))
            return true;
        return LiveSelectionAllowsHinge(selection);
    }

    public static bool LiveSelectionAllowsHinge(in SimulatorDeviceBridge.Selection selection)
    {
        if (!selection.IsValid)
            return true;
        if (!FoldableDeviceCatalog.IsToolkitListName(selection.FriendlyName))
            return false;

        FoldableDeviceCatalog.Resolution resolution =
            FoldableDeviceCatalog.ResolveByFriendlyName(
                selection.FriendlyName,
                selection.DeviceModel,
                selection.ScreenWidth,
                selection.ScreenHeight,
                selection.ScreenIndex);
        if (!resolution.IsKnownFoldable)
            return false;
        if (resolution.Screen != null && resolution.Screen.CoverOrOuter)
            return false;
        if (ResponsiveDevice.IsKnownCoverPhoneSize(
                selection.ScreenWidth, selection.ScreenHeight))
            return false;

        return true;
    }

    static void ResolveLineStyle(out Color color, out float thicknessPx)
    {
        color = ResponsiveUiEditorSettings.HingeGuideColor;
        thicknessPx = Mathf.Clamp(
            ResponsiveUiEditorSettings.HingeGuideThicknessPx,
            ResponsiveUiHingeGuide.MinThicknessPx,
            ResponsiveUiHingeGuide.MaxThicknessPx);
    }

    #endregion
}
}

#endif
