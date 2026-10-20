using FDPlus.Foldable;
using FDPlus.Foldable.Editor;
using FDPlus.IphoneDuo;
using FDPlus.PhonePad.Editor;
using FDPlus.ResponsiveUI;
using FDPlus.ResponsiveUI.Shared.Editor;
using FDPlus.ResponsiveUI.Editor;
using NUnit.Framework;
using UnityEngine;

namespace FDPlus.ResponsiveUI.Tests
{
    /// <summary>
    /// Fold8 Cover must stay FoldPhoneCover — aspect alone (~1.58) would mis-detect as Pad.
    /// Flip Cover (~1.10) must stay FlipPhoneCover — aspect alone would mis-detect as Pad.
    /// Tall covers (Fold7) stay Phone via MapFoldableProduct + no Fold8 size match.
    /// </summary>
    public class ResponsiveDeviceSimulatorLayoutTests
    {
        [SetUp]
        public void SetUp()
        {
            ResponsiveDevice.ShouldIgnoreStaleFlipCoverSize = () =>
                FoldableEditorSession.IsFoldableDeviceBound &&
                FoldableEditorSession.DeviceKind == FoldableDeviceKind.FlipablePhone &&
                FoldableEditorSession.ProductState != FoldableProductState.Closed &&
                FoldableEditorSession.ProductState != FoldableProductState.Undefined;
        }

        [TearDown]
        public void TearDown()
        {
            ResponsiveDevice.ClearSimulatorLayout();
            ResponsiveDevice.ClearOverride();
        }

        static void IgnoreWhenLiveFlipSessionBlocksCoverWxH()
        {
            if (!FoldableEditorSession.IsFoldableDeviceBound)
                return;
            if (FoldableEditorSession.DeviceKind != FoldableDeviceKind.FlipablePhone)
                return;
            if (FoldableEditorSession.ProductState == FoldableProductState.Closed)
                return;
            Assert.Ignore(
                "Live Flip Opened/HalfOpened session keeps Cover WxH from mapping to Flip Cover.");
        }

        [Test]
        public void MapFoldableProduct_FoldCoverClosed_IsPhone()
        {
            Assert.AreEqual(
                ResponsiveDevice.FormFactor.Phone,
                ResponsiveDevice.MapFoldableProduct(
                    FoldableDeviceKind.FoldablePhone,
                    FoldableProductState.Closed));
        }

        [Test]
        public void MapFoldableProduct_KindProductMatrix_MatchesResponsiveSlots()
        {
            // Canonical kind × product → form (before WxH refine).
            Assert.AreEqual(
                ResponsiveDevice.FormFactor.Phone,
                ResponsiveDevice.MapFoldableProduct(
                    FoldableDeviceKind.FoldablePhone, FoldableProductState.Closed));
            Assert.AreEqual(
                ResponsiveDevice.FormFactor.Pad,
                ResponsiveDevice.MapFoldableProduct(
                    FoldableDeviceKind.FoldablePhone, FoldableProductState.Opened));
            Assert.AreEqual(
                ResponsiveDevice.FormFactor.FoldHalfOpened,
                ResponsiveDevice.MapFoldableProduct(
                    FoldableDeviceKind.FoldablePhone, FoldableProductState.HalfOpened));

            Assert.AreEqual(
                ResponsiveDevice.FormFactor.FlipPhoneCover,
                ResponsiveDevice.MapFoldableProduct(
                    FoldableDeviceKind.FlipablePhone, FoldableProductState.Closed));
            Assert.AreEqual(
                ResponsiveDevice.FormFactor.Phone,
                ResponsiveDevice.MapFoldableProduct(
                    FoldableDeviceKind.FlipablePhone, FoldableProductState.Opened));
            Assert.AreEqual(
                ResponsiveDevice.FormFactor.FlipHalfOpened,
                ResponsiveDevice.MapFoldableProduct(
                    FoldableDeviceKind.FlipablePhone, FoldableProductState.HalfOpened));

            Assert.AreEqual(
                ResponsiveDevice.FormFactor.Phone,
                ResponsiveDevice.MapFoldableProduct(
                    FoldableDeviceKind.DualScreen, FoldableProductState.Closed));
            Assert.AreEqual(
                ResponsiveDevice.FormFactor.Pad,
                ResponsiveDevice.MapFoldableProduct(
                    FoldableDeviceKind.DualScreen, FoldableProductState.Opened));
            Assert.AreEqual(
                ResponsiveDevice.FormFactor.FoldHalfOpened,
                ResponsiveDevice.MapFoldableProduct(
                    FoldableDeviceKind.DualScreen, FoldableProductState.HalfOpened));

            Assert.AreEqual(
                ResponsiveDevice.FormFactor.Phone,
                ResponsiveDevice.MapFoldableProduct(
                    FoldableDeviceKind.Unknown, FoldableProductState.Opened));
        }

        [Test]
        public void RefineCoverForms_AfterMap_Fold8AndFlipCoversUpgrade()
        {
            Vector2 fold8Cover = ResponsiveCatalogDevices.FoldCoverPortrait;
            Vector2 fold7Cover = new Vector2(1080f, 2520f);
            Vector2 flipCover = ResponsiveCatalogDevices.FlipCoverPortrait;
            Vector2 fold8Inner = ResponsiveCatalogDevices.FoldInnerPortrait;

            // Fold Normal + Fold8 cover WxH → Fold Cover slot.
            Assert.AreEqual(
                ResponsiveDevice.FormFactor.FoldPhoneCover,
                ResponsiveDevice.RefineCoverForms(
                    ResponsiveDevice.MapFoldableProduct(
                        FoldableDeviceKind.FoldablePhone, FoldableProductState.Closed),
                    fold8Cover.x,
                    fold8Cover.y));

            // Fold Normal + tall Fold7/Ultra cover stays Phone (not Fold8 passport).
            Assert.AreEqual(
                ResponsiveDevice.FormFactor.Phone,
                ResponsiveDevice.RefineCoverForms(
                    ResponsiveDevice.MapFoldableProduct(
                        FoldableDeviceKind.FoldablePhone, FoldableProductState.Closed),
                    fold7Cover.x,
                    fold7Cover.y));

            // Lagged Expand/Pad on Fold8 cover WxH still upgrades to Fold Cover.
            Assert.AreEqual(
                ResponsiveDevice.FormFactor.FoldPhoneCover,
                ResponsiveDevice.RefineCoverForms(
                    ResponsiveDevice.FormFactor.Pad, fold8Cover.x, fold8Cover.y));

            // Flip Closed → Flip Cover (direct map + refine).
            Assert.AreEqual(
                ResponsiveDevice.FormFactor.FlipPhoneCover,
                ResponsiveDevice.RefineCoverForms(
                    ResponsiveDevice.MapFoldableProduct(
                        FoldableDeviceKind.FlipablePhone, FoldableProductState.Closed),
                    flipCover.x,
                    flipCover.y));

            // Fold HalfOpened + Fold8 inner → Fold Cover HalfOpened.
            Assert.AreEqual(
                ResponsiveDevice.FormFactor.FoldCoverHalfOpened,
                ResponsiveDevice.RefineCoverForms(
                    ResponsiveDevice.MapFoldableProduct(
                        FoldableDeviceKind.FoldablePhone, FoldableProductState.HalfOpened),
                    fold8Inner.x,
                    fold8Inner.y));

            // Flip Opened stays Phone (main), not Pad.
            Assert.AreEqual(
                ResponsiveDevice.FormFactor.Phone,
                ResponsiveDevice.RefineCoverForms(
                    ResponsiveDevice.MapFoldableProduct(
                        FoldableDeviceKind.FlipablePhone, FoldableProductState.Opened),
                    ResponsiveCatalogDevices.FlipMainPortrait.x,
                    ResponsiveCatalogDevices.FlipMainPortrait.y));
        }

        [Test]
        public void RefineFoldPhoneCover_Fold8Size_UpgradesPhoneToFoldPhoneCover()
        {
            Vector2 cover = ResponsiveCatalogDevices.FoldCoverLandscape;
            Vector2 flipMain = ResponsiveCatalogDevices.FlipMainLandscape;
            Assert.AreEqual(
                ResponsiveDevice.FormFactor.FoldPhoneCover,
                ResponsiveDevice.RefineWideCover(
                    ResponsiveDevice.FormFactor.Phone, cover.x, cover.y));
            Assert.AreEqual(
                ResponsiveDevice.FormFactor.Phone,
                ResponsiveDevice.RefineWideCover(
                    ResponsiveDevice.FormFactor.Phone, flipMain.x, flipMain.y));
            Assert.AreEqual(
                ResponsiveDevice.FormFactor.FoldPhoneCover,
                ResponsiveDevice.RefineWideCover(
                    ResponsiveDevice.FormFactor.Pad, cover.x, cover.y));
            Assert.AreEqual(
                ResponsiveDevice.FormFactor.FoldHalfOpened,
                ResponsiveDevice.RefineWideCover(
                    ResponsiveDevice.FormFactor.FoldHalfOpened, cover.x, cover.y));
        }

