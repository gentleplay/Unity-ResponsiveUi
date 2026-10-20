using System;
using UnityEditor;
using UnityEngine;

namespace FDPlus.Foldable.Editor
{
    /// <summary>
    /// Shared Editor session syncing Device Simulator plugin + catalog selection.
    /// </summary>
    [InitializeOnLoad]
    public static class FoldableEditorSession
    {
        #region Types

        public enum Source
        {
            Simulator,
            Posture,
            Bridge
        }

        #endregion

        #region State

        public static event Action<Source> Changed;

        /// <summary>
        /// Device Simulator Cover/Inner chrome just committed. Layout consumers must
        /// Apply even when session metrics were already Main (Phone→Flip first-bind).
        /// </summary>
        public static event Action ScreenSynced;

        public static bool IsFoldableDeviceBound { get; private set; }
        public static string BoundFriendlyName { get; private set; } = "";
        public static string BoundDeviceModel { get; private set; } = "";
        public static string BoundScreenName { get; private set; } = "";
        public static bool BoundScreenIsCover { get; private set; }
        public static FoldableDeviceKind DeviceKind { get; private set; } = FoldableDeviceKind.Unknown;

        public static FoldableState State { get; private set; } = FoldableState.Flat;
        public static FoldableOrientation Orientation { get; private set; } = FoldableOrientation.Horizontal;
        public static float HingeNormalized { get; private set; } = 0.5f;
        public static int HingeThicknessPx { get; private set; } = 24;
        public static bool IsSeparating { get; private set; }
        public static int MetricsWidth { get; private set; } = 1080;
        public static int MetricsHeight { get; private set; } = 2316;
        static FoldableProductState productState = FoldableProductState.Opened;

        /// <summary>
        /// Product posture. Becoming Half Opened records <see cref="HalfOpenEntryLandscape"/> from
        /// the current <see cref="IsLandscape"/>, so set the screen first.
        /// </summary>
        public static FoldableProductState ProductState
        {
            get => productState;
            private set
            {
                if (value == FoldableProductState.HalfOpened && productState != FoldableProductState.HalfOpened)
                    HalfOpenEntryLandscape = IsLandscape;
                productState = value;
            }
        }

        /// <summary>
        /// Screen (true = wide) the session had when Half Opened began. A Locked half-open card
        /// holds it while the Simulator is rotated (<see cref="AndroidHalfOpenHeldScreenLandscape"/>).
        /// </summary>
        public static bool HalfOpenEntryLandscape { get; private set; }

        /// <summary>
        /// Device screen rotation (independent of hinge Tabletop/Book).
        /// Landscape swaps catalog WxH for every product state.
        /// </summary>
        public static bool IsLandscape { get; private set; }

        /// <summary>
        /// Bound profile has a landscape-native inner (iPhone Duo). Half-open portrait is the
        /// tall laptop (horizontal fold); half-open landscape the wide book (vertical fold).
        /// Fold kind only, like the runtime hinge rule.
        /// </summary>
        public static bool BoundInnerLandscapeNative
        {
            get
            {
                var device = FindDeviceByFriendlyName(BoundFriendlyName);
                return device != null && device.HasLandscapeNativeInner;
            }
        }

        /// <summary>
        /// The active build target's player family: iPhone Duo
        /// (<paramref name="landscapeNativeInner"/>) on iOS, Android Fold / Flip on Android, none on
        /// other targets (Wide Screen). The Editor Device Simulator still previews either profile
        /// on any build target.
        /// </summary>
        public static bool BuildTargetOwnsProfile(bool landscapeNativeInner)
        {
#if UNITY_IOS
            return landscapeNativeInner;
#elif UNITY_ANDROID
            return !landscapeNativeInner;
#else
            return false;
#endif
        }

        static int suppressDepth;
        static bool nestedChangePending;
        static Source nestedChangeSource;

        public static bool NeedsInnerScreen =>
            ProductState == FoldableProductState.Opened ||
            ProductState == FoldableProductState.HalfOpened ||
            (ProductState == FoldableProductState.Undefined &&
             (State == FoldableState.HalfOpened || IsSeparating));

        /// <summary>
        /// Pose a Simulator panel's posture preset publishes for <paramref name="product"/> with
        /// <paramref name="hinge"/>: the Player lock or a changed card when it applies, else the
        /// default for Half Opened (Flip Flex rule; iPhone Duo Seated follows the hold, laptop →
        /// Portrait; an Android Fold keeps its screen, so the wide flat screen enters as
        /// Portrait), else the current pose.
        /// </summary>
        public static bool PresetPose(FoldableProductState product, FoldableOrientation hinge)
        {
            bool landscapeNative = BoundInnerLandscapeNative;
            bool? halfOpenScreen = AndroidHalfOpenHeldScreenLandscape(
                DeviceKind, product, landscapeNative) ??
                AndroidHalfOpenScreenLandscape(product, landscapeNative);
            if (halfOpenScreen.HasValue)
                return halfOpenScreen.Value;
            // Folding an Android Fold that nothing holds changes nothing: it keeps its screen.
            if (product == FoldableProductState.HalfOpened && !landscapeNative &&
                DeviceKind == FoldableDeviceKind.FoldablePhone)
                return IsLandscape;

            bool locked = FoldablePlayerOrientation.GetLockedLandscapeForProduct(
                product, landscapeNative).HasValue;
            if (product == FoldableProductState.HalfOpened || locked)
            {
                return FoldablePlayerOrientation.ResolveForProduct(
                    DeviceKind, hinge, product, landscapeNative);
            }
            return IsLandscape;
        }

