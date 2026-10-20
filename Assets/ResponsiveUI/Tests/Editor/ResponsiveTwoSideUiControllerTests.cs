using System.Reflection;
using FDPlus.ResponsiveUI;
using FDPlus.ResponsiveUI.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

using static FDPlus.ResponsiveUI.Tests.ResponsiveEditorTestUtil;

namespace FDPlus.ResponsiveUI.Tests
{
    public class ResponsiveTwoSideUiTweenControllerTests
    {
        [TestCase(ResponsiveUiSide.Left, false, 1f)]
        [TestCase(ResponsiveUiSide.Left, true, 2f)]
        [TestCase(ResponsiveUiSide.Right, false, 3f)]
        [TestCase(ResponsiveUiSide.Right, true, 4f)]
        public void GetLayout_SelectsSideAndTweenState(
            ResponsiveUiSide side,
            bool animationStart,
            float expectedX)
        {
            ResponsiveTwoSideWidget widget = new ResponsiveTwoSideWidget
            {
                animationEnabled = true,
                leftNormal = Layout(1f),
                leftAnimationStart = Layout(2f),
                rightNormal = Layout(3f),
                rightAnimationStart = Layout(4f),
            };

            ResponsiveWidgetLayout selected =
                widget.GetLayout(side, animationStart);

            Assert.AreEqual(
                expectedX,
                selected.phonePortrait.anchoredPosition.x);
        }

        [Test]
        public void GetLayout_AnimationDisabled_AlwaysUsesNormal()
        {
            ResponsiveTwoSideWidget widget = new ResponsiveTwoSideWidget
            {
                animationEnabled = false,
                leftNormal = Layout(1f),
                leftAnimationStart = Layout(2f),
            };

            ResponsiveWidgetLayout selected =
                widget.GetLayout(ResponsiveUiSide.Left, animationStart: true);

            Assert.AreEqual(1f, selected.phonePortrait.anchoredPosition.x);
        }

        [TestCase(ResponsiveUiSide.Left, false)]
        [TestCase(ResponsiveUiSide.Left, true)]
        [TestCase(ResponsiveUiSide.Right, false)]
        [TestCase(ResponsiveUiSide.Right, true)]
        public void GetTarget_MatchesGetLayoutTarget(
            ResponsiveUiSide side,
            bool animationStart)
        {
            var leftNormalRt = NewRect("LeftNormal");
            var leftStartRt = NewRect("LeftStart");
            var rightNormalRt = NewRect("RightNormal");
            var rightStartRt = NewRect("RightStart");
            try
            {
                ResponsiveTwoSideWidget widget = new ResponsiveTwoSideWidget
                {
                    animationEnabled = true,
                    leftNormal = { target = leftNormalRt },
                    leftAnimationStart = { target = leftStartRt },
                    rightNormal = { target = rightNormalRt },
                    rightAnimationStart = { target = rightStartRt },
                };

                Assert.AreSame(
                    widget.GetLayout(side, animationStart).target,
                    widget.GetTarget(side, animationStart));

                widget.animationEnabled = false;
                Assert.AreSame(
                    widget.GetLayout(side, animationStart).target,
                    widget.GetTarget(side, animationStart));
            }
            finally
            {
                Object.DestroyImmediate(leftNormalRt.gameObject);
                Object.DestroyImmediate(leftStartRt.gameObject);
                Object.DestroyImmediate(rightNormalRt.gameObject);
                Object.DestroyImmediate(rightStartRt.gameObject);
            }
        }

        [Test]
        public void SetVisible_OnOneTarget_LeavesSiblingWidgetVisible()
        {
            var controllerObject = new GameObject(
                "TwoSideVisibility",
                typeof(RectTransform));
            var shot = new GameObject("Shot", typeof(RectTransform));
            var powerup = new GameObject("Powerup", typeof(RectTransform));
            var shotChild = new GameObject("Cue Slider", typeof(RectTransform));
            shotChild.transform.SetParent(shot.transform, false);
            try
            {
                ResponsiveTwoSideUiTweenController controller =
                    controllerObject.AddComponent<
                        ResponsiveTwoSideUiTweenController>();
                SerializedObject serialized = new SerializedObject(controller);
                SerializedProperty widgets = serialized.FindProperty("widgets");
                widgets.arraySize = 2;
                AssignTargets(
                    widgets.GetArrayElementAtIndex(0),
                    shot.GetComponent<RectTransform>());
                AssignTargets(
                    widgets.GetArrayElementAtIndex(1),
                    powerup.GetComponent<RectTransform>());
                serialized.ApplyModifiedPropertiesWithoutUndo();

                Assert.IsTrue(controller.IsWidgetVisible(shot.transform));
                Assert.IsTrue(controller.IsWidgetVisible(powerup.transform));
                Assert.IsTrue(controller.FindWidgetIndex(
                    shotChild.transform,
                    out int shotIndex));
                Assert.AreEqual(0, shotIndex);

                controller.SetVisibleImmediate(shotChild.transform, false);

                Assert.IsFalse(controller.IsWidgetVisible(shot.transform));
                Assert.IsFalse(controller.IsWidgetVisible(shotChild.transform));
                Assert.IsTrue(controller.IsWidgetVisible(powerup.transform));
                Assert.IsFalse(controller.IsVisible);

                controller.SetVisibleImmediate(powerup.transform, false);
                Assert.IsFalse(controller.IsWidgetVisible(powerup.transform));

                controller.SetVisibleImmediate(true);
                Assert.IsTrue(controller.IsWidgetVisible(shot.transform));
                Assert.IsTrue(controller.IsWidgetVisible(powerup.transform));
                Assert.IsTrue(controller.IsVisible);
            }
            finally
            {
                Object.DestroyImmediate(shotChild);
                Object.DestroyImmediate(shot);
                Object.DestroyImmediate(powerup);
                Object.DestroyImmediate(controllerObject);
            }
        }

