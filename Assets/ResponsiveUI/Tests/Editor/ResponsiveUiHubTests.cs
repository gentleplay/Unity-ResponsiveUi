using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using FDPlus.ResponsiveUI;

using static FDPlus.ResponsiveUI.Tests.ResponsiveEditorTestUtil;

namespace FDPlus.ResponsiveUI.Tests
{
    /// <summary>
    /// Canvas hub resolve, client Apply, and leftover-list migration onto
    /// <see cref="ResponsiveUiController"/>.
    /// </summary>
    public class ResponsiveUiHubTests
    {
        [Test]
        public void Resolve_Assigned_WinsOverParent()
        {
            var parentGo = new GameObject("HubParent", typeof(Canvas));
            var childGo = new GameObject("Client");
            var otherGo = new GameObject("OtherHub", typeof(Canvas));
            try
            {
                childGo.transform.SetParent(parentGo.transform);
                var parentHub = parentGo.AddComponent<ResponsiveUI>();
                var otherHub = otherGo.AddComponent<ResponsiveUI>();
                Assert.AreSame(otherHub, ResponsiveUiHub.Resolve(otherHub, childGo.transform));
                Assert.AreSame(parentHub, ResponsiveUiHub.Resolve(null, childGo.transform));
            }
            finally
            {
                Object.DestroyImmediate(parentGo);
                Object.DestroyImmediate(otherGo);
            }
        }

        [Test]
        public void Resolve_FindsDisabledHubOnParentCanvas()
        {
            var canvasGo = new GameObject("Canvas", typeof(Canvas));
            var childGo = new GameObject("Child");
            try
            {
                childGo.transform.SetParent(canvasGo.transform);
                var hub = canvasGo.AddComponent<ResponsiveUI>();
                hub.enabled = false;
                Assert.AreSame(hub, ResponsiveUiHub.Resolve(null, childGo.transform));
            }
            finally
            {
                Object.DestroyImmediate(canvasGo);
            }
        }

        [Test]
        public void Resolve_NestedCanvasWithoutHub_UsesRootCanvasHub()
        {
            var rootGo = new GameObject("RootCanvas", typeof(Canvas));
            var nestedGo = new GameObject("NestedCanvas", typeof(Canvas));
            var childGo = new GameObject("Child");
            try
            {
                nestedGo.transform.SetParent(rootGo.transform);
                childGo.transform.SetParent(nestedGo.transform);
                var rootHub = rootGo.AddComponent<ResponsiveUI>();
                Assert.AreSame(rootHub, ResponsiveUiHub.Resolve(null, childGo.transform));

                var nestedHub = nestedGo.AddComponent<ResponsiveUI>();
                Assert.AreSame(nestedHub, ResponsiveUiHub.Resolve(null, childGo.transform));
            }
            finally
            {
                Object.DestroyImmediate(rootGo);
            }
        }

        [Test]
        public void AddComponent_OnCanvasChild_AssignsParentHub()
        {
            var canvasGo = new GameObject("Canvas", typeof(Canvas));
            var childGo = new GameObject("Child");
            try
            {
                childGo.transform.SetParent(canvasGo.transform);
                var hub = canvasGo.AddComponent<ResponsiveUI>();
                hub.enabled = false;

                var controller = childGo.AddComponent<ResponsiveUiController>();
                Assert.AreSame(hub, controller.Hub);

                var camera = childGo.AddComponent<ResponsiveCamera>();
                Assert.AreSame(hub, camera.Hub);

                var scroll = childGo.AddComponent<ResponsiveScrollView>();
                Assert.AreSame(hub, scroll.Hub);
            }
            finally
            {
                Object.DestroyImmediate(canvasGo);
            }
        }