        /// <summary>
        /// Android Fold / Fold Cover half-open screen, or null for iPhone Duo and when both
        /// axes are allowed. A landscape-only project rotates 90° off the wide flat screen:
        /// Fold Cover is 1848×2448, top to bottom, hinge left to right. Portrait-only is the
        /// other pose: 2448×1848, left to right, hinge top to bottom. iPhone Duo is not this path.
        /// A changed card (<see cref="ChangedAndroidHalfOpenCard"/>) wins: its
        /// <see cref="FoldableHalfOpenRules.ScreenLandscape"/>, e.g. Left to right → wide under
        /// landscape-only. <paramref name="kind"/>, <paramref name="friendlyName"/> and
        /// <paramref name="entryLandscape"/> default to the bound session (a bind passes its draft).
        /// </summary>
        public static bool? AndroidHalfOpenScreenLandscape(
            FoldableProductState product,
            bool landscapeNative,
            FoldableDeviceKind? kind = null,
            string friendlyName = null,
            bool? entryLandscape = null)
        {
            if (landscapeNative || product != FoldableProductState.HalfOpened)
                return null;

            bool? locked = FoldablePlayerOrientation.GetLockedLandscape();
            var deviceKind = kind ?? DeviceKind;
            var card = ChangedAndroidHalfOpenCard(deviceKind, product, landscapeNative, friendlyName ?? BoundFriendlyName);
            if (card != null)
            {
                bool entry = entryLandscape ??
                    (ProductState == FoldableProductState.HalfOpened ? HalfOpenEntryLandscape : IsLandscape);
                return FoldableHalfOpenRules.ScreenLandscape(
                    card, deviceKind == FoldableDeviceKind.FoldablePhone, locked, entry);
            }

            if (!locked.HasValue)
                return null;
            return !locked.Value;
        }

        /// <summary>
        /// The Android Fold / Fold Cover / Flip half-open card for <paramref name="kind"/> when it
        /// is changed from the platform default, else null (iPhone Duo, other kinds and postures).
        /// </summary>
        static FoldableHalfOpenPose ChangedAndroidHalfOpenCard(
            FoldableDeviceKind kind,
            FoldableProductState product,
            bool landscapeNative,
            string friendlyName)
        {
            if (landscapeNative || product != FoldableProductState.HalfOpened)
                return null;
            bool isFold = kind == FoldableDeviceKind.FoldablePhone;
            if (!isFold && kind != FoldableDeviceKind.FlipablePhone)
                return null;
            var pose = AndroidHalfOpenCard(kind, friendlyName);
            return pose.IsDefault ? null : pose;
        }

        /// <summary>
        /// True while the bound Android half-open card is changed, so the Player lock's pose
        /// alone does not decide the screen (<see cref="AndroidHalfOpenScreenLandscape"/>).
        /// </summary>
        public static bool AndroidHalfOpenCardChanged(FoldableProductState product, bool landscapeNative) =>
            ChangedAndroidHalfOpenCard(DeviceKind, product, landscapeNative, BoundFriendlyName) != null;

        /// <summary>
        /// True while a changed Android half-open card follows the device under the current Player
        /// Settings (e.g. Follow device under a one-axis lock): the Simulator rotates and every
        /// chrome turn is published, as the runtime hold allows both axes for that pose.
        /// </summary>
        public static bool AndroidHalfOpenFollowsDevice(FoldableProductState product, bool landscapeNative) =>
            AndroidHalfOpenCardChanged(product, landscapeNative) &&
            !AndroidHalfOpenScreenLandscape(product, landscapeNative).HasValue;

        /// <summary>
        /// Screen an Android Fold / Fold Cover / Flip Half Opened keeps while the Simulator is
        /// rotated, as its card in <see cref="FoldableSettings.Rules"/> decides: by default a Fold
        /// keeps the Player lock's pose (<see cref="AndroidHalfOpenScreenLandscape"/>) and follows
        /// rotation with both axes allowed; a Locked card keeps the screen it entered Half Opened
        /// with (<see cref="HalfOpenEntryLandscape"/>). A Flip is held only when its card is
        /// changed. Null for iPhone Duo and other postures.
        /// </summary>
        public static bool? AndroidHalfOpenHeldScreenLandscape(
            FoldableDeviceKind kind,
            FoldableProductState product,
            bool landscapeNative)
        {
            if (landscapeNative || product != FoldableProductState.HalfOpened)
                return null;
            bool isFold = kind == FoldableDeviceKind.FoldablePhone;
            if (!isFold && kind != FoldableDeviceKind.FlipablePhone)
                return null;

            var pose = AndroidHalfOpenCard(kind, BoundFriendlyName);
            if (!isFold && pose.IsDefault)
                return null;
            // A preset entering Half Opened keeps the screen it has now.
            bool entry = ProductState == FoldableProductState.HalfOpened ? HalfOpenEntryLandscape : IsLandscape;
            return FoldableHalfOpenRules.ScreenLandscape(
                pose, isFold, FoldablePlayerOrientation.GetLockedLandscape(), entry);
        }

        static readonly FoldableHalfOpenPose DefaultHalfOpenCard = new FoldableHalfOpenPose();

        /// <summary>
        /// The Android Fold / Fold Cover / Flip card for <paramref name="kind"/>; the platform
        /// default under a fixed Default Orientation or Auto Rotation with one orientation allowed,
        /// where the runtime hold never writes <c>Screen.orientation</c> (e.g. Landscape Left keeps
        /// the lock's pose whatever the card; <see cref="FoldablePlayerOrientation.AllowsAndroidHalfOpenHold"/>).
        /// </summary>
        static FoldableHalfOpenPose AndroidHalfOpenCard(FoldableDeviceKind kind, string friendlyName)
        {
            if (!FoldablePlayerOrientation.AllowsAndroidHalfOpenHold)
                return DefaultHalfOpenCard;
            bool isFold = kind == FoldableDeviceKind.FoldablePhone;
            return FoldableSettings.Rules.For(kind, isFold && IsFoldCoverDevice(friendlyName));
        }

