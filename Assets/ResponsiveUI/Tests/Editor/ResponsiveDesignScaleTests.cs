using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using FDPlus.ResponsiveUI;
using FDPlus.ResponsiveUI.Editor;

namespace FDPlus.ResponsiveUI.Tests
{
    /// <summary>
    /// Editor unit tests for Responsive UI design-scale math (run via Unity Test Runner).
    /// </summary>
    public class ResponsiveDesignScaleTests
    {
        [TearDown]
        public void TearDown()
        {
            ResponsiveDevice.ClearSimulatorLayout();
            ResponsiveDevice.ClearOverride();
        }

        [Test]
        public void CreateDefault_UsesAutoScaleModeAndUnsetDesignCanvas()
        {
            var state = ResponsiveRectState.CreateDefault();
            Assert.AreEqual(ResponsiveDesignScaleMode.Auto, state.designScaleMode);
            Assert.AreEqual(Vector2.zero, state.designCanvasSize);
            Assert.AreEqual(
                ResponsiveDesignScaleMode.Auto,
                ResponsiveCameraState.CreateDefault().uiScaleMode);
        }

        [Test]
        public void From_Rect_UsesAutoScaleModeAndUnsetDesignCanvas()
        {
            var go = new GameObject("FromRect", typeof(RectTransform));
            try
            {
                var state = ResponsiveRectState.From(go.GetComponent<RectTransform>());
                Assert.AreEqual(ResponsiveDesignScaleMode.Auto, state.designScaleMode);
                Assert.AreEqual(Vector2.zero, state.designCanvasSize);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [TestCase(ResponsiveDevice.Orientation.Landscape, true)]
        [TestCase(ResponsiveDevice.Orientation.Portrait, false)]
        public void ApplyScaler_IphoneDuo_PadReferenceFollowsScreenShape(
            ResponsiveDevice.Orientation orientation,
            bool expectWide)
        {
            var go = new GameObject("DuoScalerHub", typeof(Canvas), typeof(CanvasScaler));
            try
            {
                var scaler = go.GetComponent<CanvasScaler>();
                var hub = go.AddComponent<ResponsiveUI>();
                var hubSo = new SerializedObject(hub);
                hubSo.FindProperty("applyCanvasScaler").boolValue = true;
                hubSo.FindProperty("canvasScaler").objectReferenceValue = scaler;
                hubSo.FindProperty("padReferenceResolution").vector2Value =
                    ResponsiveRectState.BuiltinPadDesignCanvasSize;
                hubSo.ApplyModifiedPropertiesWithoutUndo();
                ResponsiveEditorTestUtil.SetHubOverride(
                    hub, ResponsiveDevice.FormFactor.iPhoneDuoSeated, orientation);

                hub.Apply();

                Vector2 pad = ResponsiveRectState.BuiltinPadDesignCanvasSize;
                Vector2 expected = expectWide
                    ? new Vector2(Mathf.Max(pad.x, pad.y), Mathf.Min(pad.x, pad.y))
                    : new Vector2(Mathf.Min(pad.x, pad.y), Mathf.Max(pad.x, pad.y));
                Assert.AreEqual(expected, scaler.referenceResolution);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        // Fold / Fold Cover Half Opened name the posture: Portrait is the wide screen.
        [TestCase(ResponsiveDevice.FormFactor.FoldHalfOpened, ResponsiveDevice.Orientation.Portrait, true)]
        [TestCase(ResponsiveDevice.FormFactor.FoldHalfOpened, ResponsiveDevice.Orientation.Landscape, false)]
        [TestCase(ResponsiveDevice.FormFactor.FoldCoverHalfOpened, ResponsiveDevice.Orientation.Portrait, true)]
        [TestCase(ResponsiveDevice.FormFactor.FoldCoverHalfOpened, ResponsiveDevice.Orientation.Landscape, false)]
        public void ApplyScaler_FoldHalfOpen_PadReferenceFollowsScreenShape(
            ResponsiveDevice.FormFactor form,
            ResponsiveDevice.Orientation orientation,
            bool expectWide)
        {
            var go = new GameObject("FoldScalerHub", typeof(Canvas), typeof(CanvasScaler));
            try
            {
                var scaler = go.GetComponent<CanvasScaler>();
                var hub = go.AddComponent<ResponsiveUI>();
                var hubSo = new SerializedObject(hub);
                hubSo.FindProperty("applyCanvasScaler").boolValue = true;
                hubSo.FindProperty("canvasScaler").objectReferenceValue = scaler;
                hubSo.FindProperty("padReferenceResolution").vector2Value =
                    ResponsiveRectState.BuiltinPadDesignCanvasSize;
                hubSo.ApplyModifiedPropertiesWithoutUndo();
                ResponsiveEditorTestUtil.SetHubOverride(hub, form, orientation);

                hub.Apply();

                Vector2 pad = ResponsiveRectState.BuiltinPadDesignCanvasSize;
                Vector2 expected = expectWide
                    ? new Vector2(Mathf.Max(pad.x, pad.y), Mathf.Min(pad.x, pad.y))
                    : new Vector2(Mathf.Min(pad.x, pad.y), Mathf.Max(pad.x, pad.y));
                Assert.AreEqual(expected, scaler.referenceResolution);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void UsesPadCanvasScalerRefs_FoldHalfOpened_True()
        {
            Assert.IsTrue(
                ResponsiveUiHandler.UsesPadCanvasScalerRefs(ResponsiveDevice.FormFactor.Pad));
            Assert.IsTrue(
                ResponsiveUiHandler.UsesPadCanvasScalerRefs(
                    ResponsiveDevice.FormFactor.FoldHalfOpened));
            Assert.IsTrue(
                ResponsiveUiHandler.UsesPadCanvasScalerRefs(
                    ResponsiveDevice.FormFactor.FoldCoverHalfOpened));
            Assert.IsFalse(
                ResponsiveUiHandler.UsesPadCanvasScalerRefs(ResponsiveDevice.FormFactor.Phone));
            Assert.IsFalse(
                ResponsiveUiHandler.UsesPadCanvasScalerRefs(
                    ResponsiveDevice.FormFactor.FoldPhoneCover));
            Assert.IsFalse(
                ResponsiveUiHandler.UsesPadCanvasScalerRefs(
                    ResponsiveDevice.FormFactor.FlipHalfOpened));
            Assert.IsFalse(
                ResponsiveUiHandler.UsesPadCanvasScalerRefs(
                    ResponsiveDevice.FormFactor.FlipPhoneCover));
        }

        [Test]
        public void ComputeCanvasRatio_Width_UsesWidthUniformly()
        {
            Vector2 phone = ResponsiveCatalogDevices.PhonePortrait;
            Vector2 ratio = ResponsiveRectState.ComputeCanvasRatio(
                phone * 0.5f,
                phone,
                ResponsiveDesignScaleMode.Width);

            Assert.AreEqual(0.5f, ratio.x, 1e-4f);
            Assert.AreEqual(0.5f, ratio.y, 1e-4f);
        }

        [Test]
        public void ComputeCanvasRatio_Height_UsesHeightUniformly()
        {
            Vector2 phone = ResponsiveCatalogDevices.PhonePortrait;
            Vector2 ratio = ResponsiveRectState.ComputeCanvasRatio(
                new Vector2(1000f, phone.y * 0.5f),
                phone,
                ResponsiveDesignScaleMode.Height);

            Assert.AreEqual(0.5f, ratio.x, 1e-4f);
            Assert.AreEqual(0.5f, ratio.y, 1e-4f);
        }

        [Test]
        public void ResolveAutoScaleMode_WiderThanDesign_PicksHeight()
        {
            Vector2 design = ResponsiveCatalogDevices.PhoneLandscape;
            Vector2 live = new Vector2(design.x * 1.2f, design.y);
            Assert.AreEqual(
                ResponsiveDesignScaleMode.Height,
                ResponsiveRectState.ResolveAutoScaleMode(
                    ResponsiveDesignScaleMode.Auto, live, design));
        }

        [Test]
        public void ResolveAutoScaleMode_TallerThanDesign_PicksWidth()
        {
            Vector2 design = ResponsiveCatalogDevices.PhoneLandscape;
            Vector2 live = ResponsiveCatalogDevices.FoldCoverLandscape;
            Assert.AreEqual(
                ResponsiveDesignScaleMode.Width,
                ResponsiveRectState.ResolveAutoScaleMode(
                    ResponsiveDesignScaleMode.Auto, live, design));
        }

        [Test]
        public void ResolveAutoScaleMode_MatchingAspect_PicksWidth()
        {
            Vector2 design = ResponsiveCatalogDevices.PhoneLandscape;
            Assert.AreEqual(
                ResponsiveDesignScaleMode.Width,
                ResponsiveRectState.ResolveAutoScaleMode(
                    ResponsiveDesignScaleMode.Auto, design, design));
        }

        [Test]
        public void ResolveAutoScaleMode_SameLive_PhoneVsPad_CanDiffer()
        {
            Vector2 live = ResponsiveCatalogDevices.FoldCoverLandscape;
            Assert.AreEqual(
                ResponsiveDesignScaleMode.Width,
                ResponsiveRectState.ResolveAutoScaleMode(
                    ResponsiveDesignScaleMode.Auto,
                    live,
                    ResponsiveCatalogDevices.PhoneLandscape));
            Assert.AreEqual(
                ResponsiveDesignScaleMode.Height,
                ResponsiveRectState.ResolveAutoScaleMode(
                    ResponsiveDesignScaleMode.Auto,
                    live,
                    ResponsiveCatalogDevices.PadLandscape));
        }

        [Test]
        public void FormatAutoScaleHint_TallerThanPhone_SaysWidth()
        {
            Vector2 design = ResponsiveCatalogDevices.PhoneLandscape;
            Vector2 live = new Vector2(design.x, design.y * 1.2f);
            string hint = ResponsiveRectStateEditorUtil.FormatAutoScaleHint(
                ResponsiveDevice.FormFactor.Phone,
                ResponsiveDevice.Orientation.Landscape,
                live);
            Assert.That(hint, Does.StartWith("Auto → Width"));
            Assert.That(hint, Does.Contain("taller than"));
        }

        [Test]
        public void FormatAutoScaleHint_WiderThanPhone_SaysHeight()
        {
            Vector2 design = ResponsiveCatalogDevices.PhoneLandscape;
            Vector2 live = new Vector2(design.x * 1.2f, design.y);
            string hint = ResponsiveRectStateEditorUtil.FormatAutoScaleHint(
                ResponsiveDevice.FormFactor.Phone,
                ResponsiveDevice.Orientation.Landscape,
                live);
            Assert.That(hint, Does.StartWith("Auto → Height"));
            Assert.That(hint, Does.Contain("wider than"));
        }

        [Test]
        public void FormatAutoScaleHint_PortraitWatchOnLandscapeSlot_MatchesOrientedDesign()
        {
            string hint = ResponsiveRectStateEditorUtil.FormatAutoScaleHint(
                ResponsiveDevice.FormFactor.Phone,
                ResponsiveDevice.Orientation.Landscape,
                ResponsiveCatalogDevices.PhonePortrait);
            Assert.That(hint, Does.StartWith("Auto → Width"));
            Assert.That(hint, Does.Contain("matches"));
        }

        [Test]
        public void ComputeCanvasRatio_Auto_MatchesResolvedWidthOrHeight()
        {
            Vector2 design = ResponsiveCatalogDevices.PhonePortrait;
            Vector2 live = new Vector2(design.x, design.y * 1.3f);
            Assert.AreEqual(
                ResponsiveDesignScaleMode.Width,
                ResponsiveRectState.ResolveAutoScaleMode(
                    ResponsiveDesignScaleMode.Auto, live, design));
            Vector2 auto = ResponsiveRectState.ComputeCanvasRatio(
                live, design, ResponsiveDesignScaleMode.Auto);
            Vector2 width = ResponsiveRectState.ComputeCanvasRatio(
                live, design, ResponsiveDesignScaleMode.Width);
            Assert.AreEqual(width.x, auto.x, 1e-4f);
            Assert.AreEqual(width.y, auto.y, 1e-4f);
        }

        [Test]
        public void ComputeCanvasRatio_Auto_MatchesUniformMin()
        {
            Vector2 design = ResponsiveCatalogDevices.PhoneLandscape;
            Vector2 live = ResponsiveCatalogDevices.FoldCoverLandscape;
            Vector2 auto = ResponsiveRectState.ComputeCanvasRatio(
                live, design, ResponsiveDesignScaleMode.Auto);
            Vector2 min = ResponsiveRectState.ComputeCanvasRatio(
                live, design, ResponsiveDesignScaleMode.UniformMin);
            Assert.AreEqual(min.x, auto.x, 1e-4f);
            Assert.AreEqual(min.y, auto.y, 1e-4f);
        }

        [Test]
        public void ComputeCanvasRatio_GameViewWidgetVsCatalog_IsNotIdentity()
        {
            Vector2 phone = ResponsiveCatalogDevices.PhonePortrait;
            Vector2 gameView = new Vector2(1221f, 563f);
            Vector2 ratio = ResponsiveRectState.ComputeCanvasRatio(
                gameView,
                phone,
                ResponsiveDesignScaleMode.Width);

            Assert.AreEqual(gameView.x / ResponsiveCatalogDevices.PhoneLandscape.x, ratio.x, 1e-4f);
            Assert.AreEqual(gameView.x / ResponsiveCatalogDevices.PhoneLandscape.x, ratio.y, 1e-4f);
        }

        [Test]
        public void ToggleDesignScaleOn_ReferenceLandscape_KeepsAuthoredPixels()
        {
            Vector2 livePos = new Vector2(450f, -220f);
            var state = new ResponsiveRectState
            {
                scaleFromDesignCanvas = true,
                designScaleMode = ResponsiveDesignScaleMode.Width,
                designCanvasSize = ResponsiveCatalogDevices.PhonePortrait,
                defaultScale = Vector2.one,
                anchoredPosition = livePos,
            };

            Vector2 current = ResponsiveCatalogDevices.PhoneLandscape;
            Vector2 ratio = state.ComputeLayoutRatio(current, ResponsiveDevice.FormFactor.Phone);
            Assert.AreEqual(1f, ratio.x, 1e-4f);
            Assert.AreEqual(1f, ratio.y, 1e-4f);

            Vector2 designPos = ResponsiveRectState.UnscaleLivePixels(livePos, ratio);
            Vector2 designScale = ResponsiveRectState.UnscaleLivePixels(Vector2.one, ratio);
            Assert.AreEqual(450f, designPos.x, 1e-3f);
            Assert.AreEqual(-220f, designPos.y, 1e-3f);
            Assert.AreEqual(1f, designScale.x, 1e-4f);
            Assert.AreEqual(1f, designScale.y, 1e-4f);
        }

        [Test]
        public void ToggleDesignScaleOn_CoverReferenceLandscape_KeepsAuthoredPixels()
        {
            Vector2 livePos = new Vector2(0f, -551f);
            var state = new ResponsiveRectState
            {
                scaleFromDesignCanvas = true,
                designScaleMode = ResponsiveDesignScaleMode.Width,
                designCanvasSize = Vector2.zero,
                defaultScale = Vector2.one,
                anchoredPosition = livePos,
            };

            Vector2 current = ResponsiveCatalogDevices.FoldCoverLandscape;
            Vector2 ratio = state.ComputeLayoutRatio(
                current,
                ResponsiveDevice.FormFactor.FoldPhoneCover,
                ResponsiveDevice.Orientation.Landscape);
            Assert.AreEqual(1f, ratio.x, 1e-4f);
            Assert.AreEqual(1f, ratio.y, 1e-4f);

            Vector2 designPos = ResponsiveRectState.UnscaleLivePixels(livePos, ratio);
            Vector2 designScale = ResponsiveRectState.UnscaleLivePixels(
                new Vector2(1.1726f, 1.1726f), ratio);
            Assert.AreEqual(0f, designPos.x, 1e-3f);
            Assert.AreEqual(-551f, designPos.y, 1e-3f);
            Assert.AreEqual(1.1726f, designScale.x, 1e-4f);
            Assert.AreEqual(1.1726f, designScale.y, 1e-4f);
        }

        [Test]
        public void ComputeCanvasRatio_OrientSwap_MatchesAxes()
        {
            Vector2 ratio = ResponsiveRectState.ComputeCanvasRatio(
                ResponsiveCatalogDevices.PhoneLandscape,
                ResponsiveCatalogDevices.PhonePortrait,
                ResponsiveDesignScaleMode.Width);

            Assert.AreEqual(1f, ratio.x, 1e-4f);
            Assert.AreEqual(1f, ratio.y, 1e-4f);
        }

        [Test]
        public void ToggleDesignScaleOn_UnscaleThenApply_KeepsLivePosition()
        {
            Vector2 livePos = new Vector2(200f, 80f);
            var state = new ResponsiveRectState
            {
                scaleFromDesignCanvas = true,
                designScaleMode = ResponsiveDesignScaleMode.Width,
                designCanvasSize = ResponsiveCatalogDevices.PhonePortrait,
                anchoredPosition = livePos,
                defaultScale = Vector2.one,
            };

            Vector2 current = ResponsiveCatalogDevices.CompactPhoneLandscape;
            Vector2 ratio = state.ComputeLayoutRatio(current, ResponsiveDevice.FormFactor.Phone);
            Assert.Greater(Mathf.Abs(ratio.x - 1f), 1e-3f);

            Vector2 designPos = ResponsiveRectState.UnscaleLivePixels(livePos, ratio);
            Vector2 applied = ResponsiveRectState.ScaleDesignPixels(designPos, ratio);
            Assert.AreEqual(livePos.x, applied.x, 1e-3f);
            Assert.AreEqual(livePos.y, applied.y, 1e-3f);
        }

        [Test]
        public void ComputeAppliedLocalScale_ReferenceLandscapeLiveSize_KeepsScaleOne()
        {
            var state = new ResponsiveRectState
            {
                scaleFromDesignCanvas = true,
                designScaleMode = ResponsiveDesignScaleMode.Width,
                designCanvasSize = ResponsiveCatalogDevices.PhonePortrait,
                defaultScale = Vector2.one,
            };

            Vector2 applied = state.ComputeAppliedLocalScale(
                ResponsiveCatalogDevices.PhoneLandscape,
                ResponsiveDevice.FormFactor.Phone);
            Assert.AreEqual(1f, applied.x, 1e-4f);
            Assert.AreEqual(1f, applied.y, 1e-4f);
        }

        [Test]
        public void ComputeAppliedLocalScale_On_CaptureAtLiveRatio_RestoresScaleOne()
        {
            var state = new ResponsiveRectState
            {
                scaleFromDesignCanvas = true,
                designScaleMode = ResponsiveDesignScaleMode.Width,
                designCanvasSize = ResponsiveCatalogDevices.PhonePortrait,
                defaultScale = Vector2.one,
            };

            Vector2 current = ResponsiveCatalogDevices.CompactPhoneLandscape;
            Vector2 ratio = state.ComputeLayoutRatio(current, ResponsiveDevice.FormFactor.Phone);
            state.defaultScale = ResponsiveRectState.UnscaleLivePixels(Vector2.one, ratio);

            Vector2 applied = state.ComputeAppliedLocalScale(current, ResponsiveDevice.FormFactor.Phone);
            Assert.AreEqual(1f, applied.x, 1e-4f);
            Assert.AreEqual(1f, applied.y, 1e-4f);
        }

        [Test]
        public void ComputeAppliedLocalScale_On_IsOriginTimesRatio()
        {
            var state = new ResponsiveRectState
            {
                scaleFromDesignCanvas = true,
                designScaleMode = ResponsiveDesignScaleMode.Width,
                designCanvasSize = ResponsiveCatalogDevices.PhonePortrait,
                defaultScale = new Vector2(2f, 1.5f),
            };

            Vector2 local = state.ComputeAppliedLocalScale(
                ResponsiveCatalogDevices.PhonePortrait * 0.5f,
                ResponsiveDevice.FormFactor.Phone);

            Assert.AreEqual(1f, local.x, 1e-4f);
            Assert.AreEqual(0.75f, local.y, 1e-4f);
        }

        [Test]
        public void DesignScale_AllAnchorPresetAxes_CaptureApplyRoundTrips()
        {
            Vector2[] anchorMins =
            {
                new Vector2(0.5f, 0.5f), // fixed
                new Vector2(0f, 0.5f),   // horizontal stretch
                new Vector2(0.5f, 0f),   // vertical stretch
                Vector2.zero,            // full stretch
            };
            Vector2[] anchorMaxes =
            {
                new Vector2(0.5f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(0.5f, 1f),
                Vector2.one,
            };

            foreach (ResponsiveDesignScaleMode mode in System.Enum.GetValues(
                         typeof(ResponsiveDesignScaleMode)))
            {
                for (int i = 0; i < anchorMins.Length; i++)
                    AssertAnchorPresetRoundTrip(
                        anchorMins[i],
                        anchorMaxes[i],
                        mode,
                        $"{mode}, preset {i}");
            }
        }

        [Test]
        public void DesignScale_FullStretch_CompensatesAnchorSpanAndScalesChildren()
        {
            var parent = new GameObject("Parent", typeof(RectTransform));
            var go = new GameObject("FullStretch", typeof(RectTransform));
            try
            {
                RectTransform parentRt = parent.GetComponent<RectTransform>();
                parentRt.sizeDelta = new Vector2(500f, 750f);
                go.transform.SetParent(parent.transform, false);
                RectTransform rt = go.GetComponent<RectTransform>();
                var state = new ResponsiveRectState
                {
                    enabled = true,
                    scaleFromDesignCanvas = true,
                    designScaleMode = ResponsiveDesignScaleMode.IndependentXY,
                    designCanvasSize = new Vector2(1000f, 1000f),
                    defaultScale = Vector2.one,
                    anchorMin = Vector2.zero,
                    anchorMax = Vector2.one,
                    anchoredPosition = new Vector2(20f, -30f),
                    sizeDelta = new Vector2(-100f, -80f),
                    pivot = new Vector2(0.25f, 0.75f),
                };

                state.Apply(
                    rt,
                    currentCanvasSize: new Vector2(500f, 750f),
                    slotForm: ResponsiveDevice.FormFactor.Phone);

                Assert.AreEqual(new Vector2(10f, -22.5f), rt.anchoredPosition);
                Assert.AreEqual(new Vector2(400f, 170f), rt.sizeDelta);
                Assert.AreEqual(0.5f, rt.localScale.x, 1e-4f);
                Assert.AreEqual(0.75f, rt.localScale.y, 1e-4f);

                // Transformed size equals natural parent stretch plus scaled authored offsets.
                Assert.AreEqual(450f, rt.rect.width * rt.localScale.x, 1e-3f);
                Assert.AreEqual(690f, rt.rect.height * rt.localScale.y, 1e-3f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(parent);
            }
        }

        [Test]
        public void HubLayoutRatio_ConstantPixelScaler_KeepsWidthHeightModesDistinct()
        {
            WithPinnedHubCanvas(new Vector2(1000f, 500f), (hub, _, rt) =>
            {
                var state = ResponsiveRectState.CreateDefault();
                state.scaleFromDesignCanvas = true;
                state.designCanvasSize = new Vector2(500f, 1000f);
                state.defaultScale = new Vector2(1.25f, 1.1f);

                state.designScaleMode = ResponsiveDesignScaleMode.Width;
                Vector2 widthRatio = hub.GetLayoutRatio(
                    state,
                    ResponsiveDevice.FormFactor.Phone,
                    rt,
                    ResponsiveDevice.Orientation.Portrait);
                Vector2 widthScale = hub.GetAppliedWidgetScale(
                    state,
                    ResponsiveDevice.FormFactor.Phone,
                    rt,
                    ResponsiveDevice.Orientation.Portrait);

                state.designScaleMode = ResponsiveDesignScaleMode.Height;
                Vector2 heightRatio = hub.GetLayoutRatio(
                    state,
                    ResponsiveDevice.FormFactor.Phone,
                    rt,
                    ResponsiveDevice.Orientation.Portrait);

                Assert.AreEqual(2f, widthRatio.x, 1e-4f);
                Assert.AreEqual(2f, widthRatio.y, 1e-4f);
                Assert.AreEqual(0.5f, heightRatio.x, 1e-4f);
                Assert.AreEqual(0.5f, heightRatio.y, 1e-4f);
                Assert.AreEqual(2.5f, widthScale.x, 1e-4f);
                Assert.AreEqual(2.2f, widthScale.y, 1e-4f);
                Assert.AreEqual(
                    widthScale,
                    hub.GetAppliedWidgetScale(
                        state,
                        ResponsiveDevice.FormFactor.Phone,
                        widthRatio));
            });
        }

        [Test]
        public void Apply_ScaleModeChangeWithoutRecapture_JumpsLiveTarget()
        {
            WithPinnedHubCanvas(new Vector2(1000f, 500f), (hub, _, rt) =>
            {
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(40f, -20f);
                rt.sizeDelta = new Vector2(100f, 80f);
                rt.localScale = Vector3.one;

                var authored = ResponsiveRectState.CreateDefault();
                authored.scaleFromDesignCanvas = true;
                authored.designScaleMode = ResponsiveDesignScaleMode.Width;
                authored.designCanvasSize = new Vector2(500f, 1000f);
                authored.defaultScale = Vector2.one;

                Vector2 widthRatio = hub.GetLayoutRatio(
                    authored,
                    ResponsiveDevice.FormFactor.Phone,
                    rt,
                    ResponsiveDevice.Orientation.Portrait);
                ResponsiveRectState captured = ResponsiveRectState.From(
                    rt, widthRatio, ResponsiveDevice.FormFactor.Phone);
                captured.CopyDesignScaleSettingsFrom(authored);
                captured.designScaleMode = ResponsiveDesignScaleMode.Height;

                Vector2 heightRatio = hub.GetLayoutRatio(
                    captured,
                    ResponsiveDevice.FormFactor.Phone,
                    rt,
                    ResponsiveDevice.Orientation.Portrait);
                captured.Apply(
                    rt,
                    heightRatio,
                    hub.GetAppliedWidgetScale(
                        captured, ResponsiveDevice.FormFactor.Phone, heightRatio));

                Assert.Greater(Mathf.Abs(rt.anchoredPosition.x - 40f), 1f);
                Assert.Greater(Mathf.Abs(rt.anchoredPosition.y + 20f), 1f);
            });
        }

        [Test]
        public void MapScalerCanvasToDesignSpace_DefaultIPhoneLandscape_IsIdentity()
        {
            Vector2 live = ResponsiveRectState.MapScalerCanvasToDesignSpace(
                canvasLocalSize: new Vector2(2537.036f, 1167.6754f),
                scalerReferenceSize: ResponsiveCatalogDevices.CompactPhoneLandscape,
                designSize: ResponsiveCatalogDevices.PhoneLandscape);
            Assert.AreEqual(ResponsiveCatalogDevices.PhoneLandscape.x, live.x, 1e-3f);
            Assert.AreEqual(ResponsiveCatalogDevices.PhoneLandscape.y, live.y, 1e-3f);

            var state = new ResponsiveRectState
            {
                scaleFromDesignCanvas = true,
                designScaleMode = ResponsiveDesignScaleMode.Width,
                designCanvasSize = ResponsiveCatalogDevices.PhonePortrait,
                defaultScale = Vector2.one,
            };
            Vector2 ratio = state.ComputeLayoutRatio(
                live,
                ResponsiveDevice.FormFactor.Phone,
                ResponsiveDevice.Orientation.Landscape);
            Assert.AreEqual(1f, ratio.x, 1e-4f);
            Assert.AreEqual(1f, ratio.y, 1e-4f);
        }

        [Test]
        public void MapScalerCanvasToDesignSpace_NonReferenceCanvas_PreservesResidual()
        {
            Vector2 canvas = new Vector2(2295f, 1290f);
            Vector2 reference = ResponsiveCatalogDevices.CompactPhoneLandscape;
            Vector2 design = ResponsiveCatalogDevices.PhoneLandscape;
            Vector2 live = ResponsiveRectState.MapScalerCanvasToDesignSpace(
                canvas,
                reference,
                design);
            Assert.AreEqual(canvas.x / reference.x * design.x, live.x, 1e-3f);
            Assert.AreEqual(canvas.y / reference.y * design.y, live.y, 1e-3f);
        }

        [Test]
        public void GetLayoutRatio_CoverReferenceWatch_IgnoresExpandedScalerCanvas()
        {
            Vector2 cover = ResponsiveCatalogDevices.FoldCoverLandscape;
            var expandedCanvas = new Vector2(
                ResponsiveCatalogDevices.CompactPhoneLandscape.x,
                1602.402f);
            ResponsiveDevice.SetSimulatorLayout(
                ResponsiveDevice.FormFactor.FoldPhoneCover,
                ResponsiveDevice.Orientation.Landscape,
                (int)cover.x,
                (int)cover.y);
            try
            {
                WithPinnedHubCanvas(expandedCanvas, (hub, _, rt) =>
                {
                    var state = ResponsiveRectState.CreateDefault();
                    state.scaleFromDesignCanvas = true;
                    state.designScaleMode = ResponsiveDesignScaleMode.Width;
                    state.designCanvasSize = Vector2.zero;
                    state.defaultScale = new Vector2(1.1726f, 1.1726f);
                    state.anchoredPosition = new Vector2(0f, -551f);
                    state.sizeDelta = new Vector2(1200f, 250f);

                    Vector2 ratio = hub.GetLayoutRatio(
                        state,
                        ResponsiveDevice.FormFactor.FoldPhoneCover,
                        rt,
                        ResponsiveDevice.Orientation.Landscape);
                    Assert.AreEqual(1f, ratio.x, 1e-4f);
                    Assert.AreEqual(1f, ratio.y, 1e-4f);

                    Vector2 applied = hub.GetAppliedWidgetScale(
                        state,
                        ResponsiveDevice.FormFactor.FoldPhoneCover,
                        ratio);
                    Assert.AreEqual(1.1726f, applied.x, 1e-4f);
                    Assert.AreEqual(1.1726f, applied.y, 1e-4f);

                    hub.ApplyWidget(
                        rt,
                        state,
                        ResponsiveDevice.FormFactor.FoldPhoneCover,
                        ResponsiveDevice.Orientation.Landscape);
                    Assert.AreEqual(0f, rt.anchoredPosition.x, 1e-3f);
                    Assert.AreEqual(-551f, rt.anchoredPosition.y, 1e-3f);
                    Assert.AreEqual(1200f, rt.sizeDelta.x, 1e-3f);
                    Assert.AreEqual(250f, rt.sizeDelta.y, 1e-3f);
                    Assert.AreEqual(1.1726f, rt.localScale.x, 1e-4f);
                    Assert.AreEqual(1.1726f, rt.localScale.y, 1e-4f);
                });
            }
            finally
            {
                ResponsiveDevice.ClearSimulatorLayout();
            }
        }

        [Test]
        public void GetLayoutRatio_CoverSafeAreaWatch_IsIdentity()
        {
            Vector2 safe = ResponsiveCatalogDevices.FoldCoverSafeAreaLandscape;
            Assert.IsTrue(
                ResponsiveDevice.IsKnownFoldCoverPhoneSize(safe.x, safe.y),
                "safe-area Cover must stay classified as FoldPhoneCover");
            ResponsiveDevice.SetSimulatorLayout(
                ResponsiveDevice.FormFactor.FoldPhoneCover,
                ResponsiveDevice.Orientation.Landscape,
                (int)safe.x,
                (int)safe.y);
            try
            {
                WithPinnedHubCanvas(
                    ResponsiveCatalogDevices.CompactPhoneLandscape,
                    (hub, _, rt) =>
                    {
                        var state = ResponsiveRectState.CreateDefault();
                        state.scaleFromDesignCanvas = true;
                        state.designScaleMode = ResponsiveDesignScaleMode.Width;
                        state.designCanvasSize = Vector2.zero;
                        state.defaultScale = new Vector2(1.1726f, 1.1726f);

                        Vector2 ratio = hub.GetLayoutRatio(
                            state,
                            ResponsiveDevice.FormFactor.FoldPhoneCover,
                            rt,
                            ResponsiveDevice.Orientation.Landscape);
                        Assert.AreEqual(1f, ratio.x, 1e-4f);
                        Assert.AreEqual(1f, ratio.y, 1e-4f);

                        Vector2 applied = hub.GetAppliedWidgetScale(
                            state,
                            ResponsiveDevice.FormFactor.FoldPhoneCover,
                            ratio);
                        Assert.AreEqual(1.1726f, applied.x, 1e-4f);
                        Assert.AreEqual(1.1726f, applied.y, 1e-4f);
                    });
            }
            finally
            {
                ResponsiveDevice.ClearSimulatorLayout();
            }
        }

        [Test]
        public void GetLayoutRatio_FoldExpandInnerSafeAreaWatch_PadSlot_IsIdentity()
        {
            Vector2 safe = ResponsiveCatalogDevices.FoldPhoneInnerSafeAreaLandscape;
            Assert.IsTrue(
                ResponsiveDevice.IsKnownFoldPhoneInnerSize(safe.x, safe.y),
                "safe-area Fold7 Inner must stay classified as Fold phone inner");
            Assert.IsTrue(ResponsiveDevice.IsKnownFoldInnerPadSize(safe.x, safe.y));
            Assert.AreEqual(
                ResponsiveDevice.FormFactor.Pad,
                ResponsiveDevice.DetectActiveFormFactor(safe, padMaxAspect: 1.6f));

            ResponsiveDevice.SetSimulatorLayout(
                ResponsiveDevice.FormFactor.Pad,
                ResponsiveDevice.Orientation.Landscape,
                (int)safe.x,
                (int)safe.y);
            try
            {
                Assert.IsTrue(
                    ResponsiveDevice.IsPostureOwnedDesignIdentity(
                        ResponsiveDevice.FormFactor.Pad, safe.x, safe.y),
                    "Expand/Pad Simulator pin owns Design Scale identity without catalog WxH");

                WithPinnedHubCanvas(
                    ResponsiveCatalogDevices.PadLandscape,
                    (hub, _, rt) =>
                    {
                        var state = ResponsiveRectState.CreateDefault();
                        state.scaleFromDesignCanvas = true;
                        state.designScaleMode = ResponsiveDesignScaleMode.Width;
                        state.designCanvasSize = Vector2.zero;
                        state.defaultScale = new Vector2(1.05f, 1.05f);

                        Vector2 ratio = hub.GetLayoutRatio(
                            state,
                            ResponsiveDevice.FormFactor.Pad,
                            rt,
                            ResponsiveDevice.Orientation.Landscape);
                        Assert.AreEqual(1f, ratio.x, 1e-4f);
                        Assert.AreEqual(1f, ratio.y, 1e-4f);

                        Vector2 applied = hub.GetAppliedWidgetScale(
                            state,
                            ResponsiveDevice.FormFactor.Pad,
                            ratio);
                        Assert.AreEqual(1.05f, applied.x, 1e-4f);
                        Assert.AreEqual(1.05f, applied.y, 1e-4f);
                    });
            }
            finally
            {
                ResponsiveDevice.ClearSimulatorLayout();
            }
        }

        [Test]
        public void GetLayoutRatio_FoldExpandFlatPosture_OffCatalogWatch_PadSlot_IsIdentity()
        {
            // OEM / safe-area WxH must not break identity when Expand/Pad posture owns the slot.
            Vector2 offCatalog = new Vector2(2000f, 1800f);
            Assert.IsFalse(
                ResponsiveDevice.IsKnownFoldPhoneInnerSize(offCatalog.x, offCatalog.y));

            ResponsiveDevice.SetSimulatorLayout(
                ResponsiveDevice.FormFactor.Pad,
                ResponsiveDevice.Orientation.Landscape,
                (int)offCatalog.x,
                (int)offCatalog.y);
            try
            {
                Assert.IsTrue(
                    ResponsiveDevice.IsPostureOwnedDesignIdentity(
                        ResponsiveDevice.FormFactor.Pad, offCatalog.x, offCatalog.y),
                    "Opened/Expand posture owns Pad identity without catalog WxH");
                Assert.IsFalse(
                    ResponsiveDevice.IsPostureOwnedDesignIdentity(
                        ResponsiveDevice.FormFactor.Phone, offCatalog.x, offCatalog.y));

                WithPinnedHubCanvas(offCatalog, (hub, _, rt) =>
                {
                    var state = ResponsiveRectState.CreateDefault();
                    state.scaleFromDesignCanvas = true;
                    state.designScaleMode = ResponsiveDesignScaleMode.Width;
                    state.designCanvasSize = Vector2.zero;
                    state.defaultScale = Vector2.one;

                    Vector2 ratio = hub.GetLayoutRatio(
                        state,
                        ResponsiveDevice.FormFactor.Pad,
                        rt,
                        ResponsiveDevice.Orientation.Landscape);
                    Assert.AreEqual(1f, ratio.x, 1e-4f);
                    Assert.AreEqual(1f, ratio.y, 1e-4f);
                });
            }
            finally
            {
                ResponsiveDevice.ClearSimulatorLayout();
            }
        }

        [Test]
        public void IsPostureOwnedDesignIdentity_FlatDetector_PadWithoutCatalogSize()
        {
            Vector2 offCatalog = new Vector2(2000f, 1800f);
            var go = new GameObject("FoldFlatPosture");
            try
            {
                var detector = go.AddComponent<FDPlus.Foldable.FoldablePostureDetector>();
                detector.DeviceKind = FDPlus.Foldable.FoldableDeviceKind.FoldablePhone;
                var layout = FDPlus.Foldable.FoldableLayoutState.CreateSimulated(
                    FDPlus.Foldable.FoldableState.Flat,
                    FDPlus.Foldable.FoldableOrientation.Horizontal,
                    (int)offCatalog.x,
                    (int)offCatalog.y,
                    0.5f,
                    24);
                layout.deviceKind = FDPlus.Foldable.FoldableDeviceKind.FoldablePhone;
                layout.productState = FDPlus.Foldable.FoldableProductState.Opened;
                detector.ApplySimulatedLayout(layout);

                Assert.IsTrue(
                    ResponsiveDevice.IsPostureOwnedDesignIdentity(
                        ResponsiveDevice.FormFactor.Pad, offCatalog.x, offCatalog.y));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void IsPostureOwnedDesignIdentity_EmptyDetector_DoesNotAssumeOpened()
        {
            var go = new GameObject("FoldAwaitingNativePosture");
            try
            {
                var detector = go.AddComponent<FDPlus.Foldable.FoldablePostureDetector>();
                detector.DeviceKind = FDPlus.Foldable.FoldableDeviceKind.FoldablePhone;
                detector.LayoutState.deviceKind =
                    FDPlus.Foldable.FoldableDeviceKind.FoldablePhone;
                detector.LayoutState.state = FDPlus.Foldable.FoldableState.Undefined;
                detector.LayoutState.productState =
                    FDPlus.Foldable.FoldableProductState.Undefined;

                Assert.IsFalse(
                    ResponsiveDevice.IsPostureOwnedDesignIdentity(
                        ResponsiveDevice.FormFactor.Pad, 2000f, 1800f),
                    "Identity must wait for the first meaningful Jetpack posture");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void GetLayoutRatio_FoldHalfOpenedSafeAreaWatch_IsIdentity()
        {
            Vector2 safe = ResponsiveCatalogDevices.FoldPhoneInnerSafeAreaLandscape;
            ResponsiveDevice.SetSimulatorLayout(
                ResponsiveDevice.FormFactor.FoldHalfOpened,
                ResponsiveDevice.Orientation.Landscape,
                (int)safe.x,
                (int)safe.y);
            try
            {
                WithPinnedHubCanvas(
                    ResponsiveCatalogDevices.FoldPhoneInnerLandscape,
                    (hub, _, rt) =>
                    {
                        var state = ResponsiveRectState.CreateDefault();
                        state.scaleFromDesignCanvas = true;
                        state.designScaleMode = ResponsiveDesignScaleMode.Width;
                        state.designCanvasSize = Vector2.zero;
                        state.defaultScale = Vector2.one;

                        // The wide Fold Half Opened screen is the Portrait slot (posture name).
                        Vector2 ratio = hub.GetLayoutRatio(
                            state,
                            ResponsiveDevice.FormFactor.FoldHalfOpened,
                            rt,
                            ResponsiveDevice.Orientation.Portrait);
                        Assert.AreEqual(1f, ratio.x, 1e-4f);
                        Assert.AreEqual(1f, ratio.y, 1e-4f);
                    });
            }
            finally
            {
                ResponsiveDevice.ClearSimulatorLayout();
            }
        }

        [Test]
        public void GetLayoutRatio_CoverWatch_PhoneSlot_IsNotIdentity()
        {
            Vector2 cover = ResponsiveCatalogDevices.FoldCoverLandscape;
            ResponsiveDevice.SetSimulatorLayout(
                ResponsiveDevice.FormFactor.FoldPhoneCover,
                ResponsiveDevice.Orientation.Landscape,
                (int)cover.x,
                (int)cover.y);
            try
            {
                WithPinnedHubCanvas(cover, (hub, _, rt) =>
                {
                    var state = ResponsiveRectState.CreateDefault();
                    state.scaleFromDesignCanvas = true;
                    state.designScaleMode = ResponsiveDesignScaleMode.Width;
                    state.designCanvasSize = Vector2.zero;

                    Vector2 ratio = hub.GetLayoutRatio(
                        state,
                        ResponsiveDevice.FormFactor.Phone,
                        rt,
                        ResponsiveDevice.Orientation.Landscape);
                    Assert.Greater(Mathf.Abs(ratio.x - 1f), 1e-3f);
                });
            }
            finally
            {
                ResponsiveDevice.ClearSimulatorLayout();
            }
        }

        [Test]
        public void GetLayoutRatio_CoverWatch_NestedPane_UsesPaneNotIdentity()
        {
            Vector2 cover = ResponsiveCatalogDevices.FoldCoverLandscape;
            ResponsiveDevice.SetSimulatorLayout(
                ResponsiveDevice.FormFactor.FoldPhoneCover,
                ResponsiveDevice.Orientation.Landscape,
                (int)cover.x,
                (int)cover.y);
            try
            {
                WithPinnedHubCanvas(cover, (hub, canvasRt, _) =>
                {
                    var nested = new GameObject(
                        "NestedPane", typeof(RectTransform), typeof(Canvas));
                    var target = new GameObject("NestedWidget", typeof(RectTransform));
                    try
                    {
                        nested.transform.SetParent(canvasRt, false);
                        target.transform.SetParent(nested.transform, false);

                        RectTransform nestedRt = nested.GetComponent<RectTransform>();
                        nestedRt.anchorMin = nestedRt.anchorMax = new Vector2(0.5f, 0.5f);
                        nestedRt.pivot = new Vector2(0.5f, 0.5f);
                        nestedRt.sizeDelta = new Vector2(200f, 400f);
                        nestedRt.ForceUpdateRectTransforms();
                        Canvas.ForceUpdateCanvases();

                        var state = ResponsiveRectState.CreateDefault();
                        state.scaleFromDesignCanvas = true;
                        state.designScaleMode = ResponsiveDesignScaleMode.Width;
                        state.designCanvasSize = Vector2.zero;

                        Vector2 ratio = hub.GetLayoutRatio(
                            state,
                            ResponsiveDevice.FormFactor.FoldPhoneCover,
                            target.GetComponent<RectTransform>(),
                            ResponsiveDevice.Orientation.Landscape);
                        Assert.Greater(Mathf.Abs(ratio.x - 1f), 1e-3f);

                        Vector2 targetAwareLive = ResponsiveDesignScale.ResolveIdentityLiveSize(
                            cover,
                            ResponsiveDevice.FormFactor.FoldPhoneCover,
                            ResponsiveDevice.Orientation.Landscape,
                            ResponsiveCatalogDevices.FoldCoverLandscape,
                            target.GetComponent<RectTransform>());
                        Assert.AreEqual(nestedRt.rect.size, targetAwareLive);
                    }
                    finally
                    {
                        UnityEngine.Object.DestroyImmediate(nested);
                    }
                });
            }
            finally
            {
                ResponsiveDevice.ClearSimulatorLayout();
            }
        }

        [Test]
        public void GetLayoutRatio_CoverIdentity_NestedWidget_KeepsAuthoredDefaultScale()
        {
            Vector2 cover = ResponsiveCatalogDevices.FoldCoverLandscape;
            ResponsiveDevice.SetSimulatorLayout(
                ResponsiveDevice.FormFactor.FoldPhoneCover,
                ResponsiveDevice.Orientation.Landscape,
                (int)cover.x,
                (int)cover.y);
            try
            {
                WithPinnedHubCanvas(cover, (hub, canvasRt, _) =>
                {
                    var parent = new GameObject("ParentWidget", typeof(RectTransform));
                    var child = new GameObject("ChildWidget", typeof(RectTransform));
                    try
                    {
                        parent.transform.SetParent(canvasRt, false);
                        child.transform.SetParent(parent.transform, false);

                        var state = ResponsiveRectState.CreateDefault();
                        state.scaleFromDesignCanvas = true;
                        state.designScaleMode = ResponsiveDesignScaleMode.Width;
                        state.designCanvasSize = Vector2.zero;
                        state.defaultScale = new Vector2(1.1726f, 1.1726f);

                        hub.ApplyWidget(
                            parent.GetComponent<RectTransform>(),
                            state,
                            ResponsiveDevice.FormFactor.FoldPhoneCover,
                            ResponsiveDevice.Orientation.Landscape);

                        Vector2 childRatio = hub.GetLayoutRatio(
                            state,
                            ResponsiveDevice.FormFactor.FoldPhoneCover,
                            child.GetComponent<RectTransform>(),
                            ResponsiveDevice.Orientation.Landscape);
                        Assert.AreEqual(1f, childRatio.x, 1e-4f);
                        Assert.AreEqual(1f, childRatio.y, 1e-4f);

                        hub.ApplyWidget(
                            child.GetComponent<RectTransform>(),
                            state,
                            ResponsiveDevice.FormFactor.FoldPhoneCover,
                            ResponsiveDevice.Orientation.Landscape);
                        Assert.AreEqual(1.1726f, parent.transform.localScale.x, 1e-4f);
                        Assert.AreEqual(1.1726f, child.transform.localScale.x, 1e-4f);
                    }
                    finally
                    {
                        UnityEngine.Object.DestroyImmediate(parent);
                    }
                });
            }
            finally
            {
                ResponsiveDevice.ClearSimulatorLayout();
            }
        }

        [Test]
        public void GetLayoutRatio_PhoneReferenceWatch_IsIdentity()
        {
            AssertDefaultProfileIdentity(
                ResponsiveCatalogDevices.PhoneLandscape,
                ResponsiveDevice.FormFactor.Phone,
                ResponsiveDevice.FormFactor.Phone);
        }

        [Test]
        public void GetLayoutRatio_CompactPhoneWatch_PhoneSlot_IsNotIdentity()
        {
            Vector2 compact = ResponsiveCatalogDevices.CompactPhoneLandscape;
            ResponsiveDevice.SetSimulatorLayout(
                ResponsiveDevice.FormFactor.Phone,
                ResponsiveDevice.Orientation.Landscape,
                (int)compact.x,
                (int)compact.y);
            try
            {
                WithPinnedHubCanvas(ExpandedScalerCanvas, (hub, _, rt) =>
                {
                    var state = ResponsiveRectState.CreateDefault();
                    state.scaleFromDesignCanvas = true;
                    state.designScaleMode = ResponsiveDesignScaleMode.Width;
                    state.designCanvasSize = Vector2.zero;

                    Vector2 ratio = hub.GetLayoutRatio(
                        state,
                        ResponsiveDevice.FormFactor.Phone,
                        rt,
                        ResponsiveDevice.Orientation.Landscape);
                    Assert.Greater(Mathf.Abs(ratio.x - 1f), 1e-3f);
                });
            }
            finally
            {
                ResponsiveDevice.ClearSimulatorLayout();
            }
        }

        [Test]
        public void GetLayoutRatio_PadReferenceWatch_IsIdentity()
        {
            AssertDefaultProfileIdentity(
                ResponsiveCatalogDevices.PadLandscape,
                ResponsiveDevice.FormFactor.Pad,
                ResponsiveDevice.FormFactor.Pad);
        }

        [Test]
        public void GetLayoutRatio_FoldHalfOpenedReferenceWatch_IsIdentity()
        {
            AssertDefaultProfileIdentity(
                ResponsiveCatalogDevices.FoldPhoneInnerLandscape,
                ResponsiveDevice.FormFactor.FoldHalfOpened,
                ResponsiveDevice.FormFactor.FoldHalfOpened);
        }

        [Test]
        public void GetLayoutRatio_FoldCoverHalfOpenedReferenceWatch_IsIdentity()
        {
            AssertDefaultProfileIdentity(
                ResponsiveCatalogDevices.FoldInnerLandscape,
                ResponsiveDevice.FormFactor.FoldCoverHalfOpened,
                ResponsiveDevice.FormFactor.FoldCoverHalfOpened);
        }

        [Test]
        public void GetLayoutRatio_FlipHalfOpenedReferenceWatch_IsIdentity()
        {
            AssertDefaultProfileIdentity(
                ResponsiveCatalogDevices.FlipMainLandscape,
                ResponsiveDevice.FormFactor.FlipHalfOpened,
                ResponsiveDevice.FormFactor.FlipHalfOpened);
        }

        [Test]
        public void GetLayoutRatio_FoldExpandInnerWatch_PadSlot_IsIdentity()
        {
            AssertDefaultProfileIdentity(
                ResponsiveCatalogDevices.FoldInnerLandscape,
                ResponsiveDevice.FormFactor.Pad,
                ResponsiveDevice.FormFactor.Pad);
        }

        [Test]
        public void GetLayoutRatio_FlipOpenedMainWatch_PhoneSlot_IsIdentity()
        {
            AssertDefaultProfileIdentity(
                ResponsiveCatalogDevices.FlipMainLandscape,
                ResponsiveDevice.FormFactor.Phone,
                ResponsiveDevice.FormFactor.Phone);
        }

        [Test]
        public void GetLayoutRatio_FlipClosedCoverWatch_FlipPhoneCoverSlot_IsIdentity()
        {
            AssertDefaultProfileIdentity(
                ResponsiveCatalogDevices.FlipCoverLandscape,
                ResponsiveDevice.FormFactor.FlipPhoneCover,
                ResponsiveDevice.FormFactor.FlipPhoneCover);
        }

        [Test]
        public void GetLayoutRatio_FlipCoverWatch_PhoneSlot_IsNotIdentity()
        {
            Vector2 cover = ResponsiveCatalogDevices.FlipCoverLandscape;
            ResponsiveDevice.SetSimulatorLayout(
                ResponsiveDevice.FormFactor.Phone,
                ResponsiveDevice.Orientation.Landscape,
                (int)cover.x,
                (int)cover.y);
            try
            {
                WithPinnedHubCanvas(ExpandedScalerCanvas, (hub, _, rt) =>
                {
                    var state = ResponsiveRectState.CreateDefault();
                    state.scaleFromDesignCanvas = true;
                    state.designScaleMode = ResponsiveDesignScaleMode.Width;
                    state.designCanvasSize = Vector2.zero;

                    Vector2 ratio = hub.GetLayoutRatio(
                        state,
                        ResponsiveDevice.FormFactor.Phone,
                        rt,
                        ResponsiveDevice.Orientation.Landscape);
                    Assert.Greater(Mathf.Abs(ratio.x - 1f), 1e-3f);
                });
            }
            finally
            {
                ResponsiveDevice.ClearSimulatorLayout();
            }
        }

        [Test]
        public void IsDesignCanvasReferenceWatch_CustomFlipCoverProvider_IsIdentity()
        {
            Func<Vector2> previous = ResponsiveRectState.FlipCoverSizeProvider;
            var customCover = new Vector2(900f, 1100f);
            try
            {
                ResponsiveRectState.FlipCoverSizeProvider = () => customCover;
                Assert.IsTrue(ResponsiveDesignScale.IsDesignCanvasReferenceWatch(
                    ResponsiveDevice.FormFactor.FlipPhoneCover,
                    ResponsiveUI.OrientSize(
                        customCover,
                        ResponsiveDevice.Orientation.Landscape),
                    ResponsiveDevice.Orientation.Landscape,
                    ResponsiveUI.OrientSize(
                        ResponsiveRectState.GetDefaultDesignCanvasSize(
                            ResponsiveDevice.FormFactor.FlipPhoneCover),
                        ResponsiveDevice.Orientation.Landscape)));
            }
            finally
            {
                ResponsiveRectState.FlipCoverSizeProvider = previous;
            }
        }

        [Test]
        public void GetLayoutRatio_FoldInnerWatch_PhoneSlot_IsNotIdentity()
        {
            Vector2 inner = ResponsiveCatalogDevices.FoldInnerLandscape;
            ResponsiveDevice.SetSimulatorLayout(
                ResponsiveDevice.FormFactor.Pad,
                ResponsiveDevice.Orientation.Landscape,
                (int)inner.x,
                (int)inner.y);
            try
            {
                WithPinnedHubCanvas(ExpandedScalerCanvas, (hub, _, rt) =>
                {
                    var state = ResponsiveRectState.CreateDefault();
                    state.scaleFromDesignCanvas = true;
                    state.designScaleMode = ResponsiveDesignScaleMode.Width;
                    state.designCanvasSize = Vector2.zero;

                    Vector2 ratio = hub.GetLayoutRatio(
                        state,
                        ResponsiveDevice.FormFactor.Phone,
                        rt,
                        ResponsiveDevice.Orientation.Landscape);
                    Assert.Greater(Mathf.Abs(ratio.x - 1f), 1e-3f);
                });
            }
            finally
            {
                ResponsiveDevice.ClearSimulatorLayout();
            }
        }

        [Test]
        public void GetLayoutRatio_FoldExpandIdentity_NestedWidget_KeepsAuthoredDefaultScale()
        {
            Vector2 inner = ResponsiveCatalogDevices.FoldInnerLandscape;
            ResponsiveDevice.SetSimulatorLayout(
                ResponsiveDevice.FormFactor.Pad,
                ResponsiveDevice.Orientation.Landscape,
                (int)inner.x,
                (int)inner.y);
            try
            {
                WithPinnedHubCanvas(ExpandedScalerCanvas, (hub, canvasRt, _) =>
                {
                    var parent = new GameObject("ParentWidget", typeof(RectTransform));
                    var child = new GameObject("ChildWidget", typeof(RectTransform));
                    try
                    {
                        parent.transform.SetParent(canvasRt, false);
                        child.transform.SetParent(parent.transform, false);

                        var state = ResponsiveRectState.CreateDefault();
                        state.scaleFromDesignCanvas = true;
                        state.designScaleMode = ResponsiveDesignScaleMode.Width;
                        state.designCanvasSize = Vector2.zero;
                        state.defaultScale = new Vector2(1.1726f, 1.1726f);

                        hub.ApplyWidget(
                            parent.GetComponent<RectTransform>(),
                            state,
                            ResponsiveDevice.FormFactor.Pad,
                            ResponsiveDevice.Orientation.Landscape);

                        Vector2 childRatio = hub.GetLayoutRatio(
                            state,
                            ResponsiveDevice.FormFactor.Pad,
                            child.GetComponent<RectTransform>(),
                            ResponsiveDevice.Orientation.Landscape);
                        Assert.AreEqual(1f, childRatio.x, 1e-4f);
                        Assert.AreEqual(1f, childRatio.y, 1e-4f);

                        hub.ApplyWidget(
                            child.GetComponent<RectTransform>(),
                            state,
                            ResponsiveDevice.FormFactor.Pad,
                            ResponsiveDevice.Orientation.Landscape);
                        Assert.AreEqual(1.1726f, parent.transform.localScale.x, 1e-4f);
                        Assert.AreEqual(1.1726f, child.transform.localScale.x, 1e-4f);
                    }
                    finally
                    {
                        UnityEngine.Object.DestroyImmediate(parent);
                    }
                });
            }
            finally
            {
                ResponsiveDevice.ClearSimulatorLayout();
            }
        }

        [Test]
        public void ComputeLayoutRatio_WidthAndHeight_DifferOnMismatchedCanvas()
        {
            var state = new ResponsiveRectState
            {
                scaleFromDesignCanvas = true,
                designCanvasSize = new Vector2(500f, 1000f),
            };
            Vector2 current = new Vector2(1000f, 500f);

            state.designScaleMode = ResponsiveDesignScaleMode.Width;
            Vector2 width = state.ComputeLayoutRatio(
                current,
                ResponsiveDevice.FormFactor.Phone,
                ResponsiveDevice.Orientation.Portrait);
            state.designScaleMode = ResponsiveDesignScaleMode.Height;
            Vector2 height = state.ComputeLayoutRatio(
                current,
                ResponsiveDevice.FormFactor.Phone,
                ResponsiveDevice.Orientation.Portrait);

            Assert.AreEqual(2f, width.x, 1e-4f);
            Assert.AreEqual(2f, width.y, 1e-4f);
            Assert.AreEqual(0.5f, height.x, 1e-4f);
            Assert.AreEqual(0.5f, height.y, 1e-4f);
        }

        [Test]
        public void ResidualRatio_NestedWidget_DoesNotCompound()
        {
            Vector2 nested = ResponsiveRectState.ResidualRatio(
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f));
            Assert.AreEqual(1f, nested.x, 1e-4f);
            Assert.AreEqual(1f, nested.y, 1e-4f);

            Vector2 root = ResponsiveRectState.ResidualRatio(
                new Vector2(0.5f, 0.5f),
                Vector2.one);
            Assert.AreEqual(0.5f, root.x, 1e-4f);
            Assert.AreEqual(0.5f, root.y, 1e-4f);
        }

        [Test]
        public void ResidualRatio_IdentityDesired_IgnoresAncestorAuthoredScale()
        {
            Vector2 nested = ResponsiveRectState.ResidualRatio(
                Vector2.one,
                new Vector2(1.1726f, 1.1726f));
            Assert.AreEqual(1f, nested.x, 1e-4f);
            Assert.AreEqual(1f, nested.y, 1e-4f);
        }

        [Test]
        public void GetLayoutRatio_ChildUnderScaledParent_UsesResidual()
        {
            WithPinnedHubCanvas(new Vector2(2000f, 2000f), (hub, canvasRt, _) =>
            {
                var parent = new GameObject("ParentWidget", typeof(RectTransform));
                var child = new GameObject("ChildWidget", typeof(RectTransform));
                try
                {
                    parent.transform.SetParent(canvasRt, false);
                    child.transform.SetParent(parent.transform, false);
                    parent.transform.localScale = new Vector3(0.5f, 0.5f, 1f);

                    var state = ResponsiveRectState.CreateDefault();
                    state.scaleFromDesignCanvas = true;
                    state.designCanvasSize = new Vector2(4000f, 4000f);
                    state.designScaleMode = ResponsiveDesignScaleMode.Width;

                    Vector2 parentRatio = hub.GetLayoutRatio(
                        state,
                        ResponsiveDevice.FormFactor.Phone,
                        parent.GetComponent<RectTransform>(),
                        ResponsiveDevice.Orientation.Portrait);
                    Vector2 childRatio = hub.GetLayoutRatio(
                        state,
                        ResponsiveDevice.FormFactor.Phone,
                        child.GetComponent<RectTransform>(),
                        ResponsiveDevice.Orientation.Portrait);

                    Assert.AreEqual(0.5f, parentRatio.x, 1e-4f);
                    Assert.AreEqual(0.5f, parentRatio.y, 1e-4f);
                    Assert.AreEqual(1f, childRatio.x, 1e-4f);
                    Assert.AreEqual(1f, childRatio.y, 1e-4f);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(parent);
                }
            });
        }

        [Test]
        public void NormalizeLayoutRatio_ZeroNearOneAndNonFinite_BecomeIdentity()
        {
            Vector2 zero = ResponsiveRectState.NormalizeLayoutRatio(Vector2.zero);
            Assert.AreEqual(1f, zero.x, 1e-6f);
            Assert.AreEqual(1f, zero.y, 1e-6f);

            Vector2 tiny = ResponsiveRectState.NormalizeLayoutRatio(new Vector2(1e-6f, 1f));
            Assert.AreEqual(1f, tiny.x, 1e-6f);
            Assert.AreEqual(1f, tiny.y, 1e-6f);

            Vector2 nonFinite = ResponsiveRectState.NormalizeLayoutRatio(
                new Vector2(float.NaN, float.PositiveInfinity));
            Assert.AreEqual(1f, nonFinite.x, 1e-6f);
            Assert.AreEqual(1f, nonFinite.y, 1e-6f);

            Vector2 kept = ResponsiveRectState.NormalizeLayoutRatio(new Vector2(0.5f, 2f));
            Assert.AreEqual(0.5f, kept.x, 1e-6f);
            Assert.AreEqual(2f, kept.y, 1e-6f);
        }

        [Test]
        public void FromAndApply_ZeroRatio_TreatedAsIdentity()
        {
            WithParentChild(new Vector2(400f, 600f), (rt, _) =>
            {
                rt.anchoredPosition = new Vector2(40f, -10f);
                rt.sizeDelta = new Vector2(80f, 30f);
                rt.localScale = new Vector3(1.2f, 0.8f, 1f);

                ResponsiveRectState captured = ResponsiveRectState.From(
                    rt,
                    Vector2.zero,
                    ResponsiveDevice.FormFactor.Phone);
                captured.enabled = true;
                captured.Apply(rt, Vector2.zero, captured.defaultScale);

                Assert.AreEqual(40f, rt.anchoredPosition.x, 1e-3f);
                Assert.AreEqual(-10f, rt.anchoredPosition.y, 1e-3f);
                Assert.AreEqual(80f, rt.sizeDelta.x, 1e-3f);
                Assert.AreEqual(30f, rt.sizeDelta.y, 1e-3f);
                Assert.AreEqual(1.2f, rt.localScale.x, 1e-4f);
                Assert.AreEqual(0.8f, rt.localScale.y, 1e-4f);
            });
        }

        [Test]
        public void DesignScaleOff_UnderScaledParent_KeepsAuthoredPixels()
        {
            WithParentChild(new Vector2(400f, 600f), (rt, parentRt) =>
            {
                parentRt.localScale = new Vector3(0.5f, 0.5f, 1f);
                var state = FixedCenterState(ResponsiveDesignScaleMode.Width);
                state.scaleFromDesignCanvas = false;
                state.Apply(rt, currentCanvasSize: new Vector2(400f, 600f), slotForm: ResponsiveDevice.FormFactor.Phone);

                Assert.AreEqual(new Vector2(100f, -40f), rt.anchoredPosition);
                Assert.AreEqual(new Vector2(200f, 80f), rt.sizeDelta);
                Assert.AreEqual(1f, rt.localScale.x, 1e-4f);
                Assert.AreEqual(1f, rt.localScale.y, 1e-4f);
            });
        }

        [Test]
        public void ComputeCanvasRatio_SquareCurrent_DoesNotSwapLandscapeDesign()
        {
            Vector2 ratio = ResponsiveRectState.ComputeCanvasRatio(
                new Vector2(1000f, 1000f),
                new Vector2(2000f, 1000f),
                ResponsiveDesignScaleMode.Width);
            Assert.AreEqual(0.5f, ratio.x, 1e-4f);
            Assert.AreEqual(0.5f, ratio.y, 1e-4f);
        }

        [Test]
        public void ComputeLayoutRatio_ExplicitLandscape_OnSquareCanvas_UsesLandscapeDesign()
        {
            var state = new ResponsiveRectState
            {
                scaleFromDesignCanvas = true,
                designScaleMode = ResponsiveDesignScaleMode.Width,
                designCanvasSize = new Vector2(1000f, 2000f),
            };

            Vector2 inferred = state.ComputeLayoutRatio(
                new Vector2(1000f, 1000f),
                ResponsiveDevice.FormFactor.Phone);
            Vector2 landscape = state.ComputeLayoutRatio(
                new Vector2(1000f, 1000f),
                ResponsiveDevice.FormFactor.Phone,
                ResponsiveDevice.Orientation.Landscape);

            Assert.AreEqual(1f, inferred.x, 1e-4f);
            Assert.AreEqual(0.5f, landscape.x, 1e-4f);
        }

        [Test]
        public void IsAxisStretched_AnyUnequalAnchors_IsStretch()
        {
            Assert.IsTrue(ResponsiveRectState.IsAxisStretched(0f, float.Epsilon));
            Assert.IsFalse(ResponsiveRectState.IsAxisStretched(0.5f, 0.5f));
        }

        [Test]
        public void ResolveArtSize_UsesSpriteRect_WhenPresent()
        {
            var go = new GameObject("ArtSize", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            try
            {
                var image = go.GetComponent<Image>();
                var tex = new Texture2D(40, 20);
                try
                {
                    image.sprite = Sprite.Create(tex, new Rect(0, 0, 40, 20), new Vector2(0.5f, 0.5f), 100f);
                    Vector2 art = ResponsiveBackgroundCover.ResolveArtSize(image, ResponsiveCatalogDevices.PadLandscape);
                    Assert.AreEqual(40f, art.x, 1e-4f);
                    Assert.AreEqual(20f, art.y, 1e-4f);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(tex);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void ResolveArtSize_FallsBack_WhenNoSprite()
        {
            var go = new GameObject("ArtFallback", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            try
            {
                Vector2 fallback = ResponsiveCatalogDevices.PadLandscape;
                Vector2 art = ResponsiveBackgroundCover.ResolveArtSize(go.GetComponent<Image>(), fallback);
                Assert.AreEqual(fallback, art);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void ComputeDisplaySize_Cover_FitsMaxAxis()
        {
            Vector2 display = ResponsiveBackgroundCover.ComputeDisplaySize(
                new Vector2(400f, 200f),
                new Vector2(800f, 600f),
                ResponsiveBgCoverMode.Cover);
            Assert.AreEqual(1200f, display.x, 1e-4f);
            Assert.AreEqual(600f, display.y, 1e-4f);
        }

        [Test]
        public void ComputeDisplaySize_Fit_FitsMinAxis()
        {
            Vector2 display = ResponsiveBackgroundCover.ComputeDisplaySize(
                new Vector2(400f, 200f),
                new Vector2(800f, 600f),
                ResponsiveBgCoverMode.Fit);
            Assert.AreEqual(800f, display.x, 1e-4f);
            Assert.AreEqual(400f, display.y, 1e-4f);
        }

        [Test]
        public void ComputeDisplaySize_Stretch_MatchesParent()
        {
            Vector2 parent = new Vector2(800f, 600f);
            Vector2 display = ResponsiveBackgroundCover.ComputeDisplaySize(
                new Vector2(400f, 200f),
                parent,
                ResponsiveBgCoverMode.Stretch);
            Assert.AreEqual(parent, display);
        }

        [Test]
        public void ComputeDisplaySize_HeightOnly_MatchesCoverDisplaySize()
        {
            Vector2 art = new Vector2(400f, 200f);
            Vector2 parent = new Vector2(800f, 600f);
            Vector2 cover = ResponsiveBackgroundCover.ComputeDisplaySize(art, parent, ResponsiveBgCoverMode.Cover);
            Vector2 heightOnly = ResponsiveBackgroundCover.ComputeDisplaySize(
                art, parent, ResponsiveBgCoverMode.HeightOnly);
            Assert.AreEqual(cover, heightOnly);
        }

        [Test]
        public void ComputeDisplaySize_WidthOnly_MatchesCoverDisplaySize()
        {
            Vector2 art = new Vector2(400f, 200f);
            Vector2 parent = new Vector2(800f, 600f);
            Vector2 cover = ResponsiveBackgroundCover.ComputeDisplaySize(art, parent, ResponsiveBgCoverMode.Cover);
            Vector2 widthOnly = ResponsiveBackgroundCover.ComputeDisplaySize(
                art, parent, ResponsiveBgCoverMode.WidthOnly);
            Assert.AreEqual(cover, widthOnly);
        }

        [Test]
        public void ComputeDisplaySize_FitWidth_MatchesParentWidth()
        {
            Vector2 display = ResponsiveBackgroundCover.ComputeDisplaySize(
                new Vector2(400f, 200f),
                new Vector2(800f, 600f),
                ResponsiveBgCoverMode.FitWidth);
            Assert.AreEqual(800f, display.x, 1e-4f);
            Assert.AreEqual(400f, display.y, 1e-4f);
        }

        [Test]
        public void ComputeDisplaySize_FitHeight_MatchesParentHeight()
        {
            Vector2 display = ResponsiveBackgroundCover.ComputeDisplaySize(
                new Vector2(400f, 200f),
                new Vector2(800f, 600f),
                ResponsiveBgCoverMode.FitHeight);
            Assert.AreEqual(1200f, display.x, 1e-4f);
            Assert.AreEqual(600f, display.y, 1e-4f);
        }

        [Test]
        public void ComputeDisplaySize_Manual_ReturnsZero()
        {
            Assert.AreEqual(
                Vector2.zero,
                ResponsiveBackgroundCover.ComputeDisplaySize(
                    new Vector2(400f, 200f),
                    new Vector2(800f, 600f),
                    ResponsiveBgCoverMode.Manual));
        }

        [Test]
        public void OrientSize_PortraitToLandscape_SwapsAxes()
        {
            Vector2 landscape = ResponsiveUI.OrientSize(
                ResponsiveCatalogDevices.PhonePortrait,
                ResponsiveDevice.Orientation.Landscape);
            Assert.AreEqual(ResponsiveCatalogDevices.PhoneLandscape.x, landscape.x, 1e-4f);
            Assert.AreEqual(ResponsiveCatalogDevices.PhoneLandscape.y, landscape.y, 1e-4f);
        }

        [Test]
        public void BuiltinOriginalBgSize_IsIpadPro13M5Landscape()
        {
            Vector2 expected = ResponsiveUI.OrientSize(
                ResponsiveRectState.BuiltinPadDesignCanvasSize,
                ResponsiveDevice.Orientation.Landscape);
            Assert.AreEqual(expected, ResponsiveUI.BuiltinOriginalBgSize);
            Assert.AreEqual(ResponsiveCatalogDevices.PadLandscape, ResponsiveUI.BuiltinOriginalBgSize);
        }

        [Test]
        public void OrientSize_AlreadyLandscape_Unchanged()
        {
            Vector2 size = ResponsiveUI.BuiltinOriginalBgSize;
            Vector2 oriented = ResponsiveUI.OrientSize(size, ResponsiveDevice.Orientation.Landscape);
            Assert.AreEqual(size, oriented);
        }

        [Test]
        public void OrientSize_LandscapeToPortrait_SwapsAxes()
        {
            Vector2 portrait = ResponsiveUI.OrientSize(
                ResponsiveUI.BuiltinOriginalBgSize,
                ResponsiveDevice.Orientation.Portrait);
            Assert.AreEqual(ResponsiveRectState.BuiltinPadDesignCanvasSize, portrait);
        }

        [Test]
        public void GetDefaultOriginalBgSize_FallsBackToBuiltinWithoutProvider()
        {
            Func<Vector2> previous = ResponsiveUI.OriginalBgSizeProvider;
            ResponsiveUI.OriginalBgSizeProvider = null;
            try
            {
                Assert.AreEqual(ResponsiveUI.BuiltinOriginalBgSize, ResponsiveUI.GetDefaultOriginalBgSize());
            }
            finally
            {
                ResponsiveUI.OriginalBgSizeProvider = previous;
            }
        }

        [Test]
        public void GetDefaultOriginalBgSize_UsesProvider()
        {
            Func<Vector2> previous = ResponsiveUI.OriginalBgSizeProvider;
            ResponsiveUI.OriginalBgSizeProvider = () => ResponsiveCatalogDevices.PhoneLandscape;
            try
            {
                Assert.AreEqual(ResponsiveCatalogDevices.PhoneLandscape, ResponsiveUI.GetDefaultOriginalBgSize());
            }
            finally
            {
                ResponsiveUI.OriginalBgSizeProvider = previous;
            }
        }

        [Test]
        public void ComputeAppliedLocalScale_Off_RestoresOrigin()
        {
            var state = new ResponsiveRectState
            {
                scaleFromDesignCanvas = false,
                designCanvasSize = ResponsiveCatalogDevices.PhonePortrait,
                defaultScale = new Vector2(2f, 1.5f),
            };

            Vector2 local = state.ComputeAppliedLocalScale(
                ResponsiveCatalogDevices.PhonePortrait * 0.5f,
                ResponsiveDevice.FormFactor.Phone);

            Assert.AreEqual(2f, local.x, 1e-4f);
            Assert.AreEqual(1.5f, local.y, 1e-4f);
        }

        [Test]
        public void UnscaleLivePixels_DividesByRatio()
        {
            Vector2 design = ResponsiveRectState.UnscaleLivePixels(
                new Vector2(50f, 80f),
                new Vector2(0.5f, 0.5f));

            Assert.AreEqual(100f, design.x, 1e-4f);
            Assert.AreEqual(160f, design.y, 1e-4f);
        }

        [Test]
        public void SlotFormFactor_MapsTwentySlotsInEnumOrder()
        {
            var expected = new[]
            {
                ResponsiveDevice.FormFactor.Phone,
                ResponsiveDevice.FormFactor.Pad,
                ResponsiveDevice.FormFactor.FoldPhoneCover,
                ResponsiveDevice.FormFactor.FoldHalfOpened,
                ResponsiveDevice.FormFactor.FoldCoverHalfOpened,
                ResponsiveDevice.FormFactor.FlipPhoneCover,
                ResponsiveDevice.FormFactor.FlipHalfOpened,
                ResponsiveDevice.FormFactor.iPhoneDuoCover,
                ResponsiveDevice.FormFactor.iPhoneDuoSeated,
                ResponsiveDevice.FormFactor.WideScreen,
            };
            CollectionAssert.AreEqual(expected, ResponsiveWidgetLayout.FormFactors);

            Assert.AreEqual(20, ResponsiveWidgetLayout.StatePropertyNames.Length);
            Assert.AreEqual("iPhoneDuoSeatedPortrait", ResponsiveWidgetLayout.StatePropertyNames[16]);
            Assert.AreEqual("iPhoneDuoSeatedLandscape", ResponsiveWidgetLayout.StatePropertyNames[17]);
            Assert.AreEqual("wideScreenLandscape", ResponsiveWidgetLayout.StatePropertyNames[19]);
            for (int i = 0; i < ResponsiveWidgetLayout.FormFactors.Length; i++)
            {
                // The enum value is the slot order: form i owns slots 2i and 2i + 1.
                Assert.AreEqual(i, (int)ResponsiveWidgetLayout.FormFactors[i]);
                Assert.AreEqual(
                    ResponsiveWidgetLayout.FormFactors[i],
                    ResponsiveWidgetLayout.SlotFormFactor(i * 2));
                Assert.AreEqual(
                    ResponsiveWidgetLayout.FormFactors[i],
                    ResponsiveWidgetLayout.SlotFormFactor(i * 2 + 1));
            }
        }

        [Test]
        public void ComputeCanvasRatio_AllModes_UseHandCalculatedTable()
        {
            Vector2 current = new Vector2(800f, 1200f);
            Vector2 design = new Vector2(1000f, 2000f);

            Assert.AreEqual(
                new Vector2(0.8f, 0.8f),
                ResponsiveRectState.ComputeCanvasRatio(
                    current, design, ResponsiveDesignScaleMode.Width));
            Assert.AreEqual(
                new Vector2(0.6f, 0.6f),
                ResponsiveRectState.ComputeCanvasRatio(
                    current, design, ResponsiveDesignScaleMode.Height));
            Assert.AreEqual(
                new Vector2(0.6f, 0.6f),
                ResponsiveRectState.ComputeCanvasRatio(
                    current, design, ResponsiveDesignScaleMode.UniformMin));
            Assert.AreEqual(
                new Vector2(0.8f, 0.8f),
                ResponsiveRectState.ComputeCanvasRatio(
                    current, design, ResponsiveDesignScaleMode.UniformMax));
            Assert.AreEqual(
                new Vector2(0.8f, 0.6f),
                ResponsiveRectState.ComputeCanvasRatio(
                    current, design, ResponsiveDesignScaleMode.IndependentXY));
        }

        [Test]
        public void DesignScale_FixedCenter_WidthAndHeight_ScaleAuthoredPixelsIndependently()
        {
            WithParentChild(new Vector2(400f, 600f), (rt, _) =>
            {
                var state = FixedCenterState(ResponsiveDesignScaleMode.Width);
                state.Apply(rt, currentCanvasSize: new Vector2(400f, 600f), slotForm: ResponsiveDevice.FormFactor.Phone);

                Assert.AreEqual(new Vector2(50f, -20f), rt.anchoredPosition);
                Assert.AreEqual(new Vector2(200f, 80f), rt.sizeDelta);
                Assert.AreEqual(0.5f, rt.localScale.x, 1e-4f);
                Assert.AreEqual(0.5f, rt.localScale.y, 1e-4f);
                Assert.AreEqual(100f, rt.rect.width * rt.localScale.x, 1e-3f);

                state.designScaleMode = ResponsiveDesignScaleMode.Height;
                state.Apply(rt, currentCanvasSize: new Vector2(400f, 600f), slotForm: ResponsiveDevice.FormFactor.Phone);

                Assert.AreEqual(new Vector2(75f, -30f), rt.anchoredPosition);
                Assert.AreEqual(new Vector2(200f, 80f), rt.sizeDelta);
                Assert.AreEqual(0.75f, rt.localScale.x, 1e-4f);
                Assert.AreEqual(0.75f, rt.localScale.y, 1e-4f);
                Assert.AreEqual(150f, rt.rect.width * rt.localScale.x, 1e-3f);
            });
        }

        [Test]
        public void DesignScale_HorizontalStretch_Width_KeepsParentSpanPlusScaledInsets()
        {
            WithParentChild(new Vector2(400f, 600f), (rt, _) =>
            {
                var state = new ResponsiveRectState
                {
                    enabled = true,
                    scaleFromDesignCanvas = true,
                    designScaleMode = ResponsiveDesignScaleMode.Width,
                    designCanvasSize = new Vector2(800f, 800f),
                    defaultScale = Vector2.one,
                    anchorMin = new Vector2(0f, 0.5f),
                    anchorMax = new Vector2(1f, 0.5f),
                    anchoredPosition = new Vector2(0f, 20f),
                    sizeDelta = new Vector2(-40f, 50f),
                    pivot = new Vector2(0.5f, 0.5f),
                };

                state.Apply(rt, currentCanvasSize: new Vector2(400f, 600f), slotForm: ResponsiveDevice.FormFactor.Phone);

                Assert.AreEqual(new Vector2(0f, 10f), rt.anchoredPosition);
                Assert.AreEqual(new Vector2(360f, 50f), rt.sizeDelta);
                Assert.AreEqual(0.5f, rt.localScale.x, 1e-4f);
                Assert.AreEqual(0.5f, rt.localScale.y, 1e-4f);
                Assert.AreEqual(380f, rt.rect.width * rt.localScale.x, 1e-3f);
            });
        }

        [Test]
        public void DesignScale_VerticalStretch_Width_KeepsParentSpanPlusScaledInsets()
        {
            WithParentChild(new Vector2(400f, 600f), (rt, _) =>
            {
                var state = new ResponsiveRectState
                {
                    enabled = true,
                    scaleFromDesignCanvas = true,
                    designScaleMode = ResponsiveDesignScaleMode.Width,
                    designCanvasSize = new Vector2(800f, 800f),
                    defaultScale = Vector2.one,
                    anchorMin = new Vector2(0.5f, 0f),
                    anchorMax = new Vector2(0.5f, 1f),
                    anchoredPosition = new Vector2(30f, 0f),
                    sizeDelta = new Vector2(60f, -60f),
                    pivot = new Vector2(0.5f, 0.5f),
                };

                state.Apply(rt, currentCanvasSize: new Vector2(400f, 600f), slotForm: ResponsiveDevice.FormFactor.Phone);

                Assert.AreEqual(new Vector2(15f, 0f), rt.anchoredPosition);
                Assert.AreEqual(new Vector2(60f, 540f), rt.sizeDelta);
                Assert.AreEqual(0.5f, rt.localScale.x, 1e-4f);
                Assert.AreEqual(0.5f, rt.localScale.y, 1e-4f);
                Assert.AreEqual(570f, rt.rect.height * rt.localScale.y, 1e-3f);
            });
        }

        [Test]
        public void DesignScale_FullStretch_Width_FillsParentPlusScaledInsets()
        {
            WithParentChild(new Vector2(400f, 600f), (rt, _) =>
            {
                var state = new ResponsiveRectState
                {
                    enabled = true,
                    scaleFromDesignCanvas = true,
                    designScaleMode = ResponsiveDesignScaleMode.Width,
                    designCanvasSize = new Vector2(800f, 800f),
                    defaultScale = Vector2.one,
                    anchorMin = Vector2.zero,
                    anchorMax = Vector2.one,
                    anchoredPosition = Vector2.zero,
                    sizeDelta = new Vector2(-80f, -40f),
                    pivot = new Vector2(0.5f, 0.5f),
                };

                state.Apply(rt, currentCanvasSize: new Vector2(400f, 600f), slotForm: ResponsiveDevice.FormFactor.Phone);

                Assert.AreEqual(Vector2.zero, rt.anchoredPosition);
                Assert.AreEqual(new Vector2(320f, 560f), rt.sizeDelta);
                Assert.AreEqual(0.5f, rt.localScale.x, 1e-4f);
                Assert.AreEqual(0.5f, rt.localScale.y, 1e-4f);
                Assert.AreEqual(360f, rt.rect.width * rt.localScale.x, 1e-3f);
                Assert.AreEqual(580f, rt.rect.height * rt.localScale.y, 1e-3f);
            });
        }

        [Test]
        public void WidgetFallback_PadRequest_UsesPhoneDesignCanvas()
        {
            WithParentChild(new Vector2(400f, 600f), (rt, _) =>
            {
                var widget = new ResponsiveWidgetLayout
                {
                    target = rt,
                    phonePortrait = FixedCenterState(
                        ResponsiveDesignScaleMode.Width,
                        Vector2.zero),
                };
                widget.padPortrait.enabled = false;

                Vector2 phoneLive = ResponsiveRectState.GetDefaultDesignCanvasSize(
                    ResponsiveDevice.FormFactor.Phone);
                widget.Apply(
                    ResponsiveDevice.FormFactor.Pad,
                    ResponsiveDevice.Orientation.Portrait,
                    phoneLive);

                Assert.AreEqual(new Vector2(100f, -40f), rt.anchoredPosition);
                Assert.AreEqual(Vector3.one, rt.localScale);
            });
        }

        [Test]
        public void ResolveActiveSlot_LandscapeFallsBackToPortrait()
        {
            var widget = new ResponsiveWidgetLayout
            {
                phonePortrait = new ResponsiveRectState { enabled = true },
                phoneLandscape = new ResponsiveRectState { enabled = false },
            };

            bool ok = widget.ResolveActiveSlot(
                new ResponsiveDevice.LayoutKey(
                    ResponsiveDevice.FormFactor.Phone,
                    ResponsiveDevice.Orientation.Landscape),
                out string propertyName,
                out ResponsiveDevice.FormFactor form,
                out ResponsiveRectState state);

            Assert.IsTrue(ok);
            Assert.AreEqual("phonePortrait", propertyName);
            Assert.AreEqual(ResponsiveDevice.FormFactor.Phone, form);
            Assert.IsTrue(state.enabled);
        }

        [Test]
        public void GetLayoutRatio_TargetOwnsCanvas_MeasuresParentPane()
        {
            WithPinnedHubCanvas(new Vector2(2000f, 2000f), (hub, _, rt) =>
            {
                // Override-sorting / batching Canvas on the Target itself (non-root).
                rt.gameObject.AddComponent<Canvas>();
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(100f, 100f);
                rt.ForceUpdateRectTransforms();
                Canvas.ForceUpdateCanvases();

                var state = ResponsiveRectState.CreateDefault();
                state.scaleFromDesignCanvas = true;
                state.designCanvasSize = new Vector2(4000f, 4000f);
                state.designScaleMode = ResponsiveDesignScaleMode.Width;

                Vector2 ratio = hub.GetLayoutRatio(
                    state,
                    ResponsiveDevice.FormFactor.Phone,
                    rt,
                    ResponsiveDevice.Orientation.Portrait);

                // Parent pane (2000) / design (4000), not own rect (100) / design.
                Assert.AreEqual(0.5f, ratio.x, 1e-4f);
                Assert.AreEqual(0.5f, ratio.y, 1e-4f);
                Assert.AreEqual(
                    hub.GetDesignScaleCurrentSize(),
                    hub.GetDesignScaleCurrentSize(rt),
                    "a Target-owned Canvas is not a constrained pane");
            });
        }

        [Test]
        public void AncestorLocalScale_TargetOwnsCanvas_StopsAtParentCanvas()
        {
            var root = new GameObject("ScaledRootCanvas", typeof(RectTransform), typeof(Canvas));
            var parent = new GameObject("ScaledParent", typeof(RectTransform));
            var target = new GameObject("CanvasTarget", typeof(RectTransform), typeof(Canvas));
            try
            {
                root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                root.transform.localScale = new Vector3(0.25f, 0.25f, 1f);
                parent.transform.SetParent(root.transform, false);
                parent.transform.localScale = new Vector3(0.5f, 0.5f, 1f);
                target.transform.SetParent(parent.transform, false);
                RectTransform rt = target.GetComponent<RectTransform>();

                Assert.AreSame(
                    root.GetComponent<Canvas>(),
                    ResponsiveRectState.GetParentCanvas(rt));

                Vector2 byDefault = ResponsiveRectState.AncestorLocalScale(rt);
                Assert.AreEqual(0.5f, byDefault.x, 1e-5f);
                Assert.AreEqual(0.5f, byDefault.y, 1e-5f);

                // A stop that is not a strict ancestor must not walk past the root Canvas.
                Vector2 selfStop = ResponsiveRectState.AncestorLocalScale(rt, rt);
                Assert.AreEqual(0.5f, selfStop.x, 1e-5f);
                Assert.AreEqual(0.5f, selfStop.y, 1e-5f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void GetLayoutRatio_FlipHalfOpenedMainWatch_PhoneFallbackSlot_IsNotIdentity()
        {
            Vector2 main = ResponsiveCatalogDevices.FlipMainLandscape;
            Vector2 phoneDesign = ResponsiveUI.OrientSize(
                ResponsiveRectState.GetDefaultDesignCanvasSize(ResponsiveDevice.FormFactor.Phone),
                ResponsiveDevice.Orientation.Landscape);
            ResponsiveDevice.SetSimulatorLayout(
                ResponsiveDevice.FormFactor.FlipHalfOpened,
                ResponsiveDevice.Orientation.Landscape,
                (int)main.x,
                (int)main.y);
            try
            {
                // Flip HalfOpened slot Disabled → Phone fallback keeps the Phone Design Canvas.
                Assert.IsFalse(ResponsiveDesignScale.IsDesignCanvasReferenceWatch(
                    ResponsiveDevice.FormFactor.Phone,
                    main,
                    ResponsiveDevice.Orientation.Landscape,
                    phoneDesign));
                // The exact Flip HalfOpened slot is still identity.
                Assert.IsTrue(ResponsiveDesignScale.IsDesignCanvasReferenceWatch(
                    ResponsiveDevice.FormFactor.FlipHalfOpened,
                    main,
                    ResponsiveDevice.Orientation.Landscape,
                    ResponsiveUI.OrientSize(
                        ResponsiveRectState.GetDefaultDesignCanvasSize(
                            ResponsiveDevice.FormFactor.FlipHalfOpened),
                        ResponsiveDevice.Orientation.Landscape)));

                WithPinnedHubCanvas(ExpandedScalerCanvas, (hub, _, rt) =>
                {
                    var state = ResponsiveRectState.CreateDefault();
                    state.scaleFromDesignCanvas = true;
                    state.designScaleMode = ResponsiveDesignScaleMode.Width;
                    state.designCanvasSize = Vector2.zero;

                    Vector2 ratio = hub.GetLayoutRatio(
                        state,
                        ResponsiveDevice.FormFactor.Phone,
                        rt,
                        ResponsiveDevice.Orientation.Landscape);
                    Assert.Greater(Mathf.Abs(ratio.x - 1f), 1e-3f);
                });

                // Flip Opened (Phone pin, no HalfOpened posture) keeps the size identity.
                ResponsiveDevice.SetSimulatorLayout(
                    ResponsiveDevice.FormFactor.Phone,
                    ResponsiveDevice.Orientation.Landscape,
                    (int)main.x,
                    (int)main.y);
                Assert.IsTrue(ResponsiveDesignScale.IsDesignCanvasReferenceWatch(
                    ResponsiveDevice.FormFactor.Phone,
                    main,
                    ResponsiveDevice.Orientation.Landscape,
                    phoneDesign));
            }
            finally
            {
                ResponsiveDevice.ClearSimulatorLayout();
            }
        }

        [Test]
        public void IsDesignCanvasReferenceWatch_IphoneDuoInner_PadFallbackMatchesFlatPad()
        {
            Vector2 inner = ResponsiveCatalogDevices.IphoneDuoInnerLandscape;
            Vector2 padDesign = ResponsiveUI.OrientSize(
                ResponsiveRectState.GetDefaultDesignCanvasSize(ResponsiveDevice.FormFactor.Pad),
                ResponsiveDevice.Orientation.Landscape);
            ResponsiveDevice.SetSimulatorLayout(
                ResponsiveDevice.FormFactor.FoldHalfOpened,
                ResponsiveDevice.Orientation.Landscape,
                (int)inner.x,
                (int)inner.y);
            try
            {
                // Disabled iPhone Duo Seated → Pad keeps the flat Pad scale (same inner panel).
                Assert.IsTrue(ResponsiveDesignScale.IsDesignCanvasReferenceWatch(
                    ResponsiveDevice.FormFactor.Pad,
                    inner,
                    ResponsiveDevice.Orientation.Landscape,
                    padDesign));
                Assert.IsTrue(ResponsiveDesignScale.IsDesignCanvasReferenceWatch(
                    ResponsiveDevice.FormFactor.iPhoneDuoSeated,
                    inner,
                    ResponsiveDevice.Orientation.Landscape,
                    ResponsiveUI.OrientSize(
                        ResponsiveRectState.GetDefaultDesignCanvasSize(
                            ResponsiveDevice.FormFactor.iPhoneDuoSeated),
                        ResponsiveDevice.Orientation.Landscape)));
            }
            finally
            {
                ResponsiveDevice.ClearSimulatorLayout();
            }
        }

        [Test]
        public void GetLayoutRatio_LandscapeToPortraitFallback_NoScaler_OrientsLiveSize()
        {
            Vector2 compact = ResponsiveCatalogDevices.CompactPhoneLandscape;
            ResponsiveDevice.SetSimulatorLayout(
                ResponsiveDevice.FormFactor.Phone,
                ResponsiveDevice.Orientation.Landscape,
                (int)compact.x,
                (int)compact.y);
            var go = new GameObject(
                "NoScalerHubCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(ResponsiveUI));
            var target = new GameObject("NoScalerWidget", typeof(RectTransform));
            try
            {
                go.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                ResponsiveUI hub = go.GetComponent<ResponsiveUI>();
                hub.enabled = false;
                RectTransform canvasRt = go.GetComponent<RectTransform>();
                canvasRt.sizeDelta = compact;
                target.transform.SetParent(go.transform, false);
                RectTransform rt = target.GetComponent<RectTransform>();
                Canvas.ForceUpdateCanvases();
                Assert.IsFalse(hub.UsesCanvasScalerResolutionFitFor(rt));

                var state = ResponsiveRectState.CreateDefault();
                state.scaleFromDesignCanvas = true;
                state.designScaleMode = ResponsiveDesignScaleMode.Auto;
                state.designCanvasSize = Vector2.zero;

                // Phone Landscape Disabled → Phone Portrait slot on a landscape display.
                Vector2 fallback = hub.GetLayoutRatio(
                    state,
                    ResponsiveDevice.FormFactor.Phone,
                    rt,
                    ResponsiveDevice.Orientation.Portrait);
                Vector2 expected = state.ComputeLayoutRatio(
                    ResponsiveUI.OrientSize(compact, ResponsiveDevice.Orientation.Portrait),
                    ResponsiveDevice.FormFactor.Phone,
                    ResponsiveDevice.Orientation.Portrait);
                Assert.AreEqual(expected.x, fallback.x, 1e-4f);
                Assert.AreEqual(expected.y, fallback.y, 1e-4f);

                Vector2 unoriented = state.ComputeLayoutRatio(
                    compact,
                    ResponsiveDevice.FormFactor.Phone,
                    ResponsiveDevice.Orientation.Portrait);
                Assert.Greater(
                    Mathf.Abs(unoriented.x - fallback.x),
                    0.05f,
                    "fallback must not compare a landscape display to a portrait design");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                ResponsiveDevice.ClearSimulatorLayout();
            }
        }

        static void AssertAnchorPresetRoundTrip(
            Vector2 anchorMin,
            Vector2 anchorMax,
            ResponsiveDesignScaleMode mode,
            string message)
        {
            var parent = new GameObject("AnchorParent", typeof(RectTransform));
            var go = new GameObject("AnchorPreset", typeof(RectTransform));
            try
            {
                Vector2 expectedPosition = new Vector2(120f, -85f);
                Vector2 expectedSize = new Vector2(-140f, 210f);
                Vector2 expectedScale = new Vector2(1.2f, 0.8f);
                Vector2 expectedPivot = new Vector2(0.25f, 0.75f);
                Vector2 currentSize = new Vector2(500f, 750f);
                Vector2 designSize = new Vector2(1000f, 1000f);
                parent.GetComponent<RectTransform>().sizeDelta = currentSize;
                go.transform.SetParent(parent.transform, false);
                RectTransform rt = go.GetComponent<RectTransform>();
                Vector2 ratio = ResponsiveRectState.ComputeCanvasRatio(
                    currentSize,
                    designSize,
                    mode);

                rt.anchorMin = anchorMin;
                rt.anchorMax = anchorMax;
                rt.pivot = expectedPivot;
                rt.anchoredPosition = expectedPosition;
                rt.sizeDelta = expectedSize;
                rt.localScale = new Vector3(expectedScale.x, expectedScale.y, 1f);

                ResponsiveRectState state = ResponsiveRectState.From(
                    rt,
                    ratio,
                    ResponsiveDevice.FormFactor.Phone);
                state.scaleFromDesignCanvas = true;
                state.designScaleMode = mode;
                state.designCanvasSize = designSize;

                rt.anchorMin = rt.anchorMax = Vector2.zero;
                rt.pivot = Vector2.one * 0.5f;
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = Vector2.zero;
                rt.localScale = Vector3.one;

                state.Apply(
                    rt,
                    currentCanvasSize: currentSize,
                    slotForm: ResponsiveDevice.FormFactor.Phone);

                Assert.AreEqual(anchorMin, rt.anchorMin, message);
                Assert.AreEqual(anchorMax, rt.anchorMax, message);
                Assert.AreEqual(expectedPivot, rt.pivot, message);
                Assert.AreEqual(expectedPosition.x, rt.anchoredPosition.x, 1e-3f, message);
                Assert.AreEqual(expectedPosition.y, rt.anchoredPosition.y, 1e-3f, message);
                Assert.AreEqual(expectedSize.x, rt.sizeDelta.x, 1e-3f, message);
                Assert.AreEqual(expectedSize.y, rt.sizeDelta.y, 1e-3f, message);
                Assert.AreEqual(expectedScale.x, rt.localScale.x, 1e-3f, message);
                Assert.AreEqual(expectedScale.y, rt.localScale.y, 1e-3f, message);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(parent);
            }
        }

        static ResponsiveRectState FixedCenterState(
            ResponsiveDesignScaleMode mode,
            Vector2? designCanvasSize = null) =>
            new ResponsiveRectState
            {
                enabled = true,
                scaleFromDesignCanvas = true,
                designScaleMode = mode,
                designCanvasSize = designCanvasSize ?? new Vector2(800f, 800f),
                defaultScale = Vector2.one,
                anchorMin = new Vector2(0.5f, 0.5f),
                anchorMax = new Vector2(0.5f, 0.5f),
                anchoredPosition = new Vector2(100f, -40f),
                sizeDelta = new Vector2(200f, 80f),
                pivot = new Vector2(0.5f, 0.5f),
            };

        static void WithParentChild(Vector2 parentSize, System.Action<RectTransform, RectTransform> body)
        {
            var parent = new GameObject("DesignScaleParent", typeof(RectTransform));
            var child = new GameObject("DesignScaleChild", typeof(RectTransform));
            try
            {
                RectTransform parentRt = parent.GetComponent<RectTransform>();
                parentRt.sizeDelta = parentSize;
                child.transform.SetParent(parent.transform, false);
                body(child.GetComponent<RectTransform>(), parentRt);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(parent);
            }
        }

        static Vector2 ExpandedScalerCanvas => new Vector2(
            ResponsiveCatalogDevices.CompactPhoneLandscape.x,
            1602.402f);

        static void AssertDefaultProfileIdentity(
            Vector2 watch,
            ResponsiveDevice.FormFactor pinForm,
            ResponsiveDevice.FormFactor slotForm)
        {
            ResponsiveDevice.SetSimulatorLayout(
                pinForm,
                ResponsiveDevice.Orientation.Landscape,
                (int)watch.x,
                (int)watch.y);
            // The wide screen's slot: Landscape, or Portrait for Fold Half Opened (posture name).
            ResponsiveDevice.Orientation slotOrientation = ResponsiveDevice.OrientationForForm(
                slotForm, ResponsiveDevice.Orientation.Landscape);
            try
            {
                WithPinnedHubCanvas(ExpandedScalerCanvas, (hub, _, rt) =>
                {
                    var state = ResponsiveRectState.CreateDefault();
                    state.scaleFromDesignCanvas = true;
                    state.designScaleMode = ResponsiveDesignScaleMode.Width;
                    state.designCanvasSize = Vector2.zero;
                    state.defaultScale = new Vector2(1.1726f, 1.1726f);
                    state.anchoredPosition = new Vector2(0f, -551f);
                    state.sizeDelta = new Vector2(1200f, 250f);

                    Vector2 ratio = hub.GetLayoutRatio(
                        state,
                        slotForm,
                        rt,
                        slotOrientation);
                    Assert.AreEqual(1f, ratio.x, 1e-4f);
                    Assert.AreEqual(1f, ratio.y, 1e-4f);

                    hub.ApplyWidget(
                        rt,
                        state,
                        slotForm,
                        slotOrientation);
                    Assert.AreEqual(0f, rt.anchoredPosition.x, 1e-3f);
                    Assert.AreEqual(-551f, rt.anchoredPosition.y, 1e-3f);
                    Assert.AreEqual(1200f, rt.sizeDelta.x, 1e-3f);
                    Assert.AreEqual(250f, rt.sizeDelta.y, 1e-3f);
                    Assert.AreEqual(1.1726f, rt.localScale.x, 1e-4f);
                    Assert.AreEqual(1.1726f, rt.localScale.y, 1e-4f);
                });
            }
            finally
            {
                ResponsiveDevice.ClearSimulatorLayout();
            }
        }

        [Test]
        public void ApplyCanvasScaler_NormalizesScreenMatchMode()
        {
            var go = new GameObject(
                "ResponsiveScalerMode",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(ResponsiveUI));
            try
            {
                CanvasScaler scaler = go.GetComponent<CanvasScaler>();
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

                ResponsiveUI hub = go.GetComponent<ResponsiveUI>();
                hub.enabled = false;
                var so = new SerializedObject(hub);
                so.FindProperty("overrideInEditor").boolValue = true;
                so.FindProperty("editorFormFactor").enumValueIndex =
                    (int)ResponsiveDevice.FormFactor.Pad;
                so.FindProperty("editorOrientation").enumValueIndex =
                    (int)ResponsiveDevice.Orientation.Landscape;
                so.FindProperty("applyCanvasScaler").boolValue = true;
                so.FindProperty("canvasScaler").objectReferenceValue = scaler;
                so.ApplyModifiedPropertiesWithoutUndo();

                hub.enabled = true;

                Assert.AreEqual(
                    CanvasScaler.ScreenMatchMode.MatchWidthOrHeight,
                    scaler.screenMatchMode);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        static void WithPinnedHubCanvas(
            Vector2 canvasSize,
            System.Action<ResponsiveUI, RectTransform, RectTransform> body)
        {
            var go = new GameObject(
                "PinnedHubCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(ResponsiveUI));
            var target = new GameObject("PinnedWidget", typeof(RectTransform));
            try
            {
                Canvas canvas = go.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;

                CanvasScaler scaler = go.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                scaler.enabled = true;

                ResponsiveUI hub = go.GetComponent<ResponsiveUI>();
                hub.enabled = false;
                var so = new SerializedObject(hub);
                SerializedProperty applyScaler = so.FindProperty("applyCanvasScaler");
                Assert.NotNull(applyScaler, "applyCanvasScaler is serialized on the hub");
                so.FindProperty("canvasScaler").objectReferenceValue = scaler;
                applyScaler.boolValue = true;
                so.ApplyModifiedPropertiesWithoutUndo();

                RectTransform canvasRt = go.GetComponent<RectTransform>();
                canvasRt.anchorMin = canvasRt.anchorMax = new Vector2(0.5f, 0.5f);
                canvasRt.pivot = new Vector2(0.5f, 0.5f);
                canvasRt.localScale = Vector3.one;
                canvasRt.sizeDelta = canvasSize;
                target.transform.SetParent(go.transform, false);
                canvasRt.ForceUpdateRectTransforms();
                Canvas.ForceUpdateCanvases();

                Assert.IsTrue(hub.UsesCanvasScalerResolutionFit);
                Assert.AreEqual(canvasSize.x, canvasRt.rect.width, 1e-3f, "pinned canvas width");
                Assert.AreEqual(canvasSize.y, canvasRt.rect.height, 1e-3f, "pinned canvas height");

                body(hub, canvasRt, target.GetComponent<RectTransform>());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }
    }
}
