using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using FDPlus.ResponsiveUI;
using FDPlus.ResponsiveUI.Shared.Editor;

namespace FDPlus.ResponsiveUI.Editor
{
    /// <summary>
    /// Builds a disabled sample Canvas + <see cref="ResponsiveUI"/> in the open scene.
    /// </summary>
    public static class ResponsiveUiSampleSetup
    {
        #region Constants

        const string RootName = "ResponsiveUI Sample";

        #endregion

        #region Editor

        [MenuItem(ResponsiveUiEditorMenus.CreateSampleCanvas, false, ResponsiveUiEditorMenus.PriorityScene + 2)]
        public static void CreateSampleCanvas()
        {
            var existing = GameObject.Find(RootName);
            if (existing != null)
                Undo.DestroyObjectImmediate(existing);

            EnsureEventSystem();

            var canvasGo = new GameObject(RootName, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(canvasGo, "Create Responsive UI Sample");

            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 0;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ResponsiveRectState.BuiltinPhoneDesignCanvasSize;
            scaler.matchWidthOrHeight = 0.5f;

            canvasGo.AddComponent<GraphicRaycaster>();

            var bgGo = CreateStretchChild(canvasGo.transform, "Background");
            var bgImage = bgGo.AddComponent<Image>();
            bgImage.color = new Color(0.12f, 0.14f, 0.18f, 1f);

            var widgetGo = CreateStretchChild(canvasGo.transform, "Widget");
            var widgetRt = widgetGo.GetComponent<RectTransform>();
            widgetRt.anchorMin = new Vector2(0.5f, 0.5f);
            widgetRt.anchorMax = new Vector2(0.5f, 0.5f);
            widgetRt.pivot = new Vector2(0.5f, 0.5f);
            widgetRt.sizeDelta = new Vector2(400f, 120f);
            widgetRt.anchoredPosition = Vector2.zero;

            var ui = Undo.AddComponent<ResponsiveUI>(canvasGo);
            ui.enabled = false;

            var controller = Undo.AddComponent<ResponsiveUiController>(canvasGo);
            controller.AssignHub(ui);

            var so = new SerializedObject(controller);
            SerializedProperty backgrounds = so.FindProperty("backgrounds");
            SerializedProperty widgets = so.FindProperty("widgets");
            if (backgrounds == null || !backgrounds.isArray ||
                widgets == null || !widgets.isArray)
            {
                Debug.LogError(
                    "[Responsive UI] Sample Canvas could not bind backgrounds/widgets.");
            }
            else
            {
                backgrounds.arraySize = 1;
                backgrounds.GetArrayElementAtIndex(0).FindPropertyRelative("image")
                    .objectReferenceValue = bgImage;

                widgets.arraySize = 1;
                widgets.GetArrayElementAtIndex(0).FindPropertyRelative("target")
                    .objectReferenceValue = widgetRt;

                so.ApplyModifiedProperties();
            }

            Selection.activeGameObject = canvasGo;
            EditorGUIUtility.PingObject(canvasGo);
            EditorSceneManager.MarkSceneDirty(canvasGo.scene);

            EditorUtility.DisplayDialog(
                "Responsive UI",
                "Created a sample Canvas with ResponsiveUI hub + ResponsiveUiController (hub disabled).\n\n" +
                "1. Assign a sprite to Background if you want cover-fit art.\n" +
                "2. Author " + ResponsiveUiEditorSettings.FormDisplayNameList + " slots on Widget.\n" +
                "3. Enable the ResponsiveUI hub.\n\n" +
                "Fold hinge splits: Responsive UI → Scene → Create Fold & Flip Setup.",
                "OK");
        }

        #endregion

        #region Helpers

        static GameObject CreateStretchChild(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, "Create Responsive UI Sample");
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            return go;
        }

        static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include) != null)
                return;

            var go = new GameObject("EventSystem");
            Undo.RegisterCreatedObjectUndo(go, "Create EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<StandaloneInputModule>();
        }

        #endregion
    }
}
