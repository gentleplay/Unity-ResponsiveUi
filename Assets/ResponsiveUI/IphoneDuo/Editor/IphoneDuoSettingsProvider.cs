using System.Collections.Generic;
using FDPlus.ResponsiveUI.Shared.Editor;
using UnityEditor;
using UnityEngine;

namespace FDPlus.IphoneDuo.Editor
{
    /// <summary>
    /// Project Settings › Responsive UI › iPhone Duo: a Seated and a Flat card
    /// (<see cref="ResponsivePoseCard"/>) over the <see cref="IphoneDuoRules"/>, the Cover rule under
    /// "Other postures (as before)", launch, timings and diagnostics under "Advanced", and a live
    /// preview of each posture and hold under the current Player orientation.
    /// </summary>
    static class IphoneDuoSettingsProvider
    {
        public const string Path = "Project/Responsive UI/iPhone Duo";

        // ResponsiveDevice.FormFactor.iPhoneDuoSeated (this module does not reference the layout runtime).
        const int SeatedForm = 8;

        static Vector2 s_Scroll;
        static bool s_SeatedExpanded = true;
        static bool s_FlatExpanded = true;
        static bool s_OtherExpanded;
        static bool s_AdvancedExpanded;

        [SettingsProvider]
        public static SettingsProvider Create() =>
            new SettingsProvider(Path, SettingsScope.Project)
            {
                label = "iPhone Duo",
                guiHandler = _ => Draw(),
                keywords = new[]
                {
                    "iPhone Duo", "foldable", "posture", "seated", "flat", "cover", "sideways", "rotate",
                    "letterbox", "flip", "orientation", "laptop", "book", "while rotating", "locked pose",
                    "design canvas",
                },
            };

        [MenuItem(ResponsiveUiEditorMenus.IphoneDuoSettings, false, ResponsiveUiEditorMenus.PriorityProject + 5)]
        static void Open() => SettingsService.OpenProjectSettings(Path);

        #region Copy

        static readonly GUIContent SeatedAcrossLabel = ResponsivePoseCard.Tip("When held the other way than the game allows",
            "What a one-axis game shows when Seated is held across its axis. " +
            "Example: a landscape game in the tall laptop hold.");

        static readonly (IphoneDuoAcrossHold, GUIContent)[] SeatedAcrossOptions =
        {
            (IphoneDuoAcrossHold.Rotate, ResponsivePoseCard.Tip("Rotate",
                "The game rotates to the hold and uses the Seated · Portrait or Seated · Landscape slot. " +
                "Example: a landscape game in the tall laptop hold shows Seated · Portrait full screen.")),
            (IphoneDuoAcrossHold.Sideways, ResponsivePoseCard.Tip("Turned a quarter (Sideways)",
                "The game keeps its orientation, full screen, turned a quarter (see Sideways turn). " +
                "Example: a landscape game in the tall laptop hold stays landscape, turned 90°.")),
            (IphoneDuoAcrossHold.SystemDefault, ResponsivePoseCard.Tip("iOS letterbox (System Default)",
                "iOS letterboxes the game. " +
                "Example: a landscape game in the tall laptop hold shows a landscape band with bars.")),
        };

        static readonly GUIContent SeatedRotationLabel = ResponsivePoseCard.Tip("While rotating",
            "What turning the Duo does while Seated. " +
            "Example: Locked keeps the laptop pose when the Duo is turned to a book.");

        static readonly (IphoneDuoSeatedRotation, GUIContent)[] SeatedRotationOptions =
        {
            (IphoneDuoSeatedRotation.Locked, ResponsivePoseCard.Tip("Locked",
                "Seated keeps one pose (Locked pose) until the Duo is flattened or closed. " +
                "Example: a landscape game stays on Seated · Portrait when turned to a book.")),
            (IphoneDuoSeatedRotation.FollowsHold, ResponsivePoseCard.Tip("Follow device",
                "The pose follows the hold: laptop → Seated · Portrait, book → Seated · Landscape. " +
                "Example: turning the laptop to a book switches to Seated · Landscape.")),
        };