        [Test]
        public void MapFoldableProduct_FoldExpandOpened_IsPad()
        {
            Assert.AreEqual(
                ResponsiveDevice.FormFactor.Pad,
                ResponsiveDevice.MapFoldableProduct(
                    FoldableDeviceKind.FoldablePhone,
                    FoldableProductState.Opened));
        }

        [Test]
        public void MapFoldableProduct_FoldHalfOpened_UsesDedicatedSlot()
        {
            Assert.AreEqual(
                ResponsiveDevice.FormFactor.FoldHalfOpened,
                ResponsiveDevice.MapFoldableProduct(
                    FoldableDeviceKind.FoldablePhone,
                    FoldableProductState.HalfOpened));
        }

        [Test]
        public void MapFoldableProduct_FlipOpened_UsesPhoneSlot()
        {
            Assert.AreEqual(
                ResponsiveDevice.FormFactor.Phone,
                ResponsiveDevice.MapFoldableProduct(
                    FoldableDeviceKind.FlipablePhone,
                    FoldableProductState.Opened));
        }

        [Test]
        public void MapFoldableProduct_FlipClosed_UsesFlipPhoneCoverSlot()
        {
            Assert.AreEqual(
                ResponsiveDevice.FormFactor.FlipPhoneCover,
                ResponsiveDevice.MapFoldableProduct(
                    FoldableDeviceKind.FlipablePhone,
                    FoldableProductState.Closed));
        }

        [Test]
        public void MapFoldableProduct_FlipHalfOpened_UsesDedicatedSlot()
        {
            Assert.AreEqual(
                ResponsiveDevice.FormFactor.FlipHalfOpened,
                ResponsiveDevice.MapFoldableProduct(
                    FoldableDeviceKind.FlipablePhone,
                    FoldableProductState.HalfOpened));
        }

        [Test]
        public void DetectActiveFormFactor_CoverAspectWithoutSimulator_IsFoldPhoneCoverForFold8()
        {
            // Fold8 Cover landscape is under padMax 1.6 (~1.580) but must be FoldPhoneCover.
            Vector2 cover = ResponsiveCatalogDevices.FoldCoverLandscape;
            Assert.AreEqual(
                ResponsiveDevice.FormFactor.FoldPhoneCover,
                ResponsiveDevice.DetectActiveFormFactor(cover, padMaxAspect: 1.6f));
        }

        [Test]
        public void DetectActiveFormFactor_FlipCoverWithoutSimulator_IsFlipPhoneCover()
        {
            IgnoreWhenLiveFlipSessionBlocksCoverWxH();
            // Flip Cover is near-square (~1.10) and would be Pad by aspect without a dedicated form.
            Vector2 cover = ResponsiveCatalogDevices.FlipCoverLandscape;
            Assert.AreEqual(
                ResponsiveDevice.FormFactor.FlipPhoneCover,
                ResponsiveDevice.DetectActiveFormFactor(cover, padMaxAspect: 1.6f));
        }

        [Test]
        public void RefineFlipPhoneCover_FlipCoverSize_UpgradesPhonePadAndStaleFoldCoverPin()
        {
            IgnoreWhenLiveFlipSessionBlocksCoverWxH();
            Vector2 cover = ResponsiveCatalogDevices.FlipCoverLandscape;
            Vector2 foldCover = ResponsiveCatalogDevices.FoldCoverLandscape;
            Vector2 flipMain = ResponsiveCatalogDevices.FlipMainLandscape;
            Assert.AreEqual(
                ResponsiveDevice.FormFactor.FlipPhoneCover,
                ResponsiveDevice.RefineFlipPhoneCover(
                    ResponsiveDevice.FormFactor.Phone, cover.x, cover.y));
            Assert.AreEqual(
                ResponsiveDevice.FormFactor.FlipPhoneCover,
                ResponsiveDevice.RefineFlipPhoneCover(
                    ResponsiveDevice.FormFactor.Pad, cover.x, cover.y));
            Assert.AreEqual(
                ResponsiveDevice.FormFactor.FlipPhoneCover,
                ResponsiveDevice.RefineFlipPhoneCover(
                    ResponsiveDevice.FormFactor.FoldPhoneCover, cover.x, cover.y));
            Assert.AreEqual(
                ResponsiveDevice.FormFactor.FoldPhoneCover,
                ResponsiveDevice.RefineFlipPhoneCover(
                    ResponsiveDevice.FormFactor.FoldPhoneCover, foldCover.x, foldCover.y));
            Assert.AreEqual(
                ResponsiveDevice.FormFactor.Phone,
                ResponsiveDevice.RefineFlipPhoneCover(
                    ResponsiveDevice.FormFactor.Phone, flipMain.x, flipMain.y));
            Assert.AreEqual(
                ResponsiveDevice.FormFactor.FlipHalfOpened,
                ResponsiveDevice.RefineFlipPhoneCover(
                    ResponsiveDevice.FormFactor.FlipHalfOpened, cover.x, cover.y));
            Assert.IsFalse(
                ResponsiveDevice.IsKnownFlipCoverPhoneSize(foldCover.x, foldCover.y));
            Assert.IsFalse(
                ResponsiveDevice.IsKnownFoldCoverPhoneSize(cover.x, cover.y));
        }

        [Test]
        public void GetWatchScreenSize_PrefersSimulatorPin()
        {
            Vector2 phone = ResponsiveCatalogDevices.PhoneLandscape;
            ResponsiveDevice.SetSimulatorLayout(
                ResponsiveDevice.FormFactor.Phone,
                ResponsiveDevice.Orientation.Landscape,
                (int)phone.x,
                (int)phone.y);

            Vector2 size = ResponsiveDevice.GetWatchScreenSize();
            Assert.AreEqual(phone.x, size.x, 1e-3f);
            Assert.AreEqual(phone.y, size.y, 1e-3f);
            Assert.IsTrue(ResponsiveDevice.GetSimulatorScreenSize(out Vector2 pinned));
            Assert.AreEqual(size, pinned);
        }

        [Test]
        public void DetectActiveFormFactor_CoverWithSimulatorLayout_IsFoldPhoneCover()
        {
            ResponsiveDevice.SetSimulatorLayout(
                ResponsiveDevice.FormFactor.Phone,
                ResponsiveDevice.Orientation.Landscape);

            Vector2 cover = ResponsiveCatalogDevices.FoldCoverLandscape;
            Assert.AreEqual(
                ResponsiveDevice.FormFactor.FoldPhoneCover,
                ResponsiveDevice.DetectActiveFormFactor(cover, padMaxAspect: 1.6f));
            Assert.AreEqual(
                ResponsiveDevice.Orientation.Landscape,
                ResponsiveDevice.CurrentOrientation);
        }

        [Test]
        public void CurrentFormFactor_CoverPinSize_IsFoldPhoneCover()
        {
            Vector2 cover = ResponsiveCatalogDevices.FoldCoverLandscape;
            ResponsiveDevice.SetSimulatorLayout(
                ResponsiveDevice.FormFactor.Phone,
                ResponsiveDevice.Orientation.Landscape,
                (int)cover.x,
                (int)cover.y);

            Assert.AreEqual(
                ResponsiveDevice.FormFactor.FoldPhoneCover,
                ResponsiveDevice.CurrentFormFactor);
            Assert.AreEqual(cover, ResponsiveDevice.GetWatchScreenSize());
        }

        [Test]
        public void CurrentFormFactor_FlipCoverPinSize_IsFlipPhoneCover()
        {
            IgnoreWhenLiveFlipSessionBlocksCoverWxH();
            Vector2 cover = ResponsiveCatalogDevices.FlipCoverLandscape;
            ResponsiveDevice.SetSimulatorLayout(
                ResponsiveDevice.FormFactor.Phone,
                ResponsiveDevice.Orientation.Landscape,
                (int)cover.x,
                (int)cover.y);

            Assert.AreEqual(
                ResponsiveDevice.FormFactor.FlipPhoneCover,
                ResponsiveDevice.CurrentFormFactor);
            Assert.AreEqual(cover, ResponsiveDevice.GetWatchScreenSize());
        }

