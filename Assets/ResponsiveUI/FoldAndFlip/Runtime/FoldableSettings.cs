using System;
using UnityEngine;

namespace FDPlus.Foldable
{
    /// <summary>
    /// Project Android half-open rules, saved by Project Settings › Responsive UI › Android Fold / Android Flip
    /// as JSON in a Resources folder (a TextAsset loads on every build target). Missing file or fields fall
    /// back to the <see cref="FoldableHalfOpenRules"/> defaults, which are the platform's own behaviour.
    /// </summary>
    public static class FoldableSettings
    {
        /// <summary>Resources path of the JSON (no extension).</summary>
        public const string ResourcesPath = "ResponsiveUI/FoldableSettings";

        /// <summary>Project path the Project Settings pages write.</summary>
        public const string AssetPath = "Assets/Resources/" + ResourcesPath + ".json";

        static FoldableHalfOpenRules s_rules;

        /// <summary>The project's rules, loaded on first use; assign to change them at runtime.</summary>
        public static FoldableHalfOpenRules Rules
        {
            get => s_rules ?? (s_rules = Load());
            set => s_rules = value;
        }

        /// <summary>A fresh copy of the project's rules (defaults when none are saved).</summary>
        public static FoldableHalfOpenRules Load()
        {
            var rules = new FoldableHalfOpenRules();
            var json = Resources.Load<TextAsset>(ResourcesPath);
            if (json != null)
            {
                FromJson(json.text, rules);
                Resources.UnloadAsset(json);
            }
            return rules;
        }

        public static string ToJson(FoldableHalfOpenRules rules) => JsonUtility.ToJson(rules, true);

        /// <summary>
        /// Overwrites <paramref name="rules"/> with the fields present in <paramref name="json"/>. Unreadable JSON
        /// leaves every card at its default (the platform's own behaviour) and logs one warning.
        /// </summary>
        public static void FromJson(string json, FoldableHalfOpenRules rules)
        {
            if (string.IsNullOrWhiteSpace(json))
                return;
            try
            {
                JsonUtility.FromJsonOverwrite(json, rules);
            }
            catch (Exception e)
            {
                rules.fold = new FoldableHalfOpenPose();
                rules.foldCover = new FoldableHalfOpenPose();
                rules.flip = new FoldableHalfOpenPose();
                Debug.LogWarning($"FoldAndFlip: {AssetPath} is not valid JSON; using the default half-open cards. {e.Message}");
            }
        }
    }
}
