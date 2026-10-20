# Structure

Architecture for contributors: where code lives, which assembly may reference which, how the
Device Simulator path is wired, and the conventions new code is expected to follow. If you only
want to *use* the package, read [./GETTING-STARTED.md](./GETTING-STARTED.md) and
[./CONCEPTS.md](./CONCEPTS.md) instead.

Package id `com.futuredigitalplus.responsiveui`. It ships as a `.unitypackage` that drops into
`Assets/`, not as a UPM package under `Packages/`.

## Folder tree

One line per folder. File-level detail lives in the tables below.

```text
Assets/ResponsiveUI/
├── README.md, LICENSE.md, package.json   # entry point, license, identity
├── Docs/                 # all documentation (this file included)
├── Runtime/              # layout player + shared types (no Editor code)
├── SharedEditor/         # dependency-free Editor infrastructure used by every module
├── Editor/               # layout editor, Project Settings, export
│   ├── Package/          # paths, export, Validate Setup, sample Canvas, Project Settings pages
│   ├── Hub/              # ResponsiveUI hub Inspector + shared Inspector chrome
│   ├── Simulator/        # Device Simulator → slot focus, layout scheduler, hinge overlay
│   ├── Widgets/          # controller, two-side tween, rect layout, Active Objects, backgrounds
│   ├── Camera/           # ResponsiveCamera Inspector, drawers, Scene sync
│   ├── Scroll/           # ResponsiveScrollView Inspector and slot drawers
│   └── Banner/           # ResponsiveTextBanner Inspector
├── FoldAndFlip/          # fold/flip module: shared posture contract + Android bridge
│   ├── Runtime/          # posture detector, layout state, IFoldablePostureBackend seam
│   │   └── Plugins/Android/Foldable.androidlib/   # Jetpack WindowManager Java bridge
│   ├── Editor/           # device catalog, posture simulator, Android Fold & Flip panel
│   │   └── Devices/      # Profiles/*.json, *.device, DeviceOverlays/ (bezel PNGs)
│   ├── Docs/             # detection reference
│   ├── Samples/          # FoldableSetup sample
│   └── Tests/            # validators + EditMode tests
├── IphoneDuo/            # iPhone Duo module (hinge bridge + posture rules)
│   ├── Runtime/          # device state, rules, posture backend
│   │   └── Plugins/iOS/  # UIKit UIHingeInteraction bridge (iOS players only)
│   ├── Editor/           # Project Settings page, Simulator panel, iOS build steps
│   └── Tests/Editor/     # EditMode tests
├── PhonePad/             # Editor-only phone/pad Simulator catalog
│   ├── Editor/           # catalog window, profile IO, skin generator
│   │   └── Devices/      # Profiles/*.json, *.device, DeviceOverlays/
│   └── Tests/            # validators
├── Tests/                # root validators + EditMode tests ([./TESTING.md](./TESTING.md))
└── Samples/              # optional sample content
```

## Assemblies and dependency rules

`includePlatforms: ["Editor"]` marks every Editor assembly; the three runtime assemblies build for
all platforms.

| Assembly | Folder | May reference |
|----------|--------|---------------|
| `com.futuredigitalplus.foldandflip` | `FoldAndFlip/Runtime` | nothing (dependency root) |
| `com.futuredigitalplus.responsiveui` | `Runtime` | `foldandflip`, `UnityEngine.UI`, `Unity.TextMeshPro` |
| `com.futuredigitalplus.iphoneduo` | `IphoneDuo/Runtime` | `foldandflip` |
| `com.futuredigitalplus.responsiveui.shared.editor` | `SharedEditor` | `UnityEditor.DeviceSimulatorModule` |
| `com.futuredigitalplus.foldandflip.editor` | `FoldAndFlip/Editor` | `foldandflip`, `shared.editor`, Device Simulator module, `UnityEngine.UI` |
| `com.futuredigitalplus.iphoneduo.editor` | `IphoneDuo/Editor` | `foldandflip`, `foldandflip.editor`, `iphoneduo`, `shared.editor`, Device Simulator module |
| `com.futuredigitalplus.phonepad.simulator.editor` | `PhonePad/Editor` | `shared.editor`, Device Simulator module |
| `com.futuredigitalplus.responsiveui.editor` | `Editor` | `responsiveui`, `UnityEngine.UI`, `shared.editor`, `foldandflip`, `foldandflip.editor`, `phonepad.simulator.editor` |

