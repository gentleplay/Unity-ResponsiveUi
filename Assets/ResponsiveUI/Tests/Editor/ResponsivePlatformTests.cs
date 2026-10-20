using FDPlus.ResponsiveUI;
using FDPlus.ResponsiveUI.Editor;
using FDPlus.ResponsiveUI.Shared.Editor;
using NUnit.Framework;
using UnityEngine;

using static FDPlus.ResponsiveUI.Tests.ResponsiveEditorTestUtil;

namespace FDPlus.ResponsiveUI.Tests
{
    /// <summary>
    /// Platform split: Android (Phone / Pad / Fold / Flip), iOS (Phone / Pad / iPhone Duo) and Wide
    /// Screen (Windows / macOS / Linux / WebGL).
    /// </summary>
    public class ResponsivePlatformTests
    {
        [Test]
        public void Owns_GroupsFormsByPlatform()
        {
            foreach (ResponsiveDevice.FormFactor form in ResponsiveWidgetLayout.FormFactors)
            {
                bool shared = form == ResponsiveDevice.FormFactor.Phone ||
                              form == ResponsiveDevice.FormFactor.Pad;
                bool duo = form == ResponsiveDevice.FormFactor.iPhoneDuoCover ||
                           form == ResponsiveDevice.FormFactor.iPhoneDuoSeated;
                bool wide = form == ResponsiveDevice.FormFactor.WideScreen;
                bool android = !shared && !duo && !wide;

                Assert.AreEqual(shared || android, ResponsivePlatforms.Owns(ResponsivePlatform.Android, form), form + " Android");
                Assert.AreEqual(shared || duo, ResponsivePlatforms.Owns(ResponsivePlatform.Ios, form), form + " iOS");
                Assert.AreEqual(wide, ResponsivePlatforms.Owns(ResponsivePlatform.WideScreen, form), form + " Wide Screen");
            }
        }

        [TestCase(ResponsivePlatform.Android, ResponsiveDevice.FormFactor.iPhoneDuoCover, ResponsiveDevice.FormFactor.Phone)]
        [TestCase(ResponsivePlatform.Android, ResponsiveDevice.FormFactor.iPhoneDuoSeated, ResponsiveDevice.FormFactor.Pad)]
        [TestCase(ResponsivePlatform.Android, ResponsiveDevice.FormFactor.FoldHalfOpened, ResponsiveDevice.FormFactor.FoldHalfOpened)]
        [TestCase(ResponsivePlatform.Ios, ResponsiveDevice.FormFactor.FoldPhoneCover, ResponsiveDevice.FormFactor.Phone)]
        [TestCase(ResponsivePlatform.Ios, ResponsiveDevice.FormFactor.FlipHalfOpened, ResponsiveDevice.FormFactor.Phone)]
        [TestCase(ResponsivePlatform.Ios, ResponsiveDevice.FormFactor.FoldCoverHalfOpened, ResponsiveDevice.FormFactor.Pad)]
        [TestCase(ResponsivePlatform.Ios, ResponsiveDevice.FormFactor.iPhoneDuoSeated, ResponsiveDevice.FormFactor.iPhoneDuoSeated)]
        [TestCase(ResponsivePlatform.Ios, ResponsiveDevice.FormFactor.WideScreen, ResponsiveDevice.FormFactor.Pad)]
        [TestCase(ResponsivePlatform.WideScreen, ResponsiveDevice.FormFactor.Phone, ResponsiveDevice.FormFactor.WideScreen)]
        [TestCase(ResponsivePlatform.WideScreen, ResponsiveDevice.FormFactor.iPhoneDuoSeated, ResponsiveDevice.FormFactor.WideScreen)]
        public void Constrain_MapsOtherPlatformForms(
            ResponsivePlatform platform,
            ResponsiveDevice.FormFactor form,
            ResponsiveDevice.FormFactor expected)
        {
            Assert.AreEqual(expected, ResponsivePlatforms.Constrain(platform, form));
        }

        [TestCase(1920f, 1080f, ResponsiveDevice.Orientation.Landscape, TestName = "WideScreen_16x9_Landscape")]
        [TestCase(2560f, 1600f, ResponsiveDevice.Orientation.Landscape, TestName = "WideScreen_16x10_Landscape")]
        [TestCase(3440f, 1440f, ResponsiveDevice.Orientation.Landscape, TestName = "WideScreen_21x9_Landscape")]
        [TestCase(1600f, 1200f, ResponsiveDevice.Orientation.Landscape, TestName = "WideScreen_4x3_Landscape")]
        [TestCase(900f, 1600f, ResponsiveDevice.Orientation.Portrait, TestName = "WideScreen_PortraitBrowser_Portrait")]
        public void WideScreen_FormIsWideScreen_OrientationFollowsAspect(
            float width,
            float height,
            ResponsiveDevice.Orientation expected)
        {
            WithPlatform(ResponsivePlatform.WideScreen, () =>
            {
                var size = new Vector2(width, height);
                Assert.AreEqual(
                    ResponsiveDevice.FormFactor.WideScreen,
                    ResponsiveDevice.DetectActiveFormFactor(size, 1.6f, foldableRuntimeSupported: true));
                Assert.AreEqual(
                    new ResponsiveDevice.LayoutKey(ResponsiveDevice.FormFactor.WideScreen, expected),
                    ResponsiveDevice.ResolveLiveLayout(size));
                Assert.AreEqual(ResponsiveDevice.FormFactor.WideScreen, ResponsiveDevice.CurrentFormFactor);
            });
        }

