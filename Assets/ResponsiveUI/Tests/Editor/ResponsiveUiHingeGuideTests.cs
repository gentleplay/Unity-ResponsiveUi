using FDPlus.Foldable;
using FDPlus.Foldable.Editor;
using NUnit.Framework;
using UnityEngine;
using FDPlus.ResponsiveUI;
using FDPlus.ResponsiveUI.Editor;

namespace FDPlus.ResponsiveUI.Tests
{
    /// <summary>
    /// Half-open hinge guide math for ResponsiveUI dual-pane border overlay.
    /// </summary>
    public class ResponsiveUiHingeGuideTests
    {
        const string Fold8 = "★ Fold / Galaxy Z Fold8-like";
        const string Flip8 = "★ Flip / Galaxy Z Flip8-like";

        [Test]
        public void IsHalfOpenedForm_HingeSplitFormsOnly()
        {
            Assert.IsTrue(ResponsiveDevice.IsHalfOpenedForm(
                ResponsiveDevice.FormFactor.FoldHalfOpened));
            Assert.IsTrue(ResponsiveDevice.IsHalfOpenedForm(
                ResponsiveDevice.FormFactor.FlipHalfOpened));
            Assert.IsTrue(ResponsiveDevice.IsHalfOpenedForm(
                ResponsiveDevice.FormFactor.FoldCoverHalfOpened));
            Assert.IsTrue(ResponsiveDevice.IsHalfOpenedForm(
                ResponsiveDevice.FormFactor.iPhoneDuoSeated));
            Assert.IsFalse(ResponsiveDevice.IsHalfOpenedForm(
                ResponsiveDevice.FormFactor.Phone));
            Assert.IsFalse(ResponsiveDevice.IsHalfOpenedForm(
                ResponsiveDevice.FormFactor.Pad));
            Assert.IsFalse(ResponsiveDevice.IsHalfOpenedForm(
                ResponsiveDevice.FormFactor.FoldPhoneCover));
            Assert.IsFalse(ResponsiveDevice.IsHalfOpenedForm(
                ResponsiveDevice.FormFactor.FlipPhoneCover));
        }

        [Test]
        public void ComputeFromLayout_Tabletop_HorizontalCenter()
        {
            FoldableLayoutState layout = SimulatedHalfOpened(
                FoldableOrientation.Horizontal, 2000, 1000, hinge: 0.5f, thickness: 20);

            Assert.IsTrue(ResponsiveUiHingeGuide.ComputeFromLayout(
                layout, out bool vertical, out float normalized, out float thickness));
            Assert.IsFalse(vertical);
            Assert.AreEqual(0.5f, normalized, 1e-3f);
            Assert.AreEqual(20f / 1000f, thickness, 1e-3f);
        }

        [Test]
        public void ComputeFromLayout_Book_VerticalCenter()
        {
            FoldableLayoutState layout = SimulatedHalfOpened(
                FoldableOrientation.Vertical, 2000, 1000, hinge: 0.4f, thickness: 20);

            Assert.IsTrue(ResponsiveUiHingeGuide.ComputeFromLayout(
                layout, out bool vertical, out float normalized, out _));
            Assert.IsTrue(vertical);
            Assert.AreEqual(0.4f, normalized, 1e-2f);
        }

        [Test]
        public void ComputeFromLayout_FlipLandscapeTabletop_IsVertical()
        {
            var layout = FoldableProductStateResolver.CreateFromProductState(
                FoldableDeviceKind.FlipablePhone,
                FoldableProductState.HalfOpened,
                2640,
                1080,
                hingeNormalized: 0.5f,
                hingeThicknessPx: 24,
                halfOpenHinge: FoldableOrientation.Horizontal,
                isSeparating: true);

            Assert.IsTrue(ResponsiveUiHingeGuide.ComputeFromLayout(
                layout, out bool vertical, out float normalized, out _));
            Assert.IsTrue(vertical);
            Assert.AreEqual(0.5f, normalized, 1e-2f);
        }

