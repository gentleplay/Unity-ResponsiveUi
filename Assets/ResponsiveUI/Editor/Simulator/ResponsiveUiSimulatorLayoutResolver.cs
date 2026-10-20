#if UNITY_EDITOR
using System;
using FDPlus.Foldable;
using FDPlus.Foldable.Editor;
using FDPlus.PhonePad.Editor;
using UnityEngine;

using FDPlus.ResponsiveUI;
using FDPlus.ResponsiveUI.Shared.Editor;

namespace FDPlus.ResponsiveUI.Editor
{
/// <summary>
/// Resolves one ResponsiveUI layout key from the current Device Simulator snapshot.
/// This is the single editor-side mapping used by foldout focus, capture, and layout apply.
/// </summary>
public static class ResponsiveUiSimulatorLayoutResolver
{
    #region Constants

    const float DefaultPadMaxAspect = ResponsiveDevice.DefaultPadMaxAspect;

    #endregion

    #region Resolve

    public static ResponsiveDevice.LayoutKey Resolve(
        SimulatorDeviceBridge.Selection selection)
    {
        ResponsiveDevice.FormFactor form = ResolveFormFactor(selection);
        ResponsiveDevice.Orientation orientation = ResolveOrientation(selection);
        return new ResponsiveDevice.LayoutKey(form, orientation);
    }

    /// <summary>
    /// Screen shape: the bound foldable session's pose, else the chrome rotation / WxH.
    /// <see cref="ResponsiveDevice"/> names the slot from it (Fold half-open names the posture).
    /// </summary>
    static ResponsiveDevice.Orientation ResolveOrientation(
        SimulatorDeviceBridge.Selection selection)
    {
        if (FoldableEditorSession.IsFoldableDeviceBound &&
            FoldableDeviceCatalog.IsToolkitListName(selection.FriendlyName))
        {
            return FoldableEditorSession.IsLandscape
                ? ResponsiveDevice.Orientation.Landscape
                : ResponsiveDevice.Orientation.Portrait;
        }

        if (selection.HasRotation)
        {
            return SimulatorDeviceBridge.IsLandscapeRotation(selection.RotationDegrees)
                ? ResponsiveDevice.Orientation.Landscape
                : ResponsiveDevice.Orientation.Portrait;
        }

        return ResponsiveDevice.DetectOrientation(
            selection.ScreenWidth,
            selection.ScreenHeight);
    }