        [TestCase(1920f, 1080f, 1f, 1f, TestName = "WideScreenScale_16x9_Design")]
        [TestCase(3840f, 2160f, 2f, 2f, TestName = "WideScreenScale_16x9_4K")]
        [TestCase(2560f, 1080f, 1.3333333f, 1f, TestName = "WideScreenScale_21x9")]
        [TestCase(1440f, 1080f, 0.75f, 1f, TestName = "WideScreenScale_4x3")]
        public void WideScreen_DesignScale_FollowsWindowRatio(
            float width,
            float height,
            float expectedX,
            float expectedY)
        {
            var state = ResponsiveRectState.CreateDefault();
            state.scaleFromDesignCanvas = true;
            state.designScaleMode = ResponsiveDesignScaleMode.IndependentXY;
            // Built-in canvas (1080x1920, landscape 1920x1080): the project's Wide Screen
            // Design Canvas may differ (this project copied Pad's).
            state.designCanvasSize =
                ResponsiveRectState.GetBuiltinDesignCanvasSize(ResponsiveDevice.FormFactor.WideScreen);
            state.defaultScale = Vector2.one;

            Vector2 ratio = state.ComputeLayoutRatio(
                new Vector2(width, height),
                ResponsiveDevice.FormFactor.WideScreen,
                ResponsiveDevice.Orientation.Landscape);

            Assert.AreEqual(expectedX, ratio.x, 1e-4f);
            Assert.AreEqual(expectedY, ratio.y, 1e-4f);
        }

        [Test]
        public void WideScreen_BuiltinCanvas_IsPortraitStored_LandscapeOriented()
        {
            Assert.AreEqual(
                new Vector2(1080f, 1920f),
                ResponsiveRectState.GetBuiltinDesignCanvasSize(ResponsiveDevice.FormFactor.WideScreen));
            Assert.AreEqual(
                new Vector2(1920f, 1080f),
                ResponsiveUI.OrientSize(
                    ResponsiveRectState.GetBuiltinDesignCanvasSize(ResponsiveDevice.FormFactor.WideScreen),
                    ResponsiveDevice.Orientation.Landscape));
        }

        [Test]
        public void WideScreen_FallsBackOnlyToItsOtherOrientation_NeverMobileSlots()
        {
            var landscape = new ResponsiveDevice.LayoutKey(
                ResponsiveDevice.FormFactor.WideScreen,
                ResponsiveDevice.Orientation.Landscape);
            Assert.IsTrue(ResponsiveWidgetSlotResolver.GetCandidate(landscape, 0, out var form, out var orientation));
            Assert.AreEqual((ResponsiveDevice.FormFactor.WideScreen, ResponsiveDevice.Orientation.Landscape), (form, orientation));
            Assert.IsTrue(ResponsiveWidgetSlotResolver.GetCandidate(landscape, 1, out form, out orientation));
            Assert.AreEqual((ResponsiveDevice.FormFactor.WideScreen, ResponsiveDevice.Orientation.Portrait), (form, orientation));
            for (int i = 2; i < ResponsiveWidgetSlotResolver.MaxCandidates; i++)
                Assert.IsFalse(ResponsiveWidgetSlotResolver.GetCandidate(landscape, i, out _, out _), "#" + i);

            // Pad / Phone authored, Wide Screen Landscape disabled: Wide Screen Portrait, never Pad.
            var widget = new ResponsiveWidgetLayout
            {
                padLandscape = new ResponsiveRectState { enabled = true },
                phoneLandscape = new ResponsiveRectState { enabled = true },
                wideScreenPortrait = new ResponsiveRectState { enabled = true },
            };
            Assert.IsTrue(widget.ResolveActiveSlot(landscape, out string slot, out _, out _, out _));
            Assert.AreEqual("wideScreenPortrait", slot);

            // No Wide Screen slot enabled: nothing applies (host fields stay as they are).
            widget.wideScreenPortrait = new ResponsiveRectState { enabled = false };
            Assert.IsFalse(widget.ResolveActiveSlot(landscape, out _, out _, out _, out _));
        }

        [TestCase(ResponsivePlatform.Android)]
        [TestCase(ResponsivePlatform.Ios)]
        public void WideScreen_IsOffOnMobile(ResponsivePlatform platform)
        {
            WithPlatform(platform, () =>
            {
                var key = new ResponsiveDevice.LayoutKey(
                    ResponsiveDevice.FormFactor.WideScreen,
                    ResponsiveDevice.Orientation.Landscape);
                for (int i = 0; i < ResponsiveWidgetSlotResolver.MaxCandidates; i++)
                    Assert.IsFalse(ResponsiveWidgetSlotResolver.GetCandidate(key, i, out _, out _), "#" + i);
                Assert.AreNotEqual(
                    ResponsiveDevice.FormFactor.WideScreen,
                    ResponsiveDevice.DetectActiveFormFactor(new Vector2(1920f, 1080f), 1.6f, true));
            });
        }

