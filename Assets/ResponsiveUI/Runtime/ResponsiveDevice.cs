using System;
using UnityEngine;
using FDPlus.Foldable;

namespace FDPlus.ResponsiveUI
{
    /// <summary>
    /// Classifies the running layout as one of the <see cref="FormFactor"/> values (Phone / Pad,
    /// Android Fold / Flip forms, iPhone Duo Cover / Seated, Wide Screen) and the screen as Portrait
    /// or Landscape. Players never return another platform's form. In the Editor, the selected
    /// Device Simulator profile drives detection on any build target (Android Fold / Flip,
    /// iPhone, iPhone Duo, or a desktop window with no device selected).
    /// Without a Simulator profile, forms follow the build target (<see cref="ResponsivePlatforms"/>):
    /// Android Phone / Pad / Fold / Flip; iOS Phone / Pad / iPhone Duo Cover / Seated;
    /// Windows / macOS / Linux / WebGL Wide Screen only, sized from the window.
    /// Fold / Flip and iPhone Duo posture mapping runs in the Editor (Device Simulator), on Android
    /// players, and on supported iPhone Duo iOS players (model gate, see
    /// <see cref="FoldablePostureDetector.IsRuntimeSupported"/>); other mobile players stay Phone / Pad.
    /// When a <see cref="FoldablePostureDetector"/> is active
    /// (<see cref="FoldablePostureSource"/>), fold/flip product states map into these form factors
    /// (Fold Expand → Pad, Fold Normal → Phone then Fold8 Cover → <see cref="FormFactor.FoldPhoneCover"/>,
    /// Flip Closed → <see cref="FormFactor.FlipPhoneCover"/>,
    /// Fold8 HalfOpened → <see cref="FormFactor.FoldCoverHalfOpened"/>,
    /// iPhone Duo Closed → <see cref="FormFactor.iPhoneDuoCover"/>,
    /// iPhone Duo HalfOpened → <see cref="FormFactor.iPhoneDuoSeated"/>, Flip Opened → Phone, etc.).
    /// </summary>
    public static class ResponsiveDevice
    {
        #region Types

        /// <summary>Phone vs Pad hardware classification (player fallback without a fold posture).</summary>
        public enum DeviceType
        {
            Phone = 0,
            Pad,
        }

        /// <summary>
        /// Layout form factor used by widget / camera / scroll slots. The value is the slot order
        /// (<see cref="ResponsiveWidgetLayout.FormFactors"/>), grouped by platform: shared
        /// (<see cref="Phone"/>, <see cref="Pad"/>), Android (Fold, Flip), iOS (iPhone Duo), and
        /// <see cref="WideScreen"/> (Windows / macOS / Linux / WebGL).
        /// Fold Expand (flat) → <see cref="Pad"/>; tall phone / Fold7 Cover / Flip Opened →
        /// <see cref="Phone"/>; Fold8 Cover (passport ~10:16) → <see cref="FoldPhoneCover"/>;
        /// Flip Closed cover → <see cref="FlipPhoneCover"/>; Fold8-style HalfOpened →
        /// <see cref="FoldCoverHalfOpened"/>; iPhone Duo Closed → <see cref="iPhoneDuoCover"/>,
        /// Opened → <see cref="Pad"/>, Seated → <see cref="iPhoneDuoSeated"/>.
        /// </summary>
        public enum FormFactor
        {
            Phone = 0,
            Pad = 1,
            /// <summary>Galaxy Z Fold8 Cover (1248×1972) — Inspector: Fold Cover.</summary>
            FoldPhoneCover = 2,
            /// <summary>Fold7-style half-open — Inspector: Fold Half Opened.</summary>
            FoldHalfOpened = 3,
            /// <summary>Fold8-style half-open — Inspector: Fold Cover Half Opened.</summary>
            FoldCoverHalfOpened = 4,
            /// <summary>Galaxy Z Flip8 Cover (948×1048) — Inspector: Flip Cover.</summary>
            FlipPhoneCover = 5,
            /// <summary>Flip clamshell half-open — Inspector: Flip Half Opened.</summary>
            FlipHalfOpened = 6,
            /// <summary>iPhone Duo closed, outer display (1398×2034) — Inspector: iPhone Duo Cover.</summary>
            iPhoneDuoCover = 7,
            /// <summary>
            /// iPhone Duo half-open inner. Portrait is the tall laptop (horizontal fold, gameplay
            /// top / controls bottom); Landscape is the wide book (vertical fold). Inspector:
            /// iPhone Duo Seated.
            /// </summary>
            iPhoneDuoSeated = 8,
            /// <summary>
            /// Windows / macOS / Linux / WebGL window or browser. Orientation follows the live
            /// aspect — Inspector: Wide Screen.
            /// </summary>
            WideScreen = 9,
        }

        /// <summary>Inspector / docs name for a form factor (FoldPhoneCover → Fold Cover).</summary>
        /// <remarks>On the iOS build target Phone / Pad read iPhone / iPad.</remarks>
        public static string GetFormDisplayName(FormFactor form) =>
            form switch
            {
                FormFactor.Phone when IosNames => "iPhone",
                FormFactor.Pad when IosNames => "iPad",
                FormFactor.Pad => "Pad",
                FormFactor.FoldHalfOpened => "Fold Half Opened",
                FormFactor.FlipHalfOpened => "Flip Half Opened",
                FormFactor.FoldPhoneCover => "Fold Cover",
                FormFactor.FlipPhoneCover => "Flip Cover",
                FormFactor.FoldCoverHalfOpened => "Fold Cover Half Opened",
                FormFactor.iPhoneDuoCover => "iPhone Duo Cover",
                FormFactor.iPhoneDuoSeated => "iPhone Duo Seated",
                FormFactor.WideScreen => "Wide Screen",
                _ => "Phone",
            };

        /// <summary>
        /// Wide Screen build target with no Device Simulator profile: the window, with no mobile detection.
        /// A selected Simulator profile overrides this on any Editor build target.
        /// </summary>
        static bool IsWideScreenBuild =>
            ResponsivePlatforms.ConstrainsForms &&
            ResponsivePlatforms.Active == ResponsivePlatform.WideScreen;

        static bool IosNames =>
            ResponsivePlatforms.ConstrainsForms &&
            ResponsivePlatforms.Active == ResponsivePlatform.Ios;

