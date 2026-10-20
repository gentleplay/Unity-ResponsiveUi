using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using FDPlus.Foldable;
using FDPlus.Foldable.Editor;
using FDPlus.ResponsiveUI.Shared.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FDPlus.Foldable.Tests
{
    public class FoldableLayoutTests
    {
        [Test]
        public void ResolvePosture_Flat_IsFlat()
        {
            var state = new FoldableLayoutState { state = FoldableState.Flat, orientation = FoldableOrientation.Horizontal };
            Assert.AreEqual(FoldablePosture.Flat, state.ResolvePosture());
        }

        [Test]
        public void ContentEquals_IgnoresOnlyNeedsUpdate()
        {
            var first = FoldableLayoutState.CreateSimulated(
                FoldableState.HalfOpened,
                FoldableOrientation.Horizontal,
                1080, 2000, 0.5f, 20);
            FoldableLayoutState second = first.CreateSnapshot();

            second.needsUpdate = !first.needsUpdate;
            Assert.IsTrue(first.ContentEquals(second));

            second.currentMetrics = new RectInt(0, 0, 1079, 2000);
            Assert.IsFalse(first.ContentEquals(second));
        }

        [Test]
        public void ResolvePosture_HalfOpenedHorizontal_IsTabletop()
        {
            var state = new FoldableLayoutState
            {
                state = FoldableState.HalfOpened,
                orientation = FoldableOrientation.Horizontal
            };
            Assert.AreEqual(FoldablePosture.Tabletop, state.ResolvePosture());
        }

        [Test]
        public void ResolvePosture_HalfOpenedVertical_IsBook()
        {
            var state = new FoldableLayoutState
            {
                state = FoldableState.HalfOpened,
                orientation = FoldableOrientation.Vertical
            };
            Assert.AreEqual(FoldablePosture.Book, state.ResolvePosture());
        }

        [Test]
        public void ResolvePosture_FlatSeparatingVertical_IsBook()
        {
            var state = new FoldableLayoutState
            {
                state = FoldableState.Flat,
                orientation = FoldableOrientation.Vertical,
                isSeparating = true
            };
            Assert.AreEqual(FoldablePosture.Book, state.ResolvePosture());
        }

        [Test]
        public void ResolvePosture_FlatSeparatingHorizontal_IsDualPane()
        {
            var state = new FoldableLayoutState
            {
                state = FoldableState.Flat,
                orientation = FoldableOrientation.Horizontal,
                isSeparating = true
            };
            Assert.AreEqual(FoldablePosture.DualPane, state.ResolvePosture());
        }

        [Test]
        public void CreateSimulated_Tabletop_HingeNearMid()
        {
            var layout = FoldableLayoutState.CreateSimulated(
                FoldableState.HalfOpened,
                FoldableOrientation.Horizontal,
                1080, 2000, 0.5f, 20);

            Assert.AreEqual(FoldablePosture.Tabletop, layout.ResolvePosture());
            Assert.AreEqual(1080, layout.foldBounds.width);
            Assert.AreEqual(20, layout.foldBounds.height);
            Assert.GreaterOrEqual(layout.foldBounds.yMin, 900);
            Assert.LessOrEqual(layout.foldBounds.yMax, 1100);
        }

        [Test]
        public void CreateSimulated_Book_HingeNearMid()
        {
            var layout = FoldableLayoutState.CreateSimulated(
                FoldableState.HalfOpened,
                FoldableOrientation.Vertical,
                2000, 1080, 0.5f, 20);

            Assert.AreEqual(FoldablePosture.Book, layout.ResolvePosture());
            Assert.AreEqual(20, layout.foldBounds.width);
            Assert.AreEqual(1080, layout.foldBounds.height);
            Assert.GreaterOrEqual(layout.foldBounds.xMin, 900);
            Assert.LessOrEqual(layout.foldBounds.xMax, 1100);
        }

        [Test]
        public void DefaultLandscape_FoldBookWide_TabletopTall_Flip_False()
        {
            // Fold: Book (vertical) is the wide flat screen, Tabletop (horizontal) the tall one.
            Assert.IsFalse(FoldableProductStateResolver.DefaultLandscapeForHalfOpen(
                FoldableDeviceKind.FoldablePhone, FoldableOrientation.Horizontal));
            Assert.IsTrue(FoldableProductStateResolver.DefaultLandscapeForHalfOpen(
                FoldableDeviceKind.FoldablePhone, FoldableOrientation.Vertical));
            Assert.IsFalse(FoldableProductStateResolver.DefaultLandscapeForHalfOpen(
                FoldableDeviceKind.FlipablePhone, FoldableOrientation.Horizontal));
        }

        [Test]
        public void ResolveDefaultLandscape_ProjectLock_WinsOverFlex()
        {
            Assert.IsTrue(FoldableProductStateResolver.ResolveDefaultLandscape(
                FoldableDeviceKind.FlipablePhone,
                FoldableOrientation.Horizontal,
                FoldableProductState.HalfOpened,
                projectLockedLandscape: true));

            Assert.IsFalse(FoldableProductStateResolver.ResolveDefaultLandscape(
                FoldableDeviceKind.FoldablePhone,
                FoldableOrientation.Horizontal,
                FoldableProductState.HalfOpened,
                projectLockedLandscape: false));

            Assert.IsTrue(FoldableProductStateResolver.ResolveDefaultLandscape(
                FoldableDeviceKind.FoldablePhone,
                FoldableOrientation.Horizontal,
                FoldableProductState.Opened,
                projectLockedLandscape: true));
        }

        // Runtime Foldable Simulator, default cards: a Fold Half Opened holds the one-axis lock's
        // pose and follows rotation with both axes. Flip, Dual and other postures rotate freely.
        [Test]
        public void HeldHalfOpenScreenLandscape_FoldHalfOpen_HoldsScreen_OthersFree()
        {
            var fold = FoldableDeviceKind.FoldablePhone;
            var half = FoldableProductState.HalfOpened;
            var previous = FoldableSettings.Rules;
            try
            {
                FoldableSettings.Rules = new FoldableHalfOpenRules();
                foreach (bool current in new[] { true, false })
                {
                    Assert.IsNull(FoldableProductStateResolver.HeldHalfOpenScreenLandscape(
                        fold, half, null, current), "both axes, entered wide " + current);
                    Assert.AreEqual(false, FoldableProductStateResolver.HeldHalfOpenScreenLandscape(
                        fold, half, true, current), "landscape-only → tall");
                    Assert.AreEqual(true, FoldableProductStateResolver.HeldHalfOpenScreenLandscape(
                        fold, half, false, current), "portrait-only → wide");
                    Assert.IsNull(FoldableProductStateResolver.HeldHalfOpenScreenLandscape(
                        fold, FoldableProductState.Opened, null, current));
                    Assert.IsNull(FoldableProductStateResolver.HeldHalfOpenScreenLandscape(
                        FoldableDeviceKind.FlipablePhone, half, null, current));
                    Assert.IsNull(FoldableProductStateResolver.HeldHalfOpenScreenLandscape(
                        FoldableDeviceKind.DualScreen, half, true, current));
                }
            }
            finally
            {
                FoldableSettings.Rules = previous;
            }
        }

        // A Locked Fold card holds the screen it entered Half Opened with, both axes allowed; the
        // Fold Cover card is its own.
        [Test]
        public void HeldHalfOpenScreenLandscape_LockedCard_HoldsEntry()
        {
            var fold = FoldableDeviceKind.FoldablePhone;
            var half = FoldableProductState.HalfOpened;
            var previous = FoldableSettings.Rules;
            try
            {
                var rules = new FoldableHalfOpenRules();
                rules.fold.whileRotating = FoldableHalfOpenRotation.Locked;
                FoldableSettings.Rules = rules;
                foreach (bool entry in new[] { true, false })
                {
                    Assert.AreEqual(entry, FoldableProductStateResolver.HeldHalfOpenScreenLandscape(
                        fold, half, null, entry), "entered wide " + entry);
                    Assert.IsNull(FoldableProductStateResolver.HeldHalfOpenScreenLandscape(
                        fold, half, null, entry, foldCover: true), "Fold Cover card is default");
                }
            }
            finally
            {
                FoldableSettings.Rules = previous;
            }
        }

        // A Flip with its default card is never held: the platform rotates it under any lock.
        [Test]
        public void Flip_DefaultCard_NeverHeld()
        {
            var flip = FoldableDeviceKind.FlipablePhone;
            var previous = FoldableSettings.Rules;
            try
            {
                FoldableSettings.Rules = new FoldableHalfOpenRules();
                foreach (bool entry in new[] { true, false })
                {
                    foreach (bool? locked in new bool?[] { null, true, false })
                    {
                        Assert.IsNull(FoldableProductStateResolver.HeldHalfOpenScreenLandscape(
                            flip, FoldableProductState.HalfOpened, locked, entry));
                    }
                }
                WithPlayerAxes(portrait: true, landscape: true, () =>
                    Assert.IsNull(FoldableEditorSession.AndroidHalfOpenHeldScreenLandscape(
                        flip, FoldableProductState.HalfOpened, false)));
            }
            finally
            {
                FoldableSettings.Rules = previous;
            }
        }

        // A Flip card running content top to down holds the tall screen under any lock.
        [Test]
        public void Flip_TopToDownCard_HoldsTall()
        {
            var flip = FoldableDeviceKind.FlipablePhone;
            var previous = FoldableSettings.Rules;
            try
            {
                var rules = new FoldableHalfOpenRules();
                rules.flip.contentRuns = FoldableContentRuns.TopToDown;
                FoldableSettings.Rules = rules;
                foreach (bool entry in new[] { true, false })
                {
                    foreach (bool? locked in new bool?[] { null, true, false })
                    {
                        Assert.AreEqual(false, FoldableProductStateResolver.HeldHalfOpenScreenLandscape(
                            flip, FoldableProductState.HalfOpened, locked, entry));
                    }
                }
                Assert.IsNull(FoldableProductStateResolver.HeldHalfOpenScreenLandscape(
                    flip, FoldableProductState.Opened, null, true));
                WithPlayerAxes(portrait: true, landscape: true, () =>
                    Assert.AreEqual(false, FoldableEditorSession.AndroidHalfOpenHeldScreenLandscape(
                        flip, FoldableProductState.HalfOpened, false)));
            }
            finally
            {
                FoldableSettings.Rules = previous;
            }
        }

        // The simulator's orientation toolbar names the slot: Fold Half Opened wide = Portrait.
        [Test]
        public void PoseLandscape_FoldHalfOpenNamesPosture_OthersScreenShape()
        {
            var half = FoldableProductState.HalfOpened;
            Assert.IsFalse(FoldableProductStateResolver.PoseLandscape(FoldableDeviceKind.FoldablePhone, half, true));
            Assert.IsTrue(FoldableProductStateResolver.PoseLandscape(FoldableDeviceKind.FoldablePhone, half, false));
            Assert.IsTrue(FoldableProductStateResolver.PoseLandscape(
                FoldableDeviceKind.FoldablePhone, FoldableProductState.Opened, true));
            Assert.IsTrue(FoldableProductStateResolver.PoseLandscape(FoldableDeviceKind.FlipablePhone, half, true));
            Assert.IsTrue(FoldableProductStateResolver.PoseLandscape(FoldableDeviceKind.DualScreen, half, true));
        }

        // Runtime Foldable Simulator under a one-axis lock: Fold Half Opened takes the lock's pose
        // (landscape-only = tall screen, portrait-only = wide); other devices keep the lock's shape.
        [Test]
        public void LockedScreenLandscape_FoldHalfOpen_LockPose_OthersKeepLock()
        {
            var fold = FoldableDeviceKind.FoldablePhone;
            var half = FoldableProductState.HalfOpened;
            Assert.AreEqual(false, FoldableProductStateResolver.LockedScreenLandscape(fold, half, true));
            Assert.AreEqual(true, FoldableProductStateResolver.LockedScreenLandscape(fold, half, false));
            Assert.IsNull(FoldableProductStateResolver.LockedScreenLandscape(fold, half, null));
            Assert.AreEqual(true, FoldableProductStateResolver.LockedScreenLandscape(
                fold, FoldableProductState.Opened, true));
            Assert.AreEqual(true, FoldableProductStateResolver.LockedScreenLandscape(
                FoldableDeviceKind.FlipablePhone, half, true));
            Assert.AreEqual(false, FoldableProductStateResolver.LockedScreenLandscape(
                FoldableDeviceKind.FlipablePhone, half, false));
            Assert.AreEqual(true, FoldableProductStateResolver.LockedScreenLandscape(
                FoldableDeviceKind.DualScreen, half, true));

            // Landscape-only: tall screen, horizontal fold. Portrait-only: wide, vertical fold.
            foreach (bool lockedLandscape in new[] { true, false })
            {
                bool wide = FoldableProductStateResolver.ResolveDefaultLandscape(
                    fold, FoldableProductStateResolver.HalfOpenHingeForPose(lockedLandscape), half,
                    FoldableProductStateResolver.LockedScreenLandscape(fold, half, lockedLandscape));
                int w = 1848, h = 2448;
                FoldableProductStateResolver.ApplyOrientedMetrics(wide, ref w, ref h);
                var layout = FoldableProductStateResolver.CreateFromProductState(
                    fold, half, w, h, halfOpenHinge: FoldableProductStateResolver.HalfOpenHingeForPose(lockedLandscape));
                Assert.AreEqual(!lockedLandscape, w > h, "screen shape, landscape lock " + lockedLandscape);
                Assert.AreEqual(lockedLandscape ? FoldableOrientation.Horizontal : FoldableOrientation.Vertical,
                    layout.orientation, "fold, landscape lock " + lockedLandscape);
            }
        }

        [Test]
        public void ResolveDefaultLandscape_Unlocked_UsesFlexForHalfOpen()
        {
            // Fold Tabletop (horizontal) is the tall screen.
            Assert.IsFalse(FoldableProductStateResolver.ResolveDefaultLandscape(
                FoldableDeviceKind.FoldablePhone,
                FoldableOrientation.Horizontal,
                FoldableProductState.HalfOpened,
                projectLockedLandscape: null));
            Assert.IsTrue(FoldableProductStateResolver.ResolveDefaultLandscape(
                FoldableDeviceKind.FoldablePhone,
                FoldableOrientation.Vertical,
                FoldableProductState.HalfOpened,
                projectLockedLandscape: null));

            Assert.IsFalse(FoldableProductStateResolver.ResolveDefaultLandscape(
                FoldableDeviceKind.FoldablePhone,
                FoldableOrientation.Horizontal,
                FoldableProductState.Opened,
                projectLockedLandscape: null));
        }

        [Test]
        public void TabletopSplit_ControlsBelowFold_CameraAbove()
        {
            var layout = FoldableLayoutState.CreateSimulated(
                FoldableState.HalfOpened,
                FoldableOrientation.Horizontal,
                1080, 2000, 0.5f, 20);

            float lowerMax = layout.foldBounds.yMin / (float)layout.currentMetrics.height;
            float upperY = layout.foldBounds.yMax / (float)layout.currentMetrics.height;

            // Bottom pane (controls) ends at/near hinge; camera starts at/near hinge.
            Assert.LessOrEqual(lowerMax, upperY + 0.05f);
            Assert.Greater(upperY, 0.4f);
        }

        [Test]
        public void CreateSimulated_ClampsInvalidMetrics()
        {
            var layout = FoldableLayoutState.CreateSimulated(
                FoldableState.Undefined,
                FoldableOrientation.Undefined,
                0, -5, 2f, 9999);

            Assert.AreEqual(FoldableState.Flat, layout.state);
            Assert.AreEqual(FoldableOrientation.Horizontal, layout.orientation);
            Assert.AreEqual(1, layout.currentMetrics.width);
            Assert.AreEqual(1, layout.currentMetrics.height);
        }

        [Test]
        public void CopyExact_CopiesAllFields()
        {
            var source = FoldableLayoutState.CreateSimulated(
                FoldableState.HalfOpened, FoldableOrientation.Vertical, 1000, 800, 0.4f, 10, true);
            source.deviceKind = FoldableDeviceKind.FoldablePhone;
            source.isCoverScreen = false;
            var dest = new FoldableLayoutState();
            dest.CopyExact(source);

            Assert.AreEqual(source.state, dest.state);
            Assert.AreEqual(source.orientation, dest.orientation);
            Assert.AreEqual(source.isSeparating, dest.isSeparating);
            Assert.AreEqual(source.foldBounds, dest.foldBounds);
            Assert.AreEqual(source.currentMetrics, dest.currentMetrics);
            Assert.AreEqual(source.deviceKind, dest.deviceKind);
            Assert.IsTrue(dest.needsUpdate);
        }

        [Test]
        public void CopyExact_ClearsHistoricalFoldState()
        {
            var destination = FoldableLayoutState.CreateSimulated(
                FoldableState.HalfOpened,
                FoldableOrientation.Vertical,
                1000, 800, 0.4f, 10, true);
            destination.deviceKind = FoldableDeviceKind.FoldablePhone;

            destination.CopyExact(new FoldableLayoutState());

            Assert.AreEqual(FoldableState.Undefined, destination.state);
            Assert.AreEqual(FoldableOrientation.Undefined, destination.orientation);
            Assert.AreEqual(FoldableDeviceKind.Unknown, destination.deviceKind);
            Assert.AreEqual(default(RectInt), destination.foldBounds);
        }

        [Test]
        public void MergeNativeSnapshot_UndefinedNative_KeepsHalfOpenedHingeAndKind()
        {
            var live = FoldableLayoutState.CreateSimulated(
                FoldableState.HalfOpened, FoldableOrientation.Horizontal, 1812, 2176, 0.5f, 24);
            live.deviceKind = FoldableDeviceKind.FoldablePhone;
            live.productState = FoldableProductState.HalfOpened;
            RectInt hinge = live.foldBounds;

            var native = new FoldableLayoutState
            {
                state = FoldableState.Undefined,
                orientation = FoldableOrientation.Undefined,
                productState = FoldableProductState.Undefined,
                deviceKind = FoldableDeviceKind.Unknown,
                currentMetrics = new RectInt(0, 0, 1812, 2176),
                maxMetrics = new RectInt(0, 0, 1812, 2176),
                foldBounds = new RectInt(0, 0, 0, 0)
            };

            live.MergeNativeSnapshot(native);
            Assert.AreEqual(FoldableState.Undefined, live.state);

            FoldableProductStateResolver.Enrich(
                live,
                FoldableDeviceKind.FoldablePhone,
                FoldableProductState.HalfOpened,
                FoldableOrientation.Horizontal);

            Assert.AreEqual(FoldableState.HalfOpened, live.state);
            Assert.AreEqual(FoldableOrientation.Horizontal, live.orientation);
            Assert.AreEqual(FoldableDeviceKind.FoldablePhone, live.deviceKind);
            Assert.AreEqual(hinge, live.foldBounds);
            Assert.AreEqual(FoldableProductState.HalfOpened, live.productState);
            Assert.IsTrue(live.isSeparating);
            Assert.AreEqual(FoldablePosture.Tabletop, live.ResolvePosture());
        }

        [Test]
        public void MergeNativeSnapshot_FlatAfterHalfOpened_ReplacesHingeWithFlat()
        {
            var live = FoldableLayoutState.CreateSimulated(
                FoldableState.HalfOpened, FoldableOrientation.Horizontal, 1812, 2176, 0.5f, 24);
            live.deviceKind = FoldableDeviceKind.FoldablePhone;
            live.productState = FoldableProductState.HalfOpened;

            var native = new FoldableLayoutState
            {
                state = FoldableState.Flat,
                orientation = FoldableOrientation.Vertical,
                productState = FoldableProductState.Undefined,
                deviceKind = FoldableDeviceKind.Unknown,
                currentMetrics = new RectInt(0, 0, 1812, 2176),
                maxMetrics = new RectInt(0, 0, 1812, 2176),
                foldBounds = new RectInt(0, 0, 1812, 24)
            };

            live.MergeNativeSnapshot(native);
            FoldableProductStateResolver.Enrich(
                live,
                FoldableDeviceKind.FoldablePhone,
                FoldableProductState.HalfOpened);

            Assert.AreEqual(FoldableState.Flat, live.state);
            Assert.AreEqual(FoldableProductState.Opened, live.productState);
            Assert.AreEqual(FoldableDeviceKind.FoldablePhone, live.deviceKind);
            Assert.AreEqual(FoldablePosture.Flat, live.ResolvePosture());
        }

        [Test]
        public void MergeNativeSnapshot_UndefinedAfterExpand_ClearsStaleFlat()
        {
            // Real Fold: Expand→Normal drops FoldingFeature. Keeping Flat would map to Pad.
            var live = FoldableLayoutState.CreateSimulated(
                FoldableState.Flat, FoldableOrientation.Vertical, 1812, 2176, 0.5f, 24);
            live.deviceKind = FoldableDeviceKind.FoldablePhone;
            live.productState = FoldableProductState.Opened;
            live.peakMaxMetrics = new RectInt(0, 0, 1812, 2176);

            var native = new FoldableLayoutState
            {
                state = FoldableState.Undefined,
                orientation = FoldableOrientation.Undefined,
                productState = FoldableProductState.Undefined,
                deviceKind = FoldableDeviceKind.Unknown,
                currentMetrics = new RectInt(0, 0, 832, 2268),
                maxMetrics = new RectInt(0, 0, 832, 2268),
                peakMaxMetrics = new RectInt(0, 0, 0, 0)
            };

            live.MergeNativeSnapshot(native);

            Assert.AreEqual(FoldableState.Undefined, live.state);
            Assert.AreEqual(new RectInt(0, 0, 1812, 2176), live.peakMaxMetrics);
            Assert.IsFalse(live.isSeparating);
        }

        [Test]
        public void NativeDetect_Fold_CoverAfterExpand_ViaMerge_IsClosedNormal()
        {
            // Galaxy Z Fold: Unfolded (Expand/Pad) → fold closed (Normal/Phone).
            // Jetpack has no CLOSED enum; cover WxH + Device Kind / peak must win.
            var live = FoldableLayoutState.CreateSimulated(
                FoldableState.Flat, FoldableOrientation.Vertical, 1812, 2176, 0.5f, 24);
            live.deviceKind = FoldableDeviceKind.FoldablePhone;
            live.productState = FoldableProductState.Opened;
            live.peakMaxMetrics = new RectInt(0, 0, 1812, 2176);

            var native = new FoldableLayoutState
            {
                state = FoldableState.Undefined,
                productState = FoldableProductState.Undefined,
                currentMetrics = new RectInt(0, 0, 832, 2268),
                maxMetrics = new RectInt(0, 0, 832, 2268),
                peakMaxMetrics = new RectInt(0, 0, 0, 0)
            };

            live.MergeNativeSnapshot(native);
            FoldableProductStateResolver.Enrich(
                live,
                FoldableDeviceKind.FoldablePhone,
                FoldableProductState.Opened);

            Assert.AreEqual(FoldableState.Undefined, live.state);
            Assert.IsTrue(live.isCoverScreen);
            Assert.AreEqual(FoldableProductState.Closed, live.productState);
            Assert.AreEqual("Normal", live.GetProductStateDisplayName());
        }

        [Test]
        public void NativeDetect_Fold_CoverAfterExpand_NoPeak_ViaKindOpenSize_IsClosed()
        {
            // Activity recreate can wipe Java session peak; Device Kind open size must still detect.
            var live = FoldableLayoutState.CreateSimulated(
                FoldableState.Flat, FoldableOrientation.Vertical, 1812, 2176, 0.5f, 24);
            live.deviceKind = FoldableDeviceKind.FoldablePhone;
            live.productState = FoldableProductState.Opened;
            live.peakMaxMetrics = new RectInt(0, 0, 0, 0);

            var native = new FoldableLayoutState
            {
                state = FoldableState.Undefined,
                productState = FoldableProductState.Undefined,
                currentMetrics = new RectInt(0, 0, 1080, 2520),
                maxMetrics = new RectInt(0, 0, 1080, 2520),
                peakMaxMetrics = new RectInt(0, 0, 0, 0)
            };

            live.MergeNativeSnapshot(native);
            FoldableProductStateResolver.Enrich(
                live,
                FoldableDeviceKind.FoldablePhone,
                FoldableProductState.Opened);

            Assert.IsTrue(live.isCoverScreen);
            Assert.AreEqual(FoldableProductState.Closed, live.productState);
        }

        [Test]
        public void ApplyCoverFlags_StaleOpenedWithCoverSize_BecomesClosed()
        {
            // After Merge clears Flat, product may still be Opened until Enrich.
            var layout = new FoldableLayoutState
            {
                currentMetrics = new RectInt(0, 0, 832, 2268),
                maxMetrics = new RectInt(0, 0, 832, 2268),
                peakMaxMetrics = new RectInt(0, 0, 1812, 2176),
                state = FoldableState.Undefined,
                deviceKind = FoldableDeviceKind.FoldablePhone,
                productState = FoldableProductState.Opened
            };
            FoldableProductStateResolver.Enrich(layout, FoldableDeviceKind.FoldablePhone);
            Assert.IsTrue(layout.isCoverScreen);
            Assert.AreEqual(FoldableProductState.Closed, layout.productState);
        }

        [Test]
        public void ApplyCoverFlags_LiveFlatWithCoverSizedWindow_StaysExpand()
        {
            // Live Jetpack FLAT must not be size-probed into Closed (freeform / lag edge cases).
            var layout = new FoldableLayoutState
            {
                currentMetrics = new RectInt(0, 0, 832, 2268),
                maxMetrics = new RectInt(0, 0, 832, 2268),
                peakMaxMetrics = new RectInt(0, 0, 1812, 2176),
                state = FoldableState.Flat,
                deviceKind = FoldableDeviceKind.FoldablePhone,
                productState = FoldableProductState.Undefined
            };
            FoldableProductStateResolver.Enrich(layout, FoldableDeviceKind.FoldablePhone);
            Assert.IsFalse(layout.isCoverScreen);
            Assert.AreEqual(FoldableProductState.Opened, layout.productState);
        }

        [Test]
        public void ApplyCoverFlags_StaleHalfOpenedWithCoverSize_BecomesClosed()
        {
            var layout = new FoldableLayoutState
            {
                currentMetrics = new RectInt(0, 0, 832, 2268),
                maxMetrics = new RectInt(0, 0, 832, 2268),
                peakMaxMetrics = new RectInt(0, 0, 1812, 2176),
                state = FoldableState.Undefined,
                deviceKind = FoldableDeviceKind.FoldablePhone,
                productState = FoldableProductState.HalfOpened
            };
            FoldableProductStateResolver.Enrich(layout, FoldableDeviceKind.FoldablePhone);
            Assert.IsTrue(layout.isCoverScreen);
            Assert.AreEqual(FoldableProductState.Closed, layout.productState);
        }

        [Test]
        public void ApplyCoverFlags_LiveHalfOpened_DoesNotSizeProbeClosed()
        {
            // Live Flex must stay HalfOpened even if kind open-size would match a tall cover.
            var layout = new FoldableLayoutState
            {
                currentMetrics = new RectInt(0, 0, 1812, 2176),
                maxMetrics = new RectInt(0, 0, 1812, 2176),
                state = FoldableState.HalfOpened,
                orientation = FoldableOrientation.Horizontal,
                deviceKind = FoldableDeviceKind.FoldablePhone,
                productState = FoldableProductState.Undefined
            };
            FoldableProductStateResolver.Enrich(layout, FoldableDeviceKind.FoldablePhone);
            Assert.IsFalse(layout.isCoverScreen);
            Assert.AreEqual(FoldableProductState.HalfOpened, layout.productState);
        }

        [Test]
        public void ProductState_Fold_CoverIsNormal()
        {
            var layout = new FoldableLayoutState
            {
                deviceKind = FoldableDeviceKind.FoldablePhone,
                isCoverScreen = true,
                state = FoldableState.Flat
            };
            Assert.AreEqual(FoldableProductState.Closed, layout.ResolveProductState());
            Assert.AreEqual("Normal", layout.GetProductStateDisplayName());
        }

        [Test]
        public void ProductState_Flip_CoverIsClosed()
        {
            var layout = new FoldableLayoutState
            {
                deviceKind = FoldableDeviceKind.FlipablePhone,
                isCoverScreen = true,
                state = FoldableState.Flat
            };
            Assert.AreEqual("Closed", layout.GetProductStateDisplayName());
        }

        [Test]
        public void ProductState_Fold_FlatInnerIsExpand()
        {
            var layout = FoldableProductStateResolver.CreateFromProductState(
                FoldableDeviceKind.FoldablePhone,
                FoldableProductState.Opened,
                1812, 2176);
            Assert.AreEqual(FoldableProductState.Opened, layout.productState);
            Assert.AreEqual("Expand", layout.GetProductStateDisplayName());
            Assert.AreEqual(FoldablePosture.Flat, layout.ResolvePosture());
        }

        [Test]
        public void ProductState_Flip_HalfOpenedIsHalfOpened()
        {
            var layout = FoldableProductStateResolver.CreateFromProductState(
                FoldableDeviceKind.FlipablePhone,
                FoldableProductState.HalfOpened,
                1080, 2640);
            Assert.AreEqual("Half Opened", layout.GetProductStateDisplayName());
            Assert.AreEqual(FoldablePosture.Tabletop, layout.ResolvePosture());
        }

        [Test]
        public void Flip_HalfOpenedTabletop_Portrait_SplitsHeight()
        {
            var layout = FoldableProductStateResolver.CreateFromProductState(
                FoldableDeviceKind.FlipablePhone,
                FoldableProductState.HalfOpened,
                1080, 2640,
                halfOpenHinge: FoldableOrientation.Horizontal);
            Assert.AreEqual(FoldableOrientation.Horizontal, layout.orientation);
            Assert.AreEqual(FoldablePosture.Tabletop, layout.ResolvePosture());
            Assert.AreEqual(1080, layout.foldBounds.width);
            Assert.Greater(layout.foldBounds.height, 0);
            Assert.Less(layout.foldBounds.height, layout.foldBounds.width);
        }

        [Test]
        public void Flip_HalfOpenedTabletop_Landscape_SplitsWidth()
        {
            // Clamshell hinge is horizontal in portrait; landscape rotation makes it vertical.
            var layout = FoldableProductStateResolver.CreateFromProductState(
                FoldableDeviceKind.FlipablePhone,
                FoldableProductState.HalfOpened,
                2640, 1080,
                halfOpenHinge: FoldableOrientation.Horizontal);
            Assert.AreEqual(FoldableOrientation.Vertical, layout.orientation);
            Assert.AreEqual(FoldablePosture.Book, layout.ResolvePosture());
            Assert.AreEqual(1080, layout.foldBounds.height);
            Assert.Greater(layout.foldBounds.width, 0);
            Assert.Less(layout.foldBounds.width, layout.foldBounds.height);
        }

        [Test]
        public void Fold_HalfOpened_HingeFollowsScreen()
        {
            // A wide screen never carries a horizontal fold: wide = Portrait book (top to bottom),
            // tall = Landscape tabletop (left to right), whatever hinge was authored.
            var wide = FoldableProductStateResolver.CreateFromProductState(
                FoldableDeviceKind.FoldablePhone,
                FoldableProductState.HalfOpened,
                2448, 1848,
                halfOpenHinge: FoldableOrientation.Horizontal);
            Assert.AreEqual(FoldableOrientation.Vertical, wide.orientation);
            Assert.AreEqual(FoldablePosture.Book, wide.ResolvePosture());
            Assert.AreEqual(1848, wide.foldBounds.height);

            var tall = FoldableProductStateResolver.CreateFromProductState(
                FoldableDeviceKind.FoldablePhone,
                FoldableProductState.HalfOpened,
                1848, 2448,
                halfOpenHinge: FoldableOrientation.Vertical);
            Assert.AreEqual(FoldableOrientation.Horizontal, tall.orientation);
            Assert.AreEqual(FoldablePosture.Tabletop, tall.ResolvePosture());
            Assert.AreEqual(1848, tall.foldBounds.width);
        }

        [Test]
        public void ToScreenSpaceHalfOpenHinge_RotatesFlipLandscape_FoldFollowsScreen()
        {
            Assert.AreEqual(
                FoldableOrientation.Horizontal,
                FoldableProductStateResolver.ToScreenSpaceHalfOpenHinge(
                    FoldableDeviceKind.FlipablePhone,
                    FoldableProductState.HalfOpened,
                    FoldableOrientation.Horizontal,
                    1080, 2640));
            Assert.AreEqual(
                FoldableOrientation.Vertical,
                FoldableProductStateResolver.ToScreenSpaceHalfOpenHinge(
                    FoldableDeviceKind.FlipablePhone,
                    FoldableProductState.HalfOpened,
                    FoldableOrientation.Horizontal,
                    2640, 1080));
            // Fold7 2184×1968 wide → vertical, 1968×2184 tall → horizontal, either authored hinge.
            Assert.AreEqual(
                FoldableOrientation.Vertical,
                FoldableProductStateResolver.ToScreenSpaceHalfOpenHinge(
                    FoldableDeviceKind.FoldablePhone,
                    FoldableProductState.HalfOpened,
                    FoldableOrientation.Horizontal,
                    2184, 1968));
            Assert.AreEqual(
                FoldableOrientation.Horizontal,
                FoldableProductStateResolver.ToScreenSpaceHalfOpenHinge(
                    FoldableDeviceKind.FoldablePhone,
                    FoldableProductState.HalfOpened,
                    FoldableOrientation.Vertical,
                    1968, 2184));
            // Expand keeps the authored axis.
            Assert.AreEqual(
                FoldableOrientation.Horizontal,
                FoldableProductStateResolver.ToScreenSpaceHalfOpenHinge(
                    FoldableDeviceKind.FoldablePhone,
                    FoldableProductState.Opened,
                    FoldableOrientation.Horizontal,
                    2184, 1968));
        }

        [TestCase(2007, 2853, FoldableOrientation.Horizontal, FoldableOrientation.Horizontal)]
        [TestCase(2007, 2853, FoldableOrientation.Vertical, FoldableOrientation.Horizontal)]
        [TestCase(2853, 2007, FoldableOrientation.Vertical, FoldableOrientation.Vertical)]
        [TestCase(2853, 2007, FoldableOrientation.Horizontal, FoldableOrientation.Vertical)]
        public void ToScreenSpaceHalfOpenHinge_IphoneDuo_TallLaptop_WideBook(
            int width,
            int height,
            FoldableOrientation authored,
            FoldableOrientation expected)
        {
            Assert.AreEqual(
                expected,
                FoldableProductStateResolver.ToScreenSpaceHalfOpenHinge(
                    FoldableDeviceKind.FoldablePhone,
                    FoldableProductState.HalfOpened,
                    authored,
                    width, height,
                    innerLandscapeNative: true));
        }

        [Test]
        public void ToScreenSpaceHalfOpenHinge_LandscapeNativeInner_OpenedKeepsAuthoredAxis()
        {
            Assert.AreEqual(
                FoldableOrientation.Horizontal,
                FoldableProductStateResolver.ToScreenSpaceHalfOpenHinge(
                    FoldableDeviceKind.FoldablePhone,
                    FoldableProductState.Opened,
                    FoldableOrientation.Horizontal,
                    2853, 2007,
                    innerLandscapeNative: true));
        }

        [Test]
        public void HalfOpenFoldAxis_GalaxyFoldAndIphoneDuo_WideBook_TallLaptop()
        {
            // Galaxy Fold and iPhone Duo alike: wide screen = book (vertical), tall = laptop (horizontal).
            Assert.AreEqual(FoldableOrientation.Vertical, HalfOpenAxis(2448, 1848, false));
            Assert.AreEqual(FoldableOrientation.Horizontal, HalfOpenAxis(1848, 2448, false));
            Assert.AreEqual(FoldableOrientation.Horizontal, HalfOpenAxis(2007, 2853, true));
            Assert.AreEqual(FoldableOrientation.Vertical, HalfOpenAxis(2853, 2007, true));
        }

        // (chromeLandscape, projectPortrait, projectLandscape) → render axes.
        // Chrome off the project axis allows only the chrome axis: with both allowed, a chrome
        // turn auto-rotates the render before the half-open hold turns the chrome back.
        [TestCase(false, false, true, true, false, TestName = "LandscapeOnly_PortraitChrome_AllowsOnlyPortrait")]
        [TestCase(true, true, false, false, true, TestName = "PortraitOnly_LandscapeChrome_AllowsOnlyLandscape")]
        [TestCase(true, false, true, false, true, TestName = "LandscapeOnly_LandscapeChrome_KeepsProject")]
        [TestCase(false, true, false, true, false, TestName = "PortraitOnly_PortraitChrome_KeepsProject")]
        [TestCase(true, true, true, true, true, TestName = "AutoRotation_KeepsBothAxes")]
        public void ResolveRenderAxes_ChromeAxisIsAlwaysAllowed(
            bool chromeLandscape,
            bool projectPortrait,
            bool projectLandscape,
            bool expectedPortrait,
            bool expectedLandscape)
        {
            SimulatorDeviceBridge.ResolveRenderAxes(
                chromeLandscape, projectPortrait, projectLandscape,
                out bool portrait, out bool landscape);
            Assert.AreEqual(expectedPortrait, portrait, "portrait");
            Assert.AreEqual(expectedLandscape, landscape, "landscape");
        }

        [Test]
        public void PoseFromChromeRotation_ProfileSwitch_KeepsSessionPose()
        {
            // Stale Galaxy Fold laptop chrome (90°) after switching to the Duo: no pose change.
            Assert.IsNull(FoldableEditorSessionWatcher.PoseFromChromeRotation(
                hasRotation: true,
                rotationDegrees: 90,
                profileChanged: true));
            Assert.IsNull(FoldableEditorSessionWatcher.PoseFromChromeRotation(
                hasRotation: false,
                rotationDegrees: 0,
                profileChanged: false));

            // Same profile: the chrome drives the pose (Duo laptop is portrait chrome = Portrait).
            Assert.AreEqual(false, FoldableEditorSessionWatcher.PoseFromChromeRotation(
                hasRotation: true,
                rotationDegrees: 0,
                profileChanged: false));
            Assert.AreEqual(true, FoldableEditorSessionWatcher.PoseFromChromeRotation(
                hasRotation: true,
                rotationDegrees: -90,
                profileChanged: false));
            Assert.AreEqual(false, FoldableEditorSessionWatcher.PoseFromChromeRotation(
                hasRotation: true,
                rotationDegrees: 180,
                profileChanged: false));
        }

        [Test]
        public void PoseAfterAndroidLock_DisallowedTurn_DoesNotResetFoldChrome()
        {
            // Landscape-only project: a portrait turn of an Android Fold must not become
            // the pose. The lock would otherwise write the simulator back to 90°.
            Assert.IsNull(FoldableEditorSessionWatcher.PoseAfterAndroidLock(
                poseLandscape: false,
                innerLandscapeNative: false,
                lockedLandscape: true));
            Assert.AreEqual(true, FoldableEditorSessionWatcher.PoseAfterAndroidLock(
                poseLandscape: true,
                innerLandscapeNative: false,
                lockedLandscape: true));

            // iPhone Duo keeps a portrait chrome turn (tall laptop) under the same lock.
            Assert.AreEqual(false, FoldableEditorSessionWatcher.PoseAfterAndroidLock(
                poseLandscape: false,
                innerLandscapeNative: true,
                lockedLandscape: true));

            // Both axes: the chrome drives Android too.
            Assert.AreEqual(false, FoldableEditorSessionWatcher.PoseAfterAndroidLock(
                poseLandscape: false,
                innerLandscapeNative: false,
                lockedLandscape: null));
        }

        [Test]
        public void PoseAfterIosHalfOpenLock_LockedSeated_DropsTheChromeTurn()
        {
            // Locked Seated holds its screen: a turn away from it is dropped (the panel turns the
            // chrome back), a turn onto it is kept.
            bool? heldTall = FoldableEditorSession.IosHalfOpenHeldScreenLandscape(
                FoldableProductState.HalfOpened, rotationLocked: true, landscape: false);
            Assert.AreEqual(false, heldTall);
            Assert.IsNull(FoldableEditorSessionWatcher.PoseAfterIosHalfOpenLock(true, heldTall));
            Assert.AreEqual(false, FoldableEditorSessionWatcher.PoseAfterIosHalfOpenLock(false, heldTall));
            Assert.IsNull(FoldableEditorSessionWatcher.PoseAfterIosHalfOpenLock(null, heldTall));

            // Follows hold, or any other posture: nothing is held and the chrome turn is the screen
            // (the behaviour before the Seated rotation rule).
            Assert.IsNull(FoldableEditorSession.IosHalfOpenHeldScreenLandscape(
                FoldableProductState.HalfOpened, rotationLocked: false, landscape: false));
            Assert.IsNull(FoldableEditorSession.IosHalfOpenHeldScreenLandscape(
                FoldableProductState.Opened, rotationLocked: true, landscape: false));
            Assert.IsNull(FoldableEditorSession.IosHalfOpenHeldScreenLandscape(
                FoldableProductState.Closed, rotationLocked: true, landscape: true));
            Assert.AreEqual(true, FoldableEditorSessionWatcher.PoseAfterIosHalfOpenLock(true, null));
            Assert.AreEqual(false, FoldableEditorSessionWatcher.PoseAfterIosHalfOpenLock(false, null));
        }

        [Test]
        public void LandscapeNativeHalfOpenHingeForPose_DuoHingeFollowsPose()
        {
            // Duo: Portrait (tall laptop) → horizontal fold, Landscape (wide book) → vertical.
            Assert.AreEqual(
                FoldableOrientation.Horizontal,
                FoldableEditorSession.LandscapeNativeHalfOpenHingeForPose(
                    FoldableProductState.HalfOpened, false, true, FoldableOrientation.Vertical));
            Assert.AreEqual(
                FoldableOrientation.Vertical,
                FoldableEditorSession.LandscapeNativeHalfOpenHingeForPose(
                    FoldableProductState.HalfOpened, true, true, FoldableOrientation.Horizontal));
            // Galaxy Fold and Duo Expand keep the authored hinge.
            Assert.AreEqual(
                FoldableOrientation.Vertical,
                FoldableEditorSession.LandscapeNativeHalfOpenHingeForPose(
                    FoldableProductState.HalfOpened, true, false, FoldableOrientation.Vertical));
            Assert.AreEqual(
                FoldableOrientation.Vertical,
                FoldableEditorSession.LandscapeNativeHalfOpenHingeForPose(
                    FoldableProductState.Opened, true, true, FoldableOrientation.Vertical));
        }

        [Test]
        public void PlayerOrientationLock_IphoneDuoSeatedIgnoresLock_OthersFollowIt()
        {
            Assert.IsTrue(FoldableProductStateResolver.IsLandscapeNativeHalfOpen(
                FoldableProductState.HalfOpened, innerLandscapeNative: true));
            Assert.IsFalse(FoldableProductStateResolver.IsLandscapeNativeHalfOpen(
                FoldableProductState.Opened, innerLandscapeNative: true));
            Assert.IsFalse(FoldableProductStateResolver.IsLandscapeNativeHalfOpen(
                FoldableProductState.HalfOpened, innerLandscapeNative: false));

            // Duo Seated: the device allows only the hold's axis while Seated, so no lock forces the pose.
            Assert.IsNull(FoldablePlayerOrientation.LockedPoseLandscape(
                true, FoldableProductState.HalfOpened, innerLandscapeNative: true));
            Assert.IsNull(FoldablePlayerOrientation.LockedHalfOpenHinge(true, innerLandscapeNative: true));
            Assert.IsNull(FoldablePlayerOrientation.LockedPoseLandscape(
                false, FoldableProductState.HalfOpened, innerLandscapeNative: true));
            Assert.IsNull(FoldablePlayerOrientation.LockedHalfOpenHinge(false, innerLandscapeNative: true));
            // Both axes: free choice.
            Assert.IsNull(FoldablePlayerOrientation.LockedPoseLandscape(
                null, FoldableProductState.HalfOpened, innerLandscapeNative: true));
            Assert.IsNull(FoldablePlayerOrientation.LockedHalfOpenHinge(null, innerLandscapeNative: true));

            // Duo Expand and Galaxy Fold Half Opened keep the screen axis.
            Assert.AreEqual(true, FoldablePlayerOrientation.LockedPoseLandscape(
                true, FoldableProductState.Opened, innerLandscapeNative: true));
            Assert.AreEqual(true, FoldablePlayerOrientation.LockedPoseLandscape(
                true, FoldableProductState.HalfOpened, innerLandscapeNative: false));
            Assert.AreEqual(FoldableOrientation.Horizontal,
                FoldablePlayerOrientation.LockedHalfOpenHinge(true, innerLandscapeNative: false));
            Assert.AreEqual(FoldableOrientation.Vertical,
                FoldablePlayerOrientation.LockedHalfOpenHinge(false, innerLandscapeNative: false));

            // Live Player Settings: literal results under each one-axis lock.
            WithPlayerAxes(portrait: false, landscape: true, () =>
            {
                Assert.IsNull(FoldablePlayerOrientation.GetLockedHalfOpenHinge(innerLandscapeNative: true));
                Assert.IsNull(FoldablePlayerOrientation.GetLockedLandscapeForProduct(
                    FoldableProductState.HalfOpened, innerLandscapeNative: true));
                Assert.AreEqual(true, FoldablePlayerOrientation.GetLockedLandscapeForProduct(
                    FoldableProductState.Opened, innerLandscapeNative: true));
            });
            WithPlayerAxes(portrait: true, landscape: false, () =>
            {
                Assert.IsNull(FoldablePlayerOrientation.GetLockedHalfOpenHinge(innerLandscapeNative: true));
                Assert.IsNull(FoldablePlayerOrientation.GetLockedLandscapeForProduct(
                    FoldableProductState.HalfOpened, innerLandscapeNative: true));
                Assert.AreEqual(false, FoldablePlayerOrientation.GetLockedLandscapeForProduct(
                    FoldableProductState.Opened, innerLandscapeNative: true));
            });
        }

        // Seated allows only the hold's axis on device, so a one-axis lock never forces the Duo Seated pose:
        // it defaults to the laptop.
        [TestCase(true, TestName = "LandscapeLock_DuoSeated_FreeLaptopDefault")]
        [TestCase(false, TestName = "PortraitLock_DuoSeated_FreeLaptopDefault")]
        public void PlayerOrientationLock_IphoneDuoSeatedIsFreeUnderLock(bool lockedLandscape)
        {
            Assert.IsNull(FoldablePlayerOrientation.LockedPoseLandscape(
                lockedLandscape, FoldableProductState.HalfOpened, innerLandscapeNative: true));
            Assert.IsNull(FoldablePlayerOrientation.LockedHalfOpenHinge(lockedLandscape, true));

            WithPlayerAxes(!lockedLandscape, lockedLandscape, () =>
            {
                Assert.IsNull(FoldablePlayerOrientation.GetLockedHalfOpenHinge(innerLandscapeNative: true));
                Assert.IsFalse(
                    FoldablePlayerOrientation.ResolveForProduct(
                        FoldableDeviceKind.FoldablePhone,
                        FoldableProductStateResolver.DefaultHalfOpenHinge(),
                        FoldableProductState.HalfOpened,
                        innerLandscapeNative: true),
                    "default Seated pose is the laptop");
            });
        }

        // A fixed Default Orientation ignores Screen.autorotateTo* on device, so Duo Seated keeps the lock.
        [Test]
        public void PlayerOrientationLock_FixedDefaultOrientation_KeepsDuoSeatedLock()
        {
            Assert.AreEqual(true, FoldablePlayerOrientation.LockedPoseLandscape(
                true, FoldableProductState.HalfOpened, innerLandscapeNative: true, autoRotation: false));
            Assert.AreEqual(false, FoldablePlayerOrientation.LockedPoseLandscape(
                false, FoldableProductState.HalfOpened, innerLandscapeNative: true, autoRotation: false));
            // Landscape lock = wide book (vertical fold), portrait lock = tall laptop (horizontal fold).
            Assert.AreEqual(FoldableOrientation.Vertical, FoldablePlayerOrientation.LockedHalfOpenHinge(
                true, innerLandscapeNative: true, autoRotation: false));
            Assert.AreEqual(FoldableOrientation.Horizontal, FoldablePlayerOrientation.LockedHalfOpenHinge(
                false, innerLandscapeNative: true, autoRotation: false));

            UIOrientation defaultOrientation = PlayerSettings.defaultInterfaceOrientation;
            try
            {
                PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
                Assert.IsFalse(FoldablePlayerOrientation.IsAutoRotation);
                Assert.AreEqual(true, FoldablePlayerOrientation.GetLockedLandscapeForProduct(
                    FoldableProductState.HalfOpened, innerLandscapeNative: true));
                Assert.AreEqual(FoldableOrientation.Vertical,
                    FoldablePlayerOrientation.GetLockedHalfOpenHinge(innerLandscapeNative: true));
            }
            finally
            {
                PlayerSettings.defaultInterfaceOrientation = defaultOrientation;
            }
        }

        [Test]
        public void BuildTargetOwnsProfile_DuoOnIos_FoldFlipOnAndroid()
        {
#if UNITY_IOS
            Assert.IsTrue(FoldableEditorSession.BuildTargetOwnsProfile(landscapeNativeInner: true));
            Assert.IsFalse(FoldableEditorSession.BuildTargetOwnsProfile(landscapeNativeInner: false));
#elif UNITY_ANDROID
            Assert.IsFalse(FoldableEditorSession.BuildTargetOwnsProfile(landscapeNativeInner: true));
            Assert.IsTrue(FoldableEditorSession.BuildTargetOwnsProfile(landscapeNativeInner: false));
#else
            Assert.IsFalse(FoldableEditorSession.BuildTargetOwnsProfile(landscapeNativeInner: true));
            Assert.IsFalse(FoldableEditorSession.BuildTargetOwnsProfile(landscapeNativeInner: false));
#endif
        }

        [Test]
        public void LandscapeNativeInner_AppliesToFoldKindOnly()
        {
            Assert.IsTrue(FoldableDeviceCatalog.IsLandscapeNativeInner(
                FoldableDeviceKind.FoldablePhone, true));
            Assert.IsFalse(FoldableDeviceCatalog.IsLandscapeNativeInner(
                FoldableDeviceKind.FlipablePhone, true));
            Assert.IsFalse(FoldableDeviceCatalog.IsLandscapeNativeInner(
                FoldableDeviceKind.DualScreen, true));
            Assert.IsFalse(FoldableDeviceCatalog.IsLandscapeNativeInner(
                FoldableDeviceKind.FoldablePhone, false));

            var duo = FoldableDeviceCatalog.FindById("iphone-duo-like");
            Assert.NotNull(duo, "catalog needs iphone-duo-like");
            Assert.IsTrue(duo.HasLandscapeNativeInner);
        }

        [Test]
        public void IphoneDuoDesignReference_SimulatorLabelIsIphoneDuoRole()
        {
            var duo = FoldableDeviceCatalog.FindById("iphone-duo-like");
            Assert.NotNull(duo, "catalog needs iphone-duo-like");
            // Project-specific: only when Project Settings use it as the Seated Design Canvas
            // (reported Inconclusive otherwise, not a silent pass).
            Assume.That(
                FoldableDeviceCatalog.IsDesignCanvasReference(duo.Id),
                "iphone-duo-like is not this project's iPhone Duo Seated Design Canvas reference");
            StringAssert.StartsWith(
                FoldableDeviceCatalog.SimulatorListTopPrefix + "iPhone Duo / ",
                duo.FriendlyName);
        }

        [Test]
        public void DesignRoleLabels_NormalizeToShortName()
        {
            Assert.IsTrue(FoldableDeviceCatalog.NamesMatch("\u0001Fold Cover / Galaxy Z Fold8-like", "Galaxy Z Fold8-like"));
            Assert.IsTrue(FoldableDeviceCatalog.NamesMatch("\u0001iPhone Duo / iPhone Duo-like", "iPhone Duo-like"));
            Assert.IsTrue(FoldableDeviceCatalog.NamesMatch("\u0001Fold / Galaxy Z Fold8-like", "Galaxy Z Fold8-like"));
            Assert.IsTrue(FoldableDeviceCatalog.NamesMatch("\u0001Flip / Galaxy Z Flip7-like", "Galaxy Z Flip7-like"));
            Assert.IsTrue(FoldableDeviceCatalog.NamesMatch(FoldableDeviceCatalog.FoldPrefix + "Pixel Fold-like", "Pixel Fold-like"));
        }

        [Test]
        public void SetDeviceBinding_FoldHalfOpenedLandscapeToDuo_KeepsLaptopPose()
        {
            var fold = FoldableDeviceCatalog.FindById("galaxy-z-fold8-like");
            var duo = FoldableDeviceCatalog.FindById("iphone-duo-like");
            Assert.NotNull(fold, "catalog needs galaxy-z-fold8-like");
            Assert.NotNull(duo, "catalog needs iphone-duo-like");

            var from = new FoldableDeviceCatalog.Resolution
            {
                Device = fold,
                Screen = fold.Screens[FoldableEditorSession.PreferredScreenIndex(fold, true)],
                IsKnownFoldable = true,
            };
            var to = new FoldableDeviceCatalog.Resolution
            {
                Device = duo,
                Screen = duo.Screens[FoldableEditorSession.PreferredScreenIndex(duo, true)],
                IsKnownFoldable = true,
            };

            try
            {
                // Both Player axes: the Seated pose is free (see
                // PublishProductState_IphoneDuoSeated_PoseFollowsHold).
                WithPlayerAxes(portrait: true, landscape: true, () =>
                {
                    FoldableEditorSession.ClearDeviceBinding(FoldableEditorSession.Source.Bridge);
                    FoldableEditorSession.ResetProductDefaults();
                    FoldableEditorSession.SetDeviceBinding(
                        from, FoldableEditorSession.Source.Bridge, applyDefaults: true);
                    FoldableEditorSession.PublishProductState(
                        FoldableProductState.HalfOpened,
                        FoldableOrientation.Horizontal,
                        0.5f,
                        24,
                        true,
                        FoldableEditorSession.MetricsWidth,
                        FoldableEditorSession.MetricsHeight,
                        FoldableEditorSession.Source.Simulator,
                        landscape: false);
                    Assert.IsFalse(FoldableEditorSession.IsLandscape, "Fold Landscape laptop is the tall screen");
                    Assert.AreEqual(FoldableOrientation.Horizontal, FoldableEditorSession.Orientation);

                    // Profile switch (watcher: applyDefaults false; stale Fold chrome is ignored).
                    FoldableEditorSession.SetDeviceBinding(
                        to, FoldableEditorSession.Source.Bridge, applyDefaults: false);
                    Assert.IsNull(FoldableEditorSessionWatcher.PoseFromChromeRotation(
                        true, 90, profileChanged: true));

                    Assert.IsTrue(FoldableEditorSession.BoundInnerLandscapeNative);
                    Assert.AreEqual(FoldableProductState.HalfOpened, FoldableEditorSession.ProductState);
                    Assert.IsFalse(FoldableEditorSession.IsLandscape, "laptop hold kept: Duo Seated Portrait");
                    Assert.AreEqual(FoldableOrientation.Horizontal, FoldableEditorSession.Orientation);
                    Assert.AreEqual(2007, FoldableEditorSession.MetricsWidth);
                    Assert.AreEqual(2853, FoldableEditorSession.MetricsHeight);

                    // The commit then turns the chrome to portrait; reading it back stays Portrait.
                    Assert.AreEqual(false, FoldableEditorSessionWatcher.PoseFromChromeRotation(
                        true, 0, profileChanged: false));
                });
            }
            finally
            {
                FoldableEditorSession.ClearDeviceBinding(FoldableEditorSession.Source.Bridge);
                FoldableEditorSession.ResetProductDefaults();
            }
        }

        [Test]
        public void SetDeviceBinding_FoldHalfOpenedToDuo_LandscapeLock_KeepsLaptopPose()
        {
            var fold = FoldableDeviceCatalog.FindById("galaxy-z-fold8-like");
            var duo = FoldableDeviceCatalog.FindById("iphone-duo-like");
            Assert.NotNull(fold, "catalog needs galaxy-z-fold8-like");
            Assert.NotNull(duo, "catalog needs iphone-duo-like");

            var from = new FoldableDeviceCatalog.Resolution
            {
                Device = fold,
                Screen = fold.Screens[FoldableEditorSession.PreferredScreenIndex(fold, true)],
                IsKnownFoldable = true,
            };
            var to = new FoldableDeviceCatalog.Resolution
            {
                Device = duo,
                Screen = duo.Screens[FoldableEditorSession.PreferredScreenIndex(duo, true)],
                IsKnownFoldable = true,
            };

            try
            {
                // Landscape-only project: Duo Seated unlocks portrait on device, so the lock does not
                // force the Duo pose and the Fold laptop hold carries over (tall 2007×2853).
                WithPlayerAxes(portrait: false, landscape: true, () =>
                {
                    FoldableEditorSession.ClearDeviceBinding(FoldableEditorSession.Source.Bridge);
                    FoldableEditorSession.ResetProductDefaults();
                    FoldableEditorSession.SetDeviceBinding(
                        from, FoldableEditorSession.Source.Bridge, applyDefaults: true);
                    FoldableEditorSession.PublishProductState(
                        FoldableProductState.HalfOpened,
                        FoldableOrientation.Horizontal,
                        0.5f,
                        24,
                        true,
                        FoldableEditorSession.MetricsWidth,
                        FoldableEditorSession.MetricsHeight,
                        FoldableEditorSession.Source.Simulator,
                        landscape: true);
                    Assert.IsFalse(FoldableEditorSession.IsLandscape, "Fold laptop under a landscape lock is the tall screen");
                    Assert.AreEqual(FoldableOrientation.Horizontal, FoldableEditorSession.Orientation);

                    FoldableEditorSession.SetDeviceBinding(
                        to, FoldableEditorSession.Source.Bridge, applyDefaults: false);

                    Assert.AreEqual(FoldableProductState.HalfOpened, FoldableEditorSession.ProductState);
                    Assert.IsFalse(FoldableEditorSession.IsLandscape, "Duo Seated Portrait (laptop)");
                    Assert.AreEqual(FoldableOrientation.Horizontal, FoldableEditorSession.Orientation);
                    Assert.AreEqual(2007, FoldableEditorSession.MetricsWidth);
                    Assert.AreEqual(2853, FoldableEditorSession.MetricsHeight);
                });
            }
            finally
            {
                FoldableEditorSession.ClearDeviceBinding(FoldableEditorSession.Source.Bridge);
                FoldableEditorSession.ResetProductDefaults();
            }
        }

        [Test]
        public void SetDeviceBinding_FoldHalfOpenedToDuo_PortraitLock_KeepsBookPose()
        {
            var fold = FoldableDeviceCatalog.FindById("galaxy-z-fold8-like");
            var duo = FoldableDeviceCatalog.FindById("iphone-duo-like");
            Assert.NotNull(fold, "catalog needs galaxy-z-fold8-like");
            Assert.NotNull(duo, "catalog needs iphone-duo-like");

            var from = new FoldableDeviceCatalog.Resolution
            {
                Device = fold,
                Screen = fold.Screens[FoldableEditorSession.PreferredScreenIndex(fold, true)],
                IsKnownFoldable = true,
            };
            var to = new FoldableDeviceCatalog.Resolution
            {
                Device = duo,
                Screen = duo.Screens[FoldableEditorSession.PreferredScreenIndex(duo, true)],
                IsKnownFoldable = true,
            };

            try
            {
                // Portrait-only project: Duo Seated unlocks landscape on device, so the lock does not
                // force the Duo pose and the Fold book hold carries over (wide 2853×2007).
                WithPlayerAxes(portrait: true, landscape: false, () =>
                {
                    FoldableEditorSession.ClearDeviceBinding(FoldableEditorSession.Source.Bridge);
                    FoldableEditorSession.ResetProductDefaults();
                    FoldableEditorSession.SetDeviceBinding(
                        from, FoldableEditorSession.Source.Bridge, applyDefaults: true);
                    FoldableEditorSession.PublishProductState(
                        FoldableProductState.HalfOpened,
                        FoldableOrientation.Vertical,
                        0.5f,
                        24,
                        true,
                        FoldableEditorSession.MetricsWidth,
                        FoldableEditorSession.MetricsHeight,
                        FoldableEditorSession.Source.Simulator,
                        landscape: false);
                    Assert.IsFalse(FoldableEditorSession.IsLandscape, "Fold book under a portrait lock");

                    FoldableEditorSession.SetDeviceBinding(
                        to, FoldableEditorSession.Source.Bridge, applyDefaults: false);

                    Assert.AreEqual(FoldableProductState.HalfOpened, FoldableEditorSession.ProductState);
                    Assert.IsTrue(FoldableEditorSession.IsLandscape, "Duo Seated Landscape (book)");
                    Assert.AreEqual(FoldableOrientation.Vertical, FoldableEditorSession.Orientation);
                    Assert.AreEqual(2853, FoldableEditorSession.MetricsWidth);
                    Assert.AreEqual(2007, FoldableEditorSession.MetricsHeight);
                });
            }
            finally
            {
                FoldableEditorSession.ClearDeviceBinding(FoldableEditorSession.Source.Bridge);
                FoldableEditorSession.ResetProductDefaults();
            }
        }

        [Test]
        public void SetDeviceBinding_DuoBookToFold_LandscapeLock_IsFoldTabletop()
        {
            var duo = FoldableDeviceCatalog.FindById("iphone-duo-like");
            var fold = FoldableDeviceCatalog.FindById("galaxy-z-fold8-like");
            Assert.NotNull(duo, "catalog needs iphone-duo-like");
            Assert.NotNull(fold, "catalog needs galaxy-z-fold8-like");

            var from = new FoldableDeviceCatalog.Resolution
            {
                Device = duo,
                Screen = duo.Screens[FoldableEditorSession.PreferredScreenIndex(duo, true)],
                IsKnownFoldable = true,
            };
            var to = new FoldableDeviceCatalog.Resolution
            {
                Device = fold,
                Screen = fold.Screens[FoldableEditorSession.PreferredScreenIndex(fold, true)],
                IsKnownFoldable = true,
            };

            try
            {
                // Landscape-only: the Duo is the wide book (Vertical); a Fold under the same lock
                // is Tabletop (Horizontal) — the switch must not keep the Duo's book crease.
                WithPlayerAxes(portrait: false, landscape: true, () =>
                {
                    FoldableEditorSession.ClearDeviceBinding(FoldableEditorSession.Source.Bridge);
                    FoldableEditorSession.ResetProductDefaults();
                    FoldableEditorSession.SetDeviceBinding(
                        from, FoldableEditorSession.Source.Bridge, applyDefaults: true);
                    FoldableEditorSession.PublishProductState(
                        FoldableProductState.HalfOpened,
                        FoldableOrientation.Vertical,
                        0.5f,
                        24,
                        true,
                        FoldableEditorSession.MetricsWidth,
                        FoldableEditorSession.MetricsHeight,
                        FoldableEditorSession.Source.Simulator);
                    Assert.IsTrue(FoldableEditorSession.IsLandscape, "Duo Seated Landscape (book)");

                    FoldableEditorSession.SetDeviceBinding(
                        to, FoldableEditorSession.Source.Bridge, applyDefaults: false);

                    Assert.AreEqual(FoldableProductState.HalfOpened, FoldableEditorSession.ProductState);
                    Assert.IsFalse(FoldableEditorSession.IsLandscape, "Fold half-open landscape lock is the tall screen");
                    Assert.AreEqual(FoldableOrientation.Horizontal, FoldableEditorSession.Orientation);
                });
            }
            finally
            {
                FoldableEditorSession.ClearDeviceBinding(FoldableEditorSession.Source.Bridge);
                FoldableEditorSession.ResetProductDefaults();
            }
        }

        [Test]
        public void DuoHalfOpened_ScreenOrientationChange_MovesHinge()
        {
            var duo = FoldableDeviceCatalog.FindById("iphone-duo-like");
            Assert.NotNull(duo, "catalog needs iphone-duo-like");
            var bound = new FoldableDeviceCatalog.Resolution
            {
                Device = duo,
                Screen = duo.Screens[FoldableEditorSession.PreferredScreenIndex(duo, true)],
                IsKnownFoldable = true,
            };

            try
            {
                // Both Player axes: the Seated pose is free (see
                // PublishProductState_IphoneDuoSeated_PoseFollowsHold).
                WithPlayerAxes(portrait: true, landscape: true, () =>
                {
                    FoldableEditorSession.ClearDeviceBinding(FoldableEditorSession.Source.Bridge);
                    FoldableEditorSession.ResetProductDefaults();
                    FoldableEditorSession.SetDeviceBinding(
                        bound, FoldableEditorSession.Source.Bridge, applyDefaults: true);
                    FoldableEditorSession.PublishProductState(
                        FoldableProductState.HalfOpened,
                        FoldableOrientation.Horizontal,
                        0.5f,
                        24,
                        true,
                        FoldableEditorSession.MetricsWidth,
                        FoldableEditorSession.MetricsHeight,
                        FoldableEditorSession.Source.Simulator,
                        landscape: false);
                    Assert.AreEqual(FoldableOrientation.Horizontal, FoldableEditorSession.Orientation);

                    // Screen dropdown → Landscape with the stale Tabletop hinge: wide book pose.
                    FoldableEditorSession.PublishProductState(
                        FoldableProductState.HalfOpened,
                        FoldableOrientation.Horizontal,
                        0.5f,
                        24,
                        true,
                        FoldableEditorSession.MetricsWidth,
                        FoldableEditorSession.MetricsHeight,
                        FoldableEditorSession.Source.Simulator,
                        landscape: true);
                    Assert.AreEqual(FoldableOrientation.Vertical, FoldableEditorSession.Orientation);
                    Assert.AreEqual(2853, FoldableEditorSession.MetricsWidth);
                    Assert.AreEqual(2007, FoldableEditorSession.MetricsHeight);
                    Assert.AreEqual(
                        FoldableEditorSession.Orientation,
                        FoldableEditorSession.BuildLayout().orientation);

                    // Simulator chrome rotation path.
                    FoldableEditorSession.PublishLandscape(false, FoldableEditorSession.Source.Bridge);
                    Assert.AreEqual(FoldableOrientation.Horizontal, FoldableEditorSession.Orientation);
                    Assert.AreEqual(
                        FoldableEditorSession.Orientation,
                        FoldableEditorSession.BuildLayout().orientation);
                });
            }
            finally
            {
                FoldableEditorSession.ClearDeviceBinding(FoldableEditorSession.Source.Bridge);
                FoldableEditorSession.ResetProductDefaults();
            }
        }

        [TestCase("galaxy-z-fold7-like", 1968, 2184, TestName = "Fold7_HalfOpen_LandscapeLock_IsTabletop")]
        [TestCase("galaxy-z-fold8-like", 1848, 2448, TestName = "Fold8_HalfOpen_LandscapeLock_IsTabletop")]
        public void PublishLandscape_AndroidHalfOpen_LandscapeLock_IsTabletop(
            string profileId,
            int expectedWidth,
            int expectedHeight)
        {
            var profile = FoldableDeviceCatalog.FindById(profileId);
            Assert.NotNull(profile, "catalog needs " + profileId);
            Assert.IsFalse(profile.HasLandscapeNativeInner);
            var bound = new FoldableDeviceCatalog.Resolution
            {
                Device = profile,
                Screen = profile.Screens[FoldableEditorSession.PreferredScreenIndex(profile, true)],
                IsKnownFoldable = true,
            };

            try
            {
                WithPlayerAxes(portrait: false, landscape: true, () =>
                {
                    FoldableEditorSession.ClearDeviceBinding(FoldableEditorSession.Source.Bridge);
                    FoldableEditorSession.ResetProductDefaults();
                    FoldableEditorSession.SetDeviceBinding(
                        bound, FoldableEditorSession.Source.Bridge, applyDefaults: true);
                    FoldableEditorSession.PublishProductState(
                        FoldableProductState.HalfOpened,
                        FoldableOrientation.Horizontal,
                        0.5f,
                        24,
                        true,
                        FoldableEditorSession.MetricsWidth,
                        FoldableEditorSession.MetricsHeight,
                        FoldableEditorSession.Source.Simulator);

                    Assert.IsFalse(FoldableEditorSession.IsLandscape, "landscape lock rotates 90 to the tall screen");
                    Assert.AreEqual(FoldableOrientation.Horizontal, FoldableEditorSession.Orientation);
                    Assert.AreEqual(expectedWidth, FoldableEditorSession.MetricsWidth);
                    Assert.AreEqual(expectedHeight, FoldableEditorSession.MetricsHeight);
                });
            }
            finally
            {
                FoldableEditorSession.ClearDeviceBinding(FoldableEditorSession.Source.Bridge);
                FoldableEditorSession.ResetProductDefaults();
            }
        }

        [Test]
        public void PublishLandscape_AndroidHalfOpen_Unlocked_HingeFollowsScreen()
        {
            var profile = FoldableDeviceCatalog.FindById("galaxy-z-fold8-like");
            Assert.NotNull(profile);
            var bound = new FoldableDeviceCatalog.Resolution
            {
                Device = profile,
                Screen = profile.Screens[FoldableEditorSession.PreferredScreenIndex(profile, true)],
                IsKnownFoldable = true,
            };

            try
            {
                WithPlayerAxes(portrait: true, landscape: true, () =>
                {
                    FoldableEditorSession.ClearDeviceBinding(FoldableEditorSession.Source.Bridge);
                    FoldableEditorSession.ResetProductDefaults();
                    FoldableEditorSession.SetDeviceBinding(
                        bound, FoldableEditorSession.Source.Bridge, applyDefaults: true);
                    FoldableEditorSession.PublishProductState(
                        FoldableProductState.HalfOpened,
                        FoldableOrientation.Horizontal,
                        0.5f,
                        24,
                        true,
                        FoldableEditorSession.MetricsWidth,
                        FoldableEditorSession.MetricsHeight,
                        FoldableEditorSession.Source.Simulator,
                        landscape: true);
                    Assert.AreEqual(FoldableOrientation.Vertical, FoldableEditorSession.Orientation,
                        "wide screen is the Portrait book: a stale Tabletop hinge becomes vertical");

                    FoldableEditorSession.PublishLandscape(false, FoldableEditorSession.Source.Bridge);
                    Assert.IsFalse(FoldableEditorSession.IsLandscape, "unlocked screen follows chrome");
                    Assert.AreEqual(FoldableOrientation.Horizontal, FoldableEditorSession.Orientation,
                        "tall screen is the Landscape tabletop");
                    Assert.AreEqual(1848, FoldableEditorSession.MetricsWidth);
                    Assert.AreEqual(2448, FoldableEditorSession.MetricsHeight);
                });
            }
            finally
            {
                FoldableEditorSession.ClearDeviceBinding(FoldableEditorSession.Source.Bridge);
                FoldableEditorSession.ResetProductDefaults();
            }
        }

        [Test]
        public void AndroidHalfOpenHeldScreenLandscape_LockPose_ElseEntryScreen()
        {
            var profile = FoldableDeviceCatalog.FindById("galaxy-z-fold8-like");
            Assert.NotNull(profile);
            var bound = new FoldableDeviceCatalog.Resolution
            {
                Device = profile,
                Screen = profile.Screens[FoldableEditorSession.PreferredScreenIndex(profile, true)],
                IsKnownFoldable = true,
            };
            var previousRules = FoldableSettings.Rules;

            try
            {
                FoldableSettings.Rules = new FoldableHalfOpenRules();
                // One axis: the lock's pose (landscape-only → tall Landscape, portrait-only → wide Portrait).
                WithPlayerAxes(portrait: false, landscape: true, () =>
                    Assert.AreEqual(false, FoldableEditorSession.AndroidHalfOpenHeldScreenLandscape(
                        FoldableDeviceKind.FoldablePhone, FoldableProductState.HalfOpened, false)));
                WithPlayerAxes(portrait: true, landscape: false, () =>
                    Assert.AreEqual(true, FoldableEditorSession.AndroidHalfOpenHeldScreenLandscape(
                        FoldableDeviceKind.FoldablePhone, FoldableProductState.HalfOpened, false)));

                WithPlayerAxes(portrait: true, landscape: true, () =>
                {
                    FoldableEditorSession.ClearDeviceBinding(FoldableEditorSession.Source.Bridge);
                    FoldableEditorSession.ResetProductDefaults();
                    FoldableEditorSession.SetDeviceBinding(
                        bound, FoldableEditorSession.Source.Bridge, applyDefaults: true);
                    FoldableEditorSession.PublishProductState(
                        FoldableProductState.Opened,
                        FoldableOrientation.Horizontal,
                        0.5f,
                        24,
                        false,
                        FoldableEditorSession.MetricsWidth,
                        FoldableEditorSession.MetricsHeight,
                        FoldableEditorSession.Source.Simulator,
                        landscape: true);

                    // Default cards, both axes: folding changes nothing, so the Half Opened preset
                    // keeps the wide flat screen.
                    Assert.IsTrue(FoldableEditorSession.PresetPose(
                        FoldableProductState.HalfOpened, FoldableProductStateResolver.DefaultHalfOpenHinge()),
                        "default card keeps the current screen");

                    // Locked Fold Cover card (Fold8 is Fold Cover): the Half Opened preset from the
                    // wide flat screen keeps that screen, so the default Tabletop hinge becomes the
                    // Portrait book (vertical, 2448×1848).
                    var rules = new FoldableHalfOpenRules();
                    rules.foldCover.whileRotating = FoldableHalfOpenRotation.Locked;
                    FoldableSettings.Rules = rules;
                    bool entry = FoldableEditorSession.PresetPose(
                        FoldableProductState.HalfOpened, FoldableProductStateResolver.DefaultHalfOpenHinge());
                    Assert.IsTrue(entry, "entering Half Opened keeps the wide flat screen");
                    FoldableEditorSession.PublishProductState(
                        FoldableProductState.HalfOpened,
                        FoldableProductStateResolver.DefaultHalfOpenHinge(),
                        0.5f,
                        24,
                        true,
                        FoldableEditorSession.MetricsWidth,
                        FoldableEditorSession.MetricsHeight,
                        FoldableEditorSession.Source.Simulator,
                        entry);
                    Assert.IsTrue(FoldableEditorSession.IsLandscape);
                    Assert.IsTrue(FoldableEditorSession.HalfOpenEntryLandscape);
                    Assert.AreEqual(FoldableOrientation.Vertical, FoldableEditorSession.Orientation);
                    Assert.AreEqual(2448, FoldableEditorSession.MetricsWidth);
                    Assert.AreEqual(1848, FoldableEditorSession.MetricsHeight);
                    Assert.AreEqual(FoldableOrientation.Vertical, FoldableEditorSession.BuildLayout().orientation);
                    Assert.AreEqual(true, FoldableEditorSession.AndroidHalfOpenHeldScreenLandscape(
                        FoldableDeviceKind.FoldablePhone, FoldableProductState.HalfOpened, false),
                        "Locked card holds the entry screen");

                    // Only the Fold card changed: it does not apply to a Fold Cover device.
                    rules = new FoldableHalfOpenRules();
                    rules.fold.whileRotating = FoldableHalfOpenRotation.Locked;
                    FoldableSettings.Rules = rules;
                    Assert.IsNull(FoldableEditorSession.AndroidHalfOpenHeldScreenLandscape(
                        FoldableDeviceKind.FoldablePhone, FoldableProductState.HalfOpened, false),
                        "the Fold card does not apply to a Fold Cover device");

                    // Both axes, default cards: the screen follows rotation; Flip, Duo and Expand are not held.
                    FoldableSettings.Rules = new FoldableHalfOpenRules();
                    Assert.IsNull(FoldableEditorSession.AndroidHalfOpenHeldScreenLandscape(
                        FoldableDeviceKind.FoldablePhone, FoldableProductState.HalfOpened, false));
                    Assert.IsNull(FoldableEditorSession.AndroidHalfOpenHeldScreenLandscape(
                        FoldableDeviceKind.FlipablePhone, FoldableProductState.HalfOpened, false));
                    Assert.IsNull(FoldableEditorSession.AndroidHalfOpenHeldScreenLandscape(
                        FoldableDeviceKind.FoldablePhone, FoldableProductState.HalfOpened, true));
                    Assert.IsNull(FoldableEditorSession.AndroidHalfOpenHeldScreenLandscape(
                        FoldableDeviceKind.FoldablePhone, FoldableProductState.Opened, false));

                    // The panel's hinge choice picks the pose: Tabletop → tall, Book → wide.
                    Assert.IsFalse(FoldablePlayerOrientation.ResolveForProduct(
                        FoldableDeviceKind.FoldablePhone, FoldableOrientation.Horizontal,
                        FoldableProductState.HalfOpened));
                    Assert.IsTrue(FoldablePlayerOrientation.ResolveForProduct(
                        FoldableDeviceKind.FoldablePhone, FoldableOrientation.Vertical,
                        FoldableProductState.HalfOpened));
                });
            }
            finally
            {
                FoldableSettings.Rules = previousRules;
                FoldableEditorSession.ClearDeviceBinding(FoldableEditorSession.Source.Bridge);
                FoldableEditorSession.ResetProductDefaults();
            }
        }

        [Test]
        public void PublishProductState_IphoneDuoSeated_Unlocked_DefaultsToLaptop()
        {
            var duo = FoldableDeviceCatalog.FindById("iphone-duo-like");
            Assert.NotNull(duo, "catalog needs iphone-duo-like");
            var bound = new FoldableDeviceCatalog.Resolution
            {
                Device = duo,
                Screen = duo.Screens[FoldableEditorSession.PreferredScreenIndex(duo, true)],
                IsKnownFoldable = true,
            };

            try
            {
                WithPlayerAxes(portrait: true, landscape: true, () =>
                {
                    FoldableEditorSession.ClearDeviceBinding(FoldableEditorSession.Source.Bridge);
                    FoldableEditorSession.ResetProductDefaults();
                    FoldableEditorSession.SetDeviceBinding(
                        bound, FoldableEditorSession.Source.Bridge, applyDefaults: true);

                    // Fold panel Half Opened preset with no lock: the default hold is the laptop
                    // (tall Seated Portrait).
                    Assert.IsNull(FoldablePlayerOrientation.GetLockedHalfOpenHinge(innerLandscapeNative: true));
                    FoldableEditorSession.PublishProductState(
                        FoldableProductState.HalfOpened,
                        FoldableProductStateResolver.DefaultHalfOpenHinge(),
                        0.5f,
                        24,
                        true,
                        FoldableEditorSession.MetricsWidth,
                        FoldableEditorSession.MetricsHeight,
                        FoldableEditorSession.Source.Simulator);

                    Assert.IsFalse(FoldableEditorSession.IsLandscape, "Seated Portrait (laptop)");
                    Assert.AreEqual(FoldableOrientation.Horizontal, FoldableEditorSession.Orientation);
                    Assert.AreEqual(2007, FoldableEditorSession.MetricsWidth);
                    Assert.AreEqual(2853, FoldableEditorSession.MetricsHeight);
                });
            }
            finally
            {
                FoldableEditorSession.ClearDeviceBinding(FoldableEditorSession.Source.Bridge);
                FoldableEditorSession.ResetProductDefaults();
            }
        }

        // Seated is never locked: the hold picks the screen. Laptop (horizontal fold) → tall
        // Seated Portrait, book (vertical fold) → wide Seated Landscape, under any Player lock.
        [TestCase(false, true, FoldableOrientation.Horizontal, false, 2007, 2853, TestName = "LandscapeOnly_DuoLaptopIsSeatedPortrait")]
        [TestCase(false, true, FoldableOrientation.Vertical, true, 2853, 2007, TestName = "LandscapeOnly_DuoBookIsSeatedLandscape")]
        [TestCase(true, false, FoldableOrientation.Horizontal, false, 2007, 2853, TestName = "PortraitOnly_DuoLaptopIsSeatedPortrait")]
        [TestCase(true, false, FoldableOrientation.Vertical, true, 2853, 2007, TestName = "PortraitOnly_DuoBookIsSeatedLandscape")]
        public void PublishProductState_IphoneDuoSeated_PoseFollowsHold(
            bool portrait,
            bool landscape,
            FoldableOrientation hinge,
            bool expectedPoseLandscape,
            int expectedWidth,
            int expectedHeight)
        {
            var duo = FoldableDeviceCatalog.FindById("iphone-duo-like");
            Assert.NotNull(duo, "catalog needs iphone-duo-like");
            var bound = new FoldableDeviceCatalog.Resolution
            {
                Device = duo,
                Screen = duo.Screens[FoldableEditorSession.PreferredScreenIndex(duo, true)],
                IsKnownFoldable = true,
            };

            try
            {
                WithPlayerAxes(portrait, landscape, () =>
                {
                    FoldableEditorSession.ClearDeviceBinding(FoldableEditorSession.Source.Bridge);
                    FoldableEditorSession.ResetProductDefaults();
                    FoldableEditorSession.SetDeviceBinding(
                        bound, FoldableEditorSession.Source.Bridge, applyDefaults: true);

                    FoldableEditorSession.PublishProductState(
                        FoldableProductState.HalfOpened,
                        hinge,
                        0.5f,
                        24,
                        true,
                        FoldableEditorSession.MetricsWidth,
                        FoldableEditorSession.MetricsHeight,
                        FoldableEditorSession.Source.Simulator);

                    Assert.AreEqual(expectedPoseLandscape, FoldableEditorSession.IsLandscape, "pose");
                    Assert.AreEqual(hinge, FoldableEditorSession.Orientation, "hinge");
                    Assert.AreEqual(expectedWidth, FoldableEditorSession.MetricsWidth, "width");
                    Assert.AreEqual(expectedHeight, FoldableEditorSession.MetricsHeight, "height");
                    Assert.AreEqual(hinge, FoldableEditorSession.BuildLayout().orientation);
                });
            }
            finally
            {
                FoldableEditorSession.ClearDeviceBinding(FoldableEditorSession.Source.Bridge);
                FoldableEditorSession.ResetProductDefaults();
            }
        }

        // Default cards, both axes: an Android Fold entering Half Opened keeps its screen (wide stays
        // wide → Portrait book, tall stays tall).
        [Test]
        public void PresetPose_FoldHalfOpen_BothAxesDefault_KeepsCurrentScreen()
        {
            var profile = FoldableDeviceCatalog.FindById("galaxy-z-fold8-like");
            Assert.NotNull(profile);
            var bound = new FoldableDeviceCatalog.Resolution
            {
                Device = profile,
                Screen = profile.Screens[FoldableEditorSession.PreferredScreenIndex(profile, true)],
                IsKnownFoldable = true,
            };
            var previousRules = FoldableSettings.Rules;

            try
            {
                FoldableSettings.Rules = new FoldableHalfOpenRules();
                WithPlayerAxes(portrait: true, landscape: true, () =>
                {
                    foreach (bool wide in new[] { true, false })
                    {
                        FoldableEditorSession.ClearDeviceBinding(FoldableEditorSession.Source.Bridge);
                        FoldableEditorSession.ResetProductDefaults();
                        FoldableEditorSession.SetDeviceBinding(
                            bound, FoldableEditorSession.Source.Bridge, applyDefaults: true);
                        FoldableEditorSession.PublishProductState(
                            FoldableProductState.Opened,
                            FoldableOrientation.Horizontal,
                            0.5f,
                            24,
                            false,
                            FoldableEditorSession.MetricsWidth,
                            FoldableEditorSession.MetricsHeight,
                            FoldableEditorSession.Source.Simulator,
                            landscape: wide);

                        foreach (var hinge in new[] { FoldableOrientation.Horizontal, FoldableOrientation.Vertical })
                        {
                            Assert.AreEqual(wide, FoldableEditorSession.PresetPose(
                                FoldableProductState.HalfOpened, hinge), "wide " + wide + ", hinge " + hinge);
                        }
                    }
                });
            }
            finally
            {
                FoldableSettings.Rules = previousRules;
                FoldableEditorSession.ClearDeviceBinding(FoldableEditorSession.Source.Bridge);
                FoldableEditorSession.ResetProductDefaults();
            }
        }

        // A changed card wins over the Player lock's pose on publish: landscape-only would give a
        // Fold Cover the tall screen, a Left to right card gives the wide one.
        [Test]
        public void PublishProductState_LandscapeOnly_ChangedCard_WinsOverLockPose()
        {
            var profile = FoldableDeviceCatalog.FindById("galaxy-z-fold8-like");
            Assert.NotNull(profile);
            var bound = new FoldableDeviceCatalog.Resolution
            {
                Device = profile,
                Screen = profile.Screens[FoldableEditorSession.PreferredScreenIndex(profile, true)],
                IsKnownFoldable = true,
            };
            var previousRules = FoldableSettings.Rules;

            try
            {
                WithPlayerAxes(portrait: false, landscape: true, () =>
                {
                    FoldableEditorSession.ClearDeviceBinding(FoldableEditorSession.Source.Bridge);
                    FoldableEditorSession.ResetProductDefaults();
                    FoldableEditorSession.SetDeviceBinding(
                        bound, FoldableEditorSession.Source.Bridge, applyDefaults: true);

                    FoldableSettings.Rules = new FoldableHalfOpenRules();
                    Assert.AreEqual(false, FoldableEditorSession.AndroidHalfOpenScreenLandscape(
                        FoldableProductState.HalfOpened, false), "default card: the lock's tall pose");

                    var rules = new FoldableHalfOpenRules();
                    rules.foldCover.contentRuns = FoldableContentRuns.LeftToRight;
                    FoldableSettings.Rules = rules;
                    Assert.AreEqual(true, FoldableEditorSession.AndroidHalfOpenScreenLandscape(
                        FoldableProductState.HalfOpened, false));
                    FoldableEditorSession.PublishProductState(
                        FoldableProductState.HalfOpened,
                        FoldableProductStateResolver.DefaultHalfOpenHinge(),
                        0.5f,
                        24,
                        true,
                        FoldableEditorSession.MetricsWidth,
                        FoldableEditorSession.MetricsHeight,
                        FoldableEditorSession.Source.Simulator);
                    Assert.IsTrue(FoldableEditorSession.IsLandscape);
                    Assert.AreEqual(2448, FoldableEditorSession.MetricsWidth);
                    Assert.AreEqual(1848, FoldableEditorSession.MetricsHeight);
                });
            }
            finally
            {
                FoldableSettings.Rules = previousRules;
                FoldableEditorSession.ClearDeviceBinding(FoldableEditorSession.Source.Bridge);
                FoldableEditorSession.ResetProductDefaults();
            }
        }

        // A changed Follow device card rotates the Simulator under a one-axis lock: a chrome turn to
        // the other shape is published. The default card keeps the lock's pose (turn dropped).
        [Test]
        public void ChromeRotation_LandscapeOnly_FollowDeviceCard_PublishesOtherShape()
        {
            var profile = FoldableDeviceCatalog.FindById("galaxy-z-fold8-like");
            Assert.NotNull(profile);
            var bound = new FoldableDeviceCatalog.Resolution
            {
                Device = profile,
                Screen = profile.Screens[FoldableEditorSession.PreferredScreenIndex(profile, true)],
                IsKnownFoldable = true,
            };
            var previousRules = FoldableSettings.Rules;

            try
            {
                WithPlayerAxes(portrait: false, landscape: true, () =>
                {
                    FoldableEditorSession.ClearDeviceBinding(FoldableEditorSession.Source.Bridge);
                    FoldableEditorSession.ResetProductDefaults();
                    FoldableEditorSession.SetDeviceBinding(
                        bound, FoldableEditorSession.Source.Bridge, applyDefaults: true);

                    // Default card: Half Opened is the lock's tall pose and a wide turn is dropped.
                    FoldableSettings.Rules = new FoldableHalfOpenRules();
                    FoldableEditorSession.PublishProductState(
                        FoldableProductState.HalfOpened,
                        FoldableProductStateResolver.DefaultHalfOpenHinge(),
                        0.5f,
                        24,
                        true,
                        FoldableEditorSession.MetricsWidth,
                        FoldableEditorSession.MetricsHeight,
                        FoldableEditorSession.Source.Simulator);
                    Assert.IsFalse(FoldableEditorSession.IsLandscape);
                    Assert.IsFalse(FoldableEditorSession.AndroidHalfOpenFollowsDevice(
                        FoldableProductState.HalfOpened, false));
                    FoldableEditorSessionWatcher.ApplyAndroidChromeRotation(true, 90, false);
                    Assert.IsFalse(FoldableEditorSession.IsLandscape, "default card drops the turn");

                    // Follow device card (Fold8 reads the Fold Cover card): both shapes are reachable.
                    var rules = new FoldableHalfOpenRules();
                    rules.foldCover.whileRotating = FoldableHalfOpenRotation.FollowDevice;
                    FoldableSettings.Rules = rules;
                    Assert.IsTrue(FoldableEditorSession.AndroidHalfOpenFollowsDevice(
                        FoldableProductState.HalfOpened, false));
                    FoldableEditorSessionWatcher.ApplyAndroidChromeRotation(true, 90, false);
                    Assert.IsTrue(FoldableEditorSession.IsLandscape, "wide turn published");
                    Assert.AreEqual(2448, FoldableEditorSession.MetricsWidth);
                    FoldableEditorSessionWatcher.ApplyAndroidChromeRotation(true, 0, false);
                    Assert.IsFalse(FoldableEditorSession.IsLandscape, "tall turn published");

                    // Leaving Half Opened: the lock decides again.
                    Assert.IsFalse(FoldableEditorSession.AndroidHalfOpenFollowsDevice(
                        FoldableProductState.Opened, false));
                });
            }
            finally
            {
                FoldableSettings.Rules = previousRules;
                FoldableEditorSession.ClearDeviceBinding(FoldableEditorSession.Source.Bridge);
                FoldableEditorSession.ResetProductDefaults();
            }
        }

        // A fixed Default Orientation (Landscape Left) keeps the platform pose whatever the card says:
        // the runtime hold never overrides it, so the Simulator does not either.
        [Test]
        public void ChangedCard_FixedDefaultOrientation_KeepsLockPose()
        {
            var profile = FoldableDeviceCatalog.FindById("galaxy-z-fold8-like");
            Assert.NotNull(profile);
            var bound = new FoldableDeviceCatalog.Resolution
            {
                Device = profile,
                Screen = profile.Screens[FoldableEditorSession.PreferredScreenIndex(profile, true)],
                IsKnownFoldable = true,
            };
            var previousRules = FoldableSettings.Rules;
            UIOrientation defaultOrientation = PlayerSettings.defaultInterfaceOrientation;

            try
            {
                FoldableEditorSession.ClearDeviceBinding(FoldableEditorSession.Source.Bridge);
                FoldableEditorSession.ResetProductDefaults();
                FoldableEditorSession.SetDeviceBinding(
                    bound, FoldableEditorSession.Source.Bridge, applyDefaults: true);
                PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
                Assert.IsFalse(FoldablePlayerOrientation.IsAutoRotation);
                Assert.AreEqual(true, FoldablePlayerOrientation.GetLockedLandscape());

                var wide = new FoldableHalfOpenRules();
                wide.foldCover.contentRuns = FoldableContentRuns.LeftToRight;
                var follow = new FoldableHalfOpenRules();
                follow.foldCover.whileRotating = FoldableHalfOpenRotation.FollowDevice;
                foreach (var rules in new[] { wide, follow })
                {
                    FoldableSettings.Rules = rules;
                    Assert.IsFalse(FoldableEditorSession.AndroidHalfOpenCardChanged(
                        FoldableProductState.HalfOpened, false));
                    Assert.IsFalse(FoldableEditorSession.AndroidHalfOpenFollowsDevice(
                        FoldableProductState.HalfOpened, false));
                    Assert.AreEqual(false, FoldableEditorSession.AndroidHalfOpenScreenLandscape(
                        FoldableProductState.HalfOpened, false), "the lock's tall pose");
                    Assert.AreEqual(false, FoldableEditorSession.AndroidHalfOpenHeldScreenLandscape(
                        FoldableDeviceKind.FoldablePhone, FoldableProductState.HalfOpened, false));
                }

                // Runtime Foldable Simulator: not Auto Rotation → the default card.
                var flip = new FoldableHalfOpenRules();
                flip.flip.contentRuns = FoldableContentRuns.TopToDown;
                flip.foldCover.contentRuns = FoldableContentRuns.LeftToRight;
                FoldableSettings.Rules = flip;
                Assert.AreEqual(false, FoldableProductStateResolver.HeldHalfOpenScreenLandscape(
                    FoldableDeviceKind.FoldablePhone, FoldableProductState.HalfOpened, true, true,
                    foldCover: true, autoRotation: false));
                Assert.IsNull(FoldableProductStateResolver.HeldHalfOpenScreenLandscape(
                    FoldableDeviceKind.FlipablePhone, FoldableProductState.HalfOpened, true, true,
                    autoRotation: false));
            }
            finally
            {
                PlayerSettings.defaultInterfaceOrientation = defaultOrientation;
                FoldableSettings.Rules = previousRules;
                FoldableEditorSession.ClearDeviceBinding(FoldableEditorSession.Source.Bridge);
                FoldableEditorSession.ResetProductDefaults();
            }
        }

        // Auto Rotation with exactly one orientation allowed is fixed on the phone (the hold stays off), so the
        // Editor keeps the lock pose whatever the card says; a second orientation on the same axis lets it apply.
        [Test]
        public void ChangedCard_OneAllowedOrientation_KeepsLockPose()
        {
            Assert.IsFalse(FoldableProductStateResolver.AllowsRuntimeHold(false, true, true, true, true), "fixed");
            Assert.IsFalse(FoldableProductStateResolver.AllowsRuntimeHold(true, true, false, false, false), "Portrait only");
            Assert.IsFalse(FoldableProductStateResolver.AllowsRuntimeHold(true, false, false, false, true), "Landscape Right only");
            Assert.IsTrue(FoldableProductStateResolver.AllowsRuntimeHold(true, true, true, false, false), "both portraits");
            Assert.IsTrue(FoldableProductStateResolver.AllowsRuntimeHold(true, false, false, true, true), "both landscapes");
            Assert.IsTrue(FoldableProductStateResolver.AllowsRuntimeHold(true, true, false, true, false), "both axes");

            var profile = FoldableDeviceCatalog.FindById("galaxy-z-fold8-like");
            Assert.NotNull(profile);
            var bound = new FoldableDeviceCatalog.Resolution
            {
                Device = profile,
                Screen = profile.Screens[FoldableEditorSession.PreferredScreenIndex(profile, true)],
                IsKnownFoldable = true,
            };
            var previousRules = FoldableSettings.Rules;

            try
            {
                FoldableEditorSession.ClearDeviceBinding(FoldableEditorSession.Source.Bridge);
                FoldableEditorSession.ResetProductDefaults();
                FoldableEditorSession.SetDeviceBinding(
                    bound, FoldableEditorSession.Source.Bridge, applyDefaults: true);

                var tall = new FoldableHalfOpenRules();
                tall.foldCover.contentRuns = FoldableContentRuns.TopToDown;
                var follow = new FoldableHalfOpenRules();
                follow.foldCover.whileRotating = FoldableHalfOpenRotation.FollowDevice;
                // Only Portrait ticked: the phone keeps Portrait, so the lock's wide pose.
                WithPlayerAxes(portrait: true, landscape: false, () =>
                {
                    Assert.IsTrue(FoldablePlayerOrientation.IsAutoRotation);
                    Assert.IsFalse(FoldablePlayerOrientation.AllowsAndroidHalfOpenHold);
                    foreach (var rules in new[] { tall, follow })
                    {
                        FoldableSettings.Rules = rules;
                        Assert.IsFalse(FoldableEditorSession.AndroidHalfOpenCardChanged(
                            FoldableProductState.HalfOpened, false));
                        Assert.IsFalse(FoldableEditorSession.AndroidHalfOpenFollowsDevice(
                            FoldableProductState.HalfOpened, false));
                        Assert.AreEqual(true, FoldableEditorSession.AndroidHalfOpenScreenLandscape(
                            FoldableProductState.HalfOpened, false), "the lock's wide pose");
                        Assert.AreEqual(true, FoldableEditorSession.AndroidHalfOpenHeldScreenLandscape(
                            FoldableDeviceKind.FoldablePhone, FoldableProductState.HalfOpened, false));
                    }

                    // Portrait and Portrait Upside Down: the hold can act, so the changed card applies.
                    PlayerSettings.allowedAutorotateToPortraitUpsideDown = true;
                    Assert.IsTrue(FoldablePlayerOrientation.AllowsAndroidHalfOpenHold);
                    FoldableSettings.Rules = tall;
                    Assert.IsTrue(FoldableEditorSession.AndroidHalfOpenCardChanged(
                        FoldableProductState.HalfOpened, false));
                    Assert.AreEqual(false, FoldableEditorSession.AndroidHalfOpenScreenLandscape(
                        FoldableProductState.HalfOpened, false), "Top → down: tall");
                });

                // Runtime Foldable Simulator: one allowed orientation → the default card.
                FoldableSettings.Rules = tall;
                Assert.AreEqual(true, FoldableProductStateResolver.HeldHalfOpenScreenLandscape(
                    FoldableDeviceKind.FoldablePhone, FoldableProductState.HalfOpened, false, false,
                    foldCover: true,
                    autoRotation: FoldableProductStateResolver.AllowsRuntimeHold(true, true, false, false, false)));
            }
            finally
            {
                FoldableSettings.Rules = previousRules;
                FoldableEditorSession.ClearDeviceBinding(FoldableEditorSession.Source.Bridge);
                FoldableEditorSession.ResetProductDefaults();
            }
        }

        /// <summary>
        /// Runs <paramref name="body"/> with Auto Rotation allowing only the given Player axes,
        /// then restores every orientation value Player Settings had.
        /// </summary>
        static void WithPlayerAxes(bool portrait, bool landscape, System.Action body)
        {
            UIOrientation defaultOrientation = PlayerSettings.defaultInterfaceOrientation;
            bool portraitUp = PlayerSettings.allowedAutorotateToPortrait;
            bool portraitDown = PlayerSettings.allowedAutorotateToPortraitUpsideDown;
            bool landscapeLeft = PlayerSettings.allowedAutorotateToLandscapeLeft;
            bool landscapeRight = PlayerSettings.allowedAutorotateToLandscapeRight;
            try
            {
                PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
                SetAutorotation(portrait, false, landscape, landscape);
                body();
            }
            finally
            {
                // Default Orientation first: a project with a fixed one can store every flag off,
                // which Unity rejects (forcing Portrait back on) while AutoRotation is still active.
                PlayerSettings.defaultInterfaceOrientation = defaultOrientation;
                SetAutorotation(portraitUp, portraitDown, landscapeLeft, landscapeRight);
            }
        }

        /// <summary>
        /// Enables before it disables: Unity forces Portrait back on (and logs an error) whenever
        /// every autorotation flag is off, which would leave Player Settings changed.
        /// </summary>
        static void SetAutorotation(bool portrait, bool portraitDown, bool landscapeLeft, bool landscapeRight)
        {
            if (portrait)
                PlayerSettings.allowedAutorotateToPortrait = true;
            if (portraitDown)
                PlayerSettings.allowedAutorotateToPortraitUpsideDown = true;
            if (landscapeLeft)
                PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            if (landscapeRight)
                PlayerSettings.allowedAutorotateToLandscapeRight = true;
            if (!portrait)
                PlayerSettings.allowedAutorotateToPortrait = false;
            if (!portraitDown)
                PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            if (!landscapeLeft)
                PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            if (!landscapeRight)
                PlayerSettings.allowedAutorotateToLandscapeRight = false;
        }

        static FoldableOrientation HalfOpenAxis(int width, int height, bool innerLandscapeNative)
        {
            bool landscape = width > height;
            bool tabletopIsLandscape = FoldableProductStateResolver.DefaultLandscapeForHalfOpen(
                FoldableDeviceKind.FoldablePhone, FoldableOrientation.Horizontal);
            FoldableOrientation authored = tabletopIsLandscape == landscape
                ? FoldableOrientation.Horizontal
                : FoldableOrientation.Vertical;
            return FoldableProductStateResolver.ToScreenSpaceHalfOpenHinge(
                FoldableDeviceKind.FoldablePhone,
                FoldableProductState.HalfOpened,
                authored,
                width, height,
                innerLandscapeNative);
        }

        [Test]
        public void DefaultLandscape_FoldablePhone_BookIsWideScreen()
        {
            // Fold Flex with both axes: Book (vertical) picks the wide screen (Portrait pose),
            // Tabletop (horizontal) the tall screen (Landscape pose).
            Assert.IsTrue(FoldableProductStateResolver.DefaultLandscapeForHalfOpen(
                FoldableDeviceKind.FoldablePhone, FoldableOrientation.Vertical));
            Assert.IsFalse(FoldableProductStateResolver.DefaultLandscapeForHalfOpen(
                FoldableDeviceKind.FlipablePhone, FoldableOrientation.Vertical));
            Assert.IsTrue(FoldableProductStateResolver.ResolveDefaultLandscape(
                FoldableDeviceKind.FoldablePhone,
                FoldableOrientation.Vertical,
                FoldableProductState.HalfOpened,
                projectLockedLandscape: null));
            Assert.IsFalse(FoldableProductStateResolver.ResolveDefaultLandscape(
                FoldableDeviceKind.FoldablePhone,
                FoldableOrientation.Horizontal,
                FoldableProductState.HalfOpened,
                projectLockedLandscape: null));
        }

        [Test]
        public void ProductState_Fold_HalfOpenedDisplayName()
        {
            var layout = FoldableProductStateResolver.CreateFromProductState(
                FoldableDeviceKind.FoldablePhone,
                FoldableProductState.HalfOpened,
                2176, 1812,
                halfOpenHinge: FoldableOrientation.Vertical);
            Assert.AreEqual("Expand Half Opened", layout.GetProductStateDisplayName());
            Assert.AreEqual(FoldablePosture.Book, layout.ResolvePosture());
        }

        [Test]
        public void ProductState_IndexRoundTrip()
        {
            Assert.AreEqual(FoldableProductState.Closed, FoldableProductStateResolver.FromIndex(0));
            Assert.AreEqual(FoldableProductState.Opened, FoldableProductStateResolver.FromIndex(1));
            Assert.AreEqual(FoldableProductState.HalfOpened, FoldableProductStateResolver.FromIndex(2));
            Assert.AreEqual(0, FoldableProductStateResolver.ToIndex(FoldableProductState.Closed));
            Assert.AreEqual(1, FoldableProductStateResolver.ToIndex(FoldableProductState.Opened));
            Assert.AreEqual(2, FoldableProductStateResolver.ToIndex(FoldableProductState.HalfOpened));
        }

        [Test]
        public void InferCoverScreen_WhenMuchSmallerThanMax()
        {
            var layout = new FoldableLayoutState
            {
                currentMetrics = new RectInt(0, 0, 720, 748),
                maxMetrics = new RectInt(0, 0, 1080, 2640),
                state = FoldableState.Undefined
            };
            Assert.IsTrue(FoldableProductStateResolver.InferCoverScreen(layout));
        }

        [Test]
        public void InferCoverScreen_ViaSessionPeak_WhenCurrentEqualsMax()
        {
            // Cover display: current==max, but session remembered inner size.
            var layout = new FoldableLayoutState
            {
                currentMetrics = new RectInt(0, 0, 832, 2268),
                maxMetrics = new RectInt(0, 0, 832, 2268),
                peakMaxMetrics = new RectInt(0, 0, 1812, 2176),
                state = FoldableState.Undefined,
                productState = FoldableProductState.Undefined
            };
            Assert.IsTrue(FoldableProductStateResolver.InferCoverScreen(layout));
            FoldableProductStateResolver.Enrich(layout, FoldableDeviceKind.FoldablePhone);
            Assert.AreEqual(FoldableProductState.Closed, layout.productState);
        }

        [Test]
        public void InferCoverScreen_ViaLargestDisplay()
        {
            var layout = new FoldableLayoutState
            {
                currentMetrics = new RectInt(0, 0, 968, 2376),
                maxMetrics = new RectInt(0, 0, 968, 2376),
                largestDisplayMetrics = new RectInt(0, 0, 1856, 2160),
                state = FoldableState.Undefined
            };
            Assert.IsTrue(FoldableProductStateResolver.InferCoverScreen(layout));
        }

        [Test]
        public void InferCoverScreen_ViaDeviceKindOpenSize_TriFoldCover()
        {
            // TriFold cover is ~80% of its own inner area; kind defaults still detect via ratio/soft rule.
            var layout = new FoldableLayoutState
            {
                currentMetrics = new RectInt(0, 0, 1080, 2520),
                maxMetrics = new RectInt(0, 0, 1080, 2520),
                state = FoldableState.Undefined,
                deviceKind = FoldableDeviceKind.FoldablePhone,
                productState = FoldableProductState.Undefined
            };
            FoldableProductStateResolver.Enrich(layout, FoldableDeviceKind.FoldablePhone);
            Assert.AreEqual(FoldableProductState.Closed, layout.productState);
        }

        [Test]
        public void InferCoverScreen_ViaOwnPeak_TriFoldCover()
        {
            var layout = new FoldableLayoutState
            {
                currentMetrics = new RectInt(0, 0, 1080, 2520),
                maxMetrics = new RectInt(0, 0, 1080, 2520),
                peakMaxMetrics = new RectInt(0, 0, 1584, 2160),
                state = FoldableState.Undefined,
                productState = FoldableProductState.Undefined
            };
            Assert.IsTrue(FoldableProductStateResolver.InferCoverScreen(layout));
        }

        [Test]
        public void InferCoverScreen_RegularPhone_RemainsUndefined()
        {
            var layout = new FoldableLayoutState
            {
                currentMetrics = new RectInt(0, 0, 1080, 2400),
                maxMetrics = new RectInt(0, 0, 1080, 2400),
                state = FoldableState.Undefined,
                productState = FoldableProductState.Undefined
            };
            FoldableProductStateResolver.Enrich(layout, FoldableDeviceKind.Unknown);
            Assert.IsFalse(layout.isCoverScreen);
            Assert.AreEqual(FoldableProductState.Undefined, layout.productState);
        }

        [Test]
        public void NativeDetect_Fold_NoFoldingFeature_WithoutKind_IsNotClosed()
        {
            // Missing FoldingFeature alone is ambiguous without Device Kind / peak / display refs.
            var layout = new FoldableLayoutState
            {
                currentMetrics = new RectInt(0, 0, 832, 2268),
                maxMetrics = new RectInt(0, 0, 832, 2268),
                state = FoldableState.Undefined,
                productState = FoldableProductState.Undefined
            };
            FoldableProductStateResolver.Enrich(layout, FoldableDeviceKind.Unknown);
            Assert.IsFalse(layout.isCoverScreen);
            Assert.AreEqual(FoldableProductState.Undefined, layout.productState);
        }

        [Test]
        public void NativeDetect_RearDisplayActive_IsClosedNormal()
        {
            var layout = new FoldableLayoutState
            {
                currentMetrics = new RectInt(0, 0, 832, 2268),
                maxMetrics = new RectInt(0, 0, 832, 2268),
                state = FoldableState.Undefined,
                isCoverScreen = true, // WindowArea rear ACTIVE from native
                productState = FoldableProductState.Undefined
            };
            FoldableProductStateResolver.Enrich(layout, FoldableDeviceKind.FoldablePhone);
            Assert.AreEqual(FoldableProductState.Closed, layout.productState);
            Assert.AreEqual("Normal", layout.GetProductStateDisplayName());
        }

        [Test]
        public void NativeDetect_RearDisplayActive_WhileFlat_KeepsClosedNormal()
        {
            // Samsung rear-display mode: device can remain FLAT/open while the activity is on cover.
            var layout = new FoldableLayoutState
            {
                currentMetrics = new RectInt(0, 0, 832, 2268),
                maxMetrics = new RectInt(0, 0, 1812, 2176),
                peakMaxMetrics = new RectInt(0, 0, 1812, 2176),
                state = FoldableState.Flat,
                isCoverScreen = true,
                productState = FoldableProductState.Undefined
            };
            FoldableProductStateResolver.Enrich(layout, FoldableDeviceKind.FoldablePhone);
            Assert.IsTrue(layout.isCoverScreen);
            Assert.AreEqual(FoldableProductState.Closed, layout.productState);
            Assert.AreEqual("Normal", layout.GetProductStateDisplayName());
        }

        [Test]
        public void NativeDetect_Fold_CurrentMuchSmallerThanMax_IsClosed()
        {
            var layout = new FoldableLayoutState
            {
                currentMetrics = new RectInt(0, 0, 832, 2268),
                maxMetrics = new RectInt(0, 0, 1812, 2176),
                state = FoldableState.Undefined,
                productState = FoldableProductState.Undefined
            };
            FoldableProductStateResolver.Enrich(layout, FoldableDeviceKind.FoldablePhone);
            Assert.IsTrue(layout.isCoverScreen);
            Assert.AreEqual(FoldableProductState.Closed, layout.productState);
        }

        [Test]
        public void NativeDetect_Fold_Flat_IsExpand()
        {
            var layout = new FoldableLayoutState
            {
                currentMetrics = new RectInt(0, 0, 1812, 2176),
                maxMetrics = new RectInt(0, 0, 1812, 2176),
                state = FoldableState.Flat,
                orientation = FoldableOrientation.Vertical,
                productState = FoldableProductState.Undefined
            };
            FoldableProductStateResolver.Enrich(layout, FoldableDeviceKind.FoldablePhone);
            Assert.IsFalse(layout.isCoverScreen);
            Assert.AreEqual(FoldableProductState.Opened, layout.productState);
            Assert.AreEqual("Expand", layout.GetProductStateDisplayName());
            Assert.AreEqual(FoldablePosture.Flat, layout.ResolvePosture());
        }

        [Test]
        public void NativeDetect_Fold_HalfOpened_IsExpandHalfOpened()
        {
            var layout = new FoldableLayoutState
            {
                currentMetrics = new RectInt(0, 0, 1812, 2176),
                maxMetrics = new RectInt(0, 0, 1812, 2176),
                state = FoldableState.HalfOpened,
                orientation = FoldableOrientation.Horizontal,
                foldBounds = new RectInt(0, 1076, 1812, 24),
                productState = FoldableProductState.Undefined
            };
            FoldableProductStateResolver.Enrich(layout, FoldableDeviceKind.FoldablePhone);
            Assert.IsFalse(layout.isCoverScreen);
            Assert.AreEqual(FoldableProductState.HalfOpened, layout.productState);
            Assert.AreEqual("Expand Half Opened", layout.GetProductStateDisplayName());
            Assert.AreEqual(FoldablePosture.Tabletop, layout.ResolvePosture());
        }

        [Test]
        public void NativeDetect_Fold_NoFeatureAfterHalfOpened_KeepsHalfOpened()
        {
            var layout = new FoldableLayoutState
            {
                currentMetrics = new RectInt(0, 0, 1812, 2176),
                maxMetrics = new RectInt(0, 0, 1812, 2176),
                state = FoldableState.Undefined,
                productState = FoldableProductState.Undefined
            };
            FoldableProductStateResolver.Enrich(
                layout,
                FoldableDeviceKind.FoldablePhone,
                FoldableProductState.HalfOpened,
                FoldableOrientation.Horizontal);
            Assert.IsFalse(layout.isCoverScreen);
            Assert.AreEqual(FoldableProductState.HalfOpened, layout.productState);
            Assert.AreEqual(FoldableOrientation.Horizontal, layout.orientation);
            Assert.IsTrue(
                FoldableProductStateResolver.ShouldPreserveHalfOpened(
                    new FoldableLayoutState
                    {
                        state = FoldableState.Undefined,
                        productState = FoldableProductState.Opened
                    },
                    FoldableProductState.HalfOpened));
        }

        [Test]
        public void NativeDetect_Fold_FlatAfterHalfOpened_IsExpand()
        {
            var layout = new FoldableLayoutState
            {
                currentMetrics = new RectInt(0, 0, 1812, 2176),
                maxMetrics = new RectInt(0, 0, 1812, 2176),
                state = FoldableState.Flat,
                productState = FoldableProductState.Undefined
            };
            FoldableProductStateResolver.Enrich(
                layout,
                FoldableDeviceKind.FoldablePhone,
                FoldableProductState.HalfOpened);
            Assert.AreEqual(FoldableProductState.Opened, layout.productState);
        }

        [Test]
        public void NativeDetect_Fold_CoverAfterHalfOpened_IsClosed()
        {
            var layout = new FoldableLayoutState
            {
                currentMetrics = new RectInt(0, 0, 832, 2268),
                maxMetrics = new RectInt(0, 0, 1812, 2176),
                state = FoldableState.Undefined,
                productState = FoldableProductState.Undefined
            };
            FoldableProductStateResolver.Enrich(
                layout,
                FoldableDeviceKind.FoldablePhone,
                FoldableProductState.HalfOpened);
            Assert.IsTrue(layout.isCoverScreen);
            Assert.AreEqual(FoldableProductState.Closed, layout.productState);
        }

        [Test]
        public void NativeDetect_Fold_CoverAfterHalfOpened_ViaMerge_ClearsStaleFlex()
        {
            // Flex→Normal: empty FoldingFeature must not keep HalfOpened state or kind
            // open-size cover detection never runs (requires Undefined).
            var live = FoldableLayoutState.CreateSimulated(
                FoldableState.HalfOpened, FoldableOrientation.Horizontal, 1812, 2176, 0.5f, 24);
            live.deviceKind = FoldableDeviceKind.FoldablePhone;
            live.productState = FoldableProductState.HalfOpened;
            live.peakMaxMetrics = new RectInt(0, 0, 1812, 2176);

            var native = new FoldableLayoutState
            {
                state = FoldableState.Undefined,
                productState = FoldableProductState.Undefined,
                currentMetrics = new RectInt(0, 0, 1080, 2520),
                maxMetrics = new RectInt(0, 0, 1080, 2520),
                peakMaxMetrics = new RectInt(0, 0, 0, 0)
            };

            live.MergeNativeSnapshot(native);
            Assert.AreEqual(FoldableState.Undefined, live.state);

            FoldableProductStateResolver.Enrich(
                live,
                FoldableDeviceKind.FoldablePhone,
                FoldableProductState.HalfOpened,
                FoldableOrientation.Horizontal);

            Assert.IsTrue(live.isCoverScreen);
            Assert.AreEqual(FoldableProductState.Closed, live.productState);
            Assert.AreEqual("Normal", live.GetProductStateDisplayName());
            Assert.AreNotEqual(FoldableState.HalfOpened, live.state);
        }

        [Test]
        public void NativeDetect_Fold_HalfOpened_ForcesIsSeparating()
        {
            // Google: HALF_OPENED always reports isSeparating true.
            var layout = new FoldableLayoutState
            {
                currentMetrics = new RectInt(0, 0, 1812, 2176),
                maxMetrics = new RectInt(0, 0, 1812, 2176),
                state = FoldableState.HalfOpened,
                orientation = FoldableOrientation.Horizontal,
                isSeparating = false,
                productState = FoldableProductState.Undefined
            };
            FoldableProductStateResolver.Enrich(layout, FoldableDeviceKind.FoldablePhone);
            Assert.AreEqual(FoldableProductState.HalfOpened, layout.productState);
            Assert.IsTrue(layout.isSeparating);
        }

        [Test]
        public void NativeDetect_Flip_NoFoldingFeature_SmallCover_IsClosed()
        {
            // Flip cover size heuristic (near-square, small) when no FoldingFeature.
            var layout = new FoldableLayoutState
            {
                currentMetrics = new RectInt(0, 0, 720, 748),
                maxMetrics = new RectInt(0, 0, 720, 748),
                state = FoldableState.Undefined,
                productState = FoldableProductState.Undefined
            };
            FoldableProductStateResolver.Enrich(layout, FoldableDeviceKind.FlipablePhone);
            Assert.AreEqual(FoldableProductState.Closed, layout.productState);
            Assert.AreEqual("Closed", layout.GetProductStateDisplayName());
        }

        [Test]
        public void NativeDetect_Flip_Flat_IsOpened()
        {
            var layout = new FoldableLayoutState
            {
                currentMetrics = new RectInt(0, 0, 1080, 2640),
                maxMetrics = new RectInt(0, 0, 1080, 2640),
                state = FoldableState.Flat,
                productState = FoldableProductState.Undefined
            };
            FoldableProductStateResolver.Enrich(layout, FoldableDeviceKind.FlipablePhone);
            Assert.AreEqual(FoldableProductState.Opened, layout.productState);
            Assert.AreEqual("Opened", layout.GetProductStateDisplayName());
        }

        [Test]
        public void NativeDetect_Flip_HalfOpened_IsHalfOpened()
        {
            var layout = new FoldableLayoutState
            {
                currentMetrics = new RectInt(0, 0, 1080, 2640),
                maxMetrics = new RectInt(0, 0, 1080, 2640),
                state = FoldableState.HalfOpened,
                orientation = FoldableOrientation.Horizontal,
                productState = FoldableProductState.Undefined
            };
            FoldableProductStateResolver.Enrich(layout, FoldableDeviceKind.FlipablePhone);
            Assert.AreEqual(FoldableProductState.HalfOpened, layout.productState);
            Assert.AreEqual("Half Opened", layout.GetProductStateDisplayName());
            Assert.AreEqual(FoldablePosture.Tabletop, layout.ResolvePosture());
        }

        [Test]
        public void NativeDetect_Dual_FlatNotSeparating_IsSinglePane()
        {
            var layout = new FoldableLayoutState
            {
                currentMetrics = new RectInt(0, 0, 675, 1800),
                maxMetrics = new RectInt(0, 0, 1350, 1800),
                state = FoldableState.Flat,
                orientation = FoldableOrientation.Vertical,
                isSeparating = false,
                productState = FoldableProductState.Undefined
            };
            FoldableProductStateResolver.Enrich(layout, FoldableDeviceKind.DualScreen);
            Assert.AreEqual(FoldableProductState.Closed, layout.productState);
            Assert.AreEqual("Single pane", layout.GetProductStateDisplayName());
        }

        [Test]
        public void NativeDetect_Dual_NoFoldingFeature_IsSinglePane()
        {
            // App on one Duo screen: typically no FoldingFeature in the window.
            var layout = new FoldableLayoutState
            {
                currentMetrics = new RectInt(0, 0, 675, 1800),
                maxMetrics = new RectInt(0, 0, 675, 1800),
                state = FoldableState.Undefined,
                isSeparating = false,
                productState = FoldableProductState.Undefined
            };
            FoldableProductStateResolver.Enrich(layout, FoldableDeviceKind.DualScreen);
            Assert.AreEqual(FoldableProductState.Closed, layout.productState);
            Assert.AreEqual("Single pane", layout.GetProductStateDisplayName());
        }

        [Test]
        public void NativeDetect_Dual_FlatSeparating_IsSpanned()
        {
            var layout = new FoldableLayoutState
            {
                currentMetrics = new RectInt(0, 0, 1350, 1800),
                maxMetrics = new RectInt(0, 0, 1350, 1800),
                state = FoldableState.Flat,
                orientation = FoldableOrientation.Vertical,
                isSeparating = true,
                productState = FoldableProductState.Undefined
            };
            FoldableProductStateResolver.Enrich(layout, FoldableDeviceKind.DualScreen);
            Assert.AreEqual(FoldableProductState.Opened, layout.productState);
            Assert.AreEqual("Spanned", layout.GetProductStateDisplayName());
            Assert.AreEqual(FoldablePosture.Flat, layout.ResolvePosture());
        }

        [Test]
        public void NativeDetect_UnknownPhone_NoFeature_IsNotForcedClosed()
        {
            // Regular phones also lack FoldingFeature — do not treat as cover.
            var layout = new FoldableLayoutState
            {
                currentMetrics = new RectInt(0, 0, 1080, 2400),
                maxMetrics = new RectInt(0, 0, 1080, 2400),
                state = FoldableState.Undefined,
                productState = FoldableProductState.Undefined
            };
            FoldableProductStateResolver.Enrich(layout, FoldableDeviceKind.Unknown);
            Assert.IsFalse(layout.isCoverScreen);
            Assert.AreEqual(FoldableProductState.Undefined, layout.productState);
        }

        [Test]
        public void Metrics_ZFold_ClosedCover()
        {
            var layout = FoldableProductStateResolver.CreateFromProductState(
                FoldableDeviceKind.FoldablePhone,
                FoldableProductState.Closed,
                832, 2268);
            Assert.AreEqual(832, layout.currentMetrics.width);
            Assert.AreEqual(2268, layout.currentMetrics.height);
            Assert.IsTrue(layout.isCoverScreen);
            Assert.AreEqual("Normal", layout.GetProductStateDisplayName());
            Assert.AreEqual(FoldablePosture.Flat, layout.ResolvePosture());
        }

        [Test]
        public void Metrics_ZFold_ExpandInner()
        {
            var layout = FoldableProductStateResolver.CreateFromProductState(
                FoldableDeviceKind.FoldablePhone,
                FoldableProductState.Opened,
                1812, 2176);
            Assert.AreEqual(1812, layout.currentMetrics.width);
            Assert.AreEqual(2176, layout.currentMetrics.height);
            Assert.IsFalse(layout.isCoverScreen);
            Assert.AreEqual(FoldablePosture.Flat, layout.ResolvePosture());
        }

        [Test]
        public void Metrics_ZFold_HalfOpenedTabletop_UsesInnerSize()
        {
            var layout = FoldableProductStateResolver.CreateFromProductState(
                FoldableDeviceKind.FoldablePhone,
                FoldableProductState.HalfOpened,
                1812, 2176,
                halfOpenHinge: FoldableOrientation.Horizontal);
            Assert.AreEqual(1812, layout.currentMetrics.width);
            Assert.AreEqual(2176, layout.currentMetrics.height);
            Assert.AreEqual(FoldablePosture.Tabletop, layout.ResolvePosture());
            Assert.AreEqual(1812, layout.foldBounds.width);
            Assert.Greater(layout.foldBounds.y, 0);
            Assert.Less(layout.foldBounds.yMax, 2176);
        }

        [Test]
        public void Metrics_PixelFold_InnerLandscapePanel()
        {
            var layout = FoldableProductStateResolver.CreateFromProductState(
                FoldableDeviceKind.FoldablePhone,
                FoldableProductState.Opened,
                2208, 1840);
            Assert.AreEqual(2208, layout.currentMetrics.width);
            Assert.AreEqual(1840, layout.currentMetrics.height);
            Assert.AreEqual("Expand", layout.GetProductStateDisplayName());
        }

        [Test]
        public void Metrics_ZFlip6_ClosedAndOpened()
        {
            var closed = FoldableProductStateResolver.CreateFromProductState(
                FoldableDeviceKind.FlipablePhone,
                FoldableProductState.Closed,
                720, 748);
            Assert.AreEqual("Closed", closed.GetProductStateDisplayName());
            Assert.AreEqual(720, closed.currentMetrics.width);

            var opened = FoldableProductStateResolver.CreateFromProductState(
                FoldableDeviceKind.FlipablePhone,
                FoldableProductState.Opened,
                1080, 2640);
            Assert.AreEqual("Opened", opened.GetProductStateDisplayName());
            Assert.AreEqual(2640, opened.currentMetrics.height);
        }

        [Test]
        public void Metrics_Dual_SingleAndSpanned()
        {
            var single = FoldableProductStateResolver.CreateFromProductState(
                FoldableDeviceKind.DualScreen,
                FoldableProductState.Closed,
                675, 1800);
            Assert.AreEqual("Single pane", single.GetProductStateDisplayName());
            Assert.AreEqual(FoldablePosture.Flat, single.ResolvePosture());

            var spanned = FoldableProductStateResolver.CreateFromProductState(
                FoldableDeviceKind.DualScreen,
                FoldableProductState.Opened,
                1350, 1800);
            Assert.AreEqual("Spanned", spanned.GetProductStateDisplayName());
            Assert.IsTrue(spanned.isSeparating);
            // Spanned dual-screen uses the full surface; Book/Tabletop is Flex (HalfOpened).
            Assert.AreEqual(FoldablePosture.Flat, spanned.ResolvePosture());
        }

        [Test]
        public void Dual_SpannedLandscape_StaysFlat()
        {
            var spanned = FoldableProductStateResolver.CreateFromProductState(
                FoldableDeviceKind.DualScreen,
                FoldableProductState.Opened,
                1800, 1350);
            Assert.AreEqual("Spanned", spanned.GetProductStateDisplayName());
            Assert.AreEqual(FoldablePosture.Flat, spanned.ResolvePosture());
        }

        [Test]
        public void Dual_FlexHalfOpened_IsTabletopOrBook()
        {
            var tabletop = FoldableProductStateResolver.CreateFromProductState(
                FoldableDeviceKind.DualScreen,
                FoldableProductState.HalfOpened,
                1350, 1800,
                halfOpenHinge: FoldableOrientation.Horizontal);
            Assert.AreEqual(FoldablePosture.Tabletop, tabletop.ResolvePosture());

            var book = FoldableProductStateResolver.CreateFromProductState(
                FoldableDeviceKind.DualScreen,
                FoldableProductState.HalfOpened,
                1350, 1800,
                halfOpenHinge: FoldableOrientation.Vertical);
            Assert.AreEqual(FoldablePosture.Book, book.ResolvePosture());
        }

        [Test]
        public void Metrics_Generic_HalfOpenedKeepsMainSize()
        {
            var layout = FoldableProductStateResolver.CreateFromProductState(
                FoldableDeviceKind.FoldablePhone,
                FoldableProductState.HalfOpened,
                1080, 2316);
            Assert.AreEqual(1080, layout.currentMetrics.width);
            Assert.AreEqual(2316, layout.currentMetrics.height);
            Assert.AreEqual(FoldablePosture.Tabletop, layout.ResolvePosture());
        }

        [Test]
        public void Enrich_DoesNotOverwriteExplicitProductState()
        {
            var layout = FoldableProductStateResolver.CreateFromProductState(
                FoldableDeviceKind.FoldablePhone,
                FoldableProductState.HalfOpened,
                1812, 2176);
            // Stale cover flag must not wipe HalfOpened once productState is set.
            layout.isCoverScreen = true;
            FoldableProductStateResolver.Enrich(layout, FoldableDeviceKind.FoldablePhone);
            Assert.AreEqual(FoldableProductState.HalfOpened, layout.productState);
            Assert.IsFalse(layout.isCoverScreen);
        }

        [Test]
        public void Enrich_ExplicitOpened_ClearsCoverInference_EvenWithCoverMetrics()
        {
            var layout = FoldableProductStateResolver.CreateFromProductState(
                FoldableDeviceKind.FlipablePhone,
                FoldableProductState.Opened,
                720, 748);
            layout.productState = FoldableProductState.Opened;
            layout.isCoverScreen = false;
            FoldableProductStateResolver.Enrich(layout, FoldableDeviceKind.FlipablePhone);
            Assert.IsFalse(layout.isCoverScreen);
            Assert.AreEqual(FoldableProductState.Opened, layout.productState);
            Assert.AreEqual(FoldablePosture.Flat, layout.ResolvePosture());
        }

        [Test]
        public void Enrich_ExplicitClosed_ForcesCover_EvenWithMainMetrics()
        {
            var layout = FoldableProductStateResolver.CreateFromProductState(
                FoldableDeviceKind.FlipablePhone,
                FoldableProductState.Closed,
                1080, 2640);
            layout.productState = FoldableProductState.Closed;
            FoldableProductStateResolver.Enrich(layout, FoldableDeviceKind.FlipablePhone);
            Assert.IsTrue(layout.isCoverScreen);
            Assert.AreEqual(FoldablePosture.Flat, layout.ResolvePosture());
        }

        [Test]
        public void Metrics_GetProduct_FlipClosedAndOpened()
        {
            Assert.IsTrue(FoldableProductStateResolver.GetProductMetrics(
                FoldableDeviceKind.FlipablePhone, FoldableProductState.Closed, out int cw, out int ch));
            Assert.AreEqual(720, cw);
            Assert.AreEqual(748, ch);

            Assert.IsTrue(FoldableProductStateResolver.GetProductMetrics(
                FoldableDeviceKind.FlipablePhone, FoldableProductState.Opened, out int ow, out int oh));
            Assert.AreEqual(1080, ow);
            Assert.AreEqual(2640, oh);
        }

        [Test]
        public void Metrics_ApplyOriented_PortraitAndLandscapeAreIdempotent()
        {
            int w = 1812;
            int h = 2176;
            FoldableProductStateResolver.ApplyOrientedMetrics(true, ref w, ref h);
            Assert.AreEqual(2176, w);
            Assert.AreEqual(1812, h);

            // Already landscape — do not swap again.
            FoldableProductStateResolver.ApplyOrientedMetrics(true, ref w, ref h);
            Assert.AreEqual(2176, w);
            Assert.AreEqual(1812, h);

            // Landscape → portrait must convert back (previous bug left landscape stuck).
            FoldableProductStateResolver.ApplyOrientedMetrics(false, ref w, ref h);
            Assert.AreEqual(1812, w);
            Assert.AreEqual(2176, h);

            FoldableProductStateResolver.ApplyOrientedMetrics(false, ref w, ref h);
            Assert.AreEqual(1812, w);
            Assert.AreEqual(2176, h);
        }

        [Test]
        public void NoteInnerPeak_DoesNotShrinkRememberedPeak()
        {
            var layout = new FoldableLayoutState
            {
                state = FoldableState.Flat,
                peakMaxMetrics = new RectInt(0, 0, 2200, 1800),
                currentMetrics = new RectInt(0, 0, 1080, 2400),
                maxMetrics = new RectInt(0, 0, 1080, 2400)
            };

            FoldableCoverHeuristics.NoteInnerPeak(layout);

            Assert.AreEqual(new RectInt(0, 0, 2200, 1800), layout.peakMaxMetrics);
        }

        [Test]
        public void MergeNativeSnapshot_PreservesLargerPeakAndWindowModeSignals()
        {
            var destination = new FoldableLayoutState
            {
                peakMaxMetrics = new RectInt(0, 0, 2200, 1800)
            };
            var source = new FoldableLayoutState
            {
                peakMaxMetrics = new RectInt(0, 0, 1080, 2400),
                isInMultiWindowMode = true,
                isInPictureInPictureMode = true
            };

            destination.MergeNativeSnapshot(source);

            Assert.AreEqual(new RectInt(0, 0, 2200, 1800), destination.peakMaxMetrics);
            Assert.IsTrue(destination.isInMultiWindowMode);
            Assert.IsTrue(destination.isInPictureInPictureMode);
        }

        [TestCase(true, false)]
        [TestCase(false, true)]
        public void InferCoverScreen_SmallWindowModeSurface_IsNotClosed(
            bool isInMultiWindowMode,
            bool isInPictureInPictureMode)
        {
            var layout = new FoldableLayoutState
            {
                deviceKind = FoldableDeviceKind.FlipablePhone,
                state = FoldableState.Undefined,
                currentMetrics = new RectInt(0, 0, 600, 600),
                maxMetrics = new RectInt(0, 0, 1080, 2640),
                isInMultiWindowMode = isInMultiWindowMode,
                isInPictureInPictureMode = isInPictureInPictureMode
            };

            Assert.IsFalse(FoldableCoverHeuristics.InferCoverScreen(layout));
            FoldableProductStateResolver.Enrich(layout, FoldableDeviceKind.FlipablePhone);
            Assert.AreEqual(FoldableProductState.Undefined, layout.productState);
            Assert.IsFalse(layout.isCoverScreen);
        }

        [Test]
        public void PathUtil_ContainedPathsCannotEscapePackageRoot()
        {
            string root = Path.Combine(Path.GetTempPath(), "FoldAndFlipPackage");

            Assert.IsTrue(ResponsiveUiEditorPath.CombineContained(
                root, "Editor/../Runtime/file.txt", out string contained));
            Assert.IsTrue(ResponsiveUiEditorPath.IsContained(root, contained));
            Assert.IsFalse(ResponsiveUiEditorPath.CombineContained(
                root, "../outside.txt", out _));
            Assert.IsFalse(ResponsiveUiEditorPath.CombineContained(
                root, Path.GetFullPath(Path.Combine(root, "..", "outside.txt")), out _));
        }

        [Test]
        public void PathUtil_AssetPrefixDoesNotAcceptSiblingDirectory()
        {
            string sibling = Application.dataPath + "Backup/file.txt";

            Assert.IsNull(ResponsiveUiEditorPath.ToAssetPath(sibling));
            Assert.IsNull(ResponsiveUiEditorPath.ToProjectRelativePath(sibling));
        }

        [Test]
        public void PathUtil_ToProjectRelativePath_MapsAssetsAndRejectsOutside()
        {
            string underAssets = Path.Combine(Application.dataPath, "ResponsiveUI", "FoldAndFlip");
            Assert.AreEqual(
                "Assets/ResponsiveUI/FoldAndFlip",
                ResponsiveUiEditorPath.ToProjectRelativePath(underAssets));
            Assert.AreEqual(
                "Assets/ResponsiveUI/FoldAndFlip",
                ResponsiveUiEditorPath.ToAssetPath(underAssets));

            // Mixed separators must still resolve (Windows Editors often produce '\\').
            string mixed = Path.Combine(Application.dataPath, "ResponsiveUI", "FoldAndFlip")
                .Replace('/', Path.DirectorySeparatorChar);
            Assert.AreEqual(
                "Assets/ResponsiveUI/FoldAndFlip",
                ResponsiveUiEditorPath.ToProjectRelativePath(mixed));

            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string underLibrary = Path.Combine(projectRoot ?? ".", "Library", "Something");
            Assert.IsNull(ResponsiveUiEditorPath.ToProjectRelativePath(underLibrary));
            Assert.IsNull(ResponsiveUiEditorPath.ToAssetPath(underLibrary));
        }

        [Test]
        public void SetDeviceBinding_SwitchProfileWithoutDefaults_KeepsHalfOpened()
        {
            FoldableDeviceCatalog.DeviceProfile first = null;
            FoldableDeviceCatalog.DeviceProfile second = null;
            FoldableDeviceCatalog.DeviceProfile[] devices = FoldableDeviceCatalog.Devices;
            for (int i = 0; i < devices.Length; i++)
            {
                FoldableDeviceCatalog.DeviceProfile device = devices[i];
                if (device.Screens == null || device.Screens.Length == 0)
                    continue;
                if (first == null)
                {
                    first = device;
                    continue;
                }

                if (device.FriendlyName != first.FriendlyName)
                {
                    second = device;
                    break;
                }
            }

            Assert.NotNull(first, "catalog needs a Fold/Flip profile");
            Assert.NotNull(second, "catalog needs a second Fold/Flip profile");

            var from = new FoldableDeviceCatalog.Resolution
            {
                Device = first,
                Screen = first.Screens[0],
                IsKnownFoldable = true,
            };
            var to = new FoldableDeviceCatalog.Resolution
            {
                Device = second,
                Screen = second.Screens[0],
                IsKnownFoldable = true,
            };

            try
            {
                FoldableEditorSession.ClearDeviceBinding(FoldableEditorSession.Source.Bridge);
                FoldableEditorSession.ResetProductDefaults();
                FoldableEditorSession.SetDeviceBinding(
                    from, FoldableEditorSession.Source.Bridge, applyDefaults: true);
                FoldableEditorSession.PublishProductState(
                    FoldableProductState.HalfOpened,
                    FoldableOrientation.Horizontal,
                    0.5f,
                    24,
                    true,
                    FoldableEditorSession.MetricsWidth,
                    FoldableEditorSession.MetricsHeight,
                    FoldableEditorSession.Source.Simulator);
                Assert.AreEqual(
                    FoldableProductState.HalfOpened,
                    FoldableEditorSession.ProductState);

                FoldableEditorSession.SetDeviceBinding(
                    to, FoldableEditorSession.Source.Bridge, applyDefaults: false);
                Assert.AreEqual(
                    FoldableProductState.HalfOpened,
                    FoldableEditorSession.ProductState);
                Assert.AreEqual(FoldableState.HalfOpened, FoldableEditorSession.State);
            }
            finally
            {
                FoldableEditorSession.ClearDeviceBinding(FoldableEditorSession.Source.Bridge);
                FoldableEditorSession.ResetProductDefaults();
            }
        }

        [Test]
        public void SetDeviceBinding_ApplyDefaultsAfterUnbind_KeepsHalfOpened()
        {
            FoldableDeviceCatalog.DeviceProfile first = null;
            FoldableDeviceCatalog.DeviceProfile second = null;
            FoldableDeviceCatalog.DeviceProfile[] devices = FoldableDeviceCatalog.Devices;
            for (int i = 0; i < devices.Length; i++)
            {
                FoldableDeviceCatalog.DeviceProfile device = devices[i];
                if (device.Screens == null || device.Screens.Length == 0)
                    continue;
                if (first == null)
                {
                    first = device;
                    continue;
                }

                if (device.FriendlyName != first.FriendlyName)
                {
                    second = device;
                    break;
                }
            }

            Assert.NotNull(first, "catalog needs a Fold/Flip profile");
            Assert.NotNull(second, "catalog needs a second Fold/Flip profile");

            var from = new FoldableDeviceCatalog.Resolution
            {
                Device = first,
                Screen = first.Screens[0],
                IsKnownFoldable = true,
            };
            var to = new FoldableDeviceCatalog.Resolution
            {
                Device = second,
                Screen = second.Screens[0],
                IsKnownFoldable = true,
            };

            try
            {
                FoldableEditorSession.ClearDeviceBinding(FoldableEditorSession.Source.Bridge);
                FoldableEditorSession.ResetProductDefaults();
                FoldableEditorSession.SetDeviceBinding(
                    from, FoldableEditorSession.Source.Bridge, applyDefaults: true);
                FoldableEditorSession.PublishProductState(
                    FoldableProductState.HalfOpened,
                    FoldableOrientation.Horizontal,
                    0.5f,
                    24,
                    true,
                    FoldableEditorSession.MetricsWidth,
                    FoldableEditorSession.MetricsHeight,
                    FoldableEditorSession.Source.Simulator);

                FoldableEditorSession.ClearDeviceBinding(FoldableEditorSession.Source.Bridge);
                Assert.AreEqual(
                    FoldableProductState.HalfOpened,
                    FoldableEditorSession.ProductState);

                FoldableEditorSession.SetDeviceBinding(
                    to, FoldableEditorSession.Source.Bridge, applyDefaults: true);
                Assert.AreEqual(
                    FoldableProductState.HalfOpened,
                    FoldableEditorSession.ProductState);
                Assert.AreEqual(FoldableState.HalfOpened, FoldableEditorSession.State);
            }
            finally
            {
                FoldableEditorSession.ClearDeviceBinding(FoldableEditorSession.Source.Bridge);
                FoldableEditorSession.ResetProductDefaults();
            }
        }

        [Test]
        public void SimulateDomainReload_KeepsHalfOpened()
        {
            FoldableDeviceCatalog.DeviceProfile device = null;
            FoldableDeviceCatalog.DeviceProfile[] devices = FoldableDeviceCatalog.Devices;
            for (int i = 0; i < devices.Length; i++)
            {
                if (devices[i].Screens == null || devices[i].Screens.Length == 0)
                    continue;
                device = devices[i];
                break;
            }

            Assert.NotNull(device, "catalog needs a Fold/Flip profile");

            var bound = new FoldableDeviceCatalog.Resolution
            {
                Device = device,
                Screen = device.Screens[0],
                IsKnownFoldable = true,
            };

            try
            {
                FoldableEditorSession.ClearDeviceBinding(FoldableEditorSession.Source.Bridge);
                FoldableEditorSession.ResetProductDefaults();
                FoldableEditorSession.SetDeviceBinding(
                    bound, FoldableEditorSession.Source.Bridge, applyDefaults: true);
                FoldableEditorSession.PublishProductState(
                    FoldableProductState.HalfOpened,
                    FoldableOrientation.Horizontal,
                    0.5f,
                    24,
                    true,
                    FoldableEditorSession.MetricsWidth,
                    FoldableEditorSession.MetricsHeight,
                    FoldableEditorSession.Source.Simulator);

                FoldableEditorSession.SimulateDomainReloadForTests();
                Assert.AreEqual(
                    FoldableProductState.HalfOpened,
                    FoldableEditorSession.ProductState);
                Assert.AreEqual(FoldableState.HalfOpened, FoldableEditorSession.State);
                Assert.IsTrue(FoldableEditorSession.IsFoldableDeviceBound);
                Assert.NotNull(FoldableSimulationGate.PlayLayout);
                Assert.AreEqual(
                    FoldableProductState.HalfOpened,
                    FoldableSimulationGate.PlayLayout.productState);
                Assert.AreEqual(
                    FoldableState.HalfOpened,
                    FoldableSimulationGate.PlayLayout.state);

                bool firstBind = !FoldableEditorSession.IsFoldableDeviceBound;
                FoldableEditorSession.SetDeviceBinding(
                    bound, FoldableEditorSession.Source.Bridge, applyDefaults: firstBind);
                Assert.AreEqual(
                    FoldableProductState.HalfOpened,
                    FoldableEditorSession.ProductState);
                Assert.AreEqual(FoldableState.HalfOpened, FoldableEditorSession.State);
            }
            finally
            {
                FoldableEditorSession.ClearDeviceBinding(FoldableEditorSession.Source.Bridge);
                FoldableEditorSession.ResetProductDefaults();
            }
        }

        [Test]
        public void SetDeviceBinding_FirstBindFlipCoverScreen_IsOpened()
        {
            FoldableDeviceCatalog.DeviceProfile flip = RequireCatalogKind(
                FoldableDeviceKind.FlipablePhone);
            var bound = CoverResolution(flip);
            try
            {
                FoldableEditorSession.ClearDeviceBinding(FoldableEditorSession.Source.Bridge);
                FoldableEditorSession.ResetProductDefaults();
                FoldableEditorSession.SetDeviceBinding(
                    bound, FoldableEditorSession.Source.Bridge, applyDefaults: true);
                Assert.AreEqual(
                    FoldableProductState.Opened, FoldableEditorSession.ProductState);
                Assert.IsFalse(FoldableEditorSession.BoundScreenIsCover);
            }
            finally
            {
                FoldableEditorSession.ClearDeviceBinding(FoldableEditorSession.Source.Bridge);
                FoldableEditorSession.ResetProductDefaults();
            }
        }

        [Test]
        public void SetDeviceBinding_FlipProfile_UpdatesActiveDetectorKind()
        {
            FoldableDeviceCatalog.DeviceProfile flip = RequireCatalogKind(
                FoldableDeviceKind.FlipablePhone);
            var go = new GameObject("SimulatorKindDetector");
            try
            {
                var detector = go.AddComponent<FoldablePostureDetector>();
                detector.DeviceKind = FoldableDeviceKind.FoldablePhone;
                FoldableEditorSession.ClearDeviceBinding(FoldableEditorSession.Source.Bridge);
                FoldableEditorSession.ResetProductDefaults();

                FoldableEditorSession.SetDeviceBinding(
                    CoverResolution(flip),
                    FoldableEditorSession.Source.Bridge,
                    applyDefaults: true);
                FoldableEditorSession.PublishScreenSynced();

                Assert.AreEqual(FoldableDeviceKind.FlipablePhone, detector.DeviceKind);
                Assert.AreEqual(
                    FoldableDeviceKind.FlipablePhone,
                    detector.LayoutState.deviceKind);
                FoldableLayoutState expected = FoldableEditorSession.BuildLayout();
                Assert.AreEqual(expected.currentMetrics, detector.LayoutState.currentMetrics);
                Assert.AreEqual(expected.productState, detector.LayoutState.productState);

                var serialized = new SerializedObject(detector);
                Assert.AreEqual(
                    (int)FoldableDeviceKind.FoldablePhone,
                    serialized.FindProperty("deviceKind").enumValueIndex,
                    "Simulator kind must not overwrite the serialized player hint");

                detector.ClearEditorDeviceKind();
                Assert.AreEqual(FoldableDeviceKind.FoldablePhone, detector.DeviceKind);
                Assert.AreEqual(
                    FoldableDeviceKind.FoldablePhone,
                    detector.LayoutState.deviceKind);
            }
            finally
            {
                FoldableEditorSession.ClearDeviceBinding(FoldableEditorSession.Source.Bridge);
                FoldableEditorSession.ResetProductDefaults();
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void SetDeviceBinding_FoldClosedToFlip_IsOpened()
        {
            FoldableDeviceCatalog.DeviceProfile fold = RequireCatalogKind(
                FoldableDeviceKind.FoldablePhone);
            FoldableDeviceCatalog.DeviceProfile flip = RequireCatalogKind(
                FoldableDeviceKind.FlipablePhone);
            try
            {
                FoldableEditorSession.ClearDeviceBinding(FoldableEditorSession.Source.Bridge);
                FoldableEditorSession.ResetProductDefaults();
                FoldableEditorSession.SetDeviceBinding(
                    CoverResolution(fold),
                    FoldableEditorSession.Source.Bridge,
                    applyDefaults: true);
                FoldableEditorSession.PublishProductState(
                    FoldableProductState.Closed,
                    FoldableOrientation.Horizontal,
                    0.5f,
                    24,
                    false,
                    FoldableEditorSession.MetricsWidth,
                    FoldableEditorSession.MetricsHeight,
                    FoldableEditorSession.Source.Simulator);
                Assert.AreEqual(
                    FoldableProductState.Closed, FoldableEditorSession.ProductState);

                FoldableEditorSession.SetDeviceBinding(
                    CoverResolution(flip),
                    FoldableEditorSession.Source.Bridge,
                    applyDefaults: false);
                Assert.AreEqual(
                    FoldableProductState.Opened, FoldableEditorSession.ProductState);
                Assert.IsFalse(FoldableEditorSession.BoundScreenIsCover);
            }
            finally
            {
                FoldableEditorSession.ClearDeviceBinding(FoldableEditorSession.Source.Bridge);
                FoldableEditorSession.ResetProductDefaults();
            }
        }

        [Test]
        public void SetDeviceBinding_FlipToFlip_KeepsClosed()
        {
            FoldableDeviceCatalog.DeviceProfile first = null;
            FoldableDeviceCatalog.DeviceProfile second = null;
            FoldableDeviceCatalog.DeviceProfile[] devices = FoldableDeviceCatalog.Devices;
            for (int i = 0; i < devices.Length; i++)
            {
                FoldableDeviceCatalog.DeviceProfile device = devices[i];
                if (device.Kind != FoldableDeviceKind.FlipablePhone ||
                    device.Screens == null ||
                    device.Screens.Length == 0)
                    continue;
                if (first == null)
                {
                    first = device;
                    continue;
                }

                if (device.FriendlyName != first.FriendlyName)
                {
                    second = device;
                    break;
                }
            }

            Assert.NotNull(first, "catalog needs a Flip profile");
            Assert.NotNull(second, "catalog needs a second Flip profile");

            try
            {
                FoldableEditorSession.ClearDeviceBinding(FoldableEditorSession.Source.Bridge);
                FoldableEditorSession.ResetProductDefaults();
                FoldableEditorSession.SetDeviceBinding(
                    CoverResolution(first),
                    FoldableEditorSession.Source.Bridge,
                    applyDefaults: true);
                FoldableEditorSession.PublishProductState(
                    FoldableProductState.Closed,
                    FoldableOrientation.Horizontal,
                    0.5f,
                    24,
                    false,
                    FoldableEditorSession.MetricsWidth,
                    FoldableEditorSession.MetricsHeight,
                    FoldableEditorSession.Source.Simulator);
                Assert.AreEqual(
                    FoldableProductState.Closed, FoldableEditorSession.ProductState);

                FoldableEditorSession.SetDeviceBinding(
                    CoverResolution(second),
                    FoldableEditorSession.Source.Bridge,
                    applyDefaults: false);
                Assert.AreEqual(
                    FoldableProductState.Closed, FoldableEditorSession.ProductState);
            }
            finally
            {
                FoldableEditorSession.ClearDeviceBinding(FoldableEditorSession.Source.Bridge);
                FoldableEditorSession.ResetProductDefaults();
            }
        }

        [Test]
        public void PackageRoot_ResolvesNestedFoldAndFlipModule()
        {
            string rootAbs = FoldableDeviceCatalog.GetPackageRoot();
            Assert.IsFalse(string.IsNullOrEmpty(rootAbs));
            Assert.IsTrue(Directory.Exists(rootAbs));
            Assert.AreEqual(
                "Assets/ResponsiveUI/FoldAndFlip",
                ResponsiveUiEditorPath.ToProjectRelativePath(rootAbs));
        }

        // ---- Stale FoldingFeature corroboration (mirrors Java isStaleFoldingFeatureForCover) ----

        static FoldableLayoutState LiveInner(FoldableState state, FoldableOrientation orientation)
        {
            var live = FoldableLayoutState.CreateSimulated(
                state, orientation, 1856, 2160, 0.5f, 24);
            live.deviceKind = FoldableDeviceKind.FoldablePhone;
            live.productState = state == FoldableState.HalfOpened
                ? FoldableProductState.HalfOpened
                : FoldableProductState.Opened;
            live.peakMaxMetrics = new RectInt(0, 0, 1856, 2160);
            live.largestDisplayMetrics = new RectInt(0, 0, 1856, 2160);
            return live;
        }

        [Test]
        public void StaleFeature_ExternalLargeDisplayReference_LiveFlatStaysOpened()
        {
            // Even if a 4K external display leaked into the reference / peak, a live FLAT that
            // spans the current window is not discarded (Java also ignores non-built-in displays).
            var live = LiveInner(FoldableState.Flat, FoldableOrientation.Vertical);
            var native = new FoldableLayoutState
            {
                state = FoldableState.Flat,
                orientation = FoldableOrientation.Vertical,
                productState = FoldableProductState.Undefined,
                currentMetrics = new RectInt(0, 0, 1856, 2160),
                maxMetrics = new RectInt(0, 0, 1856, 2160),
                peakMaxMetrics = new RectInt(0, 0, 3840, 2160),
                largestDisplayMetrics = new RectInt(0, 0, 3840, 2160),
                foldBounds = FoldableLayoutState.FromAndroidTopOrigin(
                    new RectInt(928, 0, 0, 2160), 2160)
            };

            Assert.IsFalse(FoldableCoverHeuristics.IsStaleFoldingFeature(native));
            live.MergeNativeSnapshot(native);
            FoldableProductStateResolver.Enrich(
                live, FoldableDeviceKind.FoldablePhone, FoldableProductState.Opened);

            Assert.AreEqual(FoldableState.Flat, live.state);
            Assert.IsFalse(live.isCoverScreen);
            Assert.AreEqual(FoldableProductState.Opened, live.productState);
        }

        [Test]
        public void StaleFeature_LetterboxedInnerWindowWithLiveFlat_StaysExpand()
        {
            // Portrait-locked app letterboxed on the inner display: window area < 70% of the
            // display, but the live hinge still spans the window height → not stale.
            var live = LiveInner(FoldableState.Flat, FoldableOrientation.Vertical);
            var native = new FoldableLayoutState
            {
                state = FoldableState.Flat,
                orientation = FoldableOrientation.Vertical,
                productState = FoldableProductState.Undefined,
                currentMetrics = new RectInt(413, 0, 1030, 2160),
                maxMetrics = new RectInt(0, 0, 1856, 2160),
                peakMaxMetrics = new RectInt(0, 0, 1856, 2160),
                largestDisplayMetrics = new RectInt(0, 0, 1856, 2160),
                foldBounds = FoldableLayoutState.FromAndroidTopOrigin(
                    new RectInt(515, 0, 0, 2160), 2160)
            };

            Assert.IsFalse(FoldableCoverHeuristics.IsStaleFoldingFeature(native));
            live.MergeNativeSnapshot(native);
            FoldableProductStateResolver.Enrich(
                live, FoldableDeviceKind.FoldablePhone, FoldableProductState.Opened);

            Assert.AreEqual(FoldableState.Flat, live.state);
            Assert.IsFalse(live.isCoverScreen);
            Assert.AreEqual(FoldableProductState.Opened, live.productState);
            Assert.AreEqual("Expand", live.GetProductStateDisplayName());
        }

        [Test]
        public void StaleFeature_ClosedWithLaggingInnerFlat_ResolvesCover()
        {
            // Expand→Normal: the cover window arrives with the inner hinge still attached.
            var live = LiveInner(FoldableState.Flat, FoldableOrientation.Vertical);
            var native = new FoldableLayoutState
            {
                state = FoldableState.Flat,
                orientation = FoldableOrientation.Vertical,
                productState = FoldableProductState.Undefined,
                currentMetrics = new RectInt(0, 0, 904, 2316),
                maxMetrics = new RectInt(0, 0, 904, 2316),
                peakMaxMetrics = new RectInt(0, 0, 1856, 2160),
                largestDisplayMetrics = new RectInt(0, 0, 904, 2316),
                foldBounds = new RectInt(928, 0, 0, 2160)
            };

            Assert.IsTrue(FoldableCoverHeuristics.IsStaleFoldingFeature(native));
            live.MergeNativeSnapshot(native);
            Assert.AreEqual(FoldableState.Undefined, live.state);

            FoldableProductStateResolver.Enrich(
                live, FoldableDeviceKind.FoldablePhone, FoldableProductState.Opened);

            Assert.IsTrue(live.isCoverScreen);
            Assert.AreEqual(FoldableProductState.Closed, live.productState);
            Assert.AreEqual("Normal", live.GetProductStateDisplayName());
        }

        [Test]
        public void StaleFeature_ClosedWithLaggingHalfOpened_ResolvesCoverNotFlex()
        {
            var live = LiveInner(FoldableState.HalfOpened, FoldableOrientation.Horizontal);
            var native = new FoldableLayoutState
            {
                state = FoldableState.HalfOpened,
                orientation = FoldableOrientation.Horizontal,
                isSeparating = true,
                productState = FoldableProductState.Undefined,
                currentMetrics = new RectInt(0, 0, 904, 2316),
                maxMetrics = new RectInt(0, 0, 904, 2316),
                peakMaxMetrics = new RectInt(0, 0, 1856, 2160),
                foldBounds = new RectInt(0, 1068, 1856, 24)
            };

            live.MergeNativeSnapshot(native);
            FoldableProductStateResolver.Enrich(
                live,
                FoldableDeviceKind.FoldablePhone,
                FoldableProductState.HalfOpened,
                FoldableOrientation.Horizontal);

            Assert.IsTrue(live.isCoverScreen);
            Assert.AreEqual(FoldableProductState.Closed, live.productState);
            Assert.AreNotEqual(FoldableState.HalfOpened, live.state);
        }

        [Test]
        public void FeatureSpansWindow_UnknownGeometry_CountsAsLive()
        {
            var window = new RectInt(0, 0, 904, 2316);
            Assert.IsTrue(FoldableCoverHeuristics.FeatureSpansWindow(
                FoldableOrientation.Undefined, new RectInt(0, 0, 10, 10), window));
            Assert.IsTrue(FoldableCoverHeuristics.FeatureSpansWindow(
                FoldableOrientation.Horizontal, default, window));
            Assert.IsTrue(FoldableCoverHeuristics.FeatureSpansWindow(
                FoldableOrientation.Horizontal, new RectInt(0, 1000, 904, 0), window));
            Assert.IsFalse(FoldableCoverHeuristics.FeatureSpansWindow(
                FoldableOrientation.Horizontal, new RectInt(0, 1000, 1856, 0), window));
        }

        [Test]
        public void MultiWindow_NoFeatureAfterHalfOpened_DropsFlexAndStaleHinge()
        {
            var live = FoldableLayoutState.CreateSimulated(
                FoldableState.HalfOpened, FoldableOrientation.Horizontal, 1812, 2176, 0.5f, 24);
            live.deviceKind = FoldableDeviceKind.FoldablePhone;
            live.productState = FoldableProductState.HalfOpened;

            var native = new FoldableLayoutState
            {
                state = FoldableState.Undefined,
                productState = FoldableProductState.Undefined,
                currentMetrics = new RectInt(0, 0, 906, 2176),
                maxMetrics = new RectInt(0, 0, 1812, 2176),
                foldBounds = new RectInt(0, 0, 0, 0),
                isInMultiWindowMode = true
            };

            live.MergeNativeSnapshot(native);
            FoldableProductStateResolver.Enrich(
                live,
                FoldableDeviceKind.FoldablePhone,
                FoldableProductState.HalfOpened,
                FoldableOrientation.Horizontal);

            Assert.AreEqual(default(RectInt), live.foldBounds);
            Assert.AreEqual(FoldableOrientation.Undefined, live.orientation);
            Assert.AreEqual(FoldableState.Undefined, live.state);
            Assert.AreEqual(FoldableProductState.Undefined, live.productState);
            Assert.IsFalse(live.isSeparating);
            Assert.IsFalse(live.isCoverScreen);
            Assert.AreEqual(FoldablePosture.Flat, live.ResolvePosture());
            Assert.IsFalse(FoldableProductStateResolver.ShouldPreserveHalfOpened(
                new FoldableLayoutState
                {
                    state = FoldableState.Undefined,
                    isInMultiWindowMode = true
                },
                FoldableProductState.HalfOpened));
        }

        // ---- Hinge Y origin (Android top-left → Unity bottom-left, converted once in FoldableProxy) ----

        [Test]
        public void FromAndroidTopOrigin_OffCentreHorizontalHinge_SplitsAtRealHinge()
        {
            // Android: 800 px pane above the hinge, 20 px hinge, 1820 px pane below.
            RectInt android = new RectInt(0, 800, 1080, 20);
            RectInt unity = FoldableLayoutState.FromAndroidTopOrigin(android, 2640);
            Assert.AreEqual(new RectInt(0, 1820, 1080, 20), unity);
            Assert.AreEqual(android, FoldableLayoutState.FromAndroidTopOrigin(unity, 2640));
        }

        [Test]
        public void FromAndroidTopOrigin_EmptyOrInvalid_Unchanged()
        {
            Assert.AreEqual(default(RectInt), FoldableLayoutState.FromAndroidTopOrigin(default, 2640));
            var missing = new RectInt(-1, -1, -1, -1);
            Assert.AreEqual(missing, FoldableLayoutState.FromAndroidTopOrigin(missing, 2640));
            var hinge = new RectInt(0, 800, 1080, 20);
            Assert.AreEqual(hinge, FoldableLayoutState.FromAndroidTopOrigin(hinge, 0));
            // Zero-thickness vertical hinge spanning the window keeps y = 0.
            Assert.AreEqual(
                new RectInt(928, 0, 0, 2160),
                FoldableLayoutState.FromAndroidTopOrigin(new RectInt(928, 0, 0, 2160), 2160));
        }

        // ---- FoldablePostureSource forwarding ----

        [Test]
        public void PostureSource_ReentrantRebind_AttachesForwarderOnce()
        {
            const BindingFlags StaticNonPublic = BindingFlags.Static | BindingFlags.NonPublic;
            FieldInfo activeField = typeof(FoldablePostureDetector).GetField("s_ActiveInstance", StaticNonPublic);
            FieldInfo lookupField = typeof(FoldablePostureDetector).GetField("s_LookupComplete", StaticNonPublic);
            MethodInfo rebind = typeof(FoldablePostureSource).GetMethod("RebindLayoutForward", StaticNonPublic);
            Assert.IsNotNull(activeField);
            Assert.IsNotNull(lookupField);
            Assert.IsNotNull(rebind);

            var go = new GameObject("ReentrantRebindDetector");
            int received = 0;
            System.Action<FoldableLayoutState> handler = _ => received++;
            try
            {
                go.AddComponent<FoldablePostureDetector>();
                FoldablePostureSource.LayoutChanged += handler;

                // No cached owner while an enabled detector exists: FindActiveInstance promotes it,
                // raising ActiveInstanceChanged → OnOwnerLifecycle → nested RebindLayoutForward.
                activeField.SetValue(null, null);
                lookupField.SetValue(null, false);
                rebind.Invoke(null, null);

                FoldablePostureDetector owner = FoldablePostureDetector.FindActiveInstance();
                Assert.IsNotNull(owner);
                Assert.AreEqual(1, FoldablePostureSource.CountForwardingSubscriptions(owner));

                received = 0;
                owner.ApplySimulatedLayout(FoldableLayoutState.CreateSimulated(
                    FoldableState.Flat, FoldableOrientation.Horizontal, 1080, 2400, 0.5f, 24));
                Assert.AreEqual(1, received);
            }
            finally
            {
                FoldablePostureSource.LayoutChanged -= handler;
                Object.DestroyImmediate(go);
            }
        }

        // ---- Razr+ (main 1080x2640, cover 1272x1080): hinge length matches a cover side ----

        static void AssertRazrCloseResolvesCover(
            FoldableOrientation hinge,
            int mainW, int mainH,
            int coverW, int coverH,
            RectInt androidHingeOnMain)
        {
            var live = FoldableLayoutState.CreateSimulated(
                FoldableState.Flat, hinge, mainW, mainH, 0.5f, 1);
            live.deviceKind = FoldableDeviceKind.FlipablePhone;
            live.productState = FoldableProductState.Opened;
            live.peakMaxMetrics = new RectInt(0, 0, mainW, mainH);

            // Lagging main-display feature converted with the CURRENT (cover) window height,
            // exactly as FoldableProxy does.
            var native = new FoldableLayoutState
            {
                state = FoldableState.Flat,
                orientation = hinge,
                productState = FoldableProductState.Undefined,
                currentMetrics = new RectInt(0, 0, coverW, coverH),
                maxMetrics = new RectInt(0, 0, coverW, coverH),
                peakMaxMetrics = new RectInt(0, 0, mainW, mainH),
                largestDisplayMetrics = new RectInt(0, 0, mainW, mainH),
                foldBounds = FoldableLayoutState.FromAndroidTopOrigin(androidHingeOnMain, coverH)
            };

            // Same hinge length as a cover side: only the position proves it is stale.
            int featureLength = hinge == FoldableOrientation.Horizontal
                ? native.foldBounds.width
                : native.foldBounds.height;
            int windowLength = hinge == FoldableOrientation.Horizontal ? coverW : coverH;
            Assert.AreEqual(windowLength, featureLength);

            Assert.IsFalse(FoldableCoverHeuristics.FeatureSpansWindow(
                hinge, native.foldBounds, native.currentMetrics));
            Assert.IsTrue(FoldableCoverHeuristics.IsStaleFoldingFeature(native));

            live.MergeNativeSnapshot(native);
            Assert.AreEqual(FoldableState.Undefined, live.state);
            FoldableProductStateResolver.Enrich(
                live, FoldableDeviceKind.FlipablePhone, FoldableProductState.Opened);

            Assert.IsTrue(live.isCoverScreen);
            Assert.AreEqual(FoldableProductState.Closed, live.productState);
        }

        [Test]
        public void StaleFeature_RazrClosedFromMainLandscape_ResolvesCover()
        {
            // Main landscape 2640x1080: vertical hinge x=1320, height 1080 == cover height 1080.
            AssertRazrCloseResolvesCover(
                FoldableOrientation.Vertical, 2640, 1080, 1272, 1080,
                new RectInt(1320, 0, 0, 1080));
        }

        [Test]
        public void StaleFeature_RazrClosedFromMainPortrait_ResolvesCover()
        {
            // Main portrait 1080x2640: horizontal hinge y=1320, width 1080 == portrait cover width.
            AssertRazrCloseResolvesCover(
                FoldableOrientation.Horizontal, 1080, 2640, 1080, 1272,
                new RectInt(0, 1320, 1080, 0));
        }

        [Test]
        public void FeatureSpansWindow_RequiresCentreInsideWindow()
        {
            var cover = new RectInt(0, 0, 1272, 1080);
            Assert.IsFalse(FoldableCoverHeuristics.FeatureSpansWindow(
                FoldableOrientation.Vertical, new RectInt(1320, 0, 0, 1080), cover));
            Assert.IsTrue(FoldableCoverHeuristics.FeatureSpansWindow(
                FoldableOrientation.Vertical, new RectInt(636, 0, 0, 1080), cover));
            // Bottom-origin conversion can push a stale hinge below zero.
            Assert.IsFalse(FoldableCoverHeuristics.FeatureSpansWindow(
                FoldableOrientation.Horizontal, new RectInt(0, -48, 1080, 0),
                new RectInt(0, 0, 1080, 1272)));
            // Window offset (letterbox / split) does not matter: bounds are window-relative.
            Assert.IsTrue(FoldableCoverHeuristics.FeatureSpansWindow(
                FoldableOrientation.Vertical, new RectInt(515, 0, 0, 2160),
                new RectInt(413, 0, 1030, 2160)));
        }

        // ---- Resume gate (awaitingFreshLayout) ----

        static FoldableLayoutState GateSnapshot(FoldableState state) =>
            new FoldableLayoutState
            {
                state = state,
                currentMetrics = new RectInt(0, 0, 1856, 2160)
            };

        [Test]
        public void FreshLayoutGate_ColdStart_DoesNotHold()
        {
            var gate = new FoldableFreshLayoutGate();
            FoldableLayoutState cached = GateSnapshot(FoldableState.Undefined);

            Assert.IsTrue(gate.Offer(cached, true, 0f, out FoldableLayoutState release));
            Assert.AreSame(cached, release);
            Assert.IsFalse(gate.IsHolding);
        }

        [Test]
        public void FreshLayoutGate_ResumeHoldsUntilFreshCallback()
        {
            var gate = new FoldableFreshLayoutGate();
            Assert.IsTrue(gate.Offer(GateSnapshot(FoldableState.Flat), false, 0f, out _));

            gate.NotifyTrackingStopped();
            Assert.IsFalse(gate.Offer(GateSnapshot(FoldableState.Undefined), true, 10f, out FoldableLayoutState release));
            Assert.IsNull(release);
            Assert.IsTrue(gate.IsHolding);
            Assert.IsFalse(gate.Offer(GateSnapshot(FoldableState.Undefined), true, 10.2f, out release));
            Assert.IsFalse(gate.Poll(10.3f, out release));

            FoldableLayoutState fresh = GateSnapshot(FoldableState.Flat);
            Assert.IsTrue(gate.Offer(fresh, false, 10.35f, out release));
            Assert.AreSame(fresh, release);
            Assert.IsFalse(gate.IsHolding);
            Assert.IsFalse(gate.Poll(20f, out _));
        }

        [Test]
        public void FreshLayoutGate_TimeoutReleasesNewestHeldThenPassesThrough()
        {
            var gate = new FoldableFreshLayoutGate(0.5f);
            Assert.IsTrue(gate.Offer(GateSnapshot(FoldableState.Flat), false, 0f, out _));

            Assert.IsFalse(gate.Offer(GateSnapshot(FoldableState.Undefined), true, 5f, out _));
            FoldableLayoutState newest = GateSnapshot(FoldableState.Undefined);
            Assert.IsFalse(gate.Offer(newest, true, 5.3f, out _));
            Assert.IsFalse(gate.Poll(5.49f, out _));
            Assert.IsTrue(gate.Poll(5.5f, out FoldableLayoutState release));
            Assert.AreSame(newest, release);

            // Same episode after the timeout: normal heuristics resume immediately.
            FoldableLayoutState later = GateSnapshot(FoldableState.Undefined);
            Assert.IsTrue(gate.Offer(later, true, 5.6f, out release));
            Assert.AreSame(later, release);

            // Next pause starts a new bounded hold.
            gate.NotifyTrackingStopped();
            Assert.IsFalse(gate.Offer(GateSnapshot(FoldableState.Undefined), true, 9f, out _));

            // Detach forgets the baseline: behaves like a cold start.
            gate.Reset();
            Assert.IsFalse(gate.IsHolding);
            Assert.IsTrue(gate.Offer(GateSnapshot(FoldableState.Undefined), true, 9.1f, out _));
        }

        [Test]
        public void FreshLayoutGate_HeldEmptySnapshot_KeepsPublishedExpand()
        {
            // Expand before pause; resume publishes an unconfirmed empty snapshot. Holding it keeps
            // Opened (no Expand→Undefined→Expand), then the fresh FLAT confirms Expand.
            var live = LiveInner(FoldableState.Flat, FoldableOrientation.Vertical);
            var gate = new FoldableFreshLayoutGate();
            Assert.IsTrue(gate.Offer(GateSnapshot(FoldableState.Flat), false, 0f, out _));
            gate.NotifyTrackingStopped();

            var empty = new FoldableLayoutState
            {
                state = FoldableState.Undefined,
                productState = FoldableProductState.Undefined,
                currentMetrics = new RectInt(0, 0, 1856, 2160),
                maxMetrics = new RectInt(0, 0, 1856, 2160),
                peakMaxMetrics = new RectInt(0, 0, 1856, 2160)
            };
            Assert.IsFalse(gate.Offer(empty, true, 1f, out _));
            Assert.AreEqual(FoldableProductState.Opened, live.productState);
            Assert.AreEqual("Expand", live.GetProductStateDisplayName());

            var fresh = new FoldableLayoutState
            {
                state = FoldableState.Flat,
                orientation = FoldableOrientation.Vertical,
                productState = FoldableProductState.Undefined,
                currentMetrics = new RectInt(0, 0, 1856, 2160),
                maxMetrics = new RectInt(0, 0, 1856, 2160),
                peakMaxMetrics = new RectInt(0, 0, 1856, 2160),
                foldBounds = new RectInt(928, 0, 0, 2160)
            };
            Assert.IsTrue(gate.Offer(fresh, false, 1.05f, out FoldableLayoutState release));
            live.MergeNativeSnapshot(release);
            FoldableProductStateResolver.Enrich(
                live, FoldableDeviceKind.FoldablePhone, FoldableProductState.Opened);
            Assert.AreEqual(FoldableProductState.Opened, live.productState);
            Assert.AreEqual("Expand", live.GetProductStateDisplayName());
        }

        [Test]
        public void CallbackPayload_ParsesPackedSnapshotAndFlipsHingeOrigin()
        {
            // version, awaiting, multi, pip, cover, separating, hinge, state, occlusion,
            // current l,t,w,h, max, peak, largest, bounds l,t,w,h
            const string payload =
                "1,0,0,0,1,1,0,1,1," +
                "0,0,1080,2000," +
                "0,0,1080,2400," +
                "0,0,1080,2400," +
                "0,0,2200,2480," +
                "0,100,1080,20";

            Assert.IsTrue(FoldableProxy.ParseCallbackPayload(
                payload, out FoldableLayoutState snapshot, out bool awaitingFresh));
            Assert.IsFalse(awaitingFresh);
            Assert.IsTrue(snapshot.isCoverScreen);
            Assert.IsTrue(snapshot.isSeparating);
            Assert.AreEqual(FoldableOrientation.Horizontal, snapshot.orientation);
            Assert.AreEqual(FoldableState.HalfOpened, snapshot.state);
            Assert.AreEqual(new RectInt(0, 0, 1080, 2000), snapshot.currentMetrics);
            Assert.AreEqual(new RectInt(0, 1880, 1080, 20), snapshot.foldBounds);
            Assert.IsFalse(FoldableProxy.ParseCallbackPayload("1,0", out _, out _));
        }

        static FoldableDeviceCatalog.DeviceProfile RequireCatalogKind(FoldableDeviceKind kind)
        {
            FoldableDeviceCatalog.DeviceProfile[] devices = FoldableDeviceCatalog.Devices;
            for (int i = 0; i < devices.Length; i++)
            {
                FoldableDeviceCatalog.DeviceProfile device = devices[i];
                if (device.Kind == kind &&
                    device.Screens != null &&
                    device.Screens.Length > 0)
                    return device;
            }

            Assert.Fail("catalog needs a " + kind + " profile");
            return null;
        }

        static FoldableDeviceCatalog.Resolution CoverResolution(
            FoldableDeviceCatalog.DeviceProfile device)
        {
            FoldableDeviceCatalog.ScreenProfile cover = device.Screens[0];
            for (int i = 0; i < device.Screens.Length; i++)
            {
                if (device.Screens[i].CoverOrOuter)
                {
                    cover = device.Screens[i];
                    break;
                }
            }

            return new FoldableDeviceCatalog.Resolution
            {
                Device = device,
                Screen = cover,
                IsKnownFoldable = true,
            };
        }
    }
}