        [TestCase(ResponsivePlatform.Android, false, TestName = "ScrollSeed_WideScreenOff_Android")]
        [TestCase(ResponsivePlatform.Ios, false, TestName = "ScrollSeed_WideScreenOff_Ios")]
        [TestCase(ResponsivePlatform.WideScreen, true, TestName = "ScrollSeed_WideScreenOn_WideScreen")]
        public void ScrollSeedAll_WideScreenSlots_SeedEnabledOnlyOnWideScreenTargets(
            ResponsivePlatform platform,
            bool expectedWideScreen)
        {
            WithPlatform(platform, () =>
            {
                Assert.AreEqual(
                    expectedWideScreen,
                    ResponsivePlatforms.SeedsEnabled(ResponsiveDevice.FormFactor.WideScreen));
                Assert.IsTrue(ResponsivePlatforms.SeedsEnabled(ResponsiveDevice.FormFactor.Pad));

                var layout = new ResponsiveScrollLayout();
                layout.SeedAll(0.2f, 0.5f, enablePortrait: true, enableLandscape: true);
                Assert.AreEqual(expectedWideScreen, layout.wideScreenPortrait.enabled, "wideScreenPortrait");
                Assert.AreEqual(expectedWideScreen, layout.wideScreenLandscape.enabled, "wideScreenLandscape");
                Assert.IsTrue(layout.padLandscape.enabled, "other slots stay platform-neutral");
                Assert.AreEqual(0.2f, layout.wideScreenLandscape.cellInterval, "metrics seeded either way");
            });
        }