        [Test]
        public void CurrentFormFactor_StaleFoldCoverPin_FlipCoverWatch_IsFlipPhoneCover()
        {
            IgnoreWhenLiveFlipSessionBlocksCoverWxH();
            Vector2 cover = ResponsiveCatalogDevices.FlipCoverLandscape;
            ResponsiveDevice.SetSimulatorLayout(
                ResponsiveDevice.FormFactor.FoldPhoneCover,
                ResponsiveDevice.Orientation.Landscape,
                (int)cover.x,
                (int)cover.y);

            Assert.AreEqual(
                ResponsiveDevice.FormFactor.FlipPhoneCover,
                ResponsiveDevice.CurrentFormFactor);
        }

        [Test]
        public void CurrentFormFactor_CompactPhonePin_StaysPhone()
        {
            Vector2 compact = ResponsiveCatalogDevices.CompactPhoneLandscape;
            ResponsiveDevice.SetSimulatorLayout(
                ResponsiveDevice.FormFactor.Phone,
                ResponsiveDevice.Orientation.Landscape,
                (int)compact.x,
                (int)compact.y);

            Assert.AreEqual(
                ResponsiveDevice.FormFactor.Phone,
                ResponsiveDevice.CurrentFormFactor);
            Assert.AreEqual(compact, ResponsiveDevice.GetWatchScreenSize());
        }

        [Test]
        public void CurrentFormFactor_PhoneReferencePin_IsPhone()
        {
            Vector2 phone = ResponsiveCatalogDevices.PhoneLandscape;
            ResponsiveDevice.SetSimulatorLayout(
                ResponsiveDevice.FormFactor.Phone,
                ResponsiveDevice.Orientation.Landscape,
                (int)phone.x,
                (int)phone.y);

            Assert.AreEqual(
                ResponsiveDevice.FormFactor.Phone,
                ResponsiveDevice.CurrentFormFactor);
            Assert.AreEqual(phone, ResponsiveDevice.GetWatchScreenSize());
        }

        [Test]
        public void DetectActiveFormFactor_PhonePin_CompactWatch_IsPhone()
        {
            Vector2 compact = ResponsiveCatalogDevices.CompactPhoneLandscape;
            ResponsiveDevice.SetSimulatorLayout(
                ResponsiveDevice.FormFactor.Phone,
                ResponsiveDevice.Orientation.Landscape,
                (int)compact.x,
                (int)compact.y);

            Assert.AreEqual(
                ResponsiveDevice.FormFactor.Phone,
                ResponsiveDevice.DetectActiveFormFactor(compact, padMaxAspect: 1.6f));
        }

        [TestCase(2853f, 2007f)]
        [TestCase(2007f, 2853f)]
        public void DetectActiveFormFactor_IphoneDuoInnerSize_WithoutPinOrPosture_IsNotSeated(
            float width,
            float height)
        {
            // An open Device Simulator pins its previewed layout; size-only needs no pin.
            ResponsiveDevice.ClearSimulatorLayout();
            ResponsiveDevice.ClearOverride();
            Assume.That(
                FoldablePostureSource.FindActiveLayoutState(),
                Is.Null,
                "needs no live posture in the test scene");

            // Seated comes only from a hinge posture (or a Simulator pin), never from size alone:
            // the bare Duo inner is the flat panel (Pad by aspect in the Editor, IsKnownInnerPadSize
            // on players).
            Assert.IsTrue(ResponsiveDevice.IsKnownInnerPadSize(width, height));
            Assert.AreEqual(
                ResponsiveDevice.FormFactor.Pad,
                ResponsiveDevice.DetectActiveFormFactor(new Vector2(width, height), padMaxAspect: 1.6f));
        }

        [Test]
        public void DetectActiveFormFactor_Fold8CoverLandscape_WithoutPin_IsFoldPhoneCover()
        {
            // Only Galaxy Z Fold8 Cover among toolkit folds is under default padMax 1.6
            // (1972×1248 ≈ 1.580). Size recognition maps it to FoldPhoneCover without a Simulator pin.
            Vector2 cover = ResponsiveCatalogDevices.FoldCoverLandscape;
            Vector2 coverPortrait = ResponsiveCatalogDevices.FoldCoverPortrait;
            Vector2 safe = ResponsiveCatalogDevices.FoldCoverSafeAreaLandscape;
            Vector2 safePortrait = ResponsiveCatalogDevices.FoldCoverSafeAreaPortrait;
            Assert.AreEqual(
                ResponsiveDevice.FormFactor.FoldPhoneCover,
                ResponsiveDevice.DetectActiveFormFactor(cover, padMaxAspect: 1.6f));
            Assert.IsTrue(ResponsiveDevice.IsKnownFoldCoverPhoneSize(cover.x, cover.y));
            Assert.IsTrue(ResponsiveDevice.IsKnownFoldCoverPhoneSize(coverPortrait.x, coverPortrait.y));
            Assert.IsTrue(ResponsiveDevice.IsKnownFoldCoverPhoneSize(safe.x, safe.y));
            Assert.IsTrue(ResponsiveDevice.IsKnownFoldCoverPhoneSize(safePortrait.x, safePortrait.y));
        }

        [Test]
        public void DetectActiveFormFactor_Fold8CoverSafeAreaLandscape_IsFoldPhoneCover()
        {
            // Device Simulator safe-area Cover landscape (nav inset) — not exact 1972.
            Assert.AreEqual(
                ResponsiveDevice.FormFactor.FoldPhoneCover,
                ResponsiveDevice.DetectActiveFormFactor(
                    ResponsiveCatalogDevices.FoldCoverSafeAreaLandscape, padMaxAspect: 1.6f));
        }

        [Test]
        public void DetectActiveFormFactor_Fold8CoverSafeAreaBothAxesInset_IsFoldPhoneCover()
        {
            // Safe Area can inset width and height — still ~1.58 and under padMax.
            Vector2 bothAxes = ResponsiveCatalogDevices.FoldCoverSafeAreaLandscape;
            bothAxes.y -= 80f;
            Assert.IsTrue(ResponsiveDevice.IsKnownFoldCoverPhoneSize(bothAxes.x, bothAxes.y));
            Assert.AreEqual(
                ResponsiveDevice.FormFactor.FoldPhoneCover,
                ResponsiveDevice.DetectActiveFormFactor(bothAxes, padMaxAspect: 1.6f));
        }

        [Test]
        public void DetectActiveFormFactor_Fold7InnerSafeArea_IsPad()
        {
            Vector2 safe = ResponsiveCatalogDevices.FoldPhoneInnerSafeAreaLandscape;
            Assert.IsTrue(ResponsiveDevice.IsKnownFoldPhoneInnerSize(safe.x, safe.y));
            Assert.AreEqual(
                ResponsiveDevice.FormFactor.Pad,
                ResponsiveDevice.DetectActiveFormFactor(safe, padMaxAspect: 1.6f));
            Assert.AreEqual(
                ResponsiveDevice.FormFactor.Pad,
                ResponsiveDevice.DetectActiveFormFactor(
                    safe, 1.6f, foldableRuntimeSupported: true));
        }

        [Test]
        public void IsKnownFoldPhoneInnerSize_DoesNotMatchFlipCover()
        {
            Vector2 flip = ResponsiveCatalogDevices.FlipCoverLandscape;
            Assert.IsFalse(
                ResponsiveDevice.IsKnownFoldPhoneInnerSize(flip.x, flip.y));
        }

        [Test]
        public void IphoneDuoCover_IsIphoneDuoCoverSlot_NotFold8Identity()
        {
            // 2034×1398 (~1.455) is under padMax 1.6 — aspect alone would be Pad.
            Assert.IsTrue(ResponsiveDevice.IsKnownIphoneDuoCoverSize(2034f, 1398f));
            Assert.IsTrue(ResponsiveDevice.IsKnownIphoneDuoCoverSize(1398f, 2034f));
            // Simulator safe areas of the Duo cover (iPhone Duo-like.device): portrait 1398×1755,
            // landscape 1680×1335. They are Duo cover, never Fold8 cover.
            foreach (Vector2 safe in new[]
                     {
                         new Vector2(1398f, 1755f), new Vector2(1755f, 1398f),
                         new Vector2(1680f, 1335f), new Vector2(1335f, 1680f),
                     })
            {
                Assert.IsTrue(ResponsiveDevice.IsKnownIphoneDuoCoverSize(safe.x, safe.y), safe.ToString());
                Assert.IsFalse(ResponsiveDevice.IsKnownFoldCoverPhoneSize(safe.x, safe.y), safe.ToString());
            }
            Assert.IsTrue(ResponsiveDevice.IsKnownWideCoverPhoneSize(2034f, 1398f));
            Assert.IsFalse(ResponsiveDevice.IsKnownFoldCoverPhoneSize(2034f, 1398f));
            Assert.AreEqual(
                ResponsiveDevice.FormFactor.iPhoneDuoCover,
                ResponsiveDevice.DetectActiveFormFactor(new Vector2(2034f, 1398f), 1.6f, true));
            Assert.AreEqual(
                ResponsiveDevice.FormFactor.iPhoneDuoCover,
                ResponsiveDevice.RefineFoldableForm(
                    FoldableDeviceKind.FoldablePhone,
                    ResponsiveDevice.FormFactor.Phone,
                    2034f, 1398f));
        }

