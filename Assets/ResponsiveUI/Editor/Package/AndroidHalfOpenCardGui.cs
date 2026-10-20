using System;
using System.Collections.Generic;
using FDPlus.Foldable;
using FDPlus.Foldable.Editor;
using FDPlus.ResponsiveUI.Shared.Editor;
using UnityEditor;
using UnityEngine;

namespace FDPlus.ResponsiveUI.Editor
{
    /// <summary>
    /// One Android half-open card (<see cref="FoldableHalfOpenPose"/>) for the Android Fold and Android
    /// Flip settings pages, drawn with <see cref="ResponsivePoseCard"/>. Layout slot and hinge line are
    /// read-only and follow Content runs (<see cref="FoldableHalfOpenRules.ScreenLandscape"/>); a fixed
    /// Default Orientation shows the platform defaults read-only, as the Simulator and the phone use them.
    /// </summary>
    static class AndroidHalfOpenCardGui
    {
        #region Copy

        const string ChangedNote = "On the phone the game turns its own orientation while this pose lasts.";

        const string OneOrientationTooltip =
            "Auto Rotation allows one orientation only, so the phone keeps it as if fixed and these cards keep " +
            "their defaults; allow a second orientation to edit them. Example: Auto Rotation with only Portrait ticked.";

        static readonly GUIContent LayoutSlotLabel = ResponsivePoseCard.Tip("Layout slot",
            "The layout slot this pose shows, set by the pose and Content runs. " +
            "Example: a landscape-only game half opened on a Galaxy Z Fold8 uses Fold Cover Half Opened · Landscape.");

        static readonly GUIContent ContentRunsLabel = ResponsivePoseCard.Tip("Content runs",
            "Which way the content runs while half opened, and so the screen shape. " +
            "Example: Top → down gives a tall screen with the hinge across it.");

        static readonly GUIContent HingeLineLabel = ResponsivePoseCard.Tip("Hinge line",
            "Where the hinge crosses the screen; it follows Content runs. " +
            "Example: a tall screen has the hinge across the middle.");

        static readonly GUIContent WhileRotatingLabel = ResponsivePoseCard.Tip("While rotating",
            "What turning the phone does while half opened. " +
            "Example: Locked keeps a Portrait + Landscape game on the screen it had when it half opened.");

        const string CanvasTooltip =
            "The reference size this pose's slot is authored for; it only seeds new or unset slots. " +
            "Example: 1848 × 2448 for a tall Fold Cover half open.";

        static readonly GUIContent FollowInsteadButton = ResponsivePoseCard.Tip("Follow device instead",
            "Lets the screen turn with the phone while half opened; the game then turns its own orientation. " +
            "Example: a landscape-only game half opened turns to a wide screen when the phone is turned.");

        static readonly (FoldableContentRuns, GUIContent)[] ContentRunsOptions =
        {
            (FoldableContentRuns.AsAndroid, ResponsivePoseCard.Tip("As Android gives (default)",
                "The screen shape the phone gives on its own (official Android / Samsung behaviour). " +
                "Example: on a Fold, a landscape-only game gets a tall screen.")),
            (FoldableContentRuns.TopToDown, ResponsivePoseCard.Tip("Top → down (tall)",
                "Always a tall screen with the hinge across it; the game turns its own orientation when needed. " +
                "Example: a portrait-only game half opened on a Fold gets a tall screen instead of Android's wide one.")),
            (FoldableContentRuns.LeftToRight, ResponsivePoseCard.Tip("Left → right (wide)",
                "Always a wide screen with the hinge down the middle; the game turns its own orientation when needed. " +
                "Example: a landscape-only game half opened on a Fold gets a wide screen.")),
        };

        static readonly (FoldableHalfOpenRotation, GUIContent)[] RotationOptions =
        {
            (FoldableHalfOpenRotation.AsAndroid, ResponsivePoseCard.Tip("As Android gives (default)",
                "Player Settings decide: a one-axis game keeps its screen, a Portrait + Landscape game turns with the phone. " +
                "Example: a Portrait + Landscape Fold switches between tall and wide as it turns.")),
            (FoldableHalfOpenRotation.Locked, ResponsivePoseCard.Tip("Locked",
                "Turning the phone keeps the screen the pose started with. " +
                "Example: a Portrait + Landscape game half opened tall stays tall when turned.")),
            (FoldableHalfOpenRotation.FollowDevice, ResponsivePoseCard.Tip("Follow device",
                "The screen turns with the phone, even in a one-axis game. " +
                "Example: a landscape-only game half opened turns to a wide screen when the phone is turned.")),
        };

        #endregion

        #region Card

        /// <summary>
        /// The Player Settings case for the Android cards: as <see cref="ResponsivePoseCard.ReadPlayerAxes()"/>, but
        /// Auto Rotation with one orientation allowed reads <see cref="PlayerAxesCase.Fixed"/>, as the phone keeps it
        /// (<see cref="FoldablePlayerOrientation.AllowsAndroidHalfOpenHold"/>). iPhone Duo keeps its own reading.
        /// </summary>
        public static PlayerAxesCase ReadPlayerAxes() =>
            ReadPlayerAxes(ResponsivePoseCard.ReadPlayerAxes(), FoldablePlayerOrientation.AllowsAndroidHalfOpenHold);

