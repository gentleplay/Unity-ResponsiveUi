using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace FDPlus.ResponsiveUI.Shared.Editor
{
    /// <summary>Which orientations Player Settings let the game use.</summary>
    public enum PlayerAxesCase
    {
        /// <summary>Auto Rotation with only landscape allowed.</summary>
        LandscapeOnly,
        /// <summary>Auto Rotation with only portrait allowed.</summary>
        PortraitOnly,
        /// <summary>Auto Rotation with portrait and landscape allowed.</summary>
        Both,
        /// <summary>A fixed Default Orientation: runtime orientation changes are ignored.</summary>
        Fixed,
    }

    /// <summary>Where the hinge crosses the screen in a card picture.</summary>
    public enum HingeLine
    {
        None,
        /// <summary>Left to right across the middle (a tall screen).</summary>
        Across,
        /// <summary>Top to bottom down the middle (a wide screen).</summary>
        Down,
    }

    /// <summary>
    /// Shared IMGUI drawer for the pose cards of Project Settings › Responsive UI › Android Fold,
    /// Android Flip and iPhone Duo: the Player Settings banner, one card per pose (a phone picture
    /// with the hinge, a one-line description, then the fields), and which fields a Player Settings
    /// case shows (<see cref="VisibleFields"/>). Knows no device types: pages pass labels and values.
    /// </summary>
    public static class ResponsivePoseCard
    {
        #region Fields

        public const string FieldLayoutSlot = "layoutSlot";
        public const string FieldContentRuns = "contentRuns";
        /// <summary>Content runs shown read-only (fixed Default Orientation).</summary>
        public const string FieldContentRunsFixed = "contentRuns.fixed";
        public const string FieldHingeLine = "hingeLine";
        public const string FieldWhileRotating = "whileRotating";
        /// <summary>While rotating shown read-only as <see cref="FixedByPlayer"/>.</summary>
        public const string FieldWhileRotatingFixed = "whileRotating.fixed";
        /// <summary>The first design canvas row.</summary>
        public const string FieldCanvasRow = "canvas";
        /// <summary>The second design canvas row (both orientations reachable).</summary>
        public const string FieldCanvasSecondRow = "canvas.second";

        public const string FixedByPlayer = "Fixed by Player Settings";

        public const string FixedByPlayerTooltip =
            "Player Settings decide this, so the card cannot change it. " +
            "Example: a landscape-only game cannot turn to portrait while half opened.";

        public const string FixedOrientationTooltip =
            "Default Orientation is fixed, so the phone ignores runtime orientation changes and these cards keep " +
            "their defaults; use Auto Rotation to edit them. Example: Default Orientation = Landscape Left.";

        /// <summary>
        /// The fields a card shows under <paramref name="axes"/>. <paramref name="fixedByPlayer"/>: a one-axis
        /// game whose While rotating is still the default, so it reads "Fixed by Player Settings";
        /// <paramref name="oneShape"/>: the card gives one screen shape only, so one design canvas row (else two,
        /// whatever the axes).
        /// What a case cannot reach is left out, not disabled.
        /// </summary>
        public static IEnumerable<string> VisibleFields(PlayerAxesCase axes, bool fixedByPlayer, bool oneShape = false)
        {
            yield return FieldLayoutSlot;
            if (axes == PlayerAxesCase.Fixed)
            {
                yield return FieldContentRunsFixed;
                yield return FieldHingeLine;
                yield return FieldWhileRotatingFixed;
                yield return FieldCanvasRow;
                yield break;
            }

            yield return FieldContentRuns;
            yield return FieldHingeLine;
            bool oneAxis = axes != PlayerAxesCase.Both;
            yield return oneAxis && fixedByPlayer ? FieldWhileRotatingFixed : FieldWhileRotating;
            yield return FieldCanvasRow;
            // One row per screen shape the card can give: a one-axis game whose card follows the
            // device reaches both shapes too.
            if (!oneShape)
                yield return FieldCanvasSecondRow;
        }

        #endregion

        #region Player Settings

        /// <summary>The project's Player Settings case (Default Orientation + allowed autorotation).</summary>
        public static PlayerAxesCase ReadPlayerAxes() =>
            ReadPlayerAxes(
                PlayerSettings.defaultInterfaceOrientation,
                PlayerSettings.allowedAutorotateToPortrait || PlayerSettings.allowedAutorotateToPortraitUpsideDown,
                PlayerSettings.allowedAutorotateToLandscapeLeft || PlayerSettings.allowedAutorotateToLandscapeRight);

        /// <summary>
        /// Pure form of <see cref="ReadPlayerAxes()"/>: a fixed <paramref name="defaultOrientation"/> is
        /// <see cref="PlayerAxesCase.Fixed"/>, else the allowed axes (none counts as both, as Unity forces one back on).
        /// </summary>
        public static PlayerAxesCase ReadPlayerAxes(UIOrientation defaultOrientation, bool portrait, bool landscape)
        {
            if (defaultOrientation != UIOrientation.AutoRotation)
                return PlayerAxesCase.Fixed;
            if (landscape && !portrait)
                return PlayerAxesCase.LandscapeOnly;
            if (portrait && !landscape)
                return PlayerAxesCase.PortraitOnly;
            return PlayerAxesCase.Both;
        }

        /// <summary>"This game allows: …" with a link to Player Settings.</summary>
        public static void DrawBanner(PlayerAxesCase axes)
        {
            string allows;
            switch (axes)
            {
                case PlayerAxesCase.LandscapeOnly: allows = "Landscape only"; break;
                case PlayerAxesCase.PortraitOnly: allows = "Portrait only"; break;
                case PlayerAxesCase.Both: allows = "Portrait + Landscape"; break;
                default:
                    // Android pages read Auto Rotation with one orientation as fixed (the phone keeps it).
                    allows = PlayerSettings.defaultInterfaceOrientation == UIOrientation.AutoRotation
                        ? $"{SingleAutorotation()} only (Auto Rotation with one orientation). " +
                          FixedByPlayer + ": the cards show what the phone does and cannot be changed until " +
                          "a second orientation is allowed"
                        : $"{PlayerSettings.defaultInterfaceOrientation} only (fixed Default Orientation). " +
                          FixedByPlayer + ": the cards show what the phone does and cannot be changed until " +
                          "Default Orientation is Auto Rotation";
                    break;
            }

            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(
                    new GUIContent("This game allows: " + allows,
                        "Read from Player Settings › Resolution and Presentation; the cards below show only what this " +
                        "case can reach. Example: a landscape-only game shows one design canvas row per pose."),
                    EditorStyles.wordWrappedLabel);
                if (GUILayout.Button(
                        new GUIContent("Open Player Settings",
                            "Opens Player Settings, where Default Orientation and the allowed orientations are set. " +
                            "Example: untick Portrait to make the game landscape only."),
                        GUILayout.Width(150)))
                    SettingsService.OpenProjectSettings("Project/Player");
            }
        }

        /// <summary>The one orientation Auto Rotation allows, by its Player Settings name.</summary>
        static string SingleAutorotation()
        {
            if (PlayerSettings.allowedAutorotateToPortrait) return "Portrait";
            if (PlayerSettings.allowedAutorotateToPortraitUpsideDown) return "Portrait Upside Down";
            if (PlayerSettings.allowedAutorotateToLandscapeLeft) return "Landscape Left";
            return "Landscape Right";
        }

        #endregion

        #region Card

        static readonly Color s_Outline = new Color(0.55f, 0.55f, 0.55f);
        static readonly Color s_Screen = new Color(0.2f, 0.2f, 0.2f);
        static readonly Color s_Hinge = new Color(1f, 0.55f, 0.1f);
        // Card labels are long ("When held the other way than the game allows"): widened per card.
        static readonly Stack<float> s_LabelWidths = new Stack<float>();

        /// <summary>
        /// Starts a card: a foldout title (with <paramref name="summary"/> when collapsed), then, when
        /// expanded, a phone picture (<paramref name="tall"/>, the <paramref name="hinge"/> in orange)
        /// beside <paramref name="description"/>. Returns true when expanded; always call <see cref="End"/>.
        /// </summary>
        public static bool Begin(string title, string description, bool tall, HingeLine hinge, ref bool expanded,
            string summary = null)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            s_LabelWidths.Push(EditorGUIUtility.labelWidth);
            EditorGUIUtility.labelWidth = Mathf.Max(EditorGUIUtility.labelWidth, 280);
            using (new EditorGUILayout.HorizontalScope())
            {
                expanded = Foldout(expanded, new GUIContent(title, description));
                if (!expanded && !string.IsNullOrEmpty(summary))
                    EditorGUILayout.LabelField(summary, EditorStyles.miniLabel);
            }
            if (!expanded)
                return false;

            using (new EditorGUILayout.HorizontalScope())
            {
                DrawPhone(tall, hinge);
                EditorGUILayout.LabelField(description, EditorStyles.wordWrappedLabel);
            }
            EditorGUILayout.Space(2);
            return true;
        }

        /// <summary>
        /// A foldout header that leaves <see cref="GUI.changed"/> as it was, so opening or closing it never
        /// counts as a settings change.
        /// </summary>
        public static bool Foldout(bool expanded, GUIContent header)
        {
            bool changed = GUI.changed;
            expanded = EditorGUILayout.Foldout(expanded, header, true, EditorStyles.foldoutHeader);
            GUI.changed = changed;
            return expanded;
        }

        public static void End()
        {
            if (s_LabelWidths.Count > 0)
                EditorGUIUtility.labelWidth = s_LabelWidths.Pop();
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(4);
        }

        /// <summary>A phone outline, tall or wide, with the hinge drawn across or down its middle.</summary>
        static void DrawPhone(bool tall, HingeLine hinge)
        {
            Rect slot = GUILayoutUtility.GetRect(64, 64, GUILayout.Width(64), GUILayout.Height(64));
            if (Event.current.type != EventType.Repaint)
                return;

            Vector2 size = tall ? new Vector2(34, 56) : new Vector2(56, 34);
            var phone = new Rect(slot.center - size * 0.5f, size);
            EditorGUI.DrawRect(phone, s_Outline);
            EditorGUI.DrawRect(new Rect(phone.x + 2, phone.y + 2, phone.width - 4, phone.height - 4), s_Screen);
            if (hinge == HingeLine.Across)
                EditorGUI.DrawRect(new Rect(phone.x, phone.center.y - 1, phone.width, 2), s_Hinge);
            else if (hinge == HingeLine.Down)
                EditorGUI.DrawRect(new Rect(phone.center.x - 1, phone.y, 2, phone.height), s_Hinge);
        }

        #endregion

        #region Rows

        /// <summary>
        /// A dropdown over <paramref name="options"/> (each value with its own tooltip). Only values
        /// <paramref name="offered"/> accepts are listed, plus the current one so a saved value never
        /// disappears silently.
        /// </summary>
        public static T Choice<T>(GUIContent label, T value, (T value, GUIContent content)[] options, Func<T, bool> offered = null)
        {
            var values = new List<T>();
            var contents = new List<GUIContent>();
            int index = -1;
            foreach (var option in options)
            {
                bool current = EqualityComparer<T>.Default.Equals(option.value, value);
                if (!current && offered != null && !offered(option.value))
                    continue;
                if (current)
                    index = values.Count;
                values.Add(option.value);
                contents.Add(option.content);
            }
            if (index < 0)
                return value;

            int picked = EditorGUILayout.Popup(label, index, contents.ToArray());
            return values[picked];
        }

        /// <summary>A read-only row (label and value both carry <paramref name="label"/>'s tooltip).</summary>
        public static void ReadOnly(GUIContent label, string value) =>
            EditorGUILayout.LabelField(label, new GUIContent(value, label.tooltip), EditorStyles.wordWrappedLabel);

        /// <summary>A one-line note under a field (an option that changes nothing here, or what a change does).</summary>
        public static void Note(string text) =>
            EditorGUILayout.LabelField(text, EditorStyles.wordWrappedMiniLabel);

        public const string ReferenceCanvasNote =
            "From the reference device; change it under Responsive UI › Design Scale Defaults.";

        /// <summary>
        /// A design canvas row W × H for (<paramref name="form"/>, <paramref name="landscapeSlot"/>) through
        /// <see cref="ResponsivePoseCanvasHook"/>; writes only when the developer changes it. A row the
        /// reference device gives (<see cref="ResponsivePoseCanvasHook.IsFromReference"/>) is read-only.
        /// </summary>
        public static void CanvasRow(GUIContent label, int form, bool landscapeSlot)
        {
            Func<int, bool, Vector2> get = ResponsivePoseCanvasHook.Get;
            Action<int, bool, Vector2> set = ResponsivePoseCanvasHook.Set;
            if (get == null || set == null)
            {
                ReadOnly(label, "Loads with the Responsive UI settings…");
                return;
            }

            Vector2 size = get(form, landscapeSlot);
            if (ResponsivePoseCanvasHook.IsFromReference?.Invoke(form, landscapeSlot) == true)
            {
                ReadOnly(label, $"{Mathf.RoundToInt(size.x)} × {Mathf.RoundToInt(size.y)}");
                Note(ReferenceCanvasNote);
                return;
            }
            // The row saves itself through the hook, so the page's own change check does not see it.
            bool outerChanged = GUI.changed;
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel(label);
                EditorGUI.BeginChangeCheck();
                int width = EditorGUILayout.DelayedIntField(Mathf.RoundToInt(size.x));
                EditorGUILayout.LabelField("×", GUILayout.Width(12));
                int height = EditorGUILayout.DelayedIntField(Mathf.RoundToInt(size.y));
                if (EditorGUI.EndChangeCheck() && width > 0 && height > 0)
                    set(form, landscapeSlot, new Vector2(width, height));
            }
            GUI.changed = outerChanged;
        }

        /// <summary>A label with its tooltip.</summary>
        public static GUIContent Tip(string label, string tooltip) => new GUIContent(label, tooltip);

        #endregion
    }
}
