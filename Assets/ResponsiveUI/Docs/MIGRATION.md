# Migration

For projects that already use an older drop: the standalone Fold & Flip kit, the standalone Phone &
Pad simulator kit, or an earlier version of this package. If you are installing for the first time,
read [./GETTING-STARTED.md](./GETTING-STARTED.md) instead.

## From the standalone kits to the unified package

The fold/flip kit and the phone/pad simulator kit are no longer separate packages. They ship as
modules inside `Assets/ResponsiveUI/`.

| Was | Now |
|-----|-----|
| `Assets/FoldAndFlipResponsiveKit/` | `Assets/ResponsiveUI/FoldAndFlip/` |
| `Assets/PhonePadSimulatorKit/` | `Assets/ResponsiveUI/PhonePad/` |
| — (new) | `Assets/ResponsiveUI/IphoneDuo/` |
| Per-kit **Fold & Flip** and **Phone & Pad** menus | one **Responsive UI** menu root |
| Per-kit export menus | one **Responsive UI → Project → Export Unity Package** |

### Steps

1. **Back up the project.**
2. Import the full `Assets/ResponsiveUI/` tree — layout + `FoldAndFlip` + `IphoneDuo` + `PhonePad`.
   Do not copy only `FoldAndFlip/`: the fold module alone does not contain the layout runtime it is
   authored against.
3. Delete the former roots if they are still present: `Assets/FoldAndFlipResponsiveKit`,
   `Assets/PhonePadSimulatorKit`.
4. Remove any custom launcher manifest you added for fold detection. The runtime binds Unity's
   current Activity directly.
5. **Responsive UI → Project → Apply Android Fold & Flip Recommended Settings**, then **Responsive
   UI → Project → Validate Setup**.
6. Set `FoldablePostureDetector.DeviceKind` when you target a known Fold / Flip / dual-screen
   family; leave it `Unknown` for general builds.
7. If your app loads gameplay scenes that have no posture owner of their own, enable
   **`persistAcrossScenes`** on the `FoldablePostureDetector` in your boot scene so it
   `DontDestroyOnLoad`s. Duplicate copies in later scenes destroy themselves without stealing the
   Android native owner.
8. Open device catalogs only under **Responsive UI → Devices**.

C# namespaces and the Android package id are unchanged: keep `FDPlus.Foldable` and
`com.futuredigitalplus.foldandflip`. Scene and prefab YAML binds by script GUID, which the move
preserves, so authored components keep their references.

## Replacing the former `FoldableModeManager`

`FoldableModeManager` was **removed**, not retained as a compatibility component. Unity cannot
automatically remap its deleted script GUID to `FoldablePostureDetector`, so this is a manual step.

| Situation | What to do |
|-----------|-----------|
| The old package is still installed | add `FoldablePostureDetector` to the same boot host, copy over the intended Device Kind and persistence choice, then remove `FoldableModeManager` — in that order |
| The old script was already deleted | remove the Missing Script component and add `FoldablePostureDetector` |
| Both components present | not supported. The detector is the only posture / native bridge owner |

## Renamed and removed types

Types and members a forked or deeply integrated project may still reference.

| Removed | Replacement |
|---------|-------------|
| `FoldableModeManager` | `FoldablePostureDetector` |
| `ResponsiveUiHandler.onPhone` / `onPad` events, `CurrentType` | hub layout events (`ResponsiveLayoutEvents`) and `IResponsiveUiClient.ApplyFromHub` |
| `ResponsiveUiHandler.SetActiveGroup` (phone/pad group toggling) | per-slot **Active Objects** on `ResponsiveUiController` |
| `ResponsiveUiHandler.DetectEditorFormFactor` | the Simulator path (`ResponsiveUiSimulatorLayoutResolver`) |
| `phoneOnlyObjects` lists on the hub and camera Inspectors | per-slot **Active Objects** |
| Flat hub fields `formTransitionLoadingUi`, `formTransitionLoadingEnabled`, `formTransitionMinVisibleSeconds` | the `ResponsiveFormTransitionLoading` block (enable + overlay + per-slot Show On flags + min visible) |
| `ResponsiveRectState.ResolveCaptureSlot`, `CopyDesignScaleFrom(…)` | `CopyDesignScaleSettingsFrom`, which leaves Design Canvas Size unset so slots follow the catalog |
| Camera `designUiSize` serialized field | **Design UI Size** locked to the Responsive UI form default |
| Hub list migration (`MigrateLegacyListsToController`, `EditorMigrateLegacyListsNow`, `controllerMigrated`) | widgets and Active Objects live on `ResponsiveUiController`; migrate before upgrading, since the shim is gone |
| Camera YAML migrations (`scaleOrthographicSizeMigrated`, `projectionTypeMigrated`, `SyncApplyLocalTransform`) | re-author the affected camera slots |

