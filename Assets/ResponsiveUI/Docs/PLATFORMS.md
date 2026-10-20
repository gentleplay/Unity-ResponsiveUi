# Platforms

Per-platform behaviour: the form-factor contract, what each build target detects, Android Fold / Flip,
iPhone Duo, Wide Screen, how the Player orientation lock interacts with all of it, and how to add
another OS adapter without inventing new slots.

Model and vocabulary: [CONCEPTS.md](./CONCEPTS.md). Components and their APIs:
[COMPONENTS.md](./COMPONENTS.md). The pose cards and every other project setting:
[SETTINGS.md](./SETTINGS.md). What Android and iOS actually expose, and the detection pipeline:
[DETECTION.md](../FoldAndFlip/Docs/DETECTION.md).

## Form factors (stable contract)

Layout slots are the product contract. A new OS adapter maps **into** these; it does not add a
parallel slot list. The enum value is the slot order (`ResponsiveWidgetLayout.FormFactors`): form *i*
owns slots 2*i* (Portrait) and 2*i* + 1 (Landscape) — ten forms, twenty slots.

| Value | `ResponsiveDevice.FormFactor` (Inspector) | Platform | Hardware / posture |
|------:|-------------------------------------------|----------|--------------------|
| 0 | `Phone` (**Phone**, iOS target: **iPhone**) | Android, iOS | Tall phone; tall foldable cover; Flip opened flat |
| 1 | `Pad` (**Pad**, iOS target: **iPad**) | Android, iOS | Tablet; Fold inner flat; iPhone Duo inner flat |
| 2 | `FoldPhoneCover` (**Fold Cover**) | Android | Passport-shaped fold cover (1248×1972 class) |
| 3 | `FoldHalfOpened` (**Fold Half Opened**) | Android | Fold inner, half open (tabletop / book) |
| 4 | `FoldCoverHalfOpened` (**Fold Cover Half Opened**) | Android | Half-open inner of a passport-cover fold |
| 5 | `FlipPhoneCover` (**Flip Cover**) | Android | Flip closed cover (948×1048 class) |
| 6 | `FlipHalfOpened` (**Flip Half Opened**) | Android | Clamshell, half open |
| 7 | `iPhoneDuoCover` (**iPhone Duo Cover**) | iOS | iPhone Duo closed, outer display (1398×2034); also Standing |
| 8 | `iPhoneDuoSeated` (**iPhone Duo Seated**) | iOS | iPhone Duo inner, half open |
| 9 | `WideScreen` (**Wide Screen**) | Windows, macOS, Linux, WebGL | Any window or browser; orientation from its live aspect |

Orientation is always **Portrait** or **Landscape** on top of the form.

**Naming rule.** Every form names the **screen shape**, with one exception: **Fold Half Opened** and
**Fold Cover Half Opened** name the **posture** (`ResponsiveDevice.OrientationForForm` inverts them).
Their Landscape slot is the tall screen with a **horizontal** hinge (the flat inner turned 90°,
content top to bottom); their Portrait slot is the wide screen with a **vertical** hinge (the flat
inner as in Expand, content left to right). Sizes derived from a slot use the screen shape
(`ResponsiveUI.OrientSizeForSlot`), so design canvases and Canvas Scaler references need no special
case.

| Form | Slot | Screen | Fold axis | Posture |
|------|------|--------|-----------|---------|
| Fold Half Opened / Fold Cover Half Opened | **Landscape** | tall (e.g. 1848×2448) | horizontal | tabletop |
| Fold Half Opened / Fold Cover Half Opened | **Portrait** | wide (e.g. 2448×1848) | vertical | book |
| Flip Half Opened | Portrait / Landscape | by screen shape | horizontal in portrait, vertical after a landscape turn | clamshell |
| iPhone Duo Seated | **Portrait** | tall 2007×2853 | horizontal | laptop |
| iPhone Duo Seated | **Landscape** | wide 2853×2007 | vertical | book |

The iPhone Duo inner display is landscape-native, so its crease runs the other way round from a
portrait-native Android fold for the same screen shape
(`FoldableProductStateResolver.LandscapeNativeHalfOpenHinge`). That is why Seated has its own form: a
Fold Cover Half Opened Landscape layout authored for a tabletop split would put the Duo crease
through the wrong panes.

