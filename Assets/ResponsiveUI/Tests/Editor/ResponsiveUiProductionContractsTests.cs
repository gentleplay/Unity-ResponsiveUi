using FDPlus.Foldable;
using FDPlus.ResponsiveUI;
using NUnit.Framework;
using System.Reflection;
using UnityEngine;

namespace FDPlus.ResponsiveUI.Tests
{
    /// <summary>
    /// Production contracts added for the 1.0.0 ship pass (canvas update skip, single fold owner).
    /// </summary>
    public class ResponsiveUiProductionContractsTests
    {
        [Test]
        public void ShouldForceCanvasUpdate_FirstApply_True()
        {
            var layout = new ResponsiveDevice.LayoutKey(
                ResponsiveDevice.FormFactor.Phone,
                ResponsiveDevice.Orientation.Landscape);
            Assert.IsTrue(ResponsiveUiHandler.ShouldForceCanvasUpdate(
                false, layout, layout,
                ResponsiveCatalogDevices.CompactPhoneLandscape,
                ResponsiveCatalogDevices.CompactPhoneLandscape));
        }

        [Test]
        public void ShouldForceCanvasUpdate_SameLayoutAndSize_False()
        {
            var layout = new ResponsiveDevice.LayoutKey(
                ResponsiveDevice.FormFactor.Phone,
                ResponsiveDevice.Orientation.Landscape);
            Assert.IsFalse(ResponsiveUiHandler.ShouldForceCanvasUpdate(
                true, layout, layout,
                ResponsiveCatalogDevices.CompactPhoneLandscape,
                ResponsiveCatalogDevices.CompactPhoneLandscape));
        }

        [Test]
        public void ShouldForceCanvasUpdate_FormFlip_True()
        {
            var phone = new ResponsiveDevice.LayoutKey(
                ResponsiveDevice.FormFactor.Phone,
                ResponsiveDevice.Orientation.Landscape);
            var pad = new ResponsiveDevice.LayoutKey(
                ResponsiveDevice.FormFactor.Pad,
                ResponsiveDevice.Orientation.Landscape);
            Assert.IsTrue(ResponsiveUiHandler.ShouldForceCanvasUpdate(
                true, phone, pad,
                ResponsiveCatalogDevices.CompactPhoneLandscape,
                ResponsiveCatalogDevices.CompactPhoneLandscape));
        }

        [Test]
        public void ShouldForceCanvasUpdate_SubPixelSize_False()
        {
            var layout = new ResponsiveDevice.LayoutKey(
                ResponsiveDevice.FormFactor.Phone,
                ResponsiveDevice.Orientation.Landscape);
            Vector2 compact = ResponsiveCatalogDevices.CompactPhoneLandscape;
            Assert.IsFalse(ResponsiveUiHandler.ShouldForceCanvasUpdate(
                true, layout, layout, compact, compact + new Vector2(0.5f, 0.5f)));
        }

        [Test]
        public void FindActiveInstance_SecondDetector_DoesNotStealOwner()
        {
            var firstGo = new GameObject("FoldOwner");
            var secondGo = new GameObject("FoldDuplicate");
            try
            {
                var first = firstGo.AddComponent<FoldablePostureDetector>();
                var second = secondGo.AddComponent<FoldablePostureDetector>();
                Assert.AreSame(first, FoldablePostureDetector.FindActiveInstance());
                Assert.AreNotSame(second, FoldablePostureDetector.FindActiveInstance());
            }
            finally
            {
                Object.DestroyImmediate(firstGo);
                Object.DestroyImmediate(secondGo);
            }
        }

        [Test]
        public void FindActiveInstance_OwnerDisabled_PromotesOther()
        {
            var firstGo = new GameObject("FoldOwner");
            var secondGo = new GameObject("FoldDuplicate");
            try
            {
                firstGo.AddComponent<FoldablePostureDetector>();
                var second = secondGo.AddComponent<FoldablePostureDetector>();
                firstGo.SetActive(false);
                Assert.AreSame(second, FoldablePostureDetector.FindActiveInstance());
            }
            finally
            {
                Object.DestroyImmediate(firstGo);
                Object.DestroyImmediate(secondGo);
            }
        }

        [Test]
        public void FindActiveInstance_PromotionRunsOwnerInitialization()
        {
            var firstGo = new GameObject("FoldOwner");
            var secondGo = new GameObject("FoldPromoted");
            FoldablePostureDetector second = null;
            int initializationCount = 0;
            System.Action<FoldablePostureDetector> onInitializing =
                detector =>
                {
                    if (detector == second)
                        initializationCount++;
                };
            try
            {
                firstGo.AddComponent<FoldablePostureDetector>();
                second = secondGo.AddComponent<FoldablePostureDetector>();
                FoldablePostureDetector.EditorInitializing += onInitializing;

                firstGo.SetActive(false);

                Assert.AreSame(second, FoldablePostureDetector.FindActiveInstance());
                Assert.AreEqual(1, initializationCount);
            }
            finally
            {
                FoldablePostureDetector.EditorInitializing -= onInitializing;
                Object.DestroyImmediate(firstGo);
                Object.DestroyImmediate(secondGo);
            }
        }

