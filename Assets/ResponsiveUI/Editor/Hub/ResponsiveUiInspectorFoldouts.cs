#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

using FDPlus.ResponsiveUI;

namespace FDPlus.ResponsiveUI.Editor
{
    /// <summary>
    /// Shared Inspector section chrome. GUILayout Inspectors use
    /// <see cref="ColoredFoldoutSection"/> (Foldout, not FoldoutHeaderGroup).
    /// Property drawers use <see cref="DrawSlotSection"/>. Session keys include
    /// the component instance id so two Inspectors do not share foldouts.
    /// </summary>
    public static class ResponsiveUiInspectorFoldouts
    {
        #region Constants

        public const float FoldoutArrowWidth = 18f;
        public const float FieldLabelWidth = 156f;
        public const float FieldValueMinWidth = 96f;
        public const float SlotSectionPad = 4f;
        public const float SlotSectionAccentWidth = 3f;
        public const float HeaderFoldoutPad = 16f;
        public const float HeaderExtraHeight = 4f;

        public static readonly Color TintBlue = new Color(0.30f, 0.62f, 0.92f);
        public static readonly Color TintTeal = new Color(0.22f, 0.72f, 0.58f);
        public static readonly Color TintGreen = new Color(0.48f, 0.72f, 0.28f);
        public static readonly Color TintAmber = new Color(0.92f, 0.62f, 0.22f);
        public static readonly Color TintPurple = new Color(0.70f, 0.45f, 0.88f);
        public static readonly Color TintSlate = new Color(0.45f, 0.52f, 0.62f);

        static readonly GUIContent HubFieldLabel = new GUIContent(
            "Hub",
            "ResponsiveUI on the parent Canvas. Auto-assigns when you add this component.");

        static GUIStyle s_BoldFoldout;

        #endregion

        #region Inspector

        public static void Property(SerializedObject so, string propertyName)
        {
            SerializedProperty prop = so.FindProperty(propertyName);
            if (prop != null)
                EditorGUILayout.PropertyField(prop, true);
        }

        /// <summary>
        /// Tinted collapsible block for GUILayout Inspectors. Uses Foldout, not
        /// FoldoutHeaderGroup, so Unity 6 lists stay safe. The header is always
        /// drawn at the Inspector content X so boxed and unboxed section arrows
        /// line up. <paramref name="boxed"/> wraps only the open body.
        /// </summary>
        public static void ColoredFoldoutSection(
            string sessionKey,
            string title,
            string summary,
            Color tint,
            Action draw,
            bool defaultOpen = false,
            bool boxed = false)
        {
            bool open = SlotSectionOpen(sessionKey, defaultOpen);
            bool tinted = tint.a > 0.01f;

            float line = EditorGUIUtility.singleLineHeight;
            Rect header = EditorGUILayout.GetControlRect(false, line + HeaderExtraHeight);
            if (tinted)
                DrawTintBar(header, tint);
            bool next = DrawHeaderFoldout(HeaderFoldoutRect(header, tinted), open, title, summary);
            if (next != open)
                SessionState.SetBool(sessionKey, next);

            if (next && draw != null)
            {
                float oldLabel = EditorGUIUtility.labelWidth;
                AtZeroIndent(() =>
                {
                    EditorGUIUtility.labelWidth = FieldLabelWidth;
                    if (boxed)
                        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    DrawNestedGui(draw, HeaderContentInset(tinted));
                    if (boxed)
                        EditorGUILayout.EndVertical();
                });
                EditorGUIUtility.labelWidth = oldLabel;
            }
        }

        /// <summary>
        /// Boxed parent panel for a list and its related tools. The header uses the
        /// same X as unboxed sections; only the open body is boxed so children stay
        /// attached to this list instead of the preceding Inspector section.
        /// </summary>
        public static void BoxedFoldoutSection(
            string sessionKey,
            string title,
            string summary,
            Color tint,
            Action draw,
            bool defaultOpen = true) =>
            ColoredFoldoutSection(
                sessionKey, title, summary, tint, draw, defaultOpen, boxed: true);

        public static string InspectorSectionKey(
            UnityEngine.Object target,
            string editor,
            string section)
        {
            int id = target != null ? target.GetInstanceID() : 0;
            return $"RUI.{editor}.{id}.{section}";
        }