## What each build target detects

Each build uses one slot family, from the build target (`UNITY_ANDROID` / `UNITY_IOS`, which the
Editor also defines for the active target; everything else is Wide Screen —
`ResponsivePlatforms.BuildPlatform`).

| Build target | Forms detected and shown |
|--------------|--------------------------|
| **Android** | Phone, Pad, Fold Cover, Fold Half Opened, Fold Cover Half Opened, Flip Cover, Flip Half Opened |
| **iOS** | Phone (iPhone), Pad (iPad), iPhone Duo Cover, iPhone Duo Seated |
| **Windows / macOS / Linux / WebGL** | Wide Screen only |

- A player never returns another platform's form (`ResponsivePlatforms.ConstrainDetected` /
  `Constrain`): another platform's cover or Flip half-open form becomes **Phone**; its inner
  half-open form, iPhone Duo Seated and Wide Screen become **Pad**; a Wide Screen build is always
  Wide Screen.
- In the Editor, a selected Device Simulator profile keeps that device's form on **any** build
  target — Android Fold / Flip profiles, iPhone, iPad and iPhone Duo alike, including on desktop and
  WebGL targets. With no profile selected, detection follows the build target (a Wide Screen target
  stays the window).
- Inspectors list slots in **Android** and **iOS** groups on every build target
  (`ResponsiveUiEditorSettings.SlotVisible`). Phone and Pad sit in the Android group and are the
  iPhone and iPad slots too; iPhone Duo Cover and iPhone Duo Seated are the iOS group; Wide Screen is
  its own group and shows on Wide Screen targets only. Hidden slots keep their data.
- Every hub client takes its layout from the Canvas `ResponsiveUI` hub on each pass
  (`ResponsiveUiController`, `ResponsiveTwoSideUiTweenController`, `ResponsiveCamera`,
  `ResponsiveScrollView`, `ResponsiveTextBanner`), so a profile change or a fold reaches all of them
  in the same Apply. A `FoldablePostureDetector` publishes posture only; it applies no hinge camera
  or canvas rect itself.

### The posture gate

`FoldablePostureDetector.IsRuntimeSupported` is the single gate:

```text
UNITY_EDITOR || UNITY_ANDROID                     → true
other players → FoldablePostureBackend.Current?.IsSupportedDevice
```

The iPhone Duo module registers that backend on iOS players (`IphoneDuoHinge.IsSupportedDevice`:
hardware model `iPhone19,4` only, or the `SIMULATOR_MODEL_IDENTIFIER` model in the Xcode Simulator).
Every other iPhone / iPad and every desktop or WebGL player keeps `IsRuntimeSupported` false, so the
detector is a no-op there and `ResponsiveCamera` keeps owning viewports. The Android JNI bridge is
compiled `#if UNITY_ANDROID && !UNITY_EDITOR`; the native hinge bridge is `#if UNITY_IOS &&
!UNITY_EDITOR` inside the iPhone Duo module.

### Kind × product → form

`ResponsiveDevice.MapFoldableProduct(kind, product)`, then `RefineCoverForms`
(`RefineWideCover` / `RefineFlipPhoneCover` / `RefineFoldHalfOpened`) for the WxH upgrades:

| Device Kind | Closed / Normal | Opened / Expand | HalfOpened / Flex |
|-------------|-----------------|-----------------|-------------------|
| **FoldablePhone** | Phone (→ Fold Cover at passport cover WxH; → iPhone Duo Cover at Duo cover WxH, and for any Closed on an iOS player) | Pad | Fold Half Opened (→ Fold Cover Half Opened at passport-fold inner WxH; → iPhone Duo Seated at Duo inner WxH, and for any HalfOpened on an iOS player) |
| **FlipablePhone** | Flip Cover | Phone | Flip Half Opened |
| **DualScreen** | Phone (single pane) | Pad (spanned) | Fold Half Opened |
| **Unknown** | Phone | Phone | Phone |

Android players skip the iPhone Duo size bands entirely (`ResponsiveDevice.MatchesIphoneDuoSizes`).
Cover resolution details — when size and kind probes run, rear display ACTIVE under `FLAT`, clearing
a stale Flat / HalfOpened — are in [DETECTION.md](../FoldAndFlip/Docs/DETECTION.md).

