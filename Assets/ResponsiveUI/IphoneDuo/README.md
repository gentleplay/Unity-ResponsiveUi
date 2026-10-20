# iPhone Duo

The iPhone Duo module: how the package reads the hinge, which slot each posture reaches, the posture
rules and their defaults, the runtime API and the Editor preview. It is separate from the Android
Fold & Flip module and depends on it only through the shared posture contract.

| Folder | Assembly | Contents |
|--------|----------|----------|
| `Runtime/` | `com.futuredigitalplus.iphoneduo` | `IphoneDuoDevice` (game-facing state, events, rules), `IphoneDuoRules` + `IphoneDuoSettings` (project rules), `IphoneDuoEvents` (UnityEvents component), `IphoneDuoHoldOrientation` / `IphoneDuoFlatOrientation` (apply the rules), `IphoneDuoHinge` (UIKit `UIHingeInteraction` bridge + pure posture mapper), `IphoneDuoPostureBackend` (registers with `FoldablePostureBackend` on iOS players), `Plugins/iOS/IphoneDuoHinge.mm` |
| `Editor/` | `com.futuredigitalplus.iphoneduo.editor` | Project Settings page (`IphoneDuoSettingsProvider`), the Device Simulator **iPhone Duo** panel with its rule preview, and the iOS build steps: the orientation check (`IphoneDuoBuildOrientationCheck`) and the launch settings in Info.plist (`IphoneDuoBuildPlist`) |
| `Tests/Editor/` | `com.futuredigitalplus.iphoneduo.tests.editor` | Rules, hold / flat lock states, published state, `IphoneDuoEvents`, hinge mapping, the Editor preview rules and the Info.plist step |

The runtime assembly compiles on every platform, so game code that uses `IphoneDuoDevice` or the
`IphoneDuoEvents` component also builds for Android, desktop and WebGL. Only the native bridge and
the posture backend are iOS-player code (`#if UNITY_IOS && !UNITY_EDITOR`); elsewhere
`IphoneDuoDevice.IsSupported` is false and the state stays Unknown. The shared posture contract —
`FoldablePostureDetector`, `FoldableLayoutState`, the Simulator session and the device catalog — stays
in [../FoldAndFlip](../FoldAndFlip/README.md); the detector reads this module through
`IFoldablePostureBackend`.

## Postures and holds

| Posture | Display | Slots | Hold |
|---------|---------|-------|------|
| **Cover** (Closed, also Standing) | cover, behaves like an iPhone | iPhone Duo Cover | tall / wide |
| **Seated** (half open) | inner | iPhone Duo Seated | laptop (tall 2007×2853, horizontal fold) → Seated **Portrait**; book (wide 2853×2007, vertical fold) → Seated **Landscape** |
| **Flat** (fully open) | inner | Pad | tall / wide |

