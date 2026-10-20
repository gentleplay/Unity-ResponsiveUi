using System.IO;
using FDPlus.Foldable.Editor;
using UnityEditor;

namespace FDPlus.IphoneDuo.Editor
{
    /// <summary>
    /// Project iPhone Duo rules in the Editor: loads and saves <see cref="IphoneDuoSettings.AssetPath"/>,
    /// reads the Player orientation as flags, and describes what each rule does with it (shared by the
    /// Project Settings page and the iPhone Duo Device Simulator panel). Saving also updates
    /// <see cref="IphoneDuoDevice.Rules"/>, so Play Mode picks the change up at once.
    /// </summary>
    public static class IphoneDuoEditorRules
    {
        #region Types

        /// <summary>What a posture and hold resolve to under the current rules and Player orientation.</summary>
        public readonly struct Outcome
        {
            public readonly IphoneDuoState state;
            /// <summary>Screen shape Unity renders (true: wider than tall).</summary>
            public readonly bool landscape;
            /// <summary>Plain-language result, e.g. "Sideways: the landscape game stays full screen, turned left 90°".</summary>
            public readonly string summary;

            public Outcome(IphoneDuoState state, bool landscape, string summary)
            {
                this.state = state;
                this.landscape = landscape;
                this.summary = summary;
            }
        }

        #endregion

        static IphoneDuoRules s_Rules;
        // The settings file text last read or written here: an import with other text changed on disk.
        static string s_FileJson;

        /// <summary>Raised after the rules are saved or reloaded.</summary>
        public static event System.Action Changed;

        /// <summary>The project's rules (defaults when no settings file exists). Edit, then <see cref="Save"/>.</summary>
        public static IphoneDuoRules Rules => s_Rules ??= Read();

        public static bool HasSettingsFile => File.Exists(IphoneDuoSettings.AssetPath);

        /// <summary>Seated keeps one pose against Device Simulator chrome turns (<see cref="IphoneDuoRules.seatedRotation"/>).</summary>
        public static bool SeatedRotationLocked => Rules.seatedRotation == IphoneDuoSeatedRotation.Locked;

        /// <summary>
        /// Flat keeps one hold against Device Simulator chrome turns: <see cref="IphoneDuoRules.flatRotation"/>
        /// Locked with a hold of its own (<see cref="IphoneDuoRules.FlatKeepsHold"/>; a one-axis game
        /// locked on its own axis shows that axis whatever the hold).
        /// </summary>
        public static bool FlatRotationLocked => Rules.FlatKeepsHold(ProjectFlags());

        [InitializeOnLoadMethod]
        static void RegisterSimulatorLock()
        {
            FoldableEditorSession.IosHalfOpenRotationLocked = () => SeatedRotationLocked;
            FoldableEditorSession.IosFlatRotationLocked = () => FlatRotationLocked;
        }

        /// <summary>
        /// The Seated hold under the project's rules and Player orientation (true: tall laptop =
        /// Seated Portrait), see <see cref="IphoneDuoRules.SeatedHoldTall(IphoneDuoAutorotateFlags, bool, bool)"/>.
        /// </summary>
        public static bool SeatedHoldTall(bool entryTall, bool holdTall) =>
            Rules.SeatedHoldTall(ProjectFlags(), entryTall, holdTall);

        /// <summary>
        /// The Flat hold under the project's rules and Player orientation (true: wide = Pad · Landscape),
        /// see <see cref="IphoneDuoRules.FlatHoldWide(IphoneDuoAutorotateFlags, bool, bool)"/>; the
        /// current hold <paramref name="holdWide"/> unless <see cref="FlatRotationLocked"/>.
        /// </summary>
        public static bool FlatHoldWide(bool entryWide, bool holdWide) =>
            FlatRotationLocked ? Rules.FlatHoldWide(ProjectFlags(), entryWide, holdWide) : holdWide;

        #region Storage

