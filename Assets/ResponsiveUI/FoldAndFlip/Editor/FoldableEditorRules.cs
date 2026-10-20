using System.IO;
using UnityEditor;

namespace FDPlus.Foldable.Editor
{
    /// <summary>
    /// Project Android half-open rules in the Editor: loads and saves <see cref="FoldableSettings.AssetPath"/>
    /// for Project Settings › Responsive UI › Android Fold / Android Flip. Saving also updates
    /// <see cref="FoldableSettings.Rules"/>, so the Simulator and Play Mode pick the change up at once.
    /// </summary>
    public static class FoldableEditorRules
    {
        static FoldableHalfOpenRules s_Rules;
        // The settings file text last read or written here: an import with other text changed on disk.
        static string s_FileJson;

        /// <summary>The settings file; tests redirect it to a temporary path.</summary>
        internal static string FilePath { get; set; } = FoldableSettings.AssetPath;

        /// <summary>Raised after the rules are saved or reloaded.</summary>
        public static event System.Action Changed;

        /// <summary>The project's rules (defaults when no settings file exists). Edit, then <see cref="Save"/>.</summary>
        public static FoldableHalfOpenRules Rules => s_Rules ??= Read();

        public static bool HasSettingsFile => File.Exists(FilePath);

        #region Storage

        /// <summary>Writes <see cref="Rules"/> to the settings file (created on first save).</summary>
        public static void Save()
        {
            string folder = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(folder))
                Directory.CreateDirectory(folder);
            s_FileJson = FoldableSettings.ToJson(Rules);
            File.WriteAllText(FilePath, s_FileJson);
            if (FilePath == FoldableSettings.AssetPath)
                AssetDatabase.ImportAsset(FilePath);
            FoldableSettings.Rules = Rules.Clone();
            Changed?.Invoke();
        }

        public static void ResetToDefaults()
        {
            s_Rules = new FoldableHalfOpenRules();
            Save();
        }

        /// <summary>Drops the cached rules (e.g. after the settings file changed on disk).</summary>
        public static void Reload()
        {
            s_Rules = null;
            FoldableSettings.Rules = null;
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

        static FoldableHalfOpenRules Read()
        {
            var rules = new FoldableHalfOpenRules();
            s_FileJson = ReadFileJson();
            FoldableSettings.FromJson(s_FileJson, rules);
            return rules;
        }

        static string ReadFileJson() => HasSettingsFile ? File.ReadAllText(FilePath) : null;

        #endregion
    }

    /// <summary>Reloads <see cref="FoldableEditorRules"/> when the settings file changes on disk.</summary>
    sealed class FoldableSettingsPostprocessor : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (Touches(importedAssets) || Touches(deletedAssets) || Touches(movedAssets) || Touches(movedFromAssetPaths))
                FoldableEditorRules.OnSettingsFileChanged();
        }

        static bool Touches(string[] paths) =>
            System.Array.IndexOf(paths, FoldableSettings.AssetPath) >= 0;
    }
}