        public enum Orientation
        {
            Portrait = 0,
            Landscape,
        }

        /// <summary>Form factor + orientation used to pick a widget layout slot.</summary>
        public readonly struct LayoutKey : IEquatable<LayoutKey>
        {
            public readonly FormFactor Form;
            public readonly Orientation Orientation;

            public LayoutKey(FormFactor form, Orientation orientation)
            {
                Form = form;
                Orientation = orientation;
            }

            public bool Equals(LayoutKey other) =>
                Form == other.Form && Orientation == other.Orientation;

            public override bool Equals(object obj) =>
                obj is LayoutKey other && Equals(other);

            public override int GetHashCode() =>
                ((int)Form * 397) ^ (int)Orientation;

            public override string ToString() => $"{Form}/{Orientation}";
        }

        #endregion

        #region Constants

        /// <summary>
        /// Diagonal inches at or above this value count as Android pad.
        /// </summary>
        public const float AndroidPadMinDiagonalInches = 7f;

        /// <summary>
        /// Default max/min aspect that still counts as Pad (inclusive: 16:10 tablets sit on it).
        /// Players use it; the Editor uses the hub's Editor Pad Max Aspect.
        /// </summary>
        public const float DefaultPadMaxAspect = 1.6f;

        /// <summary>
        /// Watch / scene-layout size change (px) that counts as a resize. Smaller deltas are
        /// sub-pixel or 1px Simulator flicker.
        /// </summary>
        public const float SizeHysteresisPixels = 2f;

        #endregion

        #region Runtime state

        static bool _resolved;
        static bool _hasFormOverride;
        static bool _hasOrientationOverride;
        static DeviceType _type;
        static FormFactor _formOverride;
        static Orientation _orientationOverride;
        static float _diagonalInches;
#if UNITY_EDITOR
        static bool _hasSimulatorLayout;
        static FormFactor _simulatorForm;
        static Orientation _simulatorOrientation;
        static int _simulatorWidth;
        static int _simulatorHeight;
#endif

        #endregion

        #region Current layout

        /// <summary>
        /// Active form factor: <see cref="DetectActiveFormFactor(Vector2, float)"/> for
        /// <see cref="GetWatchScreenSize"/> (Simulator pin / Device.Screen), not Editor Game View
        /// <c>UnityEngine.Screen</c>, so Cover upgrades and slot Apply follow the selected catalog profile.
        /// </summary>
        public static FormFactor CurrentFormFactor => DetectActiveFormFactor(GetWatchScreenSize());

        /// <summary>
        /// Slot orientation for the watch size (see <see cref="DetectOrientationForForm"/>).
        /// An explicit orientation override wins. Simulator pin orientation wins otherwise.
        /// Watch size, not Game View.
        /// </summary>
        public static Orientation CurrentOrientation => CurrentLayout.Orientation;

        /// <summary><see cref="ResolveLiveLayout"/> for <see cref="GetWatchScreenSize"/>.</summary>
        public static LayoutKey CurrentLayout => ResolveLiveLayout(GetWatchScreenSize());

        /// <summary>
        /// Slot orientation for <paramref name="form"/>: the Editor Simulator pin when one is set,
        /// else <paramref name="screenSize"/> through <see cref="DetectOrientationForForm"/>.
        /// </summary>
        static Orientation OrientationFor(FormFactor form, Vector2 screenSize)
        {
#if UNITY_EDITOR
            // The selected Device Simulator profile owns orientation on every Editor build target.
            if (_hasSimulatorLayout)
                return OrientationForForm(form, _simulatorOrientation);
#endif
            return DetectOrientationForForm(form, screenSize.x, screenSize.y);
        }

        /// <summary>
        /// Live layout for hosts that are not <see cref="ResponsiveUiHandler"/> (Screen + fold manager
        /// + Simulator pin). Same form detection as <see cref="DetectActiveFormFactor"/>.
        /// </summary>
        /// <remarks>
        /// Honours <see cref="SetFormOverride"/> / <see cref="SetOrientationOverride"/> pins the
        /// same way as <see cref="CurrentFormFactor"/> / <see cref="CurrentOrientation"/>.
        /// </remarks>
        public static LayoutKey ResolveLiveLayout(
            Vector2 screenSize,
            float padMaxAspect = DefaultPadMaxAspect)
        {
            FormFactor form = DetectActiveFormFactor(screenSize, padMaxAspect);
            if (_hasOrientationOverride)
                return new LayoutKey(form, _orientationOverride);

            return new LayoutKey(form, OrientationFor(form, screenSize));
        }

        /// <summary>Physical screen diagonal in inches (uses 160 DPI fallback when DPI is unavailable).</summary>
        public static float DiagonalInches
        {
            get
            {
                EnsureDiagonal();
                return _diagonalInches;
            }
        }

        #endregion

        #region Detection

        /// <summary>
        /// Slot orientation for a <paramref name="screen"/> shape. Android Fold / Fold Cover Half
        /// Opened name the posture: Landscape is the flat screen turned 90° (tall, e.g. Fold8
        /// 1848×2448, hinge left to right), Portrait the flat screen (wide, 2448×1848, hinge top
        /// to bottom). Every other form, iPhone Duo Seated and Flip Half Opened included, names
        /// the screen shape. Apply once to a screen shape; the mapping is its own inverse.
        /// </summary>
        public static Orientation OrientationForForm(FormFactor form, Orientation screen)
        {
            if (!IsFoldHalfOpenForm(form))
                return screen;
            return screen == Orientation.Landscape ? Orientation.Portrait : Orientation.Landscape;
        }

        /// <summary>
        /// Slot orientation for a <paramref name="width"/>×<paramref name="height"/> screen
        /// (<see cref="OrientationForForm"/>: Fold half-open names the posture, other forms the shape).
        /// </summary>
        public static Orientation DetectOrientationForForm(FormFactor form, float width, float height) =>
            OrientationForForm(form, DetectOrientation(width, height));