## Android Fold, Fold Cover and Flip

- **Posture comes from Jetpack WindowManager**, through the `FoldablePostureDetector` in the scene.
  Half Opened needs an active detector on device; with `persistAcrossScenes` one detector survives
  scene loads as the DontDestroyOnLoad owner, so later scenes receive `LayoutChanged` without a
  per-scene owner. Set its **Device Kind** (Fold / Flip / Dual): Android does not classify hardware,
  and `Unknown` keeps an ordinary phone from being treated as a foldable.
- **Opened (Flat)** and **HalfOpened** own Design Scale identity directly
  (`ResponsiveDevice.IsPostureOwnedDesignIdentity`) — no Screen WxH ≈ catalog comparison is needed.
  Closed / Normal still uses the cover size heuristics, because Jetpack has no `CLOSED` state.
- **Fold vs Fold Cover** is chosen by the half-open inner size: a passport-cover fold's inner WxH
  maps to Fold Cover Half Opened, a classic fold's stays Fold Half Opened.
- Compiling or configuring the bridge does not turn a phone or a small multi-window into Pad or
  Cover: hardware Pad classification still needs a real ≥7″ diagonal, and Fold / Flip size
  refinement starts only after a native detector snapshot confirms the device surface.

### Half-open posture rules

**The defaults are the platform's own behaviour.** With the default cards, a single-axis Player lock
picks the pose (landscape-only → the tall Landscape slot; portrait-only → the wide Portrait slot),
and with both axes allowed the slot follows rotation exactly as a real device does — entering half
open keeps the current screen, so a wide flat fold stays wide (the Portrait slot). Nothing in the
package holds an orientation until a developer changes a card.

**Pose cards** live in Project Settings › Responsive UI: **Android Fold**
(`Project/Responsive UI/Android Fold` — one card for *Half opened · Fold*, one for *Half opened ·
Fold Cover*) and **Android Flip** (*Half opened*). Each card sets how the content runs
(`FoldableContentRuns`: As Android gives / Top → down / Left → right) and what turning the device
does (`FoldableHalfOpenRotation`: As Android / Locked / Follow device); the Layout slot and the hinge
line follow from that and are read-only. The full field reference is in [SETTINGS.md](./SETTINGS.md).

A card changes behaviour only when it differs from its default
(`FoldableHalfOpenPose.IsDefault`), and then only under these conditions:

| Condition | Effect |
|-----------|--------|
| Default Orientation = **Auto Rotation** with **more than one** orientation allowed | The changed card applies (`FoldableProductStateResolver.AllowsRuntimeHold`) |
| Auto Rotation with exactly one orientation allowed | Counts as fixed — Android requests a fixed orientation, so the defaults apply |
| A fixed Default Orientation (Portrait, Landscape Left, …) | Cards are read-only in the settings page; the defaults apply |

On device a changed card is enforced by `FoldableOrientationHold` (FoldAndFlip runtime, Android
players only): it holds an **axis**, not one orientation — Auto Rotation with that axis's two
autorotate flags, so a 180° turn still shows upright. Each half-open entry waits for the screen to
settle (`ShapeStableFrames` = 10 frames) before the first hold; after that first hold the shape is
checked once (a wrong shape must stay stable for 10 frames and `ShapeCheckSeconds` = 1.5 s, because
some devices name their axes the other way round) and the mapping is swapped at most once per
session. Leaving half open restores the Player Settings flags and orientation exactly once. Nothing
else in Fold & Flip writes `Screen.orientation`. In the Device Simulator a changed card wins over the
lock pose, and a changed *Follow device* card lets the Simulator rotate under a one-axis lock.

Android cards are stored in `Assets/Resources/ResponsiveUI/FoldableSettings.json`; an absent file or
field reads as the Android defaults.

## iPhone Duo (iOS adapter)

The iPhone Duo is a book-style foldable: a cover display (1398×2034 px) and a **landscape-native**
inner display (2853×2007 px, 951×669 pt @3x) with a 40 pt (120 px) fold division centred on it. No
Unity package exposes the hinge, so this one ships its own bridge in the `IphoneDuo` module.

