# Device testing

Real-device verification: what must be checked on hardware before a release, a row template to
record it in, and the terminal recipes for driving an iPhone Duo in the Xcode simulator.

The automated checks ([./TESTING.md](./TESTING.md)) are static and in-process. They do not prove
Android callbacks, manifest merging, R8 behavior or the iOS hinge API. This page is the evidence
that does. Copy it into **your application's** release evidence and fill it in: the package ships it
as an empty template and certifies no fold/flip hardware. Record observed behavior — never turn an
untested cell into a compatibility claim.

Simulator and emulator results may supplement this matrix but do not replace physical-device rows.

## Build identity

Record once per matrix run:

| Field | Value |
|-------|-------|
| Package version / commit | |
| Unity version | |
| Android Gradle Plugin / Gradle | |
| AndroidX Window resolved version | |
| Build type | development / release / minified release |
| APK / AAB / IPA checksum | |
| Tester and date | |

## Minimum release coverage

| # | Device | What to cover |
|---|--------|---------------|
| 1 | A supported Fold device | cover cold start, inner cold start, flat, half-open, rotate, resume |
| 2 | A supported Fold device | **Expand → Normal** (folded closed onto the cover) and **Flex → Normal** — confirm the form leaves Pad / HalfOpened and lands on Phone or Fold Cover, not stuck on Pad / HalfOpened |
| 3 | A supported Fold device | rear-display / cover transfer ACTIVE while the inner screen stays open, if the OEM exposes it — confirm Closed/Normal, including while Jetpack may still report `FLAT` |
| 4 | A supported Flip device | cover cold start, main cold start, flat, half-open, rotate, resume |
| 5 | An ordinary Android phone with `DeviceKind.Unknown` | verify it is **not** forced Closed |
| 6 | Any supported Android version | split-screen or freeform multi-window |
| 7 | Any supported Android version | picture-in-picture, if your application enables it |
| 8 | A **minified release** build | not only a Unity development build |
| 9 | An iPhone Duo | the iOS rows below |
| 10 | An ordinary iPhone and an iPad | the detector stays a no-op (Phone / Pad) |

## Per-device row template

Duplicate for every tested device / OS / build combination.

| Field | Expected | Observed | Evidence |
|-------|----------|----------|----------|
| Manufacturer / model | | | |
| OS version / build | | | |
| WindowManager extension version (if known) | | | |
| App orientation policy | | | |
| `DeviceKind` | | | |
| Cold start surface (cover / inner / main / single pane) | | | |
| Closed / Normal | | | |
| Opened / Expand (`FLAT`) | | | |
| Half Opened (`HALF_OPENED`) — include the hinge axis | | | |
| Rotate while open and while half-open | | | |
| Resize / split-screen enter, resize, exit | | | |
| Picture-in-picture enter and exit | | | |
| Background, resume, repeated fold/unfold | | | |
| Camera rect, HUD canvases, background art, blur layers | | | |
| Release build with shrinking — callback received / layout applied | | | |
| Exceptions, ANRs or stale layout | | | |
| **Result** | pass / fail / blocked | | |

## iPhone Duo hardware rows

Run a **Development Build**. The console then prints one `FoldAndFlip: posture …` line per posture
change and one `[ResponsiveUi] <canvas>: layout <form>/<orientation>` line per hub layout change. A
`UIHingeInteraction is unavailable` warning on a Duo is a failure.

The `UIHingeInteraction` bridge is not verified until these rows pass on hardware, from an Xcode
build on the iPhone Duo iOS SDK:

| Row | Expected |
|-----|----------|
| Cold start on the cover; cold start on the inner screen | iPhone Duo Cover; Pad |
| Unfold / fold during active play, without backgrounding | iPhone Duo Cover ↔ Pad with no frozen frame |
| Flat (Opened) in a landscape-only build, turned tall | full screen, sideways — no letterbox, no resize. Turned to the opposite landscape it flips 180° upright (`IphoneDuoFlatOrientation`) |
| Seated laptop hold, flattened while still held tall | Pad Landscape, full screen, sideways (no letterbox). Tap buttons to confirm touches land; turned wide → Pad Landscape, upright. Same for a cold start flat while held tall, and for unfolding from the cover while held tall |
| Propped on the outer display (Standing) | iPhone Duo Cover, **not** Seated |
| Background / resume in each state; app switcher while partly open | state restored, no stale layout |

### Seated allows only the hold's axis

