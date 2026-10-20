# Testing

How to run every automated check in the package: the Unity EditMode suites and the PowerShell
validators. Real-device verification is separate — see [./DEVICE-TESTING.md](./DEVICE-TESTING.md).
Which of these a release must pass is listed in [./PRODUCTION.md](./PRODUCTION.md).

The PowerShell scripts need **PowerShell** and nothing else: they do **not** need Unity, do not open
the project, and do not write to it (the one exception is noted below). They run under Windows
PowerShell 5.1 and under PowerShell 7 (`pwsh`) on Windows, macOS and Linux. Without PowerShell
installed you cannot run them; there is no shell-script equivalent in the package.

## The one command

From the Unity project root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\Assets\ResponsiveUI\Tests\validate-package.ps1
```

`validate-package.ps1` is the main gate. It runs its own structural and packaging assertions, then
nests, in order: `validate-structure.ps1`, `test-design-scale.ps1`, `test-camera-projection.ps1`,
`FoldAndFlip/Tests/validate-package.ps1`, `IphoneDuo/Tests/validate-package.ps1`,
`PhonePad/Tests/validate-package.ps1`, `validate-export-manifest.ps1`, and — only when
`RESPONSIVEUI_RUN_EDITMODE=1` — `run-editmode-tests.ps1`. Each nested gate runs in its own shell so
it cannot share this process's stdout. The two module gates nest further scripts of their own, so
one command covers every validator except the two maintainer-only generators.

## PowerShell validators

### Root — `Tests/`

| Script | Guards | Notes |
|--------|--------|-------|
| `validate-package.ps1` | required files, package identity, export code, settings API, the EditMode runner's own shape; nests every other gate | the entry point |
| `validate-structure.ps1` | required Runtime/Editor source files and public API names exist; doc claims that must stay true; an unqualified `StringComparison` or similar `System` BCL name with no `using System` | greps text. **Not a compiler and not a behavior proof** — the Unity Console and the EditMode suites are the compile gate |
| `test-design-scale.ps1` | widget ratio tables (Width / Height / Uniform / IndependentXY), scaler mapping, stretch, nested residual | mirrors `ResponsiveRectState.ComputeCanvasRatio` / `Unscale` / Apply independently |
| `test-camera-projection.ps1` | camera aspect-framing vs pixel-ratio for both orthographic and FOV cameras, read from the **catalog JSON** | fails if FOV multiplies degrees by the canvas ratio, if the Unity tests use production `ScaleFieldOfView` as the expected value, or if the tests hardcode a device that is not in the catalog |
| `validate-export-manifest.ps1` | the contents of a generated `.unitypackage`: no `/Tests/`, no PhonePad `.device` or `DeviceOverlays`, `package.json` and `SharedEditor` present | **skips** (exit 0) when no `Temp/ResponsiveUI-*.unitypackage` exists |
| `run-editmode-tests.ps1` | runs all three EditMode assemblies in Unity batchmode | needs Unity and the Editor **closed** — see below |
| `export-production.ps1` | produces the `.unitypackage` that `validate-export-manifest.ps1` inspects | calls `ResponsiveUiPackageExport.ExportUnityPackageBatch`; needs Unity and the Editor closed |

Why `test-camera-projection.ps1` exists: grepping that `ScaleFieldOfView` exists does **not** prove
Scale Projection is correct, and a round-trip Scale/Unscale test also passes when both sides share
one wrong formula. The camera Unity tests must compute the expected FOV and orthographic size from
tan(half-FOV) × aspect, and must assert that the width pixel ratio is a different number.

### Fold & Flip — `FoldAndFlip/Tests/`

| Script | Guards | Notes |
|--------|--------|-------|
| `validate-package.ps1` | module structure, namespaces, packaging, the runtime-signal contract, the Java bridge files; delegates the resolution and device audits | nested by the root gate |
| `test-resolutions.ps1` | the Closed / Opened / HalfOpened matrix for every Expected device; §6b mirrors the native `Enrich` product-state detection | nested by the module gate |
| `audit-all-device-resolutions.ps1` | every `Profiles/*.json` against its `.device` file, plus the cover/open ratio | nested by the module gate |
| `generate-device-skins.ps1` | — | **maintainer-only writer.** Regenerates `.device` skins and bezel overlays, preserving existing `.meta` GUIDs and creating deterministic new ones. Not a test; do not run it in validation or CI |

### iPhone Duo — `IphoneDuo/Tests/`

| Script | Guards | Notes |
|--------|--------|-------|
| `validate-package.ps1` | module structure, the native bridge and posture-backend files, rules and Info.plist step | static only; it is not proof of iOS runtime behavior — test the postures on hardware |

### Phone & Pad — `PhonePad/Tests/`

| Script | Guards | Notes |
|--------|--------|-------|
| `validate-package.ps1` | module structure and required files; delegates the profile audit | nested by the root gate |
| `audit-profiles.ps1` | profile identity, JSON schema, upright portrait resolution and exact landscape swap, safe-area bounds, DPI, `.device` agreement, evidence source and status | **read-only** — it never creates, rewrites or deletes profiles, simulator definitions, overlays or metadata |
| `verify-official-specs.ps1` | — | **maintainer-only writer** that rewrites profile specs. Not a release test; do not run it in validation or CI |

A PhonePad pass proves static catalog consistency only. Complete Simulator review, a Unity build and
representative physical-device testing before release.

## Unity EditMode suites

Test Runner → **EditMode**. Three assemblies:

| Assembly | Folder | Covers |
|----------|--------|--------|
| `com.futuredigitalplus.responsiveui.tests.editor` | `Tests/Editor/` | layout, hub, widgets, cameras, scroll, banner, Simulator integration, export paths, Inspector chrome |
| `com.futuredigitalplus.foldandflip.tests.editor` | `FoldAndFlip/Tests/Editor/` | posture math, `NativeDetect_*` / `ApplyCoverFlags_*` cover races, half-open rules, orientation hold, layout apply |
| `com.futuredigitalplus.iphoneduo.tests.editor` | `IphoneDuo/Tests/Editor/` | hinge mapping, posture rules, hold / flat lock state, published state, `IphoneDuoEvents`, Editor rule preview, the Info.plist build step |

`ResponsivePlatformTestSetup` turns `ResponsivePlatforms.ConstrainsForms` off so detection tests see
every form factor; platform tests pin one with `ResponsiveEditorTestUtil.WithPlatform`.

### From the Test Runner

**Window → General → Test Runner → EditMode**, then run All, or expand one assembly and run it.
This is the fastest loop while developing, and the only way to run the suites while the Editor is
open.

### From the command line

`Tests/run-editmode-tests.ps1` runs all three assemblies in one Unity invocation:

```powershell
pwsh -NoProfile -File ./Assets/ResponsiveUI/Tests/run-editmode-tests.ps1
```

- **Close the interactive Editor for this project first.** Unity batchmode cannot open the same
  project concurrently.
- It finds the project's Unity version on Windows and macOS, deletes stale results, and fails when
  Unity exits without producing a fresh result file.
- Results and the log go to `Logs/` (not `Temp/`, which Unity clears on exit).
- It **skips** (exit 0) when the Editor cannot be found, or when `UNITY_SKIP_EDITMODE_TESTS=1`.

To run one assembly by hand:

```text
-runTests -testPlatform EditMode -assemblyNames com.futuredigitalplus.responsiveui.tests.editor
-runTests -testPlatform EditMode -assemblyNames com.futuredigitalplus.foldandflip.tests.editor
-runTests -testPlatform EditMode -assemblyNames com.futuredigitalplus.iphoneduo.tests.editor
```

Example invocation for a pinned Editor:

```powershell
& $env:UNITY_EDITOR -batchmode -nographics -projectPath (Get-Location) `
  -runTests -testPlatform EditMode `
  -assemblyNames com.futuredigitalplus.foldandflip.tests.editor `
  -testResults .\Logs\foldandflip-editmode.xml `
  -logFile .\Logs\foldandflip-editmode.log
```

Keep generated logs and result files under a CI artifact directory such as `Logs/` or
`TestResults/`. Do not commit them inside the package.

### What the EditMode suites cover

Hub resolve, client Apply and list migration; scroll hub layout vs serialized Hub and scroll
adopting a changed layout on a layout-only pass; design-scale math, including a Scale Mode change
without recapture jumping the Target; camera Apply and aspect-framed FOV and orthographic size
(independently computed, not `ScaleFieldOfView` feeding itself); Scale Mode recapture; scroll slots;
`OrientSize`; Project Settings background-size provider fallbacks; ForceUpdate skip; single
`FoldablePostureDetector` ownership; Fold Cover detection with and without the fold runtime;
Simulator layout resolver Phone/Pad/Cover/Flip mapping and the `MapFoldableProduct` kind × state
matrix with Cover W×H refine; client notify order; nested-canvas Design Scale size reapply;
stale-hub Apply; two-side `layoutOnly` skip while a tween is active, with idle siblings still
re-laying out; export path filters; new-row Enabled seeding, with Flip Cover Landscape starting off;
controller and two-side widgets keeping the Target active when a slot is Disabled; Device Simulator
canvas-ready Match Mode re-cover and notify-all clients; copy-Phone-on-enable; slot Copy / Paste /
Reset; empty list `+` rows (`ResetNewListRow` / `ForEachNewRow`); exact-slot bulk copy across
multiple widget and camera rows and scroll metrics; same-slot no-op behavior; per-slot Active
Objects; Inspector section chrome (`InspectorSectionKey` / `PropertySectionKey` / `DrawFoldout` /
`FieldLabelWidth` / slot height); `ResponsiveTextBanner` width and Alignment-pivot math, where a
fitted width does not rewrite `anchoredPosition`, plus Hide restore, preview lock and pending
hub-capture; and scroll slot ↔ FancyScrollView host SerializedObject sync (`CopySlotToHost` /
`CaptureHostToActiveSlot`).

### What they do not cover

EditMode tests run in the Editor, where `IsRuntimeSupported` is true. They do **not** prove Android
JNI, the iOS hinge API, manifest merging or R8 behavior.

Play Mode only, verify by hand ([./PRODUCTION.md](./PRODUCTION.md)):

- the `ResponsiveScrollView` unbound-host write + Relayout;
- the two-side re-apply after a tween that was skipped by a layout-only pass.

## Before a release

| Check | Required |
|-------|----------|
| `Tests/validate-package.ps1` (nests structure, math, module and export-manifest gates) | yes |
| All three EditMode assemblies, from the Test Runner or `run-editmode-tests.ps1` | yes |
| `Tests/export-production.ps1`, then `validate-export-manifest.ps1` against the result | yes — otherwise the export gate silently skips |
| **Responsive UI → Project → Validate Setup** in the Editor | yes |
| Device matrix rows in [./DEVICE-TESTING.md](./DEVICE-TESTING.md) | yes, before claiming device compatibility |
| `generate-device-skins.ps1`, `verify-official-specs.ps1` | no — maintainer-only writers |

A static pass is not a runtime pass. Platform contract: [./PLATFORMS.md](./PLATFORMS.md). Detection
model: [../FoldAndFlip/README.md](../FoldAndFlip/README.md).