        /// <summary>
        /// The catalog device's Half Opened inner is Fold Cover size (Fold8-style), so the Fold
        /// Cover card applies (<see cref="FoldableProductStateResolver.IsFoldCoverHalfOpenedSize"/>).
        /// </summary>
        static bool IsFoldCoverDevice(string friendlyName) =>
            ResolveScreenForProduct(friendlyName, FoldableProductState.HalfOpened, out var screen, out _) &&
            FoldableProductStateResolver.IsFoldCoverHalfOpenedSize?.Invoke(
                screen.PortraitWidth, screen.PortraitHeight) == true;

        /// <summary>
        /// Set by the iPhone Duo editor module: true while its Seated rotation rule is Locked, so a
        /// Simulator chrome turn does not change the Seated pose. Unset: the turn changes it.
        /// </summary>
        public static Func<bool> IosHalfOpenRotationLocked;

        /// <summary>
        /// Screen an iPhone Duo Seated keeps while the Simulator is rotated: the current screen
        /// <paramref name="landscape"/> while Half Opened with the rotation locked
        /// (<see cref="IosHalfOpenRotationLocked"/>), else null (the chrome turn is the screen).
        /// </summary>
        public static bool? IosHalfOpenHeldScreenLandscape(
            FoldableProductState product,
            bool rotationLocked,
            bool landscape) =>
            product == FoldableProductState.HalfOpened && rotationLocked ? landscape : (bool?)null;

        /// <summary>
        /// Set by the iPhone Duo editor module: true while its Locked Flat keeps a hold of its own, so
        /// a Simulator chrome turn does not change the Flat (Opened) screen. Unset: the turn changes it.
        /// </summary>
        public static Func<bool> IosFlatRotationLocked;

        /// <summary>
        /// Screen an iPhone Duo Flat keeps while the Simulator is rotated: the current screen
        /// <paramref name="landscape"/> while Opened with the rotation locked
        /// (<see cref="IosFlatRotationLocked"/>), else null (the chrome turn is the screen).
        /// </summary>
        public static bool? IosFlatHeldScreenLandscape(
            FoldableProductState product,
            bool rotationLocked,
            bool landscape) =>
            product == FoldableProductState.Opened && rotationLocked ? landscape : (bool?)null;

        /// <summary>
        /// Landscape-native inner Half Opened (iPhone Duo Seated): the hinge follows the screen
        /// shape (Portrait = tall laptop, horizontal fold; Landscape = wide book, vertical fold), so
        /// the panel never shows a stale hinge. Other devices and postures keep
        /// <paramref name="hinge"/>.
        /// </summary>
        public static FoldableOrientation LandscapeNativeHalfOpenHingeForPose(
            FoldableProductState product,
            bool landscape,
            bool innerLandscapeNative,
            FoldableOrientation hinge)
        {
            if (!FoldableProductStateResolver.IsLandscapeNativeHalfOpen(product, innerLandscapeNative))
                return hinge;

            return FoldableProductStateResolver.LandscapeNativeHalfOpenHinge(landscape);
        }

        public static FoldableLayoutState BuildLayout()
        {
            // Prefer live session metrics (already landscape-swapped by bridge sync).
            int width = MetricsWidth;
            int height = MetricsHeight;
            bool landscapeNative = BoundInnerLandscapeNative;

            if (ResolveScreenForProduct(BoundFriendlyName, ProductState, out var screen, out _))
            {
                screen.GetOrientedSize(IsLandscape, out width, out height);
            }

            var hinge = FoldableProductStateResolver.ToScreenSpaceHalfOpenHinge(
                DeviceKind, ProductState, Orientation, width, height, landscapeNative);
            var layout = FoldableLayoutState.CreateSimulated(
                State, hinge, width, height,
                HingeNormalized, HingeThicknessPx, IsSeparating);

            layout.deviceKind = DeviceKind;
            FoldableProductStateResolver.Enrich(layout, DeviceKind);
            // Session product state wins over CreateSimulated / Enrich inference.
            layout.productState = ProductState;
            layout.isCoverScreen = ProductState == FoldableProductState.Closed;
            return layout;
        }

        #endregion

        #region Binding