        /// <summary>Pure form of <see cref="ReadPlayerAxes()"/>: <paramref name="allowsHold"/> false reads Fixed.</summary>
        public static PlayerAxesCase ReadPlayerAxes(PlayerAxesCase axes, bool allowsHold) =>
            allowsHold ? axes : PlayerAxesCase.Fixed;

        /// <summary>Fixed by an Auto Rotation that allows one orientation (not a fixed Default Orientation).</summary>
        static bool OneOrientation(PlayerAxesCase axes) =>
            axes == PlayerAxesCase.Fixed && PlayerSettings.defaultInterfaceOrientation == UIOrientation.AutoRotation;

        /// <summary>
        /// Draws the card for <paramref name="pose"/> (form <paramref name="form"/>) under <paramref name="axes"/>
        /// and returns true when the developer changed it (the caller saves).
        /// </summary>
        public static bool Draw(
            string title,
            string description,
            FoldableHalfOpenPose pose,
            ResponsiveDevice.FormFactor form,
            bool isFold,
            PlayerAxesCase axes,
            ref bool expanded)
        {
            bool fixedOrientation = axes == PlayerAxesCase.Fixed;
            bool? locked = LockedLandscape(axes);
            // A fixed Default Orientation (or Auto Rotation with one orientation) ignores the card
            // (FoldableEditorSession falls back to defaults).
            FoldableHalfOpenPose shown = fixedOrientation ? new FoldableHalfOpenPose() : pose;
            List<bool> shapes = Shapes(shown, isFold, locked);
            bool fixedByPlayer = locked.HasValue && pose.whileRotating == FoldableHalfOpenRotation.AsAndroid;
            var fields = new HashSet<string>(ResponsivePoseCard.VisibleFields(axes, fixedByPlayer, shapes.Count == 1));
            bool tall = !shapes[0];
            string slots = SlotText(form, shapes);

            bool open = ResponsivePoseCard.Begin(title, description, tall,
                tall ? HingeLine.Across : HingeLine.Down, ref expanded,
                $"{OptionText(ContentRunsOptions, shown.contentRuns)} · {slots}");
            if (!open)
            {
                ResponsivePoseCard.End();
                return false;
            }

            var contentRuns = pose.contentRuns;
            var whileRotating = pose.whileRotating;

            ResponsivePoseCard.ReadOnly(LayoutSlotLabel, slots);

            if (fields.Contains(ResponsivePoseCard.FieldContentRuns))
            {
                contentRuns = ResponsivePoseCard.Choice(ContentRunsLabel, contentRuns, ContentRunsOptions);
                if (contentRuns != FoldableContentRuns.AsAndroid && locked.HasValue &&
                    GivesShape(new FoldableHalfOpenPose(), isFold, locked, contentRuns == FoldableContentRuns.LeftToRight))
                    ResponsivePoseCard.Note("Same screen Android gives here; the game also holds its orientation while this pose lasts.");
            }
            else
            {
                ResponsivePoseCard.ReadOnly(ContentRunsLabel,
                    $"{OptionText(ContentRunsOptions, shown.contentRuns)} · {ResponsivePoseCard.FixedByPlayer}");
            }

            ResponsivePoseCard.ReadOnly(HingeLineLabel, HingeText(shapes));

            if (!fixedOrientation && contentRuns != FoldableContentRuns.AsAndroid)
            {
                // Content runs fixes the shape, so turning the phone changes nothing.
                ResponsivePoseCard.ReadOnly(WhileRotatingLabel, "Keeps the Content runs shape");
            }
            else if (fields.Contains(ResponsivePoseCard.FieldWhileRotatingFixed))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    ResponsivePoseCard.ReadOnly(
                        new GUIContent(WhileRotatingLabel.text,
                            !fixedOrientation ? ResponsivePoseCard.FixedByPlayerTooltip
                            : OneOrientation(axes) ? OneOrientationTooltip : ResponsivePoseCard.FixedOrientationTooltip),
                        ResponsivePoseCard.FixedByPlayer);
                    if (!fixedOrientation && GUILayout.Button(FollowInsteadButton, EditorStyles.miniButton, GUILayout.Width(140)))
                        whileRotating = FoldableHalfOpenRotation.FollowDevice;
                }
            }
            else
            {
                // One axis: Locked is what the Player lock gives already; both axes: Follow device is.
                var reachable = locked.HasValue ? FoldableHalfOpenRotation.FollowDevice : FoldableHalfOpenRotation.Locked;
                whileRotating = ResponsivePoseCard.Choice(WhileRotatingLabel, whileRotating, RotationOptions,
                    value => value == FoldableHalfOpenRotation.AsAndroid || value == reachable);
            }

