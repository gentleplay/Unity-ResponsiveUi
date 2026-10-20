using FDPlus.Foldable.Editor;
using FDPlus.ResponsiveUI;
using NUnit.Framework;
using System.Reflection;
using UnityEngine;

namespace FDPlus.ResponsiveUI.Tests
{
    public class ResponsivePerformanceReliabilityTests
    {
        [Test]
        public void PlayerHardwareFallback_UsesHardwareIdentity()
        {
            Assert.AreEqual(
                ResponsiveDevice.FormFactor.Phone,
                ResponsiveDevice.ResolvePlayerHardwareFallback(
                    ResponsiveDevice.DeviceType.Phone));
            Assert.AreEqual(
                ResponsiveDevice.FormFactor.Pad,
                ResponsiveDevice.ResolvePlayerHardwareFallback(
                    ResponsiveDevice.DeviceType.Pad));
        }

        [Test]
        public void FoldRefinement_DoesNotCrossMapFoldAndFlipCoverSizes()
        {
            Assert.AreEqual(
                ResponsiveDevice.FormFactor.Phone,
                ResponsiveDevice.RefineFoldableForm(
                    FDPlus.Foldable.FoldableDeviceKind.FoldablePhone,
                    ResponsiveDevice.FormFactor.Phone,
                    948f,
                    1048f));
            Assert.AreEqual(
                ResponsiveDevice.FormFactor.Phone,
                ResponsiveDevice.RefineFoldableForm(
                    FDPlus.Foldable.FoldableDeviceKind.FlipablePhone,
                    ResponsiveDevice.FormFactor.Phone,
                    1248f,
                    1972f));
        }

