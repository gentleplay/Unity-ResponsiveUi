#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using FDPlus.ResponsiveUI.Shared.Editor;

namespace FDPlus.ResponsiveUI.Editor
{
    /// <summary>
    /// Draws one <see cref="ResponsiveTwoSideWidget"/> row: Name / Target, tween settings,
    /// and the Normal (plus Animation Start) layout for the controller's current Side.
    /// </summary>
    [CustomPropertyDrawer(typeof(ResponsiveTwoSideWidget))]
    public sealed class ResponsiveTwoSideWidgetDrawer : PropertyDrawer
    {
        #region Inspector

        public override float GetPropertyHeight(
            SerializedProperty property,
            GUIContent label)
        {
            float line = EditorGUIUtility.singleLineHeight;
            if (!property.isExpanded)
                return line;

            float spacing = EditorGUIUtility.standardVerticalSpacing;
            float height = line + spacing;
            height += ResponsiveUiInspectorFoldouts.SlotSectionHeight(
                property, "TwoSideWidget", "Widget", true, WidgetFieldsHeight(property));
            height += ResponsiveUiInspectorFoldouts.SlotSectionHeight(
                property, "TwoSideWidget", "Tween", true, TweenFieldsHeight(property));
            height += ResponsiveWidgetLayoutDrawer
                .GetPropertyHeightWithoutTarget(NormalLayout(property));
            if (property.FindPropertyRelative("animationEnabled").boolValue)
            {
                height += spacing;
                height += ResponsiveWidgetLayoutDrawer
                    .GetPropertyHeightWithoutTarget(
                        AnimationStartLayout(property));
            }
            return height;
        }

        public override void OnGUI(
            Rect position,
            SerializedProperty property,
            GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            float line = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;
            SerializedProperty name = property.FindPropertyRelative("name");
            string title = string.IsNullOrWhiteSpace(name.stringValue)
                ? label.text
                : name.stringValue;
            ResponsiveUiInspectorFoldouts.DrawPropertyFoldout(
                position,
                property,
                new GUIContent(title),
                content =>
                {
                    float y = position.y + line + spacing;
                    y = ResponsiveUiInspectorFoldouts.DrawSlotSection(
                        content,
                        y,
                        WidgetFieldsHeight(property),
                        property,
                        "TwoSideWidget",
                        "Widget",
                        "Widget",
                        WidgetSummary(property),
                        ResponsiveUiInspectorFoldouts.TintTeal,
                        true,
                        body =>
                        {
                            float by = body.y;
                            DrawField(
                                body,
                                ref by,
                                name,
                                new GUIContent("Name"),
                                last: false);
                            DrawTarget(body, ref by, property, last: true);
                        });
                    y = ResponsiveUiInspectorFoldouts.DrawSlotSection(
                        content,
                        y,
                        TweenFieldsHeight(property),
                        property,
                        "TwoSideWidget",
                        "Tween",
                        "Tween",
                        TweenSummary(property),
                        ResponsiveUiInspectorFoldouts.TintAmber,
                        true,
                        body => DrawTweenFields(body, property));

                    DrawLayout(
                        content,
                        ref y,
                        NormalLayout(property),
                        new GUIContent("Normal Layout"));
                    if (property.FindPropertyRelative("animationEnabled").boolValue)
                    {
                        DrawLayout(
                            content,
                            ref y,
                            AnimationStartLayout(property),
                            new GUIContent("Animation Start Layout"));
                    }
                });
            EditorGUI.EndProperty();
        }

        #endregion

        #region Helpers

        static void DrawLayout(
            Rect position,
            ref float y,
            SerializedProperty layout,
            GUIContent label)
        {
            float height = ResponsiveWidgetLayoutDrawer
                .GetPropertyHeightWithoutTarget(layout);
            ResponsiveWidgetLayoutDrawer.OnGUIWithoutTarget(
                new Rect(
                    position.x,
                    y,
                    position.width,
                    height),
                layout,
                label);
            y += height + EditorGUIUtility.standardVerticalSpacing;
        }

        static float WidgetFieldsHeight(SerializedProperty property)
        {
            float spacing = EditorGUIUtility.standardVerticalSpacing;
            return PropertyHeight(property, "name")
                + spacing
                + EditorGUI.GetPropertyHeight(
                    NormalLayout(property).FindPropertyRelative("target"),
                    true);
        }

        static float TweenFieldsHeight(SerializedProperty property)
        {
            float spacing = EditorGUIUtility.standardVerticalSpacing;
            float h = PropertyHeight(property, "animationEnabled");
            if (!property.FindPropertyRelative("animationEnabled").boolValue)
                return h;

            h += spacing + PropertyHeight(property, "duration");
            h += spacing + PropertyHeight(property, "delay");
            h += spacing + PropertyHeight(property, "curve");
            return h;
        }

        static string WidgetSummary(SerializedProperty property)
        {
            string name = property.FindPropertyRelative("name").stringValue;
            return string.IsNullOrWhiteSpace(name) ? "No name" : name;
        }

