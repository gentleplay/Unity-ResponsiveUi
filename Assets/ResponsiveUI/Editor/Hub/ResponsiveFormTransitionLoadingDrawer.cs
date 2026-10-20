#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

using FDPlus.ResponsiveUI;

namespace FDPlus.ResponsiveUI.Editor
{
    /// <summary>
    /// Hub Layout: enable → black overlay → loading controller → show-on slots → min visible.
    /// </summary>
    [CustomPropertyDrawer(typeof(ResponsiveFormTransitionLoading))]
    public class ResponsiveFormTransitionLoadingDrawer : PropertyDrawer
    {
        const float HelpBoxHeight = 52f;

        static readonly GUIContent EnabledLabel = new GUIContent(
            "Form Transition Loading",
            "Play Mode only. Shows a black overlay (and optional loading panel) while Fold/Flip " +
            "(and other) form switches Apply into a checked slot. Off = feature unused.");

        static readonly GUIContent BlackOverlayLabel = new GUIContent(
            "Black Overlay",
            "Full-screen Image. Play Mode drives CanvasGroup alpha 1→0 around form-switch Apply " +
            "(auto-adds CanvasGroup, ignoreParentGroups). Stretch to the Canvas. Optional if a " +
            "Loading Controller is assigned (need at least one).");

        static readonly GUIContent ControllerLabel = new GUIContent(
            "Loading Controller",
            "Optional ResponsiveUiController (cover + text) drawn on top of the black overlay. " +
            "Play Mode keeps it active and CanvasGroup-hidden between form switches " +
            "(auto-adds CanvasGroup). Edit Mode does not force-hide — activate the GameObject " +
            "to see and tune scale. Late Canvas sibling; outside other Active Objects.");

        static readonly GUIContent MinVisibleLabel = new GUIContent(
            "Min Visible (s)",
            "How long the cover stays up after Apply finishes (realtime). Default 0.25s.");

        static readonly GUIContent ShowOnSlotsLabel = new GUIContent(
            "Show On Slots",
            "Cover shows when a form switch lands on a checked slot (same grid as Active Objects). " +
            "Default: all on except Flip Cover.");

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float line = EditorGUIUtility.singleLineHeight;
            float s = EditorGUIUtility.standardVerticalSpacing;
            SerializedProperty enabled = property.FindPropertyRelative("enabled");
            if (enabled == null || !enabled.boolValue)
                return line;

            SerializedProperty blackOverlay = property.FindPropertyRelative("blackOverlay");
            SerializedProperty controller = property.FindPropertyRelative("controller");
            bool missingCover =
                (blackOverlay == null || blackOverlay.objectReferenceValue == null) &&
                (controller == null || controller.objectReferenceValue == null);

            // Enabled + black + controller + min + show-on label + slot rows + help
            float body = line + s + line + s + line + s + line + s + line;
            body += ResponsiveActiveObjectSlotsEditorUtil.FormRowsHeight(property);
            body += s + HelpBoxHeight;
            if (missingCover)
                body += s + HelpBoxHeight;
            return body;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            SerializedProperty enabled = property.FindPropertyRelative("enabled");
            SerializedProperty blackOverlay = property.FindPropertyRelative("blackOverlay");
            SerializedProperty controller = property.FindPropertyRelative("controller");
            SerializedProperty minVisible = property.FindPropertyRelative("minVisibleSeconds");
            SerializedProperty showOnSlots = property.FindPropertyRelative("showOnSlots");
            if (enabled == null ||
                blackOverlay == null ||
                controller == null ||
                minVisible == null ||
                showOnSlots == null)
            {
                EditorGUI.HelpBox(
                    position,
                    "ResponsiveFormTransitionLoading serialization is incomplete.",
                    MessageType.Error);
                return;
            }

            EditorGUI.BeginProperty(position, label, property);

            float line = EditorGUIUtility.singleLineHeight;
            float s = EditorGUIUtility.standardVerticalSpacing;
            Rect row = new Rect(position.x, position.y, position.width, line);

            EditorGUI.BeginChangeCheck();
            EditorGUI.PropertyField(row, enabled, EnabledLabel);
            bool enabledChanged = EditorGUI.EndChangeCheck();

            if (enabled.boolValue)
            {
                row.y += line + s;
                EditorGUI.BeginChangeCheck();
                EditorGUI.PropertyField(row, blackOverlay, BlackOverlayLabel);
                bool blackChanged = EditorGUI.EndChangeCheck();

                row.y += line + s;
                EditorGUI.BeginChangeCheck();
                EditorGUI.PropertyField(row, controller, ControllerLabel);
                bool controllerChanged = EditorGUI.EndChangeCheck();

                row.y += line + s;
                EditorGUI.Slider(row, minVisible, 0f, 2f, MinVisibleLabel);

                row.y += line + s;
                EditorGUI.LabelField(row, ShowOnSlotsLabel, EditorStyles.boldLabel);

                ResponsiveActiveObjectSlotsEditorUtil.DrawFormRows(
                    row, row.y + line, property, showOnSlots);
                row.y += ResponsiveActiveObjectSlotsEditorUtil.FormRowsHeight(property);

                row.y += line + s;
                row.height = HelpBoxHeight;
                EditorGUI.HelpBox(
                    row,
                    "Play Mode · form changes into a checked slot only. Black overlay covers " +
                    "Apply; loading controller (optional) on top. Hide after end-of-frame + min " +
                    "visible.",
                    MessageType.Info);

                bool missingCover =
                    blackOverlay.objectReferenceValue == null &&
                    controller.objectReferenceValue == null;
                if (missingCover)
                {
                    row.y += HelpBoxHeight + s;
                    row.height = HelpBoxHeight;
                    EditorGUI.HelpBox(
                        row,
                        "Assign a Black Overlay Image and/or a Loading Controller.",
                        MessageType.Warning);
                }
                else if (blackChanged || controllerChanged || enabledChanged)
                {
                    property.serializedObject.ApplyModifiedProperties();
                    PrepareAssignedCover(
                        controller.objectReferenceValue as ResponsiveUiController,
                        "Form Transition Loading CanvasGroup");
                    PrepareAssignedCover(
                        blackOverlay.objectReferenceValue as Image,
                        "Form Transition Black Overlay CanvasGroup");
                }
            }

            if (enabledChanged && !enabled.boolValue && Application.isPlaying)
            {
                property.serializedObject.ApplyModifiedProperties();
                if (property.serializedObject.targetObject is ResponsiveUI hub)
                    hub.FormTransitionLoading.Enabled = false;
            }
            else if (enabledChanged && enabled.boolValue && Application.isPlaying)
            {
                property.serializedObject.ApplyModifiedProperties();
                if (property.serializedObject.targetObject is ResponsiveUI hub)
                    hub.FormTransitionLoading.EnsureReady();
            }

            EditorGUI.EndProperty();
        }

        /// <summary>
        /// Black Overlay / Loading Controller root: ensure a CanvasGroup that ignores parent
        /// groups. Edit Mode leaves it visible for authoring; Play Mode starts it hidden.
        /// </summary>
        static void PrepareAssignedCover(Component cover, string undoName)
        {
            if (cover == null)
                return;

            CanvasGroup group = cover.GetComponent<CanvasGroup>();
            if (group == null)
                group = Undo.AddComponent<CanvasGroup>(cover.gameObject);

            Undo.RecordObject(group, undoName);
            group.ignoreParentGroups = true;
            group.alpha = Application.isPlaying ? 0f : 1f;
            group.blocksRaycasts = false;
            group.interactable = false;
            EditorUtility.SetDirty(group);
        }
    }
}

#endif