| Piece | Role |
|-------|------|
| `IphoneDuo/Runtime/Plugins/iOS/IphoneDuoHinge.mm` | Adds a UIKit `UIHingeInteraction` to the Unity view. Class and selectors are resolved at runtime, so it builds with any Xcode and no-ops on older iOS. It maps the system hinge status; only when that status is missing or Unknown does it derive one from the hinge angle with hysteresis (Closed at ≤10°, left above 20°; fully open at ≥170°, left below 160°). A nil hinge — the view left a hierarchy that receives hinge updates, e.g. under a full-screen ad — keeps the last status. It also applies the launch hold written to Info.plist, before any C# runs. |
| `IphoneDuo/Runtime/IphoneDuoHinge.cs` | `[DllImport("__Internal")]` wrapper plus the pure posture mapper (EditMode-tested) and the hardware model list. |
| `IphoneDuo/Runtime/IphoneDuoPostureBackend.cs` | The `IFoldablePostureBackend` registered before the first scene loads. `FoldablePostureDetector` attaches it on owner activation, polls it each frame (it reports a change when the hinge version or the `Screen` size changes) and commits through the same `CommitLayout` path as Android. It also applies the posture rules and publishes `IphoneDuoDevice.State` from what they apply. |
| `IphoneDuo/Editor/IphoneDuoDeviceSimulatorPlugin.cs` | The Device Simulator **iPhone Duo** panel: Closed / Opened / Seated plus the hold. Android profiles use the **Android Fold & Flip** panel instead. |

Posture → slot:

| Pose | Unity view | Hinge | Product | Slot |
|------|------------|-------|---------|------|
| Opened, wide | inner 2853×2007 | fully open | Opened | **Pad · Landscape** |
| Opened, tall | inner 2007×2853 | fully open | Opened | **Pad · Portrait** |
| Closed | cover | closed / unknown / none / even fully open (the status can lag the display switch) | Closed | **iPhone Duo Cover** |
| Standing (cover propped, inverted V) | cover | partially open | Closed | **iPhone Duo Cover** — a cover-sized view is Closed whatever the hinge says |
| Seated, laptop hold | inner 2007×2853 | partially open | HalfOpened | **iPhone Duo Seated · Portrait** (horizontal crease) |
| Seated, book hold | inner 2853×2007 | partially open | HalfOpened | **iPhone Duo Seated · Landscape** (vertical crease) |

Identity and fallbacks:

- Opened / HalfOpened Design Scale identity is posture-owned. The Duo **inner** WxH
  (`IsKnownIphoneDuoInnerSize`) is identity for the Seated slots and for Pad, so a disabled Seated
  slot shows Pad at the same scale as flat. Cover identity stays with the Android passport cover
  (`IsKnownFoldCoverPhoneSize`) only.
- The Duo cover reaches its own **iPhone Duo Cover** slot with normal canvas scaling: on iOS players
  from posture (any Closed or Standing, whatever the render size — Target DPI or
  `Screen.SetResolution`), and in the Editor and Simulator through `MapKnownWideCoverSize`.
- A disabled iPhone Duo Cover slot falls back to Phone. A disabled Seated slot falls back by screen
  shape to **Pad** first, then Phone: Seated Portrait (tall) → Pad Portrait → Pad Landscape → Phone
  Portrait → Phone Landscape; Seated Landscape (wide) → Pad Landscape → Pad Portrait → Phone
  Landscape → Phone Portrait. The other Seated slot is never a candidate — it is the opposite shape.
- Duo Seated Active Objects toggles are plain slots; they do not mirror Pad.

Build with **Xcode on the iOS 27.1 SDK**: an older SDK build runs letterboxed on the inner display.
Verify on hardware before claiming support ([DEVICE-TESTING.md](./DEVICE-TESTING.md)).

### iPhone Duo Seated rules

`iPhoneDuoSeated` (Inspector: **iPhone Duo Seated**) is the inner display, partly open. Like every
other form except the Android Fold half-open pair, its labels name the **screen shape**:

| Slot | Hold | Screen | Crease |
|------|------|--------|--------|
| **iPhone Duo Seated · Portrait** | laptop | tall 2007×2853 | horizontal |
| **iPhone Duo Seated · Landscape** | book | wide 2853×2007 | vertical |