        [Test]
        public void ComputeFromLayout_FoldLandscapeTabletop_StaysHorizontal()
        {
            // Fold Half Opened Landscape is the tall screen (the flat screen turned 90°): its crease
            // runs left to right. The wide screen is Portrait with a vertical crease.
            var layout = FoldableProductStateResolver.CreateFromProductState(
                FoldableDeviceKind.FoldablePhone,
                FoldableProductState.HalfOpened,
                1812,
                2176,
                hingeNormalized: 0.5f,
                hingeThicknessPx: 24,
                halfOpenHinge: FoldableOrientation.Horizontal,
                isSeparating: true);

            Assert.IsTrue(ResponsiveUiHingeGuide.ComputeFromLayout(
                layout, out bool vertical, out float normalized, out _));
            Assert.IsFalse(vertical);
            Assert.AreEqual(0.5f, normalized, 1e-2f);
        }

        [Test]
        public void MapFullScreenNormToCanvasLocal_ClippedControlsPane_PutsHingeAtTop()
        {
            float local = ResponsiveUiHingeGuide.MapFullScreenNormToCanvasLocal(
                0.5f, canvasAnchorMin: 0f, canvasAnchorMax: 0.5f);
            Assert.AreEqual(1f, local, 1e-4f);
        }

        [Test]
        public void MapFullScreenNormToCanvasLocal_FullCanvas_KeepsCenter()
        {
            float local = ResponsiveUiHingeGuide.MapFullScreenNormToCanvasLocal(
                0.5f, canvasAnchorMin: 0f, canvasAnchorMax: 1f);
            Assert.AreEqual(0.5f, local, 1e-4f);
        }

        [Test]
        public void ToGuiRect_HorizontalCenter_IsMidScreenYDown()
        {
            var view = new Rect(0f, 0f, 200f, 100f);
            Rect line = ResponsiveUiHingeGuide.ToGuiRect(view, vertical: false, 0.5f, 0.1f);
            Assert.AreEqual(200f, line.width, 1e-3f);
            Assert.AreEqual(10f, line.height, 1e-3f);
            Assert.AreEqual(45f, line.y, 1e-3f);
        }

        [Test]
        public void ToGuiRect_PixelThicknessOverride_IgnoresNormalized()
        {
            var view = new Rect(0f, 0f, 200f, 100f);
            Rect line = ResponsiveUiHingeGuide.ToGuiRect(
                view, vertical: false, 0.5f, thicknessNormalized: 0.5f, thicknessPixels: 4f);
            Assert.AreEqual(200f, line.width, 1e-3f);
            Assert.AreEqual(4f, line.height, 1e-3f);
            Assert.AreEqual(48f, line.y, 1e-3f);
        }

        [Test]
        public void DefaultGuideStyle_IsDarkGrayAndFivePx()
        {
            Assert.AreEqual(0.25f, ResponsiveUiHingeGuide.DefaultGuideColor.r, 1e-3f);
            Assert.AreEqual(0.4f, ResponsiveUiHingeGuide.DefaultGuideColor.a, 1e-3f);
            Assert.AreEqual(5f, ResponsiveUiHingeGuide.DefaultThicknessPx, 1e-3f);
            Assert.AreEqual(1f, ResponsiveUiHingeGuide.MinThicknessPx, 1e-3f);
            Assert.AreEqual(10f, ResponsiveUiHingeGuide.MaxThicknessPx, 1e-3f);
        }

        [Test]
        public void ComputeFromLayout_Opened_ReturnsFalse()
        {
            var layout = FoldableLayoutState.CreateSimulated(
                FoldableState.Flat,
                FoldableOrientation.Horizontal,
                2000,
                1000,
                hingeNormalized: 0.5f,
                hingeThicknessPx: 20,
                isSeparating: false);
            layout.productState = FoldableProductState.Opened;

            Assert.IsFalse(ResponsiveUiHingeGuide.CanDrawFromLayout(layout));
            Assert.IsFalse(ResponsiveUiHingeGuide.ComputeFromLayout(
                layout, out _, out _, out _));
        }

