# Getting Started

For anyone who has just dropped the package into a project and wants one responsive screen working.
Follow the steps in order; each one is a thing you do in the Editor. The vocabulary — form factor,
slot, fallback, design canvas — is explained in [CONCEPTS.md](CONCEPTS.md), and you can read that
before or after this page.

## 1. Install the package

1. Copy the `ResponsiveUI` folder into your project's `Assets/`, or import a `.unitypackage`
   exported from another project (**Assets → Import Package → Custom Package…**). The package is an
   Assets drop-in; it does not go in `Packages/`.
2. Let Unity finish importing the assembly definitions.
3. Accept the **Responsive UI — First-time setup** dialog when it appears. It applies the
   recommended Android and orientation Player Settings and enables the design-canvas reference
   Simulator skins.
4. Run **Responsive UI → Project → Validate Setup** and fix anything it reports.

## 2. Choose a design canvas per form factor

Open **Edit → Project Settings → Responsive UI**.

1. Under **Design Scale Defaults**, pick a catalog **Reference Device** for each form factor
   (Phone, Pad, Fold Cover, Fold Half Opened, Fold Cover Half Opened, Flip Cover, Flip Half Opened,
   iPhone Duo Cover, iPhone Duo Seated). Its portrait pixel size becomes that form's **design
   canvas**: the coordinate space your slot numbers are in.
2. **Wide Screen** has no reference device. Edit its **Design Canvas Size** directly, or press
   **Use Pad Design Canvas** to copy the Pad one.
3. Optionally set **Background Cover Size** — the one art size every cover-fit background uses
   unless a hub overrides it.

Catalog devices are editable: **Responsive UI → Devices → Phone & Pad Catalog** and
**Responsive UI → Devices → Fold & Flip Catalog**. Full tour of these pages:
[SETTINGS.md](SETTINGS.md).

## 3. Add a hub to a Canvas

The fastest route is **Responsive UI → Scene → Create Sample Canvas**. It builds a
`ResponsiveUI Sample` Canvas with a `CanvasScaler`, a `Background` image, a `Widget` child, a
`ResponsiveUI` hub (**added disabled**) and a `ResponsiveUiController` already wired to both.

To do it by hand on an existing screen:

1. Add `ResponsiveUI` to your root Canvas. One hub per Canvas; nested canvases are fine.
2. Add `ResponsiveUiController` on that Canvas or on a child. Its **Hub** field auto-assigns from
   the parent Canvas, or the root Canvas if the nested one has no hub. Several controllers can
   share one hub.
3. Enable the `ResponsiveUI` component once it is wired up. Nothing is applied while the hub
   component is disabled — serialized edits are still saved, they just do not touch the scene.

On prefabs, **leave Hub empty**. `OnEnable` and `OnTransformParentChanged` resolve the hub from the
parent Canvas, so an instance spawned under the Canvas binds itself with no serialized scene
reference.

## 4. Make a widget responsive

1. Select the `ResponsiveUiController`, open the **Widgets** foldout, and press `+` on
   **Widget Items**.
2. Assign the **Target** `RectTransform` you want to lay out. The first assign copies the live rect
   into every slot, so you start from what is already on screen.
3. Repeat for every element that needs to move. Rows in one list may be nested: Apply sorts parents
   before children.
4. For HUD roots you want to **show or hide** rather than move, use **Active Objects** instead —
   the widget Target itself is never activated or deactivated by Apply.

## 5. Author a slot per form factor

Each widget row carries twenty slots: ten form factors × Portrait/Landscape. Only the build
target's forms are listed.

1. Author **Phone Portrait** first. It is the slot most fallback chains end on, so a good Phone
   Portrait layout is a working layout everywhere.
2. Tick **Enabled** on a slot you want to differ. Enabling a Cover / Pad / Fold / Flip slot copies
   the matching Phone slot as a starting point, so you adjust rather than start blank.
3. Edit **Pos X/Y + Width/Height** on fixed axes, or **Left/Right** / **Top/Bottom** on stretched
   axes. The Inspector shows whichever matches the anchor preset.