        [Test]
        public void EmptyFoldBridgeSnapshot_DoesNotTurnAndroidPhoneIntoPad()
        {
            var go = new GameObject("ConfiguredFoldDetector");
            try
            {
                var detector =
                    go.AddComponent<FDPlus.Foldable.FoldablePostureDetector>();
                detector.DeviceKind =
                    FDPlus.Foldable.FoldableDeviceKind.FoldablePhone;
                var emptyNative = new FDPlus.Foldable.FoldableLayoutState
                {
                    deviceKind =
                        FDPlus.Foldable.FoldableDeviceKind.FoldablePhone,
                    state = FDPlus.Foldable.FoldableState.Undefined,
                    productState =
                        FDPlus.Foldable.FoldableProductState.Undefined,
                    currentMetrics = new RectInt(0, 0, 1440, 3200),
                    maxMetrics = new RectInt(0, 0, 1440, 3200),
                };

                detector.ApplySimulatedLayout(emptyNative);

                Assert.AreEqual(
                    FDPlus.Foldable.FoldableProductState.Undefined,
                    detector.LayoutState.productState);
                Assert.AreEqual(
                    ResponsiveDevice.FormFactor.Phone,
                    ResponsiveDevice.DetectActiveFormFactor(
                        new Vector2(1440f, 3200f),
                        1.6f,
                        foldableRuntimeSupported: true));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void KnownFoldInnerPadSize_BeforePosture_DetectsPad()
        {
            // Fold7/8 Expand inner (~1968×2184) before Jetpack must stay Pad.
            Assert.AreEqual(
                ResponsiveDevice.FormFactor.Pad,
                ResponsiveDevice.DetectActiveFormFactor(
                    new Vector2(1968f, 2184f),
                    1.6f,
                    foldableRuntimeSupported: true));
        }

        [Test]
        public void KnownFoldCoverLandscape_IsNotClassifiedAsPad()
        {
            // Cover landscape aspect (~1.58) sits under padMax 1.6 — must not become Pad.
            // Editor maps known WxH to FoldPhoneCover; player falls back to hardware Phone.
            Assert.AreNotEqual(
                ResponsiveDevice.FormFactor.Pad,
                ResponsiveDevice.DetectActiveFormFactor(
                    new Vector2(1972f, 1248f),
                    1.6f,
                    foldableRuntimeSupported: true));
        }

        [Test]
        public void SafeAreaRequest_DoesNotSwallowOrientationChange()
        {
            var go = new GameObject(
                "SafeAreaRotationHub",
                typeof(RectTransform),
                typeof(Canvas));
            try
            {
                ResponsiveDevice.ClearOverride();
                ResponsiveDevice.SetSimulatorLayout(
                    ResponsiveDevice.FormFactor.Phone,
                    ResponsiveDevice.Orientation.Portrait,
                    1000,
                    2000);

                var hub = go.AddComponent<ResponsiveUI>();
                hub.enabled = true;
                hub.Apply();

                FieldInfo requested = typeof(ResponsiveUiHandler).GetField(
                    "_layoutRefreshRequested",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                requested.SetValue(hub, true);

                ResponsiveDevice.SetSimulatorLayout(
                    ResponsiveDevice.FormFactor.Phone,
                    ResponsiveDevice.Orientation.Landscape,
                    2000,
                    1000);
                MethodInfo refresh = typeof(ResponsiveUiHandler).GetMethod(
                    "RefreshLayoutIfWatchSizeChanged",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                refresh.Invoke(hub, null);

                Assert.AreEqual(
                    ResponsiveDevice.Orientation.Landscape,
                    hub.CurrentOrientation);
                Assert.IsFalse((bool)requested.GetValue(hub));
            }
            finally
            {
                ResponsiveDevice.ClearSimulatorLayout();
                ResponsiveDevice.ClearOverride();
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void DetectActiveFormFactor_FormAndOrientationOverride_MatchCurrentLayout()
        {
            try
            {
                ResponsiveDevice.ClearSimulatorLayout();
                ResponsiveDevice.SetFormOverride(ResponsiveDevice.FormFactor.FoldHalfOpened);
                ResponsiveDevice.SetOrientationOverride(ResponsiveDevice.Orientation.Landscape);

                var portraitPhone = new Vector2(1000f, 2000f);
                Assert.AreEqual(
                    ResponsiveDevice.CurrentFormFactor,
                    ResponsiveDevice.DetectActiveFormFactor(
                        portraitPhone,
                        1.6f,
                        foldableRuntimeSupported: true));
                ResponsiveDevice.LayoutKey live = ResponsiveDevice.ResolveLiveLayout(portraitPhone);
                Assert.AreEqual(ResponsiveDevice.FormFactor.FoldHalfOpened, live.Form);
                Assert.AreEqual(ResponsiveDevice.Orientation.Landscape, live.Orientation);
                Assert.AreEqual(ResponsiveDevice.CurrentLayout, live);
            }
            finally
            {
                ResponsiveDevice.ClearOverride();
            }
        }

        [Test]
        public void PlaySessionReset_ClearsOverridesAndKeepsSimulatorPin()
        {
            MethodInfo reset = typeof(ResponsiveDevice).GetMethod(
                "ResetPlaySessionState",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(reset, "ResponsiveDevice has a play-session static reset");
            var attribute = reset.GetCustomAttribute<RuntimeInitializeOnLoadMethodAttribute>();
            Assert.NotNull(attribute);
            Assert.AreEqual(
                RuntimeInitializeLoadType.SubsystemRegistration,
                attribute.loadType);

            try
            {
                ResponsiveDevice.SetSimulatorLayout(
                    ResponsiveDevice.FormFactor.Phone,
                    ResponsiveDevice.Orientation.Portrait,
                    1000,
                    2000);
                ResponsiveDevice.SetFormOverride(ResponsiveDevice.FormFactor.Pad);
                ResponsiveDevice.SetOrientationOverride(ResponsiveDevice.Orientation.Landscape);

                reset.Invoke(null, null);

                Assert.IsTrue(ResponsiveDevice.HasSimulatorLayout);
                Assert.AreEqual(
                    ResponsiveDevice.FormFactor.Phone,
                    ResponsiveDevice.CurrentFormFactor);
                Assert.AreEqual(
                    ResponsiveDevice.Orientation.Portrait,
                    ResponsiveDevice.CurrentOrientation);
            }
            finally
            {
                ResponsiveDevice.ClearSimulatorLayout();
                ResponsiveDevice.ClearOverride();
            }
        }

        [Test]
        public void FocusBoundsCache_SamplesLiveBoundsAndRefreshesMembership()
        {
            var root = new GameObject("FocusRoot");
            var first = new GameObject("First", typeof(BoxCollider));
            var second = new GameObject("Second", typeof(BoxCollider));
            try
            {
                first.transform.SetParent(root.transform);

                var cache = new ResponsiveCameraFocus.TargetBoundsCache();
                Assert.IsTrue(cache.GetBounds(root.transform, null, out Bounds initial));

                first.transform.localPosition = new Vector3(3f, 0f, 0f);
                Physics.SyncTransforms();
                Assert.IsTrue(cache.GetBounds(root.transform, null, out Bounds moved));
                Assert.Greater(moved.center.x, initial.center.x + 2f);

                second.transform.localPosition = new Vector3(10f, 0f, 0f);
                second.transform.SetParent(root.transform);
                Physics.SyncTransforms();
                Assert.IsTrue(cache.GetBounds(root.transform, null, out Bounds expanded));
                Assert.Greater(expanded.max.x, 10f);

                second.transform.SetParent(null);
                Physics.SyncTransforms();
                Assert.IsTrue(cache.GetBounds(root.transform, null, out Bounds reduced));
                Assert.Less(reduced.max.x, 10f);
            }
            finally
            {
                Object.DestroyImmediate(second);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void SimulatorSelectionAdapter_DrivesFacadeWithoutUnityReflection()
        {
            var expected = new SimulatorDeviceBridge.Selection
            {
                FriendlyName = "Fake Fold",
                DeviceModel = "fake-fold",
                ScreenWidth = 1200,
                ScreenHeight = 2000,
                IsValid = true,
            };

            SimulatorDeviceBridge.SetSelectionAdapter(
                new FakeSelectionAdapter(expected));
            try
            {
                Assert.IsTrue(
                    SimulatorDeviceBridge.GetActiveSelection(out var actual));
                Assert.AreEqual(expected.DeviceModel, actual.DeviceModel);
                Assert.AreEqual(expected.OrientedScreenSize, actual.OrientedScreenSize);
            }
            finally
            {
                SimulatorDeviceBridge.SetSelectionAdapter(null);
            }
        }

        sealed class FakeSelectionAdapter : IDeviceSimulatorSelectionAdapter
        {
            readonly SimulatorDeviceBridge.Selection _selection;

            public FakeSelectionAdapter(
                SimulatorDeviceBridge.Selection selection) =>
                _selection = selection;

            public bool GetActiveSelection(
                out SimulatorDeviceBridge.Selection selection)
            {
                selection = _selection;
                return selection.IsValid;
            }
        }
    }
}
