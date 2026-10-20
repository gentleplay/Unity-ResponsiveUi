using FDPlus.Foldable;
using FDPlus.Foldable.Editor;
using FDPlus.ResponsiveUI.Shared.Editor;
using UnityEditor;
using UnityEngine;

namespace FDPlus.ResponsiveUI.Editor
{
    /// <summary>
    /// Project Settings › Responsive UI › Android Fold: the half-open cards for a Fold
    /// (<see cref="FoldableHalfOpenRules.fold"/>) and a Fold with a Cover display
    /// (<see cref="FoldableHalfOpenRules.foldCover"/>), saved by <see cref="FoldableEditorRules"/>.
    /// </summary>
    static class AndroidFoldSettingsProvider
    {
        public const string Path = "Project/Responsive UI/Android Fold";

        const string Description = "Inner screen bent part-way open, standing like a laptop or held like a book.";

        static Vector2 s_Scroll;
        static bool s_FoldExpanded = true;
        static bool s_FoldCoverExpanded = true;

        [SettingsProvider]
        public static SettingsProvider Create() =>
            new SettingsProvider(Path, SettingsScope.Project)
            {
                label = "Android Fold",
                guiHandler = _ => Draw(),
                keywords = new[]
                {
                    "Android", "Fold", "Fold Cover", "Galaxy Z Fold", "half opened", "foldable", "hinge",
                    "content runs", "while rotating", "orientation", "design canvas",
                },
            };

        static void Draw()
        {
            FoldableHalfOpenRules rules = FoldableEditorRules.Rules;
            s_Scroll = EditorGUILayout.BeginScrollView(s_Scroll);

            EditorGUILayout.HelpBox(
                "How the game behaves while an Android Fold is half opened. The defaults are the platform's own " +
                "(official Android / Samsung behaviour); a changed card makes the game hold its own orientation for " +
                "that pose on the phone. The cards apply under Default Orientation = Auto Rotation with more than one " +
                "orientation allowed.\n" +
                $"Saved to {FoldableSettings.AssetPath}.",
                MessageType.Info);
            PlayerAxesCase axes = AndroidHalfOpenCardGui.ReadPlayerAxes();
            ResponsivePoseCard.DrawBanner(axes);
            EditorGUILayout.Space(4);

            bool changed = AndroidHalfOpenCardGui.Draw(
                "Half opened · Fold (e.g. Galaxy Z Fold7)", Description, rules.fold,
                ResponsiveDevice.FormFactor.FoldHalfOpened, true, axes, ref s_FoldExpanded);
            changed |= AndroidHalfOpenCardGui.Draw(
                "Half opened · Fold Cover (e.g. Galaxy Z Fold8)", Description, rules.foldCover,
                ResponsiveDevice.FormFactor.FoldCoverHalfOpened, true, axes, ref s_FoldCoverExpanded);
            if (changed)
                FoldableEditorRules.Save();

            AndroidHalfOpenCardGui.DrawFooter("Android Fold", () =>
            {
                rules.fold = new FoldableHalfOpenPose();
                rules.foldCover = new FoldableHalfOpenPose();
            });

            EditorGUILayout.EndScrollView();
        }
    }
}