        [Test]
        public void ComputeFromLayout_ClosedCover_ReturnsFalse()
        {
            Vector2 cover = ResponsiveCatalogDevices.FoldCoverPortrait;
            var layout = FoldableProductStateResolver.CreateFromProductState(
                FoldableDeviceKind.FoldablePhone,
                FoldableProductState.Closed,
                Mathf.RoundToInt(cover.x),
                Mathf.RoundToInt(cover.y),
                isSeparating: true);

            Assert.IsTrue(layout.isCoverScreen);
            Assert.IsFalse(ResponsiveUiHingeGuide.CanDrawFromLayout(layout));
            Assert.IsFalse(ResponsiveUiHingeGuide.ComputeFromLayout(
                layout, out _, out _, out _));
        }

        [Test]
        public void ComputeFromLayout_HalfOpenedWithoutSeparating_ReturnsFalse()
        {
            FoldableLayoutState layout = SimulatedHalfOpened(
                FoldableOrientation.Horizontal, 2000, 1000, separating: false);

            Assert.IsFalse(ResponsiveUiHingeGuide.CanDrawFromLayout(layout));
            Assert.IsFalse(ResponsiveUiHingeGuide.ComputeFromLayout(
                layout, out _, out _, out _));
        }

        [Test]
        public void LiveSelectionAllowsHinge_PhoneSkin_IsFalse()
        {
            AssertLiveHinge("iPhone 17 Pro Max", ResponsiveCatalogDevices.PhonePortrait, false);
        }

        [Test]
        public void LiveSelectionAllowsHinge_FoldCover_IsFalse()
        {
            AssertLiveHinge(Fold8, ResponsiveCatalogDevices.FoldCoverPortrait, false);
        }

        [Test]
        public void LiveSelectionAllowsHinge_FoldCoverSafeArea_IsFalse()
        {
            AssertLiveHinge(Fold8, ResponsiveCatalogDevices.FoldCoverSafeAreaLandscape, false, screenIndex: 1);
        }

        [Test]
        public void LiveSelectionAllowsHinge_FlipCover_IsFalse()
        {
            AssertLiveHinge(Flip8, ResponsiveCatalogDevices.FlipCoverPortrait, false);
        }

        [Test]
        public void LiveSelectionAllowsHinge_FoldInner_IsTrue()
        {
            AssertLiveHinge(Fold8, ResponsiveCatalogDevices.FoldInnerPortrait, true, screenIndex: 1);
        }

        [Test]
        public void LiveSelectionAllowsHinge_FlipMain_IsTrue()
        {
            AssertLiveHinge(Flip8, ResponsiveCatalogDevices.FlipMainPortrait, true, screenIndex: 1);
        }

        [Test]
        public void LiveSelectionAllowsHinge_InvalidSelection_DefersToSession()
        {
            var selection = new SimulatorDeviceBridge.Selection { IsValid = false };
            Assert.IsTrue(ResponsiveUiHingeGuideOverlay.LiveSelectionAllowsHinge(selection));
        }

        static FoldableLayoutState SimulatedHalfOpened(
            FoldableOrientation orientation,
            int width,
            int height,
            float hinge = 0.5f,
            int thickness = 20,
            bool separating = true)
        {
            var layout = FoldableLayoutState.CreateSimulated(
                FoldableState.HalfOpened,
                orientation,
                width,
                height,
                hinge,
                thickness,
                separating);
            layout.productState = FoldableProductState.HalfOpened;
            return layout;
        }

        static void AssertLiveHinge(
            string friendlyName,
            Vector2 size,
            bool allowed,
            int screenIndex = 0)
        {
            Assert.AreEqual(
                allowed,
                ResponsiveUiHingeGuideOverlay.LiveSelectionAllowsHinge(
                    Selection(friendlyName, size.x, size.y, screenIndex)),
                friendlyName);
        }

        static SimulatorDeviceBridge.Selection Selection(
            string friendlyName,
            float width,
            float height,
            int screenIndex = 0)
        {
            return new SimulatorDeviceBridge.Selection
            {
                FriendlyName = friendlyName,
                DeviceModel = "",
                ScreenIndex = screenIndex,
                ScreenWidth = Mathf.RoundToInt(width),
                ScreenHeight = Mathf.RoundToInt(height),
                IsValid = true
            };
        }
    }
}
