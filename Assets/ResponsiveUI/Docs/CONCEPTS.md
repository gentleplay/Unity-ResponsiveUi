# Concepts

The model every other document assumes. Read it once: form factors, orientation, slots, the
fallback chain, the design canvas and design scale, who applies what, and where the Device
Simulator fits.

## Form factors

A **form factor** is how the package classifies the live screen. The list is closed and stable —
ten values, in a fixed order, because that order *is* the slot order on every component
(`ResponsiveWidgetLayout.FormFactors`).

| # | Enum value | Inspector label | Platform group | What it means |
|--:|-----------|-----------------|----------------|---------------|
| 0 | `Phone` | **Phone** (iPhone on iOS) | shared | A tall phone screen. Also a tall foldable cover, and a clamshell flip while it is open and flat. |
| 1 | `Pad` | **Pad** (iPad on iOS) | shared | A tablet-like screen. Also a book-style foldable opened flat, and an iPhone Duo opened flat. |
| 2 | `FoldPhoneCover` | **Fold Cover** | Android | A squat "passport" cover on a book-style foldable (~10:16, far less tall than a phone). A *tall* cover stays `Phone`. |
| 3 | `FoldHalfOpened` | **Fold Half Opened** | Android | A book-style foldable held part-open on its inner screen. |
| 4 | `FoldCoverHalfOpened` | **Fold Cover Half Opened** | Android | The same posture on a passport-cover foldable, whose inner screen is a different shape. |
| 5 | `FlipPhoneCover` | **Flip Cover** | Android | A clamshell flip closed, on its small outer cover screen. |
| 6 | `FlipHalfOpened` | **Flip Half Opened** | Android | A clamshell flip held part-open on its inner screen. |
| 7 | `iPhoneDuoCover` | **iPhone Duo Cover** | iOS | iPhone Duo closed or propped standing, on the outer display. |
| 8 | `iPhoneDuoSeated` | **iPhone Duo Seated** | iOS | iPhone Duo part-open on the inner display: a tall laptop hold with a horizontal crease, or a wide book hold with a vertical crease. |
| 9 | `WideScreen` | **Wide Screen** | desktop / web | A Windows, macOS, Linux or WebGL window, whatever its shape. |

Two rules follow from the platform grouping:

- **A player only ever reports its own platform's forms.** An Android build cannot resolve iPhone
  Duo Seated; a desktop or WebGL build is always Wide Screen, sized from the window, with no mobile
  detection at all.
- **In the Editor, a selected Device Simulator profile wins.** It drives detection and the fallback
  chain on any build target, so you can author Fold Cover and iPhone Duo slots from one Editor.

Half-open forms (3, 4, 6, 8) and the cover forms (2, 5) also need a posture source — see
[PLATFORMS.md](PLATFORMS.md).

## Orientation

Orientation is just **Portrait** or **Landscape**, and it pairs with a form factor to name a slot.
It is detected from the watched screen size, except where an override or a Simulator pin sets it.

One wrinkle is worth remembering:

| Form | What the slot orientation names |
|------|--------------------------------|
| **Fold Half Opened**, **Fold Cover Half Opened** | the **posture**, not the screen shape. Landscape is the flat inner screen turned 90° — a *tall* screen with a left-to-right hinge. Portrait is the flat inner screen — a *wide* screen with a top-to-bottom hinge. |
| every other form, including Flip Half Opened and iPhone Duo Seated | the **screen shape** — Landscape is wider than tall. |