        public static string PropertySectionKey(
            string kind,
            SerializedProperty property,
            string section)
        {
            int id = property.serializedObject.targetObject != null
                ? property.serializedObject.targetObject.GetInstanceID()
                : 0;
            return $"RUI.{kind}.{id}.{property.propertyPath}.{section}";
        }

        public static void EnsureHubField(SerializedObject so, Component from)
        {
            SerializedProperty prop = so.FindProperty("responsiveUi");
            if (prop == null || prop.objectReferenceValue != null)
                return;
            ResponsiveUI found = ResponsiveUiHub.Resolve(null, from);
            if (found == null)
                return;
            prop.objectReferenceValue = found;
            so.ApplyModifiedProperties();
            so.Update();
        }

        public static void DrawHubField(SerializedObject so) =>
            DrawRootProperty(so.FindProperty("responsiveUi"), HubFieldLabel);

        /// <summary>
        /// Design Scale authoring hints shared by the widget and two-side Inspectors.
        /// Reads the hub directly instead of building a SerializedObject every repaint.
        /// </summary>
        public static void DrawDesignScaleHints(ResponsiveUI hub, bool anyDesignScale)
        {
            if (!anyDesignScale)
                return;

            EditorGUILayout.HelpBox(
                "Design Scale authoring: store design-space anchoredPosition/sizeDelta and Default Scale (origin). " +
                "At Apply: localScale = origin × canvas ratio, position × ratio; stretched sizeDelta compensates the parent span. " +
                "Inspector numbers will not match the Target RectTransform when the ratio is not 1. " +
                "When Design Scale is off, Apply restores localScale to Default Scale.",
                MessageType.Info);

            if (hub == null || !hub.UsesCanvasScalerResolutionFit)
                return;

            EditorGUILayout.HelpBox(
                "Scale With Screen Size already fits the canvas. Design Scale still applies Width / Height / Uniform / IndependentXY as a residual ratio in that canvas's local rect, so slot modes keep working without double-counting the scaler.",
                MessageType.Info);
        }

        public static void DrawRootProperty(SerializedProperty property, GUIContent label)
        {
            if (property == null)
                return;

            float oldLabel = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = FieldLabelWidth;
            EditorGUILayout.PropertyField(property, label, true);
            EditorGUIUtility.labelWidth = oldLabel;
        }

        public static bool DrawGuiFoldout(string title, bool expanded, Action draw)
        {
            Rect row = EditorGUILayout.GetControlRect(
                false,
                EditorGUIUtility.singleLineHeight);
            bool next = DrawFoldout(row, expanded, new GUIContent(title), true);
            if (next)
                DrawNestedGui(draw);
            return next;
        }

        public static void DrawNestedGui(Action draw) =>
            DrawNestedGui(draw, FoldoutArrowWidth);

        public static void DrawNestedGui(Action draw, float inset)
        {
            if (draw == null)
                return;

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(Mathf.Max(0f, inset));
            EditorGUILayout.BeginVertical();
            draw();
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
        }

        public static bool DrawPropertyFoldout(
            Rect position,
            SerializedProperty property,
            GUIContent label,
            Action<Rect> draw)
        {
            float line = EditorGUIUtility.singleLineHeight;
            property.isExpanded = DrawFoldout(
                new Rect(position.x, position.y, position.width, line),
                property.isExpanded,
                label,
                true);
            if (!property.isExpanded)
                return false;
            DrawNestedContent(position, draw);
            return true;
        }

        public static void DrawNestedContent(Rect position, Action<Rect> draw)
        {
            if (draw == null)
                return;
            AtZeroIndent(() => draw(NestedContentRect(position)));
        }

        public static bool DrawFoldout(
            Rect rect,
            bool open,
            GUIContent label,
            bool toggleOnLabelClick = true,
            GUIStyle style = null)
        {
            return DrawFoldoutAtZeroIndent(() =>
            {
                GUIStyle foldStyle = style != null ? style : EditorStyles.foldout;
                return EditorGUI.Foldout(
                    rect, open, label, toggleOnLabelClick, foldStyle);
            });
        }

        internal static bool DrawFoldoutAtZeroIndent(Func<bool> draw)
        {
            bool next = false;
            AtZeroIndent(() => next = draw());
            return next;
        }

