using NUnit.Framework;
using FDPlus.ResponsiveUI;

using static FDPlus.ResponsiveUI.Tests.ResponsiveEditorTestUtil;

namespace FDPlus.ResponsiveUI.Tests
{
    public class ResponsiveScrollLayoutTests
    {
        [Test]
        public void DisabledFoldPhoneCover_FallsBackToPhoneInterval()
        {
            var layout = new ResponsiveScrollLayout
            {
                phoneLandscape = ResponsiveScrollState.From(0.2f, 0.5f),
                foldPhoneCoverLandscape = ResponsiveScrollState.From(0.1f, 0.25f, enabled: false),
            };
            AssertApply(layout, ResponsiveDevice.FormFactor.FoldPhoneCover, true, 0.99f, 0.99f, true, 0.2f, 0.5f);
        }

        [Test]
        public void EnabledFoldPhoneCover_UsesCoverInterval()
        {
            var layout = new ResponsiveScrollLayout
            {
                phoneLandscape = ResponsiveScrollState.From(0.2f, 0.5f),
                foldPhoneCoverLandscape = ResponsiveScrollState.From(0.12f, 0.4f),
            };
            AssertApply(layout, ResponsiveDevice.FormFactor.FoldPhoneCover, true, 0f, 0f, true, 0.12f, 0.4f);
        }

        [Test]
        public void LandscapeDisabled_FallsBackToPortraitInterval()
        {
            var layout = new ResponsiveScrollLayout
            {
                phonePortrait = ResponsiveScrollState.From(0.18f, 0.4f),
                phoneLandscape = ResponsiveScrollState.From(0.3f, 0.6f, enabled: false),
            };
            AssertApply(layout, ResponsiveDevice.FormFactor.Phone, true, 0f, 0f, true, 0.18f, 0.4f);
        }

        [Test]
        public void UnsetInterval_UsesDefault()
        {
            var layout = new ResponsiveScrollLayout
            {
                phoneLandscape = new ResponsiveScrollState
                {
                    enabled = true,
                    cellInterval = 0f,
                    scrollOffset = 2f,
                },
            };
            AssertApply(
                layout,
                ResponsiveDevice.FormFactor.Phone,
                true,
                0.99f,
                0f,
                true,
                ResponsiveScrollState.DefaultCellInterval,
                1f);
        }

        [Test]
        public void TinyInterval_ClampsToMinimum()
        {
            var layout = new ResponsiveScrollLayout
            {
                phoneLandscape = ResponsiveScrollState.From(0.001f, 0.5f),
            };
            AssertApply(
                layout,
                ResponsiveDevice.FormFactor.Phone,
                true,
                0.99f,
                0f,
                true,
                ResponsiveScrollState.MinCellInterval,
                0.5f);
        }

        [Test]
        public void NoEnabledSlot_LeavesCallerValues()
        {
            AssertApply(
                new ResponsiveScrollLayout(),
                ResponsiveDevice.FormFactor.Phone,
                true,
                0.21f,
                0.5f,
                false,
                0.21f,
                0.5f);
        }

        [Test]
        public void SeedAll_EnablesFlipHalfOpenedAndLeavesFlipCoverLandscapeDisabled()
        {
            var layout = new ResponsiveScrollLayout();
            layout.SeedAll(0.15f, 0.3f, enablePortrait: true, enableLandscape: true);
            AssertSeededSlots(layout, 0.15f, 0.3f, portrait: true, landscape: true);
        }

        [Test]
        public void SeedAll_ZeroHostInterval_UsesDefaultInterval()
        {
            var layout = new ResponsiveScrollLayout();
            layout.SeedAll(0f, 0.5f, enablePortrait: true, enableLandscape: true);
            AssertSeededSlots(
                layout,
                ResponsiveScrollState.DefaultCellInterval,
                0.5f,
                portrait: true,
                landscape: true);
        }

        [Test]
        public void SeedAll_RespectsOrientationFlags()
        {
            var layout = new ResponsiveScrollLayout();
            layout.SeedAll(0.2f, 0.5f, enablePortrait: false, enableLandscape: true);
            AssertSeededSlots(layout, 0.2f, 0.5f, portrait: false, landscape: true);
        }

