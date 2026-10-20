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
    /// Production contracts for ResponsiveCamera viewport / transform / UI-scale apply.
    /// </summary>
    public class ResponsiveCameraTests
    {
        [TearDown]
        public void TearDown()
        {
            ResponsiveDevice.ClearSimulatorLayout();
        }

        [Test]
        public void BuiltinDesignCanvasSizes_MatchCatalogReferenceProfiles()
        {
            Assert.AreEqual(ResponsiveCatalogDevices.PhonePortrait, ResponsiveRectState.BuiltinPhoneDesignCanvasSize);
            Assert.AreEqual(ResponsiveCatalogDevices.PadPortrait, ResponsiveRectState.BuiltinPadDesignCanvasSize);
            Assert.AreEqual(
                ResponsiveCatalogDevices.FoldCoverPortrait,
                ResponsiveRectState.BuiltinFoldPhoneCoverDesignCanvasSize);
            Assert.AreEqual(
                ResponsiveCatalogDevices.FoldPhoneInnerPortrait,
                ResponsiveRectState.BuiltinFoldDesignCanvasSize);
            Assert.AreEqual(
                ResponsiveCatalogDevices.FoldInnerPortrait,
                ResponsiveRectState.BuiltinFoldCoverHalfOpenedDesignCanvasSize);
            Assert.AreEqual(
                ResponsiveCatalogDevices.IphoneDuoInnerPortrait,
                ResponsiveRectState.BuiltinIphoneDuoSeatedDesignCanvasSize);
            Assert.AreEqual(
                ResponsiveCatalogDevices.FlipMainPortrait,
                ResponsiveRectState.BuiltinFlipDesignCanvasSize);
            Assert.AreEqual(
                ResponsiveCatalogDevices.FlipCoverPortrait,
                ResponsiveRectState.BuiltinFlipCoverSize);
        }

        [Test]
        public void ResolveUiLayoutSize_ScaleWithScreenSize_PrefersWatchOverExpandedCanvas()
        {
            Vector2 cover = ResponsiveCatalogDevices.FoldCoverLandscape;
            var expandedCanvas = new Vector2(cover.x, 1602.402f);
            Assert.AreEqual(
                cover,
                ResponsiveCamera.ResolveUiLayoutSize(
                    watch: cover,
                    canvasRectSize: expandedCanvas,
                    hasScaler: true,
                    scaleWithScreenSize: true));
        }

        [Test]
        public void ResolveUiLayoutSize_MissingScaler_PrefersWatchOverCanvas()
        {
            Vector2 cover = ResponsiveCatalogDevices.FoldCoverLandscape;
            var expandedCanvas = new Vector2(cover.x, 1602.402f);
            Assert.AreEqual(
                cover,
                ResponsiveCamera.ResolveUiLayoutSize(
                    watch: cover,
                    canvasRectSize: expandedCanvas,
                    hasScaler: false,
                    scaleWithScreenSize: false));
        }

        [Test]
        public void ResolveUiLayoutSize_ConstantPixelSize_KeepsCanvas()
        {
            Vector2 cover = ResponsiveCatalogDevices.FoldCoverLandscape;
            var canvas = new Vector2(800f, 600f);
            Assert.AreEqual(
                canvas,
                ResponsiveCamera.ResolveUiLayoutSize(
                    watch: cover,
                    canvasRectSize: canvas,
                    hasScaler: true,
                    scaleWithScreenSize: false));
        }

        [Test]
        public void EditorApplyCameras_NotifiesCaptureSuppressCallbacks()
        {
            var go = new GameObject("ResponsiveCameraApplySuppress");
            var targetGo = new GameObject("CamTarget");
            Action previousBegin = ResponsiveCamera.EditorBeginApply;
            Action previousEnd = ResponsiveCamera.EditorEndApply;
            Action<Camera> previousIgnore = ResponsiveCamera.EditorIgnoreTargetCapture;
            try
            {
                Camera cam = targetGo.AddComponent<Camera>();
                var component = go.AddComponent<ResponsiveCamera>();
                var so = new SerializedObject(component);
                SerializedProperty cameras = so.FindProperty("cameras");
                cameras.arraySize = 1;
                SerializedProperty layout = cameras.GetArrayElementAtIndex(0);
                layout.FindPropertyRelative("target").objectReferenceValue = cam;
                layout.FindPropertyRelative("phonePortrait").FindPropertyRelative("enabled").boolValue = true;
                so.ApplyModifiedPropertiesWithoutUndo();

                int begins = 0;
                int ends = 0;
                Camera ignored = null;
                ResponsiveCamera.EditorBeginApply = () => begins++;
                ResponsiveCamera.EditorEndApply = () => ends++;
                ResponsiveCamera.EditorIgnoreTargetCapture = c => ignored = c;

                component.enabled = true;
                begins = 0;
                ends = 0;
                ignored = null;
                component.EditorApplyCameras(new ResponsiveDevice.LayoutKey(
                    ResponsiveDevice.FormFactor.Phone,
                    ResponsiveDevice.Orientation.Portrait));

                Assert.AreEqual(1, begins);
                Assert.AreEqual(1, ends);
                Assert.AreEqual(cam, ignored);
            }
            finally
            {
                ResponsiveCamera.EditorBeginApply = previousBegin;
                ResponsiveCamera.EditorEndApply = previousEnd;
                ResponsiveCamera.EditorIgnoreTargetCapture = previousIgnore;
                UnityEngine.Object.DestroyImmediate(go);
                UnityEngine.Object.DestroyImmediate(targetGo);
            }
        }

        [Test]
        public void ClampViewport_EmptySize_BecomesFull()
        {
            Rect clamped = ResponsiveCameraState.ClampViewport(new Rect(0.2f, 0.3f, 0f, 0.5f));
            Assert.AreEqual(ResponsiveCameraState.FullViewport, clamped);
        }

        [Test]
        public void ClampViewport_ClampsToUnitSquare()
        {
            Rect clamped = ResponsiveCameraState.ClampViewport(new Rect(-0.5f, 0.2f, 2f, 0.9f));
            Assert.AreEqual(0f, clamped.x, 1e-4f);
            Assert.AreEqual(0.2f, clamped.y, 1e-4f);
            Assert.AreEqual(1f, clamped.width, 1e-4f);
            Assert.AreEqual(0.8f, clamped.height, 1e-4f);
        }

        [Test]
        public void Apply_WritesViewportAndOptionalFov()
        {
            var go = new GameObject("Cam");
            try
            {
                Camera cam = go.AddComponent<Camera>();
                cam.orthographic = false;
                cam.rect = ResponsiveCameraState.FullViewport;
                cam.fieldOfView = 60f;

                var state = new ResponsiveCameraState
                {
                    enabled = true,
                    viewport = new Rect(0f, 0.5f, 1f, 0.5f),
                    applyFieldOfView = true,
                    fieldOfView = 45f,
                };
                state.Apply(cam);

                Assert.AreEqual(new Rect(0f, 0.5f, 1f, 0.5f), cam.rect);
                Assert.AreEqual(45f, cam.fieldOfView, 1e-3f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Apply_WritesLocalPositionAndRotation()
        {
            var go = new GameObject("Cam");
            try
            {
                Camera cam = go.AddComponent<Camera>();
                cam.transform.localPosition = Vector3.zero;
                cam.transform.localEulerAngles = Vector3.zero;

                var state = new ResponsiveCameraState
                {
                    enabled = true,
                    viewport = ResponsiveCameraState.FullViewport,
                    applyLocalPosition = true,
                    localPosition = new Vector3(1f, 2f, 3f),
                    applyLocalRotation = true,
                    localEulerAngles = new Vector3(10f, 20f, 30f),
                };
                state.Apply(cam);

                Assert.AreEqual(new Vector3(1f, 2f, 3f), cam.transform.localPosition);
                Assert.AreEqual(10f, cam.transform.localEulerAngles.x, 1e-2f);
                Assert.AreEqual(20f, cam.transform.localEulerAngles.y, 1e-2f);
                Assert.AreEqual(30f, cam.transform.localEulerAngles.z, 1e-2f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Apply_ApplyLocalTransform_EitherFlagWritesPositionAndRotation()
        {
            var go = new GameObject("Cam");
            try
            {
                Camera cam = go.AddComponent<Camera>();
                cam.transform.localPosition = Vector3.zero;
                cam.transform.localEulerAngles = Vector3.zero;

                var state = new ResponsiveCameraState
                {
                    enabled = true,
                    viewport = ResponsiveCameraState.FullViewport,
                    applyLocalPosition = true,
                    applyLocalRotation = false,
                    localPosition = new Vector3(4f, 5f, 6f),
                    localEulerAngles = new Vector3(15f, 25f, 35f),
                };
                Assert.IsTrue(state.applyLocalTransform);
                state.Apply(cam);
                Assert.AreEqual(new Vector3(4f, 5f, 6f), cam.transform.localPosition);
                Assert.AreEqual(15f, cam.transform.localEulerAngles.x, 1e-2f);
                Assert.AreEqual(25f, cam.transform.localEulerAngles.y, 1e-2f);
                Assert.AreEqual(35f, cam.transform.localEulerAngles.z, 1e-2f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Apply_ScaleFromUiSize_ScalesPosition_NotRotationOrOrtho()
        {
            var go = new GameObject("Cam");
            try
            {
                Camera cam = go.AddComponent<Camera>();
                cam.orthographic = true;
                cam.transform.localPosition = Vector3.zero;
                cam.transform.localEulerAngles = Vector3.zero;
                cam.orthographicSize = 5f;

                var state = new ResponsiveCameraState
                {
                    enabled = true,
                    viewport = ResponsiveCameraState.FullViewport,
                    applyLocalPosition = true,
                    localPosition = new Vector3(10f, 20f, 30f),
                    applyLocalRotation = true,
                    localEulerAngles = new Vector3(5f, 6f, 7f),
                    scaleFromUiSize = true,
                    uiScaleMode = ResponsiveDesignScaleMode.Width,
                    orthographic = true,
                    applyOrthographicSize = true,
                    orthographicSize = 5f,
                };

                Vector2 design = ResponsiveRectState.GetDefaultDesignCanvasSize(
                    ResponsiveDevice.FormFactor.Phone);
                // Live UI twice as wide → Width mode ratio = 2.
                state.Apply(
                    cam,
                    applyViewport: true,
                    currentUiSize: design * 2f,
                    slotForm: ResponsiveDevice.FormFactor.Phone);

                Assert.AreEqual(new Vector3(20f, 40f, 60f), cam.transform.localPosition);
                Assert.AreEqual(5f, cam.transform.localEulerAngles.x, 1e-2f);
                Assert.AreEqual(6f, cam.transform.localEulerAngles.y, 1e-2f);
                Assert.AreEqual(7f, cam.transform.localEulerAngles.z, 1e-2f);
                Assert.AreEqual(5f, cam.orthographicSize, 1e-3f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Apply_ScaleProjection_Ortho_UsesAspectFramingNotPixelRatio()
        {
            var go = new GameObject("Cam");
            try
            {
                Camera cam = go.AddComponent<Camera>();
                cam.orthographic = false;
                cam.transform.localPosition = Vector3.zero;

                var state = new ResponsiveCameraState
                {
                    enabled = true,
                    viewport = ResponsiveCameraState.FullViewport,
                    applyLocalPosition = true,
                    localPosition = new Vector3(10f, 20f, 30f),
                    scaleProjection = true,
                    uiScaleMode = ResponsiveDesignScaleMode.Width,
                    orthographic = true,
                    applyOrthographicSize = true,
                    orthographicSize = 0.87f,
                };

                Vector2 design = ResponsiveUI.OrientSize(
                    ResponsiveRectState.GetDefaultDesignCanvasSize(
                        ResponsiveDevice.FormFactor.Phone),
                    ResponsiveDevice.Orientation.Landscape);
                Vector2 liveUi = ResponsiveCatalogDevices.FoldCoverLandscape;
                state.Apply(
                    cam,
                    applyViewport: true,
                    currentUiSize: liveUi,
                    slotForm: ResponsiveDevice.FormFactor.Phone,
                    slotOrientation: ResponsiveDevice.Orientation.Landscape);

                Assert.IsTrue(cam.orthographic);
                Assert.AreEqual(new Vector3(10f, 20f, 30f), cam.transform.localPosition);
                float expected = 0.87f * (design.x / design.y) / (liveUi.x / liveUi.y);
                Assert.AreEqual(expected, cam.orthographicSize, 1e-3f);
                Assert.Greater(cam.orthographicSize, 0.8f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Apply_ScaleProjection_Ortho_SameAspect_KeepsDesignOrtho()
        {
            var go = new GameObject("Cam");
            try
            {
                Camera cam = go.AddComponent<Camera>();
                cam.orthographic = true;

                var state = new ResponsiveCameraState
                {
                    enabled = true,
                    viewport = ResponsiveCameraState.FullViewport,
                    scaleProjection = true,
                    uiScaleMode = ResponsiveDesignScaleMode.Width,
                    orthographic = true,
                    applyOrthographicSize = true,
                    orthographicSize = 5f,
                };

                Vector2 design = ResponsiveRectState.GetDefaultDesignCanvasSize(
                    ResponsiveDevice.FormFactor.Phone);
                state.Apply(
                    cam,
                    applyViewport: true,
                    currentUiSize: design * 2f,
                    slotForm: ResponsiveDevice.FormFactor.Phone);

                // Same aspect → pixel ratio must not change ortho.
                Assert.AreEqual(5f, cam.orthographicSize, 1e-3f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Apply_ScaleProjection_Perspective_SameAspect_KeepsDesignFov()
        {
            var go = new GameObject("Cam");
            try
            {
                Camera cam = go.AddComponent<Camera>();
                cam.orthographic = true;
                cam.fieldOfView = 60f;

                var state = new ResponsiveCameraState
                {
                    enabled = true,
                    viewport = ResponsiveCameraState.FullViewport,
                    scaleProjection = true,
                    uiScaleMode = ResponsiveDesignScaleMode.Width,
                    orthographic = false,
                    applyFieldOfView = true,
                    fieldOfView = 60f,
                };

                Vector2 design = ResponsiveRectState.GetDefaultDesignCanvasSize(
                    ResponsiveDevice.FormFactor.Phone);
                state.Apply(
                    cam,
                    applyViewport: true,
                    currentUiSize: design * 2f,
                    slotForm: ResponsiveDevice.FormFactor.Phone);

                Assert.IsFalse(cam.orthographic);
                // Same aspect → pixel ratio must not change FOV (was 60 × 2 = 120).
                Assert.AreEqual(60f, cam.fieldOfView, 1e-3f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Apply_ScaleProjection_Perspective_UsesAspectFramingNotPixelRatio()
        {
            var go = new GameObject("Cam");
            try
            {
                Camera cam = go.AddComponent<Camera>();
                cam.orthographic = true;
                cam.fieldOfView = 60f;

                var state = new ResponsiveCameraState
                {
                    enabled = true,
                    viewport = ResponsiveCameraState.FullViewport,
                    scaleProjection = true,
                    uiScaleMode = ResponsiveDesignScaleMode.Width,
                    orthographic = false,
                    applyFieldOfView = true,
                    fieldOfView = 60f,
                };

                Vector2 design = ResponsiveUI.OrientSize(
                    ResponsiveRectState.GetDefaultDesignCanvasSize(
                        ResponsiveDevice.FormFactor.Phone),
                    ResponsiveDevice.Orientation.Landscape);
                Vector2 liveUi = ResponsiveCatalogDevices.FoldCoverLandscape;
                state.Apply(
                    cam,
                    applyViewport: true,
                    currentUiSize: liveUi,
                    slotForm: ResponsiveDevice.FormFactor.Phone,
                    slotOrientation: ResponsiveDevice.Orientation.Landscape);

                Assert.IsFalse(cam.orthographic);
                float expected = IndependentAspectFramedFov(
                    60f, liveUi, design, ResponsiveDesignScaleMode.Width);
                Assert.AreEqual(expected, cam.fieldOfView, 1e-3f);
                AssertPixelRatioIsWrong(60f, liveUi, design, cam.fieldOfView, "FOV");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Apply_BothScaleFlags_ScalesPositionAndProjection()
        {
            var go = new GameObject("Cam");
            try
            {
                Camera cam = go.AddComponent<Camera>();
                cam.orthographic = true;
                cam.transform.localPosition = Vector3.zero;

                var state = new ResponsiveCameraState
                {
                    enabled = true,
                    viewport = ResponsiveCameraState.FullViewport,
                    applyLocalPosition = true,
                    localPosition = new Vector3(10f, 20f, 30f),
                    scaleFromUiSize = true,
                    scaleProjection = true,
                    uiScaleMode = ResponsiveDesignScaleMode.Width,
                    orthographic = true,
                    applyOrthographicSize = true,
                    orthographicSize = 5f,
                };

                Vector2 design = ResponsiveRectState.GetDefaultDesignCanvasSize(
                    ResponsiveDevice.FormFactor.Phone);
                state.Apply(
                    cam,
                    applyViewport: true,
                    currentUiSize: design * 2f,
                    slotForm: ResponsiveDevice.FormFactor.Phone);

                Assert.AreEqual(new Vector3(20f, 40f, 60f), cam.transform.localPosition);
                // Same aspect as design*2 → ortho stays design value.
                Assert.AreEqual(5f, cam.orthographicSize, 1e-3f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Apply_SetsProjectionTypeFromSlot()
        {
            var go = new GameObject("Cam");
            try
            {
                Camera cam = go.AddComponent<Camera>();
                cam.orthographic = true;

                var state = new ResponsiveCameraState
                {
                    enabled = true,
                    viewport = ResponsiveCameraState.FullViewport,
                    orthographic = false,
                    applyFieldOfView = true,
                    fieldOfView = 45f,
                };
                state.Apply(cam);

                Assert.IsFalse(cam.orthographic);
                Assert.AreEqual(45f, cam.fieldOfView, 1e-3f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void From_AutoDetectsProjection()
        {
            var go = new GameObject("Cam");
            try
            {
                Camera cam = go.AddComponent<Camera>();
                cam.orthographic = false;
                cam.fieldOfView = 50f;
                ResponsiveCameraState perspective = ResponsiveCameraState.From(cam);
                Assert.IsFalse(perspective.orthographic);
                Assert.IsTrue(perspective.applyFieldOfView);
                Assert.IsFalse(perspective.applyOrthographicSize);
                Assert.AreEqual(50f, perspective.fieldOfView, 1e-3f);

                cam.orthographic = true;
                cam.orthographicSize = 7f;
                ResponsiveCameraState ortho = ResponsiveCameraState.From(cam);
                Assert.IsTrue(ortho.orthographic);
                Assert.IsTrue(ortho.applyOrthographicSize);
                Assert.IsFalse(ortho.applyFieldOfView);
                Assert.AreEqual(7f, ortho.orthographicSize, 1e-3f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void CaptureFromTarget_ScaleFromUiSizeAlone_DoesNotUnscaleOrtho()
        {
            var go = new GameObject("Cam");
            try
            {
                Camera cam = go.AddComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = 10f;
                cam.transform.localPosition = new Vector3(20f, 40f, 60f);

                var layout = new ResponsiveCameraLayout
                {
                    target = cam,
                    phonePortrait = new ResponsiveCameraState
                    {
                        enabled = true,
                        scaleFromUiSize = true,
                        scaleProjection = false,
                        uiScaleMode = ResponsiveDesignScaleMode.Width,
                    },
                };

                Vector2 design = ResponsiveRectState.GetDefaultDesignCanvasSize(
                    ResponsiveDevice.FormFactor.Phone);
                layout.CaptureFromTarget(currentUiSize: design * 2f);

                Assert.AreEqual(new Vector3(10f, 20f, 30f), layout.phonePortrait.localPosition);
                Assert.AreEqual(10f, layout.phonePortrait.orthographicSize, 1e-3f);
                Assert.IsTrue(layout.phonePortrait.orthographic);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void CaptureFromTarget_ScaleProjectionAlone_UnscalesOrthoByAspectNotPosition()
        {
            var go = new GameObject("Cam");
            try
            {
                Camera cam = go.AddComponent<Camera>();
                cam.orthographic = true;
                Vector2 design = ResponsiveUI.OrientSize(
                    ResponsiveRectState.GetDefaultDesignCanvasSize(
                        ResponsiveDevice.FormFactor.Phone),
                    ResponsiveDevice.Orientation.Landscape);
                Vector2 liveUi = ResponsiveCatalogDevices.FoldCoverLandscape;
                float liveOrtho = 0.87f * (design.x / design.y) / (liveUi.x / liveUi.y);
                cam.orthographicSize = liveOrtho;
                cam.transform.localPosition = new Vector3(20f, 40f, 60f);

                var layout = new ResponsiveCameraLayout
                {
                    target = cam,
                    phoneLandscape = new ResponsiveCameraState
                    {
                        enabled = true,
                        scaleFromUiSize = false,
                        scaleProjection = true,
                        uiScaleMode = ResponsiveDesignScaleMode.Width,
                    },
                };

                layout.CaptureFromTarget(
                    currentUiSize: liveUi,
                    enablePhonePortraitIfEmpty: false);

                Assert.AreEqual(new Vector3(20f, 40f, 60f), layout.phoneLandscape.localPosition);
                Assert.AreEqual(0.87f, layout.phoneLandscape.orthographicSize, 1e-3f);
                Assert.IsTrue(layout.phoneLandscape.orthographic);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void CaptureFromTarget_ScaleProjectionAlone_UnscalesFovByAspectNotPixelRatio()
        {
            var go = new GameObject("Cam");
            try
            {
                Camera cam = go.AddComponent<Camera>();
                cam.orthographic = false;
                Vector2 design = ResponsiveUI.OrientSize(
                    ResponsiveRectState.GetDefaultDesignCanvasSize(
                        ResponsiveDevice.FormFactor.Phone),
                    ResponsiveDevice.Orientation.Landscape);
                Vector2 liveUi = ResponsiveCatalogDevices.FoldCoverLandscape;
                cam.fieldOfView = IndependentAspectFramedFov(
                    60f, liveUi, design, ResponsiveDesignScaleMode.Width);

                var layout = new ResponsiveCameraLayout
                {
                    target = cam,
                    phoneLandscape = new ResponsiveCameraState
                    {
                        enabled = true,
                        scaleFromUiSize = false,
                        scaleProjection = true,
                        uiScaleMode = ResponsiveDesignScaleMode.Width,
                    },
                };

                layout.CaptureFromTarget(
                    currentUiSize: liveUi,
                    enablePhonePortraitIfEmpty: false);

                Assert.AreEqual(60f, layout.phoneLandscape.fieldOfView, 1e-3f);
                AssertPixelRatioIsWrong(
                    60f,
                    liveUi,
                    design,
                    layout.phoneLandscape.fieldOfView,
                    "captured FOV");
                Assert.IsFalse(layout.phoneLandscape.orthographic);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Layout_UsesUiSizeScale_WhenOnlyOrthoScale()
        {
            var layout = new ResponsiveCameraLayout
            {
                phonePortrait = new ResponsiveCameraState
                {
                    enabled = true,
                    scaleProjection = true,
                },
            };
            Assert.IsTrue(layout.UsesUiSizeScale());
        }

        [Test]
        public void Layout_UsesFocusBounds_RequiresFocusAndMatchingRect()
        {
            var boundsGo = new GameObject("Play", typeof(RectTransform));
            var otherGo = new GameObject("Other", typeof(RectTransform));
            var targetGo = new GameObject("Table");
            try
            {
                RectTransform playRt = boundsGo.GetComponent<RectTransform>();
                RectTransform otherRt = otherGo.GetComponent<RectTransform>();
                var layout = new ResponsiveCameraLayout
                {
                    phoneLandscape = new ResponsiveCameraState
                    {
                        enabled = true,
                        focusTarget = targetGo.transform,
                        focusScreen = ResponsiveCameraFocusScreen.Auto,
                        focusBounds = playRt,
                    },
                };
                Assert.IsTrue(layout.UsesFocusBounds(playRt));
                Assert.IsFalse(layout.UsesFocusBounds(otherRt));
                layout.phoneLandscape.focusScreen = ResponsiveCameraFocusScreen.None;
                Assert.IsFalse(layout.UsesFocusBounds(playRt));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(otherGo);
                UnityEngine.Object.DestroyImmediate(boundsGo);
                UnityEngine.Object.DestroyImmediate(targetGo);
            }
        }

        [Test]
        public void Layout_UsesFocusBoundsRect_MatchesAssignedRect()
        {
            var boundsGo = new GameObject("Play", typeof(RectTransform));
            var otherGo = new GameObject("Other", typeof(RectTransform));
            try
            {
                RectTransform playRt = boundsGo.GetComponent<RectTransform>();
                RectTransform otherRt = otherGo.GetComponent<RectTransform>();
                var layout = new ResponsiveCameraLayout
                {
                    phoneLandscape = new ResponsiveCameraState
                    {
                        enabled = true,
                        focusScreen = ResponsiveCameraFocusScreen.None,
                        focusBounds = playRt,
                    },
                };
                Assert.IsTrue(layout.UsesFocusBoundsRect(playRt));
                Assert.IsFalse(layout.UsesFocusBounds(playRt));
                Assert.IsFalse(layout.UsesFocusBoundsRect(otherRt));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(otherGo);
                UnityEngine.Object.DestroyImmediate(boundsGo);
            }
        }

        [Test]
        public void CaptureFromTarget_PreservesFocusFields()
        {
            var camGo = new GameObject("Cam");
            var table = new GameObject("Table");
            try
            {
                Camera cam = camGo.AddComponent<Camera>();
                cam.orthographic = true;
                cam.transform.localPosition = new Vector3(9f, 8f, 7f);
                cam.transform.localEulerAngles = new Vector3(90f, 0f, 0f);
                var layout = new ResponsiveCameraLayout
                {
                    target = cam,
                    phonePortrait = new ResponsiveCameraState
                    {
                        enabled = true,
                        localPosition = new Vector3(1f, 2f, 3f),
                        localEulerAngles = new Vector3(10f, 20f, 30f),
                        focusTarget = table.transform,
                        focusScreen = ResponsiveCameraFocusScreen.Auto,
                        focusFit = ResponsiveCameraFocusFit.Cover,
                        focusPadding = 1.4f,
                        focusBoundsLayout = new ResponsiveRectState
                        {
                            enabled = true,
                            anchoredPosition = new Vector2(12f, -40f),
                            sizeDelta = new Vector2(0f, -80f),
                        },
                    },
                };
                layout.CaptureFromTarget();
                Assert.AreSame(table.transform, layout.phonePortrait.focusTarget);
                Assert.AreEqual(ResponsiveCameraFocusScreen.Auto, layout.phonePortrait.focusScreen);
                Assert.AreEqual(ResponsiveCameraFocusFit.Cover, layout.phonePortrait.focusFit);
                Assert.AreEqual(1.4f, layout.phonePortrait.focusPadding, 1e-4f);
                Assert.AreEqual(
                    new Vector2(12f, -40f),
                    layout.phonePortrait.focusBoundsLayout.anchoredPosition);
                Assert.IsFalse(layout.phonePortrait.applyLocalPosition);
                Assert.IsFalse(layout.phonePortrait.applyLocalRotation);
                Assert.AreEqual(new Vector3(1f, 2f, 3f), layout.phonePortrait.localPosition);
                Assert.AreEqual(new Vector3(10f, 20f, 30f), layout.phonePortrait.localEulerAngles);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(camGo);
                UnityEngine.Object.DestroyImmediate(table);
            }
        }

        [Test]
        public void CaptureFromTarget_Focus_KeepsRestPoseWhenApplyLocalOn()
        {
            var camGo = new GameObject("Cam");
            var table = new GameObject("Table");
            try
            {
                Camera cam = camGo.AddComponent<Camera>();
                cam.orthographic = true;
                cam.transform.localPosition = new Vector3(9f, 8f, 7f);
                cam.transform.localEulerAngles = new Vector3(90f, 0f, 0f);
                var layout = new ResponsiveCameraLayout
                {
                    target = cam,
                    phonePortrait = new ResponsiveCameraState
                    {
                        enabled = true,
                        applyLocalPosition = true,
                        applyLocalRotation = true,
                        localPosition = new Vector3(1f, 2f, 3f),
                        localEulerAngles = new Vector3(10f, 20f, 30f),
                        focusTarget = table.transform,
                        focusScreen = ResponsiveCameraFocusScreen.Auto,
                    },
                };
                layout.CaptureFromTarget();
                Assert.IsTrue(layout.phonePortrait.applyLocalPosition);
                Assert.IsTrue(layout.phonePortrait.applyLocalRotation);
                Assert.AreEqual(new Vector3(1f, 2f, 3f), layout.phonePortrait.localPosition);
                Assert.AreEqual(90f, layout.phonePortrait.localEulerAngles.x, 1e-2f);
                Assert.AreEqual(0f, layout.phonePortrait.localEulerAngles.y, 1e-2f);
                Assert.AreEqual(0f, layout.phonePortrait.localEulerAngles.z, 1e-2f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(camGo);
                UnityEngine.Object.DestroyImmediate(table);
            }
        }

        [Test]
        public void ComputeUiScaleRatio_WhenOnlyProjectionScale_ReturnsIdentity()
        {
            var state = new ResponsiveCameraState
            {
                scaleFromUiSize = false,
                scaleProjection = true,
                uiScaleMode = ResponsiveDesignScaleMode.Width,
            };
            Vector2 design = ResponsiveRectState.GetDefaultDesignCanvasSize(
                ResponsiveDevice.FormFactor.Phone);
            Vector2 ratio = state.ComputeUiScaleRatio(
                design * 2f,
                ResponsiveDevice.FormFactor.Phone);
            Assert.AreEqual(Vector2.one, ratio);
            Assert.AreEqual(new Vector2(2f, 2f), state.ComputeCanvasScaleRatio(
                design * 2f,
                ResponsiveDevice.FormFactor.Phone));
        }

        [Test]
        public void ComputeUiScaleRatio_Landscape_UsesOrientedLockedDefault()
        {
            var state = new ResponsiveCameraState
            {
                scaleFromUiSize = true,
                uiScaleMode = ResponsiveDesignScaleMode.Width,
            };
            Vector2 portraitDefault = ResponsiveRectState.GetDefaultDesignCanvasSize(
                ResponsiveDevice.FormFactor.Pad);
            Vector2 landscapeDefault = ResponsiveUI.OrientSize(
                portraitDefault,
                ResponsiveDevice.Orientation.Landscape);

            Vector2 ratio = state.ComputeUiScaleRatio(
                landscapeDefault * 2f,
                ResponsiveDevice.FormFactor.Pad,
                ResponsiveDevice.Orientation.Landscape);

            Assert.AreEqual(new Vector2(2f, 2f), ratio);
        }

        [Test]
        public void ComputeCanvasScaleRatio_FoldExpandInner_PadSlot_IsIdentity()
        {
            var state = new ResponsiveCameraState
            {
                scaleFromUiSize = true,
                uiScaleMode = ResponsiveDesignScaleMode.Width,
            };
            Vector2 ratio = state.ComputeCanvasScaleRatio(
                ResponsiveCatalogDevices.FoldInnerLandscape,
                ResponsiveDevice.FormFactor.Pad,
                ResponsiveDevice.Orientation.Landscape);
            Assert.AreEqual(1f, ratio.x, 1e-4f);
            Assert.AreEqual(1f, ratio.y, 1e-4f);
        }

        [Test]
        public void ComputeCanvasScaleRatio_CompactPhone_PhoneSlot_IsNotIdentity()
        {
            var state = new ResponsiveCameraState
            {
                scaleFromUiSize = true,
                uiScaleMode = ResponsiveDesignScaleMode.Width,
            };
            Vector2 ratio = state.ComputeCanvasScaleRatio(
                ResponsiveCatalogDevices.CompactPhoneLandscape,
                ResponsiveDevice.FormFactor.Phone,
                ResponsiveDevice.Orientation.Landscape);
            Assert.Greater(Mathf.Abs(ratio.x - 1f), 1e-3f);
        }

        [Test]
        public void ComputeCanvasScaleRatio_FlipOpenedMain_PhoneSlot_IsIdentity()
        {
            var state = new ResponsiveCameraState
            {
                scaleFromUiSize = true,
                uiScaleMode = ResponsiveDesignScaleMode.Width,
            };
            Vector2 ratio = state.ComputeCanvasScaleRatio(
                ResponsiveCatalogDevices.FlipMainLandscape,
                ResponsiveDevice.FormFactor.Phone,
                ResponsiveDevice.Orientation.Landscape);
            Assert.AreEqual(1f, ratio.x, 1e-4f);
            Assert.AreEqual(1f, ratio.y, 1e-4f);
        }

        [Test]
        public void ComputeCanvasScaleRatio_FlipClosedCover_FlipPhoneCoverSlot_IsIdentity()
        {
            var state = new ResponsiveCameraState
            {
                scaleFromUiSize = true,
                uiScaleMode = ResponsiveDesignScaleMode.Width,
            };
            Vector2 ratio = state.ComputeCanvasScaleRatio(
                ResponsiveCatalogDevices.FlipCoverLandscape,
                ResponsiveDevice.FormFactor.FlipPhoneCover,
                ResponsiveDevice.Orientation.Landscape);
            Assert.AreEqual(1f, ratio.x, 1e-4f);
            Assert.AreEqual(1f, ratio.y, 1e-4f);
        }

        [Test]
        public void ComputeCanvasScaleRatio_FlipCoverWatch_PhoneSlot_IsNotIdentity()
        {
            var state = new ResponsiveCameraState
            {
                scaleFromUiSize = true,
                uiScaleMode = ResponsiveDesignScaleMode.Width,
            };
            Vector2 ratio = state.ComputeCanvasScaleRatio(
                ResponsiveCatalogDevices.FlipCoverLandscape,
                ResponsiveDevice.FormFactor.Phone,
                ResponsiveDevice.Orientation.Landscape);
            Assert.Greater(Mathf.Abs(ratio.x - 1f), 1e-3f);
        }

        [Test]
        public void WriteCapturedState_FlipOpenedIdentity_KeepsLiveAuthoredValues()
        {
            var go = new GameObject("ResponsiveCameraCaptureIdentity");
            try
            {
                var component = go.AddComponent<ResponsiveCamera>();
                var so = new SerializedObject(component);
                SerializedProperty cameras = so.FindProperty("cameras");
                cameras.arraySize = 1;
                SerializedProperty state =
                    cameras.GetArrayElementAtIndex(0).FindPropertyRelative("phoneLandscape");
                state.FindPropertyRelative("scaleFromUiSize").boolValue = true;
                state.FindPropertyRelative("scaleProjection").boolValue = true;
                state.FindPropertyRelative("uiScaleMode").intValue =
                    (int)ResponsiveDesignScaleMode.Width;

                var live = new ResponsiveCameraState
                {
                    localPosition = new Vector3(10f, 20f, 30f),
                    orthographicSize = 4.25f,
                    fieldOfView = 61f,
                };
                ResponsiveCameraEditorUtil.WriteCapturedState(
                    state,
                    live,
                    true,
                    ResponsiveCatalogDevices.FlipMainLandscape,
                    ResponsiveDevice.FormFactor.Phone,
                    captureLiveValues: true);

                Assert.AreEqual(
                    live.localPosition,
                    state.FindPropertyRelative("localPosition").vector3Value);
                Assert.AreEqual(
                    live.orthographicSize,
                    state.FindPropertyRelative("orthographicSize").floatValue,
                    1e-4f);
                Assert.AreEqual(
                    live.fieldOfView,
                    state.FindPropertyRelative("fieldOfView").floatValue,
                    1e-4f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void ComputeCanvasScaleRatio_CoverSafeArea_FoldPhoneCoverSlot_IsIdentity()
        {
            var state = new ResponsiveCameraState
            {
                scaleFromUiSize = true,
                uiScaleMode = ResponsiveDesignScaleMode.Width,
            };
            Vector2 ratio = state.ComputeCanvasScaleRatio(
                ResponsiveCatalogDevices.FoldCoverSafeAreaLandscape,
                ResponsiveDevice.FormFactor.FoldPhoneCover,
                ResponsiveDevice.Orientation.Landscape);
            Assert.AreEqual(1f, ratio.x, 1e-4f);
            Assert.AreEqual(1f, ratio.y, 1e-4f);
        }

        [Test]
        public void Apply_ScaleFromUiSize_FoldExpandInner_KeepsAuthoredPosition()
        {
            var go = new GameObject("Cam");
            try
            {
                Camera cam = go.AddComponent<Camera>();
                var state = new ResponsiveCameraState
                {
                    enabled = true,
                    viewport = ResponsiveCameraState.FullViewport,
                    applyLocalPosition = true,
                    localPosition = new Vector3(10f, 20f, 30f),
                    applyLocalRotation = true,
                    localEulerAngles = new Vector3(5f, 6f, 7f),
                    scaleFromUiSize = true,
                    scaleProjection = true,
                    uiScaleMode = ResponsiveDesignScaleMode.Width,
                    orthographic = true,
                    applyOrthographicSize = true,
                    orthographicSize = 0.87f,
                };

                state.Apply(
                    cam,
                    applyViewport: true,
                    currentUiSize: ResponsiveCatalogDevices.FoldInnerLandscape,
                    slotForm: ResponsiveDevice.FormFactor.Pad,
                    slotOrientation: ResponsiveDevice.Orientation.Landscape);

                Assert.AreEqual(new Vector3(10f, 20f, 30f), cam.transform.localPosition);
                Assert.AreEqual(5f, cam.transform.localEulerAngles.x, 1e-2f);
                Assert.AreEqual(0.87f, cam.orthographicSize, 1e-3f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Apply_ScaleFromUiSize_FlipOpenedMain_KeepsAuthoredPosition()
        {
            var go = new GameObject("Cam");
            try
            {
                Camera cam = go.AddComponent<Camera>();
                var state = new ResponsiveCameraState
                {
                    enabled = true,
                    viewport = ResponsiveCameraState.FullViewport,
                    applyLocalPosition = true,
                    localPosition = new Vector3(4f, 5f, 6f),
                    scaleFromUiSize = true,
                    uiScaleMode = ResponsiveDesignScaleMode.Width,
                };

                state.Apply(
                    cam,
                    applyViewport: true,
                    currentUiSize: ResponsiveCatalogDevices.FlipMainLandscape,
                    slotForm: ResponsiveDevice.FormFactor.Phone,
                    slotOrientation: ResponsiveDevice.Orientation.Landscape);

                Assert.AreEqual(new Vector3(4f, 5f, 6f), cam.transform.localPosition);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Apply_SkipViewport_StillWritesFov()
        {
            var go = new GameObject("Cam");
            try
            {
                Camera cam = go.AddComponent<Camera>();
                cam.rect = new Rect(0.1f, 0.2f, 0.3f, 0.4f);
                cam.fieldOfView = 60f;

                var state = new ResponsiveCameraState
                {
                    enabled = true,
                    viewport = ResponsiveCameraState.FullViewport,
                    applyFieldOfView = true,
                    fieldOfView = 30f,
                };
                state.Apply(cam, applyViewport: false);

                Assert.AreEqual(new Rect(0.1f, 0.2f, 0.3f, 0.4f), cam.rect);
                Assert.AreEqual(30f, cam.fieldOfView, 1e-3f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Layout_NoEnabledSlot_RestoresFullViewport()
        {
            var go = new GameObject("Cam");
            try
            {
                Camera cam = go.AddComponent<Camera>();
                cam.rect = new Rect(0f, 0.5f, 1f, 0.5f);

                var layout = new ResponsiveCameraLayout { target = cam };
                layout.Apply(
                    new ResponsiveDevice.LayoutKey(
                        ResponsiveDevice.FormFactor.Pad,
                        ResponsiveDevice.Orientation.Landscape));

                Assert.AreEqual(ResponsiveCameraState.FullViewport, cam.rect);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Layout_NoEnabledSlot_SkipViewport_LeavesRect()
        {
            var go = new GameObject("Cam");
            try
            {
                Camera cam = go.AddComponent<Camera>();
                var stale = new Rect(0f, 0.5f, 1f, 0.5f);
                cam.rect = stale;

                var layout = new ResponsiveCameraLayout { target = cam };
                layout.Apply(
                    new ResponsiveDevice.LayoutKey(
                        ResponsiveDevice.FormFactor.Phone,
                        ResponsiveDevice.Orientation.Portrait),
                    applyViewport: false);

                Assert.AreEqual(stale, cam.rect);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Layout_EnabledPadSlot_Applies()
        {
            var go = new GameObject("Cam");
            try
            {
                Camera cam = go.AddComponent<Camera>();
                var layout = new ResponsiveCameraLayout
                {
                    target = cam,
                    padLandscape = new ResponsiveCameraState
                    {
                        enabled = true,
                        viewport = new Rect(0.25f, 0f, 0.5f, 1f),
                    },
                };

                layout.Apply(
                    new ResponsiveDevice.LayoutKey(
                        ResponsiveDevice.FormFactor.Pad,
                        ResponsiveDevice.Orientation.Landscape));

                Assert.AreEqual(new Rect(0.25f, 0f, 0.5f, 1f), cam.rect);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Layout_DisabledFoldPhoneCover_FallsBackToPhoneOrientation()
        {
            var go = new GameObject("Cam");
            try
            {
                Camera cam = go.AddComponent<Camera>();
                var expectedViewport = new Rect(0.1f, 0.2f, 0.7f, 0.6f);
                var layout = new ResponsiveCameraLayout
                {
                    target = cam,
                    phoneLandscape = new ResponsiveCameraState
                    {
                        enabled = true,
                        viewport = expectedViewport,
                        orthographic = true,
                        applyOrthographicSize = true,
                        orthographicSize = 3.25f,
                    },
                    foldPhoneCoverLandscape = ResponsiveCameraState.CreateDefault(enabled: false),
                };

                layout.Apply(new ResponsiveDevice.LayoutKey(
                    ResponsiveDevice.FormFactor.FoldPhoneCover,
                    ResponsiveDevice.Orientation.Landscape));

                Assert.AreEqual(expectedViewport, cam.rect);
                Assert.IsTrue(cam.orthographic);
                Assert.AreEqual(3.25f, cam.orthographicSize, 1e-4f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Layout_PassesUiSizeIntoScaledPosition()
        {
            var go = new GameObject("Cam");
            try
            {
                Camera cam = go.AddComponent<Camera>();
                var layout = new ResponsiveCameraLayout
                {
                    target = cam,
                    phonePortrait = new ResponsiveCameraState
                    {
                        enabled = true,
                        viewport = ResponsiveCameraState.FullViewport,
                        applyLocalPosition = true,
                        localPosition = new Vector3(4f, 0f, 0f),
                        scaleFromUiSize = true,
                        uiScaleMode = ResponsiveDesignScaleMode.Width,
                    },
                };

                Vector2 design = ResponsiveRectState.GetDefaultDesignCanvasSize(
                    ResponsiveDevice.FormFactor.Phone);
                layout.Apply(
                    new ResponsiveDevice.LayoutKey(
                        ResponsiveDevice.FormFactor.Phone,
                        ResponsiveDevice.Orientation.Portrait),
                    applyViewport: true,
                    currentUiSize: design * 0.5f);

                Assert.AreEqual(new Vector3(2f, 0f, 0f), cam.transform.localPosition);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void ScaleDesignPosition_RoundTripsThroughUnscale()
        {
            var design = new Vector3(10f, 20f, 30f);
            var ratio = new Vector2(2f, 2f);
            Vector3 live = ResponsiveCameraState.ScaleDesignPosition(design, ratio);
            Vector3 back = ResponsiveCameraState.UnscaleDesignPosition(live, ratio);
            Assert.AreEqual(design.x, back.x, 1e-4f);
            Assert.AreEqual(design.y, back.y, 1e-4f);
            Assert.AreEqual(design.z, back.z, 1e-4f);
        }

        [Test]
        public void CaptureFromTarget_PreservesScaleSettingsAndUnscalesPosition()
        {
            var go = new GameObject("Cam");
            try
            {
                Camera cam = go.AddComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = 5f;
                cam.transform.localPosition = new Vector3(20f, 40f, 60f);

                var layout = new ResponsiveCameraLayout
                {
                    target = cam,
                    phonePortrait = new ResponsiveCameraState
                    {
                        enabled = true,
                        scaleFromUiSize = true,
                        scaleProjection = true,
                        uiScaleMode = ResponsiveDesignScaleMode.Width,
                        localPosition = new Vector3(1f, 1f, 1f),
                    },
                };

                Vector2 design = ResponsiveRectState.GetDefaultDesignCanvasSize(
                    ResponsiveDevice.FormFactor.Phone);
                layout.CaptureFromTarget(currentUiSize: design * 2f);

                Assert.IsTrue(layout.phonePortrait.scaleFromUiSize);
                Assert.IsTrue(layout.phonePortrait.scaleProjection);
                Assert.AreEqual(ResponsiveDesignScaleMode.Width, layout.phonePortrait.uiScaleMode);
                Assert.AreEqual(new Vector3(10f, 20f, 30f), layout.phonePortrait.localPosition);
                // Same aspect → ortho capture stays live value.
                Assert.AreEqual(5f, layout.phonePortrait.orthographicSize, 1e-3f);
                Assert.IsTrue(layout.phonePortrait.orthographic);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void ScaleOrthographicSize_Height_KeepsDesignOrtho()
        {
            const float designOrtho = 0.87f;
            Vector2 design = ResponsiveCatalogDevices.PhoneLandscape;
            Vector2 liveUi = ResponsiveCatalogDevices.FoldCoverLandscape;
            float live = ResponsiveCameraState.ScaleOrthographicSize(
                designOrtho, liveUi, design, ResponsiveDesignScaleMode.Height);
            Assert.AreEqual(designOrtho, live, 1e-4f);
        }

        [Test]
        public void ScaleOrthographicSize_WidthAndHeight_DifferOnMismatchedAspect()
        {
            const float designOrtho = 0.87f;
            Vector2 design = ResponsiveCatalogDevices.PhoneLandscape;
            Vector2 liveUi = ResponsiveCatalogDevices.FoldCoverLandscape;
            float width = ResponsiveCameraState.ScaleOrthographicSize(
                designOrtho, liveUi, design, ResponsiveDesignScaleMode.Width);
            float height = ResponsiveCameraState.ScaleOrthographicSize(
                designOrtho, liveUi, design, ResponsiveDesignScaleMode.Height);
            Assert.AreEqual(designOrtho, height, 1e-4f);
            Assert.Greater(width, height);
            float min = ResponsiveCameraState.ScaleOrthographicSize(
                designOrtho, liveUi, design, ResponsiveDesignScaleMode.UniformMin);
            float max = ResponsiveCameraState.ScaleOrthographicSize(
                designOrtho, liveUi, design, ResponsiveDesignScaleMode.UniformMax);
            Assert.AreEqual(width, min, 1e-4f);
            Assert.AreEqual(height, max, 1e-4f);
        }

        [Test]
        public void ResolveAutoScaleMode_FoldCoverVsPhone_PicksWidth()
        {
            Assert.AreEqual(
                ResponsiveDesignScaleMode.Width,
                ResponsiveRectState.ResolveAutoScaleMode(
                    ResponsiveDesignScaleMode.Auto,
                    ResponsiveCatalogDevices.FoldCoverLandscape,
                    ResponsiveCatalogDevices.PhoneLandscape));
        }

        [Test]
        public void ScaleOrthographicSize_Auto_MatchesResolvedWidthOrHeight()
        {
            const float designOrtho = 0.87f;
            Vector2 design = ResponsiveCatalogDevices.PhoneLandscape;
            Vector2 liveUi = ResponsiveCatalogDevices.FoldCoverLandscape;
            ResponsiveDesignScaleMode resolved = ResponsiveRectState.ResolveAutoScaleMode(
                ResponsiveDesignScaleMode.Auto, liveUi, design);
            float auto = ResponsiveCameraState.ScaleOrthographicSize(
                designOrtho, liveUi, design, ResponsiveDesignScaleMode.Auto);
            float expected = ResponsiveCameraState.ScaleOrthographicSize(
                designOrtho, liveUi, design, resolved);
            Assert.AreEqual(expected, auto, 1e-4f);
            float fit = ResponsiveCameraState.ScaleOrthographicSize(
                designOrtho, liveUi, design, ResponsiveDesignScaleMode.UniformMin);
            Assert.AreEqual(fit, auto, 1e-4f);
        }

        [Test]
        public void ScaleOrthographicSize_Auto_RoundTripsThroughUnscale()
        {
            const float designOrtho = 0.87f;
            Vector2 design = ResponsiveCatalogDevices.PhoneLandscape;
            Vector2 liveUi = ResponsiveCatalogDevices.FoldCoverLandscape;
            float live = ResponsiveCameraState.ScaleOrthographicSize(
                designOrtho, liveUi, design, ResponsiveDesignScaleMode.Auto);
            float back = ResponsiveCameraState.UnscaleOrthographicSize(
                live, liveUi, design, ResponsiveDesignScaleMode.Auto);
            Assert.AreEqual(designOrtho, back, 1e-4f);
        }

        [Test]
        public void ScaleOrthographicSize_Auto_PhoneVsPadDesign_DifferOnSameLive()
        {
            const float designOrtho = 0.87f;
            Vector2 live = ResponsiveCatalogDevices.FoldCoverLandscape;
            float phone = ResponsiveCameraState.ScaleOrthographicSize(
                designOrtho,
                live,
                ResponsiveCatalogDevices.PhoneLandscape,
                ResponsiveDesignScaleMode.Auto);
            float pad = ResponsiveCameraState.ScaleOrthographicSize(
                designOrtho,
                live,
                ResponsiveCatalogDevices.PadLandscape,
                ResponsiveDesignScaleMode.Auto);
            Assert.AreEqual(
                ResponsiveCameraState.ScaleOrthographicSize(
                    designOrtho,
                    live,
                    ResponsiveCatalogDevices.PhoneLandscape,
                    ResponsiveDesignScaleMode.Width),
                phone,
                1e-4f);
            Assert.AreEqual(
                ResponsiveCameraState.ScaleOrthographicSize(
                    designOrtho,
                    live,
                    ResponsiveCatalogDevices.PadLandscape,
                    ResponsiveDesignScaleMode.Height),
                pad,
                1e-4f);
            Assert.Greater(Mathf.Abs(phone - pad), 1e-3f);
        }

        [Test]
        public void ScaleOrthographicSize_MismatchedOrientation_DoesNotCrush()
        {
            const float designOrtho = 5f;
            Vector2 portrait = ResponsiveCatalogDevices.PhonePortrait;
            Vector2 landscape = ResponsiveCatalogDevices.PhoneLandscape;
            float live = ResponsiveCameraState.ScaleOrthographicSize(
                designOrtho, landscape, portrait, ResponsiveDesignScaleMode.Width);
            Assert.AreEqual(designOrtho, live, 1e-4f);
            float back = ResponsiveCameraState.UnscaleOrthographicSize(
                live, landscape, portrait, ResponsiveDesignScaleMode.Width);
            Assert.AreEqual(designOrtho, back, 1e-4f);
        }

        [Test]
        public void ScaleOrthographicSize_RoundTripsThroughUnscale_OnFoldCoverAspect()
        {
            const float designOrtho = 0.87f;
            Vector2 design = ResponsiveCatalogDevices.PhoneLandscape;
            Vector2 liveUi = ResponsiveCatalogDevices.FoldCoverLandscape;
            float live = ResponsiveCameraState.ScaleOrthographicSize(
                designOrtho, liveUi, design, ResponsiveDesignScaleMode.Width);
            float back = ResponsiveCameraState.UnscaleOrthographicSize(
                live, liveUi, design, ResponsiveDesignScaleMode.Width);
            Assert.AreEqual(designOrtho, back, 1e-4f);
        }

        [Test]
        public void ScaleOrthographicSize_UniformModes_RoundTrip()
        {
            const float designOrtho = 0.87f;
            Vector2 design = ResponsiveCatalogDevices.PhoneLandscape;
            Vector2 liveUi = ResponsiveCatalogDevices.FoldCoverLandscape;
            foreach (ResponsiveDesignScaleMode mode in new[]
                     {
                         ResponsiveDesignScaleMode.UniformMin,
                         ResponsiveDesignScaleMode.UniformMax,
                         ResponsiveDesignScaleMode.IndependentXY,
                         ResponsiveDesignScaleMode.Auto,
                     })
            {
                float live = ResponsiveCameraState.ScaleOrthographicSize(
                    designOrtho, liveUi, design, mode);
                float back = ResponsiveCameraState.UnscaleOrthographicSize(
                    live, liveUi, design, mode);
                Assert.AreEqual(designOrtho, back, 1e-4f, mode.ToString());
            }
        }

        [Test]
        public void ScaleFieldOfView_SameAspect_KeepsDesignFov()
        {
            Vector2 design = ResponsiveCatalogDevices.PhonePortrait;
            float live = ResponsiveCameraState.ScaleFieldOfView(
                60f, design * 2f, design, ResponsiveDesignScaleMode.Width);
            Assert.AreEqual(60f, live, 1e-3f);
            // Width pixel ratio on a 2× canvas is 2 → 120°. Same aspect must not do that.
            AssertPixelRatioIsWrong(60f, design * 2f, design, live, "same-aspect FOV");
        }

        [Test]
        public void ScaleFieldOfView_MatchesIndependentTanAspectFormula()
        {
            Vector2 design = ResponsiveCatalogDevices.PhoneLandscape;
            Vector2 liveUi = ResponsiveCatalogDevices.FoldCoverLandscape;
            float live = ResponsiveCameraState.ScaleFieldOfView(
                60f, liveUi, design, ResponsiveDesignScaleMode.Width);
            Assert.AreEqual(
                IndependentAspectFramedFov(60f, liveUi, design, ResponsiveDesignScaleMode.Width),
                live,
                1e-3f);
            AssertPixelRatioIsWrong(60f, liveUi, design, live, "Fold8 Cover FOV");
        }

        [Test]
        public void ScaleOrthographicSize_MatchesIndependentAspectFormula()
        {
            const float designOrtho = 0.87f;
            Vector2 design = ResponsiveCatalogDevices.PhoneLandscape;
            Vector2 liveUi = ResponsiveCatalogDevices.FoldCoverLandscape;
            float live = ResponsiveCameraState.ScaleOrthographicSize(
                designOrtho, liveUi, design, ResponsiveDesignScaleMode.Width);
            Assert.AreEqual(
                IndependentAspectFramedOrtho(
                    designOrtho, liveUi, design, ResponsiveDesignScaleMode.Width),
                live,
                1e-4f);
            AssertPixelRatioIsWrong(designOrtho, liveUi, design, live, "Fold8 Cover ortho");
        }

        [Test]
        public void ScaleFieldOfView_RoundTripsThroughUnscale_OnFoldCoverAspect()
        {
            const float designFov = 60f;
            Vector2 design = ResponsiveCatalogDevices.PhoneLandscape;
            Vector2 liveUi = ResponsiveCatalogDevices.FoldCoverLandscape;
            float live = ResponsiveCameraState.ScaleFieldOfView(
                designFov, liveUi, design, ResponsiveDesignScaleMode.Width);
            Assert.AreEqual(
                IndependentAspectFramedFov(designFov, liveUi, design, ResponsiveDesignScaleMode.Width),
                live,
                1e-3f);
            float back = ResponsiveCameraState.UnscaleFieldOfView(
                live, liveUi, design, ResponsiveDesignScaleMode.Width);
            Assert.AreEqual(designFov, back, 1e-3f);
        }

        [Test]
        public void ChangingScaleModeWithoutRecapture_JumpsLiveOrtho()
        {
            const float liveOrtho = 1.2f;
            Vector2 design = ResponsiveCatalogDevices.PhoneLandscape;
            Vector2 liveUi = ResponsiveCatalogDevices.FoldCoverLandscape;
            float storedWidth = ResponsiveCameraState.UnscaleOrthographicSize(
                liveOrtho, liveUi, design, ResponsiveDesignScaleMode.Width);
            float jumped = ResponsiveCameraState.ScaleOrthographicSize(
                storedWidth, liveUi, design, ResponsiveDesignScaleMode.Height);
            Assert.Greater(Mathf.Abs(jumped - liveOrtho), 0.05f);
        }

        [Test]
        public void RecaptureScaleProjection_ScaleModeChange_KeepsLiveOrtho()
        {
            const float liveOrtho = 1.2f;
            Vector2 design = ResponsiveCatalogDevices.PhoneLandscape;
            Vector2 liveUi = ResponsiveCatalogDevices.FoldCoverLandscape;
            float storedHeight = ResponsiveCameraState.UnscaleOrthographicSize(
                liveOrtho, liveUi, design, ResponsiveDesignScaleMode.Height);
            float restored = ResponsiveCameraState.ScaleOrthographicSize(
                storedHeight, liveUi, design, ResponsiveDesignScaleMode.Height);
            Assert.AreEqual(liveOrtho, restored, 1e-4f);
        }

        [Test]
        public void RecaptureScaleProjection_ScaleModeChange_KeepsLiveFov()
        {
            const float liveFov = 55f;
            Vector2 design = ResponsiveCatalogDevices.PhoneLandscape;
            Vector2 liveUi = ResponsiveCatalogDevices.FoldCoverLandscape;
            float storedWidth = ResponsiveCameraState.UnscaleFieldOfView(
                liveFov, liveUi, design, ResponsiveDesignScaleMode.Width);
            float jumped = ResponsiveCameraState.ScaleFieldOfView(
                storedWidth, liveUi, design, ResponsiveDesignScaleMode.Height);
            Assert.Greater(Mathf.Abs(jumped - liveFov), 1f);

            float storedHeight = ResponsiveCameraState.UnscaleFieldOfView(
                liveFov, liveUi, design, ResponsiveDesignScaleMode.Height);
            float restored = ResponsiveCameraState.ScaleFieldOfView(
                storedHeight, liveUi, design, ResponsiveDesignScaleMode.Height);
            Assert.AreEqual(liveFov, restored, 1e-3f);
        }

        [Test]
        public void SpriteFit_ComputeScale_StretchIndependent()
        {
            Vector2 scale = ResponsiveCameraSpriteFit.ComputeScale(
                new Vector2(2f, 1f),
                new Vector2(10f, 8f),
                ResponsiveCameraSpriteFitMode.Stretch);
            Assert.AreEqual(5f, scale.x, 1e-4f);
            Assert.AreEqual(8f, scale.y, 1e-4f);
        }

        [Test]
        public void SpriteFit_ComputeScale_CoverUsesMax()
        {
            Vector2 scale = ResponsiveCameraSpriteFit.ComputeScale(
                new Vector2(2f, 1f),
                new Vector2(10f, 8f),
                ResponsiveCameraSpriteFitMode.Cover);
            Assert.AreEqual(8f, scale.x, 1e-4f);
            Assert.AreEqual(8f, scale.y, 1e-4f);
        }

        [Test]
        public void SpriteFit_ComputeScale_FitUsesMin()
        {
            Vector2 scale = ResponsiveCameraSpriteFit.ComputeScale(
                new Vector2(2f, 1f),
                new Vector2(10f, 8f),
                ResponsiveCameraSpriteFitMode.Fit);
            Assert.AreEqual(5f, scale.x, 1e-4f);
            Assert.AreEqual(5f, scale.y, 1e-4f);
        }

        [Test]
        public void SpriteFit_OrthoStretch_MatchesVisibleWorldSize()
        {
            var camGo = new GameObject("Cam");
            var spriteGo = new GameObject("Sprite");
            Texture2D tex = new Texture2D(100, 50);
            try
            {
                Camera cam = camGo.AddComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = 5f;
                cam.aspect = 2f;
                cam.rect = ResponsiveCameraState.FullViewport;
                camGo.transform.position = new Vector3(0f, 0f, -10f);
                camGo.transform.rotation = Quaternion.identity;

                Sprite sprite = Sprite.Create(
                    tex, new Rect(0f, 0f, 100f, 50f), new Vector2(0.5f, 0.5f), 100f);
                SpriteRenderer sr = spriteGo.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;

                ResponsiveCameraSpriteFit.Apply(sr, cam, ResponsiveCameraSpriteFitMode.Stretch);

                Vector2 world = ResponsiveCameraSpriteFit.GetVisibleWorldSize(cam, 10f);
                Vector2 projected = ResponsiveCameraSpriteFit.MeasureProjectedSize(sr, cam);
                Assert.AreEqual(world.x, projected.x, 0.05f);
                Assert.AreEqual(world.y, projected.y, 0.05f);
                // Stretch fills and centers on the camera view.
                Vector3 viewCenter = cam.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, 10f));
                Assert.AreEqual(viewCenter.x, sr.bounds.center.x, 0.05f);
                Assert.AreEqual(viewCenter.y, sr.bounds.center.y, 0.05f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(camGo);
                UnityEngine.Object.DestroyImmediate(spriteGo);
                UnityEngine.Object.DestroyImmediate(tex);
            }
        }

        [Test]
        public void SpriteFit_PerspectiveStretch_MatchesVisibleWorldSizeAtSpriteDepth()
        {
            var camGo = new GameObject("Cam");
            var spriteGo = new GameObject("Sprite");
            Texture2D tex = new Texture2D(100, 50);
            try
            {
                Camera cam = camGo.AddComponent<Camera>();
                cam.orthographic = false;
                cam.fieldOfView = 60f;
                cam.aspect = 2f;
                camGo.transform.position = new Vector3(0f, 0f, -10f);

                Sprite sprite = Sprite.Create(
                    tex, new Rect(0f, 0f, 100f, 50f), new Vector2(0.5f, 0.5f), 100f);
                SpriteRenderer sr = spriteGo.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;

                ResponsiveCameraSpriteFit.Apply(sr, cam, ResponsiveCameraSpriteFitMode.Stretch);

                float depth = ResponsiveCameraSpriteFit.PerspectiveDepth(cam, sr.transform);
                Vector2 world = ResponsiveCameraSpriteFit.GetVisibleWorldSize(cam, depth);
                Vector2 projected = ResponsiveCameraSpriteFit.MeasureProjectedSize(sr, cam);
                Assert.AreEqual(world.x, projected.x, 0.05f);
                Assert.AreEqual(world.y, projected.y, 0.05f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(camGo);
                UnityEngine.Object.DestroyImmediate(spriteGo);
                UnityEngine.Object.DestroyImmediate(tex);
            }
        }

        [Test]
        public void SpriteFit_Background_IsPlacedBehindCameraContent()
        {
            var camGo = new GameObject("Cam");
            var spriteGo = new GameObject("Background");
            Texture2D tex = new Texture2D(100, 50);
            try
            {
                Camera cam = camGo.AddComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = 5f;
                cam.nearClipPlane = 0.3f;
                cam.farClipPlane = 100f;
                camGo.transform.position = new Vector3(0f, 0f, -10f);

                SpriteRenderer background = spriteGo.AddComponent<SpriteRenderer>();
                background.sprite = Sprite.Create(
                    tex, new Rect(0f, 0f, 100f, 50f), new Vector2(0.5f, 0.5f), 100f);
                background.sortingOrder = 42;

                ResponsiveCameraSpriteFit.ApplyBackground(
                    background, cam, ResponsiveCameraSpriteFitMode.Cover);

                float depth = ResponsiveCameraSpriteFit.PerspectiveDepth(
                    cam, background.transform);
                Assert.Greater(depth, 98f);
                Assert.Less(depth, cam.farClipPlane);
                Assert.AreEqual(SortingLayer.layers[0].id, background.sortingLayerID);
                Assert.AreEqual(short.MinValue, background.sortingOrder);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(camGo);
                UnityEngine.Object.DestroyImmediate(spriteGo);
                UnityEngine.Object.DestroyImmediate(tex);
            }
        }

        [Test]
        public void SpriteFit_TabletopBoard_CoverFillsCameraView()
        {
            var camGo = new GameObject("Cam");
            var spriteGo = new GameObject("Sprite");
            Texture2D tex = new Texture2D(200, 100);
            try
            {
                // Same orientation as scene 2D Camera + Background Board.
                Quaternion tabletop = new Quaternion(0.5f, -0.5f, 0.5f, 0.5f);
                Camera cam = camGo.AddComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = 0.9f;
                cam.aspect = 2f;
                camGo.transform.SetPositionAndRotation(new Vector3(0.78f, 4.5f, 0f), tabletop);

                Sprite sprite = Sprite.Create(
                    tex, new Rect(0f, 0f, 200f, 100f), new Vector2(0.5f, 0.5f), 100f);
                SpriteRenderer sr = spriteGo.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                spriteGo.transform.SetPositionAndRotation(Vector3.zero, tabletop);
                spriteGo.transform.localScale = new Vector3(0.1f, 0.1f, 1f);

                ResponsiveCameraSpriteFit.Apply(sr, cam, ResponsiveCameraSpriteFitMode.Cover);

                float depth = ResponsiveCameraSpriteFit.PerspectiveDepth(cam, sr.transform);
                Vector2 view = ResponsiveCameraSpriteFit.GetVisibleWorldSize(cam, depth);
                Vector2 projected = ResponsiveCameraSpriteFit.MeasureProjectedSize(sr, cam);
                Assert.GreaterOrEqual(projected.x + 1e-3f, view.x);
                Assert.GreaterOrEqual(projected.y + 1e-3f, view.y);
                Assert.IsTrue(
                    Mathf.Abs(projected.x - view.x) < 0.05f ||
                    Mathf.Abs(projected.y - view.y) < 0.05f);

                Vector3 viewCenter = cam.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, Mathf.Abs(depth)));
                Assert.AreEqual(viewCenter.x, sr.bounds.center.x, 0.05f);
                Assert.AreEqual(viewCenter.z, sr.bounds.center.z, 0.05f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(camGo);
                UnityEngine.Object.DestroyImmediate(spriteGo);
                UnityEngine.Object.DestroyImmediate(tex);
            }
        }

        [Test]
        public void SpriteFit_Fit_ContainsBackgroundWithoutCropping()
        {
            var camGo = new GameObject("Cam");
            var spriteGo = new GameObject("Background");
            Texture2D tex = new Texture2D(200, 100);
            try
            {
                Camera cam = camGo.AddComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = 5f;
                cam.aspect = 1f;
                camGo.transform.position = new Vector3(0f, 0f, -10f);

                SpriteRenderer background = spriteGo.AddComponent<SpriteRenderer>();
                background.sprite = Sprite.Create(
                    tex, new Rect(0f, 0f, 200f, 100f), new Vector2(0.5f, 0.5f), 100f);

                ResponsiveCameraSpriteFit.Apply(
                    background, cam, ResponsiveCameraSpriteFitMode.Fit);

                float depth = ResponsiveCameraSpriteFit.PerspectiveDepth(
                    cam, background.transform);
                Vector2 view = ResponsiveCameraSpriteFit.GetVisibleWorldSize(cam, depth);
                Vector2 projected = ResponsiveCameraSpriteFit.MeasureProjectedSize(background, cam);
                Assert.LessOrEqual(projected.x, view.x + 0.01f);
                Assert.LessOrEqual(projected.y, view.y + 0.01f);
                Assert.IsTrue(
                    Mathf.Abs(projected.x - view.x) < 0.05f ||
                    Mathf.Abs(projected.y - view.y) < 0.05f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(camGo);
                UnityEngine.Object.DestroyImmediate(spriteGo);
                UnityEngine.Object.DestroyImmediate(tex);
            }
        }

        [Test]
        public void SpriteFit_RotatedSprite_StretchFillsCameraView()
        {
            var camGo = new GameObject("Cam");
            var spriteGo = new GameObject("Sprite");
            Texture2D tex = new Texture2D(100, 50);
            try
            {
                Camera cam = camGo.AddComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = 5f;
                cam.aspect = 2f;
                camGo.transform.position = new Vector3(0f, 10f, 0f);
                camGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

                Sprite sprite = Sprite.Create(
                    tex, new Rect(0f, 0f, 100f, 50f), new Vector2(0.5f, 0.5f), 100f);
                SpriteRenderer sr = spriteGo.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                spriteGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                spriteGo.transform.position = new Vector3(3f, 0f, -2f);

                ResponsiveCameraSpriteFit.Apply(sr, cam, ResponsiveCameraSpriteFitMode.Stretch);

                float depth = ResponsiveCameraSpriteFit.PerspectiveDepth(cam, sr.transform);
                Vector2 view = ResponsiveCameraSpriteFit.GetVisibleWorldSize(cam, depth);
                Vector2 projected = ResponsiveCameraSpriteFit.MeasureProjectedSize(sr, cam);
                Assert.AreEqual(view.x, projected.x, 0.05f);
                Assert.AreEqual(view.y, projected.y, 0.05f);

                Vector3 viewCenter = cam.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, Mathf.Abs(depth)));
                Assert.AreEqual(viewCenter.x, sr.bounds.center.x, 0.05f);
                Assert.AreEqual(viewCenter.z, sr.bounds.center.z, 0.05f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(camGo);
                UnityEngine.Object.DestroyImmediate(spriteGo);
                UnityEngine.Object.DestroyImmediate(tex);
            }
        }

        [Test]
        public void SpriteFit_SceneTabletopStretch_FillsAndCenters()
        {
            var camGo = new GameObject("Cam");
            var spriteGo = new GameObject("Sprite");
            Texture2D tex = new Texture2D(200, 100);
            try
            {
                // Matches 8BallPool 2D Camera + Background Board orientations.
                Quaternion tabletop = Quaternion.Euler(90f, 270f, 0f);
                Camera cam = camGo.AddComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = 0.87f;
                Vector2 cover = ResponsiveCatalogDevices.FoldCoverLandscape;
                cam.aspect = cover.x / cover.y;
                cam.backgroundColor = Color.white;
                cam.clearFlags = CameraClearFlags.SolidColor;
                camGo.transform.SetPositionAndRotation(new Vector3(-0.1f, 4.5f, 0f), tabletop);

                Sprite sprite = Sprite.Create(
                    tex, new Rect(0f, 0f, 200f, 100f), new Vector2(0.5f, 0.5f), 100f);
                SpriteRenderer sr = spriteGo.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                spriteGo.transform.SetPositionAndRotation(new Vector3(-0.158f, 0f, 0f), tabletop);
                spriteGo.transform.localScale = new Vector3(0.05f, 0.05f, 1f);

                ResponsiveCameraSpriteFit.Apply(sr, cam, ResponsiveCameraSpriteFitMode.Stretch);
                Vector3 firstScale = sr.transform.localScale;
                Vector3 firstPosition = sr.transform.position;
                ResponsiveCameraSpriteFit.Apply(sr, cam, ResponsiveCameraSpriteFitMode.Stretch);

                float depth = ResponsiveCameraSpriteFit.PerspectiveDepth(cam, sr.transform);
                Vector2 view = ResponsiveCameraSpriteFit.GetVisibleWorldSize(cam, depth);
                Vector2 projected = ResponsiveCameraSpriteFit.MeasureProjectedSize(sr, cam);
                Assert.AreEqual(view.x, projected.x, 0.05f);
                Assert.AreEqual(view.y, projected.y, 0.05f);
                Assert.AreEqual(firstScale, sr.transform.localScale);
                Assert.AreEqual(firstPosition, sr.transform.position);

                Vector3 viewCenter = cam.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, Mathf.Abs(depth)));
                Assert.AreEqual(viewCenter.x, sr.bounds.center.x, 0.05f);
                Assert.AreEqual(viewCenter.z, sr.bounds.center.z, 0.05f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(camGo);
                UnityEngine.Object.DestroyImmediate(spriteGo);
                UnityEngine.Object.DestroyImmediate(tex);
            }
        }

        [Test]
        public void Layout_Focus_SpriteStaysCenteredOnFullCamera()
        {
            var camGo = new GameObject("Cam");
            var tableGo = new GameObject("Table");
            var spriteGo = new GameObject("Sprite");
            Texture2D tex = new Texture2D(200, 100);
            try
            {
                Camera cam = camGo.AddComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = 5f;
                cam.aspect = 1f;
                camGo.transform.position = new Vector3(0f, 10f, 0f);
                camGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                tableGo.transform.position = new Vector3(3f, 0f, 2f);

                Sprite sprite = Sprite.Create(
                    tex, new Rect(0f, 0f, 200f, 100f), new Vector2(0.5f, 0.5f), 100f);
                SpriteRenderer sr = spriteGo.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                spriteGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

                var layout = new ResponsiveCameraLayout
                {
                    target = cam,
                    background = sr,
                    spriteFitMode = ResponsiveCameraSpriteFitMode.Cover,
                    foldHalfOpenedLandscape = new ResponsiveCameraState
                    {
                        enabled = true,
                        viewport = ResponsiveCameraState.FullViewport,
                        applyLocalPosition = true,
                        localPosition = new Vector3(0f, 10f, 0f),
                        applyLocalRotation = true,
                        localEulerAngles = new Vector3(90f, 0f, 0f),
                        orthographic = true,
                        applyOrthographicSize = true,
                        orthographicSize = 5f,
                        focusTarget = tableGo.transform,
                        focusScreen = ResponsiveCameraFocusScreen.Auto,
                        focusFit = ResponsiveCameraFocusFit.Center,
                    },
                };
                layout.Apply(new ResponsiveDevice.LayoutKey(
                    ResponsiveDevice.FormFactor.FoldHalfOpened,
                    ResponsiveDevice.Orientation.Landscape));

                float depth = ResponsiveCameraSpriteFit.PerspectiveDepth(cam, sr.transform);
                Vector3 viewCenter = cam.ViewportToWorldPoint(
                    new Vector3(0.5f, 0.5f, Mathf.Abs(depth)));
                Assert.AreEqual(viewCenter.x, sr.bounds.center.x, 0.05f);
                Assert.AreEqual(viewCenter.z, sr.bounds.center.z, 0.05f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(camGo);
                UnityEngine.Object.DestroyImmediate(tableGo);
                UnityEngine.Object.DestroyImmediate(spriteGo);
                UnityEngine.Object.DestroyImmediate(tex);
            }
        }

        [Test]
        public void Focus_Cover_FillsThePane()
        {
            var camGo = new GameObject("Cam");
            var targetGo = new GameObject("Table");
            try
            {
                Camera cam = camGo.AddComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = 5f;
                cam.aspect = 2f;
                camGo.transform.position = new Vector3(0f, 10f, 0f);
                camGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                var box = targetGo.AddComponent<BoxCollider>();
                box.size = new Vector3(4f, 1f, 4f);

                ResponsiveCameraFocus.Apply(
                    cam,
                    targetGo.transform,
                    ResponsiveCameraFocusScreen.Full,
                    ResponsiveCameraFocusFit.Cover,
                    1f);

                Vector2 view = ResponsiveCameraSpriteFit.GetVisibleWorldSize(
                    cam, 10f, ResponsiveCameraState.FullViewport);
                Vector2 size = ResponsiveCameraFocus.MeasureTargetSize(targetGo.transform, cam);
                Assert.GreaterOrEqual(size.x / view.x, 0.98f);
                Assert.GreaterOrEqual(size.y / view.y, 0.98f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(camGo);
                UnityEngine.Object.DestroyImmediate(targetGo);
            }
        }

        [Test]
        public void Focus_Fit_ShowsTheWholeTarget()
        {
            var camGo = new GameObject("Cam");
            var targetGo = new GameObject("Table");
            try
            {
                Camera cam = camGo.AddComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = 5f;
                cam.aspect = 2f;
                camGo.transform.position = new Vector3(0f, 10f, 0f);
                camGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                var box = targetGo.AddComponent<BoxCollider>();
                box.size = new Vector3(4f, 1f, 4f);

                ResponsiveCameraFocus.Apply(
                    cam,
                    targetGo.transform,
                    ResponsiveCameraFocusScreen.Full,
                    ResponsiveCameraFocusFit.Fit,
                    1f);

                Vector2 view = ResponsiveCameraSpriteFit.GetVisibleWorldSize(
                    cam, 10f, ResponsiveCameraState.FullViewport);
                Vector2 size = ResponsiveCameraFocus.MeasureTargetSize(targetGo.transform, cam);
                Assert.LessOrEqual(size.x / view.x, 1.02f);
                Assert.LessOrEqual(size.y / view.y, 1.02f);
                Assert.GreaterOrEqual(Mathf.Max(size.x / view.x, size.y / view.y), 0.98f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(camGo);
                UnityEngine.Object.DestroyImmediate(targetGo);
            }
        }

        [Test]
        public void Layout_FocusFit_AdjustsAppliedOrthographicSeed()
        {
            var camGo = new GameObject("Cam");
            var targetGo = new GameObject("Table");
            try
            {
                Camera cam = camGo.AddComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = 5f;
                cam.aspect = 2f;
                camGo.transform.position = new Vector3(0f, 10f, 0f);
                camGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                var box = targetGo.AddComponent<BoxCollider>();
                box.size = new Vector3(4f, 1f, 4f);

                var layout = new ResponsiveCameraLayout
                {
                    target = cam,
                    phoneLandscape = new ResponsiveCameraState
                    {
                        enabled = true,
                        viewport = ResponsiveCameraState.FullViewport,
                        applyLocalPosition = true,
                        localPosition = new Vector3(0f, 10f, 0f),
                        applyLocalRotation = true,
                        localEulerAngles = new Vector3(90f, 0f, 0f),
                        orthographic = true,
                        applyOrthographicSize = true,
                        orthographicSize = 0.82f,
                        focusTarget = targetGo.transform,
                        focusScreen = ResponsiveCameraFocusScreen.Full,
                        focusFit = ResponsiveCameraFocusFit.Fit,
                    },
                };

                layout.Apply(new ResponsiveDevice.LayoutKey(
                    ResponsiveDevice.FormFactor.Phone,
                    ResponsiveDevice.Orientation.Landscape));

                Vector2 view = ResponsiveCameraSpriteFit.GetVisibleWorldSize(cam, 10f);
                Vector2 target = ResponsiveCameraFocus.MeasureTargetSize(targetGo.transform, cam);
                Assert.Greater(cam.orthographicSize, 0.82f);
                Assert.LessOrEqual(target.x / view.x, 1.02f);
                Assert.LessOrEqual(target.y / view.y, 1.02f);
                Assert.GreaterOrEqual(Mathf.Max(target.x / view.x, target.y / view.y), 0.98f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(camGo);
                UnityEngine.Object.DestroyImmediate(targetGo);
            }
        }

        [Test]
        public void Focus_Cover_NoRenderer_LeavesOrthographicSize()
        {
            var camGo = new GameObject("Cam");
            var targetGo = new GameObject("PhysicsRoot");
            try
            {
                Camera cam = camGo.AddComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = 0.82f;
                cam.aspect = 2f;
                camGo.transform.position = new Vector3(0f, 10f, 0f);
                camGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

                ResponsiveCameraFocus.Apply(
                    cam,
                    targetGo.transform,
                    ResponsiveCameraFocusScreen.Full,
                    ResponsiveCameraFocusFit.Cover,
                    1f);

                Assert.AreEqual(0.82f, cam.orthographicSize, 1e-4f);
                Assert.AreEqual(Vector2.zero, ResponsiveCameraFocus.MeasureTargetSize(
                    targetGo.transform, cam));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(camGo);
                UnityEngine.Object.DestroyImmediate(targetGo);
            }
        }

        [Test]
        public void Focus_Center_UsesRendererBoundsCenter()
        {
            var camGo = new GameObject("Cam");
            var targetGo = new GameObject("Table");
            try
            {
                Camera cam = camGo.AddComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = 5f;
                cam.aspect = 1f;
                camGo.transform.position = new Vector3(0f, 10f, 0f);
                camGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

                var box = targetGo.AddComponent<BoxCollider>();
                box.center = new Vector3(2f, 0f, 1f);
                box.size = new Vector3(2f, 1f, 2f);

                ResponsiveCameraFocus.Apply(
                    cam,
                    targetGo.transform,
                    ResponsiveCameraFocusScreen.Full,
                    ResponsiveCameraFocusFit.Center,
                    1f);

                float depth = ResponsiveCameraSpriteFit.PerspectiveDepth(cam, targetGo.transform);
                Vector3 viewCenter = cam.ViewportToWorldPoint(
                    new Vector3(0.5f, 0.5f, Mathf.Abs(depth)));
                Assert.AreEqual(box.bounds.center.x, viewCenter.x, 0.05f);
                Assert.AreEqual(box.bounds.center.z, viewCenter.z, 0.05f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(camGo);
                UnityEngine.Object.DestroyImmediate(targetGo);
            }
        }

        [Test]
        public void Focus_PerSlot_PhoneUnfocusedFoldLooksAtTarget()
        {
            var camGo = new GameObject("Cam");
            var targetGo = new GameObject("Table");
            try
            {
                Camera cam = camGo.AddComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = 5f;
                cam.aspect = 1f;
                camGo.transform.position = new Vector3(0f, 10f, 0f);
                camGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                targetGo.transform.position = new Vector3(3f, 0f, 2f);

                var phone = new ResponsiveCameraState
                {
                    enabled = true,
                    viewport = ResponsiveCameraState.FullViewport,
                    applyLocalPosition = true,
                    localPosition = new Vector3(0f, 10f, 0f),
                    applyLocalRotation = true,
                    localEulerAngles = new Vector3(90f, 0f, 0f),
                    orthographic = true,
                    applyOrthographicSize = true,
                    orthographicSize = 5f,
                    focusTarget = null,
                    focusScreen = ResponsiveCameraFocusScreen.None,
                };
                var fold = phone;
                fold.focusTarget = targetGo.transform;
                fold.focusScreen = ResponsiveCameraFocusScreen.Auto;
                fold.focusFit = ResponsiveCameraFocusFit.Center;

                var layout = new ResponsiveCameraLayout
                {
                    target = cam,
                    phoneLandscape = phone,
                    foldHalfOpenedLandscape = fold,
                };

                layout.Apply(new ResponsiveDevice.LayoutKey(
                    ResponsiveDevice.FormFactor.Phone, ResponsiveDevice.Orientation.Landscape));
                Assert.AreEqual(new Vector3(0f, 10f, 0f), cam.transform.position);

                layout.Apply(new ResponsiveDevice.LayoutKey(
                    ResponsiveDevice.FormFactor.FoldHalfOpened,
                    ResponsiveDevice.Orientation.Landscape));
                Assert.AreEqual(0f, cam.rect.y, 1e-3f);
                Assert.AreEqual(1f, cam.rect.height, 1e-3f);
                float depth = ResponsiveCameraSpriteFit.PerspectiveDepth(cam, targetGo.transform);
                Vector3 viewCenter = cam.ViewportToWorldPoint(
                    new Vector3(0.5f, 0.5f, Mathf.Abs(depth)));
                Assert.AreEqual(targetGo.transform.position.x, viewCenter.x, 0.05f);
                Assert.AreEqual(targetGo.transform.position.z, viewCenter.z, 0.05f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(camGo);
                UnityEngine.Object.DestroyImmediate(targetGo);
            }
        }

        [Test]
        public void Focus_None_LeavesCameraPose()
        {
            var camGo = new GameObject("Cam");
            var targetGo = new GameObject("Focus");
            try
            {
                Camera cam = camGo.AddComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = 5f;
                camGo.transform.position = new Vector3(0f, 10f, 0f);
                camGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                targetGo.transform.position = new Vector3(3f, 0f, 2f);

                var layout = new ResponsiveCameraLayout
                {
                    target = cam,
                    phonePortrait = new ResponsiveCameraState
                    {
                        enabled = true,
                        viewport = ResponsiveCameraState.FullViewport,
                        applyLocalPosition = true,
                        localPosition = new Vector3(0f, 10f, 0f),
                        applyLocalRotation = true,
                        localEulerAngles = new Vector3(90f, 0f, 0f),
                        orthographic = true,
                        applyOrthographicSize = true,
                        orthographicSize = 5f,
                        focusTarget = targetGo.transform,
                        focusScreen = ResponsiveCameraFocusScreen.None,
                    },
                };
                layout.Apply(new ResponsiveDevice.LayoutKey(
                    ResponsiveDevice.FormFactor.Phone, ResponsiveDevice.Orientation.Portrait));
                Assert.AreEqual(new Vector3(0f, 10f, 0f), cam.transform.position);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(camGo);
                UnityEngine.Object.DestroyImmediate(targetGo);
            }
        }

        [Test]
        public void Focus_AutoCenter_PutsTargetAtViewCenter()
        {
            var camGo = new GameObject("Cam");
            var targetGo = new GameObject("Table");
            try
            {
                Camera cam = camGo.AddComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = 5f;
                cam.aspect = 1f;
                cam.rect = new Rect(0f, 0.5f, 1f, 0.5f);
                camGo.transform.position = new Vector3(0f, 10f, 0f);
                camGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                targetGo.transform.position = new Vector3(3f, 0f, 2f);

                var layout = new ResponsiveCameraLayout
                {
                    target = cam,
                    phonePortrait = new ResponsiveCameraState
                    {
                        enabled = true,
                        viewport = new Rect(0f, 0.5f, 1f, 0.5f),
                        applyLocalPosition = true,
                        localPosition = new Vector3(0f, 10f, 0f),
                        applyLocalRotation = true,
                        localEulerAngles = new Vector3(90f, 0f, 0f),
                        orthographic = true,
                        applyOrthographicSize = true,
                        orthographicSize = 5f,
                        focusTarget = targetGo.transform,
                        focusScreen = ResponsiveCameraFocusScreen.Auto,
                        focusFit = ResponsiveCameraFocusFit.Center,
                    },
                };
                layout.Apply(new ResponsiveDevice.LayoutKey(
                    ResponsiveDevice.FormFactor.Phone, ResponsiveDevice.Orientation.Portrait));

                Assert.AreEqual(0.5f, cam.rect.y, 1e-4f);
                float depth = ResponsiveCameraSpriteFit.PerspectiveDepth(cam, targetGo.transform);
                Vector3 viewCenter = cam.ViewportToWorldPoint(
                    new Vector3(0.5f, 0.5f, Mathf.Abs(depth)));
                Assert.AreEqual(targetGo.transform.position.x, viewCenter.x, 0.05f);
                Assert.AreEqual(targetGo.transform.position.z, viewCenter.z, 0.05f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(camGo);
                UnityEngine.Object.DestroyImmediate(targetGo);
            }
        }

        [Test]
        public void Focus_Auto_FoldLandscape_LooksAtFullWhenNoBounds()
        {
            var camGo = new GameObject("Cam");
            var targetGo = new GameObject("Table");
            try
            {
                Camera cam = camGo.AddComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = 5f;
                cam.aspect = 1f;
                cam.rect = ResponsiveCameraState.FullViewport;
                camGo.transform.position = new Vector3(0f, 10f, 0f);
                camGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                targetGo.transform.position = new Vector3(3f, 0f, 2f);

                var layout = new ResponsiveCameraLayout
                {
                    target = cam,
                    foldHalfOpenedLandscape = new ResponsiveCameraState
                    {
                        enabled = true,
                        viewport = ResponsiveCameraState.FullViewport,
                        applyLocalPosition = true,
                        localPosition = new Vector3(0f, 10f, 0f),
                        applyLocalRotation = true,
                        localEulerAngles = new Vector3(90f, 0f, 0f),
                        orthographic = true,
                        applyOrthographicSize = true,
                        orthographicSize = 5f,
                        focusTarget = targetGo.transform,
                        focusScreen = ResponsiveCameraFocusScreen.Auto,
                        focusFit = ResponsiveCameraFocusFit.Center,
                    },
                };
                layout.Apply(
                    new ResponsiveDevice.LayoutKey(
                        ResponsiveDevice.FormFactor.FoldHalfOpened,
                        ResponsiveDevice.Orientation.Landscape),
                    applyViewport: true);

                Assert.AreEqual(0f, cam.rect.y, 1e-3f);
                Assert.AreEqual(1f, cam.rect.height, 1e-3f);
                float depth = ResponsiveCameraSpriteFit.PerspectiveDepth(cam, targetGo.transform);
                Vector3 viewCenter = cam.ViewportToWorldPoint(
                    new Vector3(0.5f, 0.5f, Mathf.Abs(depth)));
                Assert.AreEqual(targetGo.transform.position.x, viewCenter.x, 0.05f);
                Assert.AreEqual(targetGo.transform.position.z, viewCenter.z, 0.05f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(camGo);
                UnityEngine.Object.DestroyImmediate(targetGo);
            }
        }

        [Test]
        public void Focus_Full_CentersWithoutWritingRect()
        {
            var camGo = new GameObject("Cam");
            var targetGo = new GameObject("Table");
            try
            {
                Camera cam = camGo.AddComponent<Camera>();
                cam.orthographic = true;
                cam.aspect = 1f;
                camGo.transform.position = new Vector3(0f, 10f, 0f);
                camGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                targetGo.transform.position = Vector3.zero;

                var layout = new ResponsiveCameraLayout
                {
                    target = cam,
                    foldHalfOpenedLandscape = new ResponsiveCameraState
                    {
                        enabled = true,
                        viewport = ResponsiveCameraState.FullViewport,
                        applyLocalPosition = true,
                        localPosition = new Vector3(0f, 10f, 0f),
                        applyLocalRotation = true,
                        localEulerAngles = new Vector3(90f, 0f, 0f),
                        orthographic = true,
                        applyOrthographicSize = true,
                        orthographicSize = 5f,
                        focusTarget = targetGo.transform,
                        focusScreen = ResponsiveCameraFocusScreen.Full,
                        focusFit = ResponsiveCameraFocusFit.Center,
                    },
                };
                layout.Apply(
                    new ResponsiveDevice.LayoutKey(
                        ResponsiveDevice.FormFactor.FoldHalfOpened,
                        ResponsiveDevice.Orientation.Landscape),
                    applyViewport: true);

                Assert.AreEqual(0f, cam.rect.y, 1e-3f);
                Assert.AreEqual(1f, cam.rect.height, 1e-3f);
                float depth = ResponsiveCameraSpriteFit.PerspectiveDepth(cam, targetGo.transform);
                Vector3 viewCenter = cam.ViewportToWorldPoint(
                    new Vector3(0.5f, 0.5f, Mathf.Abs(depth)));
                Assert.AreEqual(0f, viewCenter.x, 0.05f);
                Assert.AreEqual(0f, viewCenter.z, 0.05f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(camGo);
                UnityEngine.Object.DestroyImmediate(targetGo);
            }
        }

        [Test]
        public void Focus_Full_DeferViewport_LeavesRect()
        {
            var camGo = new GameObject("Cam");
            var targetGo = new GameObject("Table");
            try
            {
                Camera cam = camGo.AddComponent<Camera>();
                cam.orthographic = true;
                cam.rect = new Rect(0f, 0.4f, 1f, 0.6f);
                camGo.transform.position = new Vector3(0f, 10f, 0f);
                camGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                targetGo.transform.position = new Vector3(1f, 0f, 0f);

                var layout = new ResponsiveCameraLayout
                {
                    target = cam,
                    foldHalfOpenedLandscape = new ResponsiveCameraState
                    {
                        enabled = true,
                        viewport = ResponsiveCameraState.FullViewport,
                        applyLocalPosition = true,
                        localPosition = new Vector3(0f, 10f, 0f),
                        applyLocalRotation = true,
                        localEulerAngles = new Vector3(90f, 0f, 0f),
                        orthographic = true,
                        applyOrthographicSize = true,
                        orthographicSize = 5f,
                        focusTarget = targetGo.transform,
                        focusScreen = ResponsiveCameraFocusScreen.Full,
                        focusFit = ResponsiveCameraFocusFit.Center,
                    },
                };
                layout.Apply(
                    new ResponsiveDevice.LayoutKey(
                        ResponsiveDevice.FormFactor.FoldHalfOpened,
                        ResponsiveDevice.Orientation.Landscape),
                    applyViewport: false);

                Assert.AreEqual(0.4f, cam.rect.y, 1e-4f);
                float depth = ResponsiveCameraSpriteFit.PerspectiveDepth(cam, targetGo.transform);
                Rect pane = ResponsiveCameraFocus.GetCameraFocusViewport(
                    cam,
                    ResponsiveCameraFocusScreen.Full);
                Vector3 viewCenter = cam.ViewportToWorldPoint(
                    new Vector3(pane.center.x, pane.center.y, Mathf.Abs(depth)));
                Assert.AreEqual(1f, viewCenter.x, 0.05f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(camGo);
                UnityEngine.Object.DestroyImmediate(targetGo);
            }
        }

        [Test]
        public void GetCameraFocusViewport_OverlayHud_BoundsBelowTopBar()
        {
            var camGo = new GameObject("Cam", typeof(Camera));
            var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            var playGo = new GameObject("Play", typeof(RectTransform));
            try
            {
                Camera cam = camGo.GetComponent<Camera>();
                cam.orthographic = true;
                cam.rect = ResponsiveCameraState.FullViewport;

                Canvas canvas = canvasGo.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                RectTransform canvasRt = canvasGo.GetComponent<RectTransform>();
                canvasRt.anchorMin = new Vector2(0.5f, 0.5f);
                canvasRt.anchorMax = new Vector2(0.5f, 0.5f);
                canvasRt.pivot = new Vector2(0.5f, 0.5f);
                canvasRt.sizeDelta = new Vector2(800f, 400f);
                canvasRt.anchoredPosition = Vector2.zero;

                RectTransform playRt = playGo.GetComponent<RectTransform>();
                playRt.SetParent(canvasRt, false);
                playRt.anchorMin = Vector2.zero;
                playRt.anchorMax = Vector2.one;
                playRt.pivot = new Vector2(0.5f, 0.5f);
                playRt.anchoredPosition = new Vector2(0f, -40f);
                playRt.sizeDelta = new Vector2(0f, -80f);

                Canvas.ForceUpdateCanvases();

                Assert.IsTrue(ResponsiveCameraFocus.GetUiBoundsViewport(cam, playRt, out Rect pane));
                Assert.AreEqual(0f, pane.x, 0.02f);
                Assert.AreEqual(1f, pane.width, 0.02f);
                Assert.Less(pane.yMax, 0.99f);
                Assert.Less(pane.center.y, 0.5f);

                Rect viaScreen = ResponsiveCameraFocus.GetCameraFocusViewport(
                    cam,
                    ResponsiveCameraFocusScreen.Bounds,
                    playRt);
                Assert.AreEqual(pane.y, viaScreen.y, 1e-4f);
                Assert.AreEqual(pane.height, viaScreen.height, 1e-4f);
                Assert.AreEqual(0f, cam.rect.y, 1e-4f);
                Assert.AreEqual(1f, cam.rect.height, 1e-4f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(playGo);
                UnityEngine.Object.DestroyImmediate(canvasGo);
                UnityEngine.Object.DestroyImmediate(camGo);
            }
        }

        [Test]
        public void GetUiBoundsViewport_WorldSpaceCanvas_UsesCameraProjection()
        {
            var camGo = new GameObject("Cam", typeof(Camera));
            var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            var playGo = new GameObject("Play", typeof(RectTransform));
            try
            {
                Camera cam = camGo.GetComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = 5f;
                cam.rect = ResponsiveCameraState.FullViewport;

                Canvas canvas = canvasGo.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                RectTransform canvasRt = canvasGo.GetComponent<RectTransform>();
                canvasRt.sizeDelta = new Vector2(4f, 2f);
                canvasRt.position = new Vector3(2f, 0f, 10f);

                RectTransform playRt = playGo.GetComponent<RectTransform>();
                playRt.SetParent(canvasRt, false);
                playRt.anchorMin = new Vector2(0.25f, 0.25f);
                playRt.anchorMax = new Vector2(0.75f, 0.75f);
                playRt.offsetMin = Vector2.zero;
                playRt.offsetMax = Vector2.zero;

                Assert.IsTrue(ResponsiveCameraFocus.GetUiBoundsViewport(cam, playRt, out Rect pane));
                Vector3 expected = cam.WorldToViewportPoint(playRt.position);
                Assert.AreEqual(expected.x, pane.center.x, 0.02f);
                Assert.AreEqual(expected.y, pane.center.y, 0.02f);
                Assert.Greater(pane.center.x, 0.5f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(playGo);
                UnityEngine.Object.DestroyImmediate(canvasGo);
                UnityEngine.Object.DestroyImmediate(camGo);
            }
        }

        [Test]
        public void ActiveSlotUsesFocusBoundsRect_OnlyMatchesResolvedSlot()
        {
            var phoneGo = new GameObject("Phone", typeof(RectTransform));
            var foldGo = new GameObject("Fold", typeof(RectTransform));
            try
            {
                RectTransform phone = phoneGo.GetComponent<RectTransform>();
                RectTransform fold = foldGo.GetComponent<RectTransform>();
                var layout = new ResponsiveCameraLayout
                {
                    phoneLandscape = new ResponsiveCameraState
                    {
                        enabled = true,
                        focusBounds = phone,
                    },
                    foldHalfOpenedLandscape = new ResponsiveCameraState
                    {
                        enabled = true,
                        focusBounds = fold,
                    },
                };

                var key = new ResponsiveDevice.LayoutKey(
                    ResponsiveDevice.FormFactor.Phone,
                    ResponsiveDevice.Orientation.Landscape);
                Assert.IsTrue(layout.ActiveSlotUsesFocusBoundsRect(phone, key));
                Assert.IsFalse(layout.ActiveSlotUsesFocusBoundsRect(fold, key));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(foldGo);
                UnityEngine.Object.DestroyImmediate(phoneGo);
            }
        }

        [Test]
        public void GetTargetWorldBounds_CombinesChildColliders()
        {
            var root = new GameObject("Target");
            var left = new GameObject("Left");
            var right = new GameObject("Right");
            try
            {
                left.transform.SetParent(root.transform);
                right.transform.SetParent(root.transform);
                left.transform.localPosition = new Vector3(-2f, 0f, 0f);
                right.transform.localPosition = new Vector3(3f, 0f, 0f);
                left.AddComponent<BoxCollider>().size = Vector3.one;
                right.AddComponent<BoxCollider>().size = Vector3.one;

                Assert.IsTrue(ResponsiveCameraFocus.GetTargetWorldBounds(
                    root.transform, null, out Bounds bounds));
                Assert.AreEqual(-2.5f, bounds.min.x, 1e-4f);
                Assert.AreEqual(3.5f, bounds.max.x, 1e-4f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void FocusGeometryHash_ChangesWhenCameraRenderSizeSettles()
        {
            var ownerGo = new GameObject("ResponsiveCamera");
            var cameraGo = new GameObject("Camera", typeof(Camera));
            var targetGo = new GameObject("Target", typeof(BoxCollider));
            var square = new RenderTexture(100, 100, 0);
            var wide = new RenderTexture(200, 100, 0);
            try
            {
                Camera camera = cameraGo.GetComponent<Camera>();
                camera.targetTexture = square;

                ResponsiveCamera owner = ownerGo.AddComponent<ResponsiveCamera>();
                var serialized = new SerializedObject(owner);
                SerializedProperty cameras = serialized.FindProperty("cameras");
                cameras.arraySize = 1;
                SerializedProperty layout = cameras.GetArrayElementAtIndex(0);
                layout.FindPropertyRelative("target").objectReferenceValue = camera;
                SerializedProperty state = layout.FindPropertyRelative("phonePortrait");
                state.FindPropertyRelative("enabled").boolValue = true;
                state.FindPropertyRelative("focusTarget").objectReferenceValue = targetGo.transform;
                state.FindPropertyRelative("focusScreen").intValue =
                    (int)ResponsiveCameraFocusScreen.Full;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                var method = typeof(ResponsiveCamera).GetMethod(
                    "ComputeFocusGeometryHashes",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic);
                object[] args = { 0, false };
                int squareHash = (int)method.Invoke(owner, args);

                camera.targetTexture = wide;
                args[1] = false;
                int wideHash = (int)method.Invoke(owner, args);

                Assert.AreNotEqual(squareHash, wideHash);
                Assert.IsTrue((bool)args[1]);
            }
            finally
            {
                cameraGo.GetComponent<Camera>().targetTexture = null;
                UnityEngine.Object.DestroyImmediate(wide);
                UnityEngine.Object.DestroyImmediate(square);
                UnityEngine.Object.DestroyImmediate(targetGo);
                UnityEngine.Object.DestroyImmediate(cameraGo);
                UnityEngine.Object.DestroyImmediate(ownerGo);
            }
        }

        [Test]
        public void FocusGeometryHash_IgnoresCameraPose_SplitsTargetMotion()
        {
            var ownerGo = new GameObject("ResponsiveCamera");
            var cameraGo = new GameObject("Camera", typeof(Camera));
            var targetGo = new GameObject("Target", typeof(BoxCollider));
            try
            {
                Camera camera = cameraGo.GetComponent<Camera>();
                ResponsiveCamera owner = ownerGo.AddComponent<ResponsiveCamera>();
                var serialized = new SerializedObject(owner);
                SerializedProperty cameras = serialized.FindProperty("cameras");
                cameras.arraySize = 1;
                SerializedProperty layout = cameras.GetArrayElementAtIndex(0);
                layout.FindPropertyRelative("target").objectReferenceValue = camera;
                SerializedProperty state = layout.FindPropertyRelative("phonePortrait");
                state.FindPropertyRelative("enabled").boolValue = true;
                state.FindPropertyRelative("focusTarget").objectReferenceValue = targetGo.transform;
                state.FindPropertyRelative("focusScreen").intValue =
                    (int)ResponsiveCameraFocusScreen.Full;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                var method = typeof(ResponsiveCamera).GetMethod(
                    "ComputeFocusGeometryHashes",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic);
                object[] args = { 0, false };
                int layoutHash = (int)method.Invoke(owner, args);
                int targetHash = (int)args[0];
                Assert.IsTrue((bool)args[1]);

                // External camera script: pose / projection are Apply outputs, not watched.
                cameraGo.transform.position += new Vector3(4f, 2f, -1f);
                cameraGo.transform.rotation = Quaternion.Euler(30f, 10f, 0f);
                camera.orthographicSize += 3f;
                camera.fieldOfView += 10f;
                args = new object[] { 0, false };
                Assert.AreEqual(layoutHash, (int)method.Invoke(owner, args));
                Assert.AreEqual(targetHash, (int)args[0]);

                // Focus Target motion changes only the target hash (no HUD re-layout).
                targetGo.transform.position += new Vector3(5f, 0f, 0f);
                Physics.SyncTransforms();
                args = new object[] { 0, false };
                Assert.AreEqual(layoutHash, (int)method.Invoke(owner, args));
                Assert.AreNotEqual(targetHash, (int)args[0]);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(targetGo);
                UnityEngine.Object.DestroyImmediate(cameraGo);
                UnityEngine.Object.DestroyImmediate(ownerGo);
            }
        }

        [Test]
        public void SpriteFit_BackSortingLayerId_MatchesFirstLayer()
        {
            Assert.AreEqual(
                SortingLayer.layers[0].id,
                ResponsiveCameraSpriteFit.GetBackSortingLayerId());
        }

        [Test]
        public void Focus_Fit_Bounds_KeepsWholeTableBelowHud()
        {
            var camGo = new GameObject("Cam");
            var targetGo = new GameObject("Table");
            var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            var playGo = new GameObject("Play", typeof(RectTransform));
            try
            {
                Camera cam = camGo.AddComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = 5f;
                cam.aspect = 2.2f;
                cam.rect = ResponsiveCameraState.FullViewport;
                camGo.transform.position = new Vector3(0f, 10f, 0f);
                camGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                var box = targetGo.AddComponent<BoxCollider>();
                box.size = new Vector3(4f, 1f, 2f);

                Canvas canvas = canvasGo.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                RectTransform canvasRt = canvasGo.GetComponent<RectTransform>();
                canvasRt.anchorMin = new Vector2(0.5f, 0.5f);
                canvasRt.anchorMax = new Vector2(0.5f, 0.5f);
                canvasRt.pivot = new Vector2(0.5f, 0.5f);
                canvasRt.sizeDelta = new Vector2(800f, 400f);
                canvasRt.anchoredPosition = Vector2.zero;

                RectTransform playRt = playGo.GetComponent<RectTransform>();
                playRt.SetParent(canvasRt, false);
                playRt.anchorMin = Vector2.zero;
                playRt.anchorMax = Vector2.one;
                playRt.pivot = new Vector2(0.5f, 0.5f);
                playRt.anchoredPosition = new Vector2(0f, -40f);
                playRt.sizeDelta = new Vector2(0f, -80f);
                Canvas.ForceUpdateCanvases();

                ResponsiveCameraFocus.Apply(
                    cam,
                    targetGo.transform,
                    ResponsiveCameraFocusScreen.Bounds,
                    ResponsiveCameraFocusFit.Fit,
                    1f,
                    null,
                    playRt);

                Rect pane = ResponsiveCameraFocus.GetCameraFocusViewport(
                    cam,
                    ResponsiveCameraFocusScreen.Bounds,
                    playRt);
                Assert.Less(pane.center.y, 0.5f);
                Assert.Less(pane.yMax, 0.95f);

                float depth = ResponsiveCameraSpriteFit.PerspectiveDepth(cam, targetGo.transform);
                Vector2 view = ResponsiveCameraSpriteFit.GetVisibleWorldSize(cam, depth, pane);
                Vector2 size = ResponsiveCameraFocus.MeasureTargetSize(targetGo.transform, cam);
                Assert.LessOrEqual(size.x / view.x, 1.02f);
                Assert.LessOrEqual(size.y / view.y, 1.02f);

                Vector3 viewCenter = cam.ViewportToWorldPoint(
                    new Vector3(pane.center.x, pane.center.y, Mathf.Abs(depth)));
                Assert.AreEqual(box.bounds.center.x, viewCenter.x, 0.05f);
                Assert.AreEqual(box.bounds.center.z, viewCenter.z, 0.05f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(playGo);
                UnityEngine.Object.DestroyImmediate(canvasGo);
                UnityEngine.Object.DestroyImmediate(targetGo);
                UnityEngine.Object.DestroyImmediate(camGo);
            }
        }

        [Test]
        public void Focus_Auto_Phone_UsesBoundsWhenAssigned()
        {
            var camGo = new GameObject("Cam");
            var targetGo = new GameObject("Table");
            var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            var playGo = new GameObject("Play", typeof(RectTransform));
            try
            {
                Camera cam = camGo.AddComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = 5f;
                cam.aspect = 1f;
                cam.rect = ResponsiveCameraState.FullViewport;
                camGo.transform.position = new Vector3(0f, 10f, 0f);
                camGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                targetGo.transform.position = Vector3.zero;

                Canvas canvas = canvasGo.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                RectTransform canvasRt = canvasGo.GetComponent<RectTransform>();
                canvasRt.anchorMin = new Vector2(0.5f, 0.5f);
                canvasRt.anchorMax = new Vector2(0.5f, 0.5f);
                canvasRt.pivot = new Vector2(0.5f, 0.5f);
                canvasRt.sizeDelta = new Vector2(800f, 400f);
                canvasRt.anchoredPosition = Vector2.zero;

                RectTransform playRt = playGo.GetComponent<RectTransform>();
                playRt.SetParent(canvasRt, false);
                playRt.anchorMin = Vector2.zero;
                playRt.anchorMax = Vector2.one;
                playRt.pivot = new Vector2(0.5f, 0.5f);
                playRt.anchoredPosition = new Vector2(0f, -40f);
                playRt.sizeDelta = new Vector2(0f, -80f);
                Canvas.ForceUpdateCanvases();

                var layout = new ResponsiveCameraLayout
                {
                    target = cam,
                    phoneLandscape = new ResponsiveCameraState
                    {
                        enabled = true,
                        viewport = ResponsiveCameraState.FullViewport,
                        applyLocalPosition = true,
                        localPosition = new Vector3(0f, 10f, 0f),
                        applyLocalRotation = true,
                        localEulerAngles = new Vector3(90f, 0f, 0f),
                        orthographic = true,
                        applyOrthographicSize = true,
                        orthographicSize = 5f,
                        focusTarget = targetGo.transform,
                        focusScreen = ResponsiveCameraFocusScreen.Auto,
                        focusFit = ResponsiveCameraFocusFit.Center,
                        focusBounds = playRt,
                    },
                };
                layout.Apply(new ResponsiveDevice.LayoutKey(
                    ResponsiveDevice.FormFactor.Phone, ResponsiveDevice.Orientation.Landscape));

                Assert.AreEqual(0f, cam.rect.y, 1e-4f);
                Assert.AreEqual(1f, cam.rect.height, 1e-4f);
                Rect pane = ResponsiveCameraFocus.GetCameraFocusViewport(
                    cam,
                    ResponsiveCameraFocusScreen.Auto,
                    playRt);
                Assert.Less(pane.center.y, 0.5f);
                float depth = ResponsiveCameraSpriteFit.PerspectiveDepth(cam, targetGo.transform);
                Vector3 viewCenter = cam.ViewportToWorldPoint(
                    new Vector3(pane.center.x, pane.center.y, Mathf.Abs(depth)));
                Assert.AreEqual(0f, viewCenter.x, 0.05f);
                Assert.AreEqual(0f, viewCenter.z, 0.05f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(playGo);
                UnityEngine.Object.DestroyImmediate(canvasGo);
                UnityEngine.Object.DestroyImmediate(targetGo);
                UnityEngine.Object.DestroyImmediate(camGo);
            }
        }

        [Test]
        public void Focus_Auto_Fold_UsesSlotBoundsWhenAssigned()
        {
            var camGo = new GameObject("Cam");
            var targetGo = new GameObject("Table");
            var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            var playGo = new GameObject("Play", typeof(RectTransform));
            try
            {
                Camera cam = camGo.AddComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = 5f;
                cam.aspect = 1f;
                cam.rect = ResponsiveCameraState.FullViewport;
                camGo.transform.position = new Vector3(0f, 10f, 0f);
                camGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                targetGo.transform.position = Vector3.zero;

                Canvas canvas = canvasGo.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                RectTransform canvasRt = canvasGo.GetComponent<RectTransform>();
                canvasRt.anchorMin = new Vector2(0.5f, 0.5f);
                canvasRt.anchorMax = new Vector2(0.5f, 0.5f);
                canvasRt.pivot = new Vector2(0.5f, 0.5f);
                canvasRt.sizeDelta = new Vector2(800f, 400f);
                canvasRt.anchoredPosition = Vector2.zero;

                RectTransform playRt = playGo.GetComponent<RectTransform>();
                playRt.SetParent(canvasRt, false);
                playRt.anchorMin = Vector2.zero;
                playRt.anchorMax = Vector2.one;
                playRt.pivot = new Vector2(0.5f, 0.5f);
                playRt.sizeDelta = new Vector2(0f, -80f);
                playRt.anchoredPosition = new Vector2(0f, -40f);
                Canvas.ForceUpdateCanvases();

                var layout = new ResponsiveCameraLayout
                {
                    target = cam,
                    foldHalfOpenedLandscape = new ResponsiveCameraState
                    {
                        enabled = true,
                        viewport = ResponsiveCameraState.FullViewport,
                        applyLocalPosition = true,
                        localPosition = new Vector3(0f, 10f, 0f),
                        applyLocalRotation = true,
                        localEulerAngles = new Vector3(90f, 0f, 0f),
                        orthographic = true,
                        applyOrthographicSize = true,
                        orthographicSize = 5f,
                        focusTarget = targetGo.transform,
                        focusScreen = ResponsiveCameraFocusScreen.Auto,
                        focusFit = ResponsiveCameraFocusFit.Center,
                        focusBounds = playRt,
                    },
                };
                layout.Apply(
                    new ResponsiveDevice.LayoutKey(
                        ResponsiveDevice.FormFactor.FoldHalfOpened,
                        ResponsiveDevice.Orientation.Landscape),
                    applyViewport: true);

                Assert.AreEqual(0f, cam.rect.y, 1e-3f);
                Assert.AreEqual(1f, cam.rect.height, 1e-3f);
                Rect pane = ResponsiveCameraFocus.GetCameraFocusViewport(
                    cam,
                    ResponsiveCameraFocusScreen.Auto,
                    playRt);
                Assert.Less(pane.center.y, 0.5f);
                float depth = ResponsiveCameraSpriteFit.PerspectiveDepth(cam, targetGo.transform);
                Vector3 viewCenter = cam.ViewportToWorldPoint(
                    new Vector3(pane.center.x, pane.center.y, Mathf.Abs(depth)));
                Assert.AreEqual(0f, viewCenter.x, 0.05f);
                Assert.AreEqual(0f, viewCenter.z, 0.05f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(playGo);
                UnityEngine.Object.DestroyImmediate(canvasGo);
                UnityEngine.Object.DestroyImmediate(targetGo);
                UnityEngine.Object.DestroyImmediate(camGo);
            }
        }

        [Test]
        public void GetCameraFocusViewport_BoundsMissing_IsFull()
        {
            var camGo = new GameObject("Cam", typeof(Camera));
            try
            {
                Camera cam = camGo.GetComponent<Camera>();
                cam.rect = ResponsiveCameraState.FullViewport;
                Rect pane = ResponsiveCameraFocus.GetCameraFocusViewport(
                    cam,
                    ResponsiveCameraFocusScreen.Bounds,
                    null);
                Assert.AreEqual(0f, pane.x, 1e-4f);
                Assert.AreEqual(0f, pane.y, 1e-4f);
                Assert.AreEqual(1f, pane.width, 1e-4f);
                Assert.AreEqual(1f, pane.height, 1e-4f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(camGo);
            }
        }

        [Test]
        public void GetCameraFocusViewport_StoredBoundsRect_UsedWithoutLiveRect()
        {
            var camGo = new GameObject("Cam", typeof(Camera));
            try
            {
                Camera cam = camGo.GetComponent<Camera>();
                cam.rect = ResponsiveCameraState.FullViewport;
                var stored = new Rect(0f, 0.1f, 1f, 0.7f);
                Rect pane = ResponsiveCameraFocus.GetCameraFocusViewport(
                    cam,
                    ResponsiveCameraFocusScreen.Bounds,
                    null,
                    stored);
                Assert.AreEqual(stored, pane);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(camGo);
            }
        }

        [Test]
        public void GetCameraFocusViewport_StoredBoundsRect_WinsOverLiveRect()
        {
            var camGo = new GameObject("Cam", typeof(Camera));
            var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            var playGo = new GameObject("Play", typeof(RectTransform));
            try
            {
                Camera cam = camGo.GetComponent<Camera>();
                cam.rect = ResponsiveCameraState.FullViewport;

                Canvas canvas = canvasGo.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                RectTransform canvasRt = canvasGo.GetComponent<RectTransform>();
                canvasRt.anchorMin = new Vector2(0.5f, 0.5f);
                canvasRt.anchorMax = new Vector2(0.5f, 0.5f);
                canvasRt.pivot = new Vector2(0.5f, 0.5f);
                canvasRt.sizeDelta = new Vector2(800f, 400f);
                canvasRt.anchoredPosition = Vector2.zero;

                RectTransform playRt = playGo.GetComponent<RectTransform>();
                playRt.SetParent(canvasRt, false);
                playRt.anchorMin = Vector2.zero;
                playRt.anchorMax = Vector2.one;
                playRt.pivot = new Vector2(0.5f, 0.5f);
                playRt.anchoredPosition = new Vector2(0f, -40f);
                playRt.sizeDelta = new Vector2(0f, -80f);
                Canvas.ForceUpdateCanvases();

                var stored = new Rect(0f, 0f, 0.5f, 1f);
                Rect live = ResponsiveCameraFocus.GetCameraFocusViewport(
                    cam, ResponsiveCameraFocusScreen.Bounds, playRt);
                Rect pane = ResponsiveCameraFocus.GetCameraFocusViewport(
                    cam, ResponsiveCameraFocusScreen.Bounds, playRt, stored);
                Assert.AreEqual(stored, pane);
                Assert.AreNotEqual(live, pane);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(playGo);
                UnityEngine.Object.DestroyImmediate(canvasGo);
                UnityEngine.Object.DestroyImmediate(camGo);
            }
        }

        [Test]
        public void Layout_SlotsKeepIndependentFocusBoundsLayouts()
        {
            var phoneLayout = new ResponsiveRectState
            {
                enabled = true,
                anchoredPosition = new Vector2(0f, -20f),
                sizeDelta = new Vector2(0f, -40f),
            };
            var foldLayout = new ResponsiveRectState
            {
                enabled = true,
                anchoredPosition = new Vector2(0f, -80f),
                sizeDelta = new Vector2(0f, -160f),
            };
            var layout = new ResponsiveCameraLayout
            {
                phoneLandscape = new ResponsiveCameraState
                {
                    enabled = true,
                    focusScreen = ResponsiveCameraFocusScreen.Bounds,
                    focusBoundsLayout = phoneLayout,
                },
                foldHalfOpenedLandscape = new ResponsiveCameraState
                {
                    enabled = true,
                    focusScreen = ResponsiveCameraFocusScreen.Bounds,
                    focusBoundsLayout = foldLayout,
                },
            };

            Assert.IsTrue(layout.phoneLandscape.focusBoundsLayout.HasSameLayout(phoneLayout));
            Assert.IsTrue(layout.foldHalfOpenedLandscape.focusBoundsLayout.HasSameLayout(foldLayout));
            Assert.IsFalse(
                layout.phoneLandscape.focusBoundsLayout.HasSameLayout(
                    layout.foldHalfOpenedLandscape.focusBoundsLayout));
        }

        [Test]
        public void Layout_ApplyFocusBoundsLayout_WritesWidgetFields()
        {
            var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            var playGo = new GameObject("Play", typeof(RectTransform));
            try
            {
                playGo.transform.SetParent(canvasGo.transform, false);
                RectTransform playRt = playGo.GetComponent<RectTransform>();
                playRt.anchorMin = new Vector2(0.5f, 0.5f);
                playRt.anchorMax = new Vector2(0.5f, 0.5f);
                playRt.pivot = new Vector2(0.5f, 0.5f);
                playRt.anchoredPosition = Vector2.zero;
                playRt.sizeDelta = new Vector2(10f, 10f);

                var state = new ResponsiveCameraState
                {
                    focusBounds = playRt,
                    focusBoundsLayout = new ResponsiveRectState
                    {
                        enabled = true,
                        anchorMin = new Vector2(0.5f, 0.5f),
                        anchorMax = new Vector2(0.5f, 0.5f),
                        pivot = new Vector2(0.5f, 0.5f),
                        anchoredPosition = new Vector2(4f, -6f),
                        sizeDelta = new Vector2(120f, 80f),
                        defaultScale = Vector2.one,
                    },
                };
                state.ApplyFocusBoundsLayout(
                    new Vector2(800f, 400f),
                    ResponsiveDevice.FormFactor.Phone,
                    ResponsiveDevice.Orientation.Landscape);
                Assert.AreEqual(new Vector2(4f, -6f), playRt.anchoredPosition);
                Assert.AreEqual(new Vector2(120f, 80f), playRt.sizeDelta);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(playGo);
                UnityEngine.Object.DestroyImmediate(canvasGo);
            }
        }

        [Test]
        public void Layout_ApplyFocusBoundsLayout_WithHub_MatchesApplyWidget()
        {
            var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            var playGo = new GameObject("Play", typeof(RectTransform));
            try
            {
                playGo.transform.SetParent(canvasGo.transform, false);
                RectTransform playRt = playGo.GetComponent<RectTransform>();
                playRt.anchorMin = Vector2.zero;
                playRt.anchorMax = Vector2.one;
                playRt.pivot = new Vector2(0.5f, 0.5f);
                playRt.offsetMin = Vector2.zero;
                playRt.offsetMax = Vector2.zero;

                var hub = canvasGo.AddComponent<ResponsiveUI>();
                var layout = new ResponsiveRectState
                {
                    enabled = true,
                    scaleFromDesignCanvas = true,
                    designScaleMode = ResponsiveDesignScaleMode.Width,
                    designCanvasSize = Vector2.zero,
                    defaultScale = Vector2.one,
                    anchorMin = Vector2.zero,
                    anchorMax = Vector2.one,
                    pivot = new Vector2(0.5f, 0.5f),
                    sizeDelta = new Vector2(-850f, -290f),
                };
                var state = new ResponsiveCameraState
                {
                    focusBounds = playRt,
                    focusBoundsLayout = layout,
                };

                state.ApplyFocusBoundsLayout(
                    new Vector2(400f, 200f),
                    ResponsiveDevice.FormFactor.Phone,
                    ResponsiveDevice.Orientation.Landscape,
                    hub);
                Vector2 fromCamera = playRt.sizeDelta;

                playRt.sizeDelta = Vector2.zero;
                hub.ApplyWidget(
                    playRt,
                    layout,
                    ResponsiveDevice.FormFactor.Phone,
                    ResponsiveDevice.Orientation.Landscape);
                Assert.AreEqual(fromCamera, playRt.sizeDelta);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(playGo);
                UnityEngine.Object.DestroyImmediate(canvasGo);
            }
        }

        [Test]
        public void Layout_Apply_WritesFocusBoundsLayoutWithoutFocusTarget()
        {
            var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            var playGo = new GameObject("Play", typeof(RectTransform));
            var camGo = new GameObject("Cam", typeof(Camera));
            try
            {
                playGo.transform.SetParent(canvasGo.transform, false);
                RectTransform playRt = playGo.GetComponent<RectTransform>();
                playRt.anchorMin = new Vector2(0.5f, 0.5f);
                playRt.anchorMax = new Vector2(0.5f, 0.5f);
                playRt.pivot = new Vector2(0.5f, 0.5f);
                playRt.anchoredPosition = Vector2.zero;
                playRt.sizeDelta = new Vector2(10f, 10f);

                var layout = new ResponsiveCameraLayout
                {
                    target = camGo.GetComponent<Camera>(),
                    phonePortrait = new ResponsiveCameraState
                    {
                        enabled = true,
                        focusBounds = playRt,
                        focusBoundsLayout = new ResponsiveRectState
                        {
                            enabled = true,
                            anchorMin = new Vector2(0.5f, 0.5f),
                            anchorMax = new Vector2(0.5f, 0.5f),
                            pivot = new Vector2(0.5f, 0.5f),
                            anchoredPosition = new Vector2(8f, -12f),
                            sizeDelta = new Vector2(200f, 90f),
                            defaultScale = Vector2.one,
                        },
                    },
                };
                layout.Apply(
                    new ResponsiveDevice.LayoutKey(
                        ResponsiveDevice.FormFactor.Phone,
                        ResponsiveDevice.Orientation.Portrait),
                    applyViewport: true,
                    new Vector2(800f, 400f));
                Assert.AreEqual(new Vector2(8f, -12f), playRt.anchoredPosition);
                Assert.AreEqual(new Vector2(200f, 90f), playRt.sizeDelta);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(playGo);
                UnityEngine.Object.DestroyImmediate(canvasGo);
                UnityEngine.Object.DestroyImmediate(camGo);
            }
        }

        [Test]
        public void Layout_Apply_SkipBoundsLayout_LeavesHud()
        {
            var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            var playGo = new GameObject("Play", typeof(RectTransform));
            var camGo = new GameObject("Cam", typeof(Camera));
            try
            {
                playGo.transform.SetParent(canvasGo.transform, false);
                RectTransform playRt = playGo.GetComponent<RectTransform>();
                playRt.anchorMin = new Vector2(0.5f, 0.5f);
                playRt.anchorMax = new Vector2(0.5f, 0.5f);
                playRt.pivot = new Vector2(0.5f, 0.5f);
                playRt.anchoredPosition = Vector2.zero;
                playRt.sizeDelta = new Vector2(10f, 10f);

                var layout = new ResponsiveCameraLayout
                {
                    target = camGo.GetComponent<Camera>(),
                    phonePortrait = new ResponsiveCameraState
                    {
                        enabled = true,
                        focusBounds = playRt,
                        focusBoundsLayout = new ResponsiveRectState
                        {
                            enabled = true,
                            anchorMin = new Vector2(0.5f, 0.5f),
                            anchorMax = new Vector2(0.5f, 0.5f),
                            pivot = new Vector2(0.5f, 0.5f),
                            anchoredPosition = new Vector2(8f, -12f),
                            sizeDelta = new Vector2(200f, 90f),
                            defaultScale = Vector2.one,
                        },
                    },
                };
                layout.Apply(
                    new ResponsiveDevice.LayoutKey(
                        ResponsiveDevice.FormFactor.Phone,
                        ResponsiveDevice.Orientation.Portrait),
                    applyViewport: true,
                    new Vector2(800f, 400f),
                    applyBoundsLayout: false);
                Assert.AreEqual(Vector2.zero, playRt.anchoredPosition);
                Assert.AreEqual(new Vector2(10f, 10f), playRt.sizeDelta);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(playGo);
                UnityEngine.Object.DestroyImmediate(canvasGo);
                UnityEngine.Object.DestroyImmediate(camGo);
            }
        }

        [Test]
        public void Layout_CaptureActiveFocusBoundsLayout_WritesActiveSlotOnly()
        {
            var playGo = new GameObject("Play", typeof(RectTransform));
            try
            {
                RectTransform playRt = playGo.GetComponent<RectTransform>();
                playRt.anchorMin = Vector2.zero;
                playRt.anchorMax = Vector2.one;
                playRt.pivot = new Vector2(0.5f, 0.5f);
                playRt.anchoredPosition = new Vector2(0f, -40f);
                playRt.sizeDelta = new Vector2(0f, -80f);

                var foldLayout = new ResponsiveRectState
                {
                    enabled = true,
                    sizeDelta = new Vector2(1f, 1f),
                };
                var layout = new ResponsiveCameraLayout
                {
                    phoneLandscape = new ResponsiveCameraState
                    {
                        enabled = true,
                        focusBounds = playRt,
                    },
                    foldHalfOpenedLandscape = new ResponsiveCameraState
                    {
                        enabled = true,
                        focusBounds = playRt,
                        focusBoundsLayout = foldLayout,
                    },
                };

                Assert.IsTrue(layout.CaptureActiveFocusBoundsLayout(
                    new ResponsiveDevice.LayoutKey(
                        ResponsiveDevice.FormFactor.Phone,
                        ResponsiveDevice.Orientation.Landscape),
                    new Vector2(800f, 400f)));
                Assert.IsTrue(layout.phoneLandscape.focusBoundsLayout.enabled);
                Assert.AreEqual(
                    new Vector2(0f, -40f),
                    layout.phoneLandscape.focusBoundsLayout.anchoredPosition);
                Assert.AreEqual(
                    new Vector2(0f, -80f),
                    layout.phoneLandscape.focusBoundsLayout.sizeDelta);
                Assert.AreEqual(
                    new Vector2(1f, 1f),
                    layout.foldHalfOpenedLandscape.focusBoundsLayout.sizeDelta);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(playGo);
            }
        }

        [Test]
        public void Layout_CaptureActiveFocusBoundsLayout_WritesDisabledExactSlot()
        {
            var playGo = new GameObject("Play", typeof(RectTransform));
            try
            {
                RectTransform playRt = playGo.GetComponent<RectTransform>();
                playRt.anchorMin = Vector2.zero;
                playRt.anchorMax = Vector2.one;
                playRt.pivot = new Vector2(0.5f, 0.5f);
                playRt.anchoredPosition = new Vector2(12f, -8f);
                playRt.sizeDelta = new Vector2(-20f, -40f);

                var layout = new ResponsiveCameraLayout
                {
                    phoneLandscape = new ResponsiveCameraState
                    {
                        enabled = false,
                        focusBounds = playRt,
                    },
                };

                Assert.IsTrue(layout.CaptureActiveFocusBoundsLayout(
                    new ResponsiveDevice.LayoutKey(
                        ResponsiveDevice.FormFactor.Phone,
                        ResponsiveDevice.Orientation.Landscape),
                    new Vector2(800f, 400f)));
                Assert.IsTrue(layout.phoneLandscape.focusBoundsLayout.enabled);
                Assert.AreEqual(
                    new Vector2(12f, -8f),
                    layout.phoneLandscape.focusBoundsLayout.anchoredPosition);
                Assert.AreEqual(
                    new Vector2(-20f, -40f),
                    layout.phoneLandscape.focusBoundsLayout.sizeDelta);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(playGo);
            }
        }

        [Test]
        public void Layout_CaptureActiveFocusBoundsLayout_StoresLiveScaleAsDefaultScale()
        {
            var playGo = new GameObject("Play", typeof(RectTransform));
            try
            {
                RectTransform playRt = playGo.GetComponent<RectTransform>();
                playRt.anchorMin = new Vector2(0.5f, 0.5f);
                playRt.anchorMax = new Vector2(0.5f, 0.5f);
                playRt.localScale = new Vector3(1.5f, 0.8f, 1f);

                var layout = new ResponsiveCameraLayout
                {
                    phoneLandscape = new ResponsiveCameraState
                    {
                        enabled = true,
                        focusBounds = playRt,
                        focusBoundsLayout = new ResponsiveRectState
                        {
                            enabled = true,
                            defaultScale = Vector2.one,
                        },
                    },
                };

                Assert.IsTrue(layout.CaptureActiveFocusBoundsLayout(
                    new ResponsiveDevice.LayoutKey(
                        ResponsiveDevice.FormFactor.Phone,
                        ResponsiveDevice.Orientation.Landscape),
                    new Vector2(800f, 400f)));
                Assert.AreEqual(
                    new Vector2(1.5f, 0.8f),
                    layout.phoneLandscape.focusBoundsLayout.defaultScale);
                Assert.IsFalse(layout.phoneLandscape.focusBoundsLayout.scaleFromDesignCanvas);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(playGo);
            }
        }

        [Test]
        public void Layout_CaptureActiveFocusBoundsLayout_ScaleOn_KeepsDesignCanvasSentinel()
        {
            var playGo = new GameObject("Play", typeof(RectTransform));
            try
            {
                RectTransform playRt = playGo.GetComponent<RectTransform>();
                playRt.anchorMin = Vector2.zero;
                playRt.anchorMax = Vector2.one;
                playRt.sizeDelta = new Vector2(-100f, -40f);

                var layout = new ResponsiveCameraLayout
                {
                    phoneLandscape = new ResponsiveCameraState
                    {
                        enabled = true,
                        focusBounds = playRt,
                        focusBoundsLayout = new ResponsiveRectState
                        {
                            enabled = true,
                            scaleFromDesignCanvas = true,
                            designScaleMode = ResponsiveDesignScaleMode.Auto,
                            designCanvasSize = Vector2.zero,
                            defaultScale = Vector2.one,
                            anchorMin = Vector2.zero,
                            anchorMax = Vector2.one,
                            sizeDelta = new Vector2(-850f, -290f),
                        },
                    },
                };

                layout.CaptureActiveFocusBoundsLayout(
                    new ResponsiveDevice.LayoutKey(
                        ResponsiveDevice.FormFactor.Phone,
                        ResponsiveDevice.Orientation.Landscape),
                    new Vector2(800f, 400f));
                Assert.IsTrue(layout.phoneLandscape.focusBoundsLayout.scaleFromDesignCanvas);
                Assert.AreEqual(
                    ResponsiveDesignScaleMode.Auto,
                    layout.phoneLandscape.focusBoundsLayout.designScaleMode);
                Assert.AreEqual(
                    Vector2.zero,
                    layout.phoneLandscape.focusBoundsLayout.designCanvasSize);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(playGo);
            }
        }

        [Test]
        public void Layout_CaptureActiveFocusBoundsLayout_AppliedHud_KeepsDesignInsetsAndOriginScale()
        {
            var parentGo = new GameObject("Parent", typeof(RectTransform));
            var playGo = new GameObject("Play", typeof(RectTransform));
            try
            {
                RectTransform parentRt = parentGo.GetComponent<RectTransform>();
                parentRt.anchorMin = parentRt.anchorMax = new Vector2(0.5f, 0.5f);
                parentRt.sizeDelta = new Vector2(500f, 250f);
                parentRt.ForceUpdateRectTransforms();

                playGo.transform.SetParent(parentGo.transform, false);
                RectTransform playRt = playGo.GetComponent<RectTransform>();
                playRt.anchorMin = Vector2.zero;
                playRt.anchorMax = Vector2.one;
                playRt.pivot = new Vector2(0.5f, 0.5f);
                playRt.offsetMin = Vector2.zero;
                playRt.offsetMax = Vector2.zero;

                var authored = new ResponsiveRectState
                {
                    enabled = true,
                    scaleFromDesignCanvas = true,
                    designScaleMode = ResponsiveDesignScaleMode.IndependentXY,
                    designCanvasSize = new Vector2(1000f, 500f),
                    defaultScale = Vector2.one,
                    anchorMin = Vector2.zero,
                    anchorMax = Vector2.one,
                    pivot = new Vector2(0.5f, 0.5f),
                    sizeDelta = new Vector2(-850f, -290f),
                };
                var layout = new ResponsiveCameraLayout
                {
                    phoneLandscape = new ResponsiveCameraState
                    {
                        enabled = true,
                        focusBounds = playRt,
                        focusBoundsLayout = authored,
                    },
                };

                var key = new ResponsiveDevice.LayoutKey(
                    ResponsiveDevice.FormFactor.Phone,
                    ResponsiveDevice.Orientation.Landscape);
                var uiSize = new Vector2(500f, 250f);
                layout.phoneLandscape.ApplyFocusBoundsLayout(
                    uiSize,
                    ResponsiveDevice.FormFactor.Phone,
                    ResponsiveDevice.Orientation.Landscape);
                Assert.AreNotEqual(authored.sizeDelta, playRt.sizeDelta);
                Assert.AreNotEqual(Vector3.one, playRt.localScale);

                Assert.IsFalse(layout.CaptureActiveFocusBoundsLayout(key, uiSize));
                Assert.AreEqual(authored.sizeDelta, layout.phoneLandscape.focusBoundsLayout.sizeDelta);
                Assert.AreEqual(authored.defaultScale, layout.phoneLandscape.focusBoundsLayout.defaultScale);
                Assert.AreEqual(
                    authored.designCanvasSize,
                    layout.phoneLandscape.focusBoundsLayout.designCanvasSize);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(playGo);
                UnityEngine.Object.DestroyImmediate(parentGo);
            }
        }

        [Test]
        public void Layout_CaptureActiveFocusBoundsLayout_ScaleOn_UnscalesEditedHud()
        {
            var parentGo = new GameObject("Parent", typeof(RectTransform));
            var playGo = new GameObject("Play", typeof(RectTransform));
            try
            {
                RectTransform parentRt = parentGo.GetComponent<RectTransform>();
                parentRt.anchorMin = parentRt.anchorMax = new Vector2(0.5f, 0.5f);
                parentRt.sizeDelta = new Vector2(500f, 250f);
                parentRt.ForceUpdateRectTransforms();

                playGo.transform.SetParent(parentGo.transform, false);
                RectTransform playRt = playGo.GetComponent<RectTransform>();
                playRt.anchorMin = Vector2.zero;
                playRt.anchorMax = Vector2.one;
                playRt.pivot = new Vector2(0.5f, 0.5f);

                var authored = new ResponsiveRectState
                {
                    enabled = true,
                    scaleFromDesignCanvas = true,
                    designScaleMode = ResponsiveDesignScaleMode.IndependentXY,
                    designCanvasSize = new Vector2(1000f, 500f),
                    defaultScale = Vector2.one,
                    anchorMin = Vector2.zero,
                    anchorMax = Vector2.one,
                    pivot = new Vector2(0.5f, 0.5f),
                    sizeDelta = new Vector2(-850f, -290f),
                };
                var layout = new ResponsiveCameraLayout
                {
                    phoneLandscape = new ResponsiveCameraState
                    {
                        enabled = true,
                        focusBounds = playRt,
                        focusBoundsLayout = authored,
                    },
                };

                var key = new ResponsiveDevice.LayoutKey(
                    ResponsiveDevice.FormFactor.Phone,
                    ResponsiveDevice.Orientation.Landscape);
                var uiSize = new Vector2(500f, 250f);
                layout.phoneLandscape.ApplyFocusBoundsLayout(
                    uiSize,
                    ResponsiveDevice.FormFactor.Phone,
                    ResponsiveDevice.Orientation.Landscape);

                Vector2 liveApplied = playRt.sizeDelta;
                playRt.sizeDelta = liveApplied + new Vector2(-20f, -10f);

                Assert.IsTrue(layout.CaptureActiveFocusBoundsLayout(key, uiSize));
                Assert.AreEqual(
                    new Vector2(-870f, -300f),
                    layout.phoneLandscape.focusBoundsLayout.sizeDelta);
                Assert.AreEqual(Vector2.one, layout.phoneLandscape.focusBoundsLayout.defaultScale);
                Assert.AreNotEqual(
                    playRt.sizeDelta,
                    layout.phoneLandscape.focusBoundsLayout.sizeDelta);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(playGo);
                UnityEngine.Object.DestroyImmediate(parentGo);
            }
        }

        [Test]
        public void SizeChangeRequiresReapply_FocusBoundsLayoutWithoutFocus_True()
        {
            var go = new GameObject("CamSizeReapply");
            var hudGo = new GameObject("FocusHud", typeof(RectTransform));
            try
            {
                var camera = go.AddComponent<ResponsiveCamera>();
                var hud = hudGo.GetComponent<RectTransform>();
                var so = new SerializedObject(camera);
                SerializedProperty cameras = so.FindProperty("cameras");
                cameras.arraySize = 1;
                SerializedProperty row = cameras.GetArrayElementAtIndex(0);
                for (int i = 0; i < ResponsiveCameraLayout.StatePropertyNames.Length; i++)
                {
                    SerializedProperty slot = row.FindPropertyRelative(
                        ResponsiveCameraLayout.StatePropertyNames[i]);
                    slot.FindPropertyRelative("scaleFromUiSize").boolValue = false;
                    slot.FindPropertyRelative("scaleProjection").boolValue = false;
                    slot.FindPropertyRelative("focusBounds").objectReferenceValue = hud;
                    slot.FindPropertyRelative("focusBoundsLayout")
                        .FindPropertyRelative("enabled").boolValue = true;
                }
                so.ApplyModifiedPropertiesWithoutUndo();

                Assert.IsTrue(camera.SizeChangeRequiresReapply());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                UnityEngine.Object.DestroyImmediate(hudGo);
            }
        }

        [Test]
        public void SizeChangeRequiresReapply_BackgroundOnly_True()
        {
            var go = new GameObject("CamBgSizeReapply");
            var bgGo = new GameObject("CamBg", typeof(SpriteRenderer));
            try
            {
                var camera = go.AddComponent<ResponsiveCamera>();
                var so = new SerializedObject(camera);
                SerializedProperty cameras = so.FindProperty("cameras");
                cameras.arraySize = 1;
                SerializedProperty row = cameras.GetArrayElementAtIndex(0);
                row.FindPropertyRelative("background").objectReferenceValue =
                    bgGo.GetComponent<SpriteRenderer>();
                for (int i = 0; i < ResponsiveCameraLayout.StatePropertyNames.Length; i++)
                {
                    SerializedProperty slot = row.FindPropertyRelative(
                        ResponsiveCameraLayout.StatePropertyNames[i]);
                    slot.FindPropertyRelative("scaleFromUiSize").boolValue = false;
                    slot.FindPropertyRelative("scaleProjection").boolValue = false;
                }
                so.ApplyModifiedPropertiesWithoutUndo();

                Assert.IsTrue(camera.SizeChangeRequiresReapply());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                UnityEngine.Object.DestroyImmediate(bgGo);
            }
        }

        [Test]
        public void SizeChangeRequiresReapply_ScaleFromUiSize_WithHubScaler_True()
        {
            var root = new GameObject("CamScalerRoot", typeof(Canvas), typeof(CanvasScaler));
            try
            {
                var scaler = root.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                var hub = root.AddComponent<ResponsiveUI>();
                var hubSo = new SerializedObject(hub);
                hubSo.FindProperty("applyCanvasScaler").boolValue = true;
                hubSo.FindProperty("canvasScaler").objectReferenceValue = scaler;
                hubSo.ApplyModifiedPropertiesWithoutUndo();

                var camera = root.AddComponent<ResponsiveCamera>();
                camera.AssignHub(hub);
                var so = new SerializedObject(camera);
                SerializedProperty cameras = so.FindProperty("cameras");
                cameras.arraySize = 1;
                SerializedProperty row = cameras.GetArrayElementAtIndex(0);
                for (int i = 0; i < ResponsiveCameraLayout.StatePropertyNames.Length; i++)
                {
                    SerializedProperty slot = row.FindPropertyRelative(
                        ResponsiveCameraLayout.StatePropertyNames[i]);
                    slot.FindPropertyRelative("scaleFromUiSize").boolValue = true;
                    slot.FindPropertyRelative("scaleProjection").boolValue = false;
                }
                so.ApplyModifiedPropertiesWithoutUndo();

                Assert.IsTrue(hub.UsesCanvasScalerResolutionFit);
                Assert.IsTrue(camera.SizeChangeRequiresReapply());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        // Independent optical contract — do not call production ScaleFieldOfView / ScaleOrthographicSize
        // for the expected value. Those helpers used to share one wrong pixel-ratio formula, and
        // Apply-vs-helper tests still passed.

        static Vector2 IndependentOrientDesign(Vector2 designUiSize, Vector2 currentUiSize)
        {
            if (Mathf.Abs(currentUiSize.x - currentUiSize.y) < 1e-5f)
                return designUiSize;
            bool designLandscape = designUiSize.x > designUiSize.y;
            bool liveLandscape = currentUiSize.x > currentUiSize.y;
            return designLandscape == liveLandscape
                ? designUiSize
                : new Vector2(designUiSize.y, designUiSize.x);
        }

        static float IndependentAspectFramedOrtho(
            float designOrtho,
            Vector2 currentUiSize,
            Vector2 designUiSize,
            ResponsiveDesignScaleMode mode)
        {
            Vector2 design = IndependentOrientDesign(designUiSize, currentUiSize);
            float designAspect = design.x / design.y;
            float liveAspect = currentUiSize.x / currentUiSize.y;
            if (mode == ResponsiveDesignScaleMode.Auto)
                mode = liveAspect > designAspect
                    ? ResponsiveDesignScaleMode.Height
                    : ResponsiveDesignScaleMode.Width;
            float widthMatched = designOrtho * (designAspect / liveAspect);
            float heightMatched = designOrtho;
            switch (mode)
            {
                case ResponsiveDesignScaleMode.Height:
                    return heightMatched;
                case ResponsiveDesignScaleMode.UniformMin:
                    return Mathf.Max(widthMatched, heightMatched);
                case ResponsiveDesignScaleMode.UniformMax:
                    return Mathf.Min(widthMatched, heightMatched);
                default:
                    return widthMatched;
            }
        }

        static float IndependentAspectFramedFov(
            float designFov,
            Vector2 currentUiSize,
            Vector2 designUiSize,
            ResponsiveDesignScaleMode mode)
        {
            float tanHalf = Mathf.Tan(0.5f * designFov * Mathf.Deg2Rad);
            float liveTan = IndependentAspectFramedOrtho(
                tanHalf, currentUiSize, designUiSize, mode);
            return 2f * Mathf.Atan(liveTan) * Mathf.Rad2Deg;
        }

        static float PixelRatioScaledFov(
            float designValue, Vector2 currentUiSize, Vector2 designUiSize) =>
            designValue * (currentUiSize.x / designUiSize.x);

        static void AssertPixelRatioIsWrong(
            float designValue,
            Vector2 currentUiSize,
            Vector2 designUiSize,
            float actual,
            string label)
        {
            float pixel = PixelRatioScaledFov(designValue, currentUiSize, designUiSize);
            Assert.Greater(
                Mathf.Abs(actual - pixel),
                1e-2f,
                "{0} must not track width pixel ratio (pixel={1} actual={2})",
                label,
                pixel,
                actual);
        }
    }
}
