using UnityEngine;

namespace FDPlus.IphoneDuo
{
    /// <summary>
    /// Project iPhone Duo rules, saved by Project Settings › Responsive UI › iPhone Duo as JSON in a
    /// Resources folder (a TextAsset loads on every build target; Android and desktop builds simply
    /// never read it). Missing file or fields fall back to the <see cref="IphoneDuoRules"/> defaults.
    /// </summary>
    public static class IphoneDuoSettings
    {
        /// <summary>Resources path of the JSON (no extension).</summary>
        public const string ResourcesPath = "ResponsiveUI/IphoneDuoSettings";

        /// <summary>Project path the Project Settings page writes.</summary>
        public const string AssetPath = "Assets/Resources/" + ResourcesPath + ".json";

        /// <summary>A fresh copy of the project's rules (defaults when none are saved).</summary>
        public static IphoneDuoRules Load()
        {
            var rules = new IphoneDuoRules();
            var json = Resources.Load<TextAsset>(ResourcesPath);
            if (json != null)
            {
                FromJson(json.text, rules);
                Resources.UnloadAsset(json);
            }
            return rules;
        }

        public static string ToJson(IphoneDuoRules rules) => JsonUtility.ToJson(rules, true);

        /// <summary>Overwrites <paramref name="rules"/> with the fields present in <paramref name="json"/>.</summary>
        public static void FromJson(string json, IphoneDuoRules rules)
        {
            if (!string.IsNullOrWhiteSpace(json))
                JsonUtility.FromJsonOverwrite(json, rules);
        }
    }
}