        [Test]
        public void SharedTargetRetarget_PreservesAllAuthoredLayouts()
        {
            GameObject controllerObject = new GameObject("Controller");
            GameObject oldTargetObject = new GameObject(
                "Old Target",
                typeof(RectTransform));
            GameObject newTargetObject = new GameObject(
                "New Target",
                typeof(RectTransform));
            try
            {
                ResponsiveTwoSideUiTweenController controller =
                    controllerObject.AddComponent<
                        ResponsiveTwoSideUiTweenController>();
                SerializedObject serialized = new SerializedObject(controller);
                SerializedProperty widgets = serialized.FindProperty("widgets");
                widgets.arraySize = 1;
                SerializedProperty widget = widgets.GetArrayElementAtIndex(0);
                string[] layouts =
                {
                    "leftNormal",
                    "leftAnimationStart",
                    "rightNormal",
                    "rightAnimationStart",
                };
                for (int i = 0; i < layouts.Length; i++)
                {
                    SerializedProperty layout =
                        widget.FindPropertyRelative(layouts[i]);
                    layout.FindPropertyRelative("target").objectReferenceValue =
                        newTargetObject.transform;
                    layout
                        .FindPropertyRelative("phonePortrait")
                        .FindPropertyRelative("anchoredPosition")
                        .vector2Value = new Vector2(i + 1f, 0f);
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();

                MethodInfo applyTargetChange =
                    typeof(ResponsiveTwoSideWidgetDrawer).GetMethod(
                        "ApplyTargetChange",
                        BindingFlags.NonPublic | BindingFlags.Static);
                applyTargetChange.Invoke(
                    null,
                    new object[]
                    {
                        widget,
                        oldTargetObject.transform,
                        oldTargetObject.transform.GetInstanceID(),
                    });
                serialized.Update();

                for (int i = 0; i < layouts.Length; i++)
                {
                    Vector2 position = widget
                        .FindPropertyRelative(layouts[i])
                        .FindPropertyRelative("phonePortrait")
                        .FindPropertyRelative("anchoredPosition")
                        .vector2Value;
                    Assert.AreEqual(i + 1f, position.x);
                }
            }
            finally
            {
                Object.DestroyImmediate(newTargetObject);
                Object.DestroyImmediate(oldTargetObject);
                Object.DestroyImmediate(controllerObject);
            }
        }

        [Test]
        public void CopyPaste_UsesTheSameWidgetClipboardAsControllerSlots()
        {
            WithTwoSide(1, (so, widgets) =>
            {
                SerializedProperty layout = widgets
                    .GetArrayElementAtIndex(0)
                    .FindPropertyRelative("leftNormal");
                WriteLayoutSlot(
                    layout,
                    "phoneLandscape",
                    PhoneSample(landscape: true),
                    enabled: true);
                so.ApplyModifiedPropertiesWithoutUndo();

                SerializedProperty phone = layout.FindPropertyRelative("phoneLandscape");
                SerializedProperty pad = layout.FindPropertyRelative("padLandscape");
                ResponsiveUiSlotClipboard.Clear();
                ResponsiveUiSlotClipboard.Copy(
                    phone,
                    ResponsiveUiSlotClipboard.Kind.Widget);
                Assert.IsTrue(
                    ResponsiveUiSlotClipboard.Paste(
                        pad,
                        ResponsiveUiSlotClipboard.Kind.Widget));

                Assert.IsTrue(
                    ResponsiveRectStateEditorUtil.SameAuthoring(
                        PhoneSample(landscape: true),
                        ResponsiveRectStateEditorUtil.ReadState(pad)));
                Assert.IsTrue(ResponsiveRectStateEditorUtil.IsEnabled(pad));
            });
        }

        [Test]
        public void BulkCopy_CopiesOnlyTheShownSideAndPreviewLayout()
        {
            WithTwoSide(2, (so, widgets) =>
            {
                so.FindProperty("side").enumValueIndex = (int)ResponsiveUiSide.Right;
                so.FindProperty("previewAnimationStart").boolValue = false;
                var sources = new ResponsiveRectState[2];
                var keep = PhoneSample(landscape: false);
                keep.anchoredPosition = new Vector2(300f, 0f);
                for (int i = 0; i < widgets.arraySize; i++)
                {
                    SerializedProperty widget = widgets.GetArrayElementAtIndex(i);
                    widget.FindPropertyRelative("animationEnabled").boolValue = true;
                    ResponsiveRectState source = PhoneSample(landscape: true);
                    source.anchoredPosition += Vector2.one * (50f * i);
                    sources[i] = source;
                    WriteLayoutSlot(
                        widget.FindPropertyRelative("rightNormal"),
                        "padLandscape",
                        source,
                        enabled: true);
                    WriteLayoutSlot(
                        widget.FindPropertyRelative("rightNormal"),
                        "foldHalfOpenedLandscape",
                        default,
                        enabled: true);
                    WriteLayoutSlot(
                        widget.FindPropertyRelative("leftNormal"),
                        "foldHalfOpenedLandscape",
                        keep,
                        enabled: true);
                }

                so.ApplyModifiedPropertiesWithoutUndo();
                int copied = ResponsiveRectStateEditorUtil.CopySlotAcrossTwoSideWidgets(
                    widgets,
                    sourceSlotIndex: ResponsiveWidgetLayout.StatePropertyIndex("padLandscape"),
                    destinationSlotIndex: ResponsiveWidgetLayout.StatePropertyIndex("foldHalfOpenedLandscape"),
                    ResponsiveUiSide.Right,
                    previewAnimationStart: false);

                Assert.AreEqual(2, copied);
                for (int i = 0; i < widgets.arraySize; i++)
                {
                    SerializedProperty widget = widgets.GetArrayElementAtIndex(i);
                    Assert.IsTrue(
                        ResponsiveRectStateEditorUtil.SameAuthoring(
                            sources[i],
                            ResponsiveRectStateEditorUtil.ReadState(
                                widget
                                    .FindPropertyRelative("rightNormal")
                                    .FindPropertyRelative("foldHalfOpenedLandscape"))));
                    Assert.IsTrue(
                        ResponsiveRectStateEditorUtil.SameAuthoring(
                            keep,
                            ResponsiveRectStateEditorUtil.ReadState(
                                widget
                                    .FindPropertyRelative("leftNormal")
                                    .FindPropertyRelative("foldHalfOpenedLandscape"))));
                }
            });
        }

        [Test]
        public void MirrorHorizontally_FlipsAnchorPivotAndPositionX()
        {
            ResponsiveRectState right = new ResponsiveRectState
            {
                enabled = true,
                scaleFromDesignCanvas = true,
                designScaleMode = ResponsiveDesignScaleMode.Width,
                designCanvasSize = ResponsiveCatalogDevices.PhonePortrait,
                defaultScale = Vector2.one,
                anchorMin = new Vector2(1f, 0.5f),
                anchorMax = new Vector2(1f, 0.5f),
                anchoredPosition = new Vector2(-200f, 12f),
                sizeDelta = new Vector2(200f, 950f),
                pivot = new Vector2(0.5f, 0.5f),
            };

            ResponsiveRectState left =
                ResponsiveRectStateEditorUtil.MirrorHorizontally(right);

            Assert.AreEqual(new Vector2(0f, 0.5f), left.anchorMin);
            Assert.AreEqual(new Vector2(0f, 0.5f), left.anchorMax);
            Assert.AreEqual(new Vector2(200f, 12f), left.anchoredPosition);
            Assert.AreEqual(new Vector2(0.5f, 0.5f), left.pivot);
            Assert.AreEqual(right.sizeDelta, left.sizeDelta);
            Assert.AreEqual(right.enabled, left.enabled);
            Assert.AreEqual(right.scaleFromDesignCanvas, left.scaleFromDesignCanvas);
            Assert.AreEqual(right.designScaleMode, left.designScaleMode);
            Assert.AreEqual(right.designCanvasSize, left.designCanvasSize);
            Assert.AreEqual(right.defaultScale, left.defaultScale);
        }

        [Test]
        public void MirrorHorizontally_MirrorsStretchedAnchorsAndNonCenteredPivot()
        {
            var stretched = new ResponsiveRectState
            {
                enabled = true,
                scaleFromDesignCanvas = true,
                designScaleMode = ResponsiveDesignScaleMode.Width,
                designCanvasSize = ResponsiveCatalogDevices.PhonePortrait,
                defaultScale = Vector2.one,
                anchorMin = new Vector2(0.1f, 0.2f),
                anchorMax = new Vector2(0.4f, 0.8f),
                anchoredPosition = new Vector2(50f, 12f),
                sizeDelta = new Vector2(-20f, 40f),
                pivot = new Vector2(0f, 0.25f),
            };

            ResponsiveRectState mirrored =
                ResponsiveRectStateEditorUtil.MirrorHorizontally(stretched);

            Assert.AreEqual(new Vector2(0.6f, 0.2f), mirrored.anchorMin);
            Assert.AreEqual(new Vector2(0.9f, 0.8f), mirrored.anchorMax);
            Assert.AreEqual(new Vector2(-50f, 12f), mirrored.anchoredPosition);
            Assert.AreEqual(new Vector2(1f, 0.25f), mirrored.pivot);
            Assert.AreEqual(stretched.sizeDelta, mirrored.sizeDelta);
            Assert.IsTrue(
                ResponsiveRectStateEditorUtil.SameAuthoring(
                    stretched,
                    ResponsiveRectStateEditorUtil.MirrorHorizontally(mirrored)));
        }

        [Test]
        public void BulkCopySide_RoundTripRestoresTheSourceSide()
        {
            WithTwoSide(1, (so, widgets) =>
            {
                ResponsiveRectState original = RightEdge(restX: -200f, y: 8f);
                original.pivot = new Vector2(0.2f, 0.4f);
                original.anchorMin = new Vector2(0.1f, 0.2f);
                original.anchorMax = new Vector2(0.4f, 0.7f);
                WriteLayoutSlot(
                    widgets.GetArrayElementAtIndex(0).FindPropertyRelative("rightNormal"),
                    "phoneLandscape",
                    original,
                    enabled: true);

                so.ApplyModifiedPropertiesWithoutUndo();
                ResponsiveRectStateEditorUtil.CopySideAcrossTwoSideWidgets(
                    widgets,
                    ResponsiveUiSide.Right,
                    ResponsiveUiSide.Left);
                ResponsiveRectStateEditorUtil.CopySideAcrossTwoSideWidgets(
                    widgets,
                    ResponsiveUiSide.Left,
                    ResponsiveUiSide.Right);

                Assert.IsTrue(
                    ResponsiveRectStateEditorUtil.SameAuthoring(
                        original,
                        ReadSlot(widgets, 0, "rightNormal", "phoneLandscape")));
            });
        }

        [Test]
        public void BulkCopyWidgetSide_CopiesEverySlotOnTheChosenSide()
        {
            WithTwoSide(2, (so, widgets) =>
            {
                SerializedProperty source = widgets
                    .GetArrayElementAtIndex(0)
                    .FindPropertyRelative("rightNormal");
                SerializedProperty destination = widgets
                    .GetArrayElementAtIndex(1)
                    .FindPropertyRelative("rightNormal");
                string[] states = ResponsiveWidgetLayout.StatePropertyNames;
                var expected = new ResponsiveRectState[states.Length];
                for (int i = 0; i < states.Length; i++)
                {
                    expected[i] = RightEdge(restX: -10f * (i + 1), y: i);
                    expected[i].sizeDelta = new Vector2(100f + i, 200f + i);
                    expected[i].enabled = i % 2 == 0;
                    WriteLayoutSlot(source, states[i], expected[i], expected[i].enabled);
                    WriteLayoutSlot(
                        destination,
                        states[i],
                        RightEdge(restX: 1f, y: 1f),
                        enabled: true);
                }

                so.ApplyModifiedPropertiesWithoutUndo();
                Assert.AreEqual(
                    1,
                    ResponsiveRectStateEditorUtil.CopyWidgetSideAcrossTwoSideWidgets(
                        widgets,
                        sourceWidgetIndex: 0,
                        destinationWidgetIndex: 1,
                        ResponsiveUiSide.Right));

                for (int i = 0; i < states.Length; i++)
                {
                    Assert.IsTrue(
                        ResponsiveRectStateEditorUtil.SameAuthoring(
                            expected[i],
                            ReadSlot(widgets, 1, "rightNormal", states[i])),
                        states[i]);
                }
            });
        }

        [Test]
        public void BulkCopySide_MirrorsNormalAndAnimationStartAndLeavesSourceUnchanged()
        {
            WithTwoSide(2, (so, widgets) =>
            {
                var keep = PhoneSample(landscape: false);
                keep.anchoredPosition = new Vector2(300f, 0f);
                var rest = new ResponsiveRectState[2];
                var start = new ResponsiveRectState[2];
                for (int i = 0; i < widgets.arraySize; i++)
                {
                    SerializedProperty widget = widgets.GetArrayElementAtIndex(i);
                    widget.FindPropertyRelative("animationEnabled").boolValue = false;
                    rest[i] = RightEdge(restX: -200f - (10f * i), y: 8f);
                    start[i] = RightEdge(restX: 200f + (10f * i), y: 8f);
                    WriteLayoutSlot(
                        widget.FindPropertyRelative("rightNormal"),
                        "phoneLandscape",
                        rest[i],
                        enabled: true);
                    WriteLayoutSlot(
                        widget.FindPropertyRelative("rightAnimationStart"),
                        "phoneLandscape",
                        start[i],
                        enabled: true);
                    WriteLayoutSlot(
                        widget.FindPropertyRelative("leftNormal"),
                        "phoneLandscape",
                        keep,
                        enabled: true);
                    WriteLayoutSlot(
                        widget.FindPropertyRelative("leftAnimationStart"),
                        "phoneLandscape",
                        keep,
                        enabled: true);
                }

                so.ApplyModifiedPropertiesWithoutUndo();
                Assert.AreEqual(
                    0,
                    ResponsiveRectStateEditorUtil.CopySideAcrossTwoSideWidgets(
                        widgets,
                        ResponsiveUiSide.Right,
                        ResponsiveUiSide.Right));
                int copied = ResponsiveRectStateEditorUtil.CopySideAcrossTwoSideWidgets(
                    widgets,
                    ResponsiveUiSide.Right,
                    ResponsiveUiSide.Left);

                Assert.AreEqual(2, copied);
                for (int i = 0; i < widgets.arraySize; i++)
                {
                    SerializedProperty widget = widgets.GetArrayElementAtIndex(i);
                    Assert.IsTrue(
                        ResponsiveRectStateEditorUtil.SameAuthoring(
                            ResponsiveRectStateEditorUtil.MirrorHorizontally(rest[i]),
                            ResponsiveRectStateEditorUtil.ReadState(
                                widget
                                    .FindPropertyRelative("leftNormal")
                                    .FindPropertyRelative("phoneLandscape"))));
                    Assert.IsTrue(
                        ResponsiveRectStateEditorUtil.SameAuthoring(
                            ResponsiveRectStateEditorUtil.MirrorHorizontally(start[i]),
                            ResponsiveRectStateEditorUtil.ReadState(
                                widget
                                    .FindPropertyRelative("leftAnimationStart")
                                    .FindPropertyRelative("phoneLandscape"))));
                    Assert.IsTrue(
                        ResponsiveRectStateEditorUtil.SameAuthoring(
                            rest[i],
                            ResponsiveRectStateEditorUtil.ReadState(
                                widget
                                    .FindPropertyRelative("rightNormal")
                                    .FindPropertyRelative("phoneLandscape"))));
                    Assert.IsTrue(
                        ResponsiveRectStateEditorUtil.SameAuthoring(
                            start[i],
                            ResponsiveRectStateEditorUtil.ReadState(
                                widget
                                    .FindPropertyRelative("rightAnimationStart")
                                    .FindPropertyRelative("phoneLandscape"))));
                }
            });
        }

        [Test]
        public void BulkCopyWidgetSide_CopiesOneSideOntoAnotherWidgetAndLeavesTheRest()
        {
            WithTwoSide(2, (so, widgets) =>
            {
                var sourceTargetGo = new GameObject("SourceTarget", typeof(RectTransform));
                var destTargetGo = new GameObject("DestTarget", typeof(RectTransform));
                try
                {
                    RectTransform sourceTarget = sourceTargetGo.GetComponent<RectTransform>();
                    RectTransform destTarget = destTargetGo.GetComponent<RectTransform>();
                    AssignTargets(widgets.GetArrayElementAtIndex(0), sourceTarget);
                    AssignTargets(widgets.GetArrayElementAtIndex(1), destTarget);

                    var keep = PhoneSample(landscape: false);
                    keep.anchoredPosition = new Vector2(300f, 0f);
                    ResponsiveRectState sourceRest = RightEdge(restX: -200f, y: 8f);
                    ResponsiveRectState sourceStart = RightEdge(restX: 200f, y: 8f);
                    ResponsiveRectState sourcePad = RightEdge(restX: -180f, y: 16f);
                    sourcePad.sizeDelta = new Vector2(240f, 800f);
                    ResponsiveRectState destRest = RightEdge(restX: -50f, y: 1f);
                    destRest.sizeDelta = new Vector2(120f, 400f);
                    WriteLayoutSlot(
                        widgets.GetArrayElementAtIndex(0).FindPropertyRelative("rightNormal"),
                        "phoneLandscape",
                        sourceRest,
                        enabled: true);
                    WriteLayoutSlot(
                        widgets.GetArrayElementAtIndex(0).FindPropertyRelative("rightNormal"),
                        "padLandscape",
                        sourcePad,
                        enabled: true);
                    WriteLayoutSlot(
                        widgets.GetArrayElementAtIndex(0).FindPropertyRelative("rightAnimationStart"),
                        "phoneLandscape",
                        sourceStart,
                        enabled: true);
                    WriteLayoutSlot(
                        widgets.GetArrayElementAtIndex(0).FindPropertyRelative("leftNormal"),
                        "phoneLandscape",
                        keep,
                        enabled: true);
                    WriteLayoutSlot(
                        widgets.GetArrayElementAtIndex(1).FindPropertyRelative("rightNormal"),
                        "phoneLandscape",
                        destRest,
                        enabled: true);
                    WriteLayoutSlot(
                        widgets.GetArrayElementAtIndex(1).FindPropertyRelative("leftNormal"),
                        "phoneLandscape",
                        keep,
                        enabled: true);

                    so.ApplyModifiedPropertiesWithoutUndo();
                    Assert.AreEqual(
                        0,
                        ResponsiveRectStateEditorUtil.CopyWidgetSideAcrossTwoSideWidgets(
                            widgets,
                            sourceWidgetIndex: 0,
                            destinationWidgetIndex: 0,
                            ResponsiveUiSide.Right));
                    Assert.AreEqual(
                        0,
                        ResponsiveRectStateEditorUtil.CopyWidgetSideAcrossTwoSideWidgets(
                            widgets,
                            sourceWidgetIndex: 0,
                            destinationWidgetIndex: 2,
                            ResponsiveUiSide.Right));
                    int copied = ResponsiveRectStateEditorUtil.CopyWidgetSideAcrossTwoSideWidgets(
                        widgets,
                        sourceWidgetIndex: 0,
                        destinationWidgetIndex: 1,
                        ResponsiveUiSide.Right);

                    Assert.AreEqual(1, copied);
                    Assert.IsTrue(
                        ResponsiveRectStateEditorUtil.SameAuthoring(
                            sourceRest,
                            ReadSlot(widgets, 1, "rightNormal", "phoneLandscape")));
                    Assert.IsTrue(
                        ResponsiveRectStateEditorUtil.SameAuthoring(
                            sourcePad,
                            ReadSlot(widgets, 1, "rightNormal", "padLandscape")));
                    Assert.IsTrue(
                        ResponsiveRectStateEditorUtil.SameAuthoring(
                            sourceStart,
                            ReadSlot(widgets, 1, "rightAnimationStart", "phoneLandscape")));
                    Assert.IsTrue(
                        ResponsiveRectStateEditorUtil.SameAuthoring(
                            sourceRest,
                            ReadSlot(widgets, 0, "rightNormal", "phoneLandscape")));
                    Assert.IsTrue(
                        ResponsiveRectStateEditorUtil.SameAuthoring(
                            keep,
                            ReadSlot(widgets, 1, "leftNormal", "phoneLandscape")));
                    Assert.AreSame(
                        destTarget,
                        widgets
                            .GetArrayElementAtIndex(1)
                            .FindPropertyRelative("rightNormal")
                            .FindPropertyRelative("target")
                            .objectReferenceValue);
                    Assert.AreSame(
                        sourceTarget,
                        widgets
                            .GetArrayElementAtIndex(0)
                            .FindPropertyRelative("rightNormal")
                            .FindPropertyRelative("target")
                            .objectReferenceValue);
                }
                finally
                {
                    Object.DestroyImmediate(destTargetGo);
                    Object.DestroyImmediate(sourceTargetGo);
                }
            });
        }

        [Test]
        public void BulkSwapWidgetSide_ExchangesOneSideAndLeavesTheOtherSide()
        {
            WithTwoSide(2, (so, widgets) =>
            {
                var keep = PhoneSample(landscape: false);
                ResponsiveRectState first = RightEdge(restX: -200f, y: 8f);
                ResponsiveRectState firstStart = RightEdge(restX: 220f, y: 8f);
                ResponsiveRectState second = RightEdge(restX: -80f, y: 4f);
                second.sizeDelta = new Vector2(160f, 500f);
                ResponsiveRectState secondStart = RightEdge(restX: 90f, y: 4f);
                WriteLayoutSlot(
                    widgets.GetArrayElementAtIndex(0).FindPropertyRelative("rightNormal"),
                    "phoneLandscape",
                    first,
                    enabled: true);
                WriteLayoutSlot(
                    widgets.GetArrayElementAtIndex(0).FindPropertyRelative("rightAnimationStart"),
                    "phoneLandscape",
                    firstStart,
                    enabled: true);
                WriteLayoutSlot(
                    widgets.GetArrayElementAtIndex(1).FindPropertyRelative("rightNormal"),
                    "phoneLandscape",
                    second,
                    enabled: true);
                WriteLayoutSlot(
                    widgets.GetArrayElementAtIndex(1).FindPropertyRelative("rightAnimationStart"),
                    "phoneLandscape",
                    secondStart,
                    enabled: true);
                WriteLayoutSlot(
                    widgets.GetArrayElementAtIndex(0).FindPropertyRelative("leftNormal"),
                    "phoneLandscape",
                    keep,
                    enabled: true);
                WriteLayoutSlot(
                    widgets.GetArrayElementAtIndex(1).FindPropertyRelative("leftNormal"),
                    "phoneLandscape",
                    keep,
                    enabled: true);

                so.ApplyModifiedPropertiesWithoutUndo();
                Assert.AreEqual(
                    0,
                    ResponsiveRectStateEditorUtil.SwapWidgetSideAcrossTwoSideWidgets(
                        widgets,
                        sourceWidgetIndex: 1,
                        destinationWidgetIndex: 1,
                        ResponsiveUiSide.Right));
                int swapped = ResponsiveRectStateEditorUtil.SwapWidgetSideAcrossTwoSideWidgets(
                    widgets,
                    sourceWidgetIndex: 0,
                    destinationWidgetIndex: 1,
                    ResponsiveUiSide.Right);

                Assert.AreEqual(2, swapped);
                Assert.IsTrue(
                    ResponsiveRectStateEditorUtil.SameAuthoring(
                        second,
                        ReadSlot(widgets, 0, "rightNormal", "phoneLandscape")));
                Assert.IsTrue(
                    ResponsiveRectStateEditorUtil.SameAuthoring(
                        secondStart,
                        ReadSlot(widgets, 0, "rightAnimationStart", "phoneLandscape")));
                Assert.IsTrue(
                    ResponsiveRectStateEditorUtil.SameAuthoring(
                        first,
                        ReadSlot(widgets, 1, "rightNormal", "phoneLandscape")));
                Assert.IsTrue(
                    ResponsiveRectStateEditorUtil.SameAuthoring(
                        firstStart,
                        ReadSlot(widgets, 1, "rightAnimationStart", "phoneLandscape")));
                Assert.IsTrue(
                    ResponsiveRectStateEditorUtil.SameAuthoring(
                        keep,
                        ReadSlot(widgets, 0, "leftNormal", "phoneLandscape")));
                Assert.IsTrue(
                    ResponsiveRectStateEditorUtil.SameAuthoring(
                        keep,
                        ReadSlot(widgets, 1, "leftNormal", "phoneLandscape")));
            });
        }

        [Test]
        public void SeedTwoSideWidget_SeedsAllFourLayoutsLikeControllerWidgets()
        {
            WithTwoSide(1, (so, widgets) =>
            {
                SerializedProperty widget = widgets.GetArrayElementAtIndex(0);
                ResponsiveRectStateEditorUtil.SeedTwoSideWidget(widget);
                so.ApplyModifiedPropertiesWithoutUndo();

                string[] layouts = ResponsiveTwoSideWidget.LayoutPropertyNames;
                string[] states = ResponsiveWidgetLayout.StatePropertyNames;
                for (int i = 0; i < layouts.Length; i++)
                {
                    SerializedProperty layout = widget.FindPropertyRelative(layouts[i]);
                    for (int s = 0; s < states.Length; s++)
                    {
                        SerializedProperty slot = layout.FindPropertyRelative(states[s]);
                        AssertSlotEnabled(
                            slot,
                            ExpectedSeedEnabled(s),
                            layouts[i] + "." + states[s]);
                        Assert.IsTrue(
                            ResponsiveRectStateEditorUtil.IsScaleFromDesignCanvas(slot),
                            layouts[i] + "." + states[s] + " Design Scale");
                    }
                }
            });
        }

        [Test]
        public void SeedTwoSideWidget_ClearsDuplicatedRow()
        {
            WithTwoSide(1, (so, widgets) =>
            {
                var go = new GameObject("TwoSideDup", typeof(RectTransform));
                try
                {
                    SerializedProperty widget = widgets.GetArrayElementAtIndex(0);
                    widget.FindPropertyRelative("name").stringValue = "Copied";
                    widget.FindPropertyRelative("duration").floatValue = 9f;
                    widget.FindPropertyRelative("delay").floatValue = 3f;
                    widget.FindPropertyRelative("animationEnabled").boolValue = false;
                    widget.FindPropertyRelative("leftNormal")
                        .FindPropertyRelative("target").objectReferenceValue =
                        go.GetComponent<RectTransform>();
                    so.ApplyModifiedPropertiesWithoutUndo();

                    ResponsiveRectStateEditorUtil.SeedTwoSideWidget(widget);
                    so.ApplyModifiedPropertiesWithoutUndo();
                    so.Update();

                    Assert.AreEqual(string.Empty, widget.FindPropertyRelative("name").stringValue);
                    Assert.AreEqual(0.25f, widget.FindPropertyRelative("duration").floatValue);
                    Assert.AreEqual(0f, widget.FindPropertyRelative("delay").floatValue);
                    Assert.IsTrue(widget.FindPropertyRelative("animationEnabled").boolValue);

                    string[] layouts = ResponsiveTwoSideWidget.LayoutPropertyNames;
                    for (int i = 0; i < layouts.Length; i++)
                    {
                        Assert.IsNull(
                            widget.FindPropertyRelative(layouts[i])
                                .FindPropertyRelative("target").objectReferenceValue,
                            layouts[i]);
                    }
                }
                finally
                {
                    Object.DestroyImmediate(go);
                }
            });
        }

        [Test]
        public void ApplyFromHub_LayoutOnly_SkipsWhileTweenActive()
        {
            var go = new GameObject("TwoSideTweenSkip", typeof(Canvas), typeof(RectTransform));
            var targetGo = new GameObject("TwoSideTarget", typeof(RectTransform));
            try
            {
                var hub = go.AddComponent<ResponsiveUI>();
                hub.enabled = true;
                var controller = go.AddComponent<ResponsiveTwoSideUiTweenController>();
                controller.AssignHub(hub);
                var rt = targetGo.GetComponent<RectTransform>();
                targetGo.transform.SetParent(go.transform, false);
                rt.anchoredPosition = new Vector2(123f, 456f);

                var so = new SerializedObject(controller);
                SerializedProperty widgets = so.FindProperty("widgets");
                widgets.arraySize = 1;
                AssignTargets(widgets.GetArrayElementAtIndex(0), rt);
                so.ApplyModifiedPropertiesWithoutUndo();

                controller.SizeChangeRequiresReapply();

                FieldInfo tweensField = typeof(ResponsiveTwoSideUiTweenController)
                    .GetField("_tweens", BindingFlags.NonPublic | BindingFlags.Instance);
                System.Array tweens = (System.Array)tweensField.GetValue(controller);
                Assert.Greater(tweens.Length, 0);
                object tween = tweens.GetValue(0);
                tween.GetType().GetField("Active").SetValue(tween, true);
                tweens.SetValue(tween, 0);

                controller.ApplyFromHub(hub, hub.CurrentLayout, layoutOnly: true);
                Assert.AreEqual(123f, rt.anchoredPosition.x, 0.1f);
                Assert.AreEqual(456f, rt.anchoredPosition.y, 0.1f);
            }
            finally
            {
                Object.DestroyImmediate(targetGo);
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void ApplyFromHub_LayoutOnly_AppliesIdleSiblingWhileTweenActive()
        {
            var go = new GameObject("TwoSideTweenSibling", typeof(Canvas), typeof(RectTransform));
            var shotGo = new GameObject("Shot", typeof(RectTransform));
            var powerupGo = new GameObject("Powerup", typeof(RectTransform));
            try
            {
                var hub = go.AddComponent<ResponsiveUI>();
                hub.enabled = true;
                var controller = go.AddComponent<ResponsiveTwoSideUiTweenController>();
                controller.AssignHub(hub);
                var shot = shotGo.GetComponent<RectTransform>();
                var powerup = powerupGo.GetComponent<RectTransform>();
                shotGo.transform.SetParent(go.transform, false);
                powerupGo.transform.SetParent(go.transform, false);
                shot.anchoredPosition = new Vector2(123f, 456f);
                powerup.anchoredPosition = new Vector2(50f, 50f);

                var so = new SerializedObject(controller);
                SerializedProperty widgets = so.FindProperty("widgets");
                widgets.arraySize = 2;
                AssignTargets(widgets.GetArrayElementAtIndex(0), shot);
                AssignTargets(widgets.GetArrayElementAtIndex(1), powerup);
                SerializedProperty powerupPhone = widgets
                    .GetArrayElementAtIndex(1)
                    .FindPropertyRelative("rightNormal")
                    .FindPropertyRelative("phonePortrait");
                powerupPhone.FindPropertyRelative("enabled").boolValue = true;
                powerupPhone.FindPropertyRelative("anchoredPosition").vector2Value =
                    new Vector2(80f, 90f);
                so.ApplyModifiedPropertiesWithoutUndo();

                controller.SizeChangeRequiresReapply();

                FieldInfo tweensField = typeof(ResponsiveTwoSideUiTweenController)
                    .GetField("_tweens", BindingFlags.NonPublic | BindingFlags.Instance);
                System.Array tweens = (System.Array)tweensField.GetValue(controller);
                object tween = tweens.GetValue(0);
                tween.GetType().GetField("Active").SetValue(tween, true);
                tweens.SetValue(tween, 0);

                controller.ApplyFromHub(
                    hub,
                    new ResponsiveDevice.LayoutKey(
                        ResponsiveDevice.FormFactor.Phone,
                        ResponsiveDevice.Orientation.Portrait),
                    layoutOnly: true);
                Assert.AreEqual(123f, shot.anchoredPosition.x, 0.1f);
                Assert.AreEqual(80f, powerup.anchoredPosition.x, 0.1f);
                Assert.AreEqual(90f, powerup.anchoredPosition.y, 0.1f);
            }
            finally
            {
                Object.DestroyImmediate(shotGo);
                Object.DestroyImmediate(powerupGo);
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void ApplyFromHub_DisabledExactSlot_DoesNotHideTarget()
        {
            var go = new GameObject("TwoSideNoHide", typeof(Canvas), typeof(RectTransform));
            var targetGo = new GameObject("TwoSideTarget", typeof(RectTransform));
            try
            {
                var hub = go.AddComponent<ResponsiveUI>();
                hub.enabled = true;
                var controller = go.AddComponent<ResponsiveTwoSideUiTweenController>();
                controller.AssignHub(hub);
                var rt = targetGo.GetComponent<RectTransform>();
                targetGo.transform.SetParent(go.transform, false);
                targetGo.SetActive(true);

                var so = new SerializedObject(controller);
                SerializedProperty widgets = so.FindProperty("widgets");
                widgets.arraySize = 1;
                SerializedProperty widget = widgets.GetArrayElementAtIndex(0);
                AssignTargets(widget, rt);
                widget.FindPropertyRelative("leftNormal")
                    .FindPropertyRelative("phoneLandscape")
                    .FindPropertyRelative("enabled").boolValue = true;
                widget.FindPropertyRelative("leftNormal")
                    .FindPropertyRelative("flipPhoneCoverLandscape")
                    .FindPropertyRelative("enabled").boolValue = false;
                so.ApplyModifiedPropertiesWithoutUndo();

                controller.ApplyFromHub(
                    hub,
                    new ResponsiveDevice.LayoutKey(
                        ResponsiveDevice.FormFactor.FlipPhoneCover,
                        ResponsiveDevice.Orientation.Landscape),
                    layoutOnly: false);
                Assert.IsTrue(targetGo.activeSelf);
            }
            finally
            {
                Object.DestroyImmediate(targetGo);
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void StartWidgetTransition_InactiveHost_SnapsToEndWithoutTween()
        {
            var hubGo = new GameObject("TwoSideInactiveHub", typeof(Canvas), typeof(RectTransform));
            var hostGo = new GameObject("TwoSideInactiveHost", typeof(RectTransform));
            var targetGo = new GameObject("TwoSideInactiveTarget", typeof(RectTransform));
            try
            {
                hostGo.transform.SetParent(hubGo.transform, false);
                targetGo.transform.SetParent(hubGo.transform, false);
                var hub = hubGo.AddComponent<ResponsiveUI>();
                hub.enabled = true;
                var controller = hostGo.AddComponent<ResponsiveTwoSideUiTweenController>();
                controller.AssignHub(hub);
                var rt = targetGo.GetComponent<RectTransform>();
                rt.anchoredPosition = new Vector2(5f, 5f);

                var so = new SerializedObject(controller);
                SerializedProperty widgets = so.FindProperty("widgets");
                widgets.arraySize = 1;
                SerializedProperty widget = widgets.GetArrayElementAtIndex(0);
                AssignTargets(widget, rt);
                widget.FindPropertyRelative("animationEnabled").boolValue = true;
                widget.FindPropertyRelative("duration").floatValue = 1f;
                SerializedProperty rightNormal = widget.FindPropertyRelative("rightNormal");
                foreach (string slotName in ResponsiveWidgetLayout.StatePropertyNames)
                {
                    SerializedProperty slot = rightNormal.FindPropertyRelative(slotName);
                    slot.FindPropertyRelative("enabled").boolValue = true;
                    slot.FindPropertyRelative("anchoredPosition").vector2Value =
                        new Vector2(80f, 90f);
                }
                so.ApplyModifiedPropertiesWithoutUndo();

                controller.SizeChangeRequiresReapply();
                hostGo.SetActive(false);
                Assert.IsFalse(controller.isActiveAndEnabled);

                MethodInfo start = typeof(ResponsiveTwoSideUiTweenController).GetMethod(
                    "StartWidgetTransition",
                    BindingFlags.NonPublic | BindingFlags.Instance,
                    null,
                    new[] { typeof(int) },
                    null);
                Assert.IsNotNull(start);
                // Unexpected LogError (StartCoroutine on inactive GameObject) fails the test.
                start.Invoke(controller, new object[] { 0 });

                Assert.AreEqual(80f, rt.anchoredPosition.x, 0.1f);
                Assert.AreEqual(90f, rt.anchoredPosition.y, 0.1f);

                FieldInfo tweensField = typeof(ResponsiveTwoSideUiTweenController)
                    .GetField("_tweens", BindingFlags.NonPublic | BindingFlags.Instance);
                System.Array tweens = (System.Array)tweensField.GetValue(controller);
                object tween = tweens.GetValue(0);
                Assert.IsFalse((bool)tween.GetType().GetField("Active").GetValue(tween));
                FieldInfo animationField = typeof(ResponsiveTwoSideUiTweenController)
                    .GetField("_animation", BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.IsNull(animationField.GetValue(controller));
            }
            finally
            {
                Object.DestroyImmediate(targetGo);
                Object.DestroyImmediate(hostGo);
                Object.DestroyImmediate(hubGo);
            }
        }

        [Test]
        public void EvaluateCurve_NullOrEmpty_IsLinear()
        {
            Assert.AreEqual(0.25f,
                ResponsiveTwoSideUiTweenController.EvaluateCurve(null, 0.25f), 1e-5f);
            Assert.AreEqual(0.75f,
                ResponsiveTwoSideUiTweenController.EvaluateCurve(new AnimationCurve(), 0.75f), 1e-5f);
            Assert.AreEqual(1f,
                ResponsiveTwoSideUiTweenController.EvaluateCurve(null, 1f), 1e-5f);

            AnimationCurve ease = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
            Assert.AreEqual(ease.Evaluate(0.25f),
                ResponsiveTwoSideUiTweenController.EvaluateCurve(ease, 0.25f), 1e-5f);
        }

        static void WithTwoSide(
            int count,
            System.Action<SerializedObject, SerializedProperty> body)
        {
            var go = new GameObject("TwoSideSlotCopyTest");
            try
            {
                var controller = go.AddComponent<ResponsiveTwoSideUiTweenController>();
                var so = new SerializedObject(controller);
                SerializedProperty widgets = so.FindProperty("widgets");
                widgets.arraySize = count;
                so.ApplyModifiedPropertiesWithoutUndo();
                so.Update();
                body(so, so.FindProperty("widgets"));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        static void AssignTargets(SerializedProperty widget, RectTransform target)
        {
            string[] layouts = ResponsiveTwoSideWidget.LayoutPropertyNames;
            for (int i = 0; i < layouts.Length; i++)
                widget
                    .FindPropertyRelative(layouts[i])
                    .FindPropertyRelative("target")
                    .objectReferenceValue = target;
        }

        static ResponsiveRectState ReadSlot(
            SerializedProperty widgets,
            int widgetIndex,
            string layoutName,
            string slotName) =>
            ResponsiveRectStateEditorUtil.ReadState(
                widgets
                    .GetArrayElementAtIndex(widgetIndex)
                    .FindPropertyRelative(layoutName)
                    .FindPropertyRelative(slotName));

        static void WriteLayoutSlot(
            SerializedProperty layout,
            string slotName,
            ResponsiveRectState state,
            bool enabled)
        {
            state.enabled = enabled;
            ResponsiveRectStateEditorUtil.WriteState(
                layout.FindPropertyRelative(slotName),
                state);
        }

        static ResponsiveRectState RightEdge(float restX, float y) =>
            new ResponsiveRectState
            {
                enabled = true,
                scaleFromDesignCanvas = true,
                designScaleMode = ResponsiveDesignScaleMode.Width,
                designCanvasSize = ResponsiveCatalogDevices.PhonePortrait,
                defaultScale = Vector2.one,
                anchorMin = new Vector2(1f, 0.5f),
                anchorMax = new Vector2(1f, 0.5f),
                anchoredPosition = new Vector2(restX, y),
                sizeDelta = new Vector2(200f, 950f),
                pivot = new Vector2(0.5f, 0.5f),
            };

        static ResponsiveRectState PhoneSample(bool landscape) =>
            new ResponsiveRectState
            {
                enabled = true,
                scaleFromDesignCanvas = true,
                designScaleMode = ResponsiveDesignScaleMode.Width,
                designCanvasSize = ResponsiveCatalogDevices.PhonePortrait,
                defaultScale = Vector2.one,
                anchorMin = landscape ? new Vector2(0.1f, 0.2f) : new Vector2(0.05f, 0.15f),
                anchorMax = landscape ? new Vector2(0.9f, 0.8f) : new Vector2(0.95f, 0.85f),
                anchoredPosition = landscape ? new Vector2(12f, 34f) : new Vector2(56f, 78f),
                sizeDelta = landscape ? new Vector2(100f, 200f) : new Vector2(300f, 400f),
                pivot = landscape ? new Vector2(0.25f, 0.75f) : new Vector2(0.5f, 0.5f),
            };

        static RectTransform NewRect(string name) =>
            new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();

        static ResponsiveWidgetLayout Layout(float positionX) =>
            new ResponsiveWidgetLayout
            {
                phonePortrait = new ResponsiveRectState
                {
                    anchoredPosition = new Vector2(positionX, 0f),
                },
            };
    }
}
