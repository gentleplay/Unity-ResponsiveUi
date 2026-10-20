# Responsive UI

Author a uGUI screen once, then let it lay itself out everywhere your game ships. A `ResponsiveUI`
hub on the Canvas classifies the live screen as one of ten **form factors** — Phone, Pad, Fold
Cover, Fold Half Opened, Fold Cover Half Opened, Flip Cover, Flip Half Opened, iPhone Duo Cover,
iPhone Duo Seated, Wide Screen — times Portrait or Landscape, and applies the layout you authored
for that slot to widgets, cameras, scroll views and text banners. Foldables are first class:
posture comes from Jetpack WindowManager on Android, from the UIKit hinge interaction on iPhone
Duo, and from Unity's Device Simulator while you author.

**Version:** 1.0.0 · **Unity:** 2023.1+ · **Package id:** `com.futuredigitalplus.responsiveui` ·
**UI system:** uGUI · **License:** MIT

> **uGUI today, UI Toolkit next.** Every component in this release targets uGUI — Canvas,
> `RectTransform` and TextMeshPro. UI Toolkit (UXML / USS) support is planned. The form-factor
> model, the slot and fallback rules and the posture detection described in
> [Docs/CONCEPTS.md](Docs/CONCEPTS.md) are UI-system agnostic, so that layer is what a UI Toolkit
> implementation will build on.

New here? Read [Docs/CONCEPTS.md](Docs/CONCEPTS.md) once, then follow
[Docs/GETTING-STARTED.md](Docs/GETTING-STARTED.md).

## What you get

| Area | What you get |
|------|--------------|
| **Widget layout** | `ResponsiveUiController` — twenty slots per Target (ten forms × Portrait/Landscape). A slot **Enabled** chooses which layout applies; Disabled Cover / HalfOpened / Pad falls back to Phone. The Target stays as authored. |
| **Design Scale** | Per slot: Width / Height / Auto / UniformMin / UniformMax / IndependentXY against that form's design canvas, so one authored layout maps onto any screen size. Cover / Fold / Flip slot Disabled → Phone design canvas. |
| **Cameras** | `ResponsiveCamera` — viewport, local position / rotation, FOV / orthographic size, Scale From UI Size, Scale Projection, per-slot **Focus Target** / Focus Bounds, and a cover-fit `SpriteRenderer` background. Read-only Design UI Size follows the form's design canvas. |
| **Scroll views** | `ResponsiveScrollView` — per-slot FancyScrollView Cell Interval / Scroll Offset, written onto the sibling host with no code in the host script. |
| **Text banners** | `ResponsiveTextBanner` — auto-width single-line TMP messages that follow a widget slot; Alignment writes the pivot so growing text does not move the banner. |
| **Two-side UI** | `ResponsiveTwoSideUiTweenController` — Left/Right layouts per widget, optional animation-start layouts, duration, delay and an `AnimationCurve`. |
| **Backgrounds** | Cover-fit `Image` backgrounds on the controller, sized from one project-wide **Background Cover Size** unless a hub overrides it. |
| **Show / hide** | Active Objects — per-slot visibility for HUD roots that are not widget Targets (exact slot, no Phone fallback). |
| **Events** | Per-slot Play Mode `UnityEvent`s on the hub (`ResponsiveLayoutEvents`), plus an optional form-transition cover during Play Mode form switches. |
| **Project Settings** | `Edit → Project Settings → Responsive UI`: design-canvas reference device per form, Background Cover Size, half-open guide, and the **Android Fold**, **Android Flip** and **iPhone Duo** pose pages. |
| **Authoring** | Slot focus follows the Device Simulator; editing the active slot pushes live. Toggle **Enabled** on any slot in the live fallback chain and the resolved layout previews immediately. Exact-slot bulk copy across widget, camera, scroll and two-side rows. |
| **Foldable posture** | `FoldablePostureDetector` maps Closed / Opened / HalfOpened product states into the form slots on Android players, iPhone Duo players and the Editor Simulator. |
| **Simulator fixtures** | Editable phone/pad JSON catalog with procedural bezels, plus Fold / Flip Device Simulator skins and panels. |
| **Ship it** | One **Validate Setup** and one **Export Unity Package** for the whole kit. |

## Requirements

| Requirement | Why |
|-------------|-----|
| Unity **2023.1** or newer | `package.json` `unity` field; also used on Unity 6 |
| uGUI (`com.unity.ugui` 2.0.0) | package dependency — all layout is `RectTransform` based |
| TextMeshPro | referenced by the runtime assembly; needed by `ResponsiveTextBanner` |
| Device Simulator (recommended) | authoring and preview. Unity's `com.unity.device-simulator.devices` package adds its own photoreal overlays beside this package's skins |
| Android Build Support | required for Android Fold / Flip posture on device |
| iOS Build Support | required for iPhone Duo hinge posture on device |
| FancyScrollView (optional) | only for `ResponsiveScrollView`; found by reflection, so there is no assembly reference |

## Install