The rules that follow from that table, and that new code must not break:

| Rule | Why |
|------|-----|
| No runtime assembly references an Editor assembly | runtime must compile in a player build |
| `foldandflip` references nothing at all | the posture contract is the root every other assembly can depend on |
| `SharedEditor` depends only on the Device Simulator module | it is the one place every module may share Editor helpers through |
| The root Editor assembly does **not** reference `iphoneduo` or `iphoneduo.editor` | the iPhone Duo module is a leaf: it plugs into `FoldablePostureBackend` through `IFoldablePostureBackend` and can be deleted without touching the layout editor |
| Unity Device Simulator private API access goes through `SimulatorDeviceBridge` in `FoldAndFlip/Editor` | one reflection seam, one place to fix when Unity changes |
| Test assemblies are referenced by nothing | they are excluded from the export ([./PRODUCTION.md](./PRODUCTION.md)) |

`Runtime/AssemblyInfo.cs`-style `InternalsVisibleTo` declarations live in `Editor/AssemblyInfo.cs`
and `FoldAndFlip/Runtime/AssemblyInfo.cs`, so the EditMode suites can reach internals without the
production API exposing them.

## Namespaces

| Assembly | Namespace |
|----------|-----------|
| `Runtime` | `FDPlus.ResponsiveUI` |
| `Editor` | `FDPlus.ResponsiveUI.Editor` |
| `SharedEditor` | `FDPlus.ResponsiveUI.Shared.Editor` |
| FoldAndFlip runtime / editor | `FDPlus.Foldable` / `FDPlus.Foldable.Editor` |
| IphoneDuo runtime / editor | `FDPlus.IphoneDuo` / `FDPlus.IphoneDuo.Editor` |
| PhonePad editor | `FDPlus.PhonePad.Editor` |

Project scripts that use layout types need `using FDPlus.ResponsiveUI;`. Unity YAML binds
components by script GUID, not by namespace, so moving a file between folders inside the same
assembly does not break scenes or prefabs.

## Editor-only code

All Editor code sits under a folder named `Editor` (`Editor/`, `SharedEditor/`,
`FoldAndFlip/Editor/`, `IphoneDuo/Editor/`, `PhonePad/Editor/`, `*/Tests/Editor/`), so Unity strips
it from player builds whether or not an `.asmdef` is present. The root editor `.asmdef` stays at
`Editor/` and picks up every subfolder automatically.

| Folder | Owns |
|--------|------|
| `Editor/Package/` | Paths, export, Validate Setup, sample Canvas, Project Settings pages (menu paths come from `SharedEditor/ResponsiveUiEditorMenus.cs`) |
| `Editor/Hub/` | `ResponsiveUI` Inspector, Detection, foldouts, slot clipboard, bulk copy Undo, Events |
| `Editor/Simulator/` | Simulator slot focus, layout resolver, canvas-ready scheduler, hinge overlay |
| `Editor/Widgets/` | Controller, two-side tween, rect/widget drawers, capture guard, Active Objects, backgrounds |
| `Editor/Camera/` | Camera Inspector, drawers, Scene sync |
| `Editor/Scroll/` | Scroll Inspector and slot drawers (one layout — no item list) |
| `Editor/Banner/` | `ResponsiveTextBanner` Inspector (no slots) |