        /// <summary>
        /// Whether a slot can show under the Player Allowed Orientations axes. A slot needs the
        /// axis its label names. iPhone Duo Seated slots
        /// always show: while Seated the game allows only the hold's axis
        /// (<c>IphoneDuoHoldOrientation</c>), so the tall laptop (Seated Portrait) and the wide book
        /// (Seated Landscape) are both reachable whatever the lock.
        /// Fold / Fold Cover Half Opened slots name the posture, which the Player lock fixes the
        /// same way (landscape-only → Half Opened Landscape). Wide Screen follows the window shape, so
        /// both its slots pass. Orientation rule only: seeding also applies
        /// <see cref="ResponsivePlatforms.SeedsEnabled"/> (Wide Screen starts Enabled on Wide
        /// Screen targets only).
        /// </summary>
        public static bool SlotAllowedByPlayerAxes(
            FormFactor form,
            Orientation slotOrientation,
            bool portraitAllowed,
            bool landscapeAllowed)
        {
            // iPhone Duo Seated allows only the hold's axis, so both Seated slots are reachable.
            // Wide Screen (desktop / WebGL) follows the window or browser shape, never the
            // mobile Player orientation lock.
            if (form == FormFactor.iPhoneDuoSeated || form == FormFactor.WideScreen)
                return true;

            return slotOrientation == Orientation.Landscape
                ? landscapeAllowed
                : portraitAllowed;
        }

        static bool IsFoldHalfOpenForm(FormFactor form) =>
            form == FormFactor.FoldHalfOpened ||
            form == FormFactor.FoldCoverHalfOpened;

        public static Orientation DetectOrientation(float width, float height)
        {
            float w = Mathf.Abs(width);
            float h = Mathf.Abs(height);
            if (w <= 0f || h <= 0f)
                return Orientation.Portrait;
            return w > h ? Orientation.Landscape : Orientation.Portrait;
        }

        public static Orientation DetectOrientation(Vector2 size) =>
            DetectOrientation(size.x, size.y);

        /// <summary>
        /// Call when the active display size may have changed (rotate, fold, multi-window)
        /// so Phone/Pad hardware classification can recompute.
        /// Does not clear Editor overrides.
        /// </summary>
        public static void NotifyDisplayChanged()
        {
            _resolved = false;
            _diagonalInches = 0f;
        }

        /// <summary>
        /// Active form factor for layout. A Device Simulator profile (Editor) uses that device on any
        /// build target. Otherwise Wide Screen targets → Wide Screen (window size, no mobile
        /// detection), and mobile targets use Fold/Flip detector → Game View / Simulator aspect
        /// (Editor) or hardware (device), constrained to the build target's forms.
        /// Posture and cover-from-size run in the Editor, on Android players, and on supported
        /// iPhone Duo iOS players; other mobile players stay Phone / Pad.
        /// </summary>
        /// <remarks>
        /// Fold8 Cover landscape (~1972×1248, aspect ~1.58) is <see cref="FormFactor.FoldPhoneCover"/>,
        /// not Pad. Flip Cover (~1048×948, near-square) is <see cref="FormFactor.FlipPhoneCover"/>,
        /// not Pad. Editor Simulator profiles can use known cover/inner sizes without a
        /// scene detector. Android players require a detector snapshot before Cover
        /// size refinements; an empty WindowLayoutInfo keeps hardware Phone/Pad except
        /// known Fold Expand inner WxH, plus the iPhone Duo inner off Android
        /// (<see cref="IsKnownInnerPadSize"/> → Pad).
        /// Tall covers (Fold7 / Ultra) stay <see cref="FormFactor.Phone"/>.
        /// </remarks>
        public static FormFactor DetectActiveFormFactor(
            Vector2 screenSize,
            float padMaxAspect = DefaultPadMaxAspect) =>
            DetectActiveFormFactor(
                screenSize, padMaxAspect, FoldablePostureSource.IsRuntimeSupported);

        /// <summary>
        /// Same as <see cref="DetectActiveFormFactor(Vector2, float)"/> with an explicit
        /// fold-runtime flag (tests can pass false to mimic non-foldable players).
        /// A <see cref="SetFormOverride"/> pin wins first, matching
        /// <see cref="CurrentFormFactor"/> (the pin is Editor / test only; the Simulator pin
        /// is separate and unaffected).
        /// </summary>
        public static FormFactor DetectActiveFormFactor(
            Vector2 screenSize,
            float padMaxAspect,
            bool foldableRuntimeSupported)
        {
            if (_hasFormOverride)
                return _formOverride;
            // No Simulator device: a Wide Screen target stays the window.
            if (IsWideScreenBuild && !EditorSimulatorDrivesLayout)
                return FormFactor.WideScreen;
            FormFactor detected = DetectUnconstrainedFormFactor(
                screenSize, padMaxAspect, foldableRuntimeSupported);
            // The selected Device Simulator profile keeps its form (Fold Cover, iPhone Duo, Phone)
            // on every Editor build target. Players have no pin and stay on the build target.
            if (EditorSimulatorDrivesLayout)
                return detected;
            return ResponsivePlatforms.ConstrainDetected(detected);
        }

        static FormFactor DetectUnconstrainedFormFactor(
            Vector2 screenSize,
            float padMaxAspect,
            bool foldableRuntimeSupported)
        {
            float w = Mathf.Abs(screenSize.x);
            float h = Mathf.Abs(screenSize.y);

#if UNITY_EDITOR
            if (foldableRuntimeSupported && _hasSimulatorLayout)
                return RefineCoverForms(_simulatorForm, w, h);
#endif

            if (foldableRuntimeSupported &&
                MapFromFoldable(
                    out FormFactor foldForm,
                    out FoldableDeviceKind foldKind))
                return RefineFoldableForm(foldKind, foldForm, w, h);

#if UNITY_EDITOR
            if (foldableRuntimeSupported &&
                w > 0f &&
                h > 0f &&
                MapKnownWideCoverSize(w, h, out FormFactor wideCover))
                return wideCover;

            if (foldableRuntimeSupported &&
                MapKnownFlipCoverSize(w, h, out FormFactor flipCover))
                return flipCover;

            if (w > 0f && h > 0f)
                return ClassifyPhoneOrPadByAspect(w, h, padMaxAspect);

            return FormFactor.Phone;
#else
            // Fold Expand inner can arrive before the first Jetpack posture callback.
            // Known Expand WxH stays Pad; do not aspect-classify (Cover landscape ~1.58
            // would misread as Pad) and do not grant Cover from size alone.
            if (foldableRuntimeSupported &&
                w > 0f &&
                h > 0f &&
                IsKnownInnerPadSize(w, h))
                return FormFactor.Pad;

            EnsureResolved();
            return ResolvePlayerHardwareFallback(_type);
#endif
        }

