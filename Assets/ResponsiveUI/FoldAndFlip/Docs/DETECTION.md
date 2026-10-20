# Detection model

What this package can and cannot detect on real **Android** devices and on **iPhone Duo**, how the
Editor and Device Simulator product states relate to those signals, and where to read the upstream
SDKs.

Which forms each platform reaches, and the per-platform behaviour built on top of this:
[../../Docs/PLATFORMS.md](../../Docs/PLATFORMS.md).

## What Android actually exposes

Jetpack WindowManager `FoldingFeature` has **only two postures**:

| API value | Package product state (typical) |
|-----------|---------------------------------|
| `FoldingFeature.State.FLAT` | **Opened / Expand** |
| `FoldingFeature.State.HALF_OPENED` | **Half Opened / Flex** |
| *(no `FoldingFeature`)* | **Ambiguous** — ordinary phone, closed foldable, cover display, or startup |

There is **no** official `CLOSED` / `FOLDED` value on `FoldingFeature`. Unity's built-in wrapper
states the same: `AndroidApplication.currentFoldingFeatures` returns an **empty array** on
non-foldables **and** on closed foldables.

### Extra signals this package uses

| Signal | Source | Maps to |
|--------|--------|---------|
| Hinge orientation | `FoldingFeature.orientation` | Tabletop vs Book (with Half Opened) |
| Separating | `FoldingFeature.isSeparating` | Dual-screen **Spanned** vs **Single pane** |
| Rear / cover session ACTIVE | `WindowAreaController` + `TYPE_REAR_FACING` + `OPERATION_TRANSFER_ACTIVITY_TO_AREA` | **Closed / Normal** (while the rear transfer is active) |
| Current ≪ max / peak / largest **built-in** display (`DEFAULT_DISPLAY` + rear displays on API 34+; the Activity's display is not used when it differs, e.g. desktop mode / external / virtual) | Size ratio (&lt; 70% area, or a narrower soft rule) | Best-effort cover |
| Device Kind Fold / Flip + current ≪ that kind's open size | No `FoldingFeature` | Best-effort cover (cold start on the cover) |
| Small near-square panel | Size heuristic | Best-effort Flip cover |
| Multi-window mode | Host Activity | Context only; **not** a posture or Closed signal |
| Picture-in-picture mode | Host Activity | Context only; **not** a posture or Closed signal |
| Catalog / Simulator product buttons | Editor only | Explicit Closed / Opened / Half Opened |

## Package detection contract

### On device (Android player)

```text
WindowArea rear ACTIVE                 → Closed / Normal        (when a transfer session exists)
FoldingFeature HALF_OPENED             → Half Opened / Flex     (reliable; never size-probed)
FoldingFeature FLAT                    → Opened / Expand        (reliable; never size-probed)
  └─ exception: rear ACTIVE + isCoverScreen while still FLAT → Closed / Normal
No FoldingFeature after live HalfOpened → clear HalfOpened; preserve Flex unless Closed, never in multi-window / PiP (hinge bounds cleared)
No FoldingFeature after live Expand/Flat → clear Flat to Undefined (so cover heuristics can run)
DualScreen + !isSeparating             → Single pane (Closed)   (including no FoldingFeature)
Current ≪ max / session peak / largest built-in display → Closed (ratio &lt; 70%, or a narrower soft rule)
Device Kind Fold/Flip + current ≪ open size → Closed            (only when FoldingFeature is missing)
Flip-sized panel, no feature           → Closed                 (best-effort; Undefined state only)
No FoldingFeature + no corroborated cover hint → Undefined      (ordinary Android keeps hardware Phone / Pad)
Unity Screen size change               → bridge refresh         (metrics catch-up when Jetpack is late)
```

#### When cover size / kind probes run

| Live `FoldableState` | Size / peak / display probe | Device Kind open-size probe | Flip small-panel probe |
|----------------------|-----------------------------|-----------------------------|------------------------|
| `HalfOpened` | No (Flex owns the posture) | No | No |
| `Flat` | No, unless `isCoverScreen` is already set (rear ACTIVE) | No | No |
| `Undefined` (no `FoldingFeature`) | Yes | Yes (Fold / Flip kind only) | Yes |

Stale **Flat** or **HalfOpened** must not survive a missing `FoldingFeature`. Jetpack has no `CLOSED`
value; on the cover the feature array is empty (Unity documents the same). Keeping Expand's Flat maps
to Opened / Pad; keeping Flex's HalfOpened blocks Device Kind open-size cover detection and leaves
Normal stuck on HalfOpened slots. `ShouldPreserveHalfOpened` restores Flex only when the cover
heuristics do not win. Live Jetpack `HALF_OPENED` / `FLAT` are never size-probed into Closed. The one
exception: a rear-display session may keep `isCoverScreen` while the state is still `FLAT` (device
open, Activity on the cover).

When Unity's `Screen` updates before `WindowInfoTracker`, the Java side drops a lagging
`FoldingFeature` only if the current metrics are cover-sized against the session peak or the largest
built-in display **and** the hinge no longer spans the current window — same length **and** centre
inside the window-relative bounds (`isStaleFoldingFeatureForCover`; the C# mirror is
`FoldableCoverHeuristics.IsStaleFoldingFeature`). Letterboxed / size-compat windows keep live `FLAT` /
`HALF_OPENED`, because Jetpack reports the hinge across them. Live `HALF_OPENED` forces
`isSeparating` (Google's contract). The session peak is remembered only on live Flat / HalfOpened and
survives an Activity rebind, so Expand → Normal still has an inner reference.

Set **Device Kind** on `FoldablePostureDetector` (Fold / Flip / Dual) so the cover ratio fallbacks
work on a cold start. `DeviceKind` is configured by the application; Android does not classify
hardware as Fold vs Flip. New detectors default to `Unknown`, which keeps an ordinary Android phone
from being classified as a foldable merely because the Jetpack bridge is available, and keeps a small
freeform window from being read as proof of a cover. Set a concrete kind only for a verified device
family.

**HalfOpened on the Android player** needs an active `FoldablePostureDetector`. With
`persistAcrossScenes` (DontDestroyOnLoad) one detector in the persistent scene serves every later
scene, so gameplay scenes receive `LayoutChanged` without a per-scene owner. Boot through the scene
that hosts the detector on device; a scene loaded without an owner reaching it has no posture source.

### Kind × product → form

`ResponsiveDevice.MapFoldableProduct(kind, product)`, then `RefineCoverForms` for the WxH upgrades:

| Device Kind | Closed / Normal | Opened / Expand | HalfOpened / Flex |
|-------------|-----------------|-----------------|-------------------|
| **FoldablePhone** | Phone → **Fold Cover** (`FoldPhoneCover`) at the passport cover WxH; **iPhone Duo Cover** (`iPhoneDuoCover`) at the Duo cover WxH, and for any Closed on an iOS player | **Pad** | **Fold HalfOpened** → **iPhone Duo Seated** (`iPhoneDuoSeated`) at the Duo inner WxH, and for any HalfOpened on an iOS player; else **Fold Cover HalfOpened** at the passport-fold inner WxH |
| **FlipablePhone** | **Flip Cover** | **Phone** | **Flip HalfOpened** |
| **DualScreen** | **Phone** (single pane) | **Pad** (spanned) | **Fold HalfOpened** (Flex reuses the Fold slots) |
| **Unknown** | Phone (not forced Closed) | Phone | Phone |

Fold **Normal** is therefore Phone first, and the passport cover size upgrades it to Fold Cover. Tall
covers stay Phone. Android players skip the iPhone Duo size bands altogether
(`ResponsiveDevice.MatchesIphoneDuoSizes`). Full slot authoring:
[../../Docs/PLATFORMS.md](../../Docs/PLATFORMS.md).

### On device (iPhone Duo, iOS 27.1)

UIKit `UIHingeInteraction` reports `closed` / `partiallyOpen` / `fullyOpen` / `unknown` (plus an
angle, used only when the status is missing or Unknown), once on attach and on every change. The
bridge exposes the status only. `IphoneDuo/Runtime/Plugins/iOS/IphoneDuoHinge.mm` adds the
interaction to the Unity view, `IphoneDuoHinge` maps it, and the detector polls on a hinge-version or
`Screen` change (the app moves between displays without backgrounding). The hinge version bumps only
when the status changes. When the status is missing or Unknown it comes from the angle with
hysteresis: Closed is entered at ≤ 10° and left above 20°; fully open is entered at ≥ 170° and left
below 160°.

```text
Cover + partiallyOpen                   → Closed / Standing  (outer display; iPhone Duo Cover, not Seated)
Cover + closed / unknown / fullyOpen    → Closed / Normal
Inner tall (2007×2853) + partiallyOpen  → Seated Portrait (laptop, horizontal crease)
Inner wide (2853×2007) + partiallyOpen  → Seated Landscape (book, vertical crease)
Inner + fullyOpen                       → Opened Landscape or Portrait (Pad)
Inner + unknown                         → by angle (hysteresis), else Opened
No hinge update yet                     → Opened (Pad)
Hinge becomes nil (the view left the
hinge hierarchy, e.g. under an ad)      → keeps the last status
No resolvable UIHingeInteraction        → size-only Closed / Opened (one warning logged)
```

The Seated slot labels name the screen shape, like every other slot outside the Android Fold
half-open pair. The crease runs the other way round from a portrait-native fold
(`FoldableProductStateResolver.LandscapeNativeHalfOpenHinge`). iOS keeps a locked app on its axis
outside Seated; while Seated the app allows only the hold's axis (`IphoneDuoHoldOrientation`), because
iOS reports the tall laptop hold as a *landscape* device orientation and a game that still allows
landscape would stay letterboxed. So the laptop hold fills the tall screen (Seated Portrait) and the
book hold the wide one (Seated Landscape). With the default **Seated rotation = Locked** the Seated
pose never switches while half open, matching the Android Fold half-open default; **Follows hold**
switches it with the hold. All of this needs Player Settings **Default Orientation = Auto Rotation**:
Unity ignores the autorotation flags under a fixed Default Orientation, so such a game keeps its lock
while Seated. Closed and Opened keep the lock. Inspectors show, allow and seed both Seated slots
(`ResponsiveDevice.SlotAllowedByPlayerAxes`); a disabled one falls back by screen shape to Pad, then
Phone. The inner display is 2853×2007 px (951×669 pt @3x) and the fold is 120 px at 3 px/pt. Full
rules: [../../Docs/PLATFORMS.md](../../Docs/PLATFORMS.md#iphone-duo-seated-rules).

The fold is a 40 pt division centred on the inner display (`foldBounds` is in render pixels, so Unity
Target DPI scaling is respected). Hardware gate: the model list in `IphoneDuoHinge` (`iPhone19,4`),
matched against `SystemInfo.deviceModel`, or against `SIMULATOR_MODEL_IDENTIFIER` in the Xcode
Simulator; every other iPhone and iPad keeps `IsRuntimeSupported` false. An unlisted model keeps
`IsRuntimeSupported` false and logs nothing (add new identifiers to the model list); a listed device
without a resolvable `UIHingeInteraction` logs one warning and stays size-only. Pausing the app
disables the interaction and resuming re-enables it, so UIKit reports a fold made while the app was
in the background. iOS has no multi-window / PiP flags in the snapshot.

`partiallyOpen` spans the whole range between closed and 180°, so Half Opened stays on until the
inner display is flat. UIKit's own containers instead follow the `.division` reserved region's
`isActive`, which can switch on later (roughly 98–173° in the iOS 27.1 simulator). That flag is
Swift-only (`UIView.reservedRegions`), so the Objective-C++ bridge cannot read it; confirm the
Half Opened ↔ Opened boundary on hardware.

### Other players (other iPhones / iPads, desktop, WebGL)

No fold / flip bridge runs on these players. Other iPhones and iPads: Phone / Pad. Windows / macOS /
Linux / WebGL: Wide Screen only, sized by the window or browser, with no mobile detection at all.

The full form table and how another OS adapter plugs in without rewriting slots:
[../../Docs/PLATFORMS.md](../../Docs/PLATFORMS.md).

### In the Editor (Device Simulator / catalog)

Product-state buttons are **authoritative within simulation only**:

- **Closed / Normal** → cover / outer screen, with the sizes from `Editor/Devices/Profiles/*.json`.
- **Opened / Expand** → inner / main display + `FLAT`.
- **Half Opened** → inner / main display + `HALF_OPENED` + the hinge defaults. Flip tabletop is
  horizontal in portrait and vertical after a landscape turn (`ToScreenSpaceHalfOpenHinge`). Fold
  slots are named by posture, not screen shape: Landscape is the flat inner turned 90° (tall,
  horizontal hinge, tabletop), Portrait the flat screen (wide, vertical hinge, book). By default —
  the platform's own behaviour — a single-axis Player lock picks the pose (landscape-only →
  Landscape, portrait-only → Portrait), and with both axes allowed the slot follows the screen shape,
  as rotating does on a real fold. The Android Fold / Flip pose cards can change that (below).
  iPhone Duo (`innerLandscapeNative`) is the exception: its Seated crease runs the other way for the
  same screen shape — Portrait is the tall 2007×2853 laptop with a horizontal hinge, Landscape the
  wide 2853×2007 book with a vertical hinge (`LandscapeNativeHalfOpenHinge`,
  `ToScreenSpaceHalfOpenHinge`), while the slot label still names the screen shape. A one-axis Auto
  Rotation lock never forces a Seated pose (Seated allows only the hold's axis), so the iPhone Duo
  panel starts Seated in the laptop hold and switches to the book hold from its hold control or a
  screen / chrome rotation; a fold profile's laptop hold switched to the Duo keeps the laptop hold. A
  fixed Default Orientation keeps the lock, as on device.
- **Android orientation hold for changed pose cards** (`FoldableOrientationHold`, FoldAndFlip
  runtime, device only). The Project Settings › Responsive UI › Android Fold / Android Flip cards
  (`Assets/Resources/ResponsiveUI/FoldableSettings.json`, `FoldableSettings` /
  `FoldableHalfOpenRules`; an absent file or field means the Android defaults) can change what
  rotating does while half open. Only a card that differs from its default is acted on, only while
  half open, and only when Player Settings **Default Orientation = Auto Rotation** with more than one
  orientation allowed (Auto Rotation with exactly one allowed counts as fixed, because Android then
  requests a fixed orientation, and the defaults apply). The hold pins an axis: Auto Rotation with
  that axis's two autorotate flags, so a 180° turn stays upright. Each half-open entry waits about 10
  stable frames before the first hold; after that first hold the screen shape is checked once (a
  wrong shape must stay stable for 10 frames and 1.5 s) and the axis mapping is swapped at most once.
  Leaving half open restores the Player Settings flags and orientation exactly once. Fold vs Fold
  Cover is chosen by the detected half-open size (`FoldableOrientationHold.IsFoldCover`, registered by
  the Responsive UI runtime). With default cards the hold does nothing.
- Selecting a known Fold / Flip / Dual Simulator profile immediately applies its catalog `DeviceKind`
  and, after the screen commits, its complete layout to the active `FoldablePostureDetector`. This is
  a nonserialized Editor override: it also reaches a detector enabled after the selection, and
  clearing the Simulator profile restores the authored Android player hint.

Use this path to design all three product states without relying on cover heuristics. Catalog
profiles are simulator fixtures, not physical-device certification and not runtime detection
evidence. Selecting a Flip profile from a Phone or Fold profile starts **Opened** (main display).
Flip Cover is first in the catalog, but that is not Closed unless you pick Closed or switch from
another Flip that was already Closed.

## Runtime pipeline

1. `FoldablePostureDetector` binds `FoldableHelper` to Unity's current Android Activity (prefer one
   owner; pause / resume start and stop the listeners). With `persistAcrossScenes` the owner in the
   persistent scene survives later scene loads as the DontDestroyOnLoad singleton; duplicates destroy
   themselves without stealing ownership.
2. Rebinding a different Activity clears the previous `WindowLayoutInfo` before publishing. Pause /
   `stop()` keeps the cached `WindowLayoutInfo` but flags snapshots `awaitingFreshLayout` until the
   first fresh callback, and the detector's `FoldableFreshLayoutGate` keeps the previous product for
   up to 0.5 s unscaled, so a resume neither replays a stale posture nor flickers
   Expand → Undefined → Expand.
3. Jetpack `WindowInfoTracker` → `FoldingFeature` + window metrics.
4. Optional `WindowAreaController` → `isCoverOrRearDisplay` when the rear transfer is ACTIVE.
5. `FoldableProxy.onChanged` → `FoldableLayoutState` (including the multi-window / PiP context). Java
   packs the snapshot into one string (`FoldableLayoutInfo.toPayload`) and posts it to the main
   looper, so the Unity proxy never reads Java fields from inside `CallStatic` (that re-entry ANRs in
   `WindowInfoLayoutListener.onChanged`). Hinge bounds are converted from Android's top-left origin to
   Unity's bottom-left origin there (`FoldableLayoutState.FromAndroidTopOrigin`); Editor Simulator
   bounds are already bottom-origin.
6. The Android-player-only detector poll copies the latest native snapshot, then
   `Enrich(..., previousProduct)` so a missing `FoldingFeature` does not flash Expand. An empty native
   hinge or Device Kind does not wipe a live HalfOpened split. Duplicate native content is coalesced
   so Responsive UI does not re-layout unchanged slots; an explicit Simulator Apply still raises
   `LayoutChanged` and can force a refresh. The detector raises posture events only — no camera or
   canvas hinge apply.
7. `LayoutChanged` → each Canvas `ResponsiveUI` hub re-detects and runs a full Apply, which notifies
   every client in the same pass (controllers, the two-side tween, cameras, scroll views, text
   banners). Only a `ResponsiveScrollView` with **no** hub listens to `LayoutChanged` itself
   (`ResponsiveScrollDriver`). Shared lookup: `FoldablePostureSource`.

Key files:

- `Runtime/Plugins/Android/Foldable.androidlib/.../FoldableHelper.java`
- `Runtime/Plugins/Android/Foldable.androidlib/.../FoldableWindowAreaTracker.java`
- `Runtime/FoldableProxy.cs`
- `Runtime/FoldableProductStateResolver.cs`
- `Runtime/FoldableCoverHeuristics.cs`
- `Runtime/FoldablePostureDetector.cs`
- `Runtime/FoldablePostureSource.cs`

## Dependencies

```gradle
implementation 'androidx.window:window:1.4.0'
implementation 'androidx.window:window-java:1.4.0'
```

(in `Runtime/Plugins/Android/Foldable.androidlib/build.gradle`)

## What not to copy blindly

Blog snippets that map:

```text
FoldingFeature == null  →  isFolded / Closed = true
```

…are **unsafe**. The same "null feature" result occurs on:

- ordinary (non-folding) phones,
- closed foldables,
- cover displays without a rear-transfer session,
- the brief moment before the first `WindowLayoutInfo` callback.

This package **does not** use that shortcut.

## Official SDKs and docs

### Google / AndroidX

- [Jetpack WindowManager releases](https://developer.android.com/jetpack/androidx/releases/window)
- [Make your app fold aware](https://developer.android.com/develop/adaptive-apps/guides/foldables/make-your-app-fold-aware)
- [Support foldable display modes (rear / dual)](https://developer.android.com/develop/adaptive-apps/guides/foldables/support-foldable-display-modes)
- [FoldingFeature](https://developer.android.com/reference/androidx/window/layout/FoldingFeature)
- [FoldingFeature.State](https://developer.android.com/reference/androidx/window/layout/FoldingFeature.State) — `FLAT` / `HALF_OPENED` only
- [WindowAreaController](https://developer.android.com/reference/androidx/window/area/WindowAreaController)
- Codelab: [WindowManager dual-screen & foldables](https://developer.android.com/codelabs/android-window-manager-dual-screen-foldables)
- Codelab: [Camera + foldables](https://developer.android.com/codelabs/android-camera-foldables)

### Unity

- [Large screen and foldable device support](https://docs.unity3d.com/Manual/android-large-screen-and-foldable-support.html)
- [AndroidFoldingFeature](https://docs.unity3d.com/ScriptReference/Android.AndroidFoldingFeature.html)
- [AndroidApplication.currentFoldingFeatures](https://docs.unity3d.com/ScriptReference/Android.AndroidApplication-currentFoldingFeatures.html) — empty on closed **and** non-folding devices
- [onFoldingFeaturesUpdated](https://docs.unity3d.com/ScriptReference/Android.AndroidApplication-onFoldingFeaturesUpdated.html)

### Samsung

- [Flex mode overview](https://developer.samsung.com/galaxy-z/flex-mode.html)
- [Implement Flex Mode into a Unity game](https://developer.samsung.com/codelab/gamedev/flex-mode-unity.html)
- [How to use Jetpack WindowManager in Android game development](https://developer.samsung.com/galaxy-gamedev/blog/en/2022/07/20/how-to-use-jetpack-window-manager-in-android-game-dev)
- [Foldable features with Unity / Unreal](https://developer.samsung.com/galaxy-gamedev/blog/en/2022/09/16/foldable-device-features-with-unity-and-unreal-engine-4)
- [Rear display mode with WindowManager](https://developer.samsung.com/sdp/blog/en/2024/09/26/unfold-the-potential-of-galaxy-fold-devices-with-windowmanagers-rear-display-mode)

### Microsoft / dual-screen

- [isSeparating explained](https://devblogs.microsoft.com/surface-duo/isseparating-foldable-jetpack-window-manager/)
- [Surface Duo Window Manager samples](https://github.com/microsoft/surface-duo-window-manager-samples)
- [Surface Duo SDK](https://github.com/microsoft/surface-duo-sdk)
- [Surface Duo Unity samples](https://github.com/microsoft/surface-duo-sdk-unity-samples)

### Web (a related posture model)

- [W3C Device Posture API](https://github.com/w3c/device-posture)
- [Samsung Internet webcam fold starter kit](https://github.com/SamsungInternet/webcam-fold-starterkit)

### Sample code worth reading

| Repository | Focus |
|------------|-------|
| [android/platform-samples …/windowmanager](https://github.com/android/platform-samples/tree/main/samples/user-interface/windowmanager) | Official Jetpack sample |
| [android/large-screen-codelabs …/WindowManager](https://github.com/android/large-screen-codelabs/tree/main/WindowManager) | Fold / dual codelab sources |
| [CesarValiente/isseparating-sample](https://github.com/CesarValiente/isseparating-sample) | `isSeparating` only |
| [microsoft/surface-duo-window-manager-samples](https://github.com/microsoft/surface-duo-window-manager-samples) | Dual-screen layouts |
| [androidx/constraintlayout](https://github.com/androidx/constraintlayout) | Foldable UI experiments |

## Verifying detection

The EditMode suites and the PowerShell validators that cover this model — including the resolution
audit that mirrors the native `Enrich` product-state detection (kind × Flat / HalfOpened / Undefined ×
cover heuristics) — are listed in [../../Docs/TESTING.md](../../Docs/TESTING.md). Those checks are
static and in-process only. For production evidence, run the EditMode suite, a minified Android
release build, and [the physical-device matrix](../../Docs/DEVICE-TESTING.md); the shipping guarantees
and known limitations are in [../../Docs/PRODUCTION.md](../../Docs/PRODUCTION.md).