        public static bool SlotSectionOpen(string sessionKey, bool defaultOpen) =>
            SessionState.GetBool(sessionKey, defaultOpen);

        public static bool SlotSectionOpen(
            SerializedProperty property,
            string kind,
            string section,
            bool defaultOpen) =>
            SlotSectionOpen(PropertySectionKey(kind, property, section), defaultOpen);

        public static float SlotSectionHeight(bool open, float bodyHeight)
        {
            float line = EditorGUIUtility.singleLineHeight;
            float s = EditorGUIUtility.standardVerticalSpacing;
            float h = SlotSectionPad + line + SlotSectionPad;
            if (open && bodyHeight > 0f)
                h += s + bodyHeight;
            return h + s;
        }

        public static float SlotSectionHeight(
            SerializedProperty property,
            string kind,
            string section,
            bool defaultOpen,
            float bodyHeight)
        {
            bool open = SlotSectionOpen(property, kind, section, defaultOpen);
            return SlotSectionHeight(open, open ? bodyHeight : 0f);
        }

        /// <summary>
        /// Property-drawer slot groups. Uses <see cref="EditorGUI.Foldout"/>, not
        /// FoldoutHeaderGroup, so these stay safe inside Unity 6 lists.
        /// </summary>
        public static float DrawSlotSection(
            Rect position,
            float y,
            float bodyHeight,
            SerializedProperty property,
            string kind,
            string section,
            string title,
            string summary,
            Color tint,
            bool defaultOpen,
            Action<Rect> drawBody) =>
            DrawSlotSection(
                position,
                y,
                bodyHeight,
                PropertySectionKey(kind, property, section),
                title,
                summary,
                tint,
                defaultOpen,
                drawBody);

        public static float DrawSlotSection(
            Rect position,
            float y,
            float bodyHeight,
            string sessionKey,
            string title,
            string summary,
            Color tint,
            bool defaultOpen,
            Action<Rect> drawBody)
        {
            bool open = SlotSectionOpen(sessionKey, defaultOpen);
            float line = EditorGUIUtility.singleLineHeight;
            float s = EditorGUIUtility.standardVerticalSpacing;
            float outer = SlotSectionHeight(open, open ? bodyHeight : 0f);
            Rect box = EditorGUI.IndentedRect(
                new Rect(position.x, y, position.width, outer - s));

            Color header = HeaderColor(tint);
            GUI.Box(box, GUIContent.none, EditorStyles.helpBox);
            EditorGUI.DrawRect(
                new Rect(box.x, box.y, SlotSectionAccentWidth, box.height),
                AccentColor(tint));
            EditorGUI.DrawRect(
                new Rect(
                    box.x + SlotSectionAccentWidth,
                    box.y,
                    box.width - SlotSectionAccentWidth,
                    SlotSectionPad + line),
                header);

            AtZeroIndent(() =>
            {
                Rect headerRow = HeaderFoldoutRect(
                    new Rect(box.x, box.y + SlotSectionPad, box.width, line),
                    tinted: true);
                bool next = DrawHeaderFoldout(headerRow, open, title, summary);
                if (next != open)
                    SessionState.SetBool(sessionKey, next);

                if (open && next && drawBody != null && bodyHeight > 0f)
                {
                    bool wide = EditorGUIUtility.wideMode;
                    float oldLabel = EditorGUIUtility.labelWidth;
                    EditorGUIUtility.wideMode = true;
                    Rect body = NestedContentRect(
                        new Rect(
                            headerRow.x,
                            box.y + SlotSectionPad + line + s,
                            headerRow.width,
                            bodyHeight));
                    EditorGUIUtility.labelWidth = FieldLabelWidthFor(body.width);
                    drawBody(body);
                    EditorGUIUtility.wideMode = wide;
                    EditorGUIUtility.labelWidth = oldLabel;
                }
            });
            return y + outer;
        }

        #endregion

        #region Helpers

        public static float HeaderTintInset(bool tinted)
        {
            float left = HeaderFoldoutPad;
            if (tinted)
                left += SlotSectionAccentWidth;
            return left;
        }

        public static float HeaderContentInset(bool tinted) =>
            HeaderTintInset(tinted) + FoldoutArrowWidth;