        /// <summary>
        /// Player fallback after no confirmed fold posture was available. Platform
        /// support or a window resolution alone must not override hardware identity.
        /// </summary>
        internal static FormFactor ResolvePlayerHardwareFallback(
            DeviceType hardwareType) =>
            hardwareType == DeviceType.Pad
                ? FormFactor.Pad
                : FormFactor.Phone;

        internal static FormFactor RefineFoldableForm(
            FoldableDeviceKind kind,
            FormFactor form,
            float width,
            float height)
        {
            switch (kind)
            {
                case FoldableDeviceKind.FoldablePhone:
#if UNITY_IOS && !UNITY_EDITOR
                    // Only iPhone Duo reports a fold on iOS and its backend decides Closed from
                    // view points, so a scaled render size (Target DPI, Screen.SetResolution)
                    // must not miss the cover size band.
                    if (form == FormFactor.Phone)
                        return FormFactor.iPhoneDuoCover;
#endif
                    return RefineFoldHalfOpened(
                        RefineWideCover(form, width, height),
                        width,
                        height);
                case FoldableDeviceKind.FlipablePhone:
                    return RefineFlipPhoneCover(form, width, height);
                default:
                    return form;
            }
        }

        /// <summary>
        /// Inclusive padMax: 16:10 tablets sit exactly on the limit (e.g. 2560×1600).
        /// </summary>
        static FormFactor ClassifyPhoneOrPadByAspect(
            float width,
            float height,
            float padMaxAspect)
        {
            float aspect = Mathf.Max(width, height) / Mathf.Min(width, height);
            float padMax = padMaxAspect > 1f ? padMaxAspect : DefaultPadMaxAspect;
            return aspect <= padMax ? FormFactor.Pad : FormFactor.Phone;
        }

        /// <summary>Closed outer displays: Fold Cover, Flip Cover, iPhone Duo Cover.</summary>
        public static bool IsCoverForm(FormFactor form) =>
            form == FormFactor.FoldPhoneCover ||
            form == FormFactor.FlipPhoneCover ||
            form == FormFactor.iPhoneDuoCover;

        /// <summary>
        /// Layout Apply writes while a Device Simulator profile is selected. Inspector focus
        /// and capture use this same key so the element and its slot stay one layout.
        /// </summary>
        public static bool EditorPreviewLayout(out LayoutKey layout)
        {
#if UNITY_EDITOR
            if (_hasSimulatorLayout)
            {
                layout = CurrentLayout;
                return true;
            }
#endif
            layout = default;
            return false;
        }

        /// <summary>
        /// Editor Device Simulator has a selected profile. Detection, slots, and focus follow that
        /// profile on any build target. Players always return false.
        /// </summary>
        public static bool EditorSimulatorDrivesLayout
        {
            get
            {
#if UNITY_EDITOR
                return _hasSimulatorLayout;
#else
                return false;
#endif
            }
        }

        /// <summary>True for every half-open (hinge-split) form.</summary>
        public static bool IsHalfOpenedForm(FormFactor form) =>
            form == FormFactor.FoldHalfOpened ||
            form == FormFactor.FlipHalfOpened ||
            form == FormFactor.FoldCoverHalfOpened ||
            form == FormFactor.iPhoneDuoSeated;

        /// <summary>
        /// Fold8 Cover → FoldPhoneCover, iPhone Duo Cover → iPhoneDuoCover, Flip Cover →
        /// FlipPhoneCover, Fold8-style HalfOpened → FoldCoverHalfOpened, iPhone Duo HalfOpened →
        /// iPhoneDuoSeated.
        /// </summary>
        public static FormFactor RefineCoverForms(FormFactor form, float width, float height) =>
            RefineFoldHalfOpened(
                RefineFlipPhoneCover(RefineWideCover(form, width, height), width, height),
                width,
                height);

        /// <summary>
        /// Maps a wide Cover WxH to its cover form (<see cref="MapKnownWideCoverSize"/>).
        /// Upgrades Phone and Pad so a lagged Expand mapping cannot keep Cover on Pad.
        /// HalfOpened is unchanged (inner tabletop is not Cover size).
        /// </summary>
        public static FormFactor RefineWideCover(FormFactor form, float width, float height)
        {
            if (!MapKnownWideCoverSize(width, height, out FormFactor cover))
                return form;
            if (IsHalfOpenedForm(form))
                return form;
            return cover;
        }

        /// <summary>
        /// iPhone Duo Cover WxH → <see cref="FormFactor.iPhoneDuoCover"/> (not on Android players,
        /// <see cref="MatchesIphoneDuoSizes"/>); Fold8 Cover WxH → <see cref="FormFactor.FoldPhoneCover"/>.
        /// </summary>
        public static bool MapKnownWideCoverSize(float width, float height, out FormFactor form)
        {
            if (MatchesIphoneDuoSizes && IsKnownIphoneDuoCoverSize(width, height))
            {
                form = FormFactor.iPhoneDuoCover;
                return true;
            }
            if (IsKnownFoldCoverPhoneSize(width, height))
            {
                form = FormFactor.FoldPhoneCover;
                return true;
            }
            form = FormFactor.Phone;
            return false;
        }

        /// <summary>
        /// Maps Flip Cover WxH to <see cref="FormFactor.FlipPhoneCover"/>. Upgrades Phone, Pad,
        /// and a leftover Fold Cover pin so a near-square cover is not Pad-by-aspect or stuck
        /// on FoldPhoneCover. HalfOpened is unchanged (inner tabletop is not Cover size).
        /// Fold8 Cover WxH is outside the Flip Cover band, so a live Fold Cover pin is not stolen.
        /// </summary>
        public static FormFactor RefineFlipPhoneCover(FormFactor form, float width, float height)
        {
            if (IsIgnoringStaleFlipCoverSize())
                return form;
            if (!IsKnownFlipCoverPhoneSize(width, height))
                return form;
            if (IsHalfOpenedForm(form))
                return form;
            return FormFactor.FlipPhoneCover;
        }

