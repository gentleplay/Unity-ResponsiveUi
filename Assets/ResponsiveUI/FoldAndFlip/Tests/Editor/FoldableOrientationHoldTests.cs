using NUnit.Framework;
using UnityEngine;

namespace FDPlus.Foldable.Tests
{
    public class FoldableOrientationHoldTests
    {
        static FoldableHalfOpenPose Pose(FoldableContentRuns runs = FoldableContentRuns.AsAndroid,
            FoldableHalfOpenRotation rotation = FoldableHalfOpenRotation.AsAndroid) =>
            new FoldableHalfOpenPose { contentRuns = runs, whileRotating = rotation };

        static readonly bool?[] Locks = { true, false, null };

        static FoldableOrientationRequest Decide(FoldableHalfOpenPose pose, bool isFold, bool halfOpen,
            bool? locked, bool entryLandscape, bool inverted = false,
            ScreenOrientation player = ScreenOrientation.AutoRotation) =>
            FoldableOrientationHold.Decide(pose, isFold, halfOpen, locked, player, entryLandscape, inverted);

        [Test]
        public void Decide_DefaultCards_NeverHold()
        {
            foreach (var locked in Locks)
            foreach (var isFold in new[] { true, false })
            foreach (var halfOpen in new[] { true, false })
            foreach (var entry in new[] { true, false })
            foreach (var inverted in new[] { true, false })
                Assert.IsFalse(Decide(Pose(), isFold, halfOpen, locked, entry, inverted).hold,
                    $"lock {locked} fold {isFold} half open {halfOpen}");
        }

        [Test]
        public void Decide_LeftToRight_LandscapeOnly_SwitchesAxis()
        {
            // Fold8 landscape-only half opened: the OS gives the tall screen, the card wants it wide.
            var request = Decide(Pose(FoldableContentRuns.LeftToRight), true, true, true, false);
            Assert.IsTrue(request.hold);
            Assert.AreEqual(false, request.tallAxis);

            request = Decide(Pose(FoldableContentRuns.TopToDown), true, true, false, true);
            Assert.IsTrue(request.hold);
            Assert.AreEqual(true, request.tallAxis);
        }

        [Test]
        public void Decide_AlreadyRightShape_KeepsCurrent()
        {
            // The axis follows the wanted shape whatever the screen shows now.
            Assert.AreEqual(false, Decide(Pose(FoldableContentRuns.LeftToRight), true, true, true, true).tallAxis);
            Assert.AreEqual(true, Decide(Pose(FoldableContentRuns.TopToDown), false, true, null, false).tallAxis);
        }

        [Test]
        public void Decide_LockedBothAxes_HoldsCurrent()
        {
            foreach (var entry in new[] { true, false })
            {
                var request = Decide(Pose(rotation: FoldableHalfOpenRotation.Locked), true, true, null, entry);
                Assert.IsTrue(request.hold);
                Assert.AreEqual(!entry, request.tallAxis);
            }
        }

        [Test]
        public void Decide_FollowDeviceUnderLock_AutoRotation()
        {
            foreach (var locked in new bool?[] { true, false })
            foreach (var isFold in new[] { true, false })
            {
                var request = Decide(Pose(rotation: FoldableHalfOpenRotation.FollowDevice), isFold, true, locked, false);
                Assert.IsTrue(request.hold);
                Assert.IsNull(request.tallAxis);
            }
        }

        [Test]
        public void Decide_LeavingHalfOpen_Releases()
        {
            Assert.IsFalse(Decide(Pose(FoldableContentRuns.LeftToRight), true, false, true, true).hold);
        }

        [Test]
        public void Decide_FixedDefaultOrientation_NeverHolds()
        {
            foreach (var fixedOrientation in new[] { ScreenOrientation.Portrait, ScreenOrientation.LandscapeLeft })
                Assert.IsFalse(Decide(Pose(FoldableContentRuns.LeftToRight, FoldableHalfOpenRotation.FollowDevice),
                    true, true, fixedOrientation != ScreenOrientation.Portrait, false, player: fixedOrientation).hold);
        }

        [Test]
        public void Decide_HoldsAxisNotOrientation_FollowsFlip()
        {
            // An axis, not one orientation: both orientations of the axis stay allowed (180° turn upright).
            var wide = Decide(Pose(FoldableContentRuns.LeftToRight), true, true, true, false);
            Assert.AreEqual(false, wide.tallAxis);
            var tall = Decide(Pose(FoldableContentRuns.TopToDown), true, true, true, true);
            Assert.AreEqual(true, tall.tallAxis);
            // The session swap turns the axis, never the hold off.
            var swapped = Decide(Pose(FoldableContentRuns.LeftToRight), true, true, true, false, inverted: true);
            Assert.IsTrue(swapped.hold);
            Assert.AreEqual(true, swapped.tallAxis);
        }