        public static void SetDeviceBinding(
            FoldableDeviceCatalog.Resolution resolution,
            Source source,
            bool applyDefaults)
        {
            if (resolution == null || !resolution.IsKnownFoldable || resolution.Device == null)
            {
                ClearDeviceBinding(source);
                return;
            }

            var device = resolution.Device;
            var screen = resolution.Screen;

            string friendly = device.FriendlyName ?? "";
            string model = device.DeviceModel ?? "";
            var kind = device.Kind;
            bool landscapeNative = device.HasLandscapeNativeInner;

            var draft = new BindingDraft
            {
                State = State,
                Orientation = Orientation,
                Hinge = HingeNormalized,
                Thickness = HingeThicknessPx,
                Separating = IsSeparating,
                Product = ProductState,
                Landscape = IsLandscape,
                Width = MetricsWidth,
                Height = MetricsHeight,
                ScreenName = screen?.Name ?? "",
                Cover = screen != null && screen.CoverOrOuter
            };

            if (applyDefaults)
                ApplyBindDefaults(resolution, landscapeNative, ref draft);
            else
                KeepPoseAcrossProfileSwitch(kind, friendly, landscapeNative, ref draft);

            // Duo Half Opened: the hinge follows the screen shape; a Fold ⇄ Duo switch keeps the
            // hold (above).
            draft.Orientation = LandscapeNativeHalfOpenHingeForPose(
                draft.Product, draft.Landscape, landscapeNative, draft.Orientation);
            // Android Fold Half Opened: the hinge follows the screen too (tall → horizontal).
            if (FoldableProductStateResolver.IsFoldHalfOpen(kind, draft.Product, landscapeNative))
                draft.Orientation = FoldableProductStateResolver.FoldHalfOpenHingeForScreen(draft.Landscape);

            // Phone or Fold → Flip: Opened (Main). Cover-first Simulator skins and a
            // leftover Fold Normal (Closed) must not stay Closed. Flip → Flip keeps
            // Closed / HalfOpened / Opened.
            if (kind == FoldableDeviceKind.FlipablePhone &&
                IsFoldableDeviceBound &&
                DeviceKind != FoldableDeviceKind.FlipablePhone)
            {
                ApplyFlipOpenedInner(friendly, ref draft);
            }

            // Always take Expand/Normal sizes from the catalog for the active product +
            // Portrait/Landscape. Never trust lagged Device Simulator Screen WxH here —
            // it can still be Cover or the previous orientation after a product switch.
            if (ResolveScreenForProduct(friendly, draft.Product, out var sized, out _))
            {
                sized.GetOrientedSize(draft.Landscape, out draft.Width, out draft.Height);
                draft.ScreenName = sized.Name ?? draft.ScreenName;
                draft.Cover = sized.CoverOrOuter || draft.Product == FoldableProductState.Closed;
            }
            else if (screen != null)
            {
                screen.GetOrientedSize(draft.Landscape, out draft.Width, out draft.Height);
            }
            else
            {
                FoldableProductStateResolver.ApplyOrientedMetrics(
                    draft.Landscape, ref draft.Width, ref draft.Height);
            }

            bool changed =
                !IsFoldableDeviceBound ||
                BoundFriendlyName != friendly ||
                BoundDeviceModel != model ||
                BoundScreenName != draft.ScreenName ||
                BoundScreenIsCover != draft.Cover ||
                DeviceKind != kind ||
                MetricsWidth != draft.Width ||
                MetricsHeight != draft.Height ||
                State != draft.State ||
                Orientation != draft.Orientation ||
                !Mathf.Approximately(HingeNormalized, draft.Hinge) ||
                HingeThicknessPx != draft.Thickness ||
                IsSeparating != draft.Separating ||
                ProductState != draft.Product ||
                IsLandscape != draft.Landscape;

            IsFoldableDeviceBound = true;
            BoundFriendlyName = friendly;
            BoundDeviceModel = model;
            BoundScreenName = draft.ScreenName;
            BoundScreenIsCover = draft.Cover;
            DeviceKind = kind;
            MetricsWidth = draft.Width;
            MetricsHeight = draft.Height;
            State = draft.State;
            Orientation = draft.Orientation;
            HingeNormalized = draft.Hinge;
            HingeThicknessPx = draft.Thickness;
            IsSeparating = draft.Separating;
            IsLandscape = draft.Landscape;
            ProductState = draft.Product;

            Persist();
            if (changed)
                Raise(source);
        }

        /// <summary>
        /// First bind: catalog defaults for the profile, keeping an explicit Half Opened / Closed.
        /// Screen orientation follows Player Settings (locked) or portrait when Auto Rotation
        /// allows both.
        /// </summary>
        static void ApplyBindDefaults(
            FoldableDeviceCatalog.Resolution resolution,
            bool landscapeNative,
            ref BindingDraft draft)
        {
            var device = resolution.Device;
            string friendly = device.FriendlyName ?? "";
            var kind = device.Kind;

            FoldableDeviceCatalog.ApplyDeviceDefaults(
                resolution,
                ref draft.State,
                ref draft.Orientation,
                ref draft.Hinge,
                ref draft.Thickness,
                ref draft.Separating);

            // Simulator can flicker unbound between ★ Fold / ★ Flip skins. Keep an
            // explicit HalfOpened / Closed instead of catalog Expand defaults.
            bool keepProduct = draft.Product == FoldableProductState.HalfOpened ||
                               draft.Product == FoldableProductState.Closed;

            if (keepProduct)
            {
                ApplyProductToRawSignals(draft.Product, ref draft.Separating, out draft.State);
            }
            // Dual: default to Spanned / Opened (Single pane looks like a narrow phone).
            else if (kind == FoldableDeviceKind.DualScreen &&
                device.Screens.Length > 1 &&
                ResolveScreenForProduct(friendly, FoldableProductState.Opened, out var spanned, out _))
            {
                draft.Product = FoldableProductState.Opened;
                draft.State = FoldableState.Flat;
                draft.Separating = true;
                draft.Orientation = FoldableOrientation.Vertical;
                spanned.GetOrientedSize(false, out draft.Width, out draft.Height);
                draft.ScreenName = spanned.Name ?? draft.ScreenName;
                draft.Cover = false;
            }
            else if (kind == FoldableDeviceKind.FoldablePhone &&
                     device.Screens.Length > 1)
            {
                // Honor live Cover screen (Normal). Play Mode rebuilds the Fold panel and
                // must not stomp Normal → Expand when Simulator is still on Cover.
                if (draft.Cover &&
                    ResolveScreenForProduct(
                        friendly, FoldableProductState.Closed, out var coverScreen, out _))
                {
                    draft.Product = FoldableProductState.Closed;
                    draft.State = FoldableState.Flat;
                    draft.Separating = false;
                    coverScreen.GetOrientedSize(false, out draft.Width, out draft.Height);
                    draft.ScreenName = coverScreen.Name ?? draft.ScreenName;
                    draft.Cover = true;
                }
                else if (ResolveScreenForProduct(
                             friendly, FoldableProductState.Opened, out var inner, out _))
                {
                    // First bind / Inner screen → Expand (inner flat).
                    // Not used when switching another Fold/Flip profile — those
                    // keep the Fold panel product state.
                    draft.Product = FoldableProductState.Opened;
                    draft.State = FoldableState.Flat;
                    draft.Separating = false;
                    inner.GetOrientedSize(false, out draft.Width, out draft.Height);
                    draft.ScreenName = inner.Name ?? draft.ScreenName;
                    draft.Cover = false;
                }
                else
                {
                    draft.Product = DeriveProductState(
                        kind, draft.Cover, draft.State, draft.Separating);
                }
            }
            else if (kind == FoldableDeviceKind.FlipablePhone)
            {
                // ★ Flip lists Cover first. Phone / first-bind must not treat that
                // as Closed — default Opened (Main).
                ApplyFlipOpenedInner(friendly, ref draft);
            }
            else
            {
                draft.Product = DeriveProductState(kind, draft.Cover, draft.State, draft.Separating);
            }

            if (draft.Product == FoldableProductState.HalfOpened)
            {
                // Android: landscape lock → horizontal tabletop. Duo: the hold, not this lock.
                var lockedHinge = FoldablePlayerOrientation.GetLockedHalfOpenHinge(
                    landscapeNative);
                if (lockedHinge.HasValue)
                    draft.Orientation = lockedHinge.Value;
            }

            draft.Landscape = FoldablePlayerOrientation.ResolveForProduct(
                kind, draft.Orientation, draft.Product, landscapeNative);
            bool? halfOpenScreen = AndroidHalfOpenScreenLandscape(
                draft.Product, landscapeNative, kind, friendly, draft.Landscape);
            if (halfOpenScreen.HasValue)
                draft.Landscape = halfOpenScreen.Value;
        }