        [Test]
        public void OverrideInEditor_PhoneOnWideScreenTarget_CapturesAndAppliesWideScreen()
        {
            var go = new GameObject("WideOverrideHub", typeof(RectTransform), typeof(Canvas));
            try
            {
                var hub = go.AddComponent<ResponsiveUI>();
                SetHubOverride(hub, ResponsiveDevice.FormFactor.Phone, ResponsiveDevice.Orientation.Portrait);
                WithPlatform(ResponsivePlatform.WideScreen, () =>
                {
                    Assert.AreEqual(ResponsiveDevice.FormFactor.WideScreen, hub.EditorFormFactor);
                    ResponsiveDevice.LayoutKey capture = hub.GetEditorCaptureLayout();
                    Assert.AreEqual(ResponsiveDevice.FormFactor.WideScreen, capture.Form);
                    Assert.AreEqual(ResponsiveDevice.Orientation.Portrait, capture.Orientation);
                });
                WithPlatform(ResponsivePlatform.Ios, () =>
                {
                    SetHubOverride(hub, ResponsiveDevice.FormFactor.WideScreen, ResponsiveDevice.Orientation.Landscape);
                    Assert.AreEqual(ResponsiveDevice.FormFactor.Pad, hub.GetEditorCaptureLayout().Form);
                });
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void WidgetApply_NoEnabledCandidate_LeavesTargetUnchanged()
        {
            var go = new GameObject("WidgetTarget", typeof(RectTransform));
            try
            {
                var target = (RectTransform)go.transform;
                target.anchoredPosition = new Vector2(7f, 8f);
                target.sizeDelta = new Vector2(70f, 80f);

                ResponsiveRectState phone = ResponsiveRectState.CreateDefault();
                phone.anchoredPosition = new Vector2(123f, 456f);
                phone.sizeDelta = new Vector2(300f, 400f);
                var widget = new ResponsiveWidgetLayout { target = target, phonePortrait = phone };
                var key = new ResponsiveDevice.LayoutKey(
                    ResponsiveDevice.FormFactor.Phone,
                    ResponsiveDevice.Orientation.Portrait);

                WithPlatform(ResponsivePlatform.WideScreen, () =>
                {
                    Assert.IsFalse(widget.ResolveActiveSlot(key, out _, out _, out _, out _));
                    widget.Apply(key, new Vector2(1080f, 1920f));
                });

                Assert.AreEqual(new Vector2(7f, 8f), target.anchoredPosition);
                Assert.AreEqual(new Vector2(70f, 80f), target.sizeDelta);

                // Same widget on a mobile target applies the enabled Phone slot.
                WithPlatform(ResponsivePlatform.Android, () => widget.Apply(key, new Vector2(1080f, 1920f)));
                Assert.AreEqual(new Vector2(123f, 456f), target.anchoredPosition);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [TestCase(false, TestName = "CurrentFormFactor_Unconstrained_MatchesActiveDetection")]
        [TestCase(true, TestName = "CurrentFormFactor_Constrained_MatchesActiveDetection")]
        public void CurrentFormFactor_NoPin_MatchesDetectActiveFormFactor(bool constrained)
        {
            System.Action check = () =>
            {
                Assert.IsFalse(ResponsiveDevice.HasSimulatorLayout);
                Assert.AreEqual(
                    ResponsiveDevice.DetectActiveFormFactor(ResponsiveDevice.GetWatchScreenSize()),
                    ResponsiveDevice.CurrentFormFactor);
            };
            if (constrained)
            {
                WithPlatform(ResponsivePlatforms.BuildPlatform, check);
                return;
            }

            ResponsiveDevice.ClearOverride();
            ResponsiveDevice.ClearSimulatorLayout();
            check();
        }

        [Test]
        public void DesignCanvasCatalog_KeepsDuoAndGalaxyFamiliesApart()
        {
            Assert.IsFalse(ResponsiveUiEditorSettings.ResolveCatalogSize(
                ResponsiveDevice.FormFactor.FoldHalfOpened, ResponsiveCatalogDevices.IphoneDuoId,
                ResponsiveDevice.Orientation.Portrait, out _, out _, out string error));
            Assert.IsNotEmpty(error);
            Assert.IsFalse(ResponsiveUiEditorSettings.ResolveCatalogSize(
                ResponsiveDevice.FormFactor.iPhoneDuoSeated, ResponsiveCatalogDevices.FoldPhoneId,
                ResponsiveDevice.Orientation.Portrait, out _, out _, out error));
            Assert.IsNotEmpty(error);

            Assert.IsTrue(ResponsiveUiEditorSettings.ResolveCatalogSize(
                ResponsiveDevice.FormFactor.iPhoneDuoSeated, ResponsiveCatalogDevices.IphoneDuoId,
                ResponsiveDevice.Orientation.Portrait, out _, out _, out _));
            Assert.IsTrue(ResponsiveUiEditorSettings.ResolveCatalogSize(
                ResponsiveDevice.FormFactor.FoldHalfOpened, ResponsiveCatalogDevices.FoldPhoneId,
                ResponsiveDevice.Orientation.Portrait, out _, out _, out _));
        }

        // Fold / Fold Cover Half Opened name the posture: background art / catalog sizes for
        // Portrait are the wide flat screen, Landscape the tall one. The Design Canvas stays tall.
        [TestCase(ResponsiveDevice.FormFactor.FoldHalfOpened)]
        [TestCase(ResponsiveDevice.FormFactor.FoldCoverHalfOpened)]
        public void CatalogSize_FoldHalfOpen_PortraitWide_LandscapeTall(ResponsiveDevice.FormFactor form)
        {
            string id = ResponsiveUiEditorSettings.GetPreferredProfileId(form);
            Assert.IsTrue(ResponsiveUiEditorSettings.ResolveCatalogSize(
                form, id, ResponsiveDevice.Orientation.Portrait, out _, out Vector2 portrait, out _));
            Assert.IsTrue(ResponsiveUiEditorSettings.ResolveCatalogSize(
                form, id, ResponsiveDevice.Orientation.Landscape, out _, out Vector2 landscape, out _));
            Assert.IsTrue(ResponsiveUiEditorSettings.ResolveCatalogCanvas(
                form, id, out _, out Vector2 design, out _));

            Assert.Greater(portrait.x, portrait.y, "Portrait is wide");
            Assert.Less(landscape.x, landscape.y, "Landscape is tall");
            Assert.Less(design.x, design.y, "Design Canvas stays tall");
            Assert.Greater(
                ResponsiveUI.OrientSizeForSlot(design, form, ResponsiveDevice.Orientation.Portrait).x,
                design.x, "Follow Design Canvas Portrait is wide");
        }

        // Design canvas per pose row: the Landscape row is the Portrait row rotated until it is set.
        [Test]
        public void DesignCanvas_UnsetLandscapeRow_IsSwapOfPortrait()
        {
            const ResponsiveDevice.FormFactor form = ResponsiveDevice.FormFactor.FoldCoverHalfOpened;
            Vector2 canvas = ResponsiveRectState.BuiltinFoldCoverHalfOpenedDesignCanvasSize;
            Assert.AreEqual(new Vector2(1848f, 2448f), ResponsiveUiEditorSettings.RowDesignCanvasSize(
                canvas, Vector2.zero, form, ResponsiveDevice.Orientation.Landscape), "Landscape slot is tall");
            Assert.AreEqual(new Vector2(2448f, 1848f), ResponsiveUiEditorSettings.RowDesignCanvasSize(
                canvas, Vector2.zero, form, ResponsiveDevice.Orientation.Portrait), "Portrait slot is wide");
            Assert.AreEqual(new Vector2(1920f, 1080f), ResponsiveUiEditorSettings.RowDesignCanvasSize(
                new Vector2(1080f, 1920f), Vector2.zero,
                ResponsiveDevice.FormFactor.Phone, ResponsiveDevice.Orientation.Landscape));
        }

        [Test]
        public void DesignCanvas_LandscapeRowSet_UsedOnlyForThatRow()
        {
            const ResponsiveDevice.FormFactor form = ResponsiveDevice.FormFactor.FoldCoverHalfOpened;
            ResponsiveUiEditorSettings settings = ResponsiveUiEditorSettings.instance;
            ResponsiveUiEditorSettings.DesignFormDefaults original = ResponsiveUiEditorSettings.GetFormDefaults(form);
            Vector2 portrait = ResponsiveUiEditorSettings.GetDesignCanvasSize(form, ResponsiveDevice.Orientation.Portrait);
            try
            {
                settings.SetDesignCanvasSize(form, ResponsiveDevice.Orientation.Landscape, new Vector2(1700f, 2300f));
                Assert.AreEqual(new Vector2(1700f, 2300f),
                    ResponsiveUiEditorSettings.GetDesignCanvasSize(form, ResponsiveDevice.Orientation.Landscape));
                Assert.AreEqual(portrait,
                    ResponsiveUiEditorSettings.GetDesignCanvasSize(form, ResponsiveDevice.Orientation.Portrait));
                Assert.AreEqual(original.designCanvasSize,
                    ResponsiveUiEditorSettings.GetFormDefaults(form).designCanvasSize, "per-form canvas kept");
            }
            finally
            {
                settings.SetFormDefaults(form, original);
            }

            Assert.AreEqual(Vector2.zero, ResponsiveUiEditorSettings.GetFormDefaults(form).landscapeDesignCanvasSize);
        }

        // A catalog reference device gives the Portrait row (R18): the pose cards show it read-only and a
        // Portrait write is dropped, as it could not take effect. Landscape stays editable; Wide Screen has no device.
        [Test]
        public void DesignCanvas_PortraitRowFromReference_ReadOnlyAndNotWritten()
        {
            const ResponsiveDevice.FormFactor form = ResponsiveDevice.FormFactor.FoldHalfOpened;
            ResponsiveUiEditorSettings settings = ResponsiveUiEditorSettings.instance;
            ResponsiveUiEditorSettings.DesignFormDefaults original = ResponsiveUiEditorSettings.GetFormDefaults(form);
            Assert.IsTrue(ResponsiveUiEditorSettings.DesignCanvasFromReference(form));
            Assert.IsFalse(ResponsiveUiEditorSettings.DesignCanvasFromReference(ResponsiveDevice.FormFactor.WideScreen));
            Assert.IsTrue(ResponsivePoseCanvasHook.IsFromReference((int)form, false), "Portrait row read-only");
            Assert.IsFalse(ResponsivePoseCanvasHook.IsFromReference((int)form, true), "Landscape row editable");
            Assert.IsFalse(ResponsivePoseCanvasHook.IsFromReference((int)ResponsiveDevice.FormFactor.WideScreen, false));

            Vector2 portrait = ResponsiveUiEditorSettings.GetDesignCanvasSize(form, ResponsiveDevice.Orientation.Portrait);
            try
            {
                settings.SetDesignCanvasSize(form, ResponsiveDevice.Orientation.Portrait, new Vector2(3000f, 1500f));
                Assert.AreEqual(original.designCanvasSize,
                    ResponsiveUiEditorSettings.GetFormDefaults(form).designCanvasSize, "ignored value not stored");
                Assert.AreEqual(portrait,
                    ResponsiveUiEditorSettings.GetDesignCanvasSize(form, ResponsiveDevice.Orientation.Portrait));
            }
            finally
            {
                settings.SetFormDefaults(form, original);
            }
        }

        // Capture (Editor unscale) and Apply (runtime rescale) share the per-form canvas, so a
        // Landscape row override never breaks the camera round trip.
        [Test]
        public void DesignCanvas_LandscapeRowSet_CameraCaptureRoundTrips()
        {
            // Pad: no posture identity, and 1800×1000 is not the reference device (ratio ≠ 1).
            const ResponsiveDevice.FormFactor form = ResponsiveDevice.FormFactor.Pad;
            const ResponsiveDevice.Orientation slot = ResponsiveDevice.Orientation.Landscape;
            ResponsiveUiEditorSettings settings = ResponsiveUiEditorSettings.instance;
            ResponsiveUiEditorSettings.DesignFormDefaults original = ResponsiveUiEditorSettings.GetFormDefaults(form);
            try
            {
                settings.SetDesignCanvasSize(form, slot, new Vector2(2000f, 900f));
                Vector2 current = new Vector2(1800f, 1000f);
                Vector3 livePosition = new Vector3(1f, 2f, -10f);
                const float liveOrtho = 5f;
                const float liveFov = 60f;
                foreach (ResponsiveDesignScaleMode mode in new[]
                         { ResponsiveDesignScaleMode.Width, ResponsiveDesignScaleMode.Height })
                {
                    Vector3 position = livePosition;
                    float ortho = liveOrtho;
                    float fov = liveFov;
                    ResponsiveCameraEditorUtil.UnscaleLiveToDesign(
                        form, slot, current, mode, true, true, ref position, ref ortho, ref fov);

                    var state = new ResponsiveCameraState();
                    Vector2 design = state.GetResolvedDesignUiSize(form, slot);
                    Vector2 live = state.ResolveLiveUiSize(current, form, slot);
                    Vector3 applied = ResponsiveCameraState.ScaleDesignPosition(
                        position, ResponsiveRectState.ComputeCanvasRatio(live, design, mode));
                    Assert.AreEqual(livePosition.x, applied.x, 1e-3f, $"{mode} position x");
                    Assert.AreEqual(livePosition.y, applied.y, 1e-3f, $"{mode} position y");
                    Assert.AreEqual(livePosition.z, applied.z, 1e-3f, $"{mode} position z");
                    Assert.AreEqual(liveOrtho,
                        ResponsiveCameraState.ScaleOrthographicSize(ortho, live, design, mode), 1e-3f, $"{mode} ortho");
                    Assert.AreEqual(liveFov,
                        ResponsiveCameraState.ScaleFieldOfView(fov, live, design, mode), 1e-2f, $"{mode} fov");
                }
            }
            finally
            {
                settings.SetFormDefaults(form, original);
            }
        }

        // A newly seeded Landscape slot stores the set Landscape row explicitly, so runtime and
        // capture read it from the slot; an unset row seeds (0,0) exactly as before.
        [Test]
        public void DesignCanvas_LandscapeRowSet_SeedsLandscapeSlot()
        {
            const ResponsiveDevice.FormFactor form = ResponsiveDevice.FormFactor.Phone;
            ResponsiveUiEditorSettings settings = ResponsiveUiEditorSettings.instance;
            ResponsiveUiEditorSettings.DesignFormDefaults original = ResponsiveUiEditorSettings.GetFormDefaults(form);
            Assert.AreEqual(Vector2.zero, SeededDesignCanvas("phoneLandscape"), "unset row: Landscape (0,0)");
            Assert.AreEqual(Vector2.zero, SeededDesignCanvas("phonePortrait"), "unset row: Portrait (0,0)");
            try
            {
                settings.SetDesignCanvasSize(form, ResponsiveDevice.Orientation.Landscape, new Vector2(2000f, 900f));
                Assert.AreEqual(new Vector2(2000f, 900f), SeededDesignCanvas("phoneLandscape"));
                Assert.AreEqual(Vector2.zero, SeededDesignCanvas("phonePortrait"), "Portrait slot unchanged");
                Assert.AreEqual(Vector2.zero, SeededDesignCanvas("padLandscape"), "other forms unchanged");
            }
            finally
            {
                settings.SetFormDefaults(form, original);
            }
        }

        static Vector2 SeededDesignCanvas(string slotName)
        {
            var go = new GameObject("ResponsivePoseRowSeedTest");
            try
            {
                var so = new UnityEditor.SerializedObject(go.AddComponent<ResponsiveUiController>());
                so.FindProperty("widgets").arraySize = 1;
                so.ApplyModifiedPropertiesWithoutUndo();
                so.Update();
                UnityEditor.SerializedProperty widget = so.FindProperty("widgets").GetArrayElementAtIndex(0);
                ResponsiveRectStateEditorUtil.SeedSlots(widget);
                return widget.FindPropertyRelative(slotName)
                    .FindPropertyRelative(ResponsiveRectStateEditorUtil.DesignCanvasSizeProp).vector2Value;
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void DesignCanvas_ExistingAssetWithoutField_Unchanged()
        {
            ResponsiveUiEditorSettings.DesignFormDefaults old =
                JsonUtility.FromJson<ResponsiveUiEditorSettings.DesignFormDefaults>(
                    "{\"profileId\":\"\",\"modelName\":\"x\",\"designCanvasSize\":{\"x\":1848,\"y\":2448}," +
                    "\"defaultScale\":{\"x\":1,\"y\":1}}");
            Assert.AreEqual(Vector2.zero, old.landscapeDesignCanvasSize);

            foreach (ResponsiveDevice.FormFactor form in ResponsiveWidgetLayout.FormFactors)
            {
                foreach (ResponsiveDevice.Orientation slot in new[]
                         { ResponsiveDevice.Orientation.Portrait, ResponsiveDevice.Orientation.Landscape })
                {
                    Vector2 today = ResponsiveUI.OrientSizeForSlot(
                        ResponsiveUiEditorSettings.GetDesignCanvasSize(form), form, slot);
                    Assert.AreEqual(today, ResponsiveUiEditorSettings.RowDesignCanvasSize(
                        ResponsiveUiEditorSettings.GetDesignCanvasSize(form),
                        old.landscapeDesignCanvasSize, form, slot), $"{form} {slot}");
                }
            }
        }

        [Test]
        public void DesignCanvasCatalog_WideScreenHasNoReferenceDevice()
        {
            const ResponsiveDevice.FormFactor wide = ResponsiveDevice.FormFactor.WideScreen;
            Assert.AreEqual("", ResponsiveUiEditorSettings.GetPreferredProfileId(wide));
            Assert.AreEqual("", ResponsiveUiEditorSettings.GetFormDefaults(wide).profileId, "stored profileId self-heals");
            Assert.IsFalse(ResponsiveUiEditorSettings.ResolveCatalogSize(
                wide, ResponsiveCatalogDevices.PadId,
                ResponsiveDevice.Orientation.Landscape, out _, out _, out _));

            // The iPad is a Design Canvas reference only through Pad (never a Fold / Wide Screen one).
            Assert.IsTrue(ResponsiveUiEditorSettings.IsDesignReferenceProfileId(ResponsiveCatalogDevices.PadId));
            foreach (ResponsiveDevice.FormFactor form in ResponsiveWidgetLayout.FormFactors)
            {
                if (form == ResponsiveDevice.FormFactor.Pad)
                    continue;
                Assert.AreNotEqual(
                    ResponsiveCatalogDevices.PadId,
                    ResponsiveUiEditorSettings.GetFormDefaults(form).profileId,
                    form.ToString());
            }

            // Wide Screen Default Scale is editable and keeps no catalog device.
            ResponsiveUiEditorSettings settings = ResponsiveUiEditorSettings.instance;
            ResponsiveUiEditorSettings.DesignFormDefaults original = ResponsiveUiEditorSettings.GetFormDefaults(wide);
            try
            {
                ResponsiveUiEditorSettings.DesignFormDefaults edited = original;
                edited.defaultScale = new Vector2(2f, 2f);
                settings.SetFormDefaults(wide, edited);

                ResponsiveUiEditorSettings.DesignFormDefaults readBack = ResponsiveUiEditorSettings.GetFormDefaults(wide);
                Assert.AreEqual(new Vector2(2f, 2f), readBack.defaultScale);
                Assert.AreEqual(new Vector2(2f, 2f), ResponsiveUiEditorSettings.GetDefaultScale(wide));
                Assert.AreEqual("", readBack.profileId);
                Assert.AreEqual(original.designCanvasSize, readBack.designCanvasSize);
            }
            finally
            {
                settings.SetFormDefaults(wide, original);
            }

            Assert.AreEqual(original.defaultScale, ResponsiveUiEditorSettings.GetFormDefaults(wide).defaultScale);
        }

        [Test]
        public void WideScreen_SeedCandidates_PadThenPhoneByShape()
        {
            foreach (ResponsiveDevice.Orientation screen in new[]
                     {
                         ResponsiveDevice.Orientation.Landscape,
                         ResponsiveDevice.Orientation.Portrait,
                     })
            {
                ResponsiveDevice.Orientation other = screen == ResponsiveDevice.Orientation.Landscape
                    ? ResponsiveDevice.Orientation.Portrait
                    : ResponsiveDevice.Orientation.Landscape;
                var key = new ResponsiveDevice.LayoutKey(ResponsiveDevice.FormFactor.WideScreen, screen);
                var expected = new[]
                {
                    (ResponsiveDevice.FormFactor.WideScreen, screen),
                    (ResponsiveDevice.FormFactor.Pad, screen),
                    (ResponsiveDevice.FormFactor.Pad, other),
                    (ResponsiveDevice.FormFactor.Phone, screen),
                    (ResponsiveDevice.FormFactor.Phone, other),
                };
                for (int i = 0; i < expected.Length; i++)
                {
                    Assert.IsTrue(
                        ResponsiveWidgetSlotResolver.GetSeedCandidate(key, i, out var form, out var orientation),
                        screen + " #" + i);
                    Assert.AreEqual(expected[i], (form, orientation), screen + " #" + i);
                }
                for (int i = expected.Length; i < ResponsiveWidgetSlotResolver.MaxCandidates; i++)
                    Assert.IsFalse(ResponsiveWidgetSlotResolver.GetSeedCandidate(key, i, out _, out _), screen + " #" + i);
            }
        }

        [Test]
        public void WideScreenBuild_NeverUsesMobileSlots()
        {
            WithPlatform(ResponsivePlatform.WideScreen, () =>
            {
                var pad = new ResponsiveDevice.LayoutKey(
                    ResponsiveDevice.FormFactor.Pad,
                    ResponsiveDevice.Orientation.Landscape);
                Assert.IsFalse(ResponsiveWidgetSlotResolver.GetCandidate(pad, 0, out _, out _));
            });
        }

        [Test]
        public void WideScreen_IgnoresPlayerOrientationLock()
        {
            foreach (ResponsiveDevice.Orientation orientation in new[]
                     {
                         ResponsiveDevice.Orientation.Portrait,
                         ResponsiveDevice.Orientation.Landscape,
                     })
            {
                Assert.IsTrue(ResponsiveDevice.SlotAllowedByPlayerAxes(
                    ResponsiveDevice.FormFactor.WideScreen, orientation, true, false));
                Assert.IsTrue(ResponsiveDevice.SlotAllowedByPlayerAxes(
                    ResponsiveDevice.FormFactor.WideScreen, orientation, false, true));
            }
        }

        [Test]
        public void IphoneDuoCover_DisabledSlot_FallsBackToPhone()
        {
            var key = new ResponsiveDevice.LayoutKey(
                ResponsiveDevice.FormFactor.iPhoneDuoCover,
                ResponsiveDevice.Orientation.Landscape);
            Assert.IsTrue(ResponsiveWidgetSlotResolver.GetCandidate(key, 2, out var form, out var orientation));
            Assert.AreEqual(ResponsiveDevice.FormFactor.Phone, form);
            Assert.AreEqual(ResponsiveDevice.Orientation.Landscape, orientation);
            Assert.AreEqual(
                new Vector2(1398f, 2034f),
                ResponsiveRectState.GetBuiltinDesignCanvasSize(ResponsiveDevice.FormFactor.iPhoneDuoCover));
        }

        [Test]
        public void Android_NeverDetectsIphoneDuoCover()
        {
            WithPlatform(ResponsivePlatform.Android, () =>
            {
                Assert.AreNotEqual(
                    ResponsiveDevice.FormFactor.iPhoneDuoCover,
                    ResponsiveDevice.DetectActiveFormFactor(new Vector2(1398f, 2034f), 1.6f, true));
            });
        }

        [Test]
        public void Ios_DetectsIphoneDuoCover_NeverFoldCover()
        {
            WithPlatform(ResponsivePlatform.Ios, () =>
            {
                Assert.AreEqual(
                    ResponsiveDevice.FormFactor.iPhoneDuoCover,
                    ResponsiveDevice.DetectActiveFormFactor(new Vector2(1398f, 2034f), 1.6f, true));
                Assert.AreNotEqual(
                    ResponsiveDevice.FormFactor.FoldPhoneCover,
                    ResponsiveDevice.DetectActiveFormFactor(new Vector2(1248f, 1972f), 1.6f, true));
            });
        }

        [Test]
        public void SimulatorProfile_KeepsItsFormOnEveryBuildTarget()
        {
            WithPlayerAxes(true, true, () =>
            {
                try
                {
                    AssertProfile(
                        ResponsivePlatform.Ios,
                        ResponsiveDevice.FormFactor.Phone,
                        ResponsiveDevice.FormFactor.FoldPhoneCover,
                        ResponsiveDevice.Orientation.Landscape,
                        1972,
                        1248);
                    AssertProfile(
                        ResponsivePlatform.WideScreen,
                        ResponsiveDevice.FormFactor.Phone,
                        ResponsiveDevice.FormFactor.FoldPhoneCover,
                        ResponsiveDevice.Orientation.Landscape,
                        1972,
                        1248);
                    AssertProfile(
                        ResponsivePlatform.Android,
                        ResponsiveDevice.FormFactor.iPhoneDuoCover,
                        ResponsiveDevice.FormFactor.iPhoneDuoCover,
                        ResponsiveDevice.Orientation.Portrait,
                        1398,
                        2034);
                    AssertProfile(
                        ResponsivePlatform.WideScreen,
                        ResponsiveDevice.FormFactor.iPhoneDuoSeated,
                        ResponsiveDevice.FormFactor.iPhoneDuoSeated,
                        ResponsiveDevice.Orientation.Portrait,
                        2007,
                        2853);
                    AssertProfile(
                        ResponsivePlatform.WideScreen,
                        ResponsiveDevice.FormFactor.Phone,
                        ResponsiveDevice.FormFactor.Phone,
                        ResponsiveDevice.Orientation.Landscape,
                        2400,
                        1080);
                }
                finally
                {
                    ResponsiveDevice.ClearSimulatorLayout();
                }
            });
        }

        static void AssertProfile(
            ResponsivePlatform platform,
            ResponsiveDevice.FormFactor pinned,
            ResponsiveDevice.FormFactor expected,
            ResponsiveDevice.Orientation orientation,
            int width,
            int height)
        {
            WithPlatform(platform, () =>
            {
                ResponsiveDevice.SetSimulatorLayout(pinned, orientation, width, height);
                var size = new Vector2(width, height);
                Assert.AreEqual(
                    expected,
                    ResponsiveDevice.DetectActiveFormFactor(size, 1.6f, true),
                    platform + " " + pinned);
                var key = new ResponsiveDevice.LayoutKey(expected, orientation);
                Assert.IsTrue(ResponsiveWidgetSlotResolver.GetCandidate(
                    key, 0, out var form, out var slotOrientation));
                Assert.AreEqual(expected, form);
                Assert.AreEqual(orientation, slotOrientation);
                Assert.IsTrue(ResponsiveUiEditorSettings.SlotVisible(
                    ResponsiveWidgetLayout.StatePropertyName(key)));
            });
        }

        [Test]
        public void SlotIndex_RoundTripsAllTwentySlots()
        {
            string[] names = ResponsiveWidgetLayout.StatePropertyNames;
            Assert.AreEqual(20, names.Length);
            for (int i = 0; i < names.Length; i++)
            {
                var key = new ResponsiveDevice.LayoutKey(
                    ResponsiveWidgetLayout.SlotFormFactor(i),
                    ResponsiveWidgetLayout.SlotOrientation(i));
                Assert.AreEqual(i, ResponsiveWidgetLayout.StatePropertyIndex(key), names[i]);
                Assert.AreEqual(i, ResponsiveWidgetLayout.StatePropertyIndex(names[i]), names[i]);
                Assert.AreEqual(names[i], ResponsiveWidgetLayout.StatePropertyName(key));
                Assert.AreEqual(i >> 1, (int)key.Form, names[i]);
            }
        }

        [TestCase(ResponsivePlatform.Android, TestName = "SlotVisible_Android")]
        [TestCase(ResponsivePlatform.Ios, TestName = "SlotVisible_Ios")]
        [TestCase(ResponsivePlatform.WideScreen, TestName = "SlotVisible_WideScreen")]
        public void SlotVisible_ListsAndroidAndIosOnEveryTarget_WideScreenFollowsBuildTarget(
            ResponsivePlatform platform)
        {
            WithPlayerAxes(true, true, () =>
            {
                WithPlatform(platform, () =>
                {
                    string[] names = ResponsiveWidgetLayout.StatePropertyNames;
                    string previous = "";
                    for (int i = 0; i < names.Length; i++)
                    {
                        ResponsiveDevice.FormFactor form = ResponsiveWidgetLayout.SlotFormFactor(i);
                        bool wide = form == ResponsiveDevice.FormFactor.WideScreen;
                        Assert.AreEqual(
                            !wide || platform == ResponsivePlatform.WideScreen,
                            ResponsiveUiEditorSettings.SlotVisible(i),
                            names[i]);
                        // Seeding / sync keep every platform's data; Wide Screen seeds only on its targets.
                        Assert.AreEqual(
                            !wide || platform == ResponsivePlatform.WideScreen,
                            ResponsiveUiEditorSettings.SlotAllowed(i),
                            names[i]);
                        if (!ResponsiveUiEditorSettings.SlotVisible(i))
                            continue;
                        string group = ResponsiveWidgetLayout.SlotGroupLabel(form);
                        if (group == previous)
                            continue;
                        if (previous == "")
                            Assert.AreEqual("Android", group);
                        else if (previous == "Android")
                            Assert.AreEqual("iOS", group);
                        else
                            Assert.AreEqual("Wide Screen", group);
                        previous = group;
                    }
                });
            });
        }

        [Test]
        public void FormDisplayName_IosReadsIphoneAndIpad()
        {
            WithPlatform(ResponsivePlatform.Ios, () =>
            {
                Assert.AreEqual("iPhone", ResponsiveDevice.GetFormDisplayName(ResponsiveDevice.FormFactor.Phone));
                Assert.AreEqual("iPad", ResponsiveDevice.GetFormDisplayName(ResponsiveDevice.FormFactor.Pad));
            });
            WithPlatform(ResponsivePlatform.Android, () =>
            {
                Assert.AreEqual("Phone", ResponsiveDevice.GetFormDisplayName(ResponsiveDevice.FormFactor.Phone));
                Assert.AreEqual("Flip Cover", ResponsiveDevice.GetFormDisplayName(ResponsiveDevice.FormFactor.FlipPhoneCover));
            });
        }

        [Test]
        public void ApplyScaler_WideScreen_UsesItsOwnReference()
        {
            var go = new GameObject("WideScalerHub", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
            try
            {
                var scaler = go.GetComponent<UnityEngine.UI.CanvasScaler>();
                var hub = go.AddComponent<ResponsiveUI>();
                var so = new UnityEditor.SerializedObject(hub);
                so.FindProperty("applyCanvasScaler").boolValue = true;
                so.FindProperty("canvasScaler").objectReferenceValue = scaler;
                so.FindProperty("padReferenceResolution").vector2Value = new Vector2(111f, 222f);
                so.ApplyModifiedPropertiesWithoutUndo();
                SetHubOverride(
                    hub, ResponsiveDevice.FormFactor.WideScreen, ResponsiveDevice.Orientation.Landscape);

                hub.Apply();

                Assert.AreEqual(new Vector2(1920f, 1080f), scaler.referenceResolution);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }
    }
}