        static readonly GUIContent SeatedPoseLabel = ResponsivePoseCard.Tip("Locked pose",
            "The pose a Locked Seated keeps. " +
            "Example: Auto keeps a landscape-only game on the tall laptop.");

        static readonly (IphoneDuoSeatedPose, GUIContent)[] SeatedPoseOptions =
        {
            (IphoneDuoSeatedPose.Auto, ResponsivePoseCard.Tip("Auto",
                "Landscape only → tall laptop, portrait only → wide book, Portrait + Landscape → the hold Seated started with. " +
                "Example: a Portrait + Landscape game seated as a book stays a book.")),
            (IphoneDuoSeatedPose.SeatedPortrait, ResponsivePoseCard.Tip("Tall laptop (Seated · Portrait)",
                "Always Seated · Portrait, the hinge across. " +
                "Example: a portrait-only game seated as a book still uses the tall laptop pose.")),
            (IphoneDuoSeatedPose.SeatedLandscape, ResponsivePoseCard.Tip("Wide book (Seated · Landscape)",
                "Always Seated · Landscape, the hinge down the middle. " +
                "Example: a landscape-only game seated as a laptop still uses the wide book pose.")),
        };

        static readonly GUIContent SeatedSlotLabel = ResponsivePoseCard.Tip("Layout slot",
            "The Seated slot this pose shows, set by the hold and the rules above. " +
            "Example: a landscape-only game in the tall laptop hold uses iPhone Duo Seated · Portrait.");

        static readonly GUIContent HingeLineLabel = ResponsivePoseCard.Tip("Hinge line",
            "Where the hinge crosses the screen; it follows the pose. " +
            "Example: the tall laptop has the hinge across the middle.");

        const string SeatedCanvasTooltip =
            "The reference size this Seated slot is authored for; it only seeds new or unset slots. " +
            "Example: the Seated · Portrait row is taller than wide.";

        static readonly GUIContent FlatAcrossLabel = ResponsivePoseCard.Tip("When held the other way",
            "What a one-axis game shows when the open Duo is held across its axis. " +
            "Example: a landscape game held tall.");

        static readonly (IphoneDuoAcrossHold, GUIContent)[] FlatAcrossOptions =
        {
            (IphoneDuoAcrossHold.Sideways, ResponsivePoseCard.Tip("Turned a quarter (Sideways)",
                "The game stays as it is, full screen, turned a quarter, also after a cold start or unfolding tall. " +
                "Example: a landscape game held tall still fills the screen in landscape, turned 90°.")),
            (IphoneDuoAcrossHold.Rotate, ResponsivePoseCard.Tip("Rotate",
                "The game rotates to the hold. " +
                "Example: a landscape game held tall turns to Pad · Portrait (it needs that layout).")),
            (IphoneDuoAcrossHold.SystemDefault, ResponsivePoseCard.Tip("iOS letterbox (System Default)",
                "iOS letterboxes the game. " +
                "Example: a landscape game held tall shows a landscape band with bars.")),
        };

        static readonly GUIContent SidewaysTurnLabel = ResponsivePoseCard.Tip("Sideways turn",
            "Which way the game is turned when it is shown sideways (Seated or Flat, a cold start or an unfold). " +
            "Example: a landscape game held tall is turned a quarter to the left.");

        static readonly (IphoneDuoSidewaysTurn, GUIContent)[] SidewaysTurnOptions =
        {
            (IphoneDuoSidewaysTurn.Left90, ResponsivePoseCard.Tip("Left 90°",
                "The view is turned a quarter to the left of the hold. " +
                "Example: the default, the turn verified on the Xcode iPhone Duo simulator.")),
            (IphoneDuoSidewaysTurn.Right90, ResponsivePoseCard.Tip("Right 90°",
                "The view is turned a quarter to the right of the hold. " +
                "Example: use it when Left 90° reads upside down on your device.")),
        };

        static readonly GUIContent FlatRotationLabel = ResponsivePoseCard.Tip("While rotating",
            "What turning the open Duo does. " +
            "Example: Locked keeps the wide screen when a Duo opened wide is turned tall.");