        [Test]
        public void SeedAll_OneAxisLock_KeepsPostureOwnedSlotsEnabled()
        {
            var layout = new ResponsiveScrollLayout();
            layout.SeedAll(0.2f, 0.5f, enablePortrait: true, enableLandscape: false);
            AssertSeededSlots(layout, 0.2f, 0.5f, portrait: true, landscape: false);
            Assert.IsFalse(layout.padLandscape.enabled);
            // Fold / Fold Cover Half Opened name the posture and follow the lock like other slots:
            // portrait-only shows only their Portrait (wide flat screen) slot.
            Assert.IsTrue(layout.foldHalfOpenedPortrait.enabled);
            Assert.IsFalse(layout.foldHalfOpenedLandscape.enabled);
            Assert.IsTrue(layout.foldCoverHalfOpenedPortrait.enabled);
            Assert.IsFalse(layout.foldCoverHalfOpenedLandscape.enabled);
            // iPhone Duo Seated allows only the hold's axis, so both Seated slots seed enabled.
            Assert.IsTrue(layout.iPhoneDuoSeatedPortrait.enabled, "portrait-only: tall laptop screen");
            Assert.IsTrue(layout.iPhoneDuoSeatedLandscape.enabled, "portrait-only: wide book while Seated");

            layout = new ResponsiveScrollLayout();
            layout.SeedAll(0.2f, 0.5f, enablePortrait: false, enableLandscape: true);
            AssertSeededSlots(layout, 0.2f, 0.5f, portrait: false, landscape: true);
            Assert.IsFalse(layout.padPortrait.enabled);
            Assert.IsTrue(layout.iPhoneDuoSeatedLandscape.enabled, "landscape-only: wide book screen");
            Assert.IsTrue(layout.iPhoneDuoSeatedPortrait.enabled, "landscape-only: tall laptop while Seated");
        }

        [Test]
        public void SeedIfEmpty_RespectsOrientationFlags()
        {
            var layout = new ResponsiveScrollLayout();
            Assert.IsTrue(layout.SeedIfEmpty(0.2f, 0.5f, enablePortrait: false, enableLandscape: true));
            AssertSeededSlots(layout, 0.2f, 0.5f, portrait: false, landscape: true);
        }

        [Test]
        public void SeedIfEmpty_SeedsBlankLayout()
        {
            var layout = new ResponsiveScrollLayout();
            Assert.IsTrue(layout.SeedIfEmpty(0.2f, 0.5f, enablePortrait: true, enableLandscape: true));
            AssertSeededSlots(layout, 0.2f, 0.5f, portrait: true, landscape: true);
        }

        [Test]
        public void SeedIfEmpty_SeedsEnabledSlotsWithUnsetInterval()
        {
            var layout = new ResponsiveScrollLayout
            {
                phoneLandscape = new ResponsiveScrollState { enabled = true },
            };
            Assert.IsTrue(layout.SeedIfEmpty(0.21f, 0.5f, enablePortrait: true, enableLandscape: true));
            AssertSeededSlots(layout, 0.21f, 0.5f, portrait: true, landscape: true);
        }

        [Test]
        public void SeedIfEmpty_LeavesAuthoredSlots()
        {
            var layout = new ResponsiveScrollLayout
            {
                phoneLandscape = ResponsiveScrollState.From(0.21f, 0.5f),
            };
            Assert.IsFalse(layout.SeedIfEmpty(0.2f, 0.5f, enablePortrait: true, enableLandscape: true));
            Assert.IsFalse(layout.foldPhoneCoverLandscape.enabled);
            Assert.AreEqual(0.21f, layout.phoneLandscape.cellInterval);
        }

        [Test]
        public void ApplyChanged_ReturnsFalseWhenAlreadyCurrent()
        {
            var layout = new ResponsiveScrollLayout
            {
                phoneLandscape = ResponsiveScrollState.From(0.2f, 0.5f),
            };
            AssertApplyChanged(
                layout,
                ResponsiveDevice.FormFactor.Phone,
                ResponsiveDevice.Orientation.Landscape,
                0.2f,
                0.5f,
                false,
                0.2f,
                0.5f);
        }

