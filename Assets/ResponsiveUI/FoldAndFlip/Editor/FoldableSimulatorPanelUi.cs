using UnityEngine;
using UnityEngine.UIElements;

namespace FDPlus.Foldable.Editor
{
    /// <summary>
    /// Plumbing shared by the Android Fold &amp; Flip and iPhone Duo Device Simulator panels.
    /// </summary>
    public static class FoldableSimulatorPanelUi
    {
        #region Constants

        static readonly Color SelectedColor = new Color(0.55f, 0.88f, 0.55f, 0.5f);

        #endregion

        #region GUI

        public static Label CreateMutedLabel(Color color)
        {
            var label = new Label();
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.marginBottom = 4;
            label.style.color = new StyleColor(color);
            return label;
        }

        /// <summary>Highlights the posture button of the active product state.</summary>
        public static void StyleSelected(Button button, bool selected)
        {
            button.style.backgroundColor = selected
                ? new StyleColor(SelectedColor)
                : new StyleColor(StyleKeyword.Null);
            button.style.unityFontStyleAndWeight = selected
                ? FontStyle.Bold
                : FontStyle.Normal;
        }

        /// <summary>Screen orientation field tooltip: the Player lock, or why the field is editable.</summary>
        public static string OrientationTooltip(bool locked) =>
            locked
                ? "Locked to Player Settings → " + FoldablePlayerOrientation.DescribeProjectOrientation()
                : "Device rotation. Editable because Player Settings allow Auto Rotation (portrait + landscape).";

        /// <summary>
        /// Screen orientation field tooltip while an Android Fold / Flip Half Opened holds its pose
        /// with both Player axes allowed (its card in Project Settings › Responsive UI is changed).
        /// </summary>
        public const string HalfOpenHeldOrientationTooltip =
            "Held while Half Opened by the Android Fold / Flip card in Project Settings › Responsive UI: " +
            "rotating does not change the pose. E.g. a Locked card keeps the screen it entered with; " +
            "pick Tabletop (Landscape) or Book (Portrait), or fold to Expand / Normal to rotate.";

        /// <summary>
        /// Text shown in place of a panel's controls: the build-target hint when the panel's
        /// platform is inactive, the select-device hint while nothing is bound (or a selection is
        /// pending), else the hint naming the other platform's panel.
        /// </summary>
        public static string UnavailableHint(
            bool platformActive,
            string buildTargetHint,
            string selectDeviceHint,
            string otherPanelHint)
        {
            if (!platformActive)
                return buildTargetHint;
            if (SimulatorDeviceBridge.IsSelectionPending || !FoldableEditorSession.IsFoldableDeviceBound)
                return selectDeviceHint;
            return otherPanelHint;
        }

        #endregion

        #region Session

        /// <summary>Binds the session to the current Simulator selection (panel created).</summary>
        public static void RefreshSessionBinding() =>
            FoldableEditorSessionWatcher.RefreshNow();

        #endregion
    }
}