The slot orientation is plain `DetectOrientation(width, height)`, so design canvases, `OrientSize`,
the Canvas Scaler reference axis and the layout ratio need no Duo special case; the built-in Duo
design canvas is 2007×2853, stored tall like every other form.

**Seated allows only the hold's axis.** While the hinge is partly open, a game whose Player Settings
use Default Orientation = **Auto Rotation** allows only the orientation of the current hold
(`IphoneDuoHoldOrientation`, driven by the posture backend), whatever its own lock: laptop hold →
Portrait only, book hold → Landscape only. The reason is that on the inner display iOS reports the
tall laptop hold as a *landscape* device orientation, and a game that still allows landscape stays
letterboxed there. A tall hold always gets **Portrait** — iOS gives an iPhone app no Portrait Upside
Down window — so on the opposite side the Unity view is turned 180° (`ViewTurns`) to keep the game
upright. Both Seated slots are therefore always shown, allowed and seeded
(`ResponsiveDevice.SlotAllowedByPlayerAxes`):

| Player Allowed Orientations (Auto Rotation) | Laptop hold | Book hold | Closed / Opened |
|---------------------------------------------|-------------|-----------|-----------------|
| **Landscape only** | portrait only → tall 2007×2853, **Seated · Portrait** | landscape only → wide 2853×2007, **Seated · Landscape** | landscape only |
| **Portrait only** | portrait only → **Seated · Portrait** | landscape only → **Seated · Landscape** | portrait only |
| **Both axes** | portrait only → **Seated · Portrait** | landscape only → **Seated · Landscape** | both |

Unity's iOS view controller applies runtime autorotation flags (iOS 16+), so the rules need no
Info.plist change once C# runs; the iOS build step only writes the launch keys. Unity ignores those
flags under a **fixed** Default Orientation, so such a game keeps its lock while Seated and reaches
only the Seated slot of its locked axis. Leaving Seated restores the project's flags and iOS rotates
back by itself. Leaving Seated for the cover waits `leaveSeatedDelaySeconds` (0.3 s by default,
because iOS resizes the scene in steps while it rotates and that can read as Closed for a frame);
every other change applies at once.

**Seated rotation** decides whether turning the device changes the hold. **Locked** (the default)
means neither the phone nor the Device Simulator ever switches Seated Portrait ↔ Seated Landscape;
only flattening or closing ends Seated. **Follows hold** is the hold-driven switch (laptop → Seated
Portrait, book → Seated Landscape). The **Locked pose** is **Auto** (the default), Seated Portrait or
Seated Landscape; Auto means a landscape-only game is always Seated Portrait (tall laptop), a
portrait-only game always Seated Landscape (wide book), and a game with both axes keeps the hold it
entered Seated with. Turning to the flipped side of the same hold is still followed, so the game
stays upright. The locked pose fixes the hold; the **Seated (laptop hold)** across-lock rule still
decides that hold's screen — *Rotate* (the default) gives a landscape game the tall Seated Portrait,
*Sideways* keeps the landscape layout turned a quarter.

**Flat (Opened) inner** behaves Android-fold style by default (**Flat rotation = Follow device**):
with a one-axis Player lock, turning the open device to the locked-out axis keeps the game exactly as
it is, full screen and sideways, instead of iOS letterboxing it, and turning it to the other
orientation of the allowed axis flips the game 180° (`IphoneDuoFlatOrientation`, pure state in
`IphoneDuoFlatLock`). It sets the interface orientation lock on Unity's view controller. Each new
hold on the allowed axis releases the lock, requests the upright interface and locks again once iOS
has stayed there for `lockSettleSeconds` (0.5 s) — a 180° rotation requested while locked is reported
by iOS but not shown. The tall hold locks only once the game has been upright since Opened, so a
letterboxed start is never locked; Seated and Closed release the lock. **Flat rotation = Locked**
keeps one flat screen while the open device turns (**Locked pose** Auto / Wide / Tall), and the flat
orientation lock steps aside while that hold owns the screen; the flipped side of the same hold is
still followed. For a one-axis game whose locked pose is its own axis the real hold is kept, and
*Rotate* then behaves like *Sideways*, which avoids a cold-start letterbox
(`IphoneDuoRules.FlatKeepsHold` / `FlatAcrossLockFor`).