        [Test]
        public void ApplyChanged_WritesWhenValuesDiffer()
        {
            var layout = new ResponsiveScrollLayout
            {
                phoneLandscape = ResponsiveScrollState.From(0.12f, 0.4f),
            };
            AssertApplyChanged(
                layout,
                ResponsiveDevice.FormFactor.Phone,
                ResponsiveDevice.Orientation.Landscape,
                0.2f,
                0.5f,
                true,
                0.12f,
                0.4f);
        }

        [Test]
        public void ApplyChanged_TracksOrientationAndProductTransitions()
        {
            var layout = new ResponsiveScrollLayout
            {
                phonePortrait = ResponsiveScrollState.From(0.2f, 0.1f),
                phoneLandscape = ResponsiveScrollState.From(0.25f, 0.2f),
                foldPhoneCoverLandscape = ResponsiveScrollState.From(0.15f, 0.3f),
                padPortrait = ResponsiveScrollState.From(0.3f, 0.4f),
                padLandscape = ResponsiveScrollState.From(0.35f, 0.5f),
                foldHalfOpenedPortrait = ResponsiveScrollState.From(0.4f, 0.6f, enabled: false),
                foldHalfOpenedLandscape = ResponsiveScrollState.From(0.45f, 0.7f, enabled: false),
                flipHalfOpenedLandscape = ResponsiveScrollState.From(0.1f, 0.8f),
            };
            float interval = 0f;
            float offset = 0f;
            AssertTransition(ref layout, ResponsiveDevice.FormFactor.Phone, ResponsiveDevice.Orientation.Portrait, ref interval, ref offset, 0.2f, 0.1f);
            AssertTransition(ref layout, ResponsiveDevice.FormFactor.Phone, ResponsiveDevice.Orientation.Landscape, ref interval, ref offset, 0.25f, 0.2f);
            AssertTransition(ref layout, ResponsiveDevice.FormFactor.Pad, ResponsiveDevice.Orientation.Landscape, ref interval, ref offset, 0.35f, 0.5f);
            // Fold Half Opened Landscape is the tall screen: disabled, it falls back to Phone Portrait.
            AssertTransition(ref layout, ResponsiveDevice.FormFactor.FoldHalfOpened, ResponsiveDevice.Orientation.Landscape, ref interval, ref offset, 0.2f, 0.1f);
            AssertTransition(ref layout, ResponsiveDevice.FormFactor.FlipHalfOpened, ResponsiveDevice.Orientation.Landscape, ref interval, ref offset, 0.1f, 0.8f);
            AssertTransition(ref layout, ResponsiveDevice.FormFactor.FoldPhoneCover, ResponsiveDevice.Orientation.Landscape, ref interval, ref offset, 0.15f, 0.3f);
            // iPhone Duo falls back to Pad of the same screen shape: Portrait (tall laptop) → Pad
            // Portrait, Landscape (wide book) → Pad Landscape.
            AssertTransition(ref layout, ResponsiveDevice.FormFactor.iPhoneDuoSeated, ResponsiveDevice.Orientation.Portrait, ref interval, ref offset, 0.3f, 0.4f);
            AssertTransition(ref layout, ResponsiveDevice.FormFactor.iPhoneDuoSeated, ResponsiveDevice.Orientation.Landscape, ref interval, ref offset, 0.35f, 0.5f);
        }

        static void AssertApply(
            in ResponsiveScrollLayout layout,
            ResponsiveDevice.FormFactor form,
            bool landscape,
            float startInterval,
            float startOffset,
            bool expectedApplied,
            float expectedInterval,
            float expectedOffset)
        {
            float interval = startInterval;
            float offset = startOffset;
            Assert.AreEqual(
                expectedApplied,
                layout.Apply(LayoutKey(form, landscape), ref interval, ref offset));
            Assert.AreEqual(expectedInterval, interval);
            Assert.AreEqual(expectedOffset, offset);
        }

        static void AssertApplyChanged(
            in ResponsiveScrollLayout layout,
            ResponsiveDevice.FormFactor form,
            ResponsiveDevice.Orientation orientation,
            float startInterval,
            float startOffset,
            bool expectedChanged,
            float expectedInterval,
            float expectedOffset)
        {
            float interval = startInterval;
            float offset = startOffset;
            Assert.AreEqual(
                expectedChanged,
                layout.ApplyChanged(LayoutKey(form, orientation), ref interval, ref offset));
            Assert.AreEqual(expectedInterval, interval);
            Assert.AreEqual(expectedOffset, offset);
        }

