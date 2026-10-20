using FDPlus.ResponsiveUI.Shared.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FDPlus.Foldable.Editor
{
    /// <summary>
    /// Builds a ready-to-run Foldable hierarchy in the open scene (or a new empty scene).
    /// </summary>
    public static class FoldableSetupMenu
    {
        #region Constants

        const string RootName = "FoldableSetup";
        const string UiRootName = "FoldableUI";

        #endregion

        #region Editor

        [MenuItem(ResponsiveUiEditorMenus.CreateFoldableSetup, false, ResponsiveUiEditorMenus.PriorityScene)]
        public static void CreateSetupInScene()
        {
            var result = BuildSetup(
                createNewScene: false,
                reuseExistingCamera: true,
                includeSkyBoxCamera: true,
                includeHealthCanvas: true,
                includeUiBlur: true,
                includeSimulator: true,
                deviceKind: FoldableDeviceKind.FoldablePhone);

            Selection.activeGameObject = result.Root;
            EditorGUIUtility.PingObject(result.Root);
            EditorSceneManager.MarkSceneDirty(result.Root.scene);

            EditorUtility.DisplayDialog(
                "Responsive UI",
                BuildSummary(result),
                "OK");
        }

        [MenuItem(ResponsiveUiEditorMenus.CreateFoldableCompleteScene, false, ResponsiveUiEditorMenus.PriorityScene + 1)]
        public static void CreateCompleteScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var result = BuildSetup(
                createNewScene: true,
                reuseExistingCamera: false,
                includeSkyBoxCamera: true,
                includeHealthCanvas: true,
                includeUiBlur: true,
                includeSimulator: true,
                deviceKind: FoldableDeviceKind.FoldablePhone);

            Selection.activeGameObject = result.Root;
            EditorSceneManager.MarkSceneDirty(scene);

            EditorUtility.DisplayDialog(
                "Responsive UI",
                "Created a new empty scene with a full Fold & Flip hierarchy.\n\n" +
                BuildSummary(result) +
                $"\nSave the scene ({ResponsiveUiEditorPath.SaveShortcutHint}) when ready.",
                "OK");
        }

        [MenuItem(ResponsiveUiEditorMenus.DiagnoseSimulatorBridge, false, ResponsiveUiEditorMenus.PriorityDebug)]
        public static void DiagnoseSimulatorBridge()
        {
            string report = SimulatorDeviceBridge.Diagnose();
            Debug.Log("[Responsive UI] Simulator Bridge diagnose:\n" + report);
            EditorUtility.DisplayDialog("Responsive UI — Simulator Bridge", report, "OK");
        }

        #endregion

        #region Types

        public sealed class SetupResult
        {
            public GameObject Root;
            public FoldablePostureDetector Detector;
            public FoldableSimulator Simulator;
            public Camera MainCamera;
            public Camera SkyBoxCamera;
            public Canvas ControlsCanvas;
            public Canvas HealthCanvas;
            public GameObject FlexBackground;
            public GameObject UiBlur;
            public bool CreatedMainCamera;
            public bool CreatedEventSystem;
            public bool ReplacedExistingSetup;
        }

        #endregion

        #region Foldable

        /// <summary>
        /// Core builder used by menu items. Safe to call from other Editor tools.
        /// </summary>
        public static SetupResult BuildSetup(
            bool createNewScene,
            bool reuseExistingCamera,
            bool includeSkyBoxCamera,
            bool includeHealthCanvas,
            bool includeUiBlur,
            bool includeSimulator,
            FoldableDeviceKind deviceKind)
        {
            var result = new SetupResult();

            // Replace a previous auto-setup root so empty scenes stay clean.
            var existing = GameObject.Find(RootName);
            if (existing != null)
            {
                Undo.DestroyObjectImmediate(existing);
                result.ReplacedExistingSetup = true;
            }

            var root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Create Foldable Setup");
            result.Root = root;

            result.MainCamera = ResolveMainCamera(reuseExistingCamera, out result.CreatedMainCamera);
            if (includeSkyBoxCamera)
                result.SkyBoxCamera = CreateSkyBoxCamera(result.MainCamera);

            EnsureEventSystem(out result.CreatedEventSystem);

            var uiRoot = CreateChild(root.transform, UiRootName);
            result.ControlsCanvas = CreateControlsCanvas(uiRoot.transform);
            result.FlexBackground = CreateFlexBackground(result.ControlsCanvas.transform);

            if (includeHealthCanvas)
                result.HealthCanvas = CreateHealthCanvas(uiRoot.transform);

            if (includeUiBlur)
                result.UiBlur = CreateUiBlur(result.ControlsCanvas.transform);

            var detector = Undo.AddComponent<FoldablePostureDetector>(root);
            result.Detector = detector;
            detector.DeviceKind = deviceKind;

            if (includeSimulator)
                result.Simulator = Undo.AddComponent<FoldableSimulator>(root);

            if (result.FlexBackground != null)
                result.FlexBackground.SetActive(false);

            EditorUtility.SetDirty(detector);
            Debug.Log("[Fold & Flip] Setup created:\n" + BuildSummary(result));
            return result;
        }

        #endregion

        #region Helpers

        static Camera ResolveMainCamera(bool reuseExisting, out bool created)
        {
            created = false;
            if (reuseExisting)
            {
                var tagged = Camera.main;
                if (tagged != null)
                    return tagged;

                var any = Object.FindFirstObjectByType<Camera>(FindObjectsInactive.Include);
                if (any != null)
                    return any;
            }

            created = true;
            var go = new GameObject("Main Camera");
            Undo.RegisterCreatedObjectUndo(go, "Create Main Camera");
            go.tag = "MainCamera";
            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.fieldOfView = 60f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 1000f;
            go.AddComponent<AudioListener>();
            go.transform.position = new Vector3(0f, 1f, -10f);
            return cam;
        }

        static Camera CreateSkyBoxCamera(Camera main)
        {
            var go = new GameObject("SkyBox Camera");
            Undo.RegisterCreatedObjectUndo(go, "Create SkyBox Camera");
            if (main != null)
            {
                go.transform.SetParent(main.transform, false);
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
            }

            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.cullingMask = 0;
            cam.depth = main != null ? main.depth - 1f : -1f;
            cam.fieldOfView = main != null ? main.fieldOfView : 60f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 1000f;
            cam.allowHDR = false;
            cam.allowMSAA = false;
            return cam;
        }

        static void EnsureEventSystem(out bool created)
        {
            created = false;
            var existing = Object.FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include);
            if (existing != null)
                return;

            created = true;
            var go = new GameObject("EventSystem");
            Undo.RegisterCreatedObjectUndo(go, "Create EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<StandaloneInputModule>();
        }

        static Canvas CreateControlsCanvas(Transform parent)
        {
            var go = CreateChild(parent, "ControlsCanvas");
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 2316f);
            scaler.matchWidthOrHeight = 0.5f;

            go.AddComponent<GraphicRaycaster>();

            // Placeholder touch pads so Flex Mode lower pane is visible immediately.
            CreateHintLabel(go.transform, "Controls (Flex lower pane)", new Vector2(0.5f, 0.15f));
            CreatePad(go.transform, "LeftStick", new Vector2(0.18f, 0.22f), new Color(1f, 1f, 1f, 0.25f));
            CreatePad(go.transform, "RightStick", new Vector2(0.82f, 0.22f), new Color(1f, 1f, 1f, 0.25f));
            return canvas;
        }

        static Canvas CreateHealthCanvas(Transform parent)
        {
            var go = CreateChild(parent, "HealthCanvas");
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 2316f);

            go.AddComponent<GraphicRaycaster>();

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(24f, -24f);
            rt.sizeDelta = new Vector2(280f, 64f);

            CreateHintLabel(go.transform, "Health", new Vector2(0.5f, 0.5f));
            return canvas;
        }

        static GameObject CreateFlexBackground(Transform controlsCanvas)
        {
            var go = CreateChild(controlsCanvas, "FlexBG");
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = new Vector2(1f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var image = go.AddComponent<Image>();
            image.color = new Color(0.22f, 0.22f, 0.28f, 1f);
            image.raycastTarget = false;

            // Behind controls, in front of game (overlay canvas).
            go.transform.SetAsFirstSibling();
            go.SetActive(false);
            return go;
        }

        static GameObject CreateUiBlur(Transform controlsCanvas)
        {
            var go = CreateChild(controlsCanvas, "UIBlur");
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var image = go.AddComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.35f);
            image.raycastTarget = false;
            go.transform.SetAsFirstSibling();
            return go;
        }

        static void CreatePad(Transform parent, string name, Vector2 anchor, Color color)
        {
            var go = CreateChild(parent, name);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(160f, 160f);
            rt.anchoredPosition = Vector2.zero;

            var image = go.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
        }

        static void CreateHintLabel(Transform parent, string text, Vector2 anchor)
        {
            var go = CreateChild(parent, "Label");
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(420f, 40f);

            var label = go.AddComponent<Text>();
            label.text = text;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = new Color(1f, 1f, 1f, 0.85f);
            label.fontSize = 22;
            label.raycastTarget = false;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (label.font == null)
                label.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        static GameObject CreateChild(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
            go.transform.SetParent(parent, false);
            return go;
        }

        static string BuildSummary(SetupResult result)
        {
            return
                $"Root: {RootName}\n" +
                $"FoldablePostureDetector: {(result.Detector != null ? result.Detector.DeviceKind.ToString() : "none")}\n" +
                $"Main Camera: {(result.CreatedMainCamera ? "created" : "reused")} ({result.MainCamera.name})\n" +
                $"SkyBox Camera: {(result.SkyBoxCamera != null ? result.SkyBoxCamera.name : "none")}\n" +
                $"ControlsCanvas + FlexBG + placeholders (sample UI only — no hinge apply)\n" +
                $"HealthCanvas: {(result.HealthCanvas != null ? "yes" : "no")}\n" +
                $"UIBlur: {(result.UiBlur != null ? "yes" : "no")}\n" +
                $"FoldableSimulator: {(result.Simulator != null ? "yes" : "no")}\n" +
                $"EventSystem: {(result.CreatedEventSystem ? "created" : "reused")}\n" +
                (result.ReplacedExistingSetup ? "Replaced previous FoldableSetup\n" : "") +
                "\nNext: Device Simulator → ★ Fold / ★ Flip → Half Opened → Play.";
        }

        #endregion
    }
}
