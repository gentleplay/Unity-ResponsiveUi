#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

using FDPlus.ResponsiveUI;

namespace FDPlus.ResponsiveUI.Editor
{
    /// <summary>
    /// Background row: Image + Match Mode. Popup order is UX (fill → contain → stretch →
    /// one-axis fit → one-axis cover → manual). Enum ints stay stable for YAML.
    /// </summary>
    [CustomPropertyDrawer(typeof(ResponsiveBackground))]
    public class ResponsiveBackgroundDrawer : PropertyDrawer
    {
        #region Constants

        static readonly GUIContent ImageLabel = new GUIContent(
            "Image",
            "UI Image to cover-fit. Sprite pixels drive aspect for Cover / Fit / Fit Width / Fit Height; " +
            "without a sprite, Project Settings → Responsive UI → Background Cover Size is used.");

        static readonly GUIContent ModeLabel = new GUIContent(
            "Match Mode",
            "How this Image fills its parent.\n" +
            "• Cover / Fit / Stretch — both axes\n" +
            "• Fit Width / Fit Height — match one axis (other may overflow)\n" +
            "• Width Only / Height Only — cover one axis; full-stretch clears the other sizeDelta\n" +
            "• Manual — leave authored sizeDelta");

        // Display order (not enum order): common fills first, Manual last.
        static readonly GUIContent[] ModeLabels =
        {
            new GUIContent(
                "Cover",
                "Fill the parent; keep aspect (may crop). Best default for full-bleed art."),
            new GUIContent(
                "Fit",
                "Fit inside the parent; keep aspect (may letterbox). Use when the whole image must stay visible."),
            new GUIContent(
                "Stretch",
                "Stretch both axes to the parent; ignore aspect. Use for solid/nine-slice panels."),
            new GUIContent(
                "Fit Width",
                "Match parent width; keep aspect (height may overflow). Good for wide banners."),
            new GUIContent(
                "Fit Height",
                "Match parent height; keep aspect (width may overflow). Good for tall side art."),
            new GUIContent(
                "Width Only",
                "Cover width like Cover; keep sizeDelta.y unless height is full-stretch (then clear Y)."),
            new GUIContent(
                "Height Only",
                "Cover height like Cover; keep sizeDelta.x unless width is full-stretch (then clear X)."),
            new GUIContent(
                "Manual",
                "Do not resize on Apply — keep the authored sizeDelta."),
        };

        static readonly int[] ModeValues =
        {
            (int)ResponsiveBgCoverMode.Cover,
            (int)ResponsiveBgCoverMode.Fit,
            (int)ResponsiveBgCoverMode.Stretch,
            (int)ResponsiveBgCoverMode.FitWidth,
            (int)ResponsiveBgCoverMode.FitHeight,
            (int)ResponsiveBgCoverMode.WidthOnly,
            (int)ResponsiveBgCoverMode.HeightOnly,
            (int)ResponsiveBgCoverMode.Manual,
        };

        #endregion

        #region Inspector

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float line = EditorGUIUtility.singleLineHeight;
            float s = EditorGUIUtility.standardVerticalSpacing;
            return line + s + line;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            float line = EditorGUIUtility.singleLineHeight;
            float s = EditorGUIUtility.standardVerticalSpacing;
            SerializedProperty image = property.FindPropertyRelative("image");
            SerializedProperty mode = property.FindPropertyRelative("coverMode");
            if (image == null || mode == null)
            {
                EditorGUI.HelpBox(
                    position,
                    "ResponsiveBackground serialization is missing image or coverMode.",
                    MessageType.Error);
                EditorGUI.EndProperty();
                return;
            }

            Rect row = new Rect(position.x, position.y, position.width, line);
            EditorGUI.BeginChangeCheck();
            EditorGUI.PropertyField(row, image, ImageLabel);
            bool changed = EditorGUI.EndChangeCheck();

            row.y += line + s;
            EditorGUI.BeginChangeCheck();
            EditorGUI.IntPopup(row, mode, ModeLabels, ModeValues, ModeLabel);
            changed |= EditorGUI.EndChangeCheck();

            EditorGUI.EndProperty();

            if (!changed || !ResponsiveUiDetectionDrawer.CanMutateInspectorLayout())
                return;

            // Commit before live Apply — parent editor ChangeCheck can miss drawer-only edits.
            property.serializedObject.ApplyModifiedProperties();
            if (property.serializedObject.targetObject is ResponsiveUiController controller)
                controller.EditorApplyBackgrounds();
        }

        #endregion

        #region Seed

        /// <summary>
        /// Unity list <c>+</c> duplicates the previous row. Clear Image and restore Cover.
        /// </summary>
        public static void ResetNewListRow(SerializedProperty row)
        {
            row.FindPropertyRelative("image").objectReferenceValue = null;
            row.FindPropertyRelative("coverMode").intValue =
                (int)ResponsiveBgCoverMode.Cover;
        }

        #endregion
    }
}

#endif
