# Responsive UI for Unity

Author a uGUI screen once, then let it lay itself out everywhere your game ships — phones,
tablets, book-style and clamshell foldables, the iPhone Duo, and desktop or WebGL windows.

**Version:** 1.0.0 · **Unity:** 2023.1+ · **Package id:** `com.futuredigitalplus.responsiveui` ·
**UI system:** uGUI · **License:** MIT

> **uGUI today, UI Toolkit next.** Every component in this release targets uGUI — Canvas,
> `RectTransform` and TextMeshPro. UI Toolkit (UXML / USS) support is planned; the form-factor
> model, the slot and fallback rules and the posture detection are UI-system agnostic, so that
> layer is what a UI Toolkit implementation will build on.

A `ResponsiveUI` hub on your Canvas classifies the live screen as one of ten **form factors** —
Phone, Pad, Fold Cover, Fold Half Opened, Fold Cover Half Opened, Flip Cover, Flip Half Opened,
iPhone Duo Cover, iPhone Duo Seated, Wide Screen — times Portrait or Landscape, and applies the
layout you authored for that slot to widgets, cameras, scroll views and text banners.

Foldables are first class. Posture comes from Jetpack WindowManager on Android, from the UIKit
hinge interaction on iPhone Duo, and from Unity's Device Simulator while you author.

---

## What you get

| Area | What you get |
|------|--------------|
| **Widget layout** | Twenty slots per Target (ten forms × Portrait/Landscape), with a fallback chain so you only author what differs |
| **Design Scale** | Width / Height / Auto / UniformMin / UniformMax / IndependentXY against each form's design canvas |
| **Cameras** | Viewport, position, rotation, FOV / orthographic size, per-slot focus target, cover-fit background |
| **Scroll views** | Per-slot FancyScrollView cell interval and scroll offset, written onto the sibling host |
| **Foldable posture** | Closed / Opened / HalfOpened mapped into form slots on Android, iPhone Duo and the Editor Simulator |
| **Project Settings** | Design-canvas reference device per form, background cover size, half-open guide, and pose pages for Android Fold, Android Flip and iPhone Duo |
| **Device Simulator** | Editable phone/pad JSON catalog with procedural bezels, plus Fold / Flip skins and control panels |
| **Authoring** | Slot focus follows the Simulator; editing the active slot pushes live; exact-slot bulk copy |

## Requirements

| Requirement | Why |
|-------------|-----|
| Unity **2023.1** or newer | also used on Unity 6 |
| uGUI (`com.unity.ugui` 2.0.0) | all layout is `RectTransform` based |
| TextMeshPro | needed by `ResponsiveTextBanner` |
| Device Simulator | recommended for authoring and preview |
| Android Build Support | for Android Fold / Flip posture on device |
| iOS Build Support | for iPhone Duo hinge posture on device |
| FancyScrollView | optional — only for `ResponsiveScrollView`, found by reflection |

## Install

This is an **Assets drop-in**, not a UPM package. It lives under `Assets/`, not `Packages/`.

```bash
git clone https://github.com/gentleplay/Unity-ResponsiveUi.git
```

Copy `Assets/ResponsiveUI/` into your project's `Assets/` folder, let Unity import, then accept the
**Responsive UI — First-time setup** dialog. Finish with **Responsive UI → Project → Validate Setup**.

You can also export a `.unitypackage` from a project that already has it:
**Responsive UI → Project → Export Unity Package**.

## Quick start

1. **Responsive UI → Scene → Create Sample Canvas**, or add `ResponsiveUI` to a Canvas yourself.
2. Add `ResponsiveUiController` and point a widget row at the `RectTransform` you want to move.
3. Author the Phone Portrait slot — every other slot falls back to it.
4. Enable and author only the slots that need to differ.
5. Open the Device Simulator and switch profiles; the slot focus follows the device.

Full walkthrough: **[Docs/GETTING-STARTED.md](Assets/ResponsiveUI/Docs/GETTING-STARTED.md)**.

## Supported devices