        [Test]
        public void IphoneDuoInner_HalfOpenedIsIphoneDuoSeated_OpenedStaysPad()
        {
            Assert.IsTrue(ResponsiveDevice.IsKnownIphoneDuoInnerSize(2853f, 2007f));
            Assert.IsTrue(ResponsiveDevice.IsKnownIphoneDuoInnerSize(2007f, 2688f));
            Assert.IsFalse(ResponsiveDevice.IsKnownFoldInnerPadSize(2853f, 2007f));
            Assert.AreEqual(
                ResponsiveDevice.FormFactor.iPhoneDuoSeated,
                ResponsiveDevice.RefineFoldableForm(
                    FoldableDeviceKind.FoldablePhone,
                    ResponsiveDevice.FormFactor.FoldHalfOpened,
                    2853f, 2007f));
            Assert.AreEqual(
                ResponsiveDevice.FormFactor.iPhoneDuoSeated,
                ResponsiveDevice.RefineCoverForms(
                    ResponsiveDevice.FormFactor.FoldHalfOpened, 2007f, 2853f));
            Assert.AreEqual(
                ResponsiveDevice.FormFactor.FoldCoverHalfOpened,
                ResponsiveDevice.RefineCoverForms(
                    ResponsiveDevice.FormFactor.FoldHalfOpened, 2448f, 1848f),
                "Fold8 inner keeps Fold Cover Half Opened");
            Assert.AreEqual(
                ResponsiveDevice.FormFactor.iPhoneDuoSeated,
                ResponsiveDevice.RefineCoverForms(
                    ResponsiveDevice.FormFactor.iPhoneDuoSeated, 1398f, 2034f),
                "Duo HalfOpened is not upgraded to Cover by stale Cover WxH");
            Assert.AreEqual(
                ResponsiveDevice.FormFactor.Pad,
                ResponsiveDevice.RefineFoldableForm(
                    FoldableDeviceKind.FoldablePhone,
                    ResponsiveDevice.FormFactor.Pad,
                    2853f, 2007f));
            Assert.AreEqual(
                ResponsiveDevice.Orientation.Portrait,
                ResponsiveDevice.DetectOrientationForForm(
                    ResponsiveDevice.FormFactor.iPhoneDuoSeated, 2007f, 2853f),
                "2007×2853 (tall laptop) is Portrait");
            Assert.AreEqual(
                ResponsiveDevice.Orientation.Landscape,
                ResponsiveDevice.DetectOrientationForForm(
                    ResponsiveDevice.FormFactor.iPhoneDuoSeated, 2853f, 2007f),
                "2853×2007 (wide book) is Landscape");
            Assert.AreEqual(
                ResponsiveDevice.Orientation.Landscape,
                ResponsiveDevice.OrientationForForm(
                    ResponsiveDevice.FormFactor.FoldHalfOpened,
                    ResponsiveDevice.Orientation.Portrait),
                "Fold half-open names the posture: the tall screen is Landscape");
            Assert.AreEqual(
                ResponsiveDevice.Orientation.Landscape,
                ResponsiveDevice.OrientationForForm(
                    ResponsiveDevice.FormFactor.FoldCoverHalfOpened,
                    ResponsiveDevice.Orientation.Portrait),
                "Fold Cover half-open names the posture: the tall screen is Landscape");
            Assert.AreEqual(
                ResponsiveDevice.Orientation.Portrait,
                ResponsiveDevice.OrientationForForm(
                    ResponsiveDevice.FormFactor.FlipHalfOpened,
                    ResponsiveDevice.Orientation.Portrait));
            Assert.AreEqual(
                ResponsiveDevice.Orientation.Landscape,
                ResponsiveDevice.OrientationForForm(
                    ResponsiveDevice.FormFactor.Pad,
                    ResponsiveDevice.Orientation.Landscape));
            Assert.IsTrue(ResponsiveUiEditorSettings.SlotAllowed(
                ResponsiveWidgetLayout.StatePropertyIndex(
                    new ResponsiveDevice.LayoutKey(
                        ResponsiveDevice.FormFactor.FoldHalfOpened,
                        ResponsiveDevice.Orientation.Landscape))));
            Assert.IsTrue(ResponsiveUiEditorSettings.SlotAllowed(
                ResponsiveWidgetLayout.StatePropertyIndex(
                    new ResponsiveDevice.LayoutKey(
                        ResponsiveDevice.FormFactor.FoldCoverHalfOpened,
                        ResponsiveDevice.Orientation.Landscape))));
        }

        [TestCase(false, true, TestName = "SlotAllowedByPlayerAxes_LandscapeOnly")]
        [TestCase(true, false, TestName = "SlotAllowedByPlayerAxes_PortraitOnly")]
        [TestCase(true, true, TestName = "SlotAllowedByPlayerAxes_BothAxes")]
        [TestCase(false, false, TestName = "SlotAllowedByPlayerAxes_NoAxis")]
        public void SlotAllowedByPlayerAxes_MatchesTableForEverySlot(bool portrait, bool landscape)
        {
            string[] names = ResponsiveWidgetLayout.StatePropertyNames;
            for (int i = 0; i < names.Length; i++)
            {
                Assert.AreEqual(
                    ResponsiveEditorTestUtil.ExpectedSlotAllowed(i, portrait, landscape),
                    ResponsiveDevice.SlotAllowedByPlayerAxes(
                        ResponsiveWidgetLayout.SlotFormFactor(i),
                        ResponsiveWidgetLayout.SlotOrientation(i),
                        portrait,
                        landscape),
                    names[i]);
            }
        }

        [Test]
        public void SlotAllowedByPlayerAxes_FoldHalfOpenedFollowsPostureAxis()
        {
            foreach (ResponsiveDevice.FormFactor form in new[]
                     {
                         ResponsiveDevice.FormFactor.FoldHalfOpened,
                         ResponsiveDevice.FormFactor.FoldCoverHalfOpened,
                     })
            {
                // Landscape-only: the tall half-open screen is the Landscape slot, so only it shows.
                Assert.IsTrue(ResponsiveDevice.SlotAllowedByPlayerAxes(
                    form, ResponsiveDevice.Orientation.Landscape, false, true), form + " Landscape");
                Assert.IsFalse(ResponsiveDevice.SlotAllowedByPlayerAxes(
                    form, ResponsiveDevice.Orientation.Portrait, false, true), form + " Portrait");
                Assert.IsTrue(ResponsiveDevice.SlotAllowedByPlayerAxes(
                    form, ResponsiveDevice.Orientation.Portrait, true, false), form + " portrait-only");
                Assert.IsFalse(ResponsiveDevice.SlotAllowedByPlayerAxes(
                    form, ResponsiveDevice.Orientation.Landscape, true, false), form + " portrait-only");
            }
        }

        [Test]
        public void DetectOrientationForForm_FoldHalfOpenedNamesPosture_OthersScreenShape()
        {
            // Fold8 half open: 1848×2448 (flat turned 90°) is Landscape, 2448×1848 (flat) Portrait.
            Assert.AreEqual(
                ResponsiveDevice.Orientation.Landscape,
                ResponsiveDevice.DetectOrientationForForm(
                    ResponsiveDevice.FormFactor.FoldCoverHalfOpened, 1848f, 2448f));
            Assert.AreEqual(
                ResponsiveDevice.Orientation.Portrait,
                ResponsiveDevice.DetectOrientationForForm(
                    ResponsiveDevice.FormFactor.FoldCoverHalfOpened, 2448f, 1848f));
            // Fold7 half open: same rule with its own sizes.
            Assert.AreEqual(
                ResponsiveDevice.Orientation.Landscape,
                ResponsiveDevice.DetectOrientationForForm(
                    ResponsiveDevice.FormFactor.FoldHalfOpened, 1968f, 2184f));
            Assert.AreEqual(
                ResponsiveDevice.Orientation.Portrait,
                ResponsiveDevice.DetectOrientationForForm(
                    ResponsiveDevice.FormFactor.FoldHalfOpened, 2184f, 1968f));
            // iPhone Duo Seated, Flip Half Opened, Pad and Phone name the screen shape.
            Assert.AreEqual(
                ResponsiveDevice.Orientation.Portrait,
                ResponsiveDevice.DetectOrientationForForm(
                    ResponsiveDevice.FormFactor.iPhoneDuoSeated, 2007f, 2853f));
            Assert.AreEqual(
                ResponsiveDevice.Orientation.Landscape,
                ResponsiveDevice.DetectOrientationForForm(
                    ResponsiveDevice.FormFactor.iPhoneDuoSeated, 2853f, 2007f));
            Assert.AreEqual(
                ResponsiveDevice.Orientation.Portrait,
                ResponsiveDevice.DetectOrientationForForm(
                    ResponsiveDevice.FormFactor.FlipHalfOpened, 1080f, 2640f));
            Assert.AreEqual(
                ResponsiveDevice.Orientation.Landscape,
                ResponsiveDevice.DetectOrientationForForm(
                    ResponsiveDevice.FormFactor.FlipHalfOpened, 2640f, 1080f));
            Assert.AreEqual(
                ResponsiveDevice.Orientation.Portrait,
                ResponsiveDevice.DetectOrientationForForm(
                    ResponsiveDevice.FormFactor.Pad, 2064f, 2752f));
            Assert.AreEqual(
                ResponsiveDevice.Orientation.Landscape,
                ResponsiveDevice.DetectOrientationForForm(
                    ResponsiveDevice.FormFactor.Phone, 2340f, 1080f));
        }