While partly open on the inner screen, an Auto Rotation build allows only the hold's orientation
(`IphoneDuoHoldOrientation`: laptop → Portrait, book → Landscape, from `Input.deviceOrientation`,
which iOS reports as landscape in the tall laptop hold — confirm this mapping on hardware). Closed
and Opened keep the lock, and a fixed Default Orientation keeps the lock while Seated.

With **Seated rotation = Locked** (the default), turning the device while Seated must not switch the
pose. Under Auto: a landscape-only build → Seated Portrait, a portrait-only build → Seated
Landscape, both axes → the entry hold, and the flipped side of the same hold is followed. The
hold-driven results below apply to **Follows hold**.

| Build orientation policy | Hold | Expected |
|--------------------------|------|----------|
| Landscape-only (Auto Rotation, landscape axes only, *Requires Fullscreen*) | laptop | rotates to the tall inner screen, no letterbox; horizontal crease → iPhone Duo Seated **Portrait** (top pane gameplay, bottom pane controls) |
| Landscape-only | book | wide inner screen, vertical crease → Seated **Landscape** |
| Landscape-only | flatten or close | back to landscape (Pad Landscape / iPhone Duo Cover) within ~0.3 s |
| Portrait-only | book | landscape only, wide → Seated Landscape |
| Portrait-only | laptop | Seated Portrait |
| Portrait-only | flatten | Pad Portrait |
| Both axes | laptop | Seated Portrait (tall) |
| Both axes | book | Seated Landscape (wide) |

For a laptop hold and a book hold, record: the screen size Unity reports (Development Build
`FoldAndFlip: posture … screen WxH`), the Seated slot applied (`[ResponsiveUi] … layout …`), that
the view rotates back after leaving Seated, and that no letterbox remains.

Do not add unreleased or rumored devices as "tested fold" rows — see
[./PLATFORMS.md](./PLATFORMS.md).

## iPhone Duo simulator recipes

