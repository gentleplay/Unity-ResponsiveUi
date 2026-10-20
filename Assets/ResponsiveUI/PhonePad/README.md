# Phone & Pad (Responsive UI module)

**Version:** 1.0.0  
**Package id:** `com.futuredigitalplus.phonepad.simulator`  
**Owner:** futuredigitalplus

Editor-only **device profile catalog** for non-foldable phones and pads.

Fold / Flip hardware and hinge splits live in **FoldAndFlip**, not here. PhonePad skins are for
ordinary phones and tablets (including iPhone / iPad authoring). See
[../Docs/PLATFORMS.md](../Docs/PLATFORMS.md).

Lives at `Assets/ResponsiveUI/PhonePad/` inside the unified **ResponsiveUI** package.
Kit Validate Setup and Export Unity Package are owned by the root package (`Editor/Package`).

Component reference: [../Docs/COMPONENTS.md](../Docs/COMPONENTS.md).

## Menus

- **Responsive UI → Devices → Phone & Pad Catalog**
- **Window → Responsive UI → Phone & Pad Catalog**
- **Responsive UI → Devices → Enable Phone & Pad Simulator Skins**
- **Responsive UI → Devices → Repair Phone & Pad Simulator Skins**
- **Responsive UI → Devices → Import Photoreal Frames (Local)**
- **Responsive UI → Devices → Clear Local Photoreal Frames**
- **Responsive UI → Project → Validate Setup** / **Export Unity Package**

First-run setup is owned by the root kit dialog and enables only the selected Design Canvas reference skins. Enabling the full catalog remains an explicit menu action. Profile portrait/landscape sizes also feed **Project Settings → Responsive UI** (Design Canvas defaults and **Background Cover Size**).

## What you can do

| Action | Behavior |
|--------|----------|
| **Add / Edit / Save / Cancel** | Create or edit Phone/Pad profiles (`Editor/Devices/Profiles/<id>.json`). Saving rewrites the profile's active skin; an unsaved draft and the list filters survive script reloads |
| **Duplicate / Remove / Reload** | Catalog maintenance. Remove also deletes the profile's skin and overlays and refreshes the Simulator list |
| **Enable / Disable All in Simulator** | Create or delete `.device` skins + kit bezel overlays |
| **Repair Phone & Pad Simulator Skins** | Rewrite every enabled skin from its profile and the current overlays (unchanged files are left alone) |
| **Import Photoreal Frames (Local)** | Download matched frames from [Device Frames Media](https://github.com/jonnyjackson26/device-frames-media) and write Unity Simulator overlays; enabled skins switch to them at once (cancelable). **Clear Local Photoreal Frames** switches them back to kit bezels |

Default bezels are **kit-generated** (rounded-rect; Apple phones get a Dynamic Island–style pill). Unmatched profiles keep procedural frames.

**Local photoreal import** covers ~30 catalog profiles with a direct (or closest) Device Frames Media match — including iPhone 15–17 Pro Max, recent iPads, Pixel 8/9, and a few Samsung stand-ins. Overlays land under `Editor/Devices/DeviceOverlays/` and are **not** exported in the MIT `.unitypackage`. Review upstream licensing before redistributing those PNGs.

Other article sources (CSS Device Frames, Lite Frames, Fastlane Frameit, SamsungDeviceSkins, Mockup Device Frames) are either web-only, license-blocked for redistribution, or wrong format for Unity Device Simulator.

**Also in your Editor:** Unity’s `com.unity.device-simulator.devices` package ships Companion-licensed Apple/Android overlays (e.g. iPhone 13 Pro Max) as separate Simulator entries.


## Tests

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Assets\ResponsiveUI\PhonePad\Tests\validate-package.ps1
```

The package gate runs the read-only profile/schema/resolution audit. See
[`../Docs/PRODUCTION.md`](../Docs/PRODUCTION.md) for release scope and limitations, and
[`../Docs/TESTING.md`](../Docs/TESTING.md) for every suite.

```text
Assets/ResponsiveUI/PhonePad/
  README.md
  Editor/
    ...
    Devices/Profiles/*.json
    Devices/*.device                       (optional, after Enable — not exported)
    Devices/DeviceOverlays/*_Main_Overlay.png  (generated with Enable — not exported)
  Tests/
    validate-package.ps1
    audit-profiles.ps1
    verify-official-specs.ps1
```
