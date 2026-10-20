# Components

Reference for every runtime component in the package: what it does, how to add it, what each Inspector field means, the public API a game would call, and the gotchas. The slot model, the fallback chain, and Design Scale are explained once in [CONCEPTS.md](CONCEPTS.md) — this page assumes them.

New to the package? Start with [GETTING-STARTED.md](GETTING-STARTED.md). Per-platform behavior is in [PLATFORMS.md](PLATFORMS.md); the Project Settings pages are in [SETTINGS.md](SETTINGS.md).

## At a glance

| Component | Role | Owns slots? |
|-----------|------|-------------|
| `ResponsiveUI` | Canvas hub: detection, Canvas Scaler, events, client notify | No |
| `ResponsiveUiController` | Widget layout, background cover images, Active Objects | Yes (20 per widget) |
| `ResponsiveCamera` | Camera viewport, pose, projection, focus, background sprite | Yes (20 per camera) |
| `ResponsiveScrollView` | FancyScrollView Cell Interval / Scroll Offset | Yes (20, one layout) |
| `ResponsiveTwoSideUiTweenController` | UI that can sit on either side of the canvas, with a tween | Yes (20 × 4 layouts per widget) |
| `ResponsiveTextBanner` | Auto-width single-line TMP banner | No — follows a controller widget slot |
| Active Objects (on `ResponsiveUiController`) | Show / hide GameObjects per exact slot | Slot toggles only |
| Layout Events (on `ResponsiveUI`) | One UnityEvent per exact slot | Slot events only |

Every component except the hub implements `IResponsiveUiClient`: it registers with a hub on enable and receives `ApplyFromHub` whenever that hub resolves a layout.

## `ResponsiveUI` — the Canvas hub

### What it does

One scene-wide owner, placed on the Canvas. It resolves the live form factor and orientation, optionally drives the Canvas Scaler, fires the per-slot UnityEvent, and then notifies every registered `IResponsiveUiClient`. It does **not** own widgets, backgrounds, or Active Objects — those live on `ResponsiveUiController` clients.

### Add it

Add Component → `ResponsiveUI`, on the Canvas GameObject. Nested canvases are fine: the hub binds `GetComponentInParent<Canvas>(true)`, so an inactive parent Canvas still counts. **Responsive UI → Scene → Create Sample Canvas** builds a ready Canvas + hub + controller + background + widget. The component **starts disabled** when added, so you can wire clients before the first Apply; enable it when ready.

### Inspector

