# Production

For whoever cuts a release: what ships, what the package guarantees, what it does not, how to
export, and the gates a release must pass. Running the gates is covered in
[./TESTING.md](./TESTING.md); real-device evidence in [./DEVICE-TESTING.md](./DEVICE-TESTING.md).

The whole kit ships as one `.unitypackage` (`com.futuredigitalplus.responsiveui`). There is no
per-module package: `FoldAndFlip`, `IphoneDuo` and `PhonePad` ship inside it. The version comes from
the `**Version:**` line in the root `README.md` (`ResponsiveUiPackageExport.ResolvePackageVersion`),
falling back to `ResponsiveUiPackageSetup.Version`.

## What ships

`ResponsiveUiPackageExport.CollectProductionAssetPaths` builds an explicit path list — it never
recurses blindly — from these roots:

| Include | Notes |
|---------|-------|
| `README.md`, `LICENSE.md` / `LICENSE`, `package.json` | entry point, license, identity |
| `Docs/` | all documentation |
| `Runtime/`, `Editor/`, `SharedEditor/` | layout player + authoring + shared Editor infrastructure. Editor C# is grouped under `Editor/Package`, `Editor/Hub`, `Editor/Simulator`, `Editor/Widgets`, `Editor/Camera`, `Editor/Scroll` and `Editor/Banner` (the asmdef stays at `Editor/`) |
| `FoldAndFlip/` | foldable runtime (shared posture contract + `IFoldablePostureBackend` seam), Android Jetpack bridge, Fold/Flip Simulator skins, **Android Fold & Flip** panel |
| `IphoneDuo/` | iPhone Duo hinge bridge (`Plugins/iOS`), posture backend, posture rules (Cover / Seated / Flat), `IphoneDuoDevice` API + `IphoneDuoEvents`, Project Settings page, Simulator panel, iOS build steps |
| `PhonePad/` | phone/pad catalog JSON + procedural bezel generator + optional local photoreal importer |
| `Samples/` | optional samples |

`ResponsiveUiPackageExport.IsExcludedExportPath` drops these:

| Exclude | Why |
|---------|-----|
| Any path containing `/Tests/`, or ending in `/Tests` | gates run in the source project only |
| PhonePad `*.device` and `*.device.off` | generated on demand by **Enable Phone & Pad Simulator Skins** |
| PhonePad `*.borders.json` | sidecars for locally imported frames |
| Everything under PhonePad `Editor/Devices/DeviceOverlays/` (and the empty folder asset itself) | procedural bezels regenerate; locally imported photoreal PNGs are third-party artwork and are never redistributed |

FoldAndFlip `.device` skins **are** shipped — they are authored, not generated.

## Production guarantees

### Layout

| Guarantee | Detail |
|-----------|--------|
| Fallback is deterministic | Disabled Cover / HalfOpened slots fall back to Phone (iPhone Duo Seated tries Pad first, by screen shape); a disabled Wide Screen slot falls back only to the other Wide Screen orientation |
| Empty slots never apply | existing authored Cover / HalfOpened values stay authored, so an empty Cover slot does not Apply a zero layout |
| Visibility is explicit | the widget Target stays as authored — use Active Objects to show or hide HUD roots |
| Seeding is predictable | new widget / camera / scroll slots start Enabled for allowed orientations except Flip Cover Landscape, and Wide Screen (Enabled only on Windows / macOS / Linux / WebGL targets) |
| Seed source is defined | enabling Cover / Pad / Fold / Flip copies Phone Landscape or Phone Portrait; iPhone Duo Seated copies the first enabled slot of its fallback chain (Pad, then Phone); Wide Screen copies Pad once |
| Cover identity survives a wrong posture | `RefineCoverForms` maps Fold Cover W×H to `FoldPhoneCover` and Flip Cover W×H to `FlipPhoneCover` even when Fold Expand or the detector posture still reports Pad, Phone, or the other cover form |
| Notifications resolve fresh slots | orientation and product-state notifications resolve a fallback slot before applying |
| Layout-only passes are safe mid-tween | they do not stop a running two-side tween; idle sibling widgets still re-layout, and a widget skipped mid-tween gets the current device layout once its tween finishes |
| One client cannot break the pass | a client callback that throws is logged and the hub pass continues for the other clients |

Pad-copied Wide Screen slots hold Pad canvas units: the Pad seed copies each hub's Pad Canvas Scaler
references into its Wide Screen references while those are still the default. Press **Use Pad Design
Canvas** in Project Settings when you want that; new projects keep the built-in 1080×1920 /
1920×1080.

