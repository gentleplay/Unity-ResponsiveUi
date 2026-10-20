using UnityEngine;

namespace FDPlus.Foldable
{
    /// <summary>
    /// Play Mode overlay that pushes simulated product states into <see cref="FoldablePostureDetector"/>.
    /// Editor only. Android players use Jetpack via the detector — this overlay
    /// must not overwrite HalfOpened with serialized Expand.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(FoldablePostureDetector))]
    public class FoldableSimulator : MonoBehaviour
    {
#if UNITY_EDITOR
        #region Constants

        static readonly string[] KindLabels = { "Fold", "Flip", "Dual" };
        static readonly string[] HingeLabels = { "Tabletop", "Book" };
        static readonly string[] ScreenOrientLabels = { "Portrait", "Landscape" };

        #endregion
#endif

        #region Serialized

        // Unconditional so the serialized layout matches between Editor and players;
        // only the Editor reads them.
#pragma warning disable CS0414, CS0649
        [SerializeField] bool showOverlay = true;
        [SerializeField] FoldableDeviceKind deviceKind = FoldableDeviceKind.FoldablePhone;
        [SerializeField] FoldableProductState productState = FoldableProductState.Opened;
        [SerializeField] FoldableOrientation halfOpenHinge = FoldableOrientation.Horizontal;
        [SerializeField] [Range(0.05f, 0.95f)] float hingeNormalized = 0.5f;
        [SerializeField] int hingeThicknessPx = 24;
        [SerializeField] int metricsWidth = 1812;
        [SerializeField] int metricsHeight = 2176;
        [SerializeField] int coverWidth = 832;
        [SerializeField] int coverHeight = 2268;
        [SerializeField] int openWidth = 1812;
        [SerializeField] int openHeight = 2176;
        [SerializeField] bool landscape;
        [SerializeField] bool isSeparating;
#pragma warning restore CS0414, CS0649

        #endregion

#if UNITY_EDITOR
        #region Runtime state

        FoldablePostureDetector detector;
        bool dirty = true;
        bool flatRestored;
        // Screen shape (true = wide) when Half Opened began; a Locked card holds it.
        bool halfOpenEntryLandscape;

        #endregion
#endif

        #region Lifecycle

        void Awake()
        {
#if UNITY_EDITOR
            detector = GetComponent<FoldablePostureDetector>();
            halfOpenEntryLandscape = landscape;
            RefreshLandscapeDefault();
#else
            enabled = false;
#endif
        }

#if UNITY_EDITOR
        void OnEnable()
        {
            // Fold panel owns the first Play Mode layout. Do not push serialized Expand.
            bool usePanelLayout =
                Application.isPlaying && FoldableSimulationGate.AllowSimulatedLayouts;
            dirty = !usePanelLayout;
            if (usePanelLayout)
                SyncOverlayFromDetector();
        }

        void Update()
        {
            if (!FoldablePostureDetector.IsRuntimeSupported)
            {
                enabled = false;
                return;
            }

            HandleHotkeys();

            // Device Simulator selected a non-Fold/Flip phone — keep full-screen Flat.
            if (!FoldableSimulationGate.AllowSimulatedLayouts)
            {
                if (!flatRestored || dirty)
                {
                    detector.ApplyFlatLayout();
                    flatRestored = true;
                    dirty = false;
                }
                return;
            }

            // Switched back to a Fold/Flip profile — show the detector posture, do not
            // re-push serialized Expand over the Fold panel / Jetpack snapshot.
            if (flatRestored)
            {
                SyncOverlayFromDetector();
                dirty = false;
            }
            flatRestored = false;
            if (dirty)
                PushLayout();
        }
#endif

        #endregion

#if UNITY_EDITOR
        #region Simulator

        void HandleHotkeys()
        {
#if !ENABLE_INPUT_SYSTEM || ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.F) &&
                (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)))
            {
                SetProductState(FoldableProductStateResolver.FromIndex(
                    (FoldableProductStateResolver.ToIndex(productState) + 1) % 3));
                SyncMetricsForProduct();
                dirty = true;
            }

            if (Input.GetKeyDown(KeyCode.LeftBracket))
            {
                hingeNormalized = Mathf.Clamp(hingeNormalized - 0.02f, 0.05f, 0.95f);
                dirty = true;
            }