| Platform | Forms it can report | Posture source |
|----------|---------------------|----------------|
| Android phone / tablet | Phone, Pad | screen diagonal |
| Android book-style foldable | Phone, Pad, Fold Cover, Fold Half Opened, Fold Cover Half Opened | Jetpack WindowManager |
| Android clamshell foldable | Phone, Flip Cover, Flip Half Opened | Jetpack WindowManager |
| iPhone / iPad | Phone, Pad | model heuristics |
| iPhone Duo | Phone, Pad, iPhone Duo Cover, iPhone Duo Seated | UIKit `UIHingeInteraction` |
| Windows / macOS / Linux / WebGL | Wide Screen | window or browser size |
| Unity Editor | any of the above | the selected Device Simulator profile |

## Documentation

Everything lives in [`Assets/ResponsiveUI/Docs/`](Assets/ResponsiveUI/Docs/):

| Doc | Question it answers |
|-----|---------------------|
| [Docs/README.md](Assets/ResponsiveUI/Docs/README.md) | Where is everything? |
| [GETTING-STARTED.md](Assets/ResponsiveUI/Docs/GETTING-STARTED.md) | How do I get one responsive screen working? |
| [CONCEPTS.md](Assets/ResponsiveUI/Docs/CONCEPTS.md) | Form factors, slots, fallback, design scale |
| [COMPONENTS.md](Assets/ResponsiveUI/Docs/COMPONENTS.md) | What does each component do? |
| [SETTINGS.md](Assets/ResponsiveUI/Docs/SETTINGS.md) | What is on the Project Settings pages? |
| [PLATFORMS.md](Assets/ResponsiveUI/Docs/PLATFORMS.md) | What runs where, and how do I add an adapter? |
| [STRUCTURE.md](Assets/ResponsiveUI/Docs/STRUCTURE.md) | Folders, assemblies, conventions |
| [PRODUCTION.md](Assets/ResponsiveUI/Docs/PRODUCTION.md) | What ships, and the release checklist |
| [TESTING.md](Assets/ResponsiveUI/Docs/TESTING.md) | How do I run the suites and validators? |
| [DEVICE-TESTING.md](Assets/ResponsiveUI/Docs/DEVICE-TESTING.md) | How do I verify on real hardware? |
| [MIGRATION.md](Assets/ResponsiveUI/Docs/MIGRATION.md) | Upgrading from an older version or the standalone fold kit |

## Modules

| Module | What it adds |
|--------|--------------|
| [FoldAndFlip](Assets/ResponsiveUI/FoldAndFlip/README.md) | Android foldable posture (Jetpack), Fold / Flip Simulator skins and panel |
| [IphoneDuo](Assets/ResponsiveUI/IphoneDuo/README.md) | iPhone Duo hinge posture (UIKit), pose rules, Info.plist keys |
| [PhonePad](Assets/ResponsiveUI/PhonePad/README.md) | Phone / pad profile catalog and Device Simulator skins |

## Upgrading from the Fold & Flip Responsive Kit

This package supersedes the standalone `FoldAndFlipResponsiveKit`, which is now the **FoldAndFlip**
module inside it. What moved, what was renamed, and what a project has to change:
[MIGRATION.md](Assets/ResponsiveUI/Docs/MIGRATION.md).

## Roadmap

| Planned | Notes |
|---------|-------|
| UI Toolkit support | UXML / USS layouts driven by the same form factors, slots and posture detection |

## References

- [Make your app fold aware (Android Developers)](https://developer.android.com/develop/adaptive-apps/guides/foldables/make-your-app-fold-aware)
- [AndroidX Window](https://developer.android.com/jetpack/androidx/releases/window)
- [Implement Flex Mode into a Unity game (Samsung Code Lab)](https://developer.samsung.com/codelab/gamedev/flex-mode-unity.html)
- [Unity Device Simulator](https://docs.unity3d.com/Manual/device-simulator.html)

## License

MIT — see [LICENSE.md](Assets/ResponsiveUI/LICENSE.md). Device model names and Simulator overlays
carry their own terms; the license file spells out which.