        [Test]
        public void ResolveLiveLayout_FoldCoverHalfOpenedSimulatorPin_NamesPostureOnce()
        {
            // The pin carries the screen shape; the slot is named once (no double flip).
            ResponsiveDevice.ClearOverride();
            try
            {
                ResponsiveDevice.SetSimulatorLayout(
                    ResponsiveDevice.FormFactor.FoldCoverHalfOpened,
                    ResponsiveDevice.Orientation.Portrait, 1848, 2448);
                Assert.AreEqual(
                    new ResponsiveDevice.LayoutKey(
                        ResponsiveDevice.FormFactor.FoldCoverHalfOpened,
                        ResponsiveDevice.Orientation.Landscape),
                    ResponsiveDevice.ResolveLiveLayout(new Vector2(1848f, 2448f)));
                ResponsiveDevice.SetSimulatorLayout(
                    ResponsiveDevice.FormFactor.FoldCoverHalfOpened,
                    ResponsiveDevice.Orientation.Landscape, 2448, 1848);
                Assert.AreEqual(
                    new ResponsiveDevice.LayoutKey(
                        ResponsiveDevice.FormFactor.FoldCoverHalfOpened,
                        ResponsiveDevice.Orientation.Portrait),
                    ResponsiveDevice.ResolveLiveLayout(new Vector2(2448f, 1848f)));
            }
            finally
            {
                ResponsiveDevice.ClearSimulatorLayout();
            }
        }

        [Test]
        public void FoldHalfOpenedHinge_LandscapeIsHorizontalOnTallScreen_PortraitVerticalOnWide()
        {
            Assert.AreEqual(
                FoldableOrientation.Horizontal,
                FoldableProductStateResolver.ToScreenSpaceHalfOpenHinge(
                    FoldableDeviceKind.FoldablePhone,
                    FoldableProductState.HalfOpened,
                    FoldableProductStateResolver.HalfOpenHingeForPose(landscape: true),
                    1848, 2448));
            Assert.AreEqual(
                FoldableOrientation.Vertical,
                FoldableProductStateResolver.ToScreenSpaceHalfOpenHinge(
                    FoldableDeviceKind.FoldablePhone,
                    FoldableProductState.HalfOpened,
                    FoldableProductStateResolver.HalfOpenHingeForPose(landscape: false),
                    2448, 1848));
            // The screen decides, not the authored hinge: a wide screen never folds horizontally.
            Assert.AreEqual(
                FoldableOrientation.Vertical,
                FoldableProductStateResolver.ToScreenSpaceHalfOpenHinge(
                    FoldableDeviceKind.FoldablePhone,
                    FoldableProductState.HalfOpened,
                    FoldableOrientation.Horizontal,
                    2448, 1848));
            Assert.AreEqual(
                FoldableOrientation.Horizontal,
                FoldableProductStateResolver.ToScreenSpaceHalfOpenHinge(
                    FoldableDeviceKind.FoldablePhone,
                    FoldableProductState.HalfOpened,
                    FoldableOrientation.Vertical,
                    1848, 2448));
        }

        [Test]
        public void ResolveLiveLayout_IphoneDuoSeated_FormPinned_OrientationFollowsScreenShape()
        {
            // The form is pinned (Seated comes from posture, not size); this checks only the
            // screen-shape → slot-label mapping. Size alone: see DetectActiveFormFactor_IphoneDuoInnerSize_*.
            ResponsiveDevice.ClearSimulatorLayout();
            try
            {
                ResponsiveDevice.SetFormOverride(ResponsiveDevice.FormFactor.iPhoneDuoSeated);

                Assert.AreEqual(
                    new ResponsiveDevice.LayoutKey(
                        ResponsiveDevice.FormFactor.iPhoneDuoSeated,
                        ResponsiveDevice.Orientation.Portrait),
                    ResponsiveDevice.ResolveLiveLayout(new Vector2(2007f, 2853f)),
                    "tall laptop screen → Duo Portrait");
                Assert.AreEqual(
                    new ResponsiveDevice.LayoutKey(
                        ResponsiveDevice.FormFactor.iPhoneDuoSeated,
                        ResponsiveDevice.Orientation.Landscape),
                    ResponsiveDevice.ResolveLiveLayout(new Vector2(2853f, 2007f)),
                    "wide book screen → Duo Landscape");
            }
            finally
            {
                ResponsiveDevice.ClearOverride();
            }
        }

        [Test]
        public void CurrentLayout_IphoneDuoSimulatorPin_PassesPinnedOrientationThrough()
        {
            Assume.That(FoldablePostureSource.IsRuntimeSupported);
            try
            {
                ResponsiveDevice.SetSimulatorLayout(
                    ResponsiveDevice.FormFactor.iPhoneDuoSeated,
                    ResponsiveDevice.Orientation.Portrait,
                    2007,
                    2853);
                Assert.AreEqual(
                    ResponsiveDevice.FormFactor.iPhoneDuoSeated,
                    ResponsiveDevice.CurrentFormFactor);
                Assert.AreEqual(
                    ResponsiveDevice.Orientation.Portrait,
                    ResponsiveDevice.CurrentOrientation);
                Assert.AreEqual(
                    new ResponsiveDevice.LayoutKey(
                        ResponsiveDevice.FormFactor.iPhoneDuoSeated,
                        ResponsiveDevice.Orientation.Portrait),
                    ResponsiveDevice.CurrentLayout);

                ResponsiveDevice.SetSimulatorLayout(
                    ResponsiveDevice.FormFactor.iPhoneDuoSeated,
                    ResponsiveDevice.Orientation.Landscape,
                    2853,
                    2007);
                Assert.AreEqual(
                    ResponsiveDevice.Orientation.Landscape,
                    ResponsiveDevice.CurrentOrientation);
                Assert.AreEqual(
                    new ResponsiveDevice.LayoutKey(
                        ResponsiveDevice.FormFactor.iPhoneDuoSeated,
                        ResponsiveDevice.Orientation.Landscape),
                    ResponsiveDevice.ResolveLiveLayout(new Vector2(2853f, 2007f)));
            }
            finally
            {
                ResponsiveDevice.ClearSimulatorLayout();
            }
        }

