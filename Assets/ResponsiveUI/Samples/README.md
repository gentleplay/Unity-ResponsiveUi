# Samples

Runtime types live in `FDPlus.ResponsiveUI`. In gameplay scripts:

```csharp
using FDPlus.ResponsiveUI;
```

Phone / Pad slots apply on Android and iOS players. Fold / Flip HalfOpened, FoldPhoneCover, and hinge splits apply in the Editor (Device Simulator), on **Android players**, and on **iPhone Duo** (UIKit hinge bridge, `IphoneDuo/` module). Other iPhones / iPads stay Phone / Pad. Windows / macOS / Linux / WebGL players use the Wide Screen slots only.

Auto-width TMP messages use `ResponsiveTextBanner` plus a controller widget Target; set Alignment on the banner (that writes RectTransform pivot) and call `Show(string)` / `Hide()`. After the pivot is set, a new message changes width only.

FancyScrollView spacing per device uses `ResponsiveScrollView`: add it on the **same GameObject** as the scroll view and author Cell Interval / Scroll Offset per slot. No code is needed in the scroll view script — the component writes the values and Relayouts on every device profile / fold state change (`Bind` is an optional override).

Platforms and the iOS adapter: [../Docs/PLATFORMS.md](../Docs/PLATFORMS.md).

## Create a sample Canvas

**Responsive UI → Scene → Create Sample Canvas** (`Editor/Package/ResponsiveUiSampleSetup.cs`) builds a disabled `ResponsiveUI` hub plus `ResponsiveUiController` on a Canvas with a Background image and a Widget target. Allowed-orientation slots start Enabled except Flip Cover Landscape, which starts Disabled, and Wide Screen, which starts Enabled only on Wide Screen targets. Disabled Cover / HalfOpened slots fall back to Phone (iPhone Duo Seated tries Pad first; Wide Screen only its other orientation); the Widget Target stays as authored. Author those slots (or enable Cover / Pad / Fold / Flip to copy Phone Landscape or Portrait), then enable the hub.

Fold hinge splits: **Responsive UI → Scene → Create Fold & Flip Setup**.

## After import

1. **Responsive UI → Project → Validate Setup**
2. **Edit → Project Settings → Responsive UI** — Reference Device per form factor (Design Canvas; Wide Screen edits its Design Canvas Size directly), **Background Cover Size** (cover-fit art), and **Fold Half-Open Guide** (one Simulator divide line; default `(0.25, 0.25, 0.25, 0.4)` / `5px`). `ResponsiveCamera` Design UI Size is read-only and follows Design Canvas.
3. **Responsive UI → Devices** — Phone & Pad / Fold & Flip catalogs

Component reference: [../Docs/COMPONENTS.md](../Docs/COMPONENTS.md). New to the package?
Start with [../Docs/GETTING-STARTED.md](../Docs/GETTING-STARTED.md).