**Cold start and the splash.** The splash and the first scenes run before any C#, so iOS builds write
the rules to Info.plist (`IphoneDuoBuildPlist`: `FDPIphoneDuoLaunchHold`, `FDPIphoneDuoFlatRule`,
`FDPIphoneDuoSeatedRule`, `FDPIphoneDuoSeatedRotation`, `FDPIphoneDuoSeatedLockedPose`,
`FDPIphoneDuoFlatRotation`, `FDPIphoneDuoFlatLockedPose`, `FDPIphoneDuoSidewaysQuarterTurns`,
`FDPIphoneDuoTraceLog`, `FDPIphoneDuoModels`) and the native bridge applies the same hold from
launch; the posture backend adopts it on its first frame. For a one-axis game the native launch hold
honours Seated rotation and the Locked pose. Handing a landscape lock back while the inner display is
held tall is not an option: iOS letterboxes a landscape interface requested on a tall-held inner
display, with or without the interface orientation lock.

The full field-by-field reference for these rules, including timings and diagnostics, is in
[SETTINGS.md](./SETTINGS.md); the module page is [IphoneDuo/README.md](../IphoneDuo/README.md).

### Same pose, different slot name

The same physical pose can reach differently named slots on the two platforms, because the Android
half-open forms name the posture while Seated names the screen shape:

| Device | Pose | Screen | Fold axis | Slot |
|--------|------|--------|-----------|------|
| Passport-cover fold | half open, flat turned 90° | tall | horizontal | Fold Cover Half Opened · Landscape |
| Passport-cover fold | half open, flat | wide | vertical | Fold Cover Half Opened · Portrait |
| Classic fold | half open, flat turned 90° | tall | horizontal | Fold Half Opened · Landscape |
| Classic fold | half open, flat | wide | vertical | Fold Half Opened · Portrait |
| iPhone Duo | half open, laptop | tall 2007×2853 | horizontal | iPhone Duo Seated · Portrait |
| iPhone Duo | half open, book | wide 2853×2007 | vertical | iPhone Duo Seated · Landscape |
| Fold / Flip / Duo | closed | cover WxH | — | Fold Cover / Flip Cover / iPhone Duo Cover |
| iPhone Duo | Standing | cover, propped | — | iPhone Duo Cover |
| Fold / Duo | flat | inner WxH | — | Pad |

A landscape-only game half open on a passport-cover fold therefore uses Fold Cover Half Opened ·
Landscape (tall, horizontal crease), and on an iPhone Duo the same physical pose uses Seated Portrait
(tall, horizontal crease). With its default Seated rotation = Locked, the Duo matches the Android
default of not switching poses while half open.

## Wide Screen (desktop and WebGL)

- One form for every window and browser: **Wide Screen Landscape** when the window is wider than
  tall, **Wide Screen Portrait** otherwise. No mobile logic runs at all — no foldable posture,
  hardware, DPI, catalog-size or Simulator pin checks; the watch size is the window or browser size
  (`Screen.width × Screen.height`), and `ResponsiveUiHandler` re-applies on every resize and on a
  WebGL fullscreen toggle.
- Design Scale works against the Wide Screen design canvas (built-in 1080×1920 stored portrait; the
  Landscape slot orients it to 1920×1080) and the live size, so 16:9, 16:10, 21:9 and 4:3 windows
  scale by their ratio. Wide Screen has no Reference Device: Project Settings › Responsive UI edits
  its Design Canvas Size and Default Scale directly, and **Use Pad Design Canvas** copies the Pad
  canvas.
- Wide Screen slots are **enabled on Windows / macOS / Linux / WebGL targets** and **off on iOS and
  Android** (`ResponsivePlatforms.SeedsEnabled`): never detected there, hidden in Inspectors, skipped
  by the slot resolver, and seeded Disabled on mobile targets. They ignore the mobile Player
  orientation lock entirely (`SlotAllowedByPlayerAxes`).
- Wide Screen never reads mobile slots at runtime: a disabled Wide Screen slot falls back only to the
  other Wide Screen orientation, and with neither enabled the host is left unchanged. Enabling or
  seeding a Wide Screen slot in the Editor copies Pad once (Pad same orientation, then Pad other,
  then Phone) as a starting point; after that it is its own data.