### Scroll views

`ResponsiveScrollView` owns scroll slots and takes its layout from the Canvas hub. It writes the
sibling FancyScrollView `cellInterval` / `scrollOffset` and Relayouts it on its own in player builds,
so the host script needs no responsive code; `Bind` is an optional override. It must sit on the
**same GameObject** as the host. Without a hub it falls back to `ResponsiveScrollDriver`. Add
Component / Reset copies a sibling FancyScrollView interval/offset into every slot. Serialized
`enabled` has no field default, so player builds keep a slot Disabled until that seed is saved; an
unset `cellInterval` (0) applies as `0.2`.

Scroll metrics update on a real transition and the host always Relayouts when initialized, so
safe-area and canvas-settle `layoutOnly` passes refresh cell positions even when metrics are
unchanged (a bound host does this with `ApplyChanged` + `Relayout`; an unbound host gets the same
from the component). Every hub pass carries the layout and scroll adopts it like the other clients:
unchanged → Relayout only, changed → full slot write. Disabled hubs cannot push scroll metrics.

**Stripping:** the unbound path reflects FancyScrollView's `cellInterval` / `scrollOffset` /
`initialized` fields and `Relayout()`. Those fields survive stripping (serialized or used by kept
code). If a high Managed Stripping Level removes `Relayout()`, the component falls back to
`UpdatePosition(currentPosition)`, which hosts keep alive through `Scroller.OnValueChanged`. On a
device build, fold or rotate once with a scroll view visible and confirm the spacing changes; if it
does not, have that host `Bind` a callback (no reflection).

If your FancyScrollView host uses `ExecuteAlways` for responsive metric authoring, keep its
Edit Mode guards: Edit Mode update/relayout calls must not initialize the cell pool, delayed cleanup
must remove clones serialized by older versions, and runtime layout must remove destroyed pool
references before calculation. Re-apply those guards when you update the upstream dependency.

### Text banners

`ResponsiveTextBanner` is a hub client, not a slot owner. Author the banner RectTransform as a
`ResponsiveUiController` widget Target, set Alignment on the banner (which writes pivot), then call
`Show(string)` / `Hide()`. After a hub Apply it recaptures that slot
(`[DefaultExecutionOrder(1000)]` LateUpdate in Play Mode; Editor `delayCall`) and fits width to TMP
without moving `anchoredPosition`. Play Mode same-form size ticks recapture only while the banner is
visible; a Simulator canvas-ready pass still notifies the banner. Hide the banner — or save with the
banner GameObject inactive — so the serialized size stays the slot original.

### Inspector authoring

Inspector list `+` on widgets, cameras, two-side tween, Active Objects and backgrounds starts
**empty** (Target/Image None, default slots). `ForEachNewRow` + `ResetNewListRow` run before and
after the list draws so Unity's duplicated previous row is not shown, including after an Inspector
rebuild. Each camera row has one optional Camera Background SpriteRenderer. `ResponsiveScrollView`
has no item list — one `ResponsiveScrollLayout` with twenty slots.

Widget, camera and scroll Inspectors provide collapsed exact-slot bulk-copy panels. Their From / To
menus follow **Show Other Slots** and Player Allowed Orientations. A confirmed copy overwrites every
destination value, **including Enabled**, while preserving all other slots and Target/binding
references. Widgets and cameras copy each list row independently; scroll copies within its single
layout. The operation is deferred outside IMGUI, is Undoable, and reports the source, destination,
copied count and clickable component in the Unity Console. Serialized values save even while
disabled; widget and camera preview require an active component plus an enabled hub, while
standalone scroll preview can run without a hub.

Inspectors share tinted collapsible sections (`ColoredFoldoutSection` / `DrawSlotSection`); Unity 6
lists use Foldout, not Foldout Header. Foldout arrows sit after the color bar (`DrawFoldout`); nested
bodies sit under the title (`DrawNestedContent` / `NestedContentRect` / `DrawGuiFoldout`). Hub fields
share `DrawHubField`. Field labels share one `FieldLabelWidth` column so values line up. Foldout
session keys include the component instance id.

### Fold and flip detection

Fold / Flip HalfOpened, Fold Cover, Flip Cover, iPhone Duo Seated and hinge splits run in the
**Editor**, in **Android players**, and in the **iPhone Duo** player (`UIHingeInteraction`). Other
iPhones and iPads stay Phone / Pad; Windows / macOS / Linux / WebGL use Wide Screen only. Full
table, the iPhone Duo Seated rules and the adapter checklist: [./PLATFORMS.md](./PLATFORMS.md).

