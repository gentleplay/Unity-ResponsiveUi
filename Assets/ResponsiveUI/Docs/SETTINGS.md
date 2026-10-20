# Settings

Reference for **Edit → Project Settings → Responsive UI** and its device sub-pages: what each page is for, what every field does, what the defaults are, and where the values are stored. Component Inspectors are in [COMPONENTS.md](COMPONENTS.md); the slot model and design canvas behind these settings are in [CONCEPTS.md](CONCEPTS.md).

## The pages

| Page | Settings path | What it is for | Stored in |
|------|---------------|----------------|-----------|
| **Responsive UI** | `Project/Responsive UI` | Project-wide authoring defaults: the Enabled-toggle lock, the design canvas per form factor, the background art size, the Simulator hinge guide | `ProjectSettings/ResponsiveUiSettings.asset` |
| **Android Fold** | `Project/Responsive UI/Android Fold` | How the game behaves while an Android Fold is half opened — one card for a Fold, one for a Fold with a cover display | `Assets/Resources/ResponsiveUI/FoldableSettings.json` |
| **Android Flip** | `Project/Responsive UI/Android Flip` | How the game behaves while an Android Flip is half opened — one card | the same `FoldableSettings.json` |
| **iPhone Duo** | `Project/Responsive UI/iPhone Duo` | How the game behaves on each iPhone Duo posture: a Seated and a Flat card, the Cover rule, launch, timings, diagnostics | `Assets/Resources/ResponsiveUI/IphoneDuoSettings.json` + iOS `Info.plist` keys |

The three device pages are also reachable from the main page's page list, and the iPhone Duo page from **Responsive UI → Project → iPhone Duo Settings**.

**The defaults equal each platform's own behaviour.** A project that never opens these pages behaves exactly as Android and iOS behave on their own. On the Android pages that is explicit: every card starts at *As Android gives*, and a card only differs once a developer changes it. A changed Android card applies only when **Default Orientation is Auto Rotation with more than one orientation allowed** — under a fixed Default Orientation, or Auto Rotation with a single orientation, the phone keeps its orientation and the cards are shown read-only. The settings JSON files do not exist until the first change: both pages say so, and both load the built-in defaults until then.

## Project Settings → Responsive UI

The main page, in the order it draws.

### Slot visibility help box

A read-only summary of how Player Settings → Resolution and Presentation drive slot visibility, ending with the project's current case: what Default Orientation says, whether Portrait slots are visible, whether Landscape slots are visible. The rules it states:

- An allowed axis shows and enables all form-factor slots; a disallowed axis hides and disables them.
- **Fold Half Opened Landscape** and **Fold Cover Half Opened Landscape** stay shown either way.
- **iPhone Duo Seated** (both slots) stays shown, because while Seated the device allows only the hold's axis.
- **Wide Screen** (both slots) is on for Windows / macOS / Linux / WebGL targets and off on iOS and Android; it ignores the mobile lock.

### Lock Enabled Toggles

| Field | Meaning | Default |
|-------|---------|---------|
| Lock Enabled Toggles | Makes every slot **Enabled** checkbox read-only in the widget, camera, and scroll Inspectors, so an authored slot cannot be switched off by accident | **On** |

### Design Scale Defaults

One row per form factor the build target shows (Android and iOS forms are listed together; Wide Screen follows the build target). Each row picks the catalog device whose **portrait** size becomes that form's design canvas — which in turn locks widget **Design Canvas Size** and camera **Design UI Size**.

| Field | Meaning |
|-------|---------|
| Reference Device | A searchable, scrollable catalog profile. Phone and Pad rows list the phone/pad catalog; the fold, flip, and iPhone Duo rows list the fold/flip catalog |
| Model Name | Read-only label from that profile |
| Design Canvas Size | Read-only portrait width × height from that profile |
| Default Scale | An extra multiplier for the form, usually `1, 1` |

**Wide Screen has no catalog device**, so its row edits **Model Name** and **Design Canvas Size** directly, and offers **Use Pad Design Canvas** to copy the Pad size once. Design Scale then follows the live window or browser size.

Built-in reference devices and their portrait design canvases:

| Form factor | Built-in model | Portrait size |
|-------------|----------------|---------------|
| Phone | iPhone 17 Pro Max | `1320 × 2868` |
| Pad | iPad Pro 13 M5 | `2064 × 2752` |
| Fold Cover | Galaxy Z Fold8 Cover | `1248 × 1972` |
| Fold Half Opened | Galaxy Z Fold7 | `1968 × 2184` |
| Fold Cover Half Opened | Galaxy Z Fold8 | `1848 × 2448` |
| Flip Cover | Galaxy Z Flip8 Cover | `948 × 1048` |
| Flip Half Opened | Galaxy Z Flip8 | `1080 × 2520` |
| iPhone Duo Cover | iPhone Duo Cover | `1398 × 2034` |
| iPhone Duo Seated | iPhone Duo | `2007 × 2853` |
| Wide Screen | Wide Screen 1920×1080 | `1080 × 1920` (stored portrait) |

Changing a reference device re-Applies loaded `ResponsiveUI` hubs and `ResponsiveCamera` components immediately, enables that profile's Device Simulator skin, and rewrites the Simulator's friendly names (refresh the Simulator list to see them). Reference devices are marked ◆ in the catalogs, listed at the top of the Device Simulator list with role labels, and stay enabled when you use Disable All. The hub Canvas Scaler keeps its own authored refs — design canvas never overwrites them.

**Reset Design Defaults to Preferred Devices** puts every row back to the built-in reference above.

### Background Cover Size

One project-wide art size for every cover-fit background `Image` on a `ResponsiveUiController`. It is independent of the per-form design canvas unless you follow one.

| Field | Meaning |
|-------|---------|
| Form Factor | Which catalog to pick from. Wide Screen has no catalog and always follows its design canvas |
| Follow Design Canvas Reference | Use that form's Design Scale Defaults size instead of a separate profile. Disabled for a form with no catalog reference |
| Orientation | Catalog sizes are stored portrait; Landscape swaps to the device's landscape W×H |
| Reference Device | The catalog profile whose screen size becomes the art size (hidden while Follow is on, which shows a **Following** row instead) |
| Size | Read-only resolved width × height |

Default: **iPad Pro 13 M5 landscape, `2752 × 2064`**. **Reset Background Cover Size to iPad Pro 13 M5** restores it.

Every hub **without** Override Background Size picks up a change immediately in **loaded** scenes. Unloaded scenes bake the size when they open, so open and save them before a player build. `ResponsiveCamera` does not use this size at all.

### Fold Half-Open Guide

| Field | Meaning | Default |
|-------|---------|---------|
| Draw Half-Open Hinge Line | Show the single Device Simulator divide line while a half-open inner screen is selected | On |
| Hinge Line Color | Line colour, with alpha | `(0.25, 0.25, 0.25, 0.4)` |
| Hinge Line Thickness (px) | Slider, `1`–`10` | `5` |

There is **one overlay for the whole project**, not one per `ResponsiveUI`. The same three fields appear in the hub Inspector's **Fold Half-Open Guide** section and write these project settings. The line is drawn only in the Device Simulator on inner half-open chrome — never in the Scene view, the Game view, or a player build.

## The device pages

All three device pages share the same shape: a Player Settings banner, one card per pose, then a footer. Every label and value carries a tooltip with an example.

### The Player Settings banner

Each page starts with **"This game allows: …"** read from Player Settings → Resolution and Presentation, plus an **Open Player Settings** button. The cards below show only what that case can reach, and there are four cases:

| Case | Banner | What the cards show |
|------|--------|---------------------|
| **Landscape only** | `Landscape only` | Auto Rotation with only landscape allowed. One design canvas row per pose, unless the card follows the device |
| **Portrait only** | `Portrait only` | Auto Rotation with only portrait allowed. Same, mirrored |
| **Portrait + Landscape** | `Portrait + Landscape` | Both axes reachable, so **While rotating** is editable and both design canvas rows can appear |
| **Fixed** | `<orientation> only (fixed Default Orientation)` — or `<orientation> only (Auto Rotation with one orientation)` | **Fixed by Player Settings**: the cards show the platform defaults read-only and cannot be changed until Default Orientation is Auto Rotation (or a second orientation is allowed) |

Two readings of the same Player Settings:

- The **Android** pages treat *Auto Rotation with a single orientation allowed* as **Fixed** as well, because the phone simply keeps that orientation. The cards then display the platform defaults read-only, in the Editor exactly as on the phone, and a note says any saved choices apply again under Auto Rotation.
- The **iPhone Duo** page reads Fixed only from a non-Auto-Rotation Default Orientation. Under a fixed Default Orientation it warns that Unity ignores runtime orientation changes, so the rules have no effect and iOS letterboxes the inner display held across the lock. With **both** axes allowed, nothing is ever "held the other way", so those rules are hidden and a note says Seated follows its While rotating rule.

**Unreachable options are left out, not disabled.** A case that cannot produce a second screen shape shows one design canvas row, not a greyed-out second one; a dropdown lists only the values that case can reach, plus the currently saved value so a stored choice never disappears silently.

### The pose card

Each card is a foldout. Collapsed, the title carries a one-line summary (the current main choice plus the layout slots it gives). Expanded, it draws a small phone picture — tall or wide, with the hinge in orange across or down the middle — beside a one-line description of the pose, then the fields.

#### Android Fold and Android Flip cards

| Field | Values | Default |
|-------|--------|---------|
| **Layout slot** | Read-only. The slot this pose shows, e.g. `Fold Cover Half Opened · Landscape (wide)`. Two entries separated by `/` when the card can give both shapes | — |
| **Content runs** | **As Android gives (default)** · **Top → down (tall)** · **Left → right (wide)** | As Android gives |
| **Hinge line** | Read-only, follows Content runs: `Across the middle`, `Down the middle`, or `Across the middle when tall, down the middle when wide` | — |
| **While rotating** | **As Android gives (default)** · **Locked** · **Follow device** — only the default plus the one reachable value is offered: *Follow device* under a one-axis lock, *Locked* with both axes | As Android gives |
| **Design canvas · Portrait / Landscape** | One row per screen shape the card can give. The Portrait row is read-only from the reference device; the Landscape row is an editable `W × H` | From the reference device |

How the fields interact:

- **Content runs** fixes the screen shape, so when it is not *As Android gives*, **While rotating** becomes read-only: *"Keeps the Content runs shape"*.
- A one-axis game whose **While rotating** is still *As Android gives* shows it read-only as **Fixed by Player Settings** — the Player lock already decides — with a **Follow device instead** button that switches it to *Follow device* in one click.
- Picking a *Content runs* value that happens to be what Android already gives here notes that the game will still hold its orientation while the pose lasts.
- Whenever the card needs the runtime to hold the orientation itself, the card says so: *"On the phone the game turns its own orientation while this pose lasts."* The hold is an **axis**, not one orientation — Auto Rotation with that axis's two flags — so a 180° turn still shows upright, and the Player Settings orientation returns once the pose ends.

The pages and their cards:

| Page | Cards |
|------|-------|
| **Android Fold** | *Half opened · Fold (e.g. Galaxy Z Fold7)* and *Half opened · Fold Cover (e.g. Galaxy Z Fold8)*, both described as "Inner screen bent part-way open, standing like a laptop or held like a book" |
| **Android Flip** | *Half opened*, "Clamshell bent part-way open, standing like a small laptop" |

Which card a device uses at runtime: the Flip card for a flip, otherwise the Fold Cover card when the half-open inner display is Fold-Cover-sized, else the Fold card.

#### iPhone Duo Seated card

"Bent like a laptop standing on a table, or held open like a book."

| Field | Values | Default |
|-------|--------|---------|
| **When held the other way than the game allows** (one-axis games only) | **Rotate** — the game rotates to the hold and uses the Seated · Portrait or Seated · Landscape slot · **Turned a quarter (Sideways)** — the game keeps its orientation, full screen, turned 90° · **iOS letterbox (System Default)** | Rotate |
| **While rotating** | **Locked** — Seated keeps one pose until the Duo is flattened or closed · **Follow device** — laptop → Seated · Portrait, book → Seated · Landscape | Locked |
| **Locked pose** (only while *Locked*) | **Auto** — landscape only → tall laptop, portrait only → wide book, Portrait + Landscape → the hold Seated started with · **Tall laptop (Seated · Portrait)** · **Wide book (Seated · Landscape)** | Auto |
| **Layout slot** | Read-only: `iPhone Duo Seated · Portrait (tall laptop)` and/or `iPhone Duo Seated · Landscape (wide book)` | — |
| **Hinge line** | Read-only: across the middle as a laptop, down the middle as a book | — |
| **Design canvas · Portrait / Landscape** | One row per reachable Seated slot, from the iPhone Duo Seated reference device | `2007 × 2853`, oriented per slot |