        /// <summary>
        /// A profile switch keeps the pose unless the Player lock forbids it for the new
        /// profile. Half Opened across Fold ⇄ Duo keeps the hold instead (laptop or book), which
        /// the new profile's screen rule then sizes.
        /// </summary>
        static void KeepPoseAcrossProfileSwitch(
            FoldableDeviceKind kind,
            string friendly,
            bool landscapeNative,
            ref BindingDraft draft)
        {
            bool entryLandscape = draft.Landscape;
            bool? lockedLandscape = FoldablePlayerOrientation.GetLockedLandscapeForProduct(
                draft.Product, landscapeNative);
            if (lockedLandscape.HasValue)
                draft.Landscape = lockedLandscape.Value;
            else if (draft.Product == FoldableProductState.HalfOpened &&
                     IsFoldableDeviceBound &&
                     landscapeNative != BoundInnerLandscapeNative)
            {
                draft.Landscape = FoldablePlayerOrientation.ResolveForProduct(
                    kind, draft.Orientation, draft.Product, landscapeNative);
            }
            bool? halfOpenScreen = AndroidHalfOpenScreenLandscape(
                draft.Product, landscapeNative, kind, friendly, entryLandscape);
            if (halfOpenScreen.HasValue)
                draft.Landscape = halfOpenScreen.Value;
            if (draft.Product == FoldableProductState.HalfOpened)
            {
                var lockedHinge = FoldablePlayerOrientation.GetLockedHalfOpenHinge(landscapeNative);
                if (lockedHinge.HasValue)
                    draft.Orientation = lockedHinge.Value;
            }
        }

        public static void ClearDeviceBinding(Source source)
        {
            if (!IsFoldableDeviceBound)
                return;

            ClearBindingFields();
            Persist();
            Raise(source);
        }

        /// <summary>
        /// Phone / Pad (not ★ Fold / ★ Flip) — next Fold bind is a fresh Expand, not a leftover Flex.
        /// Transient invalid Simulator selection must not call this.
        /// </summary>
        public static void ResetProductDefaults()
        {
            State = FoldableState.Flat;
            ProductState = FoldableProductState.Opened;
            IsSeparating = false;
            BoundScreenIsCover = false;
            Orientation = FoldableOrientation.Horizontal;
            HingeNormalized = 0.5f;
            Persist();
        }

        #endregion

        #region Publish