        /// <summary>
        /// Fold8-style HalfOpened inner → <see cref="FormFactor.FoldCoverHalfOpened"/>;
        /// iPhone Duo inner → <see cref="FormFactor.iPhoneDuoSeated"/> (its fold runs the other
        /// way for the same screen orientation). Classic Fold7 / older HalfOpened stays
        /// <see cref="FormFactor.FoldHalfOpened"/>. iOS players always map HalfOpened to
        /// iPhone Duo; Android players never do.
        /// </summary>
        public static FormFactor RefineFoldHalfOpened(FormFactor form, float width, float height)
        {
            if (form != FormFactor.FoldHalfOpened)
                return form;
#if UNITY_IOS && !UNITY_EDITOR
            // Only iPhone Duo reports a hinge on iOS; a resized scene may not match its inner WxH.
            return FormFactor.iPhoneDuoSeated;
#else
            if (MatchesIphoneDuoSizes && IsKnownIphoneDuoInnerSize(width, height))
                return FormFactor.iPhoneDuoSeated;
            if (IsKnownFoldCoverHalfOpenedSize(width, height))
                return FormFactor.FoldCoverHalfOpened;
            return form;
#endif
        }

        /// <summary>
        /// False on Android players: no iPhone Duo there, and its size bands brush Fold8
        /// inner / cover drift.
        /// </summary>
        internal static bool MatchesIphoneDuoSizes =>
#if UNITY_ANDROID && !UNITY_EDITOR
            false;
#else
            true;
#endif

