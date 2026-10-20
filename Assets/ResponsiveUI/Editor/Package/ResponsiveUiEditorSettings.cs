#if UNITY_EDITOR
using System.Collections.Generic;
using FDPlus.Foldable;
using FDPlus.Foldable.Editor;
using FDPlus.PhonePad.Editor;
using FDPlus.ResponsiveUI;
using FDPlus.ResponsiveUI.Shared.Editor;
using UnityEditor;
using UnityEngine;

namespace FDPlus.ResponsiveUI.Editor
{
    /// <summary>
    /// Project-wide editor settings for Responsive UI (Edit → Project Settings → Responsive UI).
    /// Portrait/Landscape slot visibility follows Player Allowed Orientations.
    /// Design-scale defaults (model name, canvas size, scale) apply to new / unset slots.
    /// Background cover size is picked from the same device catalogs (or Follow a Design Canvas reference).
    /// Pick any catalog device as the reference profile per form factor (Wide Screen has none:
    /// its design canvas is edited directly).
    /// </summary>
    [FilePath("ProjectSettings/ResponsiveUiSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    public class ResponsiveUiEditorSettings : ScriptableSingleton<ResponsiveUiEditorSettings>
    {
        #region Constants

        static readonly ResponsiveDevice.FormFactor[] DesignForms =
            ResponsiveWidgetLayout.FormFactors;

        #endregion

        #region Serialized

        [SerializeField]
        [Tooltip("When on, ResponsiveRectState Enabled checkboxes are read-only in the Inspector.")]
        bool lockEnabledToggles = true;

        [SerializeField]
        DesignFormDefaults phone =
            DesignFormDefaults.CreateBuiltin(ResponsiveDevice.FormFactor.Phone);
        [SerializeField]
        DesignFormDefaults pad =
            DesignFormDefaults.CreateBuiltin(ResponsiveDevice.FormFactor.Pad);
        [SerializeField]
        DesignFormDefaults foldPhoneCover =
            DesignFormDefaults.CreateBuiltin(ResponsiveDevice.FormFactor.FoldPhoneCover);
        [SerializeField]
        DesignFormDefaults fold =
            DesignFormDefaults.CreateBuiltin(ResponsiveDevice.FormFactor.FoldHalfOpened);
        [SerializeField]
        DesignFormDefaults foldCoverHalfOpened =
            DesignFormDefaults.CreateBuiltin(ResponsiveDevice.FormFactor.FoldCoverHalfOpened);
        [SerializeField]
        DesignFormDefaults flipPhoneCover =
            DesignFormDefaults.CreateBuiltin(ResponsiveDevice.FormFactor.FlipPhoneCover);
        [SerializeField]
        DesignFormDefaults flip =
            DesignFormDefaults.CreateBuiltin(ResponsiveDevice.FormFactor.FlipHalfOpened);
        [SerializeField]
        DesignFormDefaults iPhoneDuoCover =
            DesignFormDefaults.CreateBuiltin(ResponsiveDevice.FormFactor.iPhoneDuoCover);
        [SerializeField]
        DesignFormDefaults iPhoneDuoSeated =
            DesignFormDefaults.CreateBuiltin(ResponsiveDevice.FormFactor.iPhoneDuoSeated);
        [SerializeField]
        DesignFormDefaults wideScreen =
            DesignFormDefaults.CreateBuiltin(ResponsiveDevice.FormFactor.WideScreen);

        [SerializeField]
        BackgroundArtDefaults backgroundArt = BackgroundArtDefaults.CreateBuiltin();

        [SerializeField]
        bool showHalfOpenedHingeGuide = true;
        [SerializeField]
        Color hingeGuideColor = ResponsiveUiHingeGuide.DefaultGuideColor;
        [SerializeField]
        float hingeGuideThicknessPx = ResponsiveUiHingeGuide.DefaultThicknessPx;

        public static bool LockEnabledToggles => instance.lockEnabledToggles;
        public static bool ShowHalfOpenedHingeGuide => instance.showHalfOpenedHingeGuide;
        public static Color HingeGuideColor => instance.hingeGuideColor;
        public static float HingeGuideThicknessPx => instance.hingeGuideThicknessPx;

        #endregion

        #region Types

        [System.Serializable]
        public struct DesignFormDefaults
        {
            /// <summary>
            /// Catalog profile id (PhonePad or FoldAndFlip). Empty sanitizes to the preferred device;
            /// always empty for Wide Screen (no catalog device).
            /// </summary>
            public string profileId;
            public string modelName;
            public Vector2 designCanvasSize;
            public Vector2 defaultScale;
            /// <summary>
            /// Landscape slot row's design canvas. (0,0) = <see cref="designCanvasSize"/> rotated
            /// (today's behaviour; assets saved before this field read (0,0)).
            /// </summary>
            public Vector2 landscapeDesignCanvasSize;

            public static DesignFormDefaults CreateBuiltin(ResponsiveDevice.FormFactor form) =>
                new DesignFormDefaults
                {
                    profileId = GetPreferredProfileId(form),
                    modelName = ResponsiveRectState.GetBuiltinModelName(form),
                    designCanvasSize = ResponsiveRectState.GetBuiltinDesignCanvasSize(form),
                    defaultScale = ResponsiveRectState.BuiltinDefaultScale,
                };
        }

        [System.Serializable]
        public struct BackgroundArtDefaults
        {
            public ResponsiveDevice.FormFactor form;
            public bool followDesignCanvas;
            public string profileId;
            public string modelName;
            public ResponsiveDevice.Orientation orientation;
            public Vector2 size;

            public static BackgroundArtDefaults CreateBuiltin() =>
                new BackgroundArtDefaults
                {
                    form = PreferredBackgroundForm,
                    followDesignCanvas = false,
                    profileId = PreferredBackgroundProfileId,
                    modelName = ResponsiveRectState.BuiltinPadModelName,
                    orientation = PreferredBackgroundOrientation,
                    size = ResponsiveUI.OrientSize(
                        ResponsiveRectState.BuiltinPadDesignCanvasSize,
                        PreferredBackgroundOrientation),
                };
        }

        #endregion

        #region Providers

        [InitializeOnLoadMethod]
        static void BindDesignDefaultProviders()
        {
            instance.EnsureDesignDefaultsInitialized();
            ResponsiveRectState.DesignCanvasSizeProvider = GetDesignCanvasSize;
            ResponsiveRectState.FlipCoverSizeProvider = GetFlipCoverSize;
            ResponsiveRectState.DefaultScaleProvider = GetDefaultScale;
            ResponsiveUI.OriginalBgSizeProvider = GetBackgroundArtSize;
            ResponsivePoseCanvasHook.Get = (form, landscape) =>
                GetDesignCanvasSize((ResponsiveDevice.FormFactor)form, SlotOrientation(landscape));
            ResponsivePoseCanvasHook.Set = (form, landscape, size) =>
                instance.SetDesignCanvasSize(
                    (ResponsiveDevice.FormFactor)form, SlotOrientation(landscape), size);
            ResponsivePoseCanvasHook.IsFromReference = (form, landscape) =>
                !landscape && DesignCanvasFromReference((ResponsiveDevice.FormFactor)form);

            PhonePadDeviceCatalog.IsDesignCanvasReferenceId = IsPhonePadDesignReference;
            FoldableDeviceCatalog.IsDesignCanvasReferenceId = IsFoldableDesignReference;
            // Fold vs Fold Cover half-open card: the same size rule that maps Fold Cover Half Opened.
            FoldableProductStateResolver.IsFoldCoverHalfOpenedSize = (width, height) =>
                ResponsiveDevice.IsKnownFoldCoverHalfOpenedSize(width, height);

            EditorApplication.delayCall += () =>
            {
                if (Application.isBatchMode)
                    return;
                instance.BindPreferredCatalogProfiles();
                // Retargets (e.g. Fold → Fold7) update Project Settings without a
                // Settings UI edit — re-enable + re-label Design Canvas Simulator skins.
                EnsureReferenceSimulatorSkinsEnabled();
            };
        }

        #endregion

        #region Catalog bind

        static readonly (ResponsiveDevice.FormFactor Form, string ProfileId)[] PreferredCatalogProfiles =
        {
        (ResponsiveDevice.FormFactor.Phone, "iphone-17-pro-max"),
        (ResponsiveDevice.FormFactor.FoldPhoneCover, "galaxy-z-fold8-like"),
        (ResponsiveDevice.FormFactor.FlipPhoneCover, "galaxy-z-flip8-like"),
        (ResponsiveDevice.FormFactor.Pad, "ipad-pro-13-m5"),
        (ResponsiveDevice.FormFactor.FoldHalfOpened, "galaxy-z-fold7-like"),
        (ResponsiveDevice.FormFactor.FoldCoverHalfOpened, "galaxy-z-fold8-like"),
        (ResponsiveDevice.FormFactor.FlipHalfOpened, "galaxy-z-flip8-like"),
        (ResponsiveDevice.FormFactor.iPhoneDuoCover, "iphone-duo-like"),
        (ResponsiveDevice.FormFactor.iPhoneDuoSeated, "iphone-duo-like"),
    };

        const string PreferredBackgroundProfileId = "ipad-pro-13-m5";
        const ResponsiveDevice.FormFactor PreferredBackgroundForm = ResponsiveDevice.FormFactor.Pad;
        const ResponsiveDevice.Orientation PreferredBackgroundOrientation =
            ResponsiveDevice.Orientation.Landscape;

        public static string GetPreferredProfileId(ResponsiveDevice.FormFactor form)
        {
            if (!HasCatalogReference(form))
                return "";
            for (int i = 0; i < PreferredCatalogProfiles.Length; i++)
            {
                if (PreferredCatalogProfiles[i].Form == form)
                    return PreferredCatalogProfiles[i].ProfileId;
            }

            return PreferredBackgroundProfileId;
        }

        /// <summary>
        /// Phone and Pad bind to the Phone/Pad catalog; every other design form binds to
        /// the Fold/Flip/Dual catalog.
        /// </summary>
        internal static bool IsPhonePadForm(ResponsiveDevice.FormFactor form) =>
            form == ResponsiveDevice.FormFactor.Phone || form == ResponsiveDevice.FormFactor.Pad;

        /// <summary>
        /// False for Wide Screen: it has no catalog device; its design canvas is edited in
        /// Project Settings (built-in 1080×1920 portrait-stored).
        /// </summary>
        internal static bool HasCatalogReference(ResponsiveDevice.FormFactor form) =>
            form != ResponsiveDevice.FormFactor.WideScreen;

        /// <summary>
        /// "Phone / Pad / … / Wide Screen" in widget slot order, for help and tooltip text, so a
        /// new form factor does not need hand-edited lists. Not cached: names follow the build
        /// target (iOS reads iPhone / iPad).
        /// </summary>
        public static string FormDisplayNameList => JoinFormDisplayNames(visibleOnly: false);

        /// <summary><see cref="FormDisplayNameList"/> limited to the build target's forms (<see cref="FormVisible"/>).</summary>
        public static string VisibleFormDisplayNameList => JoinFormDisplayNames(visibleOnly: true);

        static string JoinFormDisplayNames(bool visibleOnly)
        {
            var names = new List<string>(DesignForms.Length);
            for (int i = 0; i < DesignForms.Length; i++)
            {
                if (!visibleOnly || FormVisible(DesignForms[i]))
                    names.Add(ResponsiveDevice.GetFormDisplayName(DesignForms[i]));
            }

            return string.Join(" / ", names);
        }

        static bool IsPhonePadDesignReference(string profileId) =>
            IsDesignReference(profileId, phonePad: true);

        static bool IsFoldableDesignReference(string profileId) =>
            IsDesignReference(profileId, phonePad: false);

        static bool IsDesignReference(string profileId, bool phonePad)
        {
            if (string.IsNullOrEmpty(profileId))
                return false;
            for (int i = 0; i < DesignForms.Length; i++)
            {
                ResponsiveDevice.FormFactor form = DesignForms[i];
                if (IsPhonePadForm(form) != phonePad || !HasCatalogReference(form))
                    continue;
                if (string.Equals(
                        profileId,
                        GetFormDefaults(form).profileId,
                        System.StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        /// <summary>True when this catalog profile is a Project Settings Design Canvas reference.</summary>
        public static bool IsDesignReferenceProfileId(string profileId) =>
            IsPhonePadDesignReference(profileId) || IsFoldableDesignReference(profileId);

        /// <summary>
        /// Keeps Design Canvas reference devices enabled in Device Simulator and rewrites
        /// friendly names whenever Project Settings references change (Flip / Fold Cover /
        /// Fold role labels + sort-to-top for references; ★ Fold/Flip/Dual or ● Phone/Pad
        /// otherwise). Patched <c>.device</c> files are Refresh'd so Simulator imports update.
        /// </summary>
        public static void EnsureReferenceSimulatorSkinsEnabled()
        {
            if (!PhonePadDeviceCatalog.EnsureDesignReferenceSkinsEnabled(
                    out string phoneError, out bool skinsChanged))
            {
                if (!string.IsNullOrEmpty(phoneError))
                    Debug.LogWarning("[Responsive UI] " + phoneError);
            }

            int labelsWritten = PhonePadDeviceCatalog.SyncAllActiveSimulatorFriendlyNames();
            skinsChanged |= labelsWritten > 0;

            for (int i = 0; i < DesignForms.Length; i++)
            {
                ResponsiveDevice.FormFactor form = DesignForms[i];
                if (IsPhonePadForm(form) || !HasCatalogReference(form))
                    continue;

                string id = GetFormDefaults(form).profileId;
                if (string.IsNullOrEmpty(id))
                    continue;
                // SetSimulatorEnabled Refresh's when it moves .device.off → .device.
                // Already-enabled refs return true without a file change — do not treat
                // that as skinsChanged or Phone/Pad-only Refresh is skipped wrongly later.
                if (FoldableDeviceCatalog.SetSimulatorEnabled(id, true, out string foldError))
                    continue;
                if (!string.IsNullOrEmpty(foldError))
                    Debug.LogWarning("[Responsive UI] " + foldError);
            }

            // Foldable label sync Refresh's when it patches. Phone/Pad Refresh only when
            // those skins/labels changed — otherwise a Fold-only reference change never
            // reimported .device assets and Device Simulator kept the old list.
            int foldLabels = FoldableDeviceCatalog.SyncDesignReferenceSimulatorLabels();
            if (skinsChanged && foldLabels == 0)
            {
                AssetDatabase.Refresh();
                PhonePadDeviceCatalog.Reload();
                PhonePadSimulatorListUtil.ForceReloadDeviceLists();
            }
        }

        void BindPreferredCatalogProfiles()
        {
            bool dirty = false;
            for (int i = 0; i < PreferredCatalogProfiles.Length; i++)
            {
                ResponsiveDevice.FormFactor form = PreferredCatalogProfiles[i].Form;
                ref DesignFormDefaults slot = ref FormDefaultsRef(form);
                if (!string.IsNullOrEmpty(slot.profileId))
                    continue;

                string preferredId = PreferredCatalogProfiles[i].ProfileId;
                if (!ResolveCatalogCanvas(form, preferredId, out string modelName, out Vector2 canvas, out _))
                    continue;

                slot = new DesignFormDefaults
                {
                    profileId = preferredId,
                    modelName = modelName,
                    designCanvasSize = canvas,
                    defaultScale = slot.defaultScale.sqrMagnitude > 0f
                        ? slot.defaultScale
                        : ResponsiveRectState.BuiltinDefaultScale,
                };
                dirty = true;
            }

            dirty |= BindPreferredBackgroundProfile();

            if (dirty)
            {
                Save(true);
                ResponsiveUiBackgroundSizeWatch.NotifyChanged();
            }
        }

        bool BindPreferredBackgroundProfile()
        {
            if (backgroundArt.followDesignCanvas)
                return false;
            if (!string.IsNullOrEmpty(backgroundArt.profileId) &&
                ResolveCatalogSize(
                    backgroundArt.form,
                    backgroundArt.profileId,
                    backgroundArt.orientation,
                    out string boundName,
                    out Vector2 boundSize,
                    out _))
            {
                if (boundName == backgroundArt.modelName && boundSize == backgroundArt.size)
                    return false;

                backgroundArt.modelName = boundName;
                backgroundArt.size = boundSize;
                return true;
            }

            string preferredId = GetPreferredProfileId(backgroundArt.form);
            if (ResolveCatalogSize(
                    backgroundArt.form,
                    preferredId,
                    backgroundArt.orientation,
                    out string modelName,
                    out Vector2 size,
                    out _))
            {
                backgroundArt = new BackgroundArtDefaults
                {
                    form = backgroundArt.form,
                    followDesignCanvas = false,
                    profileId = preferredId,
                    modelName = modelName,
                    orientation = backgroundArt.orientation,
                    size = size,
                };
                return true;
            }

            BackgroundArtDefaults builtin = BackgroundArtDefaults.CreateBuiltin();
            if (BackgroundArtEquals(backgroundArt, builtin))
                return false;

            backgroundArt = builtin;
            return true;
        }

        void EnsureDesignDefaultsInitialized()
        {
            bool dirty = false;
            for (int i = 0; i < DesignForms.Length; i++)
            {
                ResponsiveDevice.FormFactor form = DesignForms[i];
                ref DesignFormDefaults slot = ref FormDefaultsRef(form);
                dirty |= EnsureForm(ref slot, form);
            }

            BackgroundArtDefaults sanitizedArt = SanitizeBackgroundArt(backgroundArt);
            if (!BackgroundArtEquals(backgroundArt, sanitizedArt))
            {
                backgroundArt = sanitizedArt;
                dirty = true;
            }

            dirty |= BindPreferredBackgroundProfile();

            if (dirty)
            {
                Save(true);
                ResponsiveUiBackgroundSizeWatch.NotifyChanged();
            }
        }

        static bool EnsureForm(ref DesignFormDefaults value, ResponsiveDevice.FormFactor form)
        {
            DesignFormDefaults sanitized = Sanitize(value, form);
            if (sanitized.profileId == value.profileId &&
                sanitized.modelName == value.modelName &&
                sanitized.designCanvasSize == value.designCanvasSize &&
                sanitized.defaultScale == value.defaultScale &&
                sanitized.landscapeDesignCanvasSize == value.landscapeDesignCanvasSize)
                return false;
            value = sanitized;
            return true;
        }

        ref DesignFormDefaults FormDefaultsRef(ResponsiveDevice.FormFactor form)
        {
            switch (form)
            {
                case ResponsiveDevice.FormFactor.Pad:
                    return ref pad;
                case ResponsiveDevice.FormFactor.FoldPhoneCover:
                    return ref foldPhoneCover;
                case ResponsiveDevice.FormFactor.FoldHalfOpened:
                    return ref fold;
                case ResponsiveDevice.FormFactor.FoldCoverHalfOpened:
                    return ref foldCoverHalfOpened;
                case ResponsiveDevice.FormFactor.FlipPhoneCover:
                    return ref flipPhoneCover;
                case ResponsiveDevice.FormFactor.FlipHalfOpened:
                    return ref flip;
                case ResponsiveDevice.FormFactor.iPhoneDuoCover:
                    return ref iPhoneDuoCover;
                case ResponsiveDevice.FormFactor.iPhoneDuoSeated:
                    return ref iPhoneDuoSeated;
                case ResponsiveDevice.FormFactor.WideScreen:
                    return ref wideScreen;
                default:
                    return ref phone;
            }
        }

        #endregion

        #region Design defaults

        public static DesignFormDefaults GetFormDefaults(ResponsiveDevice.FormFactor form) =>
            instance.FormDefaultsRef(form);

        public static Vector2 GetDesignCanvasSize(ResponsiveDevice.FormFactor form)
        {
            if (CatalogDesignCanvas(form, out Vector2 catalog))
                return catalog;

            Vector2 size = GetFormDefaults(form).designCanvasSize;
            return ResponsiveRectState.IsUnsetDesignCanvasSize(size)
                ? ResponsiveRectState.GetBuiltinDesignCanvasSize(form)
                : size;
        }

        /// <summary>
        /// True when the form's catalog reference device gives its design canvas (every form but
        /// Wide Screen, once its reference resolves): the Portrait row then shows that size and
        /// cannot be edited per pose (Design Scale Defaults changes the reference device).
        /// </summary>
        public static bool DesignCanvasFromReference(ResponsiveDevice.FormFactor form) =>
            CatalogDesignCanvas(form, out _);

        static bool CatalogDesignCanvas(ResponsiveDevice.FormFactor form, out Vector2 catalog)
        {
            DesignFormDefaults defaults = GetFormDefaults(form);
            catalog = default;
            return !string.IsNullOrEmpty(defaults.profileId) &&
                   ResolveCatalogCanvas(form, defaults.profileId, out _, out catalog, out _) &&
                   catalog.x > 0f && catalog.y > 0f;
        }

        /// <summary>
        /// Design canvas for a slot row: the Landscape row's own size when set, else the per-form
        /// canvas; oriented to the slot's screen shape (<see cref="ResponsiveUI.OrientSizeForSlot"/>,
        /// so Fold / Fold Cover Half Opened Landscape is tall, Portrait wide).
        /// Settings page and background art only: runtime scaling (and every Editor capture /
        /// unscale or display of it) keeps the per-form canvas, so captures round-trip.
        /// </summary>
        public static Vector2 GetDesignCanvasSize(
            ResponsiveDevice.FormFactor form,
            ResponsiveDevice.Orientation slotOrientation) =>
            RowDesignCanvasSize(
                GetDesignCanvasSize(form),
                GetFormDefaults(form).landscapeDesignCanvasSize,
                form,
                slotOrientation);

        /// <summary>
        /// Row rule: Landscape uses <paramref name="landscapeRow"/> unless unset (0,0); Portrait
        /// always uses <paramref name="formCanvas"/>. Either is oriented to the slot's screen shape.
        /// </summary>
        public static Vector2 RowDesignCanvasSize(
            Vector2 formCanvas,
            Vector2 landscapeRow,
            ResponsiveDevice.FormFactor form,
            ResponsiveDevice.Orientation slotOrientation)
        {
            Vector2 size = slotOrientation == ResponsiveDevice.Orientation.Landscape &&
                           !ResponsiveRectState.IsUnsetDesignCanvasSize(landscapeRow)
                ? landscapeRow
                : formCanvas;
            return ResponsiveUI.OrientSizeForSlot(size, form, slotOrientation);
        }

        static ResponsiveDevice.Orientation SlotOrientation(bool landscape) =>
            landscape ? ResponsiveDevice.Orientation.Landscape : ResponsiveDevice.Orientation.Portrait;

        static Vector2 GetFlipCoverSize() =>
            GetDesignCanvasSize(ResponsiveDevice.FormFactor.FlipPhoneCover);

        public static string GetModelName(ResponsiveDevice.FormFactor form)
        {
            DesignFormDefaults defaults = GetFormDefaults(form);
            if (!string.IsNullOrEmpty(defaults.profileId) &&
                ResolveCatalogCanvas(form, defaults.profileId, out string catalogName, out _, out _) &&
                !string.IsNullOrWhiteSpace(catalogName))
                return catalogName;

            string name = defaults.modelName;
            return string.IsNullOrWhiteSpace(name)
                ? ResponsiveRectState.GetBuiltinModelName(form)
                : name.Trim();
        }

        public static Vector2 GetDefaultScale(ResponsiveDevice.FormFactor form)
        {
            Vector2 scale = GetFormDefaults(form).defaultScale;
            return ResponsiveRectState.IsUnsetDefaultScale(scale)
                ? ResponsiveRectState.BuiltinDefaultScale
                : scale;
        }

        #endregion

        #region Slot enabled

        /// <summary>
        /// Whether a slot should be Enabled for Player Allowed Orientations.
        /// Allowed axes start Enabled for every form factor. Existing FoldPhoneCover / HalfOpened
        /// stay authored (see <see cref="ResponsiveRectStateEditorUtil.SyncSlotEnabled"/>).
        /// Rule: <see cref="ResponsiveDevice.SlotAllowedByPlayerAxes"/>. Fold Half Opened
        /// and Fold Cover Half Opened follow the lock like other slots (named by posture).
        /// iPhone Duo Seated (both slots) always shows: Seated allows only the hold's axis.
        /// Wide Screen slots need a Wide Screen build target (<see cref="ResponsivePlatforms.SeedsEnabled"/>).
        /// Other slots follow the Player axis of their screen shape.
        /// </summary>
        public static bool SlotAllowed(int slotIndex)
        {
            string[] names = ResponsiveWidgetLayout.StatePropertyNames;
            if (slotIndex < 0 || slotIndex >= names.Length)
                return true;

            // Wide Screen slots are on for Windows / macOS / Linux / WebGL targets and off on
            // iOS / Android (their seeds start Disabled there; authored data is left alone).
            ResponsiveDevice.FormFactor form = ResponsiveWidgetLayout.SlotFormFactor(slotIndex);
            if (!ResponsivePlatforms.SeedsEnabled(form))
                return false;

            FoldablePlayerOrientation.GetAllowedAxes(out bool portrait, out bool landscape);
            return ResponsiveDevice.SlotAllowedByPlayerAxes(
                form,
                ResponsiveWidgetLayout.SlotOrientation(slotIndex),
                portrait,
                landscape);
        }

        public static bool SlotAllowed(string slotPropertyName)
        {
            if (string.IsNullOrEmpty(slotPropertyName))
                return true;

            return !ResponsiveWidgetLayout.GetStatePropertyIndex(slotPropertyName, out int index) ||
                   SlotAllowed(index);
        }

        /// <summary>
        /// Slot shown in Inspectors: <see cref="SlotAllowed"/>, then the Editor lists Android and
        /// iOS slots on every build target (grouped under those headers). Wide Screen slots show
        /// on Wide Screen targets only. Display only: seeding and sync use <see cref="SlotAllowed"/>,
        /// so another platform's slots keep their data.
        /// </summary>
        public static bool SlotVisible(int slotIndex)
        {
            if (!SlotAllowed(slotIndex))
                return false;
            if (!ResponsivePlatforms.ConstrainsForms ||
                slotIndex < 0 ||
                slotIndex >= ResponsiveWidgetLayout.StatePropertyNames.Length)
                return true;
            ResponsiveDevice.FormFactor form = ResponsiveWidgetLayout.SlotFormFactor(slotIndex);
            if (form == ResponsiveDevice.FormFactor.WideScreen)
                return ResponsivePlatforms.IsActiveForm(form);
            return true;
        }

        public static bool SlotVisible(string slotPropertyName)
        {
            if (string.IsNullOrEmpty(slotPropertyName))
                return true;

            return !ResponsiveWidgetLayout.GetStatePropertyIndex(slotPropertyName, out int index) ||
                   SlotVisible(index);
        }

        /// <summary>
        /// Design form shown in Project Settings. Android and iOS forms are listed on every Editor
        /// build target. Wide Screen is listed on Wide Screen targets.
        /// </summary>
        public static bool FormVisible(ResponsiveDevice.FormFactor form)
        {
            if (!ResponsivePlatforms.ConstrainsForms)
                return true;
            if (form == ResponsiveDevice.FormFactor.WideScreen)
                return ResponsivePlatforms.IsActiveForm(form);
            return true;
        }

        public static bool IsPortraitAllowedByPlayer() => FoldablePlayerOrientation.IsPortraitAllowed;

        public static bool IsLandscapeAllowedByPlayer() => FoldablePlayerOrientation.IsLandscapeAllowed;

        public void SetLockEnabledToggles(bool value)
        {
            if (lockEnabledToggles == value)
                return;
            lockEnabledToggles = value;
            Save(true);
        }

        public void SetHingeGuide(bool show, Color color, float thicknessPx)
        {
            thicknessPx = Mathf.Clamp(
                thicknessPx,
                ResponsiveUiHingeGuide.MinThicknessPx,
                ResponsiveUiHingeGuide.MaxThicknessPx);
            if (showHalfOpenedHingeGuide == show &&
                hingeGuideColor == color &&
                Mathf.Approximately(hingeGuideThicknessPx, thicknessPx))
                return;

            showHalfOpenedHingeGuide = show;
            hingeGuideColor = color;
            hingeGuideThicknessPx = thicknessPx;
            Save(true);
            ResponsiveUiHingeGuideOverlay.NotifySettingsChanged();
        }

        /// <summary>
        /// Sets one slot row's design canvas. Landscape: the row override ((0,0) clears it);
        /// Portrait: the per-form <see cref="DesignFormDefaults.designCanvasSize"/>, (0,0) = built-in. A Portrait
        /// size is not written while a catalog reference device gives the canvas
        /// (<see cref="DesignCanvasFromReference"/>): it could not take effect.
        /// </summary>
        public void SetDesignCanvasSize(
            ResponsiveDevice.FormFactor form,
            ResponsiveDevice.Orientation slotOrientation,
            Vector2 size)
        {
            DesignFormDefaults defaults = GetFormDefaults(form);
            if (slotOrientation == ResponsiveDevice.Orientation.Landscape)
                defaults.landscapeDesignCanvasSize = size;
            else
            {
                if (DesignCanvasFromReference(form))
                    return;
                // Keep the stored per-form shape (Fold half open stays tall) for every other reader.
                Vector2 stored = GetDesignCanvasSize(form);
                defaults.designCanvasSize = ResponsiveUI.OrientSize(
                    size,
                    stored.x > stored.y
                        ? ResponsiveDevice.Orientation.Landscape
                        : ResponsiveDevice.Orientation.Portrait);
            }
            SetFormDefaults(form, defaults);
        }

        public void SetFormDefaults(ResponsiveDevice.FormFactor form, DesignFormDefaults defaults)
        {
            DesignFormDefaults sanitized = Sanitize(defaults, form);
            ref DesignFormDefaults slot = ref FormDefaultsRef(form);
            if (SameFormDefaults(slot, sanitized))
                return;

            bool profileChanged = !string.Equals(
                slot.profileId ?? "", sanitized.profileId ?? "", System.StringComparison.Ordinal);
            slot = sanitized;
            Save(true);
            // Rewriting .device skins / AssetDatabase.Refresh only when the catalog
            // reference actually changes — not on Vector2Field / TextField noise.
            if (profileChanged)
                EnsureReferenceSimulatorSkinsEnabled();
            ResponsiveUiBackgroundSizeWatch.NotifyChanged();
        }

        #endregion

        #region Background art

        public static BackgroundArtDefaults GetBackgroundArt() => instance.backgroundArt;

        public static Vector2 GetBackgroundArtSize()
        {
            BackgroundArtDefaults art = instance.backgroundArt;
            if (art.followDesignCanvas)
                return GetDesignCanvasSize(art.form, art.orientation);

            if (!string.IsNullOrEmpty(art.profileId) &&
                ResolveCatalogSize(
                    art.form, art.profileId, art.orientation,
                    out _, out Vector2 catalog, out _) &&
                catalog.x > 0f && catalog.y > 0f)
                return catalog;

            return ResponsiveUI.BuiltinOriginalBgSize;
        }

        public void SetBackgroundArt(BackgroundArtDefaults value)
        {
            BackgroundArtDefaults sanitized = SanitizeBackgroundArt(value);
            if (BackgroundArtEquals(backgroundArt, sanitized))
                return;

            backgroundArt = sanitized;
            Save(true);
            ResponsiveUiBackgroundSizeWatch.NotifyChanged();
        }

        public bool ApplyBackgroundCatalogProfile(
            ResponsiveDevice.FormFactor form,
            string profileId,
            ResponsiveDevice.Orientation orientation,
            out string error)
        {
            if (!ResolveCatalogSize(form, profileId, orientation, out string modelName, out Vector2 size, out error))
                return false;

            SetBackgroundArt(
                new BackgroundArtDefaults
                {
                    form = form,
                    followDesignCanvas = false,
                    profileId = profileId,
                    modelName = modelName,
                    orientation = orientation,
                    size = size,
                });
            return true;
        }

        public void ResetBackgroundArtToBuiltin()
        {
            SetBackgroundArt(BackgroundArtDefaults.CreateBuiltin());
        }

        #endregion

        #region Catalog apply

        /// <summary>
        /// Applies a catalog device as the reference profile for <paramref name="form"/>:
        /// model name + portrait design canvas size from the profile.
        /// </summary>
        public bool ApplyCatalogProfile(ResponsiveDevice.FormFactor form, string profileId, out string error)
        {
            error = null;
            if (string.IsNullOrEmpty(profileId))
            {
                error = "Profile id is empty.";
                return false;
            }

            if (!ResolveCatalogCanvas(form, profileId, out string modelName, out Vector2 canvas, out error))
                return false;

            DesignFormDefaults current = GetFormDefaults(form);
            SetFormDefaults(
                form,
                new DesignFormDefaults
                {
                    profileId = profileId,
                    modelName = modelName,
                    designCanvasSize = canvas,
                    defaultScale = current.defaultScale.sqrMagnitude > 0f
                        ? current.defaultScale
                        : ResponsiveRectState.BuiltinDefaultScale,
                });
            return true;
        }

        public void ResetDesignDefaultsToBuiltins()
        {
            for (int i = 0; i < DesignForms.Length; i++)
            {
                ResponsiveDevice.FormFactor form = DesignForms[i];
                FormDefaultsRef(form) = DesignFormDefaults.CreateBuiltin(form);
            }

            Save(true);
            EnsureReferenceSimulatorSkinsEnabled();
            ResponsiveUiBackgroundSizeWatch.NotifyChanged();
        }

        #endregion

        #region Catalog resolve

        public static bool ResolveCatalogCanvas(
            ResponsiveDevice.FormFactor form,
            string profileId,
            out string modelName,
            out Vector2 canvas,
            out string error) =>
            ResolveCatalogSize(
                form, profileId,
                // The slot showing the portrait (tall) screen.
                ResponsiveDevice.OrientationForForm(form, ResponsiveDevice.Orientation.Portrait),
                out modelName, out canvas, out error);

        public static bool ResolveCatalogSize(
            ResponsiveDevice.FormFactor form,
            string profileId,
            ResponsiveDevice.Orientation orientation,
            out string modelName,
            out Vector2 canvas,
            out string error)
        {
            modelName = null;
            canvas = default;
            error = null;
            if (!HasCatalogReference(form))
            {
                error = $"{ResponsiveDevice.GetFormDisplayName(form)} has no catalog reference device.";
                return false;
            }

            // Slot orientation → screen shape (Fold half-open Portrait is wide, Landscape tall).
            bool landscape = ResponsiveDevice.OrientationForForm(form, orientation) ==
                             ResponsiveDevice.Orientation.Landscape;

            if (IsPhonePadForm(form))
            {
                var profile = PhonePadDeviceCatalog.FindById(profileId);
                if (profile == null)
                {
                    error = $"Phone/Pad profile '{profileId}' not found.";
                    return false;
                }

                bool wantPad = form == ResponsiveDevice.FormFactor.Pad;
                bool isPad = profile.Kind == PhonePadDeviceKind.Pad;
                if (wantPad != isPad)
                {
                    error = $"Profile '{profileId}' kind does not match {form}.";
                    return false;
                }

                modelName = string.IsNullOrWhiteSpace(profile.ShortName) ? profile.Id : profile.ShortName;
                canvas = landscape
                    ? new Vector2(
                        profile.LandscapeWidth > 0 ? profile.LandscapeWidth : profile.PortraitHeight,
                        profile.LandscapeHeight > 0 ? profile.LandscapeHeight : profile.PortraitWidth)
                    : new Vector2(profile.PortraitWidth, profile.PortraitHeight);
                return canvas.x > 0f && canvas.y > 0f;
            }

            var foldProfile = FoldableDeviceCatalog.FindById(profileId);
            if (foldProfile == null)
            {
                error = $"Fold/Flip profile '{profileId}' not found.";
                return false;
            }

            bool wantFlip =
                form == ResponsiveDevice.FormFactor.FlipHalfOpened ||
                form == ResponsiveDevice.FormFactor.FlipPhoneCover;
            bool wantCover = ResponsiveDevice.IsCoverForm(form);
            bool isFlip = foldProfile.Kind == FoldableDeviceKind.FlipablePhone;
            bool isFoldFamily =
                foldProfile.Kind == FoldableDeviceKind.FoldablePhone ||
                foldProfile.Kind == FoldableDeviceKind.DualScreen;
            if (wantFlip && !isFlip)
            {
                error = $"Profile '{profileId}' is not a Flip device.";
                return false;
            }

            if (!wantFlip && !isFoldFamily)
            {
                error = $"Profile '{profileId}' is not a Fold / dual-screen device.";
                return false;
            }

            // iPhone Duo forms pick landscape-native inner devices only; Galaxy Folds never.
            bool wantDuo =
                form == ResponsiveDevice.FormFactor.iPhoneDuoCover ||
                form == ResponsiveDevice.FormFactor.iPhoneDuoSeated;
            if (wantDuo != foldProfile.HasLandscapeNativeInner)
            {
                error = wantDuo
                    ? $"Profile '{profileId}' is not an iPhone Duo-like device."
                    : $"Profile '{profileId}' is an iPhone Duo-like device.";
                return false;
            }

            if (wantCover)
            {
                if (!GetCoverCanvas(foldProfile, landscape, out canvas))
                {
                    error = $"Profile '{profileId}' has no usable Cover/outer size.";
                    return false;
                }

                string baseName = string.IsNullOrWhiteSpace(foldProfile.ShortName)
                    ? foldProfile.Id
                    : foldProfile.ShortName;
                modelName = baseName.IndexOf("Cover", System.StringComparison.OrdinalIgnoreCase) >= 0
                    ? baseName
                    : baseName + " Cover";
                return true;
            }

            if (!GetHalfOpenedCanvas(foldProfile, landscape, out canvas))
            {
                error = $"Profile '{profileId}' has no usable main/inner size.";
                return false;
            }

            modelName = string.IsNullOrWhiteSpace(foldProfile.ShortName) ? foldProfile.Id : foldProfile.ShortName;
            return true;
        }

        static bool GetCoverCanvas(
            FoldableDeviceCatalog.DeviceProfile profile,
            bool landscape,
            out Vector2 canvas)
        {
            canvas = default;
            if (profile?.Screens == null || profile.Screens.Length == 0)
                return false;

            FoldableDeviceCatalog.ScreenProfile cover = null;
            for (int i = 0; i < profile.Screens.Length; i++)
            {
                var s = profile.Screens[i];
                if (s == null)
                    continue;
                if (s.CoverOrOuter)
                {
                    cover = s;
                    break;
                }
            }

            if (cover == null)
                cover = profile.Screens[0];

            return GetOrientedScreenSize(cover, landscape, out canvas);
        }

        static bool GetHalfOpenedCanvas(
            FoldableDeviceCatalog.DeviceProfile profile,
            bool landscape,
            out Vector2 canvas)
        {
            canvas = default;
            if (profile?.Screens == null || profile.Screens.Length == 0)
                return false;

            FoldableDeviceCatalog.ScreenProfile main = null;
            for (int i = 0; i < profile.Screens.Length; i++)
            {
                var s = profile.Screens[i];
                if (s == null)
                    continue;
                if (!s.CoverOrOuter)
                    main = s;
            }

            if (main == null)
                main = profile.Screens[profile.Screens.Length - 1];

            return GetOrientedScreenSize(main, landscape, out canvas);
        }

        static bool GetOrientedScreenSize(
            FoldableDeviceCatalog.ScreenProfile screen,
            bool landscape,
            out Vector2 canvas)
        {
            canvas = default;
            if (screen == null || screen.PortraitWidth < 1 || screen.PortraitHeight < 1)
                return false;

            screen.GetOrientedSize(landscape, out int width, out int height);
            if (width < 1 || height < 1)
                return false;

            canvas = new Vector2(width, height);
            return true;
        }

        #endregion

        #region Sanitize

        /// <summary>
        /// Catalog forms without a profile reset to the preferred device. Wide Screen never binds a
        /// profile and keeps its edited canvas / scale (only unset fields fall back to built-ins).
        /// </summary>
        static DesignFormDefaults Sanitize(DesignFormDefaults value, ResponsiveDevice.FormFactor form)
        {
            DesignFormDefaults builtin = DesignFormDefaults.CreateBuiltin(form);
            // The Landscape row override survives the per-form reset below; unset stays (0,0).
            builtin.landscapeDesignCanvasSize =
                ResponsiveRectState.IsUnsetDesignCanvasSize(value.landscapeDesignCanvasSize)
                    ? Vector2.zero
                    : value.landscapeDesignCanvasSize;
            value.landscapeDesignCanvasSize = builtin.landscapeDesignCanvasSize;
            if (!HasCatalogReference(form))
                value.profileId = "";
            else if (string.IsNullOrEmpty(value.profileId))
                return builtin;
            if (string.IsNullOrWhiteSpace(value.modelName))
                value.modelName = builtin.modelName;
            if (ResponsiveRectState.IsUnsetDesignCanvasSize(value.designCanvasSize))
                value.designCanvasSize = builtin.designCanvasSize;
            if (ResponsiveRectState.IsUnsetDefaultScale(value.defaultScale))
                value.defaultScale = builtin.defaultScale;
            return value;
        }

        static bool SameFormDefaults(in DesignFormDefaults a, in DesignFormDefaults b) =>
            string.Equals(a.profileId ?? "", b.profileId ?? "", System.StringComparison.Ordinal) &&
            string.Equals(a.modelName ?? "", b.modelName ?? "", System.StringComparison.Ordinal) &&
            Mathf.Approximately(a.designCanvasSize.x, b.designCanvasSize.x) &&
            Mathf.Approximately(a.designCanvasSize.y, b.designCanvasSize.y) &&
            Mathf.Approximately(a.defaultScale.x, b.defaultScale.x) &&
            Mathf.Approximately(a.defaultScale.y, b.defaultScale.y) &&
            Mathf.Approximately(a.landscapeDesignCanvasSize.x, b.landscapeDesignCanvasSize.x) &&
            Mathf.Approximately(a.landscapeDesignCanvasSize.y, b.landscapeDesignCanvasSize.y);

        static BackgroundArtDefaults SanitizeBackgroundArt(BackgroundArtDefaults value)
        {
            BackgroundArtDefaults builtin = BackgroundArtDefaults.CreateBuiltin();
            if (value.modelName == null)
                value.modelName = "";
            if (!IsDesignForm(value.form))
                value.form = builtin.form;
            // Wide Screen has no catalog device: background art can only follow its Design Canvas.
            if (!HasCatalogReference(value.form))
                value.followDesignCanvas = true;
            if (value.followDesignCanvas)
            {
                DesignFormDefaults design = GetFormDefaults(value.form);
                value.profileId = string.IsNullOrEmpty(design.profileId)
                    ? GetPreferredProfileId(value.form)
                    : design.profileId;
                value.modelName = string.IsNullOrWhiteSpace(design.modelName)
                    ? ResponsiveRectState.GetBuiltinModelName(value.form)
                    : design.modelName;
                value.size = GetDesignCanvasSize(value.form, value.orientation);
                return value;
            }

            if (string.IsNullOrEmpty(value.profileId))
                return builtin;

            if (value.size.x <= 0f || value.size.y <= 0f)
                value.size = builtin.size;
            return value;
        }

        static bool BackgroundArtEquals(in BackgroundArtDefaults a, in BackgroundArtDefaults b) =>
            a.form == b.form &&
            a.followDesignCanvas == b.followDesignCanvas &&
            a.profileId == b.profileId &&
            a.modelName == b.modelName &&
            a.orientation == b.orientation &&
            a.size == b.size;

        static bool IsDesignForm(ResponsiveDevice.FormFactor form)
        {
            for (int i = 0; i < DesignForms.Length; i++)
            {
                if (DesignForms[i] == form)
                    return true;
            }

            return false;
        }

        #endregion
    }
}

#endif