        public static void PublishProductState(
            FoldableProductState product,
            FoldableOrientation orientation,
            float hingeNormalized,
            int hingeThicknessPx,
            bool isSeparating,
            int metricsWidth,
            int metricsHeight,
            Source source,
            bool? landscape = null)
        {
            ApplyProductToRawSignals(product, ref isSeparating, out var state);
            bool landscapeNative = BoundInnerLandscapeNative;

            bool? halfOpenScreen = AndroidHalfOpenScreenLandscape(product, landscapeNative);
            if (halfOpenScreen.HasValue)
                IsLandscape = halfOpenScreen.Value;
            else if (landscape.HasValue)
                IsLandscape = landscape.Value;
            else if (product == FoldableProductState.HalfOpened ||
                     FoldablePlayerOrientation.GetLockedLandscapeForProduct(
                         product, landscapeNative).HasValue)
            {
                IsLandscape = FoldablePlayerOrientation.ResolveForProduct(
                    DeviceKind, orientation, product, landscapeNative);
            }

            // Closed → cover/outer size; Opened / HalfOpened → inner/main size.
            // Always prefer catalog sizes for the bound device so UI never keeps
            // stale Cover WxH after switching to Expand (or vice versa).
            if (ResolveScreenForProduct(BoundFriendlyName, product, out var screen, out _))
            {
                BoundScreenName = screen.Name ?? BoundScreenName;
                BoundScreenIsCover = screen.CoverOrOuter || product == FoldableProductState.Closed;
                screen.GetOrientedSize(IsLandscape, out metricsWidth, out metricsHeight);
            }
            else
            {
                BoundScreenIsCover = product == FoldableProductState.Closed;
                FoldableProductStateResolver.ApplyOrientedMetrics(
                    IsLandscape,
                    ref metricsWidth,
                    ref metricsHeight);
            }

            if (product == FoldableProductState.HalfOpened && landscapeNative)
            {
                // iPhone Duo only: the screen shape picks the hinge (tall = laptop, wide = book).
                orientation = LandscapeNativeHalfOpenHingeForPose(
                    product, IsLandscape, innerLandscapeNative: true, orientation);
            }
            else if (FoldableProductStateResolver.IsFoldHalfOpen(DeviceKind, product, innerLandscapeNative: false))
            {
                // Android Fold / Fold Cover: the crease follows the held screen (tall Landscape →
                // horizontal, wide Portrait → vertical); a Player lock fixes both the same way.
                orientation = FoldableProductStateResolver.FoldHalfOpenHingeForScreen(IsLandscape);
            }
            else if (product == FoldableProductState.HalfOpened)
            {
                // a5563fec1: Flip keeps the locked axis (landscape → horizontal tabletop).
                FoldableOrientation? lockedHinge =
                    FoldablePlayerOrientation.GetLockedHalfOpenHinge(innerLandscapeNative: false);
                if (lockedHinge.HasValue)
                    orientation = lockedHinge.Value;
            }

            PublishFold(
                state, orientation, hingeNormalized, hingeThicknessPx, isSeparating,
                metricsWidth, metricsHeight, source, product);
        }

        /// <summary>
        /// Android Fold / Fold Cover / Flip screen change. The hinge stays where it is, except a
        /// Fold Half Opened, whose crease follows the screen (tall → horizontal, wide → vertical).
        /// </summary>
        public static void PublishAndroidScreen(bool landscape, Source source)
        {
            FoldableOrientation orientation =
                FoldableProductStateResolver.IsFoldHalfOpen(DeviceKind, ProductState, innerLandscapeNative: false)
                    ? FoldableProductStateResolver.FoldHalfOpenHingeForScreen(landscape)
                    : Orientation;
            PublishScreen(landscape, orientation, source);
        }

        /// <summary>
        /// iPhone Duo screen change. Seated moves the crease with the screen
        /// (tall laptop = horizontal, wide book = vertical).
        /// </summary>
        public static void PublishIosScreen(bool landscape, Source source)
        {
            FoldableOrientation orientation = ProductState == FoldableProductState.HalfOpened
                ? FoldableProductStateResolver.LandscapeNativeHalfOpenHinge(landscape)
                : Orientation;
            PublishScreen(landscape, orientation, source);
        }

        public static void PublishLandscape(bool landscape, Source source)
        {
            if (BoundInnerLandscapeNative)
                PublishIosScreen(landscape, source);
            else
                PublishAndroidScreen(landscape, source);
        }

        static void PublishScreen(bool landscape, FoldableOrientation orientation, Source source)
        {
            if (IsLandscape == landscape && Orientation == orientation)
                return;

            IsLandscape = landscape;
            int w = MetricsWidth;
            int h = MetricsHeight;

            if (ResolveScreenForProduct(BoundFriendlyName, ProductState, out var screen, out _))
            {
                screen.GetOrientedSize(IsLandscape, out w, out h);
            }
            else
            {
                // Restore portrait pair then re-orient.
                if (w > h)
                {
                    int t = w;
                    w = h;
                    h = t;
                }

                FoldableProductStateResolver.ApplyOrientedMetrics(
                    IsLandscape, ref w, ref h);
            }

            PublishFold(
                State, orientation, HingeNormalized, HingeThicknessPx, IsSeparating,
                w, h, source, ProductState);
        }

        /// <summary>
        /// Picks the catalog screen (and WxH) for a product state on a toolkit device.
        /// </summary>
        public static bool ResolveScreenForProduct(
            string friendlyName,
            FoldableProductState product,
            out FoldableDeviceCatalog.ScreenProfile screen,
            out int screenIndex)
        {
            screen = null;
            screenIndex = 0;

            var device = FindDeviceByFriendlyName(friendlyName);
            if (device?.Screens == null || device.Screens.Length == 0)
                return false;

            bool preferInner = product != FoldableProductState.Closed;
            screenIndex = PreferredScreenIndex(device, preferInner);
            screen = device.Screens[screenIndex];
            return screen != null;
        }

        public static void PublishFold(
            FoldableState state,
            FoldableOrientation orientation,
            float hingeNormalized,
            int hingeThicknessPx,
            bool isSeparating,
            int metricsWidth,
            int metricsHeight,
            Source source,
            FoldableProductState? productOverride = null)
        {
            if (state == FoldableState.Undefined)
                state = FoldableState.Flat;
            if (orientation == FoldableOrientation.Undefined)
                orientation = FoldableOrientation.Horizontal;

            hingeNormalized = Mathf.Clamp(hingeNormalized, 0.05f, 0.95f);
            hingeThicknessPx = Mathf.Max(1, hingeThicknessPx);
            metricsWidth = Mathf.Max(1, metricsWidth);
            metricsHeight = Mathf.Max(1, metricsHeight);

            if (state == FoldableState.HalfOpened)
                BoundScreenIsCover = false;

            var product = productOverride ?? DeriveProductState(
                DeviceKind,
                BoundScreenIsCover && state != FoldableState.HalfOpened,
                state,
                isSeparating);

            // Cover / Normal never has a dual-pane hinge — keep panel + layout honest.
            if (product == FoldableProductState.Closed)
                isSeparating = false;

            bool changed =
                State != state ||
                Orientation != orientation ||
                !Mathf.Approximately(HingeNormalized, hingeNormalized) ||
                HingeThicknessPx != hingeThicknessPx ||
                IsSeparating != isSeparating ||
                MetricsWidth != metricsWidth ||
                MetricsHeight != metricsHeight ||
                ProductState != product;

            State = state;
            Orientation = orientation;
            HingeNormalized = hingeNormalized;
            HingeThicknessPx = hingeThicknessPx;
            IsSeparating = isSeparating;
            MetricsWidth = metricsWidth;
            MetricsHeight = metricsHeight;
            ProductState = product;

            Persist();
            if (changed)
                Raise(source);
        }