Every `[MenuItem]` in the package — in any module — takes its path from
`ResponsiveUiEditorMenus`, so the whole kit registers under one **Responsive UI** root. File-system
and asset paths go through `ResponsiveUiEditorPath` (forward-slash asset paths, OS-safe file-system
paths).

## Simulator state flow

Unity Device Simulator `deviceChanged` calls `SimulatorDeviceBridge.RequestSelectionPublish()`.
The bridge is the **single publisher**: it prefers the focused Simulator window (then visible, then
first valid), settles identity with a generation-cancelled `delayCall` retry chain (no idle
`EditorApplication.update` poll, identity ignores W×H flicker), then fires `SelectionChanged`.

Consumers, each with one job:

| Consumer | Job |
|----------|-----|
| `FoldableDeviceSimulatorPlugin` | UI only. Hides Fold controls while `IsSelectionPending`, then syncs bound-state labels from the session. Opened / Closed / Force Apply call `FoldableSimulatorScreenCommit` |
| `FoldableEditorSessionWatcher` | sole fold-binding consumer. Phone → Flip first-bind calls `QueueDeferred()` before bind so widget Apply waits for Cover → Main (`Changed` does not fire again; session metrics are already Main) |
| `FoldableSimulatorScreenCommit` | sole Cover/Inner chrome writer. Publishes `ScreenSynced` when Unity `Screen` matches the session product, or on the last catch-up tick if `Screen` still lags |
| `ResponsiveUiSimulatorSlotFocus` | sole layout-focus / Apply consumer (details below) |

`ResponsiveUiSimulatorSlotFocus` applies Phone/Pad from `SelectionChanged`. Fold/Flip does **not**
Apply from Cover `SelectionChanged`; `ApplySettledLayout` runs from `ScreenSynced` (session
`Changed` only when a commit is not in flight). It calls `ResponsiveUiEditorLayoutScheduler` for one
immediate focused Apply and one coalesced canvas-ready `layoutOnly` pass
(`RefreshEditorLayoutAfterCanvas`). That catch-up re-covers Match Mode and notifies every hub client
after canvases settle (`notifyAllLayoutClients`), except the client whose Target is selected.
Catalog W×H changes of ≥2px still re-Apply Phone/Pad when identity stays the same. While a Fold/Flip
session is bound, the layout pin uses session catalog metrics. Play Mode same-form size ticks still
gate extra client Apply on `SizeChangeRequiresReapply` (widget Design Scale skips a Scale With
Screen Size canvas; camera UI scale does not). Newly enabled hubs use the same scheduler instead of
a second handler-only `delayCall`.

`ResponsiveUiSimulatorLayoutResolver` produces the single `LayoutKey` used by Inspector focus,
editor capture, live preview, and scene layout application. There is no edit-mode polling or forced
Inspector rebuild in this path. Active selection reads go through
`IDeviceSimulatorSelectionAdapter` so EditMode tests can exercise selection and session behavior
without opening a Simulator window. Editor reload teardown cancels pending Simulator work; Camera /
Scroll / Widget sync and SlotFocus rebind from their `[InitializeOnLoad]` constructors, which re-run
after every domain reload.

### Runtime apply order

Runtime hub notification is phased by `ResponsiveUiApplyOrder`: controllers and two-side widgets
write in frozen early batches, one shared `Canvas.ForceUpdateCanvases` settles each batch and
cover-fits controller backgrounds, then cameras / scroll / banners / extension clients run. Every
client adopts the layout passed to `ApplyFromHub` on both full and layout-only passes (scroll
included), so no client resolves slots from a cached device state. `ResponsiveScrollView` isolates
host exceptions (logged, pass continues). Clients registered mid-batch are collected after settle
(not expanded mid-Apply), sorted by priority, and drained before the late phase. Late clients
restart from the beginning when new early geometry appears after they already ran. Direct controller
applies keep a local settle path.

External geometry owners call `ResponsiveUiHandler.RequestLayoutRefresh`; requests coalesce into one
layout-only `LateUpdate`. A safe-area manager should use this path so a safe-area-only change
reflows children even when the physical screen dimensions do not change.