        public static Rect HeaderFoldoutRect(Rect header, bool tinted)
        {
            float left = HeaderTintInset(tinted);
            return new Rect(
                header.x + left,
                header.y,
                Mathf.Max(16f, header.width - left - HeaderFoldoutPad),
                header.height);
        }

        public static Rect NestedContentRect(Rect position) =>
            InsetLeft(position, FoldoutArrowWidth);

        public static float FieldLabelWidthFor(float rowWidth)
        {
            float room = rowWidth - FieldValueMinWidth;
            if (room < 96f)
                return Mathf.Max(72f, room);
            return Mathf.Min(FieldLabelWidth, room);
        }

        public static float AtZeroIndent(Func<float> measure)
        {
            int indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;
            float h = measure();
            EditorGUI.indentLevel = indent;
            return h;
        }

        public static void AtZeroIndent(Action draw)
        {
            AtZeroIndent(() =>
            {
                draw();
                return 0f;
            });
        }

        static Rect InsetLeft(Rect rect, float left) =>
            new Rect(
                rect.x + left,
                rect.y,
                Mathf.Max(16f, rect.width - left),
                rect.height);

        static GUIStyle BoldFoldoutStyle()
        {
            if (s_BoldFoldout == null)
            {
                GUIStyle src = EditorStyles.foldout;
                s_BoldFoldout = new GUIStyle(src)
                {
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft,
                };
                s_BoldFoldout.overflow = new RectOffset(0, 0, 0, 0);
                s_BoldFoldout.padding = new RectOffset(
                    Mathf.Max(src.padding.left, (int)FoldoutArrowWidth),
                    src.padding.right,
                    src.padding.top,
                    src.padding.bottom);
            }

            return s_BoldFoldout;
        }

        static bool DrawHeaderFoldout(Rect headerRow, bool open, string title, string summary)
        {
            GUIStyle style = BoldFoldoutStyle();
            float line = EditorGUIUtility.singleLineHeight;
            float y = headerRow.y + Mathf.Max(0f, (headerRow.height - line) * 0.5f);
            float titleW = Mathf.Min(
                style.CalcSize(new GUIContent(title)).x + FoldoutArrowWidth + 8f,
                Mathf.Max(FoldoutArrowWidth + 48f, headerRow.width * 0.55f));
            bool next = DrawFoldout(
                new Rect(headerRow.x, y, titleW, line),
                open,
                new GUIContent(title),
                true,
                style);
            if (string.IsNullOrEmpty(summary))
                return next;

            Color old = GUI.color;
            GUI.color = SummaryColor();
            GUI.Label(
                new Rect(
                    headerRow.x + titleW,
                    y,
                    Mathf.Max(0f, headerRow.width - titleW),
                    line),
                summary,
                EditorStyles.miniLabel);
            GUI.color = old;
            return next;
        }

        static void DrawTintBar(Rect header, Color tint)
        {
            EditorGUI.DrawRect(
                new Rect(header.x, header.y, SlotSectionAccentWidth, header.height),
                AccentColor(tint));
            EditorGUI.DrawRect(
                new Rect(
                    header.x + SlotSectionAccentWidth,
                    header.y,
                    header.width - SlotSectionAccentWidth,
                    header.height),
                HeaderColor(tint));
        }

        static Color HeaderColor(Color tint)
        {
            float blend = EditorGUIUtility.isProSkin ? 0.38f : 0.32f;
            Color baseBg = EditorGUIUtility.isProSkin
                ? new Color(0.22f, 0.22f, 0.22f, 1f)
                : new Color(0.78f, 0.78f, 0.78f, 1f);
            Color mixed = Color.Lerp(baseBg, tint, blend);
            mixed.a = 1f;
            return mixed;
        }

        static Color AccentColor(Color tint)
        {
            Color accent = Color.Lerp(tint, Color.white, EditorGUIUtility.isProSkin ? 0.12f : 0f);
            accent.a = 1f;
            return accent;
        }

        static Color SummaryColor() =>
            EditorGUIUtility.isProSkin
                ? new Color(0.82f, 0.82f, 0.82f, 0.85f)
                : new Color(0.18f, 0.18f, 0.18f, 0.75f);

        #endregion
    }
}

#endif
