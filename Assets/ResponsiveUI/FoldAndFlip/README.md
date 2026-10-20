# FoldAndFlip (Responsive UI module)

**Version:** 1.0.0 (unified Responsive UI kit)  
**Package id:** `com.futuredigitalplus.foldandflip`  
**Namespace:** `FDPlus.Foldable`

Foldable runtime (shared posture contract + `IFoldablePostureBackend` seam), the Android (Jetpack) bridge,
Fold / Flip Device Simulator skins and the **Android Fold & Flip** panel, and Editor tools.
**Player Fold/Flip runs on Android** (Jetpack). iPhone Duo (UIKit `UIHingeInteraction`, iOS 27.1) lives in the
separate [IphoneDuo](../IphoneDuo/README.md) module, which registers a posture backend with this one.
Other iPhones / iPads stay Phone/Pad.
Author Fold/Flip slots in the Editor now — see [../Docs/PLATFORMS.md](../Docs/PLATFORMS.md).
Ships inside **`Assets/ResponsiveUI/FoldAndFlip/`** — install the whole `Assets/ResponsiveUI/` tree.
Validate Setup and Export Unity Package are owned by the root kit (`Editor/Package`), not this module.

**`persistAcrossScenes`:** optional on `FoldablePostureDetector`. When true in Play Mode the
owner `DontDestroyOnLoad`s as a singleton; duplicates destroy without stealing the Android native owner.
`FindActiveInstance` recovers the persistent instance after a cached miss. Host one
**`FoldablePostureDetector`** (detection only; `deviceKind = FoldablePhone`) in your first scene so
later scenes inherit the HalfOpened posture. Create Fold & Flip Setup adds a detector
(+ optional Simulator overlay).

## Menus

All under **Responsive UI**:

| Menu | Action |
|------|--------|
| **Scene → Create Fold & Flip Setup** | Hierarchy in the open scene |
| **Scene → Create Fold & Flip Complete Scene** | New empty scene + hierarchy |
| **Devices → Fold & Flip Catalog** | Edit fold/flip profiles |
| **Devices → Posture Simulator** | Posture preview |
| **Project → Apply Android Fold & Flip Recommended Settings** | Android / orientation Player Settings |
| **Project → Validate Setup** | Full kit validation (includes this module) |
| **Debug → Diagnose Simulator Bridge** | Simulator bridge diagnostics |

Also: **Window → Responsive UI → Fold & Flip Catalog** / **Posture Simulator**.

## Device Simulator

Fold/Flip profiles ship with ★ Fold / ★ Flip `.device` skins under `Editor/Devices/`.
In the Device Simulator plugin, the active product-state preset is highlighted in light green.
On the Android build target the **Android Fold & Flip** panel drives them; iPhone Duo uses the **iPhone Duo** panel from `../IphoneDuo/Editor` on the iOS target.
Cover/inner sizes also feed **Project Settings → Responsive UI** (Fold Cover / Fold Half Opened / Fold Cover Half Opened / Flip Cover / Flip Half Opened / iPhone Duo Cover / iPhone Duo Seated Design Canvas and **Background Cover Size**).

## Docs

| Doc | Topic |
|-----|--------|
| [Docs/DETECTION.md](Docs/DETECTION.md) | On-device vs Editor detection; kind × product → form; cover-probe rules |
| [../Docs/PLATFORMS.md](../Docs/PLATFORMS.md) | Android fold · iPhone Duo · other iOS Phone/Pad · desktop / WebGL Wide Screen · form matrix |
| [../Docs/SETTINGS.md](../Docs/SETTINGS.md) | Project Settings, including the Android Fold / Flip pose pages |
| [../Docs/DEVICE-TESTING.md](../Docs/DEVICE-TESTING.md) | Hardware coverage matrix and evidence template |
| [../Docs/PRODUCTION.md](../Docs/PRODUCTION.md) | Release / production gate |
| [../Docs/MIGRATION.md](../Docs/MIGRATION.md) | Upgrade notes |
| [../Docs/STRUCTURE.md](../Docs/STRUCTURE.md) | Folder, assembly and namespace layout |
| [../IphoneDuo/README.md](../IphoneDuo/README.md) | iPhone Duo module |
| [../Docs/COMPONENTS.md](../Docs/COMPONENTS.md) | Full component reference |

## Tests

```powershell
powershell -NoProfile -File .\Assets\ResponsiveUI\FoldAndFlip\Tests\validate-package.ps1
powershell -NoProfile -File .\Assets\ResponsiveUI\FoldAndFlip\Tests\test-resolutions.ps1
powershell -NoProfile -File .\Assets\ResponsiveUI\FoldAndFlip\Tests\audit-all-device-resolutions.ps1
```

Parent kit tests:

```powershell
powershell -NoProfile -File .\Assets\ResponsiveUI\Tests\validate-package.ps1
```