        #endregion

        #region Persist

        const string PersistPrefix = "FDPlus.Foldable.EditorSession.";

        static FoldableEditorSession()
        {
            RestorePersisted();
            FoldableDeviceCatalog.CatalogChanged += () => DropStaleCatalogBinding(notify: true);
        }

        public static void FlushPersistedSession() => Persist();

        /// <summary>
        /// Play Mode domain-reloads Editor statics. Re-apply the last Fold panel snapshot
        /// so Half Opened is not treated as a first bind (catalog Expand).
        /// </summary>
        internal static void SimulateDomainReloadForTests()
        {
            ResetStaticsToDefaults();
            RestorePersisted();
        }

        static void Persist()
        {
            SessionState.SetBool(PersistPrefix + "HasSnapshot", true);
            SessionState.SetBool(PersistPrefix + "Bound", IsFoldableDeviceBound);
            SessionState.SetString(PersistPrefix + "Friendly", BoundFriendlyName ?? "");
            SessionState.SetString(PersistPrefix + "Model", BoundDeviceModel ?? "");
            SessionState.SetString(PersistPrefix + "Screen", BoundScreenName ?? "");
            SessionState.SetBool(PersistPrefix + "Cover", BoundScreenIsCover);
            SessionState.SetInt(PersistPrefix + "Kind", (int)DeviceKind);
            SessionState.SetInt(PersistPrefix + "State", (int)State);
            SessionState.SetInt(PersistPrefix + "Orientation", (int)Orientation);
            SessionState.SetFloat(PersistPrefix + "Hinge", HingeNormalized);
            SessionState.SetInt(PersistPrefix + "Thickness", HingeThicknessPx);
            SessionState.SetBool(PersistPrefix + "Separating", IsSeparating);
            SessionState.SetInt(PersistPrefix + "Width", MetricsWidth);
            SessionState.SetInt(PersistPrefix + "Height", MetricsHeight);
            SessionState.SetInt(PersistPrefix + "Product", (int)ProductState);
            SessionState.SetBool(PersistPrefix + "Landscape", IsLandscape);
            SessionState.SetBool(PersistPrefix + "HalfOpenEntryLandscape", HalfOpenEntryLandscape);
            CommitPlayLayout();
        }

        static void RestorePersisted()
        {
            if (!SessionState.GetBool(PersistPrefix + "HasSnapshot", false))
                return;

            IsFoldableDeviceBound = SessionState.GetBool(PersistPrefix + "Bound", false);
            BoundFriendlyName = SessionState.GetString(PersistPrefix + "Friendly", "");
            BoundDeviceModel = SessionState.GetString(PersistPrefix + "Model", "");
            BoundScreenName = SessionState.GetString(PersistPrefix + "Screen", "");
            BoundScreenIsCover = SessionState.GetBool(PersistPrefix + "Cover", false);
            DeviceKind = (FoldableDeviceKind)SessionState.GetInt(
                PersistPrefix + "Kind", (int)FoldableDeviceKind.Unknown);
            State = (FoldableState)SessionState.GetInt(
                PersistPrefix + "State", (int)FoldableState.Flat);
            Orientation = (FoldableOrientation)SessionState.GetInt(
                PersistPrefix + "Orientation", (int)FoldableOrientation.Horizontal);
            HingeNormalized = SessionState.GetFloat(PersistPrefix + "Hinge", 0.5f);
            HingeThicknessPx = SessionState.GetInt(PersistPrefix + "Thickness", 24);
            IsSeparating = SessionState.GetBool(PersistPrefix + "Separating", false);
            MetricsWidth = Mathf.Max(1, SessionState.GetInt(PersistPrefix + "Width", 1080));
            MetricsHeight = Mathf.Max(1, SessionState.GetInt(PersistPrefix + "Height", 2316));
            ProductState = (FoldableProductState)SessionState.GetInt(
                PersistPrefix + "Product", (int)FoldableProductState.Opened);
            IsLandscape = SessionState.GetBool(PersistPrefix + "Landscape", false);
            HalfOpenEntryLandscape = SessionState.GetBool(
                PersistPrefix + "HalfOpenEntryLandscape", IsLandscape);

            if (IsFoldableDeviceBound)
                FoldableSimulationGate.AllowSimulatedLayouts = true;

            DropStaleCatalogBinding(notify: false);
            CommitPlayLayout();
        }

        /// <summary>
        /// Dual/Generic (and any other deleted skin) must not stay bound after the
        /// catalog JSON is gone. Restore and catalog reload both drop that persist.
        /// </summary>
        static void DropStaleCatalogBinding(bool notify)
        {
            if (!IsFoldableDeviceBound)
                return;
            if (FindDeviceByFriendlyName(BoundFriendlyName) != null)
                return;

            ClearBindingFields();
            FoldableSimulationGate.AllowSimulatedLayouts = false;
            Persist();
            CommitPlayLayout();
            if (notify)
                Raise(Source.Bridge);
        }