| Section → field | Meaning |
|-----------------|---------|
| **Detection** → Override In Editor | Lock form factor and orientation instead of detecting. Off: the next two rows become read-only **Detected Form Factor / Detected Orientation** |
| Editor Form Factor / Editor Orientation | The locked form and Portrait/Landscape used while Override In Editor is on |
| Editor Pad Max Aspect | Game View / Simulator aspect (max÷min) below this counts as Pad when no Simulator or posture pin applies. Default `1.6` |
| Show Other Slots | On (default): every allowed-orientation form foldout stays listed on clients so you can expand and edit it. Off: only the detected slot. Controller, camera, and scroll Inspectors read this one toggle |
| **Optional Canvas Scaler** → Apply Canvas Scaler | Normalize the assigned `CanvasScaler` to Scale With Screen Size / Match Width Or Height on every Apply |
| Canvas Scaler | The `CanvasScaler` to write. It is not auto-found: assign it, or nothing is written |
| Phone / Pad Reference Resolution + Match Width Or Height | Refs used for the phone-class and pad-class forms. Landscape swaps the axes |
| Wide Screen Reference Resolution + Match Width Or Height | Refs used on Wide Screen only (built-in `1080×1920`) |
| **Events** → Layout Events | One UnityEvent per exact slot. See [Layout Events](#layout-events) |
| **Fold Half-Open Guide** | Draw Half-Open Hinge Line / color / thickness. These write the **one project-wide** overlay in Project Settings, not a per-hub line ([SETTINGS.md](SETTINGS.md)) |
| **Debug** → Log Detection | Log each resolved layout |
| **Layout** → Override Background Size | This hub keeps its own background art size and ignores Project Settings → Background Cover Size |
| Original Bg Size | The designed pixel size used when Override is on. It is also baked for player builds |
| **Layout** → Form Transition Loading | See the table below |

Detection, Canvas Scaler, Events, hinge guide, and Debug start **collapsed**; the Detection title still shows the live form.

#### Form Transition Loading

A Play Mode cover for form switches, authored on the hub's Layout foldout.

| Field | Meaning |
|-------|---------|
| Enabled | Off (default): nothing runs, and no nested object work happens |
| Black Overlay | A full-screen stretched `Image`. Play Mode drives a `CanvasGroup` to alpha 1 for the whole Apply, then back to 0. Keep the Image color alpha at the cover opacity you want (usually 1) |
| Controller | An optional dedicated `ResponsiveUiController` (cover + text) drawn on top of the black overlay. Keep its root as a late sibling under the Canvas and **outside** other Active Objects |
| Min Visible Seconds | Realtime minimum the cover stays up after Apply finishes. Default `0.25` |
| Show On Slots | The same form × orientation grid as Active Objects: a form switch into a checked destination shows the cover. Default all on **except Flip Cover** |

It runs in **Play Mode only**, only when the **form factor** changes (not orientation alone), and only after the first Apply. An unchecked destination skips the cover and dismisses one already in flight. Edit Mode never force-hides the panel — activate the GameObject to author its scale and layout. One controller per hub; sharing across hubs is unsupported. Disabling the feature hides it immediately.

### API

```csharp
ResponsiveDevice.FormFactor   CurrentFormFactor { get; }
ResponsiveDevice.Orientation  CurrentOrientation { get; }
ResponsiveDevice.LayoutKey    CurrentLayout { get; }
bool                          ApplyInFlight { get; }

void Apply();                                  // full pass: resolve, scaler, events, notify
void RequestLayoutRefresh();                   // layout-only pass after canvases settle
void NotifyClients(bool layoutOnly, bool notifyAllLayoutClients = false);
void RegisterClient(IResponsiveUiClient client);
void UnregisterClient(IResponsiveUiClient client);

ResponsiveFormTransitionLoading FormTransitionLoading { get; }
Vector2 GetOriginalBgSize();
Vector2 GetDesignScaleCurrentSize(RectTransform target = null);
Vector2 GetLayoutRatio(/* slot, form, target … */);
Vector2 GetAppliedWidgetScale(/* … */);
void    ApplyWidget(/* … */);                  // one Target, ratio computed once
bool    UsesCanvasScalerResolutionFit { get; }
bool    UsesCanvasScalerResolutionFitFor(RectTransform target);

static Vector2 OrientSize(Vector2 size, ResponsiveDevice.Orientation orientation);
static Vector2 OrientSizeForSlot(Vector2 size, ResponsiveDevice.FormFactor form,
                                 ResponsiveDevice.Orientation orientation);
static Vector2 GetDefaultOriginalBgSize();
static Func<Vector2> OriginalBgSizeProvider;   // Editor configuration hook
```

Most games only ever call `Apply()` (or nothing at all — the hub applies itself on enable, on posture change, on focus / resume, and on size change).

### Notes

- **Apply order:** resolve layout → enter `ApplyInFlight` → optional form-transition cover → Canvas Scaler → events → notify clients → re-assert the cover → leave `ApplyInFlight` → hide after end-of-frame plus Min Visible Seconds.
- **Re-entrant Apply is safe.** `Apply()` / `NotifyClients` called while a pass is running (an Active Object enabling, a posture owner change, a UnityEvent) is coalesced and re-run once the outer pass finishes, up to four deferred passes — the shared notify lists are never cleared mid-iteration.
- **Failure isolation.** Each client's `ApplyFromHub` runs in its own try/catch. A client that throws (for example a widget Target destroyed at runtime) is logged against that component, and every later client still gets the new device state. Posture `LayoutChanged` and the Editor Simulator's hub loops isolate each subscriber the same way, so one failing hub cannot pin other hubs to the previous form.
- **While the hub is disabled**, client enable / rebind, direct notifications, and controller background Inspector LivePush do not apply widgets, backgrounds, or Active Objects.
- `RequireComponent` is **not** `Canvas`; the Inspector warns when no Canvas is found in the parents (inactive ones included).
- Do **not** add a separate `ResponsiveUiHandler` on the same GameObject — `ResponsiveUI` already is one.
- **Hub resolution for clients:** the assigned Hub field if set; else `ResponsiveUI` on the parent Canvas (disabled or inactive included); else that Canvas's `rootCanvas` hub; else an ancestor `ResponsiveUI`. There is no scene-wide "first hub" fallback. **Leave Hub empty on prefabs** so a Play Mode `Instantiate` finds the scene Canvas hub; a filled Hub stays until you clear it, and a later reparent does not retarget it. Changing Hub while the client stays enabled rebinds, so notifications and layout come from the same hub.

## `ResponsiveUiHandler` — the base type

`ResponsiveUI` derives from `ResponsiveUiHandler`, which holds the detection and Apply pipeline: form factor + orientation resolution, the optional Canvas Scaler write, the per-slot Play Mode UnityEvents, posture `LayoutChanged` / focus / resume re-Apply, and the client notify. Camera, scroll, two-side, and text banner components **do not** inherit it — they implement `IResponsiveUiClient`.

A scene script that needs a custom region can still inherit `ResponsiveUiHandler` and override `ApplyLayout` / `ApplyDeviceState` without a widget list. Add it on its own GameObject, never beside a `ResponsiveUI`.

## `ResponsiveUiController` — widgets, backgrounds, Active Objects

### What it does

The workhorse client. It owns per-widget form × orientation layout slots, cover-fit background `Image`s, and the Active Objects list. Several controllers under one hub are expected — HUD, table, pause menu — and each only re-applies its own rows.

### Add it

Add Component → `ResponsiveUiController`, on a sub-group under the Canvas (or on the Canvas GameObject itself). The Hub field auto-assigns. Add Component / Reset seeds **one** widget row with the allowed-orientation slots Enabled, except **Flip Cover Landscape starts Disabled**, and Wide Screen, which starts Enabled only on Wide Screen build targets. Prefab UI groups keep Hub empty and bind on enable.

### Inspector

| Section → field | Meaning |
|-----------------|---------|
| Hub | The `ResponsiveUI` this controller applies from. Auto-assigned; leave empty on prefabs |
| **Active Objects** | One row per GameObject plus the 20 slot toggles it is active in. See [Active Objects](#active-objects) |
| **Layout** → Backgrounds | Background rows: an `Image` plus its **Match Mode** |
| **Widgets** → Bulk Copy Widget Slots | Collapsed panel: copy one exact source slot onto one exact destination slot across **every** widget row |
| **Widgets** → Widget Items | The widget rows: a Target `RectTransform` plus its 20 `ResponsiveRectState` slots |

Background Match Mode values, in Inspector order:

| Match Mode | Behavior |
|------------|----------|
| **Cover** (default) | Keep the image aspect and scale to fill the parent (may crop) |
| **Fit** | Keep the aspect and scale to fit inside the parent (may letterbox) |
| **Stretch** | Ignore the aspect; stretch both axes to the parent |
| **Fit Width** | Keep the aspect; match the parent width (height may overflow) |
| **Fit Height** | Keep the aspect; match the parent height (width may overflow) |
| **Width Only** | Cover horizontally; stretch-height anchors fill Top/Bottom (`sizeDelta.y = 0`), otherwise keep `sizeDelta.y` |
| **Height Only** | Cover vertically; stretch-width anchors fill Left/Right (`sizeDelta.x = 0`), otherwise keep `sizeDelta.x` |
| **Manual** | Do not resize — keep the authored `sizeDelta` |

The art size a background covers to comes from Project Settings → Responsive UI → **Background Cover Size**, unless the hub has Override Background Size on. `ResponsiveCamera` does **not** use that size.

### API

```csharp
ResponsiveUI                  Hub { get; }
ResponsiveWidgetLayout[]      Widgets { get; }
ResponsiveBackground[]        Backgrounds { get; }
ResponsiveActiveObjectEntry[] ActiveObjects { get; }

void AssignHub(ResponsiveUI hub);
void ApplyFromHub(/* layout, layoutOnly */);   // IResponsiveUiClient — the hub calls this
bool SizeChangeRequiresReapply();
```

There is no bulk-copy API and no bulk-copy work in a player build: bulk copy is Editor-only serialized authoring.

### Notes

- A slot's **Enabled** chooses which layout applies. Turning it off falls back through the resolver — **Does not hide the widget Target**. Use Active Objects to show or hide HUD roots.
- **Uncheck Enabled on a Cover** / Half Opened / Pad / Flip slot to stop using it and reuse Phone (or Portrait) through the resolver.
- **Toggling Enabled on any slot in the live fallback chain previews the resolved layout** right away — the detected slot, its Portrait sibling, and the Phone slots behind them. Slots outside that chain only save.
- Enabling a Cover / Pad / Fold / Flip slot copies **Phone, same orientation**; iPhone Duo Seated copies the first enabled slot of its Pad-first chain; Wide Screen copies Pad once. Existing authored slots are never re-seeded — an empty Cover slot in a shipped scene stays off and falls back to Phone.
- List `+` rows start **empty** (Target None, default slots). Unity's MouseDown duplicate of the row above is cleared before and after the list draws, so a new row never inherits the previous one.
- Clearing a widget Target to **None** resets every slot on that row to new-row defaults, so the next assign is a first assign again.
- Play Mode widget Apply goes through `ResponsiveWidgetLayout.Apply(hub)` → `ResponsiveUI.ApplyWidget`, so the Design Scale ratio is computed once per Target without copying sibling widget structs.
- Apply sorts parents before children, so nested widgets in one list resolve their residual scale correctly.
- A Scale With Screen Size hub skips the Play Mode same-form size widget reapply; a Device Simulator canvas-ready pass still re-applies those widgets.
- A background Inspector edit (Match Mode or Image) re-covers immediately, including on **inactive** panels in Edit Mode.

## `ResponsiveCamera` — viewport, pose, projection, focus

### What it does

Authors camera viewport, local position / rotation, projection, and focus framing per form × orientation slot, on Editor **and** real devices. It can also fit one background `SpriteRenderer` behind the camera's view. Layout key, detection, and events come from the `ResponsiveUI` hub.

### Add it

Add Component → `ResponsiveCamera`, as a sibling of the Canvas hub (not on the camera). The Hub field auto-assigns. Add Component / Reset starts the script **disabled** and seeds **one** camera row with the allowed-orientation slots Enabled, except Flip Cover Landscape.

### Inspector

| Field | Meaning |
|-------|---------|
| Hub | The `ResponsiveUI` to apply from. Auto-assigned |
| **Cameras** → Ui Size Rect | Optional `RectTransform` whose rect drives Scale From UI Size |
| **Cameras** → Ui Size Canvas | Optional `Canvas` whose root rect drives it when Ui Size Rect is unset |
| **Cameras** → Bulk Copy Camera Slots | Collapsed panel: copy one exact slot to another across every camera row, Enabled included |
| **Cameras** → Camera Items | The camera rows |

Per camera row:

| Field | Meaning |
|-------|---------|
| Target | The `Camera` this row writes |
| Background | Optional `SpriteRenderer` fitted behind that camera's view |
| Sprite Fit Mode | **Stretch** / **Cover** / **Fit** for that background |

Per slot (each enabled slot groups into **View**, **Focus**, **Bounds Layout**, **Transform**, **Scale**):

| Field | Meaning |
|-------|---------|
| **Enabled** | Off: the fields hide and the resolver falls back. **Does not hide the camera GameObject** |
| **Viewport** | Normalized `Camera.rect` in `[0, 1]`. Authored `Camera.rect` always applies |
| **Apply Local** | One checkbox for both. On: this slot writes its `localPosition` and `localEulerAngles`. Off: the values stay stored on the slot and are not written, so Focus can own the live camera |
| **Local Position / Local Euler Angles** | Stored **per slot**, like a widget Pos. Editing them turns Apply Local on unless the slot is focusing |
| **Projection** | Orthographic or Perspective. Auto-detected from the Target on Capture or first assign; Apply writes `Camera.orthographic` |
| **Orthographic Size** / **Field Of View** | Optional projection writes for the active Projection type, each with its own apply toggle |
| **Scale From UI Size** | Scale the authored local position by live display size ÷ Design UI Size, using Scale Mode (Width / Height / Auto / UniformMin / UniformMax / IndependentXY). Rotation is never scaled. Turning it on turns on Apply Local |
| **Scale Projection** | Orthographic Size and Field Of View use Scale Mode for aspect framing: Width keeps the horizontal world span, Height the vertical, Auto fits like UniformMin. Perspective uses tan of half-FOV. Independent of Scale From UI Size |
| **Design UI Size** | Read-only, "Locked to Responsive UI settings": the form's Design Canvas from Project Settings, shown oriented to the slot. Not Background Cover Size, and no per-slot override |
| **Focus Target** | A world Transform to keep framed |
| **Focus Screen** | **None** off · **Auto** uses this slot's Focus Bounds when set, else the whole camera · **Bounds** uses Focus Bounds (Full if unset) · **Full** ignores Focus Bounds. Never writes `Camera.rect` |
| **Focus Bounds** | A HUD `RectTransform`, plus a per-slot **Bounds Layout** authored with the same fields as a widget slot |
| **Focus Fit** | **Center** slides the camera so the object sits in the pane · **Cover** fills the pane (may crop) · **Fit** shows the whole object (may letterbox) |
| **Focus Padding** | Extra world margin for Cover / Fit (`1` = bounds only) |
| **Capture From Camera** | Copy the live Target's viewport and projection into this slot, and its transform with Apply Local on — unless the slot is focusing |

Canvas Scaler fields are hidden here: they are UI-only on the shared base.

### API

```csharp
ResponsiveUI             Hub { get; }
ResponsiveCameraLayout[] Cameras { get; }

void AssignHub(ResponsiveUI hub);
void ApplyFromHub(/* layout, layoutOnly */);
bool SizeChangeRequiresReapply();
Vector2 GetUiLayoutSize();
static Vector2 ResolveUiLayoutSize(/* … */);
```

### Notes

- **Camera Background** is re-fitted after every slot Apply: scaled to the Target camera's visible world size (`ViewportToWorldPoint`, honoring `Camera.rect`, aspect, and tabletop orientation), recentered on that camera's view center, then moved behind visible content at the minimum sprite sorting order. Simple sprites use `localScale`; Sliced / Tiled use `SpriteRenderer.size`. The solve is absolute per unit axis, so re-Apply never accumulates scale. The background still fills the **full** camera, so an offset tabletop camera shows no clear-color gaps; Focus Cover / Fit then frames the object inside the pane.
- **Live size** for both scale checkboxes prefers simulated device pixels (`UnityEngine.Device.Screen` or the Simulator catalog W×H) when the UI source uses CanvasScaler Scale With Screen Size, or when no scaler is found. Constant Pixel Size uses the canvas rect. Fallback order: Ui Size Rect → Ui Size Canvas → parent Canvas → Screen.
- **Focus Bounds layout** is written with the **widget** canvas ratio (`ApplyWidget` / `GetLayoutRatio`), not camera Scale From UI Size — even before Focus Target / Screen are set. Hub and Simulator Apply write slot → HUD and never recapture Bounds Layout back into slots.
- Play Mode focus watch re-applies only on its own inputs: the Focus Bounds HUD rect, the camera viewport / render size, and the Focus Target bounds. Camera pose and Orthographic Size / FOV are **not** watched, so other camera scripts are not snapped back every frame.
- Toggling Scale From UI Size, Scale Projection, or Scale Mode recaptures the live camera into the new scale space, so enabling, disabling, or switching modes does not make the Target jump.
- Turning **Enabled** off previews the resolved fallback slot immediately. Clearing Target to **None** resets every slot on the row.
- A degenerate viewport, or no enabled slot at all, applies a full-screen viewport.
- Half-open forms on device need an active posture detector for the form mapping — see [PLATFORMS.md](PLATFORMS.md) and [../FoldAndFlip/README.md](../FoldAndFlip/README.md).

## `ResponsiveScrollView` — FancyScrollView metrics

### What it does

Authors FancyScrollView **Cell Interval** and **Scroll Offset** per form × orientation slot and writes them to a sibling scroll view. **No host code is required.** With no hub assigned, it detects layout on its own through `ResponsiveScrollDriver`.

### Add it

Add Component → `ResponsiveScrollView`, on the **same GameObject** as the FancyScrollView host. It starts **enabled** and copies the sibling's current Cell Interval / Scroll Offset into every slot; Player-allowed orientations start Enabled except Flip Cover Landscape. There is **no item list** — one scroll layout per component, so `+` does not add a second one.

### Inspector

| Field | Meaning |
|-------|---------|
| Hub | The `ResponsiveUI` to apply from. Auto-assigned; empty means standalone detection |
| **Scroll Layout** → Bulk Copy Scroll Slots | Collapsed panel: copy Enabled, Cell Interval, and Scroll Offset from one exact slot to another |
| **Scroll Layout** → Scroll Slots | The 20 slots |

Per slot:

| Field | Meaning |
|-------|---------|
| **Enabled** | Off: the metrics hide and the resolver falls back. No enabled slot anywhere leaves the host fields unchanged |
| **Cell Interval** | Written to the host's `cellInterval`, range `0.01`–`1`, as authored (no Design Scale ratio). Unset (`0`) applies as `0.2` |
| **Scroll Offset** | Written to the host's `scrollOffset`, range `0`–`1`, as authored. Reset default `0.5` |

The Inspector warns when the component is disabled, when the hub is missing or disabled, or when no FancyScrollView sits on the same GameObject.

### API

```csharp
ResponsiveUI            Hub { get; }
ResponsiveScrollLayout  ScrollMetrics { get; }

void AssignHub(ResponsiveUI hub);
void Bind(Action apply);                                     // optional; Bind(null) restores the automatic write
bool Apply(ref float cellInterval, ref float scrollOffset);
bool ApplyChanged(ref float cellInterval, ref float scrollOffset);
void SeedAll(float cellInterval, float scrollOffset, bool portrait, bool landscape);
bool SeedIfEmpty(float cellInterval, float scrollOffset, bool portrait, bool landscape);
void TakeLayout(ResponsiveScrollLayout layout, ResponsiveUI hub);
void ApplyFromHub(/* layout, layoutOnly */);
```

Two ways metrics reach the host:

| Host | How |
|------|-----|
| **No `Bind`** (default — any FancyScrollView subclass) | Play Mode and device: the component finds the sibling that serializes `cellInterval` / `scrollOffset`, writes the resolved slot, and Relayouts it. Edit Mode: `ResponsiveScrollSync` writes the host SerializedObject |
| **`Bind(callback)`** (optional) | The callback replaces the automatic write — typically `ApplyChanged(ref cellInterval, ref scrollOffset)` then `Relayout()` once initialized |

### Notes

- The host is any sibling `MonoBehaviour` whose type (or a base type) declares float `cellInterval` and `scrollOffset` — FancyScrollView's own serialized fields, so every subclass qualifies.
- Relayout waits until the host is initialized (its first `UpdateContents`), so an empty pool is never built early. The metrics are still written before that, so the first layout already uses the right slot. If a stripped player build removed `Relayout()`, `UpdatePosition(currentPosition)` is used instead.
- Reflection runs once per host **type**, Relayout goes through a cached delegate, the sibling search runs once per enable and allocates nothing, and nothing runs per frame. With a hub assigned there is no per-frame detect at all.
- A throwing host callback or Relayout is logged against the component and does **not** abort the hub pass, so later widgets, cameras, and banners still apply.
- The layout the hub passes is adopted on **every** pass: the same form × orientation keeps the slot metrics and only Relayouts for the settled viewport, a different layout runs the full slot write. So the component can never keep resolving from a stale device state.
- Toggling **Enabled** in either direction re-resolves the Apply slot and pushes it to the host, so turning the active slot off shows the fallback metrics immediately.
- Do **not** add a `LateUpdate` on the host — it would hide FancyScrollView's own editor `LateUpdate`.

## `ResponsiveTwoSideUiTweenController` — either-side UI with a tween

### What it does

For controls that can live on the left or the right of the canvas. Each widget stores the same twenty slots as a controller widget, on **four** layouts: Left / Right × Normal / Animation Start. Position changes tween with a per-widget duration, delay, and curve.

### Add it

Add Component → `ResponsiveTwoSideUiTweenController`, under the Canvas. Add Component / Reset and list `+` seed all four layouts with the same Enabled defaults as controller widgets; a new row starts empty (no name, no Targets).

### Inspector

| Section → field | Meaning |
|-----------------|---------|
| Hub | The `ResponsiveUI` to apply from. Auto-assigned |
| **Layout Authoring** → Side | The side being authored and previewed (`Left` / `Right`). Default `Right` |
| **Layout Authoring** → Preview | Show the Normal or the Animation Start layout in the Scene |
| **Layout Authoring** → Use Unscaled Time | Tween on unscaled time (default on), so it still runs while `Time.timeScale` is 0 |
| **Widgets** → Bulk Copy Widget Slots | Copy one exact slot to another across every row, on the **currently shown** Side + Preview layout only |
| **Widgets** → Bulk Copy Side | Copy every slot on Normal and Animation Start from Left → Right or Right → Left, mirroring X (Anchor Min/Max through `1 − x`, Pos X negated, Pivot X → `1 − x`). The Inspector Side then switches to the destination so the preview shows the mirrored result |
| **Widgets** → Bulk Copy Widget | Copy or **swap** every slot on Normal and Animation Start from one widget row onto another, on a chosen Left or Right side. Targets and the other side stay unchanged |
| **Widgets** → Widget Items | Each row groups **Widget** (Name / Target) and **Tween** |
| **Preview** | Play / Pause / Stop / Refresh / Hide for an Edit Mode preview |

Per widget row:

| Field | Meaning |
|-------|---------|
| Name | A label for the row |
| Animation Enabled | Off: the Animation Start layouts are unused and the widget snaps |
| Duration / Delay | Seconds for the position transition (both `≥ 0`) |
| Curve | `AnimationCurve` for the transition. A missing or key-less curve evaluates as linear |
| Left Normal / Left Animation Start / Right Normal / Right Animation Start | Four full 20-slot layouts, each with its own Target |

### API

```csharp
ResponsiveUiSide            Side { get; }
bool                        IsVisible { get; }
ResponsiveTwoSideWidget[]   Widgets { get; }
ResponsiveUI                Hub { get; }

void SetSide(ResponsiveUiSide value);   void SetSide(int value);
void SetSideImmediate(ResponsiveUiSide value);
void SetVisible(bool value);            void SetVisibleImmediate(bool value);
void SetVisible(Transform target, bool value);
void SetVisibleImmediate(Transform target, bool value);
void SetVisible(int widgetIndex, bool value);
void SetVisibleImmediate(int widgetIndex, bool value);
void Show();  void Hide();  void Trigger(bool value);
bool IsWidgetVisible(Transform target);   bool IsWidgetVisible(int index);
bool FindWidgetIndex(Transform target, out int index);
void Preview();
```

`SetVisible` / `Hide` / `Show` / `Trigger` apply to **every** widget. The `Transform` and `int` overloads target one widget — the one whose Target is that transform or an ancestor — so a single widget can hide without moving the others. **`SetSide` still moves every widget together.**

### Notes

- Disabled slots still use the layout fallback chain; **visibility stays on** `SetVisible`, never on slot Enabled.
- A layout-only hub pass does not stop a running tween and skips widgets whose tween is active, so an in-flight animation is not snapped while idle siblings re-layout. A widget skipped that way is flagged, and when its tween finishes the current device layout is applied once — its End was solved for the geometry from before the pass, so without this it would rest at a stale position until the next full Apply.
- Apply reads the active layout **by ref**, so hiding one widget does not copy another widget's four layouts.
- Play Mode `SetVisible` on one widget applies that Target through `ApplyWidget` (ratio computed once) and does not re-layout siblings.
- Animating a widget while the controller is inactive or disabled snaps it to the end layout, with no tween.
- The Bulk Copy Side / Widget confirmations use a session opt-out separate from the widget-slot copy.

## `ResponsiveTextBanner` — auto-width single-line TMP banner

### What it does

Fits a single-line TMP banner's **width** to its rendered text while keeping the slot height, then anchors it at a chosen alignment point so that point never moves between messages. It does **not** own slots: author the banner `RectTransform` as a `ResponsiveUiController` widget Target and set Alignment here.

### Add it

Add Component → `ResponsiveTextBanner`, on the Canvas or a child (often next to a gameplay adapter). Assign **Banner** and **Text**; Reset fills them from this GameObject and its children. Leave Hub empty on prefabs.

### Inspector

| Section → field | Meaning |
|-----------------|---------|
| **Targets** → Banner | The `RectTransform` to size and align |
| **Targets** → Text | The `TextMeshProUGUI` to measure and display |
| **Sizing** → Text Padding | Added to TMP `preferredWidth`. The TMP rect uses text padding only. Default `20` |
| **Sizing** → Box Padding | Also added to the banner width. Banner width = text + both paddings. Default `100` |
| **Sizing** → Alignment | A 3×3 grid, horizontal then vertical, with **Middle** (not Center) on the middle row: Left Up, Center Up, Right Up · Left Middle, Center Middle, Right Middle · Left Down, Center Down, Right Down. Writes the banner `RectTransform.pivot` |
| **Display** → Display Duration | Play Mode auto-hide seconds on unscaled time, so it still hides while `Time.timeScale` is 0. `0` stays until `Hide`. Default `2.5` |
| **Display** → Clear Text On Hide | Clear the TMP text when hidden. Default on |
| **Preview** | Editor preview of a message |
| Hub | The `ResponsiveUI` to apply from. Auto-assigned |

There are no slot foldouts and no bulk copy, and Scene edits are never written back into slots.

### API

```csharp
bool   IsVisible { get; }
bool   IsPreviewLocked { get; }
string VisibleText { get; }
Align  Alignment { get; }

void Show(string value, bool autoHide = true);
void Hide();
void RefreshLayout();
void CaptureSlot();
void SetAlignment(Align value);
void SetTargets(/* banner, text, capture */);
void SetPreviewLock(bool locked);

static float   CalculateWidth(/* … */);
static Vector2 AlignmentAnchor(Align alignment);
static Vector2 CalculateAlignedPosition(/* … */);
static Vector2 ConvertAnchoredPositionForPivot(/* … */);
```

| Call | Behavior |
|------|----------|
| `Show(string, autoHide)` | The first show recaptures the widget slot (unless a hub recapture is pending), writes the Alignment pivot, measures TMP, and sets the fitted width. A follow-up `Show` while already visible changes width only — `anchoredPosition` is not rewritten. Re-showing the **same** message while it is still fitted only extends the hide timer |
| `Hide()` | Undoes the banner's own writes (Alignment pivot and position, fitted width, TMP rect width) while the rect still holds them, then hides. If the owning controller rewrote the rect since, that layout is **kept** and becomes the slot. Safe on an already-hidden banner. A child Target is deactivated; if this component sits on the banner GameObject, Graphic/TMP are disabled instead so the hub client stays enabled |
| `RefreshLayout()` | Captures a pending hub slot if there is one, then writes the Alignment pivot and re-fits while visible |
| `CaptureSlot()` | Samples the current rect as the slot original. No-op while a hub recapture is pending |
| `SetPreviewLock(bool)` | Blocks timed `Show(..., autoHide: true)` while an Editor preview owns the banner. Unlocking calls `Hide()` |

### Notes

- Alignment enum integer order matches Unity's `TextAnchor` 0–8, so a scene value of `3` is Left Middle.
- `[DefaultExecutionOrder(1000)]`, so the Play Mode LateUpdate recapture runs after default-order widget Apply.
- **Hub recapture:** `ApplyFromHub` sets a pending flag rather than sampling at once, because the hub can notify the banner before the controller writes the widget. Play Mode LateUpdate then captures the hub-written slot (widget pivot included) and re-applies the Alignment pivot and fitted width if visible. Edit Mode uses `EditorApplication.delayCall`. A same-frame `Show` or `CaptureSlot` waits for that recapture, so a fitted width is never stored as the slot.
- **Measure:** wrapping is forced to `NoWrap` and overflow to `Overflow` for `preferredWidth` (fallback `GetPreferredValues(..., 99999, 99999)`), then the previous wrap and overflow are restored.
- `SizeChangeRequiresReapply` is **true only while visible**, so a hidden message skips Play Mode same-form size recapture and the TMP rebuild. A Simulator canvas-ready pass still notifies the banner.
- **Ownership:** the banner records exactly what it last wrote. `Hide`, disable, and the first `Show` undo those writes only while the rect still holds them. Anything written since — a new device slot from the owning controller, a same-form Design Scale tick a hidden banner was not notified of, or Inspector LivePush — is kept and adopted as the slot instead of being snapped back.
- Hide the banner, or save with its GameObject inactive, so the serialized size stays the slot original.

## Active Objects

### What it does

One list on `ResponsiveUiController`. Each row pairs a GameObject with the twenty form × orientation slots it should be active in. Apply `SetActive`s listed objects for the **exact** live slot.

### Add it

It is a section of the `ResponsiveUiController` Inspector — a tinted **Foldout** (not a Foldout Header, so Unity 6 can draw the list `+`). The section starts expanded and the list open. New rows start empty (Target None) with the **Player-allowed** slots on.

### Inspector

| Row field | Meaning |
|-----------|---------|
| Target | The GameObject to show or hide |
| Slot toggles | One Portrait / Landscape pair per form. Checked = show, unchecked = hide |

**Show Other Slots** on the hub lists every allowed form's P/L toggles; off shows only the detected form. Disallowed Portrait or Landscape axes are hidden.

### API

```csharp
ResponsiveActiveObjectEntry[] ActiveObjects { get; }        // on ResponsiveUiController

struct ResponsiveActiveObjectEntry { GameObject target; ResponsiveActiveObjectSlots slots; }
struct ResponsiveActiveObjectSlots {
    bool IsActive(in ResponsiveDevice.LayoutKey layout);
    bool Get(int slotIndex);  void Set(int slotIndex, bool value);
    static ResponsiveActiveObjectSlots FormTransitionDefaults();   // all on except Flip Cover
}
static class ResponsiveActiveObjects { static void Apply(/* entries, layout */); }
```

### Notes

| Slot state | Object |
|------------|--------|
| Checked for the live layout | Active |
| Unchecked for the live layout | Inactive |
| Not in the list | Unchanged |

- There is **no Phone fallback**: unchecked means hide, not reuse Phone. iPhone Duo Seated toggles are plain slots and do not mirror Pad.
- Duplicate rows for one GameObject: the last entry wins.
- Do **not** list a widget Target in Active Objects: widget slots own the RectTransform layout, and a listed Target would fight that GameObject's authored active state.
- Use it for alternate chrome and HUD roots — cover-only controls, landscape-only bars, half-open tabletop UI.

## Layout Events

### What it does

One `UnityEvent` per exact form × orientation slot, on the `ResponsiveUI` hub. Play Mode Apply fires the event for the live slot only.

### Add it

The **Events** section of the hub Inspector (collapsed by default). The event list follows **Show Other Slots** and Player Allowed Orientations like widget foldouts.

### Inspector

| Field | Meaning |
|-------|---------|
| One `UnityEvent` per slot | Invoked when the applied layout becomes that slot |

### API

```csharp
struct ResponsiveLayoutEvents {
    void       Invoke(in ResponsiveDevice.LayoutKey layout);
    UnityEvent Get(int slotIndex);
    // plus one public UnityEvent field per slot: phonePortrait, phoneLandscape, padPortrait, …
}
```

### Notes

- **Play Mode only.** Edit Mode `ExecuteAlways` Apply never invokes them.
- **No fallback**: Cover Landscape fires Cover Landscape only.
- A slot event fires once the hub's `Start` has run (and again after a re-enable), then only when the applied layout **changes** — not on every re-Apply of the same layout from a scene load, resume, or posture callback.

## Data types

These are the serialized structs behind the components. [CONCEPTS.md](CONCEPTS.md) explains the slot model and Design Scale that give them meaning.

| Type | Role |
|------|------|
| `ResponsiveWidgetLayout` | One widget row: a Target `RectTransform` plus twenty `ResponsiveRectState` slots. Also holds the shared `FormFactors` order and slot-name helpers |
| `ResponsiveRectState` | One widget slot: Enabled, Scale From Design Canvas, Scale Mode, Design Canvas Size, Default Scale, anchors, pivot, `anchoredPosition`, `sizeDelta`. Fixed axes show Pos X/Y + Width/Height; stretched axes show Left/Right or Top/Bottom while storing Unity's canonical values |
| `ResponsiveCameraLayout` | One camera row: Target, Background, Sprite Fit Mode, twenty `ResponsiveCameraState` slots |
| `ResponsiveCameraState` | One camera slot: the View / Focus / Bounds Layout / Transform / Scale fields listed above |
| `ResponsiveScrollLayout` / `ResponsiveScrollState` | One scroll layout and one scroll slot (Enabled, Cell Interval, Scroll Offset) |
| `ResponsiveTwoSideWidget` | One two-side row: name, tween settings, and the four `ResponsiveWidgetLayout`s |
| `ResponsiveBackground` | One background row: `Image` + Match Mode |
| `ResponsiveActiveObjectSlots` / `ResponsiveActiveObjectEntry` | The twenty slot toggles and one Active Objects row |
| `ResponsiveLayoutEvents` | The twenty per-slot UnityEvents |
| `ResponsiveFormTransitionLoading` | The hub's Play Mode form-switch cover |
| `IResponsiveUiClient` / `ResponsiveUiHub` | The client contract and the hub-resolution helper every client uses |

## Related

- [CONCEPTS.md](CONCEPTS.md) — form factors, slots, the fallback chain, design canvas and Design Scale
- [GETTING-STARTED.md](GETTING-STARTED.md) — install and first responsive widget
- [SETTINGS.md](SETTINGS.md) — Project Settings → Responsive UI and the device pose pages
- [PLATFORMS.md](PLATFORMS.md) — what runs where
- [../FoldAndFlip/README.md](../FoldAndFlip/README.md) — Android fold and flip posture detection
- [../IphoneDuo/README.md](../IphoneDuo/README.md) — iPhone Duo postures, rules, and runtime API