            for (int i = 0; i < shapes.Count; i++)
            {
                if (i == 1 && !fields.Contains(ResponsivePoseCard.FieldCanvasSecondRow))
                    break;
                var slot = Slot(form, shapes[i]);
                ResponsivePoseCard.CanvasRow(
                    new GUIContent($"Design canvas · {slot}", CanvasTooltip),
                    (int)form,
                    slot == ResponsiveDevice.Orientation.Landscape);
            }

            bool changed = contentRuns != pose.contentRuns || whileRotating != pose.whileRotating;
            pose.contentRuns = contentRuns;
            pose.whileRotating = whileRotating;

            if (fixedOrientation)
            {
                EditorGUILayout.HelpBox(
                    $"{ResponsivePoseCard.FixedByPlayer}: " +
                    (OneOrientation(axes)
                        ? "Auto Rotation allows one orientation only, so the phone keeps it."
                        : "Default Orientation is fixed, so the phone keeps it.") +
                    (pose.IsDefault ? "" : " Your saved choices apply again under Auto Rotation."),
                    MessageType.None);
            }
            else if (FoldableHalfOpenRules.NeedsRuntimeHold(pose, locked))
            {
                EditorGUILayout.HelpBox(ChangedNote, MessageType.Info);
            }

            ResponsivePoseCard.End();
            return changed;
        }

        /// <summary>Reset / Select / Reload, as on the iPhone Duo page.</summary>
        public static void DrawFooter(string section, Action reset)
        {
            EditorGUILayout.Space(8);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("Reset to Defaults",
                        "Puts this section's cards back to the platform's own behaviour. Example: a changed Content runs returns to As Android gives.")) &&
                    EditorUtility.DisplayDialog(section, $"Reset every {section} card to its default?", "Reset", "Cancel"))
                {
                    reset();
                    FoldableEditorRules.Save();
                }
                using (new EditorGUI.DisabledScope(!FoldableEditorRules.HasSettingsFile))
                {
                    if (GUILayout.Button(new GUIContent("Select Settings File",
                            "Shows the saved settings file in the Project window. Example: to check it into version control.")))
                        EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<TextAsset>(FoldableSettings.AssetPath));
                }
                if (GUILayout.Button(new GUIContent("Reload",
                        "Reads the settings file again. Example: after a version control update changed it.")))
                    FoldableEditorRules.Reload();
            }
            if (!FoldableEditorRules.HasSettingsFile)
                EditorGUILayout.LabelField("Using the built-in defaults: the first change creates the settings file.", EditorStyles.miniLabel);
        }

        #endregion

        #region Rules

        /// <summary>The Player Settings lock as the rules read it (true: landscape only, null: both axes).</summary>
        static bool? LockedLandscape(PlayerAxesCase axes)
        {
            switch (axes)
            {
                case PlayerAxesCase.LandscapeOnly: return true;
                case PlayerAxesCase.PortraitOnly: return false;
                case PlayerAxesCase.Both: return null;
                default: return FoldablePlayerOrientation.GetLockedLandscape();
            }
        }

        /// <summary>The screen shapes the card can give (true: wide), tall first; both when it follows the phone.</summary>
        static List<bool> Shapes(FoldableHalfOpenPose pose, bool isFold, bool? locked)
        {
            bool? enteredWide = FoldableHalfOpenRules.ScreenLandscape(pose, isFold, locked, true);
            bool? enteredTall = FoldableHalfOpenRules.ScreenLandscape(pose, isFold, locked, false);
            if (enteredWide.HasValue && enteredTall.HasValue && enteredWide.Value == enteredTall.Value)
                return new List<bool> { enteredWide.Value };
            return new List<bool> { false, true };
        }

        static bool GivesShape(FoldableHalfOpenPose pose, bool isFold, bool? locked, bool wide)
        {
            List<bool> shapes = Shapes(pose, isFold, locked);
            return shapes.Count == 1 && shapes[0] == wide;
        }

        static ResponsiveDevice.Orientation Slot(ResponsiveDevice.FormFactor form, bool wide) =>
            ResponsiveDevice.OrientationForForm(form,
                wide ? ResponsiveDevice.Orientation.Landscape : ResponsiveDevice.Orientation.Portrait);

        static string SlotText(ResponsiveDevice.FormFactor form, List<bool> shapes)
        {
            var parts = new List<string>();
            foreach (bool wide in shapes)
                parts.Add($"{ResponsiveDevice.GetFormDisplayName(form)} · {Slot(form, wide)} ({(wide ? "wide" : "tall")})");
            return string.Join(" / ", parts);
        }

        static string HingeText(List<bool> shapes)
        {
            if (shapes.Count > 1)
                return "Across the middle when tall, down the middle when wide";
            return shapes[0] ? "Down the middle" : "Across the middle";
        }

        static string OptionText<T>((T value, GUIContent content)[] options, T value)
        {
            foreach (var option in options)
            {
                if (EqualityComparer<T>.Default.Equals(option.value, value))
                    return option.content.text;
            }
            return value.ToString();
        }

        #endregion
    }
}