This package is an **Assets drop-in**, not a UPM package — it lives under `Assets/`, not in
`Packages/`.

**Option A — copy into `Assets/`**

1. Copy the `ResponsiveUI` folder into your project's `Assets/`.
2. Let Unity import the assembly definitions and scripts.
3. Accept the **Responsive UI — First-time setup** dialog to apply the recommended Android and
   orientation Player Settings and enable the design-canvas reference skins.

**Option B — import a `.unitypackage`**

1. In the source project: **Responsive UI → Project → Export Unity Package**.
2. In the target project: **Assets → Import Package → Custom Package…**.

**Then, in either case:** run **Responsive UI → Project → Validate Setup**, and pick a design-canvas
**Reference Device** per form factor in **Edit → Project Settings → Responsive UI**.

## Quick start

1. Add a hub: **Responsive UI → Scene → Create Sample Canvas**, or add `ResponsiveUI` to an
   existing Canvas yourself.
2. Add `ResponsiveUiController` on that Canvas or a child, and add a widget row pointing at the
   `RectTransform` you want to move.
3. Author the Phone Portrait slot — it is the fallback every other slot borrows from.
4. Enable and author the slots that need to differ (Pad, the Fold / Flip / iPhone Duo covers and
   half-open poses, Wide Screen).
5. Open the **Device Simulator**, switch profiles, and watch the slot focus follow the device.

Each step, with the menu paths and the things that bite: **[Docs/GETTING-STARTED.md](Docs/GETTING-STARTED.md)**.

## Supported devices and platforms

| Platform | Forms it can report | Posture source |
|----------|---------------------|----------------|
| Android phone / tablet | Phone, Pad | screen diagonal (≥ 7″ → Pad, needs real DPI) |
| Android book-style foldable | Phone, Pad, Fold Cover, Fold Half Opened, Fold Cover Half Opened | Jetpack WindowManager through `FoldablePostureDetector` |
| Android clamshell foldable | Phone, Flip Cover, Flip Half Opened | Jetpack WindowManager through `FoldablePostureDetector` |
| iPhone / iPad | Phone, Pad | model heuristics — no hinge forms |
| iPhone Duo | Phone, Pad, iPhone Duo Cover, iPhone Duo Seated | UIKit `UIHingeInteraction` through `FoldablePostureDetector` |
| Windows / macOS / Linux / WebGL | Wide Screen only | none — sized from the window or browser, no mobile detection |
| Unity Editor | any of the above | the selected Device Simulator profile, on any build target |

Players never return another platform's form. Per-platform detail, and how to add an adapter for a
new hinged device: **[Docs/PLATFORMS.md](Docs/PLATFORMS.md)**.

## Good to know

- **Leave Hub empty** on prefabs. A client resolves its hub from the parent Canvas (or the root
  Canvas) on enable and on parent change, so an instantiated prefab binds without a serialized
  scene reference.
- Android half-open forms and iPhone Duo Seated need an **active** `FoldablePostureDetector`; it can
  persist across scene loads, so one detector in the scene that hosts it covers the whole session.
- Layout events fire once from `Start`, then only when the applied form × orientation changes.
- Camera, scroll and banner components are hub **clients**; they do not inherit the hub, and you
  never add a second handler beside `ResponsiveUI`.

## Documentation

| Doc | Question it answers |
|-----|---------------------|
| [Docs/README.md](Docs/README.md) | Where is everything? |
| [Docs/GETTING-STARTED.md](Docs/GETTING-STARTED.md) | How do I get one responsive screen working? |
| [Docs/CONCEPTS.md](Docs/CONCEPTS.md) | Form factors, slots, fallback, design scale, the hub contract |
| [Docs/COMPONENTS.md](Docs/COMPONENTS.md) | What does each component do, and what is its API? |
| [Docs/SETTINGS.md](Docs/SETTINGS.md) | What is on the Project Settings pages? |
| [Docs/PLATFORMS.md](Docs/PLATFORMS.md) | What runs where, and how do I add an adapter? |
| [Docs/STRUCTURE.md](Docs/STRUCTURE.md) | Folders (`Runtime`, `Editor/Package`, modules), assemblies, conventions |
| [Docs/PRODUCTION.md](Docs/PRODUCTION.md) | What ships, and what is the release checklist? |
| [Docs/TESTING.md](Docs/TESTING.md) | How do I run the suites and validators? |
| [Docs/DEVICE-TESTING.md](Docs/DEVICE-TESTING.md) | How do I verify on real hardware and simulators? |
| [Docs/MIGRATION.md](Docs/MIGRATION.md) | How do I upgrade from an older version or the standalone fold kit? |
| [FoldAndFlip/README.md](FoldAndFlip/README.md) | The Android fold / flip module |
| [IphoneDuo/README.md](IphoneDuo/README.md) | The iPhone Duo module |
| [PhonePad/README.md](PhonePad/README.md) | The phone / pad catalog and Simulator skins |

MIT licensed — see [LICENSE.md](LICENSE.md). Device model names and Simulator overlays carry their
own terms; the license file spells out which.