        static void CommitPlayLayout()
        {
            if (!IsFoldableDeviceBound)
            {
                FoldableSimulationGate.ClearPlayLayout();
                return;
            }

            FoldableSimulationGate.SetPlayLayout(BuildLayout());
        }

        static void ResetStaticsToDefaults()
        {
            ClearBindingFields();
            State = FoldableState.Flat;
            Orientation = FoldableOrientation.Horizontal;
            HingeNormalized = 0.5f;
            HingeThicknessPx = 24;
            IsSeparating = false;
            MetricsWidth = 1080;
            MetricsHeight = 2316;
            ProductState = FoldableProductState.Opened;
            IsLandscape = false;
            HalfOpenEntryLandscape = false;
            FoldableSimulationGate.ClearPlayLayout();
        }

        #endregion

        #region Helpers

        /// <summary>Binding values <see cref="SetDeviceBinding"/> resolves before committing.</summary>
        struct BindingDraft
        {
            public FoldableState State;
            public FoldableOrientation Orientation;
            public float Hinge;
            public int Thickness;
            public bool Separating;
            public FoldableProductState Product;
            public bool Landscape;
            public int Width;
            public int Height;
            public string ScreenName;
            public bool Cover;
        }

        /// <summary>Unbound: device identity cleared; posture fields are left to the caller.</summary>
        static void ClearBindingFields()
        {
            IsFoldableDeviceBound = false;
            BoundFriendlyName = "";
            BoundDeviceModel = "";
            BoundScreenName = "";
            BoundScreenIsCover = false;
            DeviceKind = FoldableDeviceKind.Unknown;
        }

        static void ApplyFlipOpenedInner(string friendlyName, ref BindingDraft draft)
        {
            draft.Product = FoldableProductState.Opened;
            draft.State = FoldableState.Flat;
            draft.Separating = false;
            draft.Cover = false;
            if (!ResolveScreenForProduct(
                    friendlyName, FoldableProductState.Opened, out var main, out _))
                return;

            main.GetOrientedSize(false, out draft.Width, out draft.Height);
            draft.ScreenName = main.Name ?? draft.ScreenName;
        }

        static void ApplyProductToRawSignals(
            FoldableProductState product,
            ref bool isSeparating,
            out FoldableState state)
        {
            switch (product)
            {
                case FoldableProductState.Closed:
                    state = FoldableState.Flat;
                    isSeparating = false;
                    break;
                case FoldableProductState.HalfOpened:
                    state = FoldableState.HalfOpened;
                    // Keep the panel toggle — Flex still has a physical hinge (isSeparating).
                    break;
                default:
                    // Opened / Expand: dual-screen spanned; Fold Expand clears HalfOpened hinge.
                    state = FoldableState.Flat;
                    isSeparating = DeviceKind == FoldableDeviceKind.DualScreen;
                    break;
            }
        }

        static FoldableProductState DeriveProductState(
            FoldableDeviceKind kind,
            bool cover,
            FoldableState state,
            bool separating)
        {
            return FoldableProductStateResolver.Resolve(new FoldableLayoutState
            {
                deviceKind = kind,
                isCoverScreen = cover,
                state = state,
                isSeparating = separating
            });
        }

        /// <summary>
        /// Raises <see cref="Changed"/>. A change published by a handler during the dispatch is
        /// raised once more after it (latest source), not dropped; handlers are isolated so one
        /// throwing does not stop the rest.
        /// </summary>
        static void Raise(Source source)
        {
            if (suppressDepth > 0)
            {
                nestedChangePending = true;
                nestedChangeSource = source;
                return;
            }

            suppressDepth++;
            try
            {
                InvokeChanged(source);
                if (nestedChangePending)
                {
                    nestedChangePending = false;
                    InvokeChanged(nestedChangeSource);
                }
            }
            finally
            {
                nestedChangePending = false;
                suppressDepth--;
            }
        }

        static void InvokeChanged(Source source)
        {
            Action<Source> handlers = Changed;
            if (handlers == null)
                return;

            Delegate[] subscribers = handlers.GetInvocationList();
            for (int i = 0; i < subscribers.Length; i++)
            {
                try
                {
                    ((Action<Source>)subscribers[i]).Invoke(source);
                }
                catch (Exception e)
                {
                    Debug.LogException(e, subscribers[i].Target as UnityEngine.Object);
                }
            }
        }

        /// <summary>
        /// After <c>SyncFromSession</c> switches Cover→Main. Not folded into
        /// <see cref="Changed"/> because first-bind metrics are already Main.
        /// </summary>
        public static void PublishScreenSynced()
        {
            ScreenSynced?.Invoke();
        }

        public static FoldableDeviceCatalog.DeviceProfile FindDeviceByFriendlyName(string friendlyName)
        {
            foreach (var device in FoldableDeviceCatalog.Devices)
            {
                if (FoldableDeviceCatalog.NamesMatch(device.FriendlyName, friendlyName) ||
                    FoldableDeviceCatalog.NamesMatch(device.ShortName, friendlyName))
                    return device;
            }

            return null;
        }

        public static int PreferredScreenIndex(FoldableDeviceCatalog.DeviceProfile device, bool preferInner)
        {
            if (device?.Screens == null || device.Screens.Length == 0)
                return 0;

            if (!preferInner)
            {
                for (int i = 0; i < device.Screens.Length; i++)
                {
                    if (device.Screens[i].CoverOrOuter)
                        return i;
                }

                return 0;
            }

            for (int i = 0; i < device.Screens.Length; i++)
            {
                if (!device.Screens[i].CoverOrOuter)
                    return i;
            }

            return 0;
        }

        #endregion
    }
}