        [Test]
        public void Apply_NotifiesRegisteredClient()
        {
            var go = new GameObject("HubApply");
            try
            {
                var hub = go.AddComponent<ResponsiveUI>();
                var spy = go.AddComponent<HubClientSpy>();
                spy.sizeReapply = true;
                hub.RegisterClient(spy);
                hub.Apply();
                Assert.AreEqual(1, spy.applyCount);

                hub.NotifyClients(layoutOnly: true);
                Assert.AreEqual(2, spy.applyCount);

                spy.sizeReapply = false;
                hub.NotifyClients(layoutOnly: true);
                Assert.AreEqual(2, spy.applyCount);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void IsFormTransition_TrueWhenFormChanges()
        {
            Assert.IsTrue(ResponsiveFormTransitionLoading.IsFormChange(
                ResponsiveDevice.FormFactor.Phone,
                ResponsiveDevice.FormFactor.FoldPhoneCover));
            Assert.IsTrue(ResponsiveFormTransitionLoading.IsFormChange(
                ResponsiveDevice.FormFactor.FoldHalfOpened,
                ResponsiveDevice.FormFactor.FoldCoverHalfOpened));
            Assert.IsFalse(ResponsiveFormTransitionLoading.IsFormChange(
                ResponsiveDevice.FormFactor.Pad,
                ResponsiveDevice.FormFactor.Pad));
        }

        [Test]
        public void ShouldCoverFormChange_RespectsDestinationSlotFlag()
        {
            Assert.IsFalse(ResponsiveFormTransitionLoading.ShouldCoverFormChange(
                ResponsiveDevice.FormFactor.Phone,
                ResponsiveDevice.FormFactor.FlipPhoneCover,
                showOnNext: false));
            Assert.IsFalse(ResponsiveFormTransitionLoading.ShouldCoverFormChange(
                ResponsiveDevice.FormFactor.FlipHalfOpened,
                ResponsiveDevice.FormFactor.FlipPhoneCover,
                showOnNext: false));
            Assert.IsFalse(ResponsiveFormTransitionLoading.ShouldCoverFormChange(
                ResponsiveDevice.FormFactor.Phone,
                ResponsiveDevice.FormFactor.FoldPhoneCover,
                showOnNext: false));
            Assert.IsTrue(ResponsiveFormTransitionLoading.ShouldCoverFormChange(
                ResponsiveDevice.FormFactor.FlipPhoneCover,
                ResponsiveDevice.FormFactor.Phone,
                showOnNext: true));
            Assert.IsTrue(ResponsiveFormTransitionLoading.ShouldCoverFormChange(
                ResponsiveDevice.FormFactor.Phone,
                ResponsiveDevice.FormFactor.FoldPhoneCover,
                showOnNext: true));
            Assert.IsFalse(ResponsiveFormTransitionLoading.ShouldCoverFormChange(
                ResponsiveDevice.FormFactor.Pad,
                ResponsiveDevice.FormFactor.Pad,
                showOnNext: true));
        }

        [Test]
        public void FormTransitionDefaults_DisablesFlipPhoneCoverSlots()
        {
            ResponsiveActiveObjectSlots slots =
                ResponsiveActiveObjectSlots.FormTransitionDefaults();
            Assert.IsFalse(slots.IsActive(new ResponsiveDevice.LayoutKey(
                ResponsiveDevice.FormFactor.FlipPhoneCover,
                ResponsiveDevice.Orientation.Portrait)));
            Assert.IsFalse(slots.IsActive(new ResponsiveDevice.LayoutKey(
                ResponsiveDevice.FormFactor.FlipPhoneCover,
                ResponsiveDevice.Orientation.Landscape)));
            Assert.IsTrue(slots.IsActive(new ResponsiveDevice.LayoutKey(
                ResponsiveDevice.FormFactor.Phone,
                ResponsiveDevice.Orientation.Portrait)));
            Assert.IsTrue(slots.IsActive(new ResponsiveDevice.LayoutKey(
                ResponsiveDevice.FormFactor.Pad,
                ResponsiveDevice.Orientation.Landscape)));
            Assert.IsTrue(slots.IsActive(new ResponsiveDevice.LayoutKey(
                ResponsiveDevice.FormFactor.FoldHalfOpened,
                ResponsiveDevice.Orientation.Portrait)));
        }

        [Test]
        public void ShouldShowFormTransitionLoading_GatesPlayModeFormChangeAfterFirstApply()
        {
            Assert.IsFalse(ResponsiveFormTransitionLoading.ShouldShow(
                featureEnabled: false,
                isPlaying: true,
                hasBlackOverlay: true,
                hasController: true,
                hasAppliedBefore: true,
                ResponsiveDevice.FormFactor.Phone,
                ResponsiveDevice.FormFactor.FoldPhoneCover,
                showOnNext: true));
            Assert.IsFalse(ResponsiveFormTransitionLoading.ShouldShow(
                featureEnabled: true,
                isPlaying: true,
                hasBlackOverlay: true,
                hasController: true,
                hasAppliedBefore: false,
                ResponsiveDevice.FormFactor.Phone,
                ResponsiveDevice.FormFactor.FoldPhoneCover,
                showOnNext: true));
            Assert.IsFalse(ResponsiveFormTransitionLoading.ShouldShow(
                featureEnabled: true,
                isPlaying: false,
                hasBlackOverlay: true,
                hasController: true,
                hasAppliedBefore: true,
                ResponsiveDevice.FormFactor.Phone,
                ResponsiveDevice.FormFactor.FoldPhoneCover,
                showOnNext: true));
            Assert.IsFalse(ResponsiveFormTransitionLoading.ShouldShow(
                featureEnabled: true,
                isPlaying: true,
                hasBlackOverlay: false,
                hasController: false,
                hasAppliedBefore: true,
                ResponsiveDevice.FormFactor.Phone,
                ResponsiveDevice.FormFactor.FoldPhoneCover,
                showOnNext: true));
            Assert.IsFalse(ResponsiveFormTransitionLoading.ShouldShow(
                featureEnabled: true,
                isPlaying: true,
                hasBlackOverlay: true,
                hasController: true,
                hasAppliedBefore: true,
                ResponsiveDevice.FormFactor.Phone,
                ResponsiveDevice.FormFactor.Phone,
                showOnNext: true));
            Assert.IsFalse(ResponsiveFormTransitionLoading.ShouldShow(
                featureEnabled: true,
                isPlaying: true,
                hasBlackOverlay: true,
                hasController: true,
                hasAppliedBefore: true,
                ResponsiveDevice.FormFactor.Phone,
                ResponsiveDevice.FormFactor.FlipPhoneCover,
                showOnNext: false));
            Assert.IsTrue(ResponsiveFormTransitionLoading.ShouldShow(
                featureEnabled: true,
                isPlaying: true,
                hasBlackOverlay: true,
                hasController: false,
                hasAppliedBefore: true,
                ResponsiveDevice.FormFactor.Phone,
                ResponsiveDevice.FormFactor.FoldPhoneCover,
                showOnNext: true));
            Assert.IsTrue(ResponsiveFormTransitionLoading.ShouldShow(
                featureEnabled: true,
                isPlaying: true,
                hasBlackOverlay: false,
                hasController: true,
                hasAppliedBefore: true,
                ResponsiveDevice.FormFactor.Phone,
                ResponsiveDevice.FormFactor.FoldPhoneCover,
                showOnNext: true));
            Assert.IsTrue(ResponsiveFormTransitionLoading.ShouldShow(
                featureEnabled: true,
                isPlaying: true,
                hasBlackOverlay: true,
                hasController: true,
                hasAppliedBefore: true,
                ResponsiveDevice.FormFactor.FoldHalfOpened,
                ResponsiveDevice.FormFactor.Pad,
                showOnNext: true));
            Assert.IsTrue(ResponsiveFormTransitionLoading.ShouldShow(
                featureEnabled: true,
                isPlaying: true,
                hasBlackOverlay: true,
                hasController: true,
                hasAppliedBefore: true,
                ResponsiveDevice.FormFactor.FlipPhoneCover,
                ResponsiveDevice.FormFactor.Phone,
                showOnNext: true));
            Assert.IsFalse(ResponsiveFormTransitionLoading.ShouldShow(
                featureEnabled: true,
                isPlaying: true,
                hasBlackOverlay: true,
                hasController: true,
                hasAppliedBefore: true,
                ResponsiveDevice.FormFactor.Phone,
                ResponsiveDevice.FormFactor.FoldPhoneCover,
                showOnNext: false));
        }

        [Test]
        public void NotifyClients_LayoutOnly_NotifyAll_AppliesWhenSizeReapplyFalse()
        {
            var go = new GameObject("HubNotifyAll");
            try
            {
                var hub = go.AddComponent<ResponsiveUI>();
                var spy = go.AddComponent<HubClientSpy>();
                spy.sizeReapply = false;
                hub.RegisterClient(spy);
                hub.NotifyClients(layoutOnly: true, notifyAllLayoutClients: true);
                Assert.AreEqual(1, spy.applyCount);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void NotifyClients_LayoutOnly_SkipsAuthoringClient()
        {
            var go = new GameObject("HubAuthoringSkip");
            try
            {
                var hub = go.AddComponent<ResponsiveUI>();
                var authored = go.AddComponent<HubClientSpy>();
                authored.sizeReapply = true;
                authored.authoringSelected = true;
                var other = go.AddComponent<HubClientSpy>();
                other.sizeReapply = true;
                hub.RegisterClient(authored);
                hub.RegisterClient(other);

                hub.NotifyClients(layoutOnly: true, notifyAllLayoutClients: true);
                Assert.AreEqual(0, authored.applyCount);
                Assert.AreEqual(1, other.applyCount);

                hub.NotifyClients(layoutOnly: false);
                Assert.AreEqual(1, authored.applyCount);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void ScrollView_ApplyFromHub_UsesPassedLayoutNotSerializedHub()
        {
            var canvasGo = new GameObject("ScrollHubCanvas", typeof(Canvas));
            var childGo = new GameObject("ScrollClient");
            try
            {
                childGo.transform.SetParent(canvasGo.transform);
                var hub = canvasGo.AddComponent<ResponsiveUI>();
                hub.enabled = true;
                SetHubOverride(
                    hub,
                    ResponsiveDevice.FormFactor.Phone,
                    ResponsiveDevice.Orientation.Portrait);
                hub.Apply();

                var scroll = childGo.AddComponent<ResponsiveScrollView>();
                scroll.TakeLayout(DistinctScrollMetrics(), hub);
                float interval = 0f;
                float offset = 0f;
                scroll.Bind(() => scroll.Apply(ref interval, ref offset));

                scroll.ApplyFromHub(
                    hub,
                    new ResponsiveDevice.LayoutKey(
                        ResponsiveDevice.FormFactor.Pad,
                        ResponsiveDevice.Orientation.Landscape),
                    layoutOnly: false);

                Assert.AreEqual(0.77f, interval);
                Assert.AreEqual(0.31f, offset);
            }
            finally
            {
                Object.DestroyImmediate(canvasGo);
            }
        }

        [Test]
        public void ScrollView_LayoutOnlyWithChangedLayout_AppliesNewSlot()
        {
            var canvasGo = new GameObject("ScrollHubCanvas", typeof(Canvas));
            var childGo = new GameObject("ScrollClient");
            try
            {
                childGo.transform.SetParent(canvasGo.transform);
                var hub = canvasGo.AddComponent<ResponsiveUI>();
                hub.enabled = true;
                SetHubOverride(
                    hub,
                    ResponsiveDevice.FormFactor.Phone,
                    ResponsiveDevice.Orientation.Portrait);
                hub.Apply();

                var scroll = childGo.AddComponent<ResponsiveScrollView>();
                scroll.TakeLayout(DistinctScrollMetrics(), hub);
                float interval = 0f;
                float offset = 0f;
                scroll.Bind(() => scroll.Apply(ref interval, ref offset));
                scroll.ApplyFromHub(
                    hub,
                    new ResponsiveDevice.LayoutKey(
                        ResponsiveDevice.FormFactor.Phone,
                        ResponsiveDevice.Orientation.Portrait),
                    layoutOnly: false);
                Assert.AreEqual(0.11f, interval);

                // A layout-only pass that carries a new device state must not keep
                // resolving the slot cached by the last full pass.
                scroll.ApplyFromHub(
                    hub,
                    new ResponsiveDevice.LayoutKey(
                        ResponsiveDevice.FormFactor.Pad,
                        ResponsiveDevice.Orientation.Landscape),
                    layoutOnly: true);

                Assert.AreEqual(0.77f, interval);
                Assert.AreEqual(0.31f, offset);
            }
            finally
            {
                Object.DestroyImmediate(canvasGo);
            }
        }

        [Test]
        public void ScrollView_AssignHubWhileEnabled_RebindsAndIgnoresOldHubNotify()
        {
            var canvasA = new GameObject("ScrollHubA", typeof(Canvas));
            var canvasB = new GameObject("ScrollHubB", typeof(Canvas));
            var childGo = new GameObject("ScrollClient");
            try
            {
                childGo.transform.SetParent(canvasA.transform);
                var hubA = canvasA.AddComponent<ResponsiveUI>();
                var hubB = canvasB.AddComponent<ResponsiveUI>();
                hubA.enabled = true;
                hubB.enabled = true;
                SetHubOverride(
                    hubA,
                    ResponsiveDevice.FormFactor.Phone,
                    ResponsiveDevice.Orientation.Portrait);
                SetHubOverride(
                    hubB,
                    ResponsiveDevice.FormFactor.Pad,
                    ResponsiveDevice.Orientation.Landscape);
                hubA.Apply();
                hubB.Apply();

                var scroll = childGo.AddComponent<ResponsiveScrollView>();
                scroll.TakeLayout(DistinctScrollMetrics(), hubA);
                float interval = 0f;
                float offset = 0f;
                int applyCount = 0;
                scroll.Bind(() =>
                {
                    applyCount++;
                    scroll.Apply(ref interval, ref offset);
                });

                scroll.AssignHub(hubB);
                Assert.AreEqual(0.77f, interval);
                Assert.AreEqual(0.31f, offset);

                applyCount = 0;
                hubA.NotifyClients(layoutOnly: false);
                Assert.AreEqual(0, applyCount);

                hubB.NotifyClients(layoutOnly: false);
                Assert.AreEqual(1, applyCount);
                Assert.AreEqual(0.77f, interval);
                Assert.AreEqual(0.31f, offset);
            }
            finally
            {
                Object.DestroyImmediate(canvasA);
                Object.DestroyImmediate(canvasB);
            }
        }

        [Test]
        public void ScrollView_StaleHubNotifyAfterSerializedReassign_AppliesNewHubLayout()
        {
            var canvasA = new GameObject("ScrollHubA", typeof(Canvas));
            var canvasB = new GameObject("ScrollHubB", typeof(Canvas));
            var childGo = new GameObject("ScrollClient");
            try
            {
                childGo.transform.SetParent(canvasA.transform);
                var hubA = canvasA.AddComponent<ResponsiveUI>();
                var hubB = canvasB.AddComponent<ResponsiveUI>();
                hubA.enabled = true;
                hubB.enabled = true;
                SetHubOverride(
                    hubA,
                    ResponsiveDevice.FormFactor.Phone,
                    ResponsiveDevice.Orientation.Portrait);
                SetHubOverride(
                    hubB,
                    ResponsiveDevice.FormFactor.Pad,
                    ResponsiveDevice.Orientation.Landscape);
                hubA.Apply();
                hubB.Apply();

                var scroll = childGo.AddComponent<ResponsiveScrollView>();
                scroll.TakeLayout(DistinctScrollMetrics(), hubA);
                float interval = 0f;
                float offset = 0f;
                scroll.Bind(() => scroll.Apply(ref interval, ref offset));

                var so = new SerializedObject(scroll);
                so.FindProperty("responsiveUi").objectReferenceValue = hubB;
                so.ApplyModifiedPropertiesWithoutUndo();

                scroll.ApplyFromHub(
                    hubA,
                    new ResponsiveDevice.LayoutKey(
                        ResponsiveDevice.FormFactor.Phone,
                        ResponsiveDevice.Orientation.Portrait),
                    layoutOnly: false);

                Assert.AreEqual(0.77f, interval);
                Assert.AreEqual(0.31f, offset);
            }
            finally
            {
                Object.DestroyImmediate(canvasA);
                Object.DestroyImmediate(canvasB);
            }
        }

        [Test]
        public void ScrollView_ApplyAfterSerializedReassign_UsesNewHubCurrentLayout()
        {
            var canvasA = new GameObject("ScrollHubA", typeof(Canvas));
            var canvasB = new GameObject("ScrollHubB", typeof(Canvas));
            var childGo = new GameObject("ScrollClient");
            try
            {
                childGo.transform.SetParent(canvasA.transform);
                var hubA = canvasA.AddComponent<ResponsiveUI>();
                var hubB = canvasB.AddComponent<ResponsiveUI>();
                hubA.enabled = true;
                hubB.enabled = true;
                SetHubOverride(
                    hubA,
                    ResponsiveDevice.FormFactor.Phone,
                    ResponsiveDevice.Orientation.Portrait);
                SetHubOverride(
                    hubB,
                    ResponsiveDevice.FormFactor.Pad,
                    ResponsiveDevice.Orientation.Landscape);
                hubA.Apply();
                hubB.Apply();

                var scroll = childGo.AddComponent<ResponsiveScrollView>();
                scroll.TakeLayout(DistinctScrollMetrics(), hubA);
                float interval = 0f;
                float offset = 0f;
                scroll.Bind(() => scroll.Apply(ref interval, ref offset));
                scroll.ApplyFromHub(
                    hubA,
                    new ResponsiveDevice.LayoutKey(
                        ResponsiveDevice.FormFactor.Phone,
                        ResponsiveDevice.Orientation.Portrait),
                    layoutOnly: false);
                Assert.AreEqual(0.11f, interval);

                var so = new SerializedObject(scroll);
                so.FindProperty("responsiveUi").objectReferenceValue = hubB;
                so.ApplyModifiedPropertiesWithoutUndo();

                Assert.IsTrue(scroll.Apply(ref interval, ref offset));
                Assert.AreEqual(0.77f, interval);
                Assert.AreEqual(0.31f, offset);
            }
            finally
            {
                Object.DestroyImmediate(canvasA);
                Object.DestroyImmediate(canvasB);
            }
        }

        [Test]
        public void Apply_CoverFitsControllerBackground()
        {
            var canvasGo = new GameObject("CoverCanvas", typeof(RectTransform), typeof(Canvas));
            var parentGo = new GameObject("CoverParent", typeof(RectTransform));
            var bgGo = new GameObject("CoverImage", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            try
            {
                parentGo.transform.SetParent(canvasGo.transform, false);
                bgGo.transform.SetParent(parentGo.transform, false);

                var parentRt = parentGo.GetComponent<RectTransform>();
                parentRt.anchorMin = parentRt.anchorMax = new Vector2(0.5f, 0.5f);
                parentRt.sizeDelta = new Vector2(800f, 600f);

                var bgRt = bgGo.GetComponent<RectTransform>();
                bgRt.anchorMin = Vector2.zero;
                bgRt.anchorMax = Vector2.one;
                bgRt.offsetMin = Vector2.zero;
                bgRt.offsetMax = Vector2.zero;

                var hub = canvasGo.AddComponent<ResponsiveUI>();
                var hubSo = new SerializedObject(hub);
                hubSo.FindProperty("overrideOriginalBgSize").boolValue = true;
                hubSo.FindProperty("originalBgSize").vector2Value = new Vector2(400f, 200f);
                hubSo.ApplyModifiedPropertiesWithoutUndo();

                var controller = canvasGo.AddComponent<ResponsiveUiController>();
                controller.AssignHub(hub);
                var so = new SerializedObject(controller);
                so.FindProperty("backgrounds").arraySize = 1;
                so.FindProperty("backgrounds").GetArrayElementAtIndex(0)
                    .FindPropertyRelative("image").objectReferenceValue = bgGo.GetComponent<Image>();
                so.ApplyModifiedPropertiesWithoutUndo();

                Canvas.ForceUpdateCanvases();
                hub.enabled = true;
                hub.Apply();

                Assert.AreEqual(400f, bgRt.sizeDelta.x, 0.5f);
                Assert.AreEqual(0f, bgRt.sizeDelta.y, 0.5f);
            }
            finally
            {
                Object.DestroyImmediate(canvasGo);
            }
        }

        [Test]
        public void RefreshEditorLayoutAfterCanvas_RecoversBackgroundAfterCanvasSettle()
        {
            var canvasGo = new GameObject("CatchupCover", typeof(RectTransform), typeof(Canvas));
            var parentGo = new GameObject("Parent", typeof(RectTransform));
            var bgGo = new GameObject("Bg", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            UnityEngine.Object previousSelection = Selection.activeObject;
            try
            {
                Selection.activeObject = null;
                parentGo.transform.SetParent(canvasGo.transform, false);
                bgGo.transform.SetParent(parentGo.transform, false);

                var parentRt = parentGo.GetComponent<RectTransform>();
                parentRt.anchorMin = parentRt.anchorMax = new Vector2(0.5f, 0.5f);
                parentRt.sizeDelta = new Vector2(800f, 600f);

                var bgRt = bgGo.GetComponent<RectTransform>();
                bgRt.anchorMin = Vector2.zero;
                bgRt.anchorMax = Vector2.one;
                bgRt.offsetMin = Vector2.zero;
                bgRt.offsetMax = Vector2.zero;

                var hub = canvasGo.AddComponent<ResponsiveUI>();
                var hubSo = new SerializedObject(hub);
                hubSo.FindProperty("overrideOriginalBgSize").boolValue = true;
                hubSo.FindProperty("originalBgSize").vector2Value = new Vector2(400f, 200f);
                hubSo.ApplyModifiedPropertiesWithoutUndo();

                var controller = canvasGo.AddComponent<ResponsiveUiController>();
                controller.AssignHub(hub);
                var so = new SerializedObject(controller);
                so.FindProperty("backgrounds").arraySize = 1;
                so.FindProperty("backgrounds").GetArrayElementAtIndex(0)
                    .FindPropertyRelative("image").objectReferenceValue = bgGo.GetComponent<Image>();
                so.ApplyModifiedPropertiesWithoutUndo();

                Canvas.ForceUpdateCanvases();
                hub.enabled = true;
                hub.Apply();
                Assert.AreEqual(400f, bgRt.sizeDelta.x, 0.5f);

                parentRt.sizeDelta = new Vector2(1000f, 800f);
                Canvas.ForceUpdateCanvases();
                hub.RefreshEditorLayoutAfterCanvas(false, default);

                Assert.AreEqual(600f, bgRt.sizeDelta.x, 0.5f);
                Assert.AreEqual(0f, bgRt.sizeDelta.y, 0.5f);
            }
            finally
            {
                Selection.activeObject = previousSelection;
                Object.DestroyImmediate(canvasGo);
            }
        }

        [Test]
        public void RefreshEditorLayoutAfterCanvas_NotifiesClientsThatSkipSizeReapply()
        {
            var go = new GameObject("CatchupAllClients");
            try
            {
                var hub = go.AddComponent<ResponsiveUI>();
                hub.enabled = true;
                var spy = go.AddComponent<HubClientSpy>();
                spy.sizeReapply = false;
                hub.RegisterClient(spy);
                hub.Apply();
                int afterApply = spy.applyCount;

                hub.RefreshEditorLayoutAfterCanvas(false, default);
                Assert.AreEqual(afterApply + 1, spy.applyCount);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void RefreshEditorLayoutAfterCanvas_ChangedLayout_RunsFullApplyWithNewLayout()
        {
            var go = new GameObject("CatchupChangedLayout");
            try
            {
                var hub = go.AddComponent<ResponsiveUI>();
                hub.enabled = true;
                SetHubOverride(
                    hub,
                    ResponsiveDevice.FormFactor.Phone,
                    ResponsiveDevice.Orientation.Portrait);
                var spy = go.AddComponent<HubClientSpy>();
                hub.RegisterClient(spy);
                hub.Apply();

                // Detection changes without a full Apply reaching the hub first.
                SetHubOverride(
                    hub,
                    ResponsiveDevice.FormFactor.Pad,
                    ResponsiveDevice.Orientation.Landscape);
                hub.RefreshEditorLayoutAfterCanvas(false, default);

                var pad = new ResponsiveDevice.LayoutKey(
                    ResponsiveDevice.FormFactor.Pad,
                    ResponsiveDevice.Orientation.Landscape);
                Assert.AreEqual(pad, hub.CurrentLayout);
                Assert.AreEqual(pad, spy.lastLayout);
                Assert.IsFalse(spy.lastLayoutOnly);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void NotifyClients_ThrowingClient_DoesNotBlockLaterClients()
        {
            var go = new GameObject("NotifyIsolation");
            try
            {
                var hub = go.AddComponent<ResponsiveUI>();
                hub.enabled = true;
                var thrower = go.AddComponent<ThrowingHubClient>();
                var spy = go.AddComponent<HubClientSpy>();
                hub.RegisterClient(thrower);
                hub.RegisterClient(spy);
                int before = spy.applyCount;

                LogAssert.Expect(LogType.Exception, new Regex("ThrowingHubClient"));
                hub.NotifyClients(layoutOnly: false);

                Assert.AreEqual(before + 1, spy.applyCount);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void ApplyFromHub_CoversBackgroundAfterShowingWidgetParent()
        {
            var canvasGo = new GameObject("ShowThenCover", typeof(RectTransform), typeof(Canvas));
            var parentGo = new GameObject("WidgetParent", typeof(RectTransform));
            var bgGo = new GameObject("Bg", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            try
            {
                parentGo.transform.SetParent(canvasGo.transform, false);
                bgGo.transform.SetParent(parentGo.transform, false);

                var parentRt = parentGo.GetComponent<RectTransform>();
                parentRt.anchorMin = parentRt.anchorMax = new Vector2(0.5f, 0.5f);
                parentRt.sizeDelta = new Vector2(800f, 600f);

                var bgRt = bgGo.GetComponent<RectTransform>();
                bgRt.anchorMin = Vector2.zero;
                bgRt.anchorMax = Vector2.one;
                bgRt.offsetMin = Vector2.zero;
                bgRt.offsetMax = Vector2.zero;

                var hub = canvasGo.AddComponent<ResponsiveUI>();
                hub.enabled = true;
                var hubSo = new SerializedObject(hub);
                hubSo.FindProperty("overrideOriginalBgSize").boolValue = true;
                hubSo.FindProperty("originalBgSize").vector2Value = new Vector2(400f, 200f);
                hubSo.ApplyModifiedPropertiesWithoutUndo();

                ResponsiveRectState widgetState =
                    ResponsiveRectState.From(parentRt);
                widgetState.enabled = true;
                widgetState.sizeDelta = new Vector2(1000f, 800f);
                var controller = canvasGo.AddComponent<ResponsiveUiController>();
                ResponsiveEditorTestUtil.SetControllerLists(
                    controller,
                    new[]
                    {
                        new ResponsiveWidgetLayout
                        {
                            target = parentGo.GetComponent<RectTransform>(),
                            phoneLandscape = widgetState,
                            flipPhoneCoverLandscape = new ResponsiveRectState { enabled = false },
                        },
                    },
                    new[]
                    {
                        new ResponsiveBackground
                        {
                            image = bgGo.GetComponent<Image>(),
                        },
                    },
                    System.Array.Empty<ResponsiveActiveObjectEntry>(),
                    hub);

                parentGo.SetActive(false);
                controller.ApplyFromHub(
                    hub,
                    new ResponsiveDevice.LayoutKey(
                        ResponsiveDevice.FormFactor.FlipPhoneCover,
                        ResponsiveDevice.Orientation.Landscape),
                    layoutOnly: false);
                Assert.IsFalse(parentGo.activeSelf);

                parentGo.SetActive(true);
                parentRt.sizeDelta = new Vector2(1000f, 800f);
                controller.ApplyFromHub(
                    hub,
                    new ResponsiveDevice.LayoutKey(
                        ResponsiveDevice.FormFactor.Phone,
                        ResponsiveDevice.Orientation.Landscape),
                    layoutOnly: false);

                Assert.IsTrue(parentGo.activeSelf);
                Assert.AreEqual(600f, bgRt.sizeDelta.x, 0.5f);
                Assert.AreEqual(0f, bgRt.sizeDelta.y, 0.5f);
            }
            finally
            {
                Object.DestroyImmediate(canvasGo);
            }
        }

        [Test]
        public void Apply_Backgrounds_EditMode_WorksWhenControllerInactive()
        {
            var canvasGo = new GameObject("InactiveCover", typeof(RectTransform), typeof(Canvas));
            var parentGo = new GameObject("Parent", typeof(RectTransform));
            var bgGo = new GameObject("Bg", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            try
            {
                parentGo.transform.SetParent(canvasGo.transform, false);
                bgGo.transform.SetParent(parentGo.transform, false);

                var parentRt = parentGo.GetComponent<RectTransform>();
                parentRt.anchorMin = parentRt.anchorMax = new Vector2(0.5f, 0.5f);
                parentRt.sizeDelta = new Vector2(800f, 600f);

                var bgRt = bgGo.GetComponent<RectTransform>();
                bgRt.anchorMin = Vector2.zero;
                bgRt.anchorMax = Vector2.one;
                bgRt.offsetMin = Vector2.zero;
                bgRt.offsetMax = Vector2.zero;

                var hub = canvasGo.AddComponent<ResponsiveUI>();
                hub.enabled = true;
                var hubSo = new SerializedObject(hub);
                hubSo.FindProperty("overrideOriginalBgSize").boolValue = true;
                hubSo.FindProperty("originalBgSize").vector2Value = new Vector2(400f, 200f);
                hubSo.ApplyModifiedPropertiesWithoutUndo();

                var controller = canvasGo.AddComponent<ResponsiveUiController>();
                controller.AssignHub(hub);
                var so = new SerializedObject(controller);
                so.FindProperty("backgrounds").arraySize = 1;
                so.FindProperty("backgrounds").GetArrayElementAtIndex(0)
                    .FindPropertyRelative("image").objectReferenceValue = bgGo.GetComponent<Image>();
                so.ApplyModifiedPropertiesWithoutUndo();

                canvasGo.SetActive(false);
                Canvas.ForceUpdateCanvases();
                Assert.IsFalse(controller.isActiveAndEnabled);
                controller.EditorApplyBackgrounds();

                Assert.AreEqual(400f, bgRt.sizeDelta.x, 0.5f);
                Assert.AreEqual(0f, bgRt.sizeDelta.y, 0.5f);
            }
            finally
            {
                Object.DestroyImmediate(canvasGo);
            }
        }

        [Test]
        public void Controller_DisabledHub_BlocksEnableHubAndInspectorApply()
        {
            var canvasGo = new GameObject("DisabledHubCanvas", typeof(RectTransform), typeof(Canvas));
            var parentGo = new GameObject("Parent", typeof(RectTransform));
            var bgGo = new GameObject("Bg", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var widgetGo = new GameObject("Widget", typeof(RectTransform));
            var activeGo = new GameObject("Active");
            try
            {
                parentGo.transform.SetParent(canvasGo.transform, false);
                bgGo.transform.SetParent(parentGo.transform, false);
                widgetGo.transform.SetParent(canvasGo.transform, false);
                activeGo.transform.SetParent(canvasGo.transform, false);

                var parentRt = parentGo.GetComponent<RectTransform>();
                parentRt.anchorMin = parentRt.anchorMax = new Vector2(0.5f, 0.5f);
                parentRt.sizeDelta = new Vector2(800f, 600f);

                var bgRt = bgGo.GetComponent<RectTransform>();
                bgRt.anchorMin = Vector2.zero;
                bgRt.anchorMax = Vector2.one;
                bgRt.sizeDelta = new Vector2(-123f, -45f);

                var widgetRt = widgetGo.GetComponent<RectTransform>();
                widgetRt.anchoredPosition = new Vector2(7f, 9f);

                var hub = canvasGo.AddComponent<ResponsiveUI>();
                hub.enabled = false;
                var hubSo = new SerializedObject(hub);
                hubSo.FindProperty("overrideOriginalBgSize").boolValue = true;
                hubSo.FindProperty("originalBgSize").vector2Value = new Vector2(400f, 200f);
                hubSo.ApplyModifiedPropertiesWithoutUndo();

                var controller = canvasGo.AddComponent<ResponsiveUiController>();
                controller.AssignHub(hub);
                var so = new SerializedObject(controller);
                so.FindProperty("backgrounds").arraySize = 1;
                so.FindProperty("backgrounds").GetArrayElementAtIndex(0)
                    .FindPropertyRelative("image").objectReferenceValue = bgGo.GetComponent<Image>();
                so.FindProperty("widgets").arraySize = 1;
                SerializedProperty widget = so.FindProperty("widgets").GetArrayElementAtIndex(0);
                widget.FindPropertyRelative("target").objectReferenceValue = widgetRt;
                SerializedProperty phone = widget.FindPropertyRelative("phonePortrait");
                phone.FindPropertyRelative("enabled").boolValue = true;
                phone.FindPropertyRelative("anchoredPosition").vector2Value = new Vector2(100f, 200f);
                so.FindProperty("activeObjects").arraySize = 1;
                so.FindProperty("activeObjects").GetArrayElementAtIndex(0)
                    .FindPropertyRelative("target").objectReferenceValue = activeGo;
                so.ApplyModifiedPropertiesWithoutUndo();

                controller.enabled = false;
                controller.enabled = true;
                controller.ApplyFromHub(
                    hub,
                    new ResponsiveDevice.LayoutKey(
                        ResponsiveDevice.FormFactor.Phone,
                        ResponsiveDevice.Orientation.Portrait),
                    layoutOnly: false);
                controller.EditorApplyBackgrounds();

                Assert.AreEqual(new Vector2(7f, 9f), widgetRt.anchoredPosition);
                Assert.IsTrue(activeGo.activeSelf);
                Assert.AreEqual(new Vector2(-123f, -45f), bgRt.sizeDelta);
            }
            finally
            {
                Object.DestroyImmediate(canvasGo);
            }
        }

        [Test]
        public void ApplyCoverHeight_StretchWidth_ClearsLetterboxX()
        {
            var parentGo = new GameObject("HOnlyParent", typeof(RectTransform));
            var bgGo = new GameObject("HOnlyBg", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            try
            {
                bgGo.transform.SetParent(parentGo.transform, false);
                var parentRt = parentGo.GetComponent<RectTransform>();
                parentRt.anchorMin = parentRt.anchorMax = new Vector2(0.5f, 0.5f);
                parentRt.sizeDelta = new Vector2(800f, 600f);

                var bgRt = bgGo.GetComponent<RectTransform>();
                bgRt.anchorMin = Vector2.zero;
                bgRt.anchorMax = Vector2.one;
                bgRt.sizeDelta = new Vector2(-200f, 0f);

                Canvas.ForceUpdateCanvases();
                ResponsiveBackgroundCover.ApplyCoverHeight(
                    bgGo.GetComponent<Image>(),
                    new Vector2(400f, 200f),
                    parentRt.rect.size);

                Assert.AreEqual(0f, bgRt.sizeDelta.x, 0.5f);
                Assert.AreEqual(0f, bgRt.sizeDelta.y, 0.5f);
            }
            finally
            {
                Object.DestroyImmediate(parentGo);
            }
        }

        [Test]
        public void ApplyCoverHeight_NonStretchWidth_KeepsSizeDeltaX()
        {
            var parentGo = new GameObject("HOnlyKeepXParent", typeof(RectTransform));
            var bgGo = new GameObject("HOnlyKeepXBg", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            try
            {
                bgGo.transform.SetParent(parentGo.transform, false);
                var parentRt = parentGo.GetComponent<RectTransform>();
                parentRt.anchorMin = parentRt.anchorMax = new Vector2(0.5f, 0.5f);
                parentRt.sizeDelta = new Vector2(800f, 600f);

                var bgRt = bgGo.GetComponent<RectTransform>();
                bgRt.anchorMin = bgRt.anchorMax = new Vector2(0.5f, 0.5f);
                bgRt.sizeDelta = new Vector2(420f, 100f);

                Canvas.ForceUpdateCanvases();
                ResponsiveBackgroundCover.ApplyCoverHeight(
                    bgGo.GetComponent<Image>(),
                    new Vector2(400f, 200f),
                    parentRt.rect.size);

                Assert.AreEqual(420f, bgRt.sizeDelta.x, 0.5f);
                Assert.AreEqual(600f, bgRt.sizeDelta.y, 0.5f);
            }
            finally
            {
                Object.DestroyImmediate(parentGo);
            }
        }

        [Test]
        public void ApplyCoverWidth_StretchHeight_ClearsLetterboxY()
        {
            var parentGo = new GameObject("WOnlyParent", typeof(RectTransform));
            var bgGo = new GameObject("WOnlyBg", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            try
            {
                bgGo.transform.SetParent(parentGo.transform, false);
                var parentRt = parentGo.GetComponent<RectTransform>();
                parentRt.anchorMin = parentRt.anchorMax = new Vector2(0.5f, 0.5f);
                parentRt.sizeDelta = new Vector2(800f, 600f);

                var bgRt = bgGo.GetComponent<RectTransform>();
                bgRt.anchorMin = Vector2.zero;
                bgRt.anchorMax = Vector2.one;
                bgRt.sizeDelta = new Vector2(0f, -200f);

                Canvas.ForceUpdateCanvases();
                // CoverScale = max(800/400, 600/200) = 3 → display width 1200.
                // Full-stretch: sizeDelta.x = 1200 - 800 = 400; sizeDelta.y cleared to 0.
                ResponsiveBackgroundCover.ApplyCoverWidth(
                    bgGo.GetComponent<Image>(),
                    new Vector2(400f, 200f),
                    parentRt.rect.size);

                Assert.AreEqual(400f, bgRt.sizeDelta.x, 0.5f);
                Assert.AreEqual(0f, bgRt.sizeDelta.y, 0.5f);
            }
            finally
            {
                Object.DestroyImmediate(parentGo);
            }
        }

        [Test]
        public void ApplyCoverWidth_NonStretchHeight_KeepsSizeDeltaY()
        {
            var parentGo = new GameObject("WOnlyKeepYParent", typeof(RectTransform));
            var bgGo = new GameObject("WOnlyKeepYBg", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            try
            {
                bgGo.transform.SetParent(parentGo.transform, false);
                var parentRt = parentGo.GetComponent<RectTransform>();
                parentRt.anchorMin = parentRt.anchorMax = new Vector2(0.5f, 0.5f);
                parentRt.sizeDelta = new Vector2(800f, 600f);

                var bgRt = bgGo.GetComponent<RectTransform>();
                bgRt.anchorMin = bgRt.anchorMax = new Vector2(0.5f, 0.5f);
                bgRt.sizeDelta = new Vector2(100f, 333f);

                Canvas.ForceUpdateCanvases();
                ResponsiveBackgroundCover.ApplyCoverWidth(
                    bgGo.GetComponent<Image>(),
                    new Vector2(400f, 200f),
                    parentRt.rect.size);

                Assert.AreEqual(1200f, bgRt.sizeDelta.x, 0.5f);
                Assert.AreEqual(333f, bgRt.sizeDelta.y, 0.5f);
            }
            finally
            {
                Object.DestroyImmediate(parentGo);
            }
        }

        [Test]
        public void ApplyBackground_Manual_LeavesSizeDelta()
        {
            var parentGo = new GameObject("ManualParent", typeof(RectTransform));
            var bgGo = new GameObject("ManualBg", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            try
            {
                bgGo.transform.SetParent(parentGo.transform, false);
                var parentRt = parentGo.GetComponent<RectTransform>();
                parentRt.anchorMin = parentRt.anchorMax = new Vector2(0.5f, 0.5f);
                parentRt.sizeDelta = new Vector2(800f, 600f);

                var bgRt = bgGo.GetComponent<RectTransform>();
                bgRt.anchorMin = bgRt.anchorMax = new Vector2(0.5f, 0.5f);
                bgRt.sizeDelta = new Vector2(111f, 222f);

                Canvas.ForceUpdateCanvases();
                ResponsiveBackgroundCover.ApplyBackground(
                    bgGo.GetComponent<Image>(),
                    new Vector2(400f, 200f),
                    parentRt.rect.size,
                    ResponsiveBgCoverMode.Manual);

                Assert.AreEqual(111f, bgRt.sizeDelta.x, 0.5f);
                Assert.AreEqual(222f, bgRt.sizeDelta.y, 0.5f);
            }
            finally
            {
                Object.DestroyImmediate(parentGo);
            }
        }

        [Test]
        public void Controller_SizeChangeRequiresReapply_BackgroundsAlone_False()
        {
            var go = new GameObject("HubBgSize");
            try
            {
                var controller = go.AddComponent<ResponsiveUiController>();
                Assert.IsFalse(controller.SizeChangeRequiresReapply());

                var so = new SerializedObject(controller);
                so.FindProperty("backgrounds").arraySize = 1;
                so.ApplyModifiedPropertiesWithoutUndo();
                // Cover is hub ApplyLayout — backgrounds must not force a widget client reapply.
                Assert.IsFalse(controller.SizeChangeRequiresReapply());
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void ScrollView_SizeChangeRequiresReapply_True()
        {
            var go = new GameObject("HubScrollSize");
            try
            {
                var scroll = go.AddComponent<ResponsiveScrollView>();
                Assert.IsTrue(scroll.SizeChangeRequiresReapply());
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void ScrollView_LayoutOnly_InvokesApplyForViewportRelayout()
        {
            var go = new GameObject("HubScrollApply");
            try
            {
                var hub = go.AddComponent<ResponsiveUI>();
                hub.enabled = true;
                var scroll = go.AddComponent<ResponsiveScrollView>();
                scroll.AssignHub(hub);

                int applyCount = 0;
                scroll.Bind(() => applyCount++);
                int initialCount = applyCount;
                var layout = new ResponsiveDevice.LayoutKey(
                    ResponsiveDevice.FormFactor.Phone,
                    ResponsiveDevice.Orientation.Landscape);

                scroll.ApplyFromHub(hub, layout, layoutOnly: true);
                Assert.AreEqual(initialCount + 1, applyCount);

                hub.enabled = false;
                scroll.ApplyFromHub(hub, layout, layoutOnly: false);
                Assert.AreEqual(initialCount + 1, applyCount);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Controller_SizeChangeRequiresReapply_WhenDesignScaleWidget()
        {
            var go = new GameObject("HubDesignScale");
            try
            {
                var controller = go.AddComponent<ResponsiveUiController>();
                var so = new SerializedObject(controller);
                so.FindProperty("widgets").arraySize = 1;
                SerializedProperty phonePortrait = so.FindProperty("widgets").GetArrayElementAtIndex(0)
                    .FindPropertyRelative("phonePortrait");
                phonePortrait.FindPropertyRelative("enabled").boolValue = true;
                phonePortrait.FindPropertyRelative("scaleFromDesignCanvas").boolValue = true;
                so.ApplyModifiedPropertiesWithoutUndo();
                Assert.IsTrue(controller.SizeChangeRequiresReapply());
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Controller_SizeChangeRequiresReapply_DisabledPad_UsesPhoneFallback_True()
        {
            var go = new GameObject("HubDisabledSlot", typeof(Canvas));
            try
            {
                var hub = go.AddComponent<ResponsiveUI>();
                hub.enabled = true;
                SetHubOverride(
                    hub,
                    ResponsiveDevice.FormFactor.Pad,
                    ResponsiveDevice.Orientation.Landscape);
                var controller = go.AddComponent<ResponsiveUiController>();
                controller.AssignHub(hub);
                var so = new SerializedObject(controller);
                so.FindProperty("widgets").arraySize = 1;
                SerializedProperty widget = so.FindProperty("widgets").GetArrayElementAtIndex(0);
                SerializedProperty phoneLandscape = widget.FindPropertyRelative("phoneLandscape");
                phoneLandscape.FindPropertyRelative("enabled").boolValue = true;
                phoneLandscape.FindPropertyRelative("scaleFromDesignCanvas").boolValue = true;
                widget.FindPropertyRelative("padLandscape")
                    .FindPropertyRelative("enabled").boolValue = false;
                so.ApplyModifiedPropertiesWithoutUndo();

                hub.Apply();
                Assert.IsTrue(controller.SizeChangeRequiresReapply());
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Controller_SizeChangeRequiresReapply_NestedCanvas_TrueWithHubScaler()
        {
            var root = new GameObject("HubNestedRoot", typeof(Canvas), typeof(CanvasScaler));
            try
            {
                var scaler = root.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                var hub = root.AddComponent<ResponsiveUI>();
                var hubSo = new SerializedObject(hub);
                hubSo.FindProperty("applyCanvasScaler").boolValue = true;
                hubSo.FindProperty("canvasScaler").objectReferenceValue = scaler;
                hubSo.ApplyModifiedPropertiesWithoutUndo();

                var nested = new GameObject("NestedPane", typeof(Canvas), typeof(RectTransform));
                nested.transform.SetParent(root.transform, false);
                var nestedRt = nested.GetComponent<RectTransform>();
                nestedRt.anchorMin = new Vector2(0.5f, 0.5f);
                nestedRt.anchorMax = new Vector2(0.5f, 0.5f);
                nestedRt.sizeDelta = new Vector2(800f, 400f);

                var widgetGo = new GameObject("NestedWidget", typeof(RectTransform));
                widgetGo.transform.SetParent(nested.transform, false);
                var widgetRt = widgetGo.GetComponent<RectTransform>();

                var controller = root.AddComponent<ResponsiveUiController>();
                controller.AssignHub(hub);
                var so = new SerializedObject(controller);
                SerializedProperty widgets = so.FindProperty("widgets");
                widgets.arraySize = 1;
                SerializedProperty widget = widgets.GetArrayElementAtIndex(0);
                widget.FindPropertyRelative("target").objectReferenceValue = widgetRt;
                for (int i = 0; i < ResponsiveWidgetLayout.StatePropertyNames.Length; i++)
                {
                    widget.FindPropertyRelative(ResponsiveWidgetLayout.StatePropertyNames[i])
                        .FindPropertyRelative("scaleFromDesignCanvas").boolValue = true;
                    widget.FindPropertyRelative(ResponsiveWidgetLayout.StatePropertyNames[i])
                        .FindPropertyRelative("enabled").boolValue = true;
                }
                so.ApplyModifiedPropertiesWithoutUndo();

                Canvas.ForceUpdateCanvases();
                Assert.IsTrue(controller.SizeChangeRequiresReapply());
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Controller_SizeChangeRequiresReapply_RootCanvasWithScaler_False()
        {
            var root = new GameObject("HubScalerRoot", typeof(Canvas), typeof(CanvasScaler));
            try
            {
                var scaler = root.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                var hub = root.AddComponent<ResponsiveUI>();
                var hubSo = new SerializedObject(hub);
                hubSo.FindProperty("applyCanvasScaler").boolValue = true;
                hubSo.FindProperty("canvasScaler").objectReferenceValue = scaler;
                hubSo.ApplyModifiedPropertiesWithoutUndo();

                var widgetGo = new GameObject("RootWidget", typeof(RectTransform));
                widgetGo.transform.SetParent(root.transform, false);
                var widgetRt = widgetGo.GetComponent<RectTransform>();

                var controller = root.AddComponent<ResponsiveUiController>();
                controller.AssignHub(hub);
                var so = new SerializedObject(controller);
                SerializedProperty widgets = so.FindProperty("widgets");
                widgets.arraySize = 1;
                SerializedProperty widget = widgets.GetArrayElementAtIndex(0);
                widget.FindPropertyRelative("target").objectReferenceValue = widgetRt;
                for (int i = 0; i < ResponsiveWidgetLayout.StatePropertyNames.Length; i++)
                {
                    widget.FindPropertyRelative(ResponsiveWidgetLayout.StatePropertyNames[i])
                        .FindPropertyRelative("scaleFromDesignCanvas").boolValue = true;
                }
                so.ApplyModifiedPropertiesWithoutUndo();

                Assert.IsFalse(controller.SizeChangeRequiresReapply());
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ApplyNow_DuringHubApply_DoesNotRecurse()
        {
            var go = new GameObject("HubApplyInFlight");
            try
            {
                var hub = go.AddComponent<ResponsiveUI>();
                var spy = go.AddComponent<RecursingHubClientSpy>();
                hub.RegisterClient(spy);
                hub.Apply();
                Assert.AreEqual(1, spy.applyCount);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void NotifyClients_ClientRegisteredDuringApply_ReceivesCurrentLayout()
        {
            var go = new GameObject("HubRegisterDuringApply");
            try
            {
                var hub = go.AddComponent<ResponsiveUI>();
                var added = go.AddComponent<HubClientSpy>();
                var registering = go.AddComponent<RegisteringHubClientSpy>();
                registering.clientToRegister = added;
                hub.RegisterClient(registering);

                hub.Apply();

                Assert.AreEqual(1, registering.applyCount);
                Assert.AreEqual(1, added.applyCount);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void NotifyClients_ControllerEnabledDuringApply_SettlesBeforeLateClient()
        {
            var root = new GameObject(
                "HubMidApplyRegistration",
                typeof(RectTransform),
                typeof(Canvas));
            var ownerGo = new GameObject("Owner");
            var enabledGo = new GameObject(
                "EnabledController",
                typeof(RectTransform));
            try
            {
                ownerGo.transform.SetParent(root.transform, false);
                enabledGo.transform.SetParent(root.transform, false);

                var hub = root.AddComponent<ResponsiveUI>();
                hub.enabled = true;
                SetHubOverride(
                    hub,
                    ResponsiveDevice.FormFactor.Phone,
                    ResponsiveDevice.Orientation.Portrait);

                RectTransform target = enabledGo.GetComponent<RectTransform>();
                ResponsiveRectState state = ResponsiveRectState.From(target);
                state.enabled = true;
                state.defaultScale = Vector2.one;
                state.sizeDelta = new Vector2(321f, 123f);

                enabledGo.SetActive(false);
                var enabledController =
                    enabledGo.AddComponent<ResponsiveUiController>();
                ResponsiveEditorTestUtil.SetControllerLists(
                    enabledController,
                    new[]
                    {
                        new ResponsiveWidgetLayout
                        {
                            target = target,
                            phonePortrait = state,
                        },
                    },
                    System.Array.Empty<ResponsiveBackground>(),
                    System.Array.Empty<ResponsiveActiveObjectEntry>(),
                    hub);

                var owner = ownerGo.AddComponent<ResponsiveUiController>();
                var activeSlots = new ResponsiveActiveObjectSlots
                {
                    phonePortrait = true,
                };
                ResponsiveEditorTestUtil.SetControllerLists(
                    owner,
                    System.Array.Empty<ResponsiveWidgetLayout>(),
                    System.Array.Empty<ResponsiveBackground>(),
                    new[]
                    {
                        new ResponsiveActiveObjectEntry
                        {
                            target = enabledGo,
                            slots = activeSlots,
                        },
                    },
                    hub);

                var reader = root.AddComponent<GeometryReadingHubClientSpy>();
                reader.target = target;
                reader.applyCount = 0;
                reader.observedSize = Vector2.zero;
                hub.RegisterClient(reader);

                hub.Apply();

                Assert.AreEqual(1, reader.applyCount);
                Assert.AreEqual(state.sizeDelta, reader.observedSize);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void Apply_ReenteredFromActiveObjectEnable_DefersWithoutThrowing()
        {
            var root = new GameObject(
                "HubReentrantApply",
                typeof(RectTransform),
                typeof(Canvas));
            var ownerGo = new GameObject("Owner");
            var toggledGo = new GameObject("ToggledOwnerChange");
            var laterGo = new GameObject("LaterController", typeof(RectTransform));
            try
            {
                ownerGo.transform.SetParent(root.transform, false);
                toggledGo.transform.SetParent(root.transform, false);
                laterGo.transform.SetParent(root.transform, false);

                var hub = root.AddComponent<ResponsiveUI>();
                hub.enabled = true;
                SetHubOverride(
                    hub,
                    ResponsiveDevice.FormFactor.Phone,
                    ResponsiveDevice.Orientation.Portrait);

                // Stands in for a FoldablePostureDetector whose enable raises
                // ActiveInstanceChanged → hub.Apply() while the hub is notifying.
                var reentrant = toggledGo.AddComponent<ApplyHubOnEnableSpy>();

                var owner = ownerGo.AddComponent<ResponsiveUiController>();
                ResponsiveEditorTestUtil.SetControllerLists(
                    owner,
                    System.Array.Empty<ResponsiveWidgetLayout>(),
                    System.Array.Empty<ResponsiveBackground>(),
                    new[]
                    {
                        new ResponsiveActiveObjectEntry
                        {
                            target = toggledGo,
                            slots = new ResponsiveActiveObjectSlots { phonePortrait = true },
                        },
                    },
                    hub);

                RectTransform laterTarget = laterGo.GetComponent<RectTransform>();
                ResponsiveRectState state = ResponsiveRectState.From(laterTarget);
                state.enabled = true;
                state.defaultScale = Vector2.one;
                state.sizeDelta = new Vector2(321f, 123f);
                var later = laterGo.AddComponent<ResponsiveUiController>();
                ResponsiveEditorTestUtil.SetControllerLists(
                    later,
                    new[]
                    {
                        new ResponsiveWidgetLayout
                        {
                            target = laterTarget,
                            phonePortrait = state,
                        },
                    },
                    System.Array.Empty<ResponsiveBackground>(),
                    System.Array.Empty<ResponsiveActiveObjectEntry>(),
                    hub);

                toggledGo.SetActive(false);
                laterTarget.sizeDelta = Vector2.zero;
                reentrant.hub = hub;

                Assert.DoesNotThrow(() => hub.Apply());

                Assert.AreEqual(1, reentrant.enableCount);
                Assert.IsTrue(toggledGo.activeSelf);
                Assert.AreEqual(state.sizeDelta, laterTarget.sizeDelta);
                Assert.IsFalse(hub.ApplyInFlight);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void NotifyClients_ReenteredFromClient_RerunsAfterOuterPass()
        {
            var go = new GameObject("HubReentrantNotify");
            try
            {
                var hub = go.AddComponent<ResponsiveUI>();
                var reentering = go.AddComponent<NotifyingHubClientSpy>();
                var after = go.AddComponent<HubClientSpy>();
                hub.RegisterClient(reentering);
                hub.RegisterClient(after);

                Assert.DoesNotThrow(() => hub.NotifyClients(layoutOnly: false));

                // Outer pass (1) + one deferred pass (2); the nested call does not
                // clear the outer pass, so the second client is not skipped.
                Assert.AreEqual(2, reentering.applyCount);
                Assert.AreEqual(2, after.applyCount);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void ApplyFromHub_StaleHub_KeepsBoundHub()
        {
            var aGo = new GameObject("HubA", typeof(Canvas));
            var bGo = new GameObject("HubB", typeof(Canvas));
            try
            {
                var hubA = aGo.AddComponent<ResponsiveUI>();
                hubA.enabled = true;
                var hubB = bGo.AddComponent<ResponsiveUI>();
                hubB.enabled = true;
                var layout = hubB.CurrentLayout;

                var controller = aGo.AddComponent<ResponsiveUiController>();
                controller.AssignHub(hubA);
                controller.ApplyFromHub(hubB, layout, layoutOnly: false);
                Assert.AreSame(hubA, controller.Hub);

                var camera = aGo.AddComponent<ResponsiveCamera>();
                camera.AssignHub(hubA);
                camera.ApplyFromHub(hubB, layout, layoutOnly: false);
                Assert.AreSame(hubA, camera.Hub);

                var scroll = aGo.AddComponent<ResponsiveScrollView>();
                scroll.AssignHub(hubA);
                scroll.ApplyFromHub(hubB, layout, layoutOnly: false);
                Assert.AreSame(hubA, scroll.Hub);

                var twoSide = aGo.AddComponent<ResponsiveTwoSideUiTweenController>();
                twoSide.AssignHub(hubA);
                twoSide.ApplyFromHub(hubB, layout, layoutOnly: false);
                Assert.AreSame(hubA, twoSide.Hub);

                var banner = aGo.AddComponent<ResponsiveTextBanner>();
                var bannerSo = new SerializedObject(banner);
                bannerSo.FindProperty("responsiveUi").objectReferenceValue = hubA;
                bannerSo.ApplyModifiedPropertiesWithoutUndo();
                banner.ApplyFromHub(hubB, layout, layoutOnly: false);
                Assert.AreSame(hubA, banner.Hub);
            }
            finally
            {
                Object.DestroyImmediate(aGo);
                Object.DestroyImmediate(bGo);
            }
        }
    }

    class ThrowingHubClient : MonoBehaviour, IResponsiveUiClient
    {
        public ResponsiveUI Hub => GetComponent<ResponsiveUI>();

        public void ApplyFromHub(
            ResponsiveUI hub,
            in ResponsiveDevice.LayoutKey layout,
            bool layoutOnly) =>
            throw new System.InvalidOperationException("ThrowingHubClient");

        public bool SizeChangeRequiresReapply() => true;

#if UNITY_EDITOR
        public bool IsAuthoringSelectedTarget() => false;
#endif
    }

    class HubClientSpy : MonoBehaviour, IResponsiveUiClient
    {
        public int applyCount;
        public bool sizeReapply = true;
        public bool authoringSelected;
        public ResponsiveDevice.LayoutKey lastLayout;
        public bool lastLayoutOnly;

        public ResponsiveUI Hub => GetComponent<ResponsiveUI>();

        public virtual void ApplyFromHub(
            ResponsiveUI hub,
            in ResponsiveDevice.LayoutKey layout,
            bool layoutOnly)
        {
            applyCount++;
            lastLayout = layout;
            lastLayoutOnly = layoutOnly;
        }

        public bool SizeChangeRequiresReapply() => sizeReapply;

#if UNITY_EDITOR
        public bool IsAuthoringSelectedTarget() => authoringSelected;
#endif
    }

    class RecursingHubClientSpy : HubClientSpy
    {
        public override void ApplyFromHub(
            ResponsiveUI hub,
            in ResponsiveDevice.LayoutKey layout,
            bool layoutOnly)
        {
            applyCount++;
            ResponsiveUiHub.ApplyNow(this, layoutOnly);
        }
    }

    class RegisteringHubClientSpy : HubClientSpy
    {
        public IResponsiveUiClient clientToRegister;

        public override void ApplyFromHub(
            ResponsiveUI hub,
            in ResponsiveDevice.LayoutKey layout,
            bool layoutOnly)
        {
            applyCount++;
            hub.RegisterClient(clientToRegister);
            ResponsiveUiHub.ApplyNow(clientToRegister, layoutOnly);
        }
    }

    class NotifyingHubClientSpy : HubClientSpy
    {
        bool _reentered;

        public override void ApplyFromHub(
            ResponsiveUI hub,
            in ResponsiveDevice.LayoutKey layout,
            bool layoutOnly)
        {
            applyCount++;
            if (_reentered)
                return;
            _reentered = true;
            hub.NotifyClients(layoutOnly);
        }
    }

    [ExecuteAlways]
    class ApplyHubOnEnableSpy : MonoBehaviour
    {
        public ResponsiveUI hub;
        public int enableCount;

        void OnEnable()
        {
            if (hub == null)
                return;
            enableCount++;
            hub.Apply();
        }
    }

    class GeometryReadingHubClientSpy : HubClientSpy
    {
        public RectTransform target;
        public Vector2 observedSize;

        public override void ApplyFromHub(
            ResponsiveUI hub,
            in ResponsiveDevice.LayoutKey layout,
            bool layoutOnly)
        {
            applyCount++;
            observedSize = target.sizeDelta;
        }
    }
}