        [Test]
        public void FindActiveInstance_DoesNotPromotePendingPersistentDuplicate()
        {
            var firstGo = new GameObject("FoldOwner");
            var duplicateGo = new GameObject("FoldPendingDestroy");
            try
            {
                firstGo.AddComponent<FoldablePostureDetector>();
                var duplicate = duplicateGo.AddComponent<FoldablePostureDetector>();
                FieldInfo duplicateField = typeof(FoldablePostureDetector).GetField(
                    "isDuplicatePersist",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                duplicateField.SetValue(duplicate, true);

                firstGo.SetActive(false);

                Assert.IsNull(FoldablePostureDetector.FindActiveInstance());
            }
            finally
            {
                Object.DestroyImmediate(firstGo);
                Object.DestroyImmediate(duplicateGo);
            }
        }

        [Test]
        public void FindActiveInstance_AfterEmptyLookup_PicksLaterDetector()
        {
            Assert.IsNull(FoldablePostureDetector.FindActiveInstance());
            var go = new GameObject("FoldLater");
            try
            {
                var detector = go.AddComponent<FoldablePostureDetector>();
                Assert.AreSame(detector, FoldablePostureDetector.FindActiveInstance());
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void IsRuntimeSupported_IsTrueInEditor()
        {
            Assert.IsTrue(FoldablePostureDetector.IsRuntimeSupported);
        }

        [Test]
        public void DeviceKind_SetterUpdatesCurrentLayoutState()
        {
            var go = new GameObject("FoldKindSync");
            try
            {
                var detector = go.AddComponent<FoldablePostureDetector>();
                detector.ClearEditorDeviceKind();

                detector.DeviceKind = FoldableDeviceKind.FlipablePhone;

                Assert.AreEqual(FoldableDeviceKind.FlipablePhone, detector.DeviceKind);
                Assert.AreEqual(FoldableDeviceKind.FlipablePhone, detector.LayoutState.deviceKind);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void ApplySimulatedLayout_SameLayoutTwice_RemainsExplicitRefresh()
        {
            var go = new GameObject("FoldExplicitRefresh");
            try
            {
                var detector = go.AddComponent<FoldablePostureDetector>();
                var layout = FoldableLayoutState.CreateSimulated(
                    FoldableState.Flat,
                    FoldableOrientation.Horizontal,
                    1080, 1920, 0.5f, 20);
                int notifications = 0;
                detector.LayoutChanged += _ => notifications++;

                detector.ApplySimulatedLayout(layout);
                detector.ApplySimulatedLayout(layout);

                Assert.AreEqual(2, notifications);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void LayoutChanged_SubscribersReceiveIsolatedSnapshots()
        {
            var go = new GameObject("FoldSnapshotIsolation");
            try
            {
                var detector = go.AddComponent<FoldablePostureDetector>();
                var layout = FoldableLayoutState.CreateSimulated(
                    FoldableState.HalfOpened,
                    FoldableOrientation.Vertical,
                    1920, 1080, 0.5f, 20);
                FoldableState secondSubscriberState = FoldableState.Undefined;
                detector.LayoutChanged += state => state.state = FoldableState.Flat;
                detector.LayoutChanged += state => secondSubscriberState = state.state;

                detector.ApplySimulatedLayout(layout);

                Assert.AreEqual(FoldableState.HalfOpened, secondSubscriberState);
                Assert.AreEqual(FoldableState.HalfOpened, detector.LayoutState.state);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void DetectActiveFormFactor_Fold8Cover_WithoutFoldableRuntime_IsNotFoldPhoneCover()
        {
            Assert.AreNotEqual(
                ResponsiveDevice.FormFactor.FoldPhoneCover,
                ResponsiveDevice.DetectActiveFormFactor(
                    ResponsiveCatalogDevices.FoldCoverPortrait, 1.6f, foldableRuntimeSupported: false));
        }

        [Test]
        public void DetectActiveFormFactor_Fold8Cover_WithFoldableRuntime_IsFoldPhoneCover()
        {
            Assert.AreEqual(
                ResponsiveDevice.FormFactor.FoldPhoneCover,
                ResponsiveDevice.DetectActiveFormFactor(
                    ResponsiveCatalogDevices.FoldCoverPortrait, 1.6f, foldableRuntimeSupported: true));
        }

        [Test]
        public void ApplyOrder_SortsWidgetsBeforeCameraBeforeBanner()
        {
            var go = new GameObject("ApplyOrder");
            try
            {
                var banner = go.AddComponent<ResponsiveTextBanner>();
                var camera = go.AddComponent<ResponsiveCamera>();
                var scroll = go.AddComponent<ResponsiveScrollView>();
                var twoSide = go.AddComponent<ResponsiveTwoSideUiTweenController>();
                var controller = go.AddComponent<ResponsiveUiController>();
                var clients = new System.Collections.Generic.List<IResponsiveUiClient>
                {
                    banner, camera, scroll, twoSide, controller
                };
                ResponsiveUiApplyOrder.InsertionSortByPriority(clients);
                Assert.AreSame(controller, clients[0]);
                Assert.AreSame(twoSide, clients[1]);
                Assert.AreSame(camera, clients[2]);
                Assert.AreSame(scroll, clients[3]);
                Assert.AreSame(banner, clients[4]);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void ApplyOrder_UnknownClients_StayStable()
        {
            var clients = new System.Collections.Generic.List<IResponsiveUiClient> { null, null };
            ResponsiveUiApplyOrder.InsertionSortByPriority(clients);
            Assert.IsNull(clients[0]);
            Assert.IsNull(clients[1]);
            Assert.AreEqual(5, ResponsiveUiApplyOrder.ClientPriority(null));
        }
    }
}