A full Play Mode `Apply` that changes form factor after the first Apply can show an optional hub
**Form Transition Loading** (`ResponsiveFormTransitionLoading`: enable + black overlay Image and/or
loading controller + per-slot Show On flags + min visible; both cover via `CanvasGroup` with
`ignoreParentGroups`; end-of-frame hide; a destination slot that is off skips and dismisses any
in-flight cover; defaults leave Flip Cover off) so Fold/Flip Opened / HalfOpened / Cover switches do
not flash half-applied widgets and cameras. The hub raises the black overlay (and activates the
controller) under `ApplyInFlight` (`OnEnable` skips `ApplyNow`), then notifies clients after the
scaler. Disabling hides immediately.

Profiler Timeline markers use the `ResponsiveUI.*` prefix: `ResponsiveUI.Hub.Apply`,
`ResponsiveUI.Hub.LayoutRefresh`, `ResponsiveUI.Hub.NotifyClients`,
`ResponsiveUI.Hub.CanvasSettle`, and `ResponsiveUI.Camera.FocusWatch`.

## Design canvas defaults

**Project Settings → Responsive UI** picks a catalog **Reference Device** per form factor: Phone,
Pad, Fold Cover, Fold Half Opened, Fold Cover Half Opened, Flip Cover, Flip Half Opened, iPhone Duo
Cover, iPhone Duo Seated. Wide Screen has no catalog device — its row edits Design Canvas Size and
Default Scale directly (built-in 1080×1920, stored portrait), and **Use Pad Design Canvas** copies
the Pad size. The portrait size locks widget Design Canvas Size and camera **Design UI Size**.

The hub Canvas Scaler keeps authored references: Phone (Phone, Fold Cover, Flip Cover, Flip Half
Opened, iPhone Duo Cover), Pad (Pad, Fold Half Opened, Fold Cover Half Opened, iPhone Duo Seated),
and its own Wide Screen references — a Pad seed of a Wide Screen slot copies Pad's references while
they are still the default. Changing a Design Canvas reference re-Applies loaded `ResponsiveUI` hubs
(clients follow) in the Editor.

**Background Cover Size** (same page) is a separate project-wide cover-art size: a 13-inch-tablet
landscape default from the catalog, another catalog device, or Follow a Design Canvas reference.
Loaded `ResponsiveUI` components without Override update immediately; other scenes bake the size on
open (`OnValidate`) for player builds. Project Settings changes made while Play Mode owns the scene
are remembered and synchronized on `EnteredEditMode`; the watcher never dirties scene objects during
Play Mode.

**Fold Half-Open Guide** (same page) is one Device Simulator overlay for Expand Half Opened on Inner
chrome. `ResponsiveUiHingeGuideOverlay` draws it once per Simulator window and removes it on
Phone/Pad, Cover (Normal / Closed), and Expand. Default color `(0.25, 0.25, 0.25, 0.4)`, thickness
`5px`. Scene `ResponsiveUI` components do not each draw a line.

Cameras do not read Background Cover Size. In player builds, unset (`0,0`) camera and widget design
sizes use built-in form defaults unless the slot was authored with an explicit size.

## Inspector slot contract

Widgets (`ResponsiveUiController`), cameras (`ResponsiveCamera`), and `ResponsiveScrollView` share
twenty slots: ten form factors × Portrait / Landscape. The canonical order lives on
`ResponsiveWidgetLayout.FormFactors` (enum value = slot order), and Design Forms and the drawers
alias it:

```text
Phone → Pad → Fold Cover → Fold Half Opened → Fold Cover Half Opened →
Flip Cover → Flip Half Opened → iPhone Duo Cover → iPhone Duo Seated → Wide Screen
```

Unity YAML binds slot fields by **name**, so reordering the declarations does not require scene
migration.