        [Test]
        public void IphoneDuoSizes_DoNotMatchOtherCatalogPanels()
        {
            Vector2 fold8Cover = ResponsiveCatalogDevices.FoldCoverLandscape;
            Vector2 fold8CoverSafe = ResponsiveCatalogDevices.FoldCoverSafeAreaLandscape;
            Vector2 flip = ResponsiveCatalogDevices.FlipCoverLandscape;
            Assert.IsFalse(ResponsiveDevice.IsKnownIphoneDuoCoverSize(fold8Cover.x, fold8Cover.y));
            Assert.IsFalse(ResponsiveDevice.IsKnownIphoneDuoCoverSize(fold8CoverSafe.x, fold8CoverSafe.y));
            Assert.IsFalse(ResponsiveDevice.IsKnownIphoneDuoCoverSize(flip.x, flip.y));
            Assert.IsFalse(ResponsiveDevice.IsKnownIphoneDuoCoverSize(2266f, 1488f), "iPad mini");
            Assert.IsFalse(ResponsiveDevice.IsKnownIphoneDuoCoverSize(2360f, 1640f), "iPad Air 11");
            Assert.IsFalse(ResponsiveDevice.IsKnownIphoneDuoCoverSize(2622f, 1206f), "iPhone 17 Pro");
            Assert.IsFalse(ResponsiveDevice.IsKnownIphoneDuoInnerSize(2448f, 1848f), "Fold8 inner");
            Assert.IsFalse(ResponsiveDevice.IsKnownIphoneDuoInnerSize(2184f, 1968f), "Fold7 inner");
            Assert.IsFalse(ResponsiveDevice.IsKnownIphoneDuoInnerSize(2388f, 1668f), "iPad Pro 11");
            Assert.IsFalse(ResponsiveDevice.IsKnownIphoneDuoInnerSize(2420f, 1668f), "iPad Pro 11 M4");
            Assert.IsFalse(ResponsiveDevice.IsKnownIphoneDuoInnerSize(2360f, 1640f), "iPad Air 11");
            Assert.IsFalse(ResponsiveDevice.IsKnownIphoneDuoInnerSize(2266f, 1488f), "iPad mini");
            Assert.IsFalse(ResponsiveDevice.IsKnownIphoneDuoInnerSize(2732f, 2048f), "iPad Pro 12.9");
            Assert.IsFalse(ResponsiveDevice.IsKnownIphoneDuoInnerSize(2752f, 2064f), "iPad Pro / Air 13");
            Assert.IsFalse(ResponsiveDevice.IsKnownIphoneDuoInnerSize(2064f, 2752f), "iPad Pro 13 portrait");
            Assert.IsFalse(ResponsiveDevice.IsKnownIphoneDuoInnerSize(2740f, 1920f), "MatePad Pro 13.2 safe area");
            Assert.IsFalse(ResponsiveDevice.IsKnownIphoneDuoInnerSize(2328f, 1848f), "Fold8 inner safe area");
            Assert.IsFalse(ResponsiveDevice.IsKnownIphoneDuoInnerSize(1972f, 1248f), "Fold8 cover");
            Assert.IsFalse(ResponsiveDevice.IsKnownIphoneDuoInnerSize(2670f, 1878f), "old 2670×1878 guess");
            Assert.IsFalse(ResponsiveDevice.IsKnownIphoneDuoCoverSize(2853f, 2007f), "Duo inner is not cover");
        }

        [TestCase(2007f, 2853f, TestName = "IphoneDuoInner_Tall")]
        [TestCase(2853f, 2007f, TestName = "IphoneDuoInner_Wide")]
        [TestCase(2007f, 2688f, TestName = "IphoneDuoInner_TallSafeArea")]
        [TestCase(2727f, 1944f, TestName = "IphoneDuoInner_WideSafeArea")]
        [TestCase(2007f, 2790f, TestName = "IphoneDuoInner_TallTopInsetOnly")]
        public void IphoneDuoInnerSize_MatchesXcodeSimulatorPanel(float width, float height)
        {
            Assert.IsTrue(ResponsiveDevice.IsKnownIphoneDuoInnerSize(width, height));
            Assert.IsFalse(ResponsiveDevice.IsKnownFoldInnerPadSize(width, height), "not Fold inner");
            Assert.IsFalse(ResponsiveDevice.IsKnownFoldCoverPhoneSize(width, height), "not Fold8 Cover");
            Assert.IsFalse(ResponsiveDevice.IsKnownIphoneDuoCoverSize(width, height), "not Duo Cover");
            Assert.AreEqual(
                ResponsiveDevice.FormFactor.iPhoneDuoSeated,
                ResponsiveDevice.RefineCoverForms(
                    ResponsiveDevice.FormFactor.FoldHalfOpened, width, height));
        }

        // Device path: Seated allows only the hold's axis, so either lock can get either screen. The tall
        // laptop (2007×2853, 669×951 pt) is Seated Portrait, the wide book (2853×2007) Seated Landscape.
        [TestCase(669f, 951f, 2007, 2853, false, true, ResponsiveDevice.Orientation.Portrait,
            ResponsiveDevice.Orientation.Portrait, TestName = "LandscapeOnlyGame_LaptopIsSeatedPortraitThenPadPortrait")]
        [TestCase(951f, 669f, 2853, 2007, false, true, ResponsiveDevice.Orientation.Landscape,
            ResponsiveDevice.Orientation.Landscape, TestName = "LandscapeOnlyGame_BookIsSeatedLandscapeThenPadLandscape")]
        [TestCase(669f, 951f, 2007, 2853, true, false, ResponsiveDevice.Orientation.Portrait,
            ResponsiveDevice.Orientation.Portrait, TestName = "PortraitOnlyGame_LaptopIsSeatedPortraitThenPadPortrait")]
        [TestCase(951f, 669f, 2853, 2007, true, false, ResponsiveDevice.Orientation.Landscape,
            ResponsiveDevice.Orientation.Landscape, TestName = "PortraitOnlyGame_BookIsSeatedLandscapeThenPadLandscape")]
        public void IphoneDuoDevice_SeatedSlotFollowsScreenShape(
            float pointWidth,
            float pointHeight,
            int pixelWidth,
            int pixelHeight,
            bool portraitAllowed,
            bool landscapeAllowed,
            ResponsiveDevice.Orientation expectedSlot,
            ResponsiveDevice.Orientation expectedPadFallback)
        {
            FoldableLayoutState layout = IphoneDuoHinge.BuildLayout(
                IphoneDuoHingeStatus.PartiallyOpen, pointWidth, pointHeight, pixelWidth, pixelHeight);
            Assert.AreEqual(FoldableProductState.HalfOpened, layout.productState);

            ResponsiveDevice.FormFactor form = ResponsiveDevice.RefineFoldableForm(
                FoldableDeviceKind.FoldablePhone,
                ResponsiveDevice.MapFoldableProduct(FoldableDeviceKind.FoldablePhone, layout.productState),
                pixelWidth,
                pixelHeight);
            Assert.AreEqual(ResponsiveDevice.FormFactor.iPhoneDuoSeated, form);

            var key = new ResponsiveDevice.LayoutKey(
                form, ResponsiveDevice.DetectOrientationForForm(form, pixelWidth, pixelHeight));
            Assert.AreEqual(expectedSlot, key.Orientation);
            Assert.IsTrue(
                ResponsiveDevice.SlotAllowedByPlayerAxes(form, key.Orientation, portraitAllowed, landscapeAllowed),
                "the slot the device shows is the one the Inspector allows");

            // A disabled Seated slot falls back to Pad of the same screen shape first.
            Assert.IsTrue(ResponsiveWidgetSlotResolver.GetCandidate(
                key, 1, out ResponsiveDevice.FormFactor fallbackForm, out ResponsiveDevice.Orientation fallback));
            Assert.AreEqual(ResponsiveDevice.FormFactor.Pad, fallbackForm);
            Assert.AreEqual(expectedPadFallback, fallback);
        }

        [Test]
        public void IphoneDuoInner_MatchesCatalogAndBuiltinCanvas()
        {
            Assert.AreEqual(new Vector2(2007f, 2853f), ResponsiveCatalogDevices.IphoneDuoInnerPortrait);
            Assert.AreEqual(new Vector2(2853f, 2007f), ResponsiveCatalogDevices.IphoneDuoInnerLandscape);
            Assert.AreEqual(
                new Vector2(2007f, 2853f),
                ResponsiveRectState.BuiltinIphoneDuoSeatedDesignCanvasSize);
            Assert.AreEqual(2007, Mathf.RoundToInt(IphoneDuoHinge.InnerShortPoints * 3f), "669 pt @3x");
            Assert.AreEqual(2853, Mathf.RoundToInt(IphoneDuoHinge.InnerLongPoints * 3f), "951 pt @3x");
        }

        [Test]
        public void DetectActiveFormFactor_GenericPadAspect_WithoutPin_IsPad()
        {
            // Non-Fold8 sizes under padMax still classify as Pad by aspect.
            Assert.AreEqual(
                ResponsiveDevice.FormFactor.Pad,
                ResponsiveDevice.DetectActiveFormFactor(
                    new Vector2(1600f, 1200f), padMaxAspect: 1.6f));
        }

        [Test]
        public void DetectActiveFormFactor_Fold8Cover_BeatsStalePadPin()
        {
            // Expand pin left behind after Normal — Cover WxH must still apply FoldPhoneCover.
            ResponsiveDevice.SetSimulatorLayout(
                ResponsiveDevice.FormFactor.Pad,
                ResponsiveDevice.Orientation.Landscape);

            Assert.AreEqual(
                ResponsiveDevice.FormFactor.FoldPhoneCover,
                ResponsiveDevice.DetectActiveFormFactor(
                    ResponsiveCatalogDevices.FoldCoverLandscape, padMaxAspect: 1.6f));
        }