If your project still carries pre-migration hub lists or camera YAML, upgrade from the older version
that still had the shims **first**, then import this one.

## Runtime snapshot additions

`FoldableLayoutState` now also carries:

- `isInMultiWindowMode`
- `isInPictureInPictureMode`

Treat both as Activity / window context. They are independent of `FoldableProductState` and
`FoldablePosture` — in particular, neither identifies a fold posture and neither may be read as
Closed. Existing listeners keep receiving `LayoutChanged` unchanged.

## Slot set

The form-factor set grew. A project upgrading from a build with fewer slots gets the new ones
Disabled unless it seeds them, and a disabled slot falls back as described in
[./CONCEPTS.md](./CONCEPTS.md). Current order, which is also the serialized slot order
(`ResponsiveWidgetLayout.FormFactors`):

```text
Phone → Pad → Fold Cover → Fold Half Opened → Fold Cover Half Opened →
Flip Cover → Flip Half Opened → iPhone Duo Cover → iPhone Duo Seated → Wide Screen
```

Slot fields bind by **name** in Unity YAML, so this order can change in a future version without
forcing a scene migration — but a newly inserted form factor still needs authoring or an explicit
decision to let it fall back.

## Android dependency and shrinking

The Android library pins AndroidX Window `1.4.0` (`window` and `window-java`) and publishes
`consumer-rules.pro` for release shrinking. The recommended settings enforce Android `targetSdk` /
compile API **34+**. If your host forces another Window version, validate the final Gradle graph and
run a **minified release** build on hardware before shipping.

## iOS fold and flip

iOS fold support is **iPhone Duo** only, through UIKit `UIHingeInteraction` (`IphoneDuoHinge` in the
separate `IphoneDuo/` module). Do not copy the Android library to iOS.

Another Apple foldable needs its hardware identifier in `IphoneDuoHinge.FoldableModels` (reported
through `IsSupportedDevice`) and, if its posture model differs, an `IFoldablePostureBackend` that
publishes `FoldableLayoutState`. Report support only for hardware you have actually covered — see
[./DEVICE-TESTING.md](./DEVICE-TESTING.md).

## Editor code layout

Root layout editor C# is grouped under `Editor/Package`, `Editor/Hub`, `Editor/Simulator`,
`Editor/Widgets`, `Editor/Camera`, `Editor/Scroll` and `Editor/Banner`. Namespaces are unchanged
(`FDPlus.ResponsiveUI.Editor`) and the asmdef still sits at `Editor/`, so this affects only patches
and forks that reference file paths. Details: [./STRUCTURE.md](./STRUCTURE.md).

## Documentation moved

Per-module documentation folders were consolidated into `Docs/`. If you link to these from your own
notes, repoint them:

| Was | Now |
|-----|-----|
| `Docs/FEATURES.md` | [./CONCEPTS.md](./CONCEPTS.md), [./COMPONENTS.md](./COMPONENTS.md), [./SETTINGS.md](./SETTINGS.md) |
| `Docs/IPHONE-DUO-SIMULATOR.md` | [./DEVICE-TESTING.md](./DEVICE-TESTING.md) |
| `FoldAndFlip/Docs/PHYSICAL-DEVICE-RELEASE-MATRIX.md` | [./DEVICE-TESTING.md](./DEVICE-TESTING.md) |
| `FoldAndFlip/Docs/PRODUCTION.md`, `PhonePad/Docs/PRODUCTION.md` | [./PRODUCTION.md](./PRODUCTION.md) |
| `FoldAndFlip/Docs/STRUCTURE.md`, `PhonePad/Docs/STRUCTURE.md` | [./STRUCTURE.md](./STRUCTURE.md) |
| `FoldAndFlip/Docs/MIGRATION.md` | this file |
| `Tests/README.md`, `FoldAndFlip/Tests/README.md` | [./TESTING.md](./TESTING.md) |
| `FoldAndFlip/Docs/DETECTION.md` | unchanged — see [../FoldAndFlip/README.md](../FoldAndFlip/README.md) |

## After upgrading

1. **Responsive UI → Project → Validate Setup**.
2. **Edit → Project Settings → Responsive UI** — confirm Reference Devices, Background Cover Size,
   the hinge guide, and the Android Fold / Android Flip / iPhone Duo pose pages
   ([./SETTINGS.md](./SETTINGS.md)).
3. Run the gates in [./TESTING.md](./TESTING.md).
4. Walk the release checklist in [./PRODUCTION.md](./PRODUCTION.md), then record real-device rows in
   [./DEVICE-TESTING.md](./DEVICE-TESTING.md).