        static readonly (IphoneDuoFlatRotation, GUIContent)[] FlatRotationOptions =
        {
            (IphoneDuoFlatRotation.FollowDevice, ResponsivePoseCard.Tip("Follow device",
                "The rules as before: the hold, When held the other way and Flip 180° decide. " +
                "Example: a Portrait + Landscape game turns between Pad · Landscape and Pad · Portrait.")),
            (IphoneDuoFlatRotation.Locked, ResponsivePoseCard.Tip("Locked",
                "Flat keeps one screen (Locked pose) until folded or closed; the flipped side still shows it upright. " +
                "Example: a Portrait + Landscape game opened wide stays wide when turned tall.")),
        };

        static readonly GUIContent FlatPoseLabel = ResponsivePoseCard.Tip("Locked pose",
            "The screen a Locked Flat keeps. " +
            "Example: Auto keeps a landscape-only game wide.");

        static readonly (IphoneDuoFlatPose, GUIContent)[] FlatPoseOptions =
        {
            (IphoneDuoFlatPose.Auto, ResponsivePoseCard.Tip("Auto",
                "Landscape only → wide, portrait only → tall, Portrait + Landscape → the screen Flat started with. " +
                "Example: a Portrait + Landscape game opened tall stays tall.")),
            (IphoneDuoFlatPose.Wide, ResponsivePoseCard.Tip("Wide (Pad · Landscape)",
                "Always the wide Pad · Landscape screen. " +
                "Example: a portrait-only game opened flat keeps a wide screen.")),
            (IphoneDuoFlatPose.Tall, ResponsivePoseCard.Tip("Tall (Pad · Portrait)",
                "Always the tall Pad · Portrait screen. " +
                "Example: a landscape-only game opened flat keeps a tall screen.")),
        };

        static readonly GUIContent FlipLabel = ResponsivePoseCard.Tip("Flip 180° on the opposite side",
            "Turning the open Duo to the opposite side of the game's axis flips the game upright. " +
            "Example: on, a landscape game turned upside down rights itself; off, it stays upside down.");

        static readonly GUIContent FlatSlotLabel = ResponsivePoseCard.Tip("Layout slot",
            "Flat uses the Pad slots, shared with every iPad. " +
            "Example: a Duo opened wide shows Pad · Landscape.");

        static readonly GUIContent FlatCanvasLabel = ResponsivePoseCard.Tip("Design canvas",
            "Flat is authored on the Pad design canvas, shared with every iPad. " +
            "Example: change it under Pad in Project Settings › Responsive UI.");

        static readonly GUIContent OtherHeader = ResponsivePoseCard.Tip("Other postures (as before)",
            "Postures without a card, with their rule as before. " +
            "Example: the Closed (cover display) rule.");

        static readonly GUIContent CoverAcrossLabel = ResponsivePoseCard.Tip("Cover (Closed), held the other way",
            "What the cover display shows when held across the game's axis. " +
            "Example: a landscape game on the tall cover display.");

        static readonly (IphoneDuoAcrossHold, GUIContent)[] CoverAcrossOptions =
        {
            (IphoneDuoAcrossHold.SystemDefault, ResponsivePoseCard.Tip("System Default (like any iPhone)",
                "iOS shows the game sideways, like on any iPhone. " +
                "Example: a landscape game on the tall cover reads sideways.")),
            (IphoneDuoAcrossHold.Rotate, ResponsivePoseCard.Tip("Rotate",
                "The game rotates to the hold (it needs layouts for that axis). " +
                "Example: a landscape game on the tall cover turns to portrait.")),
            (IphoneDuoAcrossHold.Sideways, ResponsivePoseCard.Tip("Turned a quarter (Sideways)",
                "The game keeps its orientation, full screen, turned a quarter. " +
                "Example: a landscape game on the tall cover fills it, turned 90°.")),
        };

        static readonly GUIContent AdvancedHeader = ResponsivePoseCard.Tip("Advanced",
            "Launch screen, timings and diagnostics. " +
            "Example: turn on the debug overlay to see the posture on screen.");

        static readonly GUIContent LaunchLabel = ResponsivePoseCard.Tip("Launch screen fills the display",
            "iOS builds: iOS's launch screen fills the display when the game starts held the other way than it allows (every iPhone then shows it on the other axis first). " +
            "Example: a landscape game opened tall shows a full-screen launch image, then rotates to landscape.");

