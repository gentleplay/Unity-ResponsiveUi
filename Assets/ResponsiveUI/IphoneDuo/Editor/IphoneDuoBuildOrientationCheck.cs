using FDPlus.Foldable.Editor;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace FDPlus.IphoneDuo.Editor
{
    /// <summary>
    /// iOS build warning: the iPhone Duo rules for every posture (and the native launch hold) rewrite
    /// <c>Screen.autorotateTo*</c>, which Unity only honours when the Default Orientation is Auto
    /// Rotation (<see cref="IphoneDuoEditorRules.RulesApply"/>). A fixed Default Orientation keeps the
    /// game on it in every posture.
    /// </summary>
    sealed class IphoneDuoBuildOrientationCheck : IPreprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.iOS || IphoneDuoEditorRules.RulesApply)
                return;

            Debug.LogWarning(
                $"[iPhone Duo] Default Orientation is {FoldablePlayerOrientation.GetDefaultOrientation()}: " +
                "the iPhone Duo rules (Project Settings › Responsive UI › iPhone Duo) cannot change the " +
                "orientation, so no posture follows its rule (Seated does not rotate to the hold, Sideways " +
                "does not turn the game, the launch hold is off) and the inner display letterboxes the game " +
                "when held across it (Unity ignores Screen.autorotateTo* unless the Default Orientation is " +
                "Auto Rotation). Use Auto Rotation with only the game's axis allowed.");
        }
    }
}