        [Test]
        public void DetectActiveFormFactor_Fold8Cover_WithSimulatorPin_IsFoldPhoneCover()
        {
            ResponsiveDevice.SetSimulatorLayout(
                ResponsiveDevice.MapFoldableProduct(
                    FoldableDeviceKind.FoldablePhone,
                    FoldableProductState.Closed),
                ResponsiveDevice.Orientation.Landscape);

            Assert.AreEqual(
                ResponsiveDevice.FormFactor.FoldPhoneCover,
                ResponsiveDevice.DetectActiveFormFactor(
                    ResponsiveCatalogDevices.FoldCoverLandscape, padMaxAspect: 1.6f));
        }

        [Test]
        public void DetectActiveFormFactor_Fold8UltraCover_WithoutPin_StaysPhone()
        {
            // Ultra cover 2520×1080 ≈ 2.33 — never hits the Pad landmine; stays tall Phone.
            Assert.AreEqual(
                ResponsiveDevice.FormFactor.Phone,
                ResponsiveDevice.DetectActiveFormFactor(
                    ResponsiveCatalogDevices.FlipMainLandscape, padMaxAspect: 1.6f));
        }

        [Test]
        public void ResolveLiveLayout_Fold8CoverLandscape_IsFoldPhoneCoverLandscape()
        {
            Vector2 cover = ResponsiveCatalogDevices.FoldCoverLandscape;
            ResponsiveDevice.LayoutKey key = ResponsiveDevice.ResolveLiveLayout(cover, padMaxAspect: 1.6f);
            Assert.AreEqual(ResponsiveDevice.FormFactor.FoldPhoneCover, key.Form);
            Assert.AreEqual(ResponsiveDevice.Orientation.Landscape, key.Orientation);
        }

        [Test]
        public void IsToolkitListName_LiveFold8_IsTrue()
        {
            Assert.IsTrue(FoldableDeviceCatalog.IsToolkitListName("★ Fold / Galaxy Z Fold8-like"));
            Assert.IsTrue(FoldableDeviceCatalog.IsToolkitListName("Galaxy Z Fold8-like"));
            Assert.IsNotNull(FoldableDeviceCatalog.FindById(ResponsiveCatalogDevices.FoldId));
        }

        [Test]
        public void IsToolkitListName_DeletedDualAndGeneric_IsFalse()
        {
            Assert.IsFalse(FoldableDeviceCatalog.IsToolkitListName("★ Dual / Dual-screen Hinge-like"));
            Assert.IsFalse(FoldableDeviceCatalog.IsToolkitListName("Dual-screen Hinge-like"));
            Assert.IsFalse(FoldableDeviceCatalog.IsToolkitListName("★ Fold / Foldable Generic"));
            Assert.IsFalse(FoldableDeviceCatalog.IsToolkitListName("Foldable Generic"));
            Assert.IsFalse(FoldableDeviceCatalog.IsToolkitListName("Generic"));
            Assert.IsNull(FoldableEditorSession.FindDeviceByFriendlyName("Dual-screen Hinge-like"));
            Assert.IsNull(FoldableEditorSession.FindDeviceByFriendlyName("Foldable Generic"));
        }

        [Test]
        public void LayoutResolver_PhoneLandscape_IsPhone()
        {
            PhonePadDeviceCatalog.DeviceProfile phone =
                PhonePadDeviceCatalog.FindById(ResponsiveCatalogDevices.PhoneId);
            Vector2 size = ResponsiveCatalogDevices.PhoneLandscape;
            ResponsiveDevice.LayoutKey key = ResponsiveUiSimulatorLayoutResolver.Resolve(
                PhonePadSelection(phone, size, landscape: true));
            Assert.AreEqual(ResponsiveDevice.FormFactor.Phone, key.Form);
            Assert.AreEqual(ResponsiveDevice.Orientation.Landscape, key.Orientation);
        }

        [Test]
        public void LayoutResolver_PadLandscape_IsPad()
        {
            PhonePadDeviceCatalog.DeviceProfile pad =
                PhonePadDeviceCatalog.FindById(ResponsiveCatalogDevices.PadId);
            Vector2 size = ResponsiveCatalogDevices.PadLandscape;
            ResponsiveDevice.LayoutKey key = ResponsiveUiSimulatorLayoutResolver.Resolve(
                PhonePadSelection(pad, size, landscape: true));
            Assert.AreEqual(ResponsiveDevice.FormFactor.Pad, key.Form);
            Assert.AreEqual(ResponsiveDevice.Orientation.Landscape, key.Orientation);
        }

        [Test]
        public void LayoutResolver_GalaxyTabS10_ListLabel_IsPad()
        {
            // ● Pad / + 16:10 (aspect == padMax) must resolve Pad, not Phone.
            var selection = new SimulatorDeviceBridge.Selection
            {
                FriendlyName = ResponsiveUiSimulatorName.PadListPrefix + "Galaxy Tab S10",
                DeviceModel = "SM-X710",
                ScreenWidth = 2560,
                ScreenHeight = 1600,
                RotationDegrees = 90,
                HasRotation = true,
                IsValid = true,
                DeviceIndex = 0,
                ScreenIndex = 0,
            };
            ResponsiveDevice.LayoutKey key = ResponsiveUiSimulatorLayoutResolver.Resolve(selection);
            Assert.AreEqual(ResponsiveDevice.FormFactor.Pad, key.Form);
            Assert.AreEqual(ResponsiveDevice.Orientation.Landscape, key.Orientation);
        }

        [Test]
        public void LayoutResolver_PadListLabel_BeatsStaleFoldableBinding()
        {
            FoldableDeviceCatalog.DeviceProfile fold =
                FoldableDeviceCatalog.FindById(ResponsiveCatalogDevices.FoldId);
            Assert.NotNull(fold);
            FoldableDeviceCatalog.ScreenProfile coverScreen = fold.Screens[0];
            for (int i = 0; i < fold.Screens.Length; i++)
            {
                if (fold.Screens[i].CoverOrOuter)
                {
                    coverScreen = fold.Screens[i];
                    break;
                }
            }

            var bound = new FoldableDeviceCatalog.Resolution
            {
                Device = fold,
                Screen = coverScreen,
                IsKnownFoldable = true,
            };
            try
            {
                FoldableEditorSession.ClearDeviceBinding(FoldableEditorSession.Source.Bridge);
                FoldableEditorSession.SetDeviceBinding(
                    bound, FoldableEditorSession.Source.Bridge, applyDefaults: true);

                ResponsiveDevice.LayoutKey key = ResponsiveUiSimulatorLayoutResolver.Resolve(
                    new SimulatorDeviceBridge.Selection
                    {
                        FriendlyName = ResponsiveUiSimulatorName.PadListPrefix + "Galaxy Tab S10",
                        DeviceModel = "SM-X710",
                        ScreenWidth = 2560,
                        ScreenHeight = 1600,
                        RotationDegrees = 90,
                        HasRotation = true,
                        IsValid = true,
                        DeviceIndex = 0,
                        ScreenIndex = 0,
                    });
                Assert.AreEqual(ResponsiveDevice.FormFactor.Pad, key.Form);
            }
            finally
            {
                FoldableEditorSession.ClearDeviceBinding(FoldableEditorSession.Source.Bridge);
                FoldableEditorSession.ResetProductDefaults();
                ResponsiveDevice.ClearSimulatorLayout();
            }
        }

        [Test]
        public void LayoutResolver_SixteenTenAspectWithoutListLabel_IsPad()
        {
            if (FoldableEditorSession.IsFoldableDeviceBound)
                Assert.Ignore("Live foldable session owns form before aspect fallback.");

            var selection = new SimulatorDeviceBridge.Selection
            {
                FriendlyName = "Unknown Tablet",
                DeviceModel = "",
                ScreenWidth = 2560,
                ScreenHeight = 1600,
                RotationDegrees = 90,
                HasRotation = true,
                IsValid = true,
                DeviceIndex = 0,
                ScreenIndex = 0,
            };
            ResponsiveDevice.LayoutKey key = ResponsiveUiSimulatorLayoutResolver.Resolve(selection);
            Assert.AreEqual(ResponsiveDevice.FormFactor.Pad, key.Form);
        }

        [Test]
        public void DetectActiveFormFactor_ExactPadMaxAspect_IsPad()
        {
            Assert.AreEqual(
                ResponsiveDevice.FormFactor.Pad,
                ResponsiveDevice.DetectActiveFormFactor(
                    new Vector2(2560f, 1600f), padMaxAspect: 1.6f));
        }

        [Test]
        public void ResolvePhonePadListRole_PadAndPhonePrefixes()
        {
            Assert.IsTrue(ResponsiveUiSimulatorName.ResolvePhonePadListRole(
                ResponsiveUiSimulatorName.PadListPrefix + "Galaxy Tab S10", out bool isPad));
            Assert.IsTrue(isPad);
            Assert.IsTrue(ResponsiveUiSimulatorName.ResolvePhonePadListRole(
                ResponsiveUiSimulatorName.PhoneDesignListPrefix + "iPhone 17 Pro Max", out isPad));
            Assert.IsFalse(isPad);
            Assert.IsFalse(ResponsiveUiSimulatorName.ResolvePhonePadListRole(
                "★ Fold / Galaxy Z Fold8", out _));
        }