        /// <summary>
        /// Fold8 Cover full (1248×1972) and safe-area insets. Exact 1972-only matching
        /// missed Simulator Safe Area sizes → Pad ortho (~1.58 under padMax 1.6).
        /// Tall Fold7/Ultra Cover (1080×2520) is not matched — stays <see cref="FormFactor.Phone"/>.
        /// </summary>
        public static bool IsKnownFoldCoverPhoneSize(float width, float height)
        {
            int w = Mathf.RoundToInt(Mathf.Abs(width));
            int h = Mathf.RoundToInt(Mathf.Abs(height));
            if ((w == 1248 && (h == 1972 || h == 1852)) ||
                (h == 1248 && (w == 1972 || w == 1852)))
                return true;

            int longSide = Mathf.Max(w, h);
            int shortSide = Mathf.Min(w, h);
            if (shortSide <= 0 || longSide <= 0)
                return false;

            // Exact short side with minor long-side drift (full Cover landscape/portrait).
            if (shortSide == 1248 && longSide >= 1800 && longSide <= 2000)
                return true;

            // Simulator Safe Area often insets both axes (not only 1852×1248). Fold8 Cover
            // aspect is ~1.580 — still under default padMax 1.6, so aspect-alone → Pad.
            if (shortSide >= 1100 && shortSide <= 1300 &&
                longSide >= 1700 && longSide <= 2100)
            {
                float aspect = longSide / (float)shortSide;
                if (aspect > 1.55f && aspect < 1.595f)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Flip8 Cover full (948×1048). Near-square (~1.10) is under padMax 1.6, so
        /// aspect-alone would be Pad. Must not match Fold8 Cover (short ≥ 1100) or
        /// Flip main (long ≥ 2500).
        /// </summary>
        public static bool IsKnownFlipCoverPhoneSize(float width, float height)
        {
            int w = Mathf.RoundToInt(Mathf.Abs(width));
            int h = Mathf.RoundToInt(Mathf.Abs(height));
            if ((w == 948 && h == 1048) || (h == 948 && w == 1048))
                return true;

            int longSide = Mathf.Max(w, h);
            int shortSide = Mathf.Min(w, h);
            if (shortSide <= 0 || longSide <= 0)
                return false;

            if (shortSide == 948 && longSide >= 980 && longSide <= 1100)
                return true;

            if (shortSide >= 850 && shortSide <= 1050 &&
                longSide >= 900 && longSide <= 1150)
            {
                float aspect = longSide / (float)shortSide;
                if (aspect >= 1f && aspect < 1.25f)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Fold8 Inner full (1848×2448) and safe-area drift. Aspect ~1.325.
        /// Fold7 Inner (1968×2184, ~1.11) is not matched — stays Fold Half Opened.
        /// </summary>
        public static bool IsKnownFoldCoverHalfOpenedSize(float width, float height)
        {
            int w = Mathf.RoundToInt(Mathf.Abs(width));
            int h = Mathf.RoundToInt(Mathf.Abs(height));
            if ((w == 1848 && (h == 2448 || h == 2328)) ||
                (h == 1848 && (w == 2448 || w == 2328)))
                return true;

            int longSide = Mathf.Max(w, h);
            int shortSide = Mathf.Min(w, h);
            if (shortSide <= 0 || longSide <= 0)
                return false;

            if (shortSide == 1848 && longSide >= 2200 && longSide <= 2500)
                return true;

            if (shortSide >= 1700 && shortSide <= 1950 &&
                longSide >= 2200 && longSide <= 2550)
            {
                float aspect = longSide / (float)shortSide;
                if (aspect > 1.28f && aspect < 1.38f)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Fold7 Inner full (1968×2184) and Device Simulator / OEM safe-area drift
        /// (e.g. landscape <c>2154×1968</c>). Aspect ~1.11 — must not match Fold8
        /// inner (~1.325) or Flip Cover (~1.10 but short ≤ 1050).
        /// </summary>
        public static bool IsKnownFoldPhoneInnerSize(float width, float height)
        {
            int w = Mathf.RoundToInt(Mathf.Abs(width));
            int h = Mathf.RoundToInt(Mathf.Abs(height));
            if ((w == 1968 && h == 2184) || (h == 1968 && w == 2184))
                return true;

            int longSide = Mathf.Max(w, h);
            int shortSide = Mathf.Min(w, h);
            if (shortSide <= 0 || longSide <= 0)
                return false;

            // Exact short side with long-side inset (Safe Area on Fold7-like Inner).
            if (shortSide == 1968 && longSide >= 2050 && longSide <= 2250)
                return true;

            // Both-axis insets; keep Fold7-like aspect away from Fold8 inner.
            if (shortSide >= 1850 && shortSide <= 2050 &&
                longSide >= 2000 && longSide <= 2300)
            {
                float aspect = longSide / (float)shortSide;
                if (aspect > 1.05f && aspect < 1.20f)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Fold Expand / Dual Opened Pad identity: Fold7 or Fold8 inner, including
        /// Simulator safe-area insets.
        /// </summary>
        public static bool IsKnownFoldInnerPadSize(float width, float height) =>
            IsKnownFoldPhoneInnerSize(width, height) ||
            IsKnownFoldCoverHalfOpenedSize(width, height);

        /// <summary>
        /// iPhone Duo Cover full (1398×2034, aspect ~1.455) and safe-area insets. Upgrades a
        /// FoldablePhone Closed / Phone to <see cref="FormFactor.iPhoneDuoCover"/> in the Editor /
        /// Simulator; iOS players map Closed to Cover from posture, not size
        /// (<see cref="RefineFoldableForm"/>).
        /// Not a Design Scale identity size — that stays Fold8-only. Disjoint from Fold8 Cover
        /// (aspect ≥ 1.55) and every iPad (short side ≥ 1488).
        /// </summary>
        public static bool IsKnownIphoneDuoCoverSize(float width, float height)
        {
            int w = Mathf.RoundToInt(Mathf.Abs(width));
            int h = Mathf.RoundToInt(Mathf.Abs(height));
            if ((w == 1398 && h == 2034) || (h == 1398 && w == 2034))
                return true;

            int longSide = Mathf.Max(w, h);
            int shortSide = Mathf.Min(w, h);
            if (shortSide < 1250 || shortSide > 1420 || longSide < 1600 || longSide > 2060)
                return false;

            float aspect = longSide / (float)shortSide;
            return aspect >= 1.20f && aspect <= 1.50f;
        }

        /// <summary>
        /// iPhone Duo Inner full (2853×2007 px landscape-native = 951×669 pt @3x, aspect ~1.42) and
        /// safe-area insets (down to ~2007×2688 tall / 2727×1944 wide).
        /// HalfOpened → <see cref="FormFactor.iPhoneDuoSeated"/>; player Expand fallback → Pad.
        /// Opened / HalfOpened Design Scale identity is posture-owned.
        /// The band (short 1940–2020, long 2680–2870) is disjoint from Fold7 / Fold8 inner and
        /// cover (long ≤ 2550) and every iPad: 13″ is 2048–2064 short, 11″ / Air / mini are
        /// ≤ 2420 long. Android players skip it (<see cref="MatchesIphoneDuoSizes"/>).
        /// </summary>
        public static bool IsKnownIphoneDuoInnerSize(float width, float height)
        {
            int w = Mathf.RoundToInt(Mathf.Abs(width));
            int h = Mathf.RoundToInt(Mathf.Abs(height));
            if ((w == 2007 && h == 2853) || (h == 2007 && w == 2853))
                return true;

            int longSide = Mathf.Max(w, h);
            int shortSide = Mathf.Min(w, h);
            if (shortSide < 1940 || shortSide > 2020 || longSide < 2680 || longSide > 2870)
                return false;

            float aspect = longSide / (float)shortSide;
            return aspect >= 1.33f && aspect <= 1.48f;
        }

        /// <summary>
        /// Fold8 passport Cover or iPhone Duo Cover (not Design Scale identity size checks).
        /// The iPhone Duo band is skipped where <see cref="MatchesIphoneDuoSizes"/> is false.
        /// </summary>
        public static bool IsKnownWideCoverPhoneSize(float width, float height) =>
            IsKnownFoldCoverPhoneSize(width, height) ||
            (MatchesIphoneDuoSizes && IsKnownIphoneDuoCoverSize(width, height));

        /// <summary>
        /// Player Expand fallback before the first posture: Fold inner, or iPhone Duo inner
        /// where <see cref="MatchesIphoneDuoSizes"/> is true → Pad.
        /// </summary>
        public static bool IsKnownInnerPadSize(float width, float height) =>
            IsKnownFoldInnerPadSize(width, height) ||
            (MatchesIphoneDuoSizes && IsKnownIphoneDuoInnerSize(width, height));

        /// <summary>
        /// Fold Cover, iPhone Duo Cover, or Flip Cover WxH (including Simulator safe-area insets).
        /// </summary>
        public static bool IsKnownCoverPhoneSize(float width, float height) =>
            IsKnownWideCoverPhoneSize(width, height) ||
            IsKnownFlipCoverPhoneSize(width, height);

        /// <summary>
        /// Design Scale identity from Jetpack / Simulator <b>product posture</b>
        /// (<see cref="FoldableProductState.Opened"/> / Flat, <see cref="FoldableProductState.HalfOpened"/>)
        /// without requiring Screen WxH ≈ catalog Design Canvas.
        /// Closed/Normal still uses Cover size helpers (Jetpack has no CLOSED enum).
        /// </summary>
        public static bool IsPostureOwnedDesignIdentity(
            FormFactor slotForm,
            float watchWidth,
            float watchHeight)
        {
            if (!ResolveOpenedOrHalfOpenedForm(out FormFactor mapped, out _))
                return false;

            FormFactor refined = RefineCoverForms(mapped, watchWidth, watchHeight);
            return refined == slotForm;
        }

        /// <summary>
        /// Live Fold/Flip Opened or HalfOpened mapping from the posture detector, or
        /// an Editor Simulator pin that already encodes Expand / HalfOpened.
        /// </summary>
        public static bool ResolveOpenedOrHalfOpenedForm(
            out FormFactor mapped,
            out FoldableProductState product)
        {
            mapped = FormFactor.Phone;
            product = FoldableProductState.Undefined;

            if (!FoldablePostureSource.IsRuntimeSupported)
                return false;

            FoldableLayoutState layout = FoldablePostureSource.FindActiveLayoutState();
            if (layout != null && layout.deviceKind != FoldableDeviceKind.Unknown)
            {
                product = layout.productState;
                if (product == FoldableProductState.Undefined)
                {
                    // A newly enabled detector has no confirmed posture yet.
                    if (layout.state == FoldableState.Undefined)
                        return false;

                    product = layout.ResolveProductState();
                }

                if (product != FoldableProductState.Opened &&
                    product != FoldableProductState.HalfOpened)
                    return false;

                mapped = MapFoldableProduct(layout.deviceKind, product);
                return true;
            }

#if UNITY_EDITOR
            // Device Simulator Expand / HalfOpened pin — form is already mapped.
            if (_hasSimulatorLayout &&
                InferOpenedOrHalfOpenedProduct(_simulatorForm, out product))
            {
                mapped = _simulatorForm;
                return true;
            }
#endif

            return false;
        }

#if UNITY_EDITOR
        static bool InferOpenedOrHalfOpenedProduct(
            FormFactor form,
            out FoldableProductState product)
        {
            switch (form)
            {
                case FormFactor.Pad:
                    product = FoldableProductState.Opened;
                    return true;
                case FormFactor.FoldHalfOpened:
                case FormFactor.FoldCoverHalfOpened:
                case FormFactor.iPhoneDuoSeated:
                case FormFactor.FlipHalfOpened:
                    product = FoldableProductState.HalfOpened;
                    return true;
                default:
                    product = FoldableProductState.Undefined;
                    return false;
            }
        }
#endif

        #endregion

        #region Simulator pin

#if UNITY_EDITOR
        /// <summary>
        /// Raised when the Editor Simulator layout pin changes. Hosts that are not
        /// <see cref="ResponsiveUiHandler"/> subscribe instead of polling Detect.
        /// </summary>
        public static event Action SimulatorLayoutChanged;

        /// <summary>
        /// Editor Fold session: ★ Flip Opened / HalfOpened while Device Simulator is
        /// still on Cover (catalog lists Cover first). Cover WxH must not map Active
        /// Objects to Flip Cover.
        /// </summary>
        public static Func<bool> ShouldIgnoreStaleFlipCoverSize;
#endif

        /// <summary>
        /// True when Flip Opened / HalfOpened owns layout and leftover Cover chrome
        /// must not upgrade to <see cref="FormFactor.FlipPhoneCover"/>.
        /// </summary>
        public static bool IsIgnoringStaleFlipCoverSize()
        {
#if UNITY_EDITOR
            return ShouldIgnoreStaleFlipCoverSize != null && ShouldIgnoreStaleFlipCoverSize();
#else
            return false;
#endif
        }

        static bool MapKnownFlipCoverSize(float width, float height, out FormFactor form)
        {
            form = FormFactor.FlipPhoneCover;
            if (width <= 0f || height <= 0f || !IsKnownFlipCoverPhoneSize(width, height))
                return false;
            if (IsIgnoringStaleFlipCoverSize())
            {
                form = FormFactor.Phone;
                return true;
            }

            form = FormFactor.FlipPhoneCover;
            return true;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Pins form/orientation from the Editor Device Simulator (Fold Cover/Expand/HalfOpened).
        /// Survives Play Mode so Cover (~1.58 aspect) does not mis-detect as Pad.
        /// Cleared when the Simulator leaves a toolkit foldable device.
        /// </summary>
        /// <param name="orientation">Screen shape (Simulator rotation), not the slot label:
        /// <see cref="OrientationForForm"/> names the slot once.</param>
        /// <param name="width">Catalog pixels oriented to Simulator rotation. 0 keeps the previous pin.</param>
        /// <param name="height">Catalog pixels oriented to Simulator rotation. 0 keeps the previous pin.</param>
        public static void SetSimulatorLayout(
            FormFactor form,
            Orientation orientation,
            int width = 0,
            int height = 0)
        {
            if (width <= 0 || height <= 0)
            {
                width = _simulatorWidth;
                height = _simulatorHeight;
            }

            if (_hasSimulatorLayout &&
                _simulatorForm == form &&
                _simulatorOrientation == orientation &&
                _simulatorWidth == width &&
                _simulatorHeight == height)
                return;

            _hasSimulatorLayout = true;
            _simulatorForm = form;
            _simulatorOrientation = orientation;
            _simulatorWidth = width;
            _simulatorHeight = height;
            SimulatorLayoutChanged?.Invoke();
        }

        /// <summary>Clears <see cref="SetSimulatorLayout"/> (does not clear test override pins).</summary>
        public static void ClearSimulatorLayout()
        {
            if (!_hasSimulatorLayout && _simulatorWidth == 0 && _simulatorHeight == 0)
                return;
            _hasSimulatorLayout = false;
            _simulatorWidth = 0;
            _simulatorHeight = 0;
            SimulatorLayoutChanged?.Invoke();
        }

        public static bool HasSimulatorLayout => _hasSimulatorLayout;

        /// <summary>
        /// Simulator catalog pixels when <see cref="SetSimulatorLayout"/> pinned a size.
        /// </summary>
        public static bool GetSimulatorScreenSize(out Vector2 size)
        {
            if (_hasSimulatorLayout && _simulatorWidth > 0 && _simulatorHeight > 0)
            {
                size = new Vector2(_simulatorWidth, _simulatorHeight);
                return true;
            }

            size = default;
            return false;
        }
#endif

        /// <summary>
        /// Live size for detection and Design Scale. Prefers Simulator catalog pixels, then
        /// <c>UnityEngine.Device.Screen</c> (simulated device). <c>UnityEngine.Screen</c> in the
        /// Editor is the Game View widget (Scale to Fit) and must not drive canvas ratio.
        /// </summary>
        public static Vector2 GetWatchScreenSize()
        {
#if UNITY_EDITOR
            if (GetSimulatorScreenSize(out Vector2 pinned))
                return pinned;
#endif

            int w = UnityEngine.Device.Screen.width;
            int h = UnityEngine.Device.Screen.height;
            if (w > 0 && h > 0)
                return new Vector2(w, h);

            return new Vector2(Screen.width, Screen.height);
        }

        #endregion

        #region Foldable mapping

        /// <summary>
        /// Maps FoldAndFlip product posture → Responsive form factor.
        /// Fold Expand (Opened) → Pad; Fold Normal → Phone (Fold8 / iPhone Duo Cover WxH upgrade via
        /// <see cref="RefineWideCover"/>; iOS players map every Closed to iPhone Duo Cover in
        /// <see cref="RefineFoldableForm"/>); Flip Closed → Flip Cover; Flip Opened → Phone;
        /// HalfOpened → dedicated slots.
        /// </summary>
        public static FormFactor MapFoldableProduct(
            FoldableDeviceKind kind,
            FoldableProductState product)
        {
            switch (kind)
            {
                case FoldableDeviceKind.FoldablePhone:
                    switch (product)
                    {
                        case FoldableProductState.Opened:
                            return FormFactor.Pad; // Fold Expand ≈ Pad
                        case FoldableProductState.HalfOpened:
                            return FormFactor.FoldHalfOpened;
                        case FoldableProductState.Closed:
                            // Fold Normal (cover). Cover WxH → FoldPhoneCover / iPhoneDuoCover via
                            // RefineWideCover (iOS players: RefineFoldableForm → iPhoneDuoCover).
                            return FormFactor.Phone;
                        default:
                            return FormFactor.Phone;
                    }

                case FoldableDeviceKind.FlipablePhone:
                    switch (product)
                    {
                        case FoldableProductState.HalfOpened:
                            return FormFactor.FlipHalfOpened;
                        case FoldableProductState.Closed:
                            return FormFactor.FlipPhoneCover;
                        case FoldableProductState.Opened:
                            return FormFactor.Phone; // Flip Opened uses Phone slots
                        default:
                            return FormFactor.Phone;
                    }

                case FoldableDeviceKind.DualScreen:
                    switch (product)
                    {
                        case FoldableProductState.Opened:
                            return FormFactor.Pad; // Spanned
                        case FoldableProductState.HalfOpened:
                            return FormFactor.FoldHalfOpened; // Flex reuses Fold half-open slots
                        case FoldableProductState.Closed:
                            return FormFactor.Phone; // Single pane
                        default:
                            return FormFactor.Phone;
                    }

                default:
                    return FormFactor.Phone;
            }
        }

        public static FormFactor MapFoldableLayout(FoldableLayoutState layout)
        {
            if (layout == null)
                return FormFactor.Phone;

            FoldableProductState product = layout.productState;
            if (product == FoldableProductState.Undefined)
                product = layout.ResolveProductState();

            return MapFoldableProduct(layout.deviceKind, product);
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        /// <summary>
        /// Fold vs Fold Cover half-open card for the Android runtime hold: the same size rule that maps
        /// Fold Cover Half Opened, read from the device screen (never from the hold's own state).
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void BindFoldCoverHalfOpenCard()
        {
            FoldableOrientationHold.IsFoldCover = () =>
            {
                Vector2 size = GetWatchScreenSize();
                return IsKnownFoldCoverHalfOpenedSize(size.x, size.y);
            };
        }
#endif

        #endregion

        #region Overrides

        /// <summary>Pins form factor (including Fold/Flip HalfOpened) for Editor / tests.</summary>
        public static void SetFormOverride(FormFactor form)
        {
            _formOverride = form;
            _hasFormOverride = true;
        }

        /// <summary>Pins Portrait/Landscape for Editor / tests.</summary>
        public static void SetOrientationOverride(Orientation orientation)
        {
            _orientationOverride = orientation;
            _hasOrientationOverride = true;
        }

        /// <summary>
        /// Play-session reset (also runs when Enter Play Mode skips domain reload):
        /// hardware cache and Editor / test override pins. The Simulator pin is kept on
        /// purpose — it is authored in Edit Mode and must survive entering Play Mode
        /// (see <c>SetSimulatorLayout</c>). Editor-installed hooks such as
        /// <c>ShouldIgnoreStaleFlipCoverSize</c> are configuration and are not cleared.
        /// <c>SimulatorLayoutChanged</c> is not cleared: subscribers are scene objects
        /// that unsubscribe on disable, and with Enter Play Mode scene reload off they stay
        /// enabled across the transition.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetPlaySessionState()
        {
            _resolved = false;
            _type = DeviceType.Phone;
            _diagonalInches = 0f;
            _hasFormOverride = false;
            _hasOrientationOverride = false;
            _formOverride = FormFactor.Phone;
            _orientationOverride = Orientation.Portrait;
        }

        /// <summary>Clears pinned overrides and re-reads the device.</summary>
        public static void ClearOverride()
        {
            if (!_hasFormOverride && !_hasOrientationOverride)
                return;

            _hasFormOverride = false;
            _hasOrientationOverride = false;
            _resolved = false;
            EnsureResolved();
        }

        #endregion

        #region Hardware

        static bool MapFromFoldable(
            out FormFactor form,
            out FoldableDeviceKind kind)
        {
            form = FormFactor.Phone;
            kind = FoldableDeviceKind.Unknown;
            if (!FoldablePostureSource.IsRuntimeSupported)
                return false;

            FoldableLayoutState layout = FoldablePostureSource.FindActiveLayoutState();
            if (layout == null)
                return false;

            if (layout.deviceKind == FoldableDeviceKind.Unknown)
            {
                // Owner present but no fold/flip kind yet — keep hardware Phone/Pad.
                return false;
            }
            kind = layout.deviceKind;

            // First Jetpack callback has not arrived — do not treat empty as Fold Expand/Pad.
            if (layout.productState == FoldableProductState.Undefined &&
                layout.state == FoldableState.Undefined)
                return false;

            form = MapFoldableLayout(layout);
            return true;
        }

        static void EnsureResolved()
        {
            if (_resolved)
                return;

            EnsureDiagonal();
            _type = DetectType(_diagonalInches);
            _resolved = true;
        }

        static void EnsureDiagonal()
        {
            if (_diagonalInches > 0f)
                return;

            _diagonalInches = ComputeDiagonalInches();
        }

        static DeviceType DetectType(float diagonalInches)
        {
            // Unity reports IPhonePlayer for both iPhone and iPad.
            if (Application.platform == RuntimePlatform.IPhonePlayer)
                return IsIosPadDevice() ? DeviceType.Pad : DeviceType.Phone;

            if (Application.platform == RuntimePlatform.Android)
            {
                // Without a real DPI, diagonal is meaningless (160 fallback can mark phones as Pad).
                if (Screen.dpi <= 0f)
                    return DeviceType.Phone;

                return diagonalInches >= AndroidPadMinDiagonalInches
                    ? DeviceType.Pad
                    : DeviceType.Phone;
            }

            // Editor / desktop — default Phone. Use SetFormOverride or handler override to preview Pad.
            return DeviceType.Phone;
        }

        static bool IsIosPadDevice()
        {
            string model = SystemInfo.deviceModel;
            if (!string.IsNullOrEmpty(model) &&
                model.IndexOf("iPad", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

#if UNITY_IOS && !UNITY_EDITOR
        string generation = UnityEngine.iOS.Device.generation.ToString();
        if (generation.StartsWith("iPad", StringComparison.OrdinalIgnoreCase))
            return true;
#endif

            return false;
        }

        static float ComputeDiagonalInches()
        {
            float dpi = Screen.dpi;
            if (dpi <= 0f)
                dpi = 160f;

            float wIn = Screen.width / dpi;
            float hIn = Screen.height / dpi;
            return Mathf.Sqrt(wIn * wIn + hIn * hIn);
        }

        #endregion
    }

}