A *Locked* Seated whose pose already sits on a one-axis game's own axis is never held across it, so the card notes that the across-the-lock rule is never used there.

#### iPhone Duo Flat card

"Opened all the way and flat, like a small tablet." Flat has no hinge line — it uses the **Pad** slots, shared with every iPad.

| Field | Values | Default |
|-------|--------|---------|
| **When held the other way** (one-axis games only) | **Turned a quarter (Sideways)** — the game stays as it is, full screen, turned 90°, also after a cold start or unfolding tall · **Rotate** — the game rotates to the hold (it needs that layout) · **iOS letterbox (System Default)** | Turned a quarter (Sideways) |
| **Sideways turn** (shown when Seated or Flat can turn sideways) | **Left 90°** — the turn verified on the Xcode iPhone Duo simulator · **Right 90°** — use it when Left 90° reads upside down on your device | Left 90° |
| **While rotating** | **Follow device** — the hold, *When held the other way*, and *Flip 180°* decide · **Locked** — Flat keeps one screen until folded or closed; the flipped side still shows it upright | Follow device |
| **Locked pose** (only while *Locked*) | **Auto** — landscape only → wide, portrait only → tall, Portrait + Landscape → the screen Flat started with · **Wide (Pad · Landscape)** · **Tall (Pad · Portrait)** | Auto |
| **Flip 180° on the opposite side** (one-axis games only) | On: turning the open Duo to the opposite side of the game's axis flips the game upright. Off: it stays upside down | On |
| **Layout slot** | Read-only: `Pad · Landscape (wide)` and/or `Pad · Portrait (tall)`, "(same as every iPad)" | — |
| **Design canvas** | Read-only: "Same as every iPad: edit under Pad" | Pad design canvas |

Two notes the card adds for a one-axis game:

- A *Locked* Flat that keeps a hold of its own, with *When held the other way* on **iOS letterbox**, is letterboxed when held across the game's axis and full screen on its own axis.
- A *Locked* Flat on the game's own axis never rotates, so **Rotate** behaves as **Turned a quarter (Sideways)** there; the card says so rather than silently changing the value.

#### Other postures (as before)

A collapsed foldout for the postures without a card:

| Field | Values | Default |
|-------|--------|---------|
| **Cover (Closed), held the other way** | **System Default (like any iPhone)** — iOS shows the game sideways · **Rotate** — the game rotates to the hold | System Default |

*Turned a quarter (Sideways)* is deliberately **not** offered here: on the cover display it is what System Default already does.

#### Advanced

A collapsed foldout with three groups:

| Group → field | Meaning | Default |
|---------------|---------|---------|
| **Launch (iOS build)** → Launch screen fills the display | iOS builds: iOS's launch screen fills the display when the game starts held the other way than it allows. Side effect on **every** iPhone: launched that way, the launch screen shows on the other axis and the game then rotates to its own. iPads are unchanged; applied at the next iOS build | Off |
| **Timings (seconds)** → Leave Seated delay | Seconds the posture must stay out of Seated before the Cover rules apply, because iOS resizes the scene in steps while it rotates | `0.3` |
| **Timings** → Flat lock settle | Flat: seconds the game must stay upright before the orientation lock returns (covers the rotation animation) | `0.5` |
| **Timings** → Rotation retry | Flat: seconds before a flip request iOS has not carried out is sent again. Minimum `0.05` | `0.5` |
| **Diagnostics** → Trace log (development builds) | Log one line per posture or orientation change to the Xcode console | On |
| **Diagnostics** → Debug overlay | Development builds and the Editor show posture, hold, and screen mode in a screen corner | Off |

Timing fields commit on Enter or focus loss, not on every keystroke.

#### Preview

Below the cards, **Preview (current Player orientation)** resolves the project's rules for each posture and hold and prints the outcome in plain language: Cover · tall, Cover · wide, Seated · laptop, Seated · book, Flat · tall, Flat · wide. The Device Simulator's iPhone Duo panel shows the same decision for the selected hold.

### Footer: Reset, Select, Reload