- The Canvas Scaler uses its own **Wide Screen** reference (`wideScreenReferenceResolution`, default
  1080×1920 stored portrait, with `wideScreenMatchWidthOrHeight`), never the Phone / Pad references at
  runtime. Because Pad values are in the Pad coordinate space, a Pad seed also copies the hub's Pad
  references into the Wide Screen ones in the same Undo step while they are still the built-in
  default; authored Wide Screen references are left alone. Pair such a seed with **Use Pad Design
  Canvas**.

## Orientation lock

- **Android.** With `android:appCategory="game"` in the host manifest, Android 16 exempts the app
  from the ≥600 dp orientation override, so the manifest orientation is kept on large screens. The
  package's own half-open hold (`FoldableOrientationHold`) runs only for a changed pose card, only
  while half open, and only under Auto Rotation with more than one orientation allowed.
- **iOS, iPhone Duo inner.** Supported orientations are ignored unless `UIRequiresFullScreen` is set
  (Unity: Player Settings → iOS → *Requires Fullscreen*). With that key, an iOS 27.1 SDK build keeps
  its orientation lock, while iOS resizes the scene in steps (TN3192). A landscape-only build then
  fills the inner display in the book pose; held tall while still locked, iOS scales the game into a
  centred letterboxed band — which is why Seated allows only the hold's axis, so the laptop hold
  rotates and fills the tall inner display (Seated Portrait). Closed and Opened keep the lock by
  default. Keep *Requires Fullscreen* on for a one-axis game.
- **Slot visibility follows the lock.** A slot whose orientation the Player does not allow is hidden,
  disallowed and not seeded — except iPhone Duo Seated (both slots always allowed) and Wide Screen
  (not a mobile form). Existing slot data is never auto-enabled or auto-disabled on load: a hidden
  slot keeps what it holds.
- Other iPhones, iPads, desktop and WebGL players keep `IsRuntimeSupported` false and are unaffected
  by any of this.

## Authoring the extra slots

The same slots serve Android foldables and iPhone Duo. Phone and Pad must be correct on **both** iOS
and Android; treat Fold / Flip / Cover / Seated as additive.

1. **Enable the slots you ship.** Android and iOS groups are listed on every Editor build target;
   Wide Screen shows on desktop and WebGL targets. Seeding defaults:
   **Flip Cover Landscape starts Disabled** and reuses Phone until you enable it; Flip Half Opened
   starts Enabled on new or seeded rows when its orientation is allowed; both iPhone Duo Seated slots
   start Enabled; Wide Screen slots start Enabled on Wide Screen targets, copied from Pad once. Leave Cover / Half Opened slots **off**
   until the slot is authored — that reuses Phone, which is better than applying an empty layout.
2. **Preview in the Device Simulator**, on any build target. Fold and Flip profiles use the **Android
   Fold & Flip** panel (Closed / Expand / Half Opened); the iPhone Duo profile uses the **iPhone Duo**
   panel (Closed / Opened / Seated, laptop or book hold). With no device selected on a desktop or
   WebGL target, resize the Game View to preview Wide Screen.
3. **Save the scene.** Android players then apply those slots from Jetpack, iPhone Duo from
   `UIHingeInteraction`. Other iPhones and iPads stay on Phone / Pad.

Half Opened on device needs an active `FoldablePostureDetector` in a loaded scene — put it in your
persistent scene with `persistAcrossScenes` on, and set its Device Kind. Use Active Objects for HUD
roots that are not widget Targets. Fall-back summary: cameras, scroll views, the two-side tween and
`ResponsiveUiController` widgets resolve through `ResponsiveWidgetSlotResolver` — Pad, Cover and Flip
Half Opened fall back to **Phone**; Fold half-open slots fall back to Phone by screen shape only;
iPhone Duo Seated tries Pad first by screen shape; Wide Screen falls back only to its other
orientation.

## How another OS adapter plugs in