        static readonly GUIContent LeaveSeatedLabel = ResponsivePoseCard.Tip("Leave Seated delay",
            "Seconds the Duo must stay out of Seated before the Cover rules apply, as iOS resizes in steps while it rotates. " +
            "Example: 0.3 ignores a one-frame Closed reading during a rotation.");

        static readonly GUIContent SettleLabel = ResponsivePoseCard.Tip("Flat lock settle",
            "Flat: seconds the game must stay upright before the orientation lock returns. " +
            "Example: 0.5 waits out the rotation animation after a 180° flip.");

        static readonly GUIContent RetryLabel = ResponsivePoseCard.Tip("Rotation retry",
            "Flat: seconds before a flip request iOS has not carried out is sent again. " +
            "Example: 0.5 sends a missed 180° flip again half a second later.");

        static readonly GUIContent TraceLabel = ResponsivePoseCard.Tip("Trace log (development builds)",
            "Development builds log one line per posture or orientation change. " +
            "Example: unfolding to Flat writes one line to the Xcode console.");

        static readonly GUIContent OverlayLabel = ResponsivePoseCard.Tip("Debug overlay",
            "Development builds and the Editor show the posture, hold and screen mode in a corner of the screen. " +
            "Example: turn it on to check which hold Seated uses.");

        #endregion

