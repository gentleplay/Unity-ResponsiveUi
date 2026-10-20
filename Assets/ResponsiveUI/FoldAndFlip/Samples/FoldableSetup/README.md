# Fold & Flip Guided Setup Sample

This folder is a guided setup (no committed scene — avoids pipeline/input/camera coupling).

Install the full kit: copy **`Assets/ResponsiveUI/`** (see [`../../../README.md`](../../../README.md)).

## Create the sample

1. **Responsive UI → Project → Apply Android Fold & Flip Recommended Settings**
2. Create and save an empty scene named `FoldableSetupSample`.
3. **Responsive UI → Scene → Create Fold & Flip Setup**
4. Confirm `FoldableSetup` has `FoldablePostureDetector` (+ optional `FoldableSimulator` on the same object).
   Sample cameras/canvases are for preview only — posture is detection-only via the detector.
   `FoldableSimulator` is Editor Play Mode only. Android players use Jetpack on `FoldablePostureDetector`.
5. Set **Device Kind** on `FoldablePostureDetector` when targeting a known Fold/Flip/Dual family.

## Verify

| Mode | Action |
|------|--------|
| Simulator | ★ Fold / ★ Flip / ★ Dual profiles → Closed / Opened / Half Opened → Play. Overlay does not run on the Android player. |
| Catalog | **Responsive UI → Devices → Fold & Flip Catalog** |
| Posture | **Responsive UI → Devices → Posture Simulator** |
| Validate | **Responsive UI → Project → Validate Setup** |

See [Docs/DETECTION.md](../../Docs/DETECTION.md) before assuming Closed is always native-detectable.
Player fold/hinge runs on **Android and iPhone Duo**; other iPhones / iPads stay Phone/Pad — [PLATFORMS.md](../../../Docs/PLATFORMS.md).