The iPhone Duo bridge follows these rules, and a future foldable (an Apple clamshell, another
vendor's hinge API) should too: add a **source** that fills `FoldableLayoutState` and leave the layout
types unchanged.

### Do

| Step | Where |
|------|-------|
| Keep the slots and `MapFoldableProduct` as they are | `FDPlus.ResponsiveUI` |
| Publish `FoldableLayoutState` (`deviceKind`, `productState`, metrics, hinge) | The same shape the Android proxy already uses |
| Call `FoldablePostureDetector.ApplySimulatedLayout` / `FoldablePostureSource.ApplySimulatedLayout`, **or** the same `CommitLayout` path the Android bridge uses | One owner, via `FindActiveInstance` |
| Set `DeviceKind` from app configuration on Android; on iOS from the hardware model table | `IphoneDuoHinge.Models` |
| Report support only for hardware the source really covers (`iPhone19,4` today); `IsRuntimeSupported` reads it through the backend | `IFoldablePostureBackend.IsSupportedDevice` |
| Keep the iOS Phone vs Pad split for non-folding iPhones and iPads | `ResponsiveDevice.DetectType` |
| Fill a physical-device matrix before claiming OEM behaviour | [DEVICE-TESTING.md](./DEVICE-TESTING.md) |

### Do not

- Drive HalfOpened from screen aspect alone, or from rumoured hardware.
- Copy the Jetpack `Foldable.androidlib` onto another platform.
- Apply a hinge `Camera.rect` while `IsRuntimeSupported` is still false — the detector must stay a
  no-op so `ResponsiveCamera` can own viewports.
- Treat Device Simulator profiles as certified hardware specifications.
- Add a new cover size to the Design Scale identity helpers
  (`ResponsiveDesignScale.IsDesignCanvasReferenceWatch`): cover identity stays with the Android
  passport cover, and only a foldable's inner size is identity (Pad and the half-open slots).

### Code split

```text
FoldAndFlip/Runtime/                     # shared posture contract + Android backend
  FoldablePostureDetector.cs             # IsRuntimeSupported is the single gate
  FoldablePostureBackend.cs              # IFoldablePostureBackend seam for non-Android players
  FoldableHalfOpenRules.cs               # pose cards (pure)
  FoldableOrientationHold.cs             # Android players: holds the axis a changed card asks for
  Plugins/Android/Foldable.androidlib/   # Android only
IphoneDuo/Runtime/                       # own assembly, refs FoldAndFlip; compiles everywhere, native iOS only
  IphoneDuoHinge.cs                      # native wrapper + pure posture mapper + model list
  IphoneDuoPostureBackend.cs             # iOS players: registers the backend
  IphoneDuoDevice.cs                     # game-facing State / StateChanged / Rules / IsSupported
  IphoneDuoRules.cs, IphoneDuoSettings.cs# per-posture rules; JSON in Resources (Project Settings page)
  IphoneDuoEvents.cs                     # UnityEvents component (posture, hold, sideways)
  IphoneDuoHoldOrientation.cs            # across-lock rules + Seated hold axis (IphoneDuoHoldLock: pure state)
  IphoneDuoFlatOrientation.cs            # Flat inner: lock + 180° flip (IphoneDuoFlatLock: pure state)
  IphoneDuoDebugOverlay.cs               # development builds / iOS-target Play Mode: on-screen state
  Plugins/iOS/IphoneDuoHinge.mm          # iOS only: UIHingeInteraction → status, launch hold
IphoneDuo/Editor/                        # Project Settings page, Simulator panel, iOS build steps
IphoneDuo/Tests/Editor/                  # rules, lock states, hinge mapping, Info.plist step
```

`ResponsiveUiHandler` and `ResponsiveScrollDriver` already re-apply on `LayoutChanged`; they should
not grow OS `#if`s as long as the detector is the only publisher.

## Related

| Doc | Use |
|-----|-----|
| [CONCEPTS.md](./CONCEPTS.md) | Forms, slots, the fallback chain, design canvas and design scale |
| [COMPONENTS.md](./COMPONENTS.md) | Every runtime component and its API |
| [SETTINGS.md](./SETTINGS.md) | Project Settings › Responsive UI, including every pose card |
| [DETECTION.md](../FoldAndFlip/Docs/DETECTION.md) | What Android and iOS expose, and the detection pipeline |
| [IphoneDuo/README.md](../IphoneDuo/README.md) | The iPhone Duo module: postures, rules, runtime API |
| [DEVICE-TESTING.md](./DEVICE-TESTING.md) | Real-device verification and the simulator recipes |
| [PRODUCTION.md](./PRODUCTION.md) | What ships, guarantees, known limitations |