The hardware gate is the model list in `IphoneDuoHinge` (`iPhone19,4` today), matched against
`SystemInfo.deviceModel` or, in the Xcode Simulator, `SIMULATOR_MODEL_IDENTIFIER`. On every other
device the posture backend reports no support and the detector stays a no-op. What the hinge API
reports and how it maps to postures:
[../FoldAndFlip/Docs/DETECTION.md](../FoldAndFlip/Docs/DETECTION.md). How the slots, fallbacks and the
Player orientation lock behave: [../Docs/PLATFORMS.md](../Docs/PLATFORMS.md#iphone-duo-seated-rules).

## Rules (Project Settings › Responsive UI › iPhone Duo)

Menu: **Responsive UI › Project › iPhone Duo Settings**, or **Rules…** in the Simulator panel. The
rules are saved to `Assets/Resources/ResponsiveUI/IphoneDuoSettings.json`. That is a TextAsset, so it
loads safely on every build target; without the file the defaults below apply. The page itself — its
pose cards, the Player Settings banner, which options a case can reach — is documented in
[../Docs/SETTINGS.md](../Docs/SETTINGS.md).

The **held across the Player lock** rules apply to a one-axis game held the other way. A one-axis
game has Default Orientation = **Auto Rotation** with only landscape or only portrait allowed. "Held
the other way" means, for example, a landscape game held tall. Unity ignores runtime orientation
changes under a fixed Default Orientation, so the rules then have no effect (the iOS build warns).

| Rule | Default | Options |
|------|---------|---------|
| Cover | **System Default**: iOS shows the game sideways, like any iPhone | Rotate |
| Seated (laptop hold) | **Rotate**: tall full screen, the Seated Portrait slots | Sideways, System Default (letterbox) |
| Seated rotation | **Locked**: rotating never switches Seated Portrait ↔ Seated Landscape | Follows hold: laptop hold → Seated Portrait, book hold → Seated Landscape |
| Seated locked pose | **Auto**: landscape-only game → Seated Portrait, portrait-only → Seated Landscape, both axes → the hold it entered Seated with | Seated Portrait, Seated Landscape |
| Flat | **Sideways**: the game stays as it is, full screen (Android-fold style), including after a cold start or an unfold while held tall | Rotate, System Default (letterbox) |
| Flat rotation | **Follow device**: the Flat rules above | Locked: turning the open device keeps one flat screen (the Locked pose) |
| Flat locked pose | **Auto**: landscape-only game → Wide, portrait-only → Tall, both axes → the screen it had when opened flat (only when Flat rotation is Locked) | Wide, Tall |
| Flat · Flip 180° | **On**: turning to the opposite side flips the game upright | Off: it keeps its orientation |
| Sideways turn | **Left 90°** | Right 90° |
| Launch screen fills the display (iOS build) | **Off** | On: iOS's launch screen fills a hold across the lock instead of being letterboxed. The other axis is listed under `UISupportedInterfaceOrientations~iphone`, so every iPhone launched that way shows the launch screen on that axis and then rotates; iPads are unchanged |
| Timings | Leave Seated 0.3 s, flat lock settle 0.5 s, rotation retry 0.5 s | any |
| Diagnostics | Trace log on (development builds), debug overlay off | |

- **Sideways** keeps the game's own orientation.
  - Turning a flat wide hold tall uses the interface orientation lock, so iOS keeps the game as it
    was.
  - Otherwise — from Seated, a cold start, or an unfold while held across the lock — iOS gets the
    interface of the hold's axis and the Unity view is turned a quarter. Unity renders the game's own
    size and touches follow the turn. A landscape game held tall and a portrait game held wide are
    mirror cases.
    - A tall hold always gets Portrait: iOS gives an iPhone app no Portrait Upside Down window, so
      Portrait is the upright interface on one side of the tall hold and the view turn makes up for
      the other.
  - iOS letterboxes any interface across the hold on the inner display, with or without the lock.
  - The **splash and first scenes** follow the posture rules too. They run before any C#, so iOS
    builds write the Flat and Seated rules to Info.plist (`IphoneDuoBuildPlist`) and the iOS plugin
    applies them from launch, reading the hinge early. The posture code takes the hold over on its
    first frame (or when the posture detector is removed first). Turning the device to the game's own
    axis before then ends it; without a posture detector it ends 20 s after launch with the view
    straight.
- **Rotate** gives the game the hold's shape, so the game needs layouts for that shape. A tall hold
  gets Portrait here too; on the side where Portrait Upside Down would be upright, the view is turned
  180°.
- **System Default** leaves the Player lock as it is.

**Seated rotation** is separate from the across-lock rules: it decides whether turning the device
changes the Seated hold. While Locked, neither the phone nor the Device Simulator switches Seated
Portrait ↔ Seated Landscape; only flattening or closing ends Seated. Turning to the flipped side of
the same hold (laptop → upside-down laptop) is still followed, so the game stays upright. The locked
pose fixes the hold; **Seated (laptop hold)** still decides that hold's screen — Rotate gives a
landscape game the tall Seated Portrait, Sideways keeps the landscape layout turned a quarter. In the
Simulator the iPhone Duo panel's Seated hold control is disabled while Locked, and a chrome turn is
turned back. Stored in `IphoneDuoRules.seatedRotation` / `seatedLockedPose` (runtime:
`IphoneDuoDevice.Rules`). The native launch hold honours both for one-axis games.

**Flat rotation** works the same way for the open device: while Locked, neither the phone nor the
Device Simulator changes the flat screen, and the flipped side of the same hold is still followed so
the game stays upright. It holds in the Simulator, in C# and in the native launch hold, and the Flat
orientation lock steps aside while the Flat hold owns the screen. For a one-axis game whose locked
pose is its own axis the real hold is kept, and Rotate then behaves like Sideways (no cold-start
letterbox). Stored in `IphoneDuoRules.flatRotation` / `flatLockedPose`.

### Info.plist keys (iOS builds)

`IphoneDuoBuildPlist` writes the rules the native launch hold reads before any C# runs. Integer
values are the enum values:

| Key | Type | Values |
|-----|------|--------|
| `FDPIphoneDuoLaunchHold` | bool | true when the rules apply (Auto Rotation; false under a fixed Default Orientation) |
| `FDPIphoneDuoSeatedRule` | int | `IphoneDuoAcrossHold`: 0 Sideways, 1 Rotate, 2 System Default |
| `FDPIphoneDuoFlatRule` | int | `IphoneDuoAcrossHold`: 0 Sideways, 1 Rotate, 2 System Default |
| `FDPIphoneDuoSeatedRotation` | int | 0 Locked, 1 Follows hold |
| `FDPIphoneDuoSeatedLockedPose` | int | 0 Auto, 1 Seated Portrait, 2 Seated Landscape |
| `FDPIphoneDuoFlatRotation` | int | 0 Follow device, 1 Locked |
| `FDPIphoneDuoFlatLockedPose` | int | 0 Auto, 1 Wide, 2 Tall |
| `FDPIphoneDuoSidewaysQuarterTurns` | int | +1 Left 90°, −1 Right 90° |
| `FDPIphoneDuoTraceLog` | bool | the trace rule, and only in a development build |
| `FDPIphoneDuoModels` | array | the hinge hardware model list |
| `UISupportedInterfaceOrientations~iphone` | array | written only when *Launch screen fills the display* is on for a one-axis game: the Player orientations plus the other axis |

A missing key reads as 0 on the native side, so an older build or a project without the step behaves
as the defaults do.

## Runtime API

```csharp
using FDPlus.IphoneDuo;

// Current posture, hold and screen mode; Unknown off an iPhone Duo.
IphoneDuoState state = IphoneDuoDevice.State;
if (state.posture == IphoneDuoPosture.Seated && state.hold == IphoneDuoHold.Tall) { /* laptop */ }

IphoneDuoDevice.StateChanged += s =>
{
    // s.posture: Cover / Seated / Flat; s.hold: Wide / Tall; s.acrossLock: held across the Player lock
    // s.mode: Native / HoldAxis (rotated to the hold) / Sideways (view turned) / Locked (flat lock)
};

// Change the rules at runtime (e.g. from a gameplay option); null reloads the project's rules.
IphoneDuoDevice.Rules.flatAcrossLock = IphoneDuoAcrossHold.Rotate;
IphoneDuoDevice.Rules.sidewaysTurn = IphoneDuoSidewaysTurn.Right90;
IphoneDuoDevice.Rules = null;

bool onDuo = IphoneDuoDevice.IsSupported;
```

Without code, add **Responsive UI › iPhone Duo Events** (`IphoneDuoEvents`) to any GameObject. It has
UnityEvents for Cover / Seated / Flat, the Wide / Tall hold, Sideways started / ended, and any change
(posture, hold, mode). With **Invoke On Enable** the current state also fires when the component is
enabled.

## Editor preview

With the iOS build target and the **iPhone Duo** profile selected in the Device Simulator, the
**iPhone Duo** panel has:

- **Posture:** Closed / Opened / Seated.
- **Hold:** Wide / Tall; Seated: Book / Laptop. The hold is kept across postures, as on the device.
- **Rule line:** the outcome under the project's rules.

The Simulator screen follows the same decision the iOS player makes. Sideways shows the layout of the
game's own axis that the turned game renders — the Device Simulator cannot turn the picture itself.

The panel publishes the outcome as `IphoneDuoDevice.State` (again when Play Mode starts), so
`IphoneDuoEvents` and game code react in Play Mode. Closing the panel publishes the Unknown state.
The Project Settings page previews every posture and hold under the current Player orientation, and
reloads a settings file changed on disk (for example by version control) so the next edit does not
overwrite it.

## Diagnostics

- **Trace log** (development builds): one line per change in the Xcode console.
  - `iPhone Duo flat: …` — device and interface orientation, and the lock.
  - `iPhone Duo hold: turn …` — the view turn (Sideways, or Rotate's 180° on a tall hold).
  - `iPhone Duo: launch hold …` (native) — the splash before C# runs: device and interface
    orientation, hinge status, rule, view turns, window and scene size.
- **Debug overlay** (development builds on an iPhone Duo, and Play Mode on the iOS build target):
  shows the state, screen size, device orientation and rules in the top-left of the safe area. It is
  created only while the flag is on — checked at the first scene, when `IphoneDuoDevice.Rules` is
  assigned, and on each state change — and removes itself when the flag turns off.

Build, install, log and screenshot recipes for the Xcode iPhone Duo simulator, plus the real-device
checks this module owes: [../Docs/DEVICE-TESTING.md](../Docs/DEVICE-TESTING.md).