Terminal commands for exercising the `IphoneDuo/` module in the Xcode iPhone Duo simulator. Posture
behavior and the rules themselves: [../IphoneDuo/README.md](../IphoneDuo/README.md) and
[./PLATFORMS.md](./PLATFORMS.md#iphone-duo-seated-rules).

Set your own values once per shell. Every recipe below uses these variables:

```bash
UDID=<UDID>                                       # or: xcrun simctl list devices | grep "iPhone Duo ("
BUNDLE=<your-bundle-id>
EXPORT=<path-to-your-xcode-export>                # the Xcode project Unity exported
DD=/tmp/duo-dd                                    # Xcode derived data
APP="$DD/Build/Products/Debug-iphonesimulator/<your-app-name>.app"
```

If the app name contains a space, keep it quoted everywhere and never match the process with
`\S+`-style patterns.

### What the terminal cannot do

- **Posture and rotation** (Closed / Seated / Flat, tall / wide, either side) are set by hand in the
  simulator window. `simctl` has no hinge or rotation command, and the simulator device window is
  not scriptable. Everything else below runs from the terminal.
- **`simctl io … screenshot` is interface-relative.** It always looks upright, so it cannot show an
  app that is upside down or sideways for the user. Use the window capture under
  [Screenshots](#screenshots).

### Find and boot the simulator

```bash
xcrun simctl list devices | grep "iPhone Duo"     # UDID and state
xcrun simctl boot "$UDID"                          # if Shutdown
xcrun simctl io "$UDID" enumerate                  # display ports: inner and cover
```

The simulator exposes two displays — the inner screen and the smaller cover screen. Note both pixel
sizes from `enumerate`; they are what Unity reports as `Screen.width`/`Screen.height` per posture.

### Build and install

1. **Unity export.** Build an iOS **Simulator SDK** Xcode export from Unity as a **Development
   build** (Player Settings → iOS → Target SDK = Simulator SDK, Development Build on). Quit Xcode
   first: Unity aborts an iOS build while Xcode is open. Point `$EXPORT` at the result.

2. **Xcode build** for the simulator — no Xcode UI needed:

   ```bash
   cd "$EXPORT"
   xcodebuild -workspace Unity-iPhone.xcworkspace -scheme Unity-iPhone -configuration Debug \
     -destination "platform=iOS Simulator,id=$UDID" -derivedDataPath "$DD" build > /tmp/duo-xcb.log 2>&1
   grep -E "BUILD (SUCCEEDED|FAILED)" /tmp/duo-xcb.log
   grep -E "IphoneDuoHinge\.mm:[0-9]+:[0-9]+: (error|warning)|Undefined symbol" /tmp/duo-xcb.log
   ```

3. **Install:**

   ```bash
   xcrun simctl terminate "$UDID" "$BUNDLE" 2>/dev/null
   xcrun simctl install "$UDID" "$APP"
   ```

### Native-only changes (fast loop)

A change to `IphoneDuo/Runtime/Plugins/iOS/IphoneDuoHinge.mm` alone needs no new Unity export: copy
the file into the export and rebuild with Xcode (incremental, about a minute). C# changes, including
a renamed `DllImport`, need a new Unity export.

```bash
cp Assets/ResponsiveUI/IphoneDuo/Runtime/Plugins/iOS/IphoneDuoHinge.mm \
   "$EXPORT/Libraries/ResponsiveUI/IphoneDuo/Runtime/Plugins/iOS/IphoneDuoHinge.mm"
# then step 2 (xcodebuild) and step 3 (install)
```

### Info.plist keys

`IphoneDuoBuildPlist` writes the launch keys. Read or change them in the export without rebuilding
in Unity:

```bash
P="$EXPORT/Info.plist"
/usr/libexec/PlistBuddy -c "Print :UISupportedInterfaceOrientations~iphone" "$P"
for k in FDPIphoneDuoLaunchHold FDPIphoneDuoFlatRule FDPIphoneDuoSeatedRule \
         FDPIphoneDuoSeatedRotation FDPIphoneDuoSeatedLockedPose \
         FDPIphoneDuoFlatRotation FDPIphoneDuoFlatLockedPose \
         FDPIphoneDuoSidewaysQuarterTurns FDPIphoneDuoModels FDPIphoneDuoTraceLog; do
  echo "$k: $(/usr/libexec/PlistBuddy -c "Print :$k" "$P" 2>&1)"; done
/usr/libexec/PlistBuddy -c "Add :FDPIphoneDuoTraceLog bool true" "$P"      # or Set … if it exists
/usr/libexec/PlistBuddy -c "Print :FDPIphoneDuoTraceLog" "$APP/Info.plist" # what was installed
```

| Key | Values |
|-----|--------|
| `FDPIphoneDuoFlatRule`, `FDPIphoneDuoSeatedRule`, Cover rule | 0 Sideways, 1 Rotate, 2 System Default (`IphoneDuoAcrossHold`) |
| `FDPIphoneDuoSeatedRotation` | 0 Locked, 1 Follows hold |
| `FDPIphoneDuoSeatedLockedPose` | 0 Auto, 1 Seated Portrait, 2 Seated Landscape |
| `FDPIphoneDuoFlatRotation` | 0 Follow device, 1 Locked |
| `FDPIphoneDuoFlatLockedPose` | 0 Auto, 1 Wide, 2 Tall |

A missing key reads as 0.

### Launch and logs

Unity's `Debug.Log` goes to the app's stdout, not the unified log. `--console-pty` captures stdout
and `NSLog` together:

```bash
xcrun simctl launch --console-pty "$UDID" "$BUNDLE" > /tmp/duo.log 2>&1 &
sleep 15; pkill -f "simctl launch --console-pty"
grep -E "iPhone Duo|FoldAndFlip: posture|\[ResponsiveUi\].*layout" /tmp/duo.log
```

Trace lines appear in development builds with **Diagnostics › Trace log** on in Project Settings ›
Responsive UI › iPhone Duo:

| Line | Source | Shows |
|------|--------|-------|
| `iPhone Duo: launch keeps interface …` | native, before C# | launched in an orientation the Player lock rules out; its axis stays allowed |
| `iPhone Duo: launch hold (mask …, device …, rule …)` | native | the launch hold started |
| `iPhone Duo: launch hold device … interface … status … rule … turns … window … scene … unity …` | native | each change while the splash runs; `window` must equal `scene` |
| `FoldAndFlip: posture … screen WxH` | C# | posture and Unity screen size |
| `[ResponsiveUi] UI: layout <form>/<orientation> (screen WxH)` | C# | the slot applied |
| `iPhone Duo flat: opened … device … interface … locked …` | C# | flat lock |
| `iPhone Duo hold: turn … mode … device …` | C# | the view turn starts or stops |

Numbers in native lines are UIKit values. Device orientation: 1 Portrait, 2 Upside Down, 3 Landscape
Left, 4 Landscape Right. Interface orientation: 1 Portrait, 2 Upside Down, 3 Landscape Right,
4 Landscape Left. Status (hinge): 2 partly open (Seated).

`NSLog` lines only, without relaunching — for example after starting the app by hand:

```bash
xcrun simctl spawn "$UDID" log show --last 2m --predicate 'eventMessage CONTAINS "iPhone Duo"'
```

### Screenshots

Per display, interface-relative (always upright):

```bash
xcrun simctl io "$UDID" enumerate                                   # port UUIDs
xcrun simctl io "$UDID" screenshot --display=<port UUID> /tmp/duo.png
```

**True view** — what the user actually sees, including an app that is upside down or sideways —
means capturing the simulator window. Save as `duo-windows.swift`:

```swift
import CoreGraphics
let windows = CGWindowListCopyWindowInfo([.optionAll], kCGNullWindowID) as! [[String: Any]]
for w in windows {
    let owner = w[kCGWindowOwnerName as String] as? String ?? ""
    let name = w[kCGWindowName as String] as? String ?? ""
    if owner.contains("Simulator") || owner.contains("Device") {
        print(w[kCGWindowNumber as String]!, owner, name)
    }
}
```

```bash
WID=$(swift duo-windows.swift | grep "iPhone Duo" | head -1 | cut -d' ' -f1)
screencapture -x -o -l "$WID" /tmp/duo-view.png
```

### Launch matrix

This relaunches the app whenever the posture or hold changes, captures the first 15 s from the
simulator window, and writes a contact sheet per launch. Start it, then move the simulator through
each state in the table below and hold each one for about 5 s. It needs a development build with the
trace log on, because it reads the posture from the log.

Save as `duo-montage.swift` (contact sheet:
`swift duo-montage.swift out.png cols cellWidth title files…`):

```swift
import AppKit
let a = CommandLine.arguments
let out = a[1], cols = Int(a[2])!, cellW = CGFloat(Double(a[3])!), title = a[4]
let images = a.dropFirst(5).compactMap { NSImage(contentsOfFile: $0) }
guard let first = images.first else { exit(1) }
let cellH = cellW * first.size.height / first.size.width, titleH: CGFloat = 28, labelH: CGFloat = 16
let rows = (images.count + cols - 1) / cols
let W = CGFloat(cols) * cellW, H = titleH + CGFloat(rows) * (cellH + labelH)
let rep = NSBitmapImageRep(bitmapDataPlanes: nil, pixelsWide: Int(W), pixelsHigh: Int(H), bitsPerSample: 8,
    samplesPerPixel: 4, hasAlpha: true, isPlanar: false, colorSpaceName: .deviceRGB, bytesPerRow: 0, bitsPerPixel: 0)!
NSGraphicsContext.saveGraphicsState()
NSGraphicsContext.current = NSGraphicsContext(bitmapImageRep: rep)
NSColor(white: 0.12, alpha: 1).setFill(); NSRect(x: 0, y: 0, width: W, height: H).fill()
let text: [NSAttributedString.Key: Any] = [.foregroundColor: NSColor.white, .font: NSFont.systemFont(ofSize: 14)]
(title as NSString).draw(at: NSPoint(x: 6, y: H - titleH + 6), withAttributes: text)
for (i, image) in images.enumerated() {
    let x = CGFloat(i % cols) * cellW, y = H - titleH - CGFloat(i / cols + 1) * (cellH + labelH)
    image.draw(in: NSRect(x: x, y: y + labelH, width: cellW - 2, height: cellH - 2))
    ("\(i + 1)" as NSString).draw(at: NSPoint(x: x + 4, y: y), withAttributes: text)
}
NSGraphicsContext.restoreGraphicsState()
try! rep.representation(using: .png, properties: [:])!.write(to: URL(fileURLWithPath: out))
```

Save as `duo-launch-matrix.py` next to both Swift files:

```python
# python3 duo-launch-matrix.py <UDID> <your-bundle-id> <output dir>
import glob, os, re, subprocess, sys, time

UDID, BUNDLE, OUT = sys.argv[1], sys.argv[2], sys.argv[3]
HERE = os.path.dirname(os.path.abspath(__file__))
POSTURE = re.compile(r'FoldAndFlip: posture (\w+)')
FLAT = re.compile(r'iPhone Duo flat: opened \w+ device (\w+) interface (\w+)')

def quiet(cmd):
    subprocess.run(cmd, shell=True, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)

def window_id():
    out = subprocess.run(['swift', os.path.join(HERE, 'duo-windows.swift')], capture_output=True, text=True).stdout
    return next(line.split()[0] for line in out.splitlines() if 'iPhone Duo' in line)

def state(text):
    p, d = POSTURE.findall(text), FLAT.findall(text)
    return (p[-1] if p else '?', d[-1][0] if d else '?', d[-1][1] if d else '?')

os.makedirs(OUT, exist_ok=True)
wid = window_id()
n = len(glob.glob(os.path.join(OUT, 'run_*')))
print('ready, window', wid, flush=True)
while True:
    n += 1
    run = os.path.join(OUT, f'run_{n:02d}')
    os.makedirs(run)
    quiet('pkill -f "simctl launch --console-pty"')
    quiet(f'xcrun simctl terminate {UDID} {BUNDLE}')
    time.sleep(1)
    log_path = os.path.join(run, 'log.txt')
    subprocess.Popen(['xcrun', 'simctl', 'launch', '--console-pty', UDID, BUNDLE],
                     stdout=open(log_path, 'w'), stderr=subprocess.STDOUT)
    start, frames = time.time(), []
    for i in range(31):                       # 0.5 s apart, then one after 3 s more
        if i == 30:
            time.sleep(3)
        frame = os.path.join(run, f'f{i + 1:02d}.png')
        subprocess.run(['screencapture', '-x', '-o', '-l', wid, frame])
        frames.append(frame)
        time.sleep(max(0, start + 0.5 * (i + 1) - time.time()))
    s = state(open(log_path, errors='ignore').read())
    title = f'run {n}: posture {s[0]} device {s[1]} interface {s[2]}'
    subprocess.run(['swift', os.path.join(HERE, 'duo-montage.swift'), os.path.join(run, 'sheet.png'), '8', '170', title] + frames)
    print('CAPTURED', title, flush=True)
    # Relaunch once a different posture / hold has stayed for 4 s.
    changed_at, seen = None, None
    while True:
        time.sleep(0.5)
        p, device, _ = state(open(log_path, errors='ignore').read())
        if (p, device) != s[:2] and device not in ('FaceUp', 'FaceDown', 'Unknown', '?'):
            if seen != (p, device):
                seen, changed_at = (p, device), time.time()
            elif time.time() - changed_at >= 4:
                print('CHANGE', seen, flush=True)
                break
        else:
            seen = None
```

```bash
python3 duo-launch-matrix.py "$UDID" "$BUNDLE" /tmp/duo-matrix
open /tmp/duo-matrix/run_01/sheet.png        # one sheet per launch; log.txt beside it
```

States to cover, with the default rules (Cover System Default, Seated Rotate, Flat Sideways, Seated
rotation Locked, Locked pose Auto, Flat rotation Follow device) and a landscape-only app:

| Posture | Hold | Device orientation in the log | Expected |
|---------|------|-------------------------------|----------|
| Flat | wide, both sides | Portrait / PortraitUpsideDown | landscape, full screen, upright |
| Flat | tall, both sides | LandscapeLeft / LandscapeRight | splash and content sideways (turned 90° left), full screen; interface Portrait on both sides |
| Seated | laptop, both sides | LandscapeLeft / LandscapeRight | splash and content upright, tall full screen, `iPhoneDuoSeated/Portrait` at the inner screen's tall size |
| Seated | book, both sides | Portrait / PortraitUpsideDown | Locked: stays `iPhoneDuoSeated/Portrait` (turning to the book hold, or a cold start in it, never switches); Follows hold: landscape, full screen, `iPhoneDuoSeated/Landscape` |
| Flat, Flat rotation Locked | wide or tall, both sides | any | keeps the hold the Flat locked pose gives (Auto: a landscape-only app → wide). On a one-axis app whose locked pose is its own axis (Auto always) the real hold is kept and the Rotate rule acts like Sideways; the flipped side of the same hold stays upright. Not yet device-tested |
| Closed | tall / wide | any | like any iPhone: landscape, sideways when held tall |

On the Landscape Right side of a tall hold, iOS reports Portrait — it gives an iPhone app no Portrait
Upside Down window — and the view turn makes the content read upright: look for `turns 2` (Rotate)
or `turns -1` (Sideways) in the native trace and `hold: turn True` from C#.

### Debugger (optional)

Attach only once the app is running; attaching during launch can stall it (resume with
`kill -CONT <pid>`):

```bash
lldb -p "$(pgrep -f '<your-app-name>')"
```

## See also

| Doc | For |
|-----|-----|
| [./TESTING.md](./TESTING.md) | the automated suites and validators |
| [./PRODUCTION.md](./PRODUCTION.md) | the release checklist this feeds |
| [./PLATFORMS.md](./PLATFORMS.md) | which postures run on which platform |
| [../IphoneDuo/README.md](../IphoneDuo/README.md) | iPhone Duo postures, holds and rules |
| [../FoldAndFlip/README.md](../FoldAndFlip/README.md) | the Android fold/flip module |