        static void Draw()
        {
            IphoneDuoRules rules = IphoneDuoEditorRules.Rules;
            s_Scroll = EditorGUILayout.BeginScrollView(s_Scroll);

            EditorGUILayout.HelpBox(
                "How the game behaves on each iPhone Duo posture. Seated and Flat have a card each; the rules " +
                "for holding the Duo the other way apply to a one-axis game (Default Orientation = Auto Rotation " +
                "with only landscape or only portrait allowed), e.g. a landscape game held tall.\n" +
                $"Saved to {IphoneDuoSettings.AssetPath}. At runtime read or change them through " +
                "IphoneDuoDevice.Rules; react to posture changes with IphoneDuoDevice.StateChanged or the " +
                "iPhone Duo Events component.",
                MessageType.Info);
            PlayerAxesCase axes = ResponsivePoseCard.ReadPlayerAxes();
            ResponsivePoseCard.DrawBanner(axes);
            DrawPlayerOrientation();
            EditorGUILayout.Space(4);

            IphoneDuoAutorotateFlags project = IphoneDuoEditorRules.ProjectFlags();
            bool fixedOrientation = axes == PlayerAxesCase.Fixed;

            EditorGUI.BeginChangeCheck();
            DrawSeated(rules, project, fixedOrientation);
            DrawFlat(rules, project, fixedOrientation);
            float labelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = Mathf.Max(labelWidth, 280);
            DrawOther(rules);
            DrawAdvanced(rules);
            EditorGUIUtility.labelWidth = labelWidth;
            if (EditorGUI.EndChangeCheck())
                IphoneDuoEditorRules.Save();

            DrawPreview();

            EditorGUILayout.Space(8);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("Reset to Defaults",
                        "Puts every iPhone Duo rule back to its default. Example: Seated returns to Rotate, Locked, Auto.")) &&
                    EditorUtility.DisplayDialog("iPhone Duo", "Reset every iPhone Duo rule to its default?", "Reset", "Cancel"))
                    IphoneDuoEditorRules.ResetToDefaults();
                using (new EditorGUI.DisabledScope(!IphoneDuoEditorRules.HasSettingsFile))
                {
                    if (GUILayout.Button(new GUIContent("Select Settings File",
                            "Shows the saved settings file in the Project window. Example: to check it into version control.")))
                        EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<TextAsset>(IphoneDuoSettings.AssetPath));
                }
                if (GUILayout.Button(new GUIContent("Reload",
                        "Reads the settings file again. Example: after a version control update changed it.")))
                    IphoneDuoEditorRules.Reload();
            }
            if (!IphoneDuoEditorRules.HasSettingsFile)
                EditorGUILayout.LabelField("Using the built-in defaults: the first change creates the settings file.", EditorStyles.miniLabel);

            EditorGUILayout.EndScrollView();
        }

        static void DrawPlayerOrientation()
        {
            if (!IphoneDuoEditorRules.RulesApply)
            {
                EditorGUILayout.HelpBox(
                    "Default Orientation is fixed: Unity ignores runtime orientation changes, so these rules have no " +
                    "effect and iOS letterboxes the game when the inner display is held across it. Use Auto Rotation " +
                    "with only the game's axis allowed (Player Settings › Resolution and Presentation).",
                    MessageType.Warning);
            }
            else if (!IphoneDuoEditorRules.ProjectFlags().IsOneAxis)
            {
                EditorGUILayout.HelpBox(
                    "Both axes are allowed, so nothing is held the other way: those rules are hidden. " +
                    "Seated follows its While rotating rule.",
                    MessageType.None);
            }
        }

        #region Cards

        static void DrawSeated(IphoneDuoRules rules, IphoneDuoAutorotateFlags project, bool fixedOrientation)
        {
            List<bool> slots = SeatedSlots(rules, project); // true: Seated · Landscape (wide book)
            bool tall = !slots[0];
            bool open = ResponsivePoseCard.Begin("Seated",
                "Bent like a laptop standing on a table, or held open like a book.",
                tall, tall ? HingeLine.Across : HingeLine.Down, ref s_SeatedExpanded,
                $"{Option(SeatedRotationOptions, rules.seatedRotation)} · {SeatedSlotText(slots)}");
            if (open)
            {
                using (new EditorGUI.DisabledScope(fixedOrientation))
                {
                    if (project.IsOneAxis)
                    {
                        rules.seatedAcrossLock = ResponsivePoseCard.Choice(SeatedAcrossLabel, rules.seatedAcrossLock, SeatedAcrossOptions);
                        if (SeatedNeverAcross(rules, project))
                            ResponsivePoseCard.Note("Never used here: the Locked pose is already on the game's axis.");
                    }
                    rules.seatedRotation = ResponsivePoseCard.Choice(SeatedRotationLabel, rules.seatedRotation, SeatedRotationOptions);
                    if (rules.seatedRotation == IphoneDuoSeatedRotation.Locked)
                        rules.seatedLockedPose = ResponsivePoseCard.Choice(SeatedPoseLabel, rules.seatedLockedPose, SeatedPoseOptions);
                }
                ResponsivePoseCard.ReadOnly(SeatedSlotLabel, SeatedSlotText(slots));
                ResponsivePoseCard.ReadOnly(HingeLineLabel, slots.Count > 1
                    ? "Across the middle as a laptop, down the middle as a book"
                    : slots[0] ? "Down the middle (book)" : "Across the middle (laptop)");
                foreach (bool landscape in slots)
                {
                    ResponsivePoseCard.CanvasRow(
                        new GUIContent($"Design canvas · {(landscape ? "Landscape" : "Portrait")}", SeatedCanvasTooltip),
                        SeatedForm, landscape);
                }
                if (fixedOrientation)
                    FixedNote();
            }
            ResponsivePoseCard.End();
        }

        static void DrawFlat(IphoneDuoRules rules, IphoneDuoAutorotateFlags project, bool fixedOrientation)
        {
            List<bool> slots = FlatSlots(rules, project); // true: Pad · Landscape (wide)
            bool open = ResponsivePoseCard.Begin("Flat (fully open)",
                "Opened all the way and flat, like a small tablet.",
                !slots[0], HingeLine.None, ref s_FlatExpanded,
                $"{Option(FlatRotationOptions, rules.flatRotation)} · {FlatSlotText(slots)}");
            if (open)
            {
                using (new EditorGUI.DisabledScope(fixedOrientation))
                {
                    if (project.IsOneAxis)
                    {
                        rules.flatAcrossLock = ResponsivePoseCard.Choice(FlatAcrossLabel, rules.flatAcrossLock, FlatAcrossOptions);
                        if (rules.flatRotation == IphoneDuoFlatRotation.Locked && rules.FlatKeepsHold(project) &&
                            rules.flatAcrossLock == IphoneDuoAcrossHold.SystemDefault)
                            ResponsivePoseCard.Note("Locked keeps the hold here: held across the game's axis, iOS letterboxes the Locked pose (as the Simulator shows); held on the game's own axis it is full screen.");
                        else if (rules.FlatAcrossLockFor(project) != rules.flatAcrossLock)
                            ResponsivePoseCard.Note("Acts as Turned a quarter (Sideways) here: a Locked Flat on the game's own axis never rotates.");

                        if (rules.seatedAcrossLock == IphoneDuoAcrossHold.Sideways ||
                            rules.FlatAcrossLockFor(project) == IphoneDuoAcrossHold.Sideways)
                            rules.sidewaysTurn = ResponsivePoseCard.Choice(SidewaysTurnLabel, rules.sidewaysTurn, SidewaysTurnOptions);
                    }
                    rules.flatRotation = ResponsivePoseCard.Choice(FlatRotationLabel, rules.flatRotation, FlatRotationOptions);
                    if (rules.flatRotation == IphoneDuoFlatRotation.Locked)
                        rules.flatLockedPose = ResponsivePoseCard.Choice(FlatPoseLabel, rules.flatLockedPose, FlatPoseOptions);
                    if (project.IsOneAxis)
                        rules.flatFlip180 = EditorGUILayout.Toggle(FlipLabel, rules.flatFlip180);
                }
                ResponsivePoseCard.ReadOnly(FlatSlotLabel, FlatSlotText(slots) + " (same as every iPad)");
                ResponsivePoseCard.ReadOnly(FlatCanvasLabel, "Same as every iPad: edit under Pad");
                if (fixedOrientation)
                    FixedNote();
            }
            ResponsivePoseCard.End();
        }

        static void DrawOther(IphoneDuoRules rules)
        {
            EditorGUILayout.Space(4);
            s_OtherExpanded = ResponsivePoseCard.Foldout(s_OtherExpanded, OtherHeader);
            if (!s_OtherExpanded)
                return;
            // Sideways on the cover is what System Default does (iOS shows it sideways natively): not offered.
            using (new EditorGUI.IndentLevelScope())
                rules.coverAcrossLock = ResponsivePoseCard.Choice(CoverAcrossLabel, rules.coverAcrossLock, CoverAcrossOptions,
                    value => value != IphoneDuoAcrossHold.Sideways);
        }

        static void DrawAdvanced(IphoneDuoRules rules)
        {
            s_AdvancedExpanded = ResponsivePoseCard.Foldout(s_AdvancedExpanded, AdvancedHeader);
            if (!s_AdvancedExpanded)
                return;
            using (new EditorGUI.IndentLevelScope())
            {
                Header("Launch (iOS build)");
                rules.launchScreenFillsAcrossHold = EditorGUILayout.Toggle(LaunchLabel, rules.launchScreenFillsAcrossHold);
                if (rules.launchScreenFillsAcrossHold)
                {
                    EditorGUILayout.HelpBox(
                        "Every iPhone launched held across the Player lock shows iOS's launch screen on the other axis, " +
                        "then the game rotates to its own. iPads are unchanged. Applied at the next iOS build.",
                        MessageType.None);
                }

                Header("Timings (seconds)");
                // Delayed: saved on Enter / focus loss, not on every keystroke.
                rules.leaveSeatedDelaySeconds = Mathf.Max(0f, EditorGUILayout.DelayedFloatField(LeaveSeatedLabel, rules.leaveSeatedDelaySeconds));
                rules.lockSettleSeconds = Mathf.Max(0f, EditorGUILayout.DelayedFloatField(SettleLabel, rules.lockSettleSeconds));
                rules.requestRetrySeconds = Mathf.Max(0.05f, EditorGUILayout.DelayedFloatField(RetryLabel, rules.requestRetrySeconds));

                Header("Diagnostics");
                rules.traceLog = EditorGUILayout.Toggle(TraceLabel, rules.traceLog);
                rules.debugOverlay = EditorGUILayout.Toggle(OverlayLabel, rules.debugOverlay);
            }
        }

        static void FixedNote() =>
            EditorGUILayout.HelpBox(
                $"{ResponsivePoseCard.FixedByPlayer}: Default Orientation is fixed, so these rules have no effect until it is Auto Rotation.",
                MessageType.None);

        #endregion

        #region Rules

        /// <summary>
        /// The Seated slots the game can show (true: Seated · Landscape), Portrait first: the holds
        /// Seated can be in (one under Locked, both when it follows the hold), each as
        /// <see cref="IphoneDuoEditorRules.Resolve(IphoneDuoPosture, bool)"/> renders it.
        /// </summary>
        static List<bool> SeatedSlots(IphoneDuoRules rules, IphoneDuoAutorotateFlags project)
        {
            var holds = rules.seatedRotation == IphoneDuoSeatedRotation.FollowsHold
                ? new[] { true, false }
                : new[] { rules.SeatedHoldTall(project, true, true), rules.SeatedHoldTall(project, false, false) };
            return Rendered(IphoneDuoPosture.Seated, holds);
        }

        /// <summary>The Pad slots Flat can show (true: Pad · Landscape), Portrait first.</summary>
        static List<bool> FlatSlots(IphoneDuoRules rules, IphoneDuoAutorotateFlags project)
        {
            var holds = rules.flatRotation == IphoneDuoFlatRotation.FollowDevice
                ? new[] { true, false }
                : new[] { !rules.FlatHoldWide(project, true, true), !rules.FlatHoldWide(project, false, false) };
            return Rendered(IphoneDuoPosture.Flat, holds);
        }

        static List<bool> Rendered(IphoneDuoPosture posture, bool[] tallHolds)
        {
            bool portrait = false, landscape = false;
            foreach (bool tall in tallHolds)
            {
                if (IphoneDuoEditorRules.Resolve(posture, tall).landscape)
                    landscape = true;
                else
                    portrait = true;
            }
            var slots = new List<bool>();
            if (portrait)
                slots.Add(false);
            if (landscape)
                slots.Add(true);
            return slots;
        }

        /// <summary>
        /// A Locked Seated whose pose is on a one-axis game's own axis is never held across it
        /// (the tall laptop is across a landscape lock), so the held-the-other-way rule never applies.
        /// </summary>
        static bool SeatedNeverAcross(IphoneDuoRules rules, IphoneDuoAutorotateFlags project) =>
            project.IsOneAxis &&
            rules.seatedRotation == IphoneDuoSeatedRotation.Locked &&
            rules.seatedLockedPose != IphoneDuoSeatedPose.Auto &&
            rules.SeatedHoldTall(project, true, true) != project.AllowsLandscapeAxis;

        static string SeatedSlotText(List<bool> slots)
        {
            var parts = new List<string>();
            foreach (bool landscape in slots)
                parts.Add(landscape ? "iPhone Duo Seated · Landscape (wide book)" : "iPhone Duo Seated · Portrait (tall laptop)");
            return string.Join(" / ", parts);
        }

        static string FlatSlotText(List<bool> slots)
        {
            var parts = new List<string>();
            foreach (bool landscape in slots)
                parts.Add(landscape ? "Pad · Landscape (wide)" : "Pad · Portrait (tall)");
            return string.Join(" / ", parts);
        }

        static string Option<T>((T value, GUIContent content)[] options, T value)
        {
            foreach (var option in options)
            {
                if (EqualityComparer<T>.Default.Equals(option.value, value))
                    return option.content.text;
            }
            return value.ToString();
        }

        #endregion

        #region Preview

        static void DrawPreview()
        {
            Header("Preview (current Player orientation)");
            Row("Cover · tall", IphoneDuoPosture.Cover, true);
            Row("Cover · wide", IphoneDuoPosture.Cover, false);
            Row("Seated · laptop", IphoneDuoPosture.Seated, true);
            Row("Seated · book", IphoneDuoPosture.Seated, false);
            Row("Flat · tall", IphoneDuoPosture.Flat, true);
            Row("Flat · wide", IphoneDuoPosture.Flat, false);
        }

        static void Row(string label, IphoneDuoPosture posture, bool tall)
        {
            IphoneDuoEditorRules.Outcome outcome = IphoneDuoEditorRules.Resolve(posture, tall);
            EditorGUILayout.LabelField(label, outcome.summary, EditorStyles.wordWrappedMiniLabel);
        }

        static void Header(string text)
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField(text, EditorStyles.boldLabel);
        }

        #endregion
    }
}