        static void AssertTransition(
            ref ResponsiveScrollLayout layout,
            ResponsiveDevice.FormFactor form,
            ResponsiveDevice.Orientation orientation,
            ref float interval,
            ref float offset,
            float expectedInterval,
            float expectedOffset)
        {
            Assert.IsTrue(layout.ApplyChanged(
                LayoutKey(form, orientation),
                ref interval,
                ref offset));
            Assert.AreEqual(expectedInterval, interval);
            Assert.AreEqual(expectedOffset, offset);
        }

        static void AssertSeededSlots(
            in ResponsiveScrollLayout layout,
            float interval,
            float offset,
            bool portrait,
            bool landscape)
        {
            string[] names = ResponsiveScrollLayout.StatePropertyNames;
            for (int i = 0; i < names.Length; i++)
            {
                ResponsiveScrollState slot = Slot(layout, i);
                Assert.AreEqual(
                    ExpectedScrollSeedEnabled(i, portrait, landscape),
                    slot.enabled,
                    names[i]);
                Assert.AreEqual(interval, slot.cellInterval, names[i]);
                Assert.AreEqual(offset, slot.scrollOffset, names[i]);
            }
        }

        static bool ExpectedScrollSeedEnabled(int slotIndex, bool portrait, bool landscape) =>
            // Flip Cover Landscape always seeds disabled; Wide Screen seeds Enabled on Wide Screen
            // targets only; every other slot follows the lock.
            !ResponsiveWidgetLayout.IsSeedDisabledSlot(slotIndex) &&
            ResponsivePlatforms.SeedsEnabled(ResponsiveWidgetLayout.SlotFormFactor(slotIndex)) &&
            ResponsiveEditorTestUtil.ExpectedSlotAllowed(slotIndex, portrait, landscape);

        /// <summary>
        /// Same form × orientation map as <see cref="ResponsiveScrollLayout"/> Apply.
        /// </summary>
        static ResponsiveScrollState Slot(in ResponsiveScrollLayout layout, int slotIndex)
        {
            bool landscape = ResponsiveWidgetLayout.SlotOrientation(slotIndex) ==
                ResponsiveDevice.Orientation.Landscape;
            switch (ResponsiveWidgetLayout.SlotFormFactor(slotIndex))
            {
                case ResponsiveDevice.FormFactor.Pad:
                    return landscape ? layout.padLandscape : layout.padPortrait;
                case ResponsiveDevice.FormFactor.FoldHalfOpened:
                    return landscape
                        ? layout.foldHalfOpenedLandscape
                        : layout.foldHalfOpenedPortrait;
                case ResponsiveDevice.FormFactor.FlipHalfOpened:
                    return landscape
                        ? layout.flipHalfOpenedLandscape
                        : layout.flipHalfOpenedPortrait;
                case ResponsiveDevice.FormFactor.FoldPhoneCover:
                    return landscape ? layout.foldPhoneCoverLandscape : layout.foldPhoneCoverPortrait;
                case ResponsiveDevice.FormFactor.FlipPhoneCover:
                    return landscape
                        ? layout.flipPhoneCoverLandscape
                        : layout.flipPhoneCoverPortrait;
                case ResponsiveDevice.FormFactor.FoldCoverHalfOpened:
                    return landscape
                        ? layout.foldCoverHalfOpenedLandscape
                        : layout.foldCoverHalfOpenedPortrait;
                case ResponsiveDevice.FormFactor.iPhoneDuoCover:
                    return landscape
                        ? layout.iPhoneDuoCoverLandscape
                        : layout.iPhoneDuoCoverPortrait;
                case ResponsiveDevice.FormFactor.iPhoneDuoSeated:
                    return landscape
                        ? layout.iPhoneDuoSeatedLandscape
                        : layout.iPhoneDuoSeatedPortrait;
                case ResponsiveDevice.FormFactor.WideScreen:
                    return landscape
                        ? layout.wideScreenLandscape
                        : layout.wideScreenPortrait;
                default:
                    return landscape ? layout.phoneLandscape : layout.phonePortrait;
            }
        }
    }
}