`ResponsiveDevice.OrientationForForm` performs that mapping and is its own inverse. The Player's
allowed orientations hide slots whose axis the build cannot reach, with two exceptions: both Wide
Screen slots always show (a window can be any shape), and both iPhone Duo Seated slots always show
(while Seated, the device allows only the hold's own axis).

## Slots

A **slot** is one form factor × one orientation: 10 × 2 = **twenty slots**. Widgets, cameras and
scroll views all carry the same twenty, in the same order, with the same Inspector behaviour. Unity
YAML binds slot fields by name, so the declaration order can change without migrating scenes.

A widget slot (`ResponsiveRectState`) stores exactly this:

| Field | Meaning |
|-------|---------|
| **Enabled** | Whether Apply may use this slot at all. Off means "fall back", not "hide". |
| **Anchor Min / Max**, **Pivot** | The `RectTransform` anchor preset and pivot for this slot. |
| **Anchored Position** | Position, in this slot's design-canvas pixels. Shown as Pos X/Y on fixed axes. |
| **Size Delta** | Size, Unity's canonical value. Shown as Width/Height on fixed axes and Left/Right or Top/Bottom on stretched axes. |
| **Default Scale** | The origin `localScale` XY, normally (1,1). |
| **Scale From Design Canvas** | Whether design scale is applied to this slot. |
| **Scale Mode** | Which ratio design scale uses (table below). New and Reset slots start on `Auto`. |
| **Design Canvas Size** | Normally serialized (0,0), meaning "follow Project Settings for this form". The Inspector shows the resolved size, oriented to the slot. |

Cameras store a parallel per-slot state (viewport, local position and rotation, projection, focus)
and scroll views store Cell Interval and Scroll Offset. Active Objects are the odd one out: they
store one **bool per slot** and have no fallback — unticked means hidden in that slot, never "reuse
Phone".

## The fallback chain

A disabled slot is not a hole. Apply walks an ordered candidate list (up to
`ResponsiveWidgetSlotResolver.MaxCandidates` = 5) and uses the first **enabled** slot it finds. The
same chain drives runtime Apply, Editor capture and the Inspector preview, so what you see while
authoring is what ships.

| Requested slot | Candidates, in order |
|----------------|----------------------|
| **Phone** Portrait | Phone Portrait |
| **Phone** Landscape | Phone Landscape → Phone Portrait |
| **Pad**, **Fold Cover**, **Flip Cover**, **Flip Half Opened**, **iPhone Duo Cover** (Portrait) | exact → Phone Portrait |
| the same forms (Landscape) | exact → same form Portrait → Phone Landscape → Phone Portrait |
| **Fold Half Opened**, **Fold Cover Half Opened** | exact → Phone of the same **screen shape** → Phone Portrait (only when that shape is landscape). The form's other slot is never a candidate: it is the opposite shape. |
| **iPhone Duo Seated** | exact → Pad (same shape) → Pad (other shape) → Phone (same shape) → Phone (other shape) |
| **Wide Screen** | exact → the other Wide Screen orientation. Never a mobile slot. |

Three things to keep in mind:

- **Candidates from another platform's forms are skipped** on a player, so a chain never resolves to
  a slot the build cannot reach. In the Editor, a selected Simulator profile suspends that filter
  and applies its own chain.
- **If nothing in the chain is enabled**, no layout is applied and the Target keeps whatever it was
  authored with.
- **Enabling a slot seeds it** from the first enabled candidate in a near-identical *seed* chain, as
  a one-time authoring copy: Cover / Pad / Fold / Flip copy the matching Phone slot, iPhone Duo
  Seated copies Pad then Phone by shape, and Wide Screen copies Pad once (at runtime Wide Screen
  still never reads a mobile slot).

## The design canvas and design scale

Every form factor has a **design canvas**: the pixel size your slot numbers are expressed in. You
pick it once per form in **Project Settings → Responsive UI** by choosing a catalog reference
device; its portrait size becomes the design canvas and also locks the camera's read-only
**Design UI Size**. Wide Screen has no reference device and its size is edited directly. If a form
has no setting — in a player build, where the Editor provider is absent — the built-in size for that
form is used.

| Form | Built-in design canvas (stored portrait) |
|------|------------------------------------------|
| Phone | 1320 × 2868 |
| Pad | 2064 × 2752 |
| Fold Cover | 1248 × 1972 |
| Fold Half Opened | 1968 × 2184 |
| Fold Cover Half Opened | 1848 × 2448 |
| Flip Cover | 948 × 1048 |
| Flip Half Opened | 1080 × 2520 |
| iPhone Duo Cover | 1398 × 2034 |
| iPhone Duo Seated | 2007 × 2853 |
| Wide Screen | 1080 × 1920 |

Sizes are always stored portrait; the Landscape slot orients them, and a posture-named slot orients
them by screen shape.

**Design scale** is how a layout authored at one of those sizes reaches a different screen. With
**Scale From Design Canvas** on, Apply computes a ratio from the live canvas size against the
slot's design canvas, then:

| What | Happens |
|------|---------|
| `anchoredPosition` | multiplied by the ratio |
| `localScale` | `Default Scale × ratio`, on both axes |
| a **fixed** axis | Width / Height stays as authored — the transform scale does the work |
| a **stretched** axis | `sizeDelta` compensates the live parent anchor span, so the final edge offsets scale exactly once instead of twice |
| under a **Scale With Screen Size** `CanvasScaler` | the ratio is the canvas-local residual against the slot's design canvas; raw screen pixels never enter the calculation |
| nested widget rows in one list | parents are applied before children, and a child's ratio divides out the ancestor scale so the canvas ratio is not squared |

With Scale From Design Canvas off, the slot is applied exactly as authored and `localScale` is
restored to **Default Scale**.

| Scale Mode | Ratio it uses |
|------------|---------------|
| **Width** | width ratio, uniform on both axes |
| **Height** | height ratio, uniform on both axes |
| **Auto** (default) | Width or Height chosen from the live aspect against this form's design canvas — wider than the design canvas keeps the vertical span, taller keeps the horizontal span. Same fit as UniformMin. |
| **UniformMin** | `min(width, height)` ratio — fit inside |
| **UniformMax** | `max(width, height)` ratio — cover / fill |
| **IndependentXY** | width ratio on X, height ratio on Y |

### Identity: when the numbers are the numbers

On the device a slot was designed for, the ratio is **1**, and the Inspector values land on the live
object unchanged. That is deliberate and it is what makes the Inspector readable. Identity holds
when the watched size matches the slot's design canvas (within about 1%), and also when the detector
or Simulator says the posture *owns* that slot — a flat or half-open posture is identity without
needing the screen to match a catalog entry exactly, which is what makes Simulator safe-area sizes
behave.

Two cases are deliberately **not** identity, and both surprise people once:

- A **constrained nested Canvas pane** scales against the pane, not the device.
- A **Cover / Fold / Flip slot Disabled** falls back to Phone and therefore keeps the *Phone* design
  canvas, not the form's own. Enable the dedicated slot if you want that form's coordinate space.
  This differs from the mapped states that intentionally own a shared slot — a flip opened flat uses
  the Phone slot, a fold opened flat uses the Pad slot, and both are identity.

The live size used for all of this is the watched screen size — the Simulator profile's size, then
`UnityEngine.Device.Screen`. The Editor's plain `UnityEngine.Screen` is the Game View widget and
never drives the ratio.

## The hub and handler contract

`ResponsiveUI` is the **hub**, one per Canvas. It derives from `ResponsiveUiHandler`, which holds
the detection and Apply pipeline; both run with `[ExecuteAlways]`, which is why authoring previews
work outside Play Mode. Never add a separate `ResponsiveUiHandler` beside `ResponsiveUI` — the hub
already is one.

| Who | Responsibility |
|-----|----------------|
| `ResponsiveUiHandler` (base) | Resolve form factor × orientation; apply the optional `CanvasScaler` references; fire the per-slot layout `UnityEvent`s; re-Apply on fold `LayoutChanged`, focus and resume; notify clients. |
| `ResponsiveUI` (hub) | The background art size (and its override), the Play Mode form-transition cover, debug logging, and the client registry. |
| `IResponsiveUiClient` | Everything that *receives* layout: `ResponsiveUiController` (widgets, backgrounds, Active Objects), `ResponsiveCamera`, `ResponsiveScrollView`, `ResponsiveTextBanner`. Clients register on enable, unregister on disable, and get `ApplyFromHub(hub, layout, layoutOnly)`. They do **not** inherit the handler. |

How a client finds its hub: the **Hub** field if set, otherwise the `ResponsiveUI` on the parent
Canvas, otherwise the one on that Canvas's `rootCanvas`, otherwise the nearest `ResponsiveUI`
ancestor. The resolve runs again on `OnEnable` and `OnTransformParentChanged`, which is why prefabs
should leave the field empty.

When Apply runs:

| Trigger | Result |
|---------|--------|
| `Start` | First Apply, and the first layout-event fire |
| Screen size or orientation change | Re-detect and Apply. Size deltas under 2 px are ignored as sub-pixel or Simulator flicker |
| Fold posture `LayoutChanged` | Re-detect and Apply |
| App focus / resume | Re-detect and Apply, because posture may have changed while backgrounded |
| Device Simulator profile switch | A full Apply, then a canvas-ready pass that notifies **every** client |
| Same-form size tick in Play Mode | A layout-only Apply for the clients that ask for it |

Apply needs the hub **component enabled**. While it is disabled, Inspector edits are still
serialized — they simply do not touch the scene. Layout events fire from `Start` and then only when
the applied form × orientation actually changes.

## Where the Device Simulator fits

The Device Simulator is the authoring surface, not a separate code path. A selected profile:

- **drives detection** on any build target, so one Editor authors Android, iOS and desktop slots;
- **supplies the watched size** that design scale and identity use, instead of the Game View;
- **focuses the slot** — the matching foldout auto-expands and the others re-collapse when the
  profile or posture changes, and editing the active slot pushes straight onto the Target;
- **hosts the posture panels** — Android Fold & Flip on an Android build target, iPhone Duo on an
  iOS one — which publish the posture the detector would publish on device;
- **draws the half-open guide line** on inner chrome only, and removes it on phone, pad, cover and
  flat profiles.

The package ships its own phone, pad, fold and flip profiles; Unity's own
`com.unity.device-simulator.devices` package adds photoreal overlays beside them. Catalog entries
are editable JSON — see [SETTINGS.md](SETTINGS.md) and [../PhonePad/README.md](../PhonePad/README.md).

## Next

- [COMPONENTS.md](COMPONENTS.md) — every component, Inspector and API.
- [PLATFORMS.md](PLATFORMS.md) — which platform reports which forms, and posture sources.
- [GETTING-STARTED.md](GETTING-STARTED.md) — the hands-on path if you have not walked it yet.