        static string TweenSummary(SerializedProperty property)
        {
            if (!property.FindPropertyRelative("animationEnabled").boolValue)
                return "Off";

            float duration = property.FindPropertyRelative("duration").floatValue;
            return $"{duration:0.##}s";
        }

        static void DrawTweenFields(Rect body, SerializedProperty property)
        {
            float y = body.y;
            SerializedProperty animationEnabled =
                property.FindPropertyRelative("animationEnabled");
            bool last = !animationEnabled.boolValue;
            DrawField(
                body,
                ref y,
                animationEnabled,
                new GUIContent("Animation Enabled"),
                last);
            if (last)
                return;

            DrawField(
                body,
                ref y,
                property.FindPropertyRelative("duration"),
                new GUIContent("Duration"),
                last: false);
            DrawField(
                body,
                ref y,
                property.FindPropertyRelative("delay"),
                new GUIContent("Delay"),
                last: false);
            DrawField(
                body,
                ref y,
                property.FindPropertyRelative("curve"),
                new GUIContent("Animation Curve"),
                last: true);
        }

        static void DrawTarget(
            Rect area,
            ref float y,
            SerializedProperty widget,
            bool last)
        {
            SerializedProperty target = NormalLayout(widget)
                .FindPropertyRelative("target");
            Object previousTarget = target.objectReferenceValue;
            int previousTargetId = target.objectReferenceInstanceIDValue;
            float height = EditorGUI.GetPropertyHeight(target, true);
            EditorGUI.BeginChangeCheck();
            EditorGUI.PropertyField(
                new Rect(area.x, y, area.width, height),
                target,
                new GUIContent("Target"),
                true);
            if (EditorGUI.EndChangeCheck())
            {
                Object nextTarget = target.objectReferenceValue;
                SyncTargets(widget, nextTarget);
                ApplyTargetChange(
                    widget,
                    previousTarget,
                    previousTargetId);
            }
            y += height;
            if (!last)
                y += EditorGUIUtility.standardVerticalSpacing;
        }

        static void DrawField(
            Rect area,
            ref float y,
            SerializedProperty property,
            GUIContent label,
            bool last)
        {
            float height = EditorGUI.GetPropertyHeight(property, true);
            EditorGUI.PropertyField(
                new Rect(area.x, y, area.width, height),
                property,
                label,
                true);
            y += height;
            if (!last)
                y += EditorGUIUtility.standardVerticalSpacing;
        }

        static float PropertyHeight(
            SerializedProperty property,
            string relativeName) =>
            EditorGUI.GetPropertyHeight(
                property.FindPropertyRelative(relativeName),
                true);

        static SerializedProperty NormalLayout(SerializedProperty property) =>
            property.FindPropertyRelative(
                ResponsiveRectStateEditorUtil.TwoSideLayoutPropertyName(
                    Side(property), animationStart: false));

        static SerializedProperty AnimationStartLayout(
            SerializedProperty property) =>
            property.FindPropertyRelative(
                ResponsiveRectStateEditorUtil.TwoSideLayoutPropertyName(
                    Side(property), animationStart: true));

        static void SyncTargets(
            SerializedProperty property,
            Object target)
        {
            string[] layouts = ResponsiveTwoSideWidget.LayoutPropertyNames;
            for (int i = 0; i < layouts.Length; i++)
            {
                property
                    .FindPropertyRelative(layouts[i])
                    .FindPropertyRelative("target")
                    .objectReferenceValue = target;
            }
        }

        static void ApplyTargetChange(
            SerializedProperty widget,
            Object previousTarget,
            int previousTargetId)
        {
            ResponsiveUI hub = ResponsiveUiDetectionDrawer.HandlerOf(
                widget.serializedObject) as ResponsiveUI;
            string[] layouts = ResponsiveTwoSideWidget.LayoutPropertyNames;
            Object nextTarget = widget
                .FindPropertyRelative(layouts[0])
                .FindPropertyRelative("target")
                .objectReferenceValue;
            bool cleared = ResponsiveUiObjectReference.WasClearedToNone(
                previousTargetId,
                nextTarget);
            bool firstAssign = !cleared &&
                ResponsiveUiObjectReference.IsFirstAssign(previousTarget);
            if (!firstAssign && !cleared)
            {
                widget.serializedObject.ApplyModifiedProperties();
                return;
            }

            for (int i = 0; i < layouts.Length; i++)
            {
                SerializedProperty layout =
                    widget.FindPropertyRelative(layouts[i]);
                if (firstAssign)
                    ResponsiveRectStateEditorUtil.SeedSlots(layout);
                ResponsiveRectStateEditorUtil.ApplyInspectorTargetChange(
                    layout,
                    previousTarget,
                    previousTargetId,
                    hub);
            }
            widget.serializedObject.ApplyModifiedProperties();
        }

        static ResponsiveUiSide Side(SerializedProperty property)
        {
            SerializedProperty side =
                property.serializedObject.FindProperty("side");
            return (ResponsiveUiSide)side.intValue;
        }

        #endregion
    }
}
#endif