            if (Input.GetKeyDown(KeyCode.RightBracket))
            {
                hingeNormalized = Mathf.Clamp(hingeNormalized + 0.02f, 0.05f, 0.95f);
                dirty = true;
            }
#endif
        }

        #endregion

        #region GUI

        void OnGUI()
        {
            if (!showOverlay)
                return;

            if (!FoldableSimulationGate.AllowSimulatedLayouts)
            {
                GUILayout.BeginArea(new Rect(12f, 12f, 320f, 90f), "Foldable Simulator", GUI.skin.window);
                GUILayout.Label(
                    "Device Simulator is not a ★ Fold / ★ Flip / ★ Dual profile.\n" +
                    "Layout restored to full-screen Flat.");
                GUILayout.EndArea();
                return;
            }

            GUILayout.BeginArea(new Rect(12f, 12f, 320f, 340f), "Foldable Simulator", GUI.skin.window);

            DrawKindToolbar();
            DrawProductToolbar();
            DrawScreenOrientation();
            DrawHingeControls();

            bool sep = GUILayout.Toggle(isSeparating, "isSeparating (dual-pane)");
            if (sep != isSeparating)
            {
                isSeparating = sep;
                dirty = true;
            }

            GUILayout.Label($"{metricsWidth}×{metricsHeight}");
            GUILayout.Label("Shift+F cycles product state");
            GUILayout.EndArea();
        }

        void DrawKindToolbar()
        {
            GUILayout.Label("Device kind");
            int kindIdx = KindToIndex(deviceKind);
            int next = GUILayout.Toolbar(kindIdx, KindLabels);
            if (next == kindIdx)
                return;

            deviceKind = IndexToKind(next);
            ApplyCatalogPairForKind(deviceKind);
            SyncMetricsForProduct();
            dirty = true;
        }

        void DrawProductToolbar()
        {
            GUILayout.Label("Product state: " +
                            FoldableProductStateResolver.GetDisplayName(deviceKind, productState));

            int idx = FoldableProductStateResolver.ToIndex(productState);
            int next = GUILayout.Toolbar(idx, FoldableProductStateResolver.GetDisplayNames(deviceKind));
            if (next == idx)
                return;

            SetProductState(FoldableProductStateResolver.FromIndex(next));
            if (productState == FoldableProductState.HalfOpened ||
                FoldableProductStateResolver.GetLockedLandscapeFromScreen().HasValue)
            {
                RefreshLandscapeDefault();
            }
            SyncMetricsForProduct();
            dirty = true;
        }

        void DrawScreenOrientation()
        {
            bool? projectLocked = FoldableProductStateResolver.GetLockedLandscapeFromScreen();
            var lockedLandscape = FoldableProductStateResolver.LockedScreenLandscape(
                deviceKind, productState, projectLocked);
            // An Android Fold / Flip Half Opened keeps its screen while rotated when its card holds it.
            var heldLandscape = FoldableProductStateResolver.HeldHalfOpenScreenLandscape(
                deviceKind, productState, projectLocked, halfOpenEntryLandscape, FoldCover,
                FoldableProductStateResolver.AllowsRuntimeHoldFromScreen());
            var fixedLandscape = heldLandscape ?? lockedLandscape;
            if (fixedLandscape.HasValue)
                landscape = fixedLandscape.Value;

            GUILayout.Label(lockedLandscape.HasValue
                ? "Screen orientation (locked by project)"
                : heldLandscape.HasValue
                    ? "Screen orientation (held while Half Opened)"
                    : "Screen orientation");
            GUI.enabled = !fixedLandscape.HasValue;
            // Labelled with the slot name: a Fold Half Opened wide screen is Portrait.
            int orientIdx = FoldableProductStateResolver.PoseLandscape(
                deviceKind, productState, landscape) ? 1 : 0;
            int next = GUILayout.Toolbar(orientIdx, ScreenOrientLabels);
            GUI.enabled = true;
            if (fixedLandscape.HasValue || next == orientIdx)
                return;

            landscape = FoldableProductStateResolver.PoseLandscape(deviceKind, productState, next == 1);
            SyncMetricsForProduct();
            dirty = true;
        }

        void DrawHingeControls()
        {
            bool halfOpen = productState == FoldableProductState.HalfOpened;
            var lockedHinge = FoldableProductStateResolver.GetLockedHalfOpenHingeFromScreen();
            if (halfOpen && lockedHinge.HasValue)
                halfOpenHinge = lockedHinge.Value;
            // Android Fold Half Opened: the crease follows the held screen, so picking a hinge
            // would rotate it.
            bool creaseFollowsScreen = FoldableProductStateResolver.IsFoldHalfOpen(
                deviceKind, productState, innerLandscapeNative: false);
            if (creaseFollowsScreen)
                halfOpenHinge = FoldableProductStateResolver.FoldHalfOpenHingeForScreen(landscape);

            GUI.enabled = halfOpen && !lockedHinge.HasValue && !creaseFollowsScreen;

            GUILayout.Label(lockedHinge.HasValue
                ? "Half-open hinge (locked by project)"
                : "Half-open hinge");
            int orient = halfOpenHinge == FoldableOrientation.Vertical ? 1 : 0;
            int nextOrient = GUILayout.Toolbar(orient, HingeLabels);
            GUI.enabled = halfOpen && !lockedHinge.HasValue;
            if (nextOrient != orient && !lockedHinge.HasValue && !creaseFollowsScreen)
            {
                halfOpenHinge = nextOrient == 1
                    ? FoldableOrientation.Vertical
                    : FoldableOrientation.Horizontal;
                RefreshLandscapeDefault();
                SyncMetricsForProduct();
                dirty = true;
            }

            GUILayout.Label($"Hinge {hingeNormalized:0.00}");
            float hinge = GUILayout.HorizontalSlider(hingeNormalized, 0.05f, 0.95f);
            if (!Mathf.Approximately(hinge, hingeNormalized))
            {
                hingeNormalized = hinge;
                dirty = true;
            }

            GUI.enabled = true;
        }

        #endregion

        #region Apply

        void SyncOverlayFromDetector()
        {
            FoldableLayoutState layout = detector.LayoutState;
            if (layout.productState == FoldableProductState.Undefined)
                return;

            if (layout.deviceKind != FoldableDeviceKind.Unknown)
                deviceKind = layout.deviceKind;
            if (layout.orientation != FoldableOrientation.Undefined)
                halfOpenHinge = layout.orientation;
            isSeparating = layout.isSeparating;
            metricsWidth = Mathf.Max(1, layout.currentMetrics.width);
            metricsHeight = Mathf.Max(1, layout.currentMetrics.height);
            landscape = metricsWidth > metricsHeight;
            SetProductState(layout.productState);
        }

        void PushLayout()
        {
            SyncMetricsForProduct();

            detector.SetEditorDeviceKind(deviceKind);
            detector.ApplySimulatedLayout(
                FoldableProductStateResolver.CreateFromProductState(
                    deviceKind,
                    productState,
                    metricsWidth,
                    metricsHeight,
                    hingeNormalized,
                    hingeThicknessPx,
                    halfOpenHinge,
                    isSeparating));

            dirty = false;
        }

        void SyncMetricsForProduct()
        {
            bool cover = productState == FoldableProductState.Closed;
            metricsWidth = cover ? coverWidth : openWidth;
            metricsHeight = cover ? coverHeight : openHeight;
            FoldableProductStateResolver.ApplyOrientedMetrics(landscape, ref metricsWidth, ref metricsHeight);
        }

        void ApplyCatalogPairForKind(FoldableDeviceKind kind)
        {
            if (FoldableProductStateResolver.GetProductMetrics(
                    kind, FoldableProductState.Closed, out int cw, out int ch))
            {
                coverWidth = cw;
                coverHeight = ch;
            }

            if (FoldableProductStateResolver.GetProductMetrics(
                    kind, FoldableProductState.Opened, out int ow, out int oh))
            {
                openWidth = ow;
                openHeight = oh;
            }

            halfOpenHinge = kind == FoldableDeviceKind.DualScreen
                ? FoldableOrientation.Vertical
                : FoldableOrientation.Horizontal;
        }

        void RefreshLandscapeDefault()
        {
            var lockedHinge = FoldableProductStateResolver.GetLockedHalfOpenHingeFromScreen();
            if (productState == FoldableProductState.HalfOpened && lockedHinge.HasValue)
                halfOpenHinge = lockedHinge.Value;

            bool? projectLocked = FoldableProductStateResolver.GetLockedLandscapeFromScreen();
            // A held Android Fold / Flip enters Half Opened with its card's screen (the lock's pose,
            // or the screen it had) and its crease follows that screen.
            var heldLandscape = FoldableProductStateResolver.HeldHalfOpenScreenLandscape(
                deviceKind, productState, projectLocked, halfOpenEntryLandscape, FoldCover,
                FoldableProductStateResolver.AllowsRuntimeHoldFromScreen());
            if (heldLandscape.HasValue)
            {
                landscape = heldLandscape.Value;
                halfOpenHinge = FoldableProductStateResolver.FoldHalfOpenHingeForScreen(landscape);
                return;
            }

            landscape = FoldableProductStateResolver.ResolveDefaultLandscape(
                deviceKind,
                halfOpenHinge,
                productState,
                FoldableProductStateResolver.LockedScreenLandscape(
                    deviceKind, productState, projectLocked));
        }

        #endregion

        #region Helpers

        /// <summary>Sets the product, remembering the screen it had when Half Opened begins.</summary>
        void SetProductState(FoldableProductState next)
        {
            if (next == FoldableProductState.HalfOpened && productState != FoldableProductState.HalfOpened)
                halfOpenEntryLandscape = landscape;
            productState = next;
        }

        /// <summary>True when the open screen is Fold Cover size (Fold8-style), so its card applies.</summary>
        bool FoldCover =>
            deviceKind == FoldableDeviceKind.FoldablePhone &&
            FoldableProductStateResolver.IsFoldCoverHalfOpenedSize?.Invoke(openWidth, openHeight) == true;

        static int KindToIndex(FoldableDeviceKind kind)
        {
            switch (kind)
            {
                case FoldableDeviceKind.FlipablePhone: return 1;
                case FoldableDeviceKind.DualScreen: return 2;
                default: return 0;
            }
        }

        static FoldableDeviceKind IndexToKind(int index)
        {
            switch (index)
            {
                case 1: return FoldableDeviceKind.FlipablePhone;
                case 2: return FoldableDeviceKind.DualScreen;
                default: return FoldableDeviceKind.FoldablePhone;
            }
        }

        #endregion
#endif
    }
}