        [Test]
        public void AxisMappingNeedsSwap_OnlyWhenTheAxisGaveTheOtherShape()
        {
            // Wide wanted, landscape axis held, screen still tall: the device names its axes the other way.
            Assert.IsTrue(FoldableOrientationHold.AxisMappingNeedsSwap(true, true, 1848, 2448));
            Assert.IsTrue(FoldableOrientationHold.AxisMappingNeedsSwap(false, false, 2448, 1848));
            // Got the wanted shape.
            Assert.IsFalse(FoldableOrientationHold.AxisMappingNeedsSwap(true, true, 2448, 1848));
            Assert.IsFalse(FoldableOrientationHold.AxisMappingNeedsSwap(false, false, 1848, 2448));
            // The held axis's own aspect (an already swapped mapping): no second swap.
            Assert.IsFalse(FoldableOrientationHold.AxisMappingNeedsSwap(true, false, 1848, 2448));
            // No answer from a square or unknown size.
            Assert.IsFalse(FoldableOrientationHold.AxisMappingNeedsSwap(true, true, 2000, 2000));
            Assert.IsFalse(FoldableOrientationHold.AxisMappingNeedsSwap(true, true, 0, 0));
        }
        /// <summary>
        /// Steps the shape check frame by frame as the Android wiring does: "keep" when the wanted shape
        /// shows, "swap" when the check concludes a swap, null when it never concludes.
        /// </summary>
        static string RunCheck(bool wantedLandscape, bool heldLandscapeAxis, float secondsPerFrame,
            params (int width, int height, int frames)[] screens)
        {
            int lastWidth = -1, lastHeight = -1, stable = 0;
            float elapsed = 0f;
            foreach (var screen in screens)
            for (int i = 0; i < screen.frames; i++)
            {
                elapsed += secondsPerFrame;
                if (screen.width == lastWidth && screen.height == lastHeight)
                    stable++;
                else
                {
                    lastWidth = screen.width;
                    lastHeight = screen.height;
                    stable = 0;
                }
                if (FoldableOrientationHold.ShapeIsWanted(wantedLandscape, screen.width, screen.height))
                    return "keep";
                if (FoldableOrientationHold.ShapeCheckConcludesSwap(wantedLandscape, heldLandscapeAxis,
                        screen.width, screen.height, stable, elapsed, true))
                    return "swap";
            }
            return null;
        }

        [Test]
        public void ShapeCheck_DisplaySwitchMidCheck_NoSwap()
        {
            // Closed → half open: the cover, then the inner display still tall, then the held wide shape.
            Assert.AreEqual("keep", RunCheck(true, true, 0.05f,
                (1080, 2316, 8), (1848, 2448, 6), (2448, 1848, 1)));
            // Sizes keep changing past the timeout: never settled, never a swap.
            Assert.IsNull(RunCheck(true, true, 0.05f,
                (1080, 2316, 8), (1848, 2448, 8), (1080, 2316, 8), (1848, 2448, 8), (1080, 2316, 8)));
        }

        [Test]
        public void ShapeCheck_WantedShapeAppearsLate_NoSwap()
        {
            // Tall and stable for 25 frames, but the timeout has not passed: the late wide screen keeps.
            Assert.AreEqual("keep", RunCheck(true, true, 0.05f, (1848, 2448, 25), (2448, 1848, 1)));
            // Past the timeout but not yet stable for long enough.
            Assert.AreEqual("keep", RunCheck(true, true, 0.5f, (1848, 2448, 5), (2448, 1848, 1)));
        }

        [Test]
        public void ShapeCheck_WrongShapeStablePastTimeout_SwapsOnce()
        {
            Assert.AreEqual("swap", RunCheck(true, true, 0.05f, (1848, 2448, 40)));
            // After the swap the held axis is the screen's aspect: the same screen never swaps again.
            Assert.IsNull(RunCheck(true, false, 0.05f, (1848, 2448, 40)));
            // Not half open any more: no swap.
            Assert.IsFalse(FoldableOrientationHold.ShapeCheckConcludesSwap(true, true, 1848, 2448,
                FoldableOrientationHold.ShapeStableFrames, FoldableOrientationHold.ShapeCheckSeconds, false));
            // The settled wrong shape after a swap (OS ignores the request) still reads as settled wrong.
            Assert.IsTrue(FoldableOrientationHold.ShapeSettledWrong(true, 1848, 2448,
                FoldableOrientationHold.ShapeStableFrames, FoldableOrientationHold.ShapeCheckSeconds, true));
        }
    }
}
