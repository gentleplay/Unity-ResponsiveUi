using FDPlus.Foldable;
using FDPlus.Foldable.Editor;
using FDPlus.ResponsiveUI.Shared.Editor;
using UnityEditor;
using UnityEngine;

namespace FDPlus.ResponsiveUI.Editor
{
    /// <summary>
    /// Project Settings › Responsive UI › Android Flip: the half-open card for a Flip
    /// (<see cref="FoldableHalfOpenRules.flip"/>), saved by <see cref="FoldableEditorRules"/>.
    /// </summary>
    static class AndroidFlipSettingsProvider
    {
        public const string Path = "Project/Responsive UI/Android Flip";

        static Vector2 s_Scroll;
        static bool s_FlipExpanded = true;

        [SettingsProvider]
        public static SettingsProvider Create() =>
            new SettingsProvider(Path, SettingsScope.Project)
            {
                label = "Android Flip",
                guiHandler = _ => Draw(),
                keywords = new[]
                {
                    "Android", "Flip", "Galaxy Z Flip", "half opened", "flex", "foldable", "hinge",
                    "content runs", "while rotating", "orientation", "design canvas",
                },
            };

        static void Draw()
        {
            FoldableHalfOpenRules rules = FoldableEditorRules.Rules;
            s_Scroll = EditorGUILayout.BeginScrollView(s_Scroll);

            EditorGUILayout.HelpBox(
                "How the game behaves while an Android Flip is half opened. The defaults are the platform's own " +
                "(official Android / Samsung behaviour); a changed card makes the game hold its own orientation for " +
                "that pose on the phone. The card applies under Default Orientation = Auto Rotation with more than one " +
                "orientation allowed.\n" +
                $"Saved to {FoldableSettings.AssetPath}.",
                MessageType.Info);
            PlayerAxesCase axes = AndroidHalfOpenCardGui.ReadPlayerAxes();
            ResponsivePoseCard.DrawBanner(axes);
            EditorGUILayout.Space(4);

            if (AndroidHalfOpenCardGui.Draw(
                    "Half opened", "Clamshell bent part-way open, standing like a small laptop.", rules.flip,
                    ResponsiveDevice.FormFactor.FlipHalfOpened, false, axes, ref s_FlipExpanded))
                FoldableEditorRules.Save();

            AndroidHalfOpenCardGui.DrawFooter("Android Flip", () => rules.flip = new FoldableHalfOpenPose());

            EditorGUILayout.EndScrollView();
        }
    }
}