    static ResponsiveDevice.FormFactor ResolveFormFactor(
        SimulatorDeviceBridge.Selection selection)
    {
        string friendlyName = selection.FriendlyName ?? "";

        // Hot path for Phone/Pad skins: prefix is authoritative and O(1). Also beats a
        // stale Fold/Flip session left bound for one frame after switching devices.
        if (ResponsiveUiSimulatorName.ResolvePhonePadListRole(friendlyName, out bool listIsPad))
        {
            return listIsPad
                ? ResponsiveDevice.FormFactor.Pad
                : ResponsiveDevice.FormFactor.Phone;
        }

        // Fold Cover WxH still upgrades Pad→FoldPhoneCover (Expand lag). Flip Opened
        // session wins over leftover Cover chrome (★ Flip lists Cover first).
        FoldableDeviceCatalog.Resolution bySize = FoldableDeviceCatalog.Resolve(
            selection.DeviceModel,
            selection.ScreenWidth,
            selection.ScreenHeight);
        if (bySize.IsKnownFoldable && bySize.Screen != null &&
            FoldableDeviceCatalog.ScreenMatchesPublic(bySize.Screen, selection.ScreenWidth, selection.ScreenHeight))
        {
            return RefineResolvedForm(
                ResponsiveDevice.MapFoldableProduct(
                    bySize.Device.Kind,
                    ResolveProductPreferringScreen(bySize)),
                selection);
        }

        if (FoldableDeviceCatalog.IsToolkitListName(friendlyName))
        {
            FoldableDeviceCatalog.Resolution resolution =
                FoldableDeviceCatalog.ResolveByFriendlyName(
                    friendlyName,
                    selection.DeviceModel,
                    selection.ScreenWidth,
                    selection.ScreenHeight,
                    selection.ScreenIndex);
            if (resolution.IsKnownFoldable && resolution.Device != null)
            {
                return RefineResolvedForm(
                    ResponsiveDevice.MapFoldableProduct(
                        resolution.Device.Kind,
                        ResolveProductPreferringScreen(resolution)),
                    selection);
            }
        }

        // Bound session without a size match (transient 0×0) — ProductState only.
        if (FoldableEditorSession.IsFoldableDeviceBound &&
            FoldableEditorSession.DeviceKind != FoldableDeviceKind.Unknown)
        {
            FoldableProductState product = FoldableEditorSession.ProductState;
            if (FoldableEditorSession.BoundScreenIsCover &&
                !ResponsiveDevice.IsIgnoringStaleFlipCoverSize())
                product = FoldableProductState.Closed;
            return RefineResolvedForm(
                ResponsiveDevice.MapFoldableProduct(
                    FoldableEditorSession.DeviceKind,
                    product),
                selection);
        }

        // Design-ref skins use "\u0001ShortName" (no ● Pad/Phone role) — catalog Kind.
        PhonePadDeviceCatalog.DeviceProfile profile =
            FindPhonePadProfile(friendlyName);
        if (profile != null)
        {
            return profile.Kind == PhonePadDeviceKind.Pad
                ? ResponsiveDevice.FormFactor.Pad
                : ResponsiveDevice.FormFactor.Phone;
        }

        if (bySize.IsKnownFoldable)
        {
            return RefineResolvedForm(
                ResponsiveDevice.MapFoldableProduct(
                    bySize.Device.Kind,
                    ResolveProductPreferringScreen(bySize)),
                selection);
        }

        float width = Mathf.Abs(selection.ScreenWidth);
        float height = Mathf.Abs(selection.ScreenHeight);
        if (width <= 0f || height <= 0f)
            return ResponsiveDevice.FormFactor.Phone;

        float aspect = Mathf.Max(width, height) / Mathf.Min(width, height);
        // Fold8 Cover (~1.58) and iPhone Duo Cover (~1.46) are under padMax — never classify
        // as Pad by aspect when Simulator WxH is a known Cover size (incl. Safe Area insets).
        if (ResponsiveDevice.MapKnownWideCoverSize(width, height, out ResponsiveDevice.FormFactor cover))
            return cover;

        if (ResponsiveDevice.IsKnownFlipCoverPhoneSize(width, height))
        {
            if (ResponsiveDevice.IsIgnoringStaleFlipCoverSize())
                return ResponsiveDevice.FormFactor.Phone;
            return ResponsiveDevice.FormFactor.FlipPhoneCover;
        }

        // Inclusive: 16:10 tablets (1600×2560) are exactly padMax and must be Pad.
        return aspect <= DefaultPadMaxAspect
            ? ResponsiveDevice.FormFactor.Pad
            : ResponsiveDevice.FormFactor.Phone;
    }

    static ResponsiveDevice.FormFactor RefineResolvedForm(
        ResponsiveDevice.FormFactor form,
        SimulatorDeviceBridge.Selection selection) =>
        ResponsiveDevice.RefineCoverForms(
            form, selection.ScreenWidth, selection.ScreenHeight);

    /// <summary>
    /// Session Opened / Closed / HalfOpened wins. Inferring Closed from Cover WxH
    /// after Flip first-bind made Active Objects use Flip Cover while the
    /// Fold panel already showed Opened.
    /// </summary>
    static FoldableProductState ResolveProductPreferringScreen(
        FoldableDeviceCatalog.Resolution resolution)
    {
        if (FoldableEditorSession.IsFoldableDeviceBound &&
            resolution.Device != null &&
            FoldableEditorSession.DeviceKind == resolution.Device.Kind)
        {
            FoldableProductState product = FoldableEditorSession.ProductState;
            if (product == FoldableProductState.HalfOpened)
                return FoldableProductState.HalfOpened;
            if (product == FoldableProductState.Closed)
                return FoldableProductState.Closed;
            if (product == FoldableProductState.Opened)
                return FoldableProductState.Opened;
        }

        return InferProductFromScreen(resolution);
    }

    static FoldableProductState InferProductFromScreen(
        FoldableDeviceCatalog.Resolution resolution)
    {
        if (resolution.Screen != null && resolution.Screen.CoverOrOuter)
            return FoldableProductState.Closed;
        return FoldableProductState.Opened;
    }

    static PhonePadDeviceCatalog.DeviceProfile FindPhonePadProfile(string friendlyName)
    {
        // Catalog ShortName / Id never carry Simulator role prefixes — compare once.
        string normalized = ResponsiveUiSimulatorName.Normalize(friendlyName);
        if (string.IsNullOrEmpty(normalized))
            return null;

        PhonePadDeviceCatalog.DeviceProfile[] devices = PhonePadDeviceCatalog.Devices;
        for (int i = 0; i < devices.Length; i++)
        {
            PhonePadDeviceCatalog.DeviceProfile device = devices[i];
            if (string.Equals(device.ShortName, normalized, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(device.Id, normalized, StringComparison.OrdinalIgnoreCase))
                return device;
        }

        return null;
    }

    #endregion
}
}

#endif