4. Turn on **Scale From Design Canvas** for a slot that should scale with the screen, and pick a
   **Scale Mode** (`Auto` is the default for new slots).
5. Leave a slot **Disabled** when the fallback is good enough. Disabled Cover / HalfOpened / Pad
   falls back to Phone; a disabled Wide Screen slot only tries the other Wide Screen orientation.
   [CONCEPTS.md](CONCEPTS.md) has the full chain.

To copy one slot across every row at once, use the collapsed **Bulk Copy … Slots** panel at the top
of the Widgets foldout: pick **From** and **To**, confirm, and every row is rewritten (Undo works).

## 6. Preview it in the Device Simulator

1. Open **Window → General → Device Simulator**.
2. Pick a profile. The reference devices you chose in step 2 are listed at the top with their role
   labels. Phone and pad fixtures come from the package catalog; if they are missing, run
   **Responsive UI → Devices → Enable Phone & Pad Simulator Skins**.
3. The hub's **Detection** header shows the detected form and orientation, and the matching slot
   foldout auto-expands. Rotate the device to see the Landscape slot.
4. Edit the active slot and the Target updates live. Toggle **Enabled** anywhere in the live
   fallback chain and the resolved layout previews immediately, so you can see what a disabled slot
   actually falls back to.
5. For fold and flip postures, use the **Android Fold & Flip** Simulator panel (Android build
   target) or the **iPhone Duo** panel (iOS build target) to switch Cover / Opened / Half Opened.
6. If you prefer a fixed Game View instead of the Simulator, tick **Override In Editor** on the hub
   and pin a form and orientation by hand.

## 7. Add a posture detector for foldables

Android half-open forms (Fold Half Opened, Fold Cover Half Opened, Flip Half Opened) and iPhone Duo
Seated need an **active** `FoldablePostureDetector` at runtime. Without one, those devices resolve
to Phone or Pad.

1. Run **Responsive UI → Scene → Create Fold & Flip Setup** in the scene that should host the
   detector, or **Create Fold & Flip Complete Scene** for a fresh scene. Both build a hierarchy
   containing a `FoldablePostureDetector` and an optional Play Mode `FoldableSimulator`.
2. Set **Device Kind** on the detector (book-style fold vs clamshell flip) so product states get
   the right labels and defaults.
3. Leave **Persist Across Scenes** on. The detector then survives scene loads in Play Mode, so one
   detector in the scene that hosts it covers the whole session, and a later duplicate destroys
   itself. Keep exactly one detector as the native bridge owner in a build.

## 8. Before you build

| Check | Where |
|-------|-------|
| **Validate Setup** passes | **Responsive UI → Project → Validate Setup** |
| Player orientations match the slots you authored | **Project Settings → Player**. Slots for disallowed orientations are hidden; iPhone Duo Seated needs Auto Rotation with one axis |
| Recommended Android settings applied | **Responsive UI → Project → Apply Android Fold & Flip Recommended Settings** |
| A posture detector is in the scene that hosts it | step 7 |
| Pose behaviour reviewed | **Project Settings → Responsive UI → Android Fold / Android Flip / iPhone Duo** |
| Scenes you did not open are re-saved after changing **Background Cover Size** | the size is baked per scene for player builds |
| Design canvas sizes are set for every form you ship | step 2 — unset slots fall back to built-in form sizes in players |

## Next steps

- [CONCEPTS.md](CONCEPTS.md) — the model behind slots, fallback and design scale.
- [COMPONENTS.md](COMPONENTS.md) — cameras, scroll views, text banners, two-side tweens, events.
- [SETTINGS.md](SETTINGS.md) — every Project Settings field, including the pose cards.
- [PLATFORMS.md](PLATFORMS.md) — what each platform can report, and adding a new adapter.
- [DEVICE-TESTING.md](DEVICE-TESTING.md) — verifying on real hardware.
- [PRODUCTION.md](PRODUCTION.md) — the release checklist and what ships.