Every device page ends with the same three buttons:

| Button | What it does |
|--------|--------------|
| **Reset to Defaults** | After a confirmation dialog, puts that page's cards back to the platform's own behaviour — on the Android pages, every card returns to *As Android gives*; on the iPhone Duo page, every rule returns to its default (Seated back to Rotate, Locked, Auto) |
| **Select Settings File** | Pings the saved JSON in the Project window, for example to check it into version control. Disabled while no file exists |
| **Reload** | Reads the settings file again, for example after a version-control update changed it |

While no settings file exists the page notes: *"Using the built-in defaults: the first change creates the settings file."* The iPhone Duo page's save also updates the live runtime rules, so Play Mode picks a change up at once.

## Where settings are stored

| File | Holds | Notes |
|------|-------|-------|
| `ProjectSettings/ResponsiveUiSettings.asset` | Lock Enabled Toggles, Design Scale Defaults per form (reference profile id, model name, portrait design canvas, Default Scale, and the per-form Landscape row size), Background Cover Size, the hinge guide | A Unity `ScriptableSingleton` in the project folder, not under `Assets/`. The Landscape row size is Editor-only: it seeds new Landscape slots and drives previews and background art, while runtime layout and camera capture keep the per-form size so captures round-trip |
| `Assets/Resources/ResponsiveUI/FoldableSettings.json` | The Android Fold, Fold Cover, and Flip half-open cards | Loaded as a `TextAsset` from `Resources`, so it is available on every build target. A missing file or field falls back to the platform defaults; unreadable JSON logs one warning and uses the defaults |
| `Assets/Resources/ResponsiveUI/IphoneDuoSettings.json` | Every iPhone Duo rule, timing, and diagnostic | Same `Resources` loading. Android and desktop builds simply never read it. Readable and replaceable at runtime through `IphoneDuoDevice.Rules` |
| The iOS `Info.plist` | The keys below, written after every iOS build | Needed because the native launch hold runs before any C# |

### iOS Info.plist keys

A post-build step writes these so the splash and first scenes already follow the posture rules.

| Key | Type | Values |
|-----|------|--------|
| `FDPIphoneDuoLaunchHold` | Boolean | True when Default Orientation is Auto Rotation, i.e. when the rules apply at all |
| `FDPIphoneDuoSeatedRule` | Integer | Seated *When held the other way*: `0` Sideways · `1` Rotate · `2` System Default |
| `FDPIphoneDuoFlatRule` | Integer | Flat *When held the other way*: `0` Sideways · `1` Rotate · `2` System Default |
| `FDPIphoneDuoSeatedRotation` | Integer | `0` Locked · `1` Follows hold |
| `FDPIphoneDuoSeatedLockedPose` | Integer | `0` Auto · `1` Seated Portrait · `2` Seated Landscape |
| `FDPIphoneDuoFlatRotation` | Integer | `0` Follow device · `1` Locked |
| `FDPIphoneDuoFlatLockedPose` | Integer | `0` Auto · `1` Wide · `2` Tall |
| `FDPIphoneDuoSidewaysQuarterTurns` | Integer | `+1` Left 90° · `−1` Right 90° |
| `FDPIphoneDuoTraceLog` | Boolean | Trace log **and** a development build |
| `FDPIphoneDuoModels` | Array of strings | The hinge model list the bridge accepts |
| `UISupportedInterfaceOrientations~iphone` | Array of strings | Written **only** when *Launch screen fills the display* is on, under Auto Rotation with a one-axis lock: the Player orientations plus the other axis. iPads keep their own list, and the game still runs on its own axis — Unity's view controller allows only the Player orientations at runtime |

## Related

- [CONCEPTS.md](CONCEPTS.md) — form factors, slots, the fallback chain, design canvas and Design Scale
- [COMPONENTS.md](COMPONENTS.md) — the runtime components these settings feed
- [GETTING-STARTED.md](GETTING-STARTED.md) — install and first responsive widget
- [PLATFORMS.md](PLATFORMS.md) — per-platform behaviour and the form-factor contract
- [../FoldAndFlip/README.md](../FoldAndFlip/README.md) — the Android fold and flip module behind the Android pages
- [../IphoneDuo/README.md](../IphoneDuo/README.md) — the iPhone Duo module, its runtime API, and its Device Simulator panel