| Guarantee | Detail |
|-----------|--------|
| Jetpack mapping | `FLAT` and `HALF_OPENED` are mapped from AndroidX `FoldingFeature` |
| HalfOpened survives an empty callback | a missing FoldingFeature after a live HalfOpened clears hinge state to Undefined, then `ShouldPreserveHalfOpened` restores Flex unless cover/Closed or Jetpack `FLAT` wins. An empty feature is never treated as Closed or Expand by itself; live hinge bounds, orientation and Device Kind survive it |
| Stale Flat is cleared | a missing FoldingFeature after Expand clears stale `Flat` so cover-size / Device Kind heuristics can resolve Closed/Normal (Phone) instead of staying Opened/Pad. Expand→Normal and Flex→Normal clear stale Flat/HalfOpened the same way |
| No size-probing live postures | live `HALF_OPENED` never size-probes into Closed and forces `isSeparating` (Google's FoldingFeature contract). Live `FLAT` also skips the size probe, except rear-display ACTIVE, which may keep `isCoverScreen` while the state is still `FLAT`. Device Kind open-size and Flip small-panel probes run only when `FoldableState` is `Undefined` |
| Late Jetpack republish | an Android `Screen` size change triggers a bridge `refresh`. If that refresh still carries a lagging `FLAT`/`HALF_OPENED` while metrics are cover-sized and the hinge no longer spans the window, the Java side clears that FoldingFeature (external displays never count as the reference size). Session peak grows only on live Flat / HalfOpened and survives Activity rebind |
| Posture clamping | Tabletop and Book layouts clamp camera viewports and UI anchors to `[0, 1]` |
| Window modes are carried, not interpreted | `isInMultiWindowMode` and `isInPictureInPictureMode` are copied into each runtime layout snapshot and suppress cover-size Closed heuristics while active |
| Activity rebind is clean | rebind clears stale `WindowLayoutInfo` so a previous Activity's hinge is not republished |
| One owner | only one `FoldablePostureDetector` should own the native listener; pause and resume start and stop WindowManager tracking. Prefer one detector per build |
| Persistence | `persistAcrossScenes` `DontDestroyOnLoad`s the owner across scene loads. Duplicate scene copies destroy themselves without stealing the native owner, and `FindActiveInstance` recovers the persistent singleton after a cached miss |
| Shrinking | consumer shrinker rules retain the Java listener and payload Unity uses. The Android library pins AndroidX Window `1.4.0` (`window` and `window-java`) and publishes `consumer-rules.pro` |
| Recommended settings | Android `targetSdk` / compile API **34+** with Window **1.4.0** |
| Skin generation | existing `.meta` GUIDs are preserved by the skin generator |

### PhonePad catalog

| Guarantee | Detail |
|-----------|--------|
| Stable identity | profile IDs are public, stable identifiers and must not be renamed |
| Schema | each profile uses the documented JSON schema, an upright portrait resolution, and an exact landscape width/height swap |
| Skins are regenerable | generated `.device` skins are not shipped. Recreate them after import via **Enable Phone & Pad Simulator Skins**. When skins exist locally they must match the profile ID, model, portrait resolution and DPI |
| Safe areas | must remain within their portrait or landscape screen bounds |
| Evidence | profile evidence must include an HTTPS source and a verification status of `matched` or `approx` |
| Overlays are presentation | generated overlays and locally imported photoreal frames are presentation assets, not spec sources |

## Known limitations

These are code-level contracts. The PowerShell validators and the Unity EditMode suites are static
and in-process checks; none of them proves Android callbacks, manifest merging, R8 behavior, iOS
hinge callbacks, or OEM hardware.

| Limitation | Detail |
|------------|--------|
| No universal Closed state | Android exposes none. Closed/Normal stays a best-effort result unless an explicit rear/cover session or a trustworthy size reference is available |
| `DeviceKind` is configuration | it is app configuration, not an Android hardware classification |
| Window modes are not postures | multi-window and picture-in-picture flags describe the host Activity at callback time and must not be read as Closed |
| Simulator profiles are fixtures | design fixtures, not certified hardware specifications |
| No hardware certification | the package does not certify fold/flip hardware. It ships the bridges as **best-effort code contracts**; the device matrix is consumer evidence, not a package ship gate |
| No rumored hardware | do not treat an unreleased or rumored foldable as a supported runtime. Another Apple foldable needs its hardware identifier in `IphoneDuoHinge.FoldableModels` and, if its posture differs, its own `IFoldablePostureBackend` |
| One iOS bridge | iOS fold support is iPhone Duo only, through UIKit `UIHingeInteraction` (`IphoneDuo/Runtime/Plugins/iOS/IphoneDuoHinge.mm`, iOS-only importer). Do not copy the Android library to iOS |
| Editor shows one target | the Editor shows the active build target's slots — switch the target to author the others |
| Play-Mode-only paths | the `ResponsiveScrollView` unbound-host write + Relayout, and the two-side re-apply after a tween skipped by a layout-only pass, are not covered by EditMode tests. Verify both by hand |

## Editor production workflow

| Step | Behavior |
|------|----------|
| **Device Catalog** | profile fields are locked until **Edit Profile**; **Save Profile** writes JSON and syncs `.device` sizes |
| **Use in Simulator** | sends the selected profile and the **currently previewed screen** |
| Catalog filters | keep the visible list selection synchronized with the active profile — save or cancel edits before changing filters |
| Entering Play Mode | keeps the Android Fold & Flip / iPhone Duo panel product (Half Opened / Normal / Closed). A domain reload restores that snapshot so a first-bind catalog Expand is not applied, and `FoldablePostureDetector` applies the snapshot on enable so inner-screen HUD does not flash Expand before detection |

Validate Setup and Export Unity Package are owned by root `Editor/Package`; no module has its own
export menu.

## Release checklist

### 1. Author the slots you ship

Open each scene with a hub, select `ResponsiveUI` / `ResponsiveUiController` / `ResponsiveCamera` /
`ResponsiveScrollView` / `ResponsiveTextBanner`, and switch the Device Simulator to each form you
ship (Cover, Pad, Half Opened) so the matching slot auto-expands — Allowed Orientations still hide
the other axis. Author those slots (Flip HalfOpened starts Enabled on new/seeded rows; enable Flip
Cover Landscape when you want that layout instead of Phone fallback), then **save**.

- Hide text banners, or leave them inactive, before saving so the serialized size stays the slot
  original.
- Use bulk copy to seed repeated destination slots only after the source slots are final. Switch the
  Simulator to the destination form afterwards and inspect at least one widget, camera and scroll
  host: bulk copy duplicates authored values but does not prove one layout visually fits another
  device.
- Keep the `ResponsiveUI` hub disabled while wiring a scene. Disabled hubs block controller
  widget/background/Active Object Apply, including Inspector background LivePush. Enable the hub
  only once its clients are ready.
- For every `ResponsiveScrollView`, enter Play Mode with the Device Simulator, switch Normal /
  Expand / Half Opened, and confirm the host's **Cell Interval** follows the Detected Slot. The Edit
  Mode Inspector writer and the Play Mode writer are separate paths, so check both.
- Use one safe-area manager per intended inset container. It should notify the hub through
  `RequestLayoutRefresh` so safe-area-only changes settle widgets and backgrounds once without
  firing Phone/Pad gameplay events. Do not put overlapping safe-area managers on one canvas branch.
- If you changed **Background Cover Size**, open each scene that has a `ResponsiveUI` hub once so
  `OnValidate` bakes the size (loaded scenes already update). Camera Design UI Size is locked to the
  Responsive UI form default, which resolves to the built-in form size in player builds; widget
  Design Canvas Size is locked the same way.
- After seeding, scroll Cover slots are Enabled with host metrics and Flip Cover Landscape starts
  Disabled. Widget YAML `enabled: 0` on Cover / HalfOpened falls back to Phone (iPhone Duo Seated:
  Pad first). Missing widget Cover / Flip Cover fields deserialize Disabled.

### 2. Confirm the fold/flip host wiring

- Confirm one scene in the boot path hosts a persistent `FoldablePostureDetector`
  (`persistAcrossScenes` on, Device Kind set), and that the boot path really goes through it, so
  later scenes inherit that owner. **Responsive UI → Scene → Create Fold & Flip Setup** adds a
  detector and, optionally, a Simulator overlay.
- Confirm Fold Cover HalfOpened is **Enabled** in every scene that should use it. Enabling a slot
  seeds it from Phone by screen shape, so author it afterwards. A widget you deliberately leave
  Disabled still falls back to Phone.
- On Android, HUD follows the live `LayoutKey` from fold posture `LayoutChanged`
  (`FoldablePostureDetector` via `FoldablePostureSource`).

### 3. Run the gates

See [./TESTING.md](./TESTING.md) for the full list and how to run each one. The short version, from
the Unity project root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Assets\ResponsiveUI\Tests\validate-package.ps1
```

That one gate nests the module gates and the math, structure and export-manifest checks. Then, in
the Editor:

1. **Responsive UI → Project → Validate Setup**
2. **Responsive UI → Project → Export Unity Package**

### 4. Profile a device build

On a development device, capture rotation, resume, multi-window and Fold Expand / Normal /
HalfOpened in the Unity Profiler. Filter the Timeline by `ResponsiveUI.` and confirm one
`ResponsiveUI.Hub.CanvasSettle` per normal hub notification batch; inspect
`ResponsiveUI.Camera.FocusWatch` when camera focus is active.

### 5. Record real-device evidence

Fill in the coverage matrix in [./DEVICE-TESTING.md](./DEVICE-TESTING.md) before claiming OEM
fold/flip or iPhone Duo compatibility for an application. Keep generated test logs under a CI
artifact directory such as `TestResults/` or `Logs/`; do not commit them inside the package.

## Export

With the Editor open: **Responsive UI → Project → Export Unity Package**, then save
`ResponsiveUI-<version>.unitypackage`. Import it elsewhere through **Assets → Import Package →
Custom Package…**.

Batch export (Editor closed — batchmode cannot share an open project):

```text
Unity -batchmode -nographics -projectPath <project> \
    -executeMethod FDPlus.ResponsiveUI.Editor.ResponsiveUiPackageExport.ExportUnityPackageBatch
```

Or through the wrapper script:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Assets\ResponsiveUI\Tests\export-production.ps1
```

Batch export writes to `<project>/Temp/ResponsiveUI-<version>.unitypackage`, which is where
`validate-export-manifest.ps1` looks for it.

## Gates

| Gate | Script | Proves |
|------|--------|--------|
| Structural | `Tests/validate-structure.ps1` (nested by `validate-package.ps1`) | files and public API names exist; an unqualified `StringComparison` or similar BCL name needs `using System`. Not a C# compile and not a formula proof |
| Math | `Tests/test-design-scale.ps1`, `Tests/test-camera-projection.ps1` | independent widget ratio and camera projection contracts |
| Module | `FoldAndFlip/Tests/validate-package.ps1`, `IphoneDuo/Tests/validate-package.ps1`, `PhonePad/Tests/validate-package.ps1` | per-module structure, namespaces, packaging, catalog consistency |
| EditMode | `Tests/run-editmode-tests.ps1` | behavioral contracts in Unity, across all three test assemblies |
| Export | `Tests/validate-export-manifest.ps1` | generated `.unitypackage` contents: no `/Tests/`, no PhonePad `.device` / `DeviceOverlays`, includes `package.json` and `SharedEditor` |

`Tests/validate-package.ps1` is the main gate: it nests the structural, math, module and
export-manifest gates. It skips EditMode unless `RESPONSIVEUI_RUN_EDITMODE=1`, because Unity
batchmode cannot share an open Editor, and the export-manifest script skips when no
`Temp/ResponsiveUI-*.unitypackage` exists. A pass proves static consistency only.

## After import in a consumer project

1. Accept **First-time setup** to apply the recommended Android settings and enable only the Design
   Canvas reference skins.
2. **Responsive UI → Project → Validate Setup**.
3. **Edit → Project Settings → Responsive UI** — confirm Reference Devices, **Background Cover
   Size** and the Fold Half-Open Guide, then the **Android Fold**, **Android Flip** and **iPhone
   Duo** pages ([./SETTINGS.md](./SETTINGS.md)).
4. **Responsive UI → Devices → Enable Phone & Pad Simulator Skins** if you want the full PhonePad
   catalog (procedural bezels).
5. Optional: **Responsive UI → Devices → Import Photoreal Frames (Local)**, then re-enable or repair
   skins. Editor-only — review the upstream frame licenses before redistributing any PNGs, which is
   also why `DeviceOverlays/` is excluded from the export.
6. Wire the hub and the posture detector as described in
   [./STRUCTURE.md](./STRUCTURE.md#integrating-into-a-project).
7. Optional: fill [./DEVICE-TESTING.md](./DEVICE-TESTING.md) in **your** application before claiming
   fold/flip OEM compatibility.

Platforms and the iOS adapter: [./PLATFORMS.md](./PLATFORMS.md). Upgrading an existing project:
[./MIGRATION.md](./MIGRATION.md).