        /// <summary>Writes <see cref="Rules"/> to the settings file (created on first save).</summary>
        public static void Save()
        {
            string folder = Path.GetDirectoryName(IphoneDuoSettings.AssetPath);
            if (!string.IsNullOrEmpty(folder))
                Directory.CreateDirectory(folder);
            s_FileJson = IphoneDuoSettings.ToJson(Rules);
            File.WriteAllText(IphoneDuoSettings.AssetPath, s_FileJson);
            AssetDatabase.ImportAsset(IphoneDuoSettings.AssetPath);
            IphoneDuoDevice.Rules = Rules.Clone();
            Changed?.Invoke();
        }

        public static void ResetToDefaults()
        {
            s_Rules = new IphoneDuoRules();
            Save();
        }

        /// <summary>Drops the cached rules (e.g. after the settings file changed on disk).</summary>
        public static void Reload()
        {
            s_Rules = null;
            IphoneDuoDevice.Rules = null;
            Changed?.Invoke();
        }

        /// <summary>
        /// The settings file was imported, created or deleted (e.g. a version control update): reloads
        /// the rules unless the file still holds what <see cref="Save"/> wrote, so a later Save never
        /// overwrites a change made on disk.
        /// </summary>
        internal static void OnSettingsFileChanged()
        {
            if (s_Rules != null && ReadFileJson() == s_FileJson)
                return;
            Reload();
        }

        static IphoneDuoRules Read()
        {
            var rules = new IphoneDuoRules();
            s_FileJson = ReadFileJson();
            IphoneDuoSettings.FromJson(s_FileJson, rules);
            return rules;
        }

        static string ReadFileJson() => HasSettingsFile ? File.ReadAllText(IphoneDuoSettings.AssetPath) : null;

        #endregion

        #region Player orientation

        /// <summary>
        /// The rules change the orientation at runtime, which Unity honours only under Default
        /// Orientation = Auto Rotation.
        /// </summary>
        public static bool RulesApply => FoldablePlayerOrientation.IsAutoRotation;

        /// <summary>Player orientation as flags (a fixed Default Orientation allows only itself).</summary>
        public static IphoneDuoAutorotateFlags ProjectFlags()
        {
            switch (PlayerSettings.defaultInterfaceOrientation)
            {
                case UIOrientation.Portrait: return new IphoneDuoAutorotateFlags(true, false, false, false);
                case UIOrientation.PortraitUpsideDown: return new IphoneDuoAutorotateFlags(false, true, false, false);
                case UIOrientation.LandscapeLeft: return new IphoneDuoAutorotateFlags(false, false, true, false);
                case UIOrientation.LandscapeRight: return new IphoneDuoAutorotateFlags(false, false, false, true);
                default:
                    return new IphoneDuoAutorotateFlags(
                        PlayerSettings.allowedAutorotateToPortrait,
                        PlayerSettings.allowedAutorotateToPortraitUpsideDown,
                        PlayerSettings.allowedAutorotateToLandscapeLeft,
                        PlayerSettings.allowedAutorotateToLandscapeRight);
            }
        }

        static string AxisName(IphoneDuoAutorotateFlags project) =>
            !project.IsOneAxis ? "auto-rotating" : project.AllowsLandscapeAxis ? "landscape" : "portrait";

        #endregion

        #region Outcome

        /// <summary>
        /// What <paramref name="posture"/> held <paramref name="tall"/> or wide resolves to under the
        /// project's rules and Player orientation.
        /// </summary>
        public static Outcome Resolve(IphoneDuoPosture posture, bool tall) =>
            Resolve(posture, tall, Rules, ProjectFlags(), RulesApply);