| Behavior | Contract |
|----------|----------|
| Auto-expand | the detected Simulator slot opens; disallowed Player orientations are hidden |
| Seeding | allowed slots seed **Enabled** on new/seeded rows, except Flip Cover Landscape (Disabled) and Wide Screen (Enabled only on Wide Screen targets, `ResponsivePlatforms.SeedsEnabled`) |
| Seed source | enabling Cover / Pad / Fold / Flip copies Phone Landscape or Phone Portrait; iPhone Duo Seated copies the first enabled slot of its fallback chain; Wide Screen copies Pad once (`ResponsiveWidgetSlotResolver.GetSeedCandidate`) |
| Fallback | Disabled Cover / HalfOpened / Pad slots fall back to Phone (iPhone Duo Seated tries Pad first, by screen shape — see [./PLATFORMS.md](./PLATFORMS.md#iphone-duo-seated-rules)); a disabled Wide Screen slot falls back only to the other Wide Screen orientation, never Pad or Phone |
| Posture naming | Fold and Fold Cover Half Opened are named by posture (Landscape = tall flat screen turned 90°, horizontal hinge; Portrait = wide flat screen, vertical hinge; `ResponsiveDevice.OrientationForForm`), follow the Player orientation lock normally, and fall back by screen shape to Phone only |
| Always allowed | both iPhone Duo Seated slots and both Wide Screen slots (`SlotAllowedByPlayerAxes`): Seated allows only the hold's axis (`IphoneDuoHoldOrientation`, Default Orientation = Auto Rotation), Wide Screen ignores the mobile lock |
| Visibility | Widget Targets stay as authored; use Active Objects to show or hide HUD roots. Two-side tween uses the same resolver (`SetVisible` owns two-side visibility) |
| Banner | `ResponsiveTextBanner` has no slot foldouts — author its RectTransform as a controller widget Target and set Alignment on the banner (that writes pivot, so text width does not move position) |

The shared fallback resolver is `ResponsiveWidgetSlotResolver` (Cover/HalfOpened → Phone fallback
for widgets, cameras, scroll, and two-side).

### Inspector chrome

Each foldout has **Copy / Paste / Reset** (`ResponsiveUiSlotClipboard`). The same utility owns the
collapsed exact-slot bulk-copy panel and available-slot filtering for the widget, two-side tween,
camera, and scroll Inspectors. `ResponsiveUiInspectorFoldouts.BoxedFoldoutSection` supplies one
visual hierarchy: **Widgets** contains Bulk Copy + **Widget Items** (controller and two-side tween),
**Cameras** contains settings + Bulk Copy + **Camera Items**, and **Scroll Layout** contains Bulk
Copy + **Scroll Slots**. The shared box wraps the open body only, so the Widgets / Cameras / Scroll
Layout headers keep the same arrow X as Detection, Active Objects, and Usage notes.

Hub, controller, camera, scroll, and banner Inspectors use tinted `ColoredFoldoutSection` groups
with `InspectorSectionKey`, so two selected components do not share foldouts. Foldout arrows sit
after the section color bar (`DrawFoldout` does not add a second Unity indent). Nested bodies sit
under the title (`DrawNestedContent` / `DrawPropertyFoldout` / `NestedContentRect` /
`DrawGuiFoldout`). Hub fields share `DrawHubField`. Field labels share one `FieldLabelWidth` column
so values line up across those Inspectors and slot sections.

Two-side widgets reuse `ResponsiveWidgetLayout` / `ResponsiveRectState` for every Left/Right ×
Normal / Animation Start layout, so slot fields and foldout Copy / Paste match
`ResponsiveUiController` widgets. Two-side bulk copy writes only the currently shown Side + Preview
layout (`CopySlotAcrossTwoSideWidgets`). **Bulk Copy Side** mirrors every slot on Normal and
Animation Start from Left to Right or Right to Left (`CopySideAcrossTwoSideWidgets`,
`MirrorHorizontally`) and switches the Inspector Side to the destination for preview. **Bulk Copy
Widget** copies or swaps every slot on that side between two widget rows
(`CopyWidgetSideAcrossTwoSideWidgets`, `SwapWidgetSideAcrossTwoSideWidgets`).

Widget geometry uses Unity's canonical anchor/position/size representation, while the Inspector
presents Pos/Size on fixed axes and edge offsets on stretched axes. Design Scale applies
`localScale` on every axis so child content follows the selected mode; stretched axes compensate the
parent anchor span through `sizeDelta` so final edge offsets scale exactly once. Under Scale With
Screen Size the authoritative canvas rect is oriented to the active slot and mapped from
`CanvasScaler` reference units into the slot Design Canvas; raw screen pixels are not mixed into
widget scaling.

Widget bulk copy loops `ResponsiveRectStateEditorUtil.CopySlotAcrossWidgets`; camera bulk copy loops
`ResponsiveCameraEditorUtil.CopySlotAcrossLayouts`; scroll uses
`ResponsiveScrollEditorUtil.CopySlot`. Mutation is deferred outside the IMGUI event. Controller and
camera preview APIs apply only the edited component, and capture suppression prevents preview from
writing back into serialized slots. Widget Reset keeps Size Delta.

Slot foldouts and bulk-copy menus read **Show Other Slots** / Override from the Canvas
`ResponsiveUI` hub. An empty Hub field auto-assigns that Canvas (disabled included) or the
`rootCanvas` hub — never a random scene hub (`ResponsiveUiHub.Resolve`, `EnsureHubField`). Runtime
`OnEnable` and `OnTransformParentChanged` use the same empty-Hub resolve, so a prefab spawned under
the Canvas binds without a serialized scene reference. Active Objects and Events Inspector sections
use `ColoredFoldoutSection` (Foldout, not Foldout Header) so Unity 6 lists are not nested Foldout
Headers.

Editor helpers: `SeedSlots`, `ResetNewListRow`, `ForEachNewRow`, `SeedTwoSideWidget`, `CopyFallback`
/ `FindFallbackSource`, `GetNewRows`, `SlotAllowed`, `ApplyListFoldouts`, `Copy` / `Paste` / `Reset`,
`ResetSlot`, `CopySlotAcrossWidgets`, `CopySlotAcrossTwoSideWidgets`, `CopySideAcrossTwoSideWidgets`,
`CopyWidgetSideAcrossTwoSideWidgets`, `SwapWidgetSideAcrossTwoSideWidgets`, and
`MirrorHorizontally`. Controller widgets use `IsExactLayoutSlot` / `IsSeedDisabledSlot`. Unity list
`+` on widgets, cameras, two-side tween, Active Objects, and backgrounds runs `ForEachNewRow`
**before and after** the list draws so the duplicated previous row is cleared
(`ResetNewListRow`), and the remembered list size survives an Inspector rebuild. Camera rows contain
one optional Camera Background field. `ResponsiveScrollView` has no item list. Active Objects live
on the controller; Events live on the hub.

Component-by-component reference: [./COMPONENTS.md](./COMPONENTS.md).

## Integrating into a project

Three things a host project owns. Nothing in the package assumes a particular scene name or boot
order.

1. **Host a persistent posture detector.** Fold/Flip postures come from one
   `FoldablePostureDetector`. Put it on the first scene that loads (a boot or login scene), set its
   Device Kind, and tick **`persistAcrossScenes`** so it `DontDestroyOnLoad`s and later scenes
   inherit the same owner. Duplicate copies in later scenes destroy themselves without stealing the
   native listener. Boot through that scene on device, or Android HalfOpened and Fold Cover
   HalfOpened never arrive. **Responsive UI → Scene → Create Fold & Flip Setup** adds a detector
   (and, optionally, a Simulator overlay).
2. **Add the hub to a Canvas.** One `ResponsiveUI` component per Canvas that should respond; its
   clients (`ResponsiveUiController`, `ResponsiveCamera`, `ResponsiveScrollView`,
   `ResponsiveTextBanner`) resolve it from that Canvas automatically.
   **Responsive UI → Scene → Create Sample Canvas** builds a wired example.
3. **Author the slots you ship**, then save the scene. Slots a project leaves disabled fall back as
   described above, so a project that only ships Phone and Pad needs no Cover work.

Where settings land:

| What | File | Read by |
|------|------|---------|
| Design Canvas references, Background Cover Size, hinge guide | `ProjectSettings/ResponsiveUiSettings.asset` | Editor only |
| Android Fold / Flip pose rules | `Assets/Resources/ResponsiveUI/FoldableSettings.json` | runtime (TextAsset, loads on every build target) |
| iPhone Duo pose rules | `Assets/Resources/ResponsiveUI/IphoneDuoSettings.json` | runtime (TextAsset, loads on every build target) |

Both JSON files are optional: a missing file or field falls back to the built-in defaults. The
`ProjectSettings` asset is not part of the exported package, so each project picks its own reference
devices. Details: [./SETTINGS.md](./SETTINGS.md).

## C# conventions

### Regions

Production C# uses a few short `#region` names so Inspector, Apply, capture, and Simulator code can
be collapsed while debugging. Prefer **3–5 regions per file**; skip regions on tiny enum-only files
and generated `*.g.cs`.

| Region | Contents |
|--------|----------|
| Types / Constants / Serialized | Nested types, literals, Unity YAML fields (**do not reorder Serialized**) |
| Lifecycle / Detection / Apply | Enable, form-factor resolve, layout write |
| Slot enabled / Capture / LivePush | Inspector Enabled, canvas ratio, Scene ↔ slot sync |
| Foldable / Simulator / Catalog | FoldAndFlip + Device Simulator + device catalogs |
| Inspector / Helpers | CustomEditors, drawers, Project Settings, shared utilities |

### Naming

| Area | Convention |
|------|-----------|
| Layout types | `Responsive…` prefix in `FDPlus.ResponsiveUI` |
| Fold/flip types | `Foldable…` prefix in `FDPlus.Foldable`; the Device Simulator reflection seam keeps its descriptive names (`SimulatorDeviceBridge`, `IDeviceSimulatorSelectionAdapter`) |
| iPhone Duo types | `IphoneDuo…` prefix in `FDPlus.IphoneDuo` |
| PhonePad types | `PhonePad…` prefix in `FDPlus.PhonePad.Editor` |
| Methods | plain verbs — `Resolve…`, `Find…`, `Apply…`, `Compute…`; no `Try` prefix |
| Menus | registered through `ResponsiveUiEditorMenus`, never a literal `[MenuItem]` string |
| Paths | built through `ResponsiveUiEditorPath` |

### Other

- Device Simulator profiles are authored in `Editor/Devices/Profiles/*.json`; `.device` files mirror
  those values for the Simulator. Profiles are authoritative for identity and dimensions, and the
  validators fail on drift between the two sides rather than regenerating either.
- Profile IDs are public, stable identifiers and must not be renamed.
- Script `.meta` GUIDs move with the files, so scene and prefab YAML keeps binding when code is
  reorganized within an assembly.

## See also

| Doc | For |
|-----|-----|
| [./CONCEPTS.md](./CONCEPTS.md) | the layout model the code implements |
| [./COMPONENTS.md](./COMPONENTS.md) | per-component reference |
| [./PLATFORMS.md](./PLATFORMS.md) | per-platform behavior and how to add an adapter |
| [./TESTING.md](./TESTING.md) | EditMode suites and the PowerShell validators |
| [./PRODUCTION.md](./PRODUCTION.md) | what ships and the release gates |
| [../FoldAndFlip/README.md](../FoldAndFlip/README.md) · [../IphoneDuo/README.md](../IphoneDuo/README.md) · [../PhonePad/README.md](../PhonePad/README.md) | module pages |