        [Test]
        public void LayoutResolver_FoldCoverLandscape_IsFoldPhoneCover()
        {
            if (FoldableEditorSession.IsFoldableDeviceBound &&
                FoldableEditorSession.ProductState == FoldableProductState.HalfOpened)
                Assert.Ignore("Live Fold HalfOpened session owns product inference.");

            FoldableDeviceCatalog.DeviceProfile fold =
                FoldableDeviceCatalog.FindById(ResponsiveCatalogDevices.FoldId);
            Vector2 cover = ResponsiveCatalogDevices.FoldCoverLandscape;
            ResponsiveDevice.LayoutKey key = ResponsiveUiSimulatorLayoutResolver.Resolve(
                FoldSelection(fold, cover, landscape: true));
            Assert.AreEqual(ResponsiveDevice.FormFactor.FoldPhoneCover, key.Form);
            Assert.AreEqual(ResponsiveDevice.Orientation.Landscape, key.Orientation);
        }

        [Test]
        public void LayoutResolver_FlipCoverLandscape_IsFlipPhoneCover()
        {
            if (FoldableEditorSession.IsFoldableDeviceBound &&
                FoldableEditorSession.ProductState == FoldableProductState.HalfOpened)
                Assert.Ignore("Live Flip HalfOpened session owns product inference.");
            IgnoreWhenLiveFlipSessionBlocksCoverWxH();

            FoldableDeviceCatalog.DeviceProfile flip =
                FoldableDeviceCatalog.FindById(ResponsiveCatalogDevices.FlipId);
            Vector2 cover = ResponsiveCatalogDevices.FlipCoverLandscape;
            ResponsiveDevice.LayoutKey key = ResponsiveUiSimulatorLayoutResolver.Resolve(
                FoldSelection(flip, cover, landscape: true));
            Assert.AreEqual(ResponsiveDevice.FormFactor.FlipPhoneCover, key.Form);
            Assert.AreEqual(ResponsiveDevice.Orientation.Landscape, key.Orientation);
        }

        [Test]
        public void LayoutResolver_FlipOpenedSession_CoverWxH_IsPhone()
        {
            FoldableDeviceCatalog.DeviceProfile flip =
                FoldableDeviceCatalog.FindById(ResponsiveCatalogDevices.FlipId);
            Assert.NotNull(flip, "catalog needs Flip8");
            FoldableDeviceCatalog.ScreenProfile coverScreen = flip.Screens[0];
            for (int i = 0; i < flip.Screens.Length; i++)
            {
                if (flip.Screens[i].CoverOrOuter)
                {
                    coverScreen = flip.Screens[i];
                    break;
                }
            }

            var bound = new FoldableDeviceCatalog.Resolution
            {
                Device = flip,
                Screen = coverScreen,
                IsKnownFoldable = true,
            };
            try
            {
                FoldableEditorSession.ClearDeviceBinding(FoldableEditorSession.Source.Bridge);
                FoldableEditorSession.ResetProductDefaults();
                FoldableEditorSession.SetDeviceBinding(
                    bound, FoldableEditorSession.Source.Bridge, applyDefaults: true);
                Assert.AreEqual(
                    FoldableProductState.Opened, FoldableEditorSession.ProductState);

                Vector2 cover = ResponsiveCatalogDevices.FlipCoverLandscape;
                ResponsiveDevice.LayoutKey key = ResponsiveUiSimulatorLayoutResolver.Resolve(
                    FoldSelection(flip, cover, landscape: true));
                Assert.AreEqual(ResponsiveDevice.FormFactor.Phone, key.Form);
                Assert.AreEqual(
                    ResponsiveDevice.FormFactor.Phone,
                    ResponsiveDevice.DetectActiveFormFactor(cover, padMaxAspect: 1.6f));
                Assert.AreEqual(
                    ResponsiveDevice.FormFactor.Phone,
                    ResponsiveDevice.RefineFlipPhoneCover(
                        ResponsiveDevice.FormFactor.Phone, cover.x, cover.y));
            }
            finally
            {
                FoldableEditorSession.ClearDeviceBinding(FoldableEditorSession.Source.Bridge);
                FoldableEditorSession.ResetProductDefaults();
                ResponsiveDevice.ClearSimulatorLayout();
            }
        }

        [Test]
        public void LayoutResolver_FoldInnerLandscape_IsPad()
        {
            if (FoldableEditorSession.IsFoldableDeviceBound &&
                FoldableEditorSession.ProductState == FoldableProductState.HalfOpened)
                Assert.Ignore("Live Fold HalfOpened session owns product inference.");

            FoldableDeviceCatalog.DeviceProfile fold =
                FoldableDeviceCatalog.FindById(ResponsiveCatalogDevices.FoldId);
            Vector2 inner = ResponsiveCatalogDevices.FoldInnerLandscape;
            ResponsiveDevice.LayoutKey key = ResponsiveUiSimulatorLayoutResolver.Resolve(
                FoldSelection(fold, inner, landscape: true));
            Assert.AreEqual(ResponsiveDevice.FormFactor.Pad, key.Form);
            Assert.AreEqual(ResponsiveDevice.Orientation.Landscape, key.Orientation);
        }

        [Test]
        public void LayoutResolver_FlipMainLandscape_IsPhone()
        {
            if (FoldableEditorSession.IsFoldableDeviceBound &&
                FoldableEditorSession.ProductState == FoldableProductState.HalfOpened)
                Assert.Ignore("Live Flip HalfOpened session owns product inference.");

            FoldableDeviceCatalog.DeviceProfile flip =
                FoldableDeviceCatalog.FindById(ResponsiveCatalogDevices.FlipId);
            Vector2 main = ResponsiveCatalogDevices.FlipMainLandscape;
            ResponsiveDevice.LayoutKey key = ResponsiveUiSimulatorLayoutResolver.Resolve(
                FoldSelection(flip, main, landscape: true));
            Assert.AreEqual(ResponsiveDevice.FormFactor.Phone, key.Form);
            Assert.AreEqual(ResponsiveDevice.Orientation.Landscape, key.Orientation);
        }

        [Test]
        public void SelectionIdentity_IgnoresResolutionFlicker()
        {
            SimulatorDeviceBridge.Selection a = new SimulatorDeviceBridge.Selection
            {
                DeviceModel = "iPhone18,3",
                FriendlyName = "iPhone 17 Pro Max",
                ScreenIndex = 0,
                RotationDegrees = 90,
                HasRotation = true,
                ScreenWidth = 1320,
                ScreenHeight = 2868,
                IsValid = true,
            };
            SimulatorDeviceBridge.Selection b = a;
            b.ScreenWidth = 1;
            b.ScreenHeight = 1;
            Assert.AreEqual(
                SimulatorDeviceBridge.BuildSelectionIdentity(a),
                SimulatorDeviceBridge.BuildSelectionIdentity(b));
            Assert.AreNotEqual("", SimulatorDeviceBridge.BuildSelectionIdentity(a));
            a.IsValid = false;
            Assert.AreEqual("", SimulatorDeviceBridge.BuildSelectionIdentity(a));
        }

        static SimulatorDeviceBridge.Selection PhonePadSelection(
            PhonePadDeviceCatalog.DeviceProfile profile,
            Vector2 size,
            bool landscape)
        {
            return new SimulatorDeviceBridge.Selection
            {
                FriendlyName = profile.ShortName,
                DeviceModel = profile.DeviceModel,
                ScreenWidth = (int)size.x,
                ScreenHeight = (int)size.y,
                RotationDegrees = landscape ? 90 : 0,
                HasRotation = true,
                IsValid = true,
                DeviceIndex = 0,
                ScreenIndex = 0,
            };
        }

        static SimulatorDeviceBridge.Selection FoldSelection(
            FoldableDeviceCatalog.DeviceProfile profile,
            Vector2 size,
            bool landscape)
        {
            string prefix = profile.Kind == FoldableDeviceKind.FlipablePhone
                ? FoldableDeviceCatalog.FlipPrefix
                : FoldableDeviceCatalog.FoldPrefix;
            return new SimulatorDeviceBridge.Selection
            {
                FriendlyName = prefix + profile.ShortName,
                DeviceModel = profile.DeviceModel,
                ScreenWidth = (int)size.x,
                ScreenHeight = (int)size.y,
                RotationDegrees = landscape ? 90 : 0,
                HasRotation = true,
                IsValid = true,
                DeviceIndex = 0,
                ScreenIndex = 0,
            };
        }
    }
}