        /// <summary>
        /// What <paramref name="posture"/> held <paramref name="tall"/> or wide resolves to under
        /// <paramref name="rules"/>, the Player flags <paramref name="project"/> and whether the Default
        /// Orientation is Auto Rotation (<paramref name="autoRotation"/>, see <see cref="RulesApply"/>).
        /// </summary>
        public static Outcome Resolve(
            IphoneDuoPosture posture,
            bool tall,
            IphoneDuoRules rules,
            IphoneDuoAutorotateFlags project,
            bool autoRotation)
        {
            IphoneDuoAutorotateFlags hold = tall
                ? new IphoneDuoAutorotateFlags(true, false, false, false)
                : new IphoneDuoAutorotateFlags(false, false, true, true);
            IphoneDuoScreenMode mode = autoRotation
                ? IphoneDuoHoldOrientation.Resolve(posture, hold, project, flatLocked: false, rules)
                : IphoneDuoScreenMode.Native;
            bool landscape = mode == IphoneDuoScreenMode.HoldAxis || !project.IsOneAxis
                ? !tall
                : project.AllowsLandscapeAxis;

            IphoneDuoState state = IphoneDuoHoldOrientation.BuildState(posture, hold, project, mode, flatLocked: false);
            return new Outcome(state, landscape, Describe(posture, tall, state.acrossLock, mode, project, rules, autoRotation));
        }

        static string Describe(
            IphoneDuoPosture posture,
            bool tall,
            bool across,
            IphoneDuoScreenMode mode,
            IphoneDuoAutorotateFlags project,
            IphoneDuoRules rules,
            bool autoRotation)
        {
            string axis = AxisName(project);
            if (!autoRotation)
            {
                return $"Default Orientation is {FoldablePlayerOrientation.GetDefaultOrientation()}: the rules need " +
                       "Auto Rotation, so the game keeps that orientation (iOS letterboxes it on the inner display).";
            }

            switch (mode)
            {
                case IphoneDuoScreenMode.Sideways:
                    return $"Sideways: the {axis} game stays full screen, turned " +
                           (rules.sidewaysTurn == IphoneDuoSidewaysTurn.Right90 ? "right" : "left") + " 90°" +
                           (posture == IphoneDuoPosture.Flat ? " (from a wide hold the flat lock keeps it turned the way it was)." : ".");
                case IphoneDuoScreenMode.HoldAxis:
                    if (posture == IphoneDuoPosture.Flat && rules.FlatKeepsHold(project) && !across)
                    {
                        return $"Locked: the game keeps the {(tall ? "portrait" : "landscape")} screen; turning to the " +
                               "other hold does not rotate it, the opposite side flips it upright.";
                    }
                    return across
                        ? $"Rotate: the game rotates to {(tall ? "portrait" : "landscape")} (needs those layouts)."
                        : $"The game rotates to the hold's {(tall ? "portrait" : "landscape")} axis.";
            }

            if (across)
            {
                return posture == IphoneDuoPosture.Cover
                    ? $"System Default: iOS shows the {axis} game sideways, like any iPhone."
                    : $"System Default: iOS letterboxes the {axis} game.";
            }
            if (posture == IphoneDuoPosture.Flat && project.IsOneAxis)
            {
                if (rules.flatRotation == IphoneDuoFlatRotation.Locked && !rules.FlatKeepsHold(project))
                {
                    return $"Locked: full screen {axis}; turning the device never rotates it" +
                           (rules.flatFlip180 ? ", the opposite side flips it 180° upright." : ".");
                }
                return rules.flatFlip180
                    ? $"Full screen {axis}; turning to the opposite side flips it 180° upright."
                    : $"Full screen {axis}; turning to the opposite side keeps its orientation.";
            }
            return $"Full screen {(project.IsOneAxis ? axis : tall ? "portrait" : "landscape")}.";
        }

        #endregion
    }
}

namespace FDPlus.IphoneDuo.Editor
{
    /// <summary>Reloads <see cref="IphoneDuoEditorRules"/> when the settings file changes on disk.</summary>
    sealed class IphoneDuoSettingsPostprocessor : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (Touches(importedAssets) || Touches(deletedAssets) || Touches(movedAssets) || Touches(movedFromAssetPaths))
                IphoneDuoEditorRules.OnSettingsFileChanged();
        }

        static bool Touches(string[] paths) =>
            System.Array.IndexOf(paths, IphoneDuoSettings.AssetPath) >= 0;
    }
}
