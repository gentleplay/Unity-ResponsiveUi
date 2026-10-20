using System.Collections.Generic;
using System.IO;
using FDPlus.Foldable;
using FDPlus.Foldable.Editor;
using FDPlus.ResponsiveUI.Shared.Editor;
using NUnit.Framework;
using UnityEditor;

using static FDPlus.ResponsiveUI.Tests.ResponsiveEditorTestUtil;

namespace FDPlus.ResponsiveUI.Tests
{
    /// <summary>
    /// Pose-card settings pages: the Player Settings case, which fields a case shows, and the Android
    /// half-open rules round trip through <see cref="FoldableEditorRules"/>.
    /// </summary>
    public class ResponsivePoseCardTests
    {
        #region Player Settings

        [Test]
        public void ReadPlayerAxes_Cases()
        {
            WithPlayerAxes(false, true, () =>
                Assert.AreEqual(PlayerAxesCase.LandscapeOnly, ResponsivePoseCard.ReadPlayerAxes()));
            WithPlayerAxes(true, false, () =>
                Assert.AreEqual(PlayerAxesCase.PortraitOnly, ResponsivePoseCard.ReadPlayerAxes()));
            WithPlayerAxes(true, true, () =>
                Assert.AreEqual(PlayerAxesCase.Both, ResponsivePoseCard.ReadPlayerAxes()));
            WithPlayerAxes(false, true, () =>
            {
                PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
                Assert.AreEqual(PlayerAxesCase.Fixed, ResponsivePoseCard.ReadPlayerAxes());
            });
            Assert.AreEqual(PlayerAxesCase.Fixed, ResponsivePoseCard.ReadPlayerAxes(UIOrientation.Portrait, true, true));
        }

        // Android pages: Auto Rotation with one orientation allowed is fixed on the phone (R19); iPhone Duo keeps
        // the shared reading above.
        [Test]
        public void AndroidReadPlayerAxes_OneOrientationIsFixed()
        {
            Assert.AreEqual(PlayerAxesCase.Fixed, FDPlus.ResponsiveUI.Editor.AndroidHalfOpenCardGui.ReadPlayerAxes(PlayerAxesCase.PortraitOnly, false));
            Assert.AreEqual(PlayerAxesCase.PortraitOnly, FDPlus.ResponsiveUI.Editor.AndroidHalfOpenCardGui.ReadPlayerAxes(PlayerAxesCase.PortraitOnly, true));
            WithPlayerAxes(true, false, () =>
            {
                Assert.AreEqual(PlayerAxesCase.PortraitOnly, ResponsivePoseCard.ReadPlayerAxes(), "shared reading");
                Assert.AreEqual(PlayerAxesCase.Fixed, FDPlus.ResponsiveUI.Editor.AndroidHalfOpenCardGui.ReadPlayerAxes(), "Portrait only ticked");
                PlayerSettings.allowedAutorotateToPortraitUpsideDown = true;
                Assert.AreEqual(PlayerAxesCase.PortraitOnly, FDPlus.ResponsiveUI.Editor.AndroidHalfOpenCardGui.ReadPlayerAxes(), "both portraits");
            });
            WithPlayerAxes(false, true, () =>
                Assert.AreEqual(PlayerAxesCase.LandscapeOnly, FDPlus.ResponsiveUI.Editor.AndroidHalfOpenCardGui.ReadPlayerAxes(), "both landscapes"));
            WithPlayerAxes(true, true, () =>
                Assert.AreEqual(PlayerAxesCase.Both, FDPlus.ResponsiveUI.Editor.AndroidHalfOpenCardGui.ReadPlayerAxes()));
        }

        #endregion

        #region Fields

        [Test]
        public void VisibleFields_HidesWhatTheCaseCannotReach()
        {
            foreach (var axes in new[] { PlayerAxesCase.LandscapeOnly, PlayerAxesCase.PortraitOnly })
            {
                // The Player lock gives one shape: one row.
                var fixedByPlayer = Fields(axes, true, true);
                Assert.Contains(ResponsivePoseCard.FieldCanvasRow, fixedByPlayer, axes + ": one canvas row");
                CollectionAssert.DoesNotContain(fixedByPlayer, ResponsivePoseCard.FieldCanvasSecondRow, axes + ": no second row");
                Assert.Contains(ResponsivePoseCard.FieldWhileRotatingFixed, fixedByPlayer, axes + ": rotating read-only");
                CollectionAssert.DoesNotContain(fixedByPlayer, ResponsivePoseCard.FieldWhileRotating, axes.ToString());
                Assert.Contains(ResponsivePoseCard.FieldContentRuns, fixedByPlayer, axes + ": content runs editable");

                var changed = Fields(axes, false, true);
                Assert.Contains(ResponsivePoseCard.FieldWhileRotating, changed, axes + ": changed rotating editable");
                CollectionAssert.DoesNotContain(changed, ResponsivePoseCard.FieldCanvasSecondRow, axes.ToString());

                // Follow device on a one-axis game: the phone gives both shapes, so both rows.
                var follows = Fields(axes, false, false);
                Assert.Contains(ResponsivePoseCard.FieldCanvasRow, follows, axes + ": follow device first row");
                Assert.Contains(ResponsivePoseCard.FieldCanvasSecondRow, follows, axes + ": follow device second row");
            }

            var both = Fields(PlayerAxesCase.Both, false);
            Assert.Contains(ResponsivePoseCard.FieldCanvasRow, both);
            Assert.Contains(ResponsivePoseCard.FieldCanvasSecondRow, both, "both axes: two rows");
            Assert.Contains(ResponsivePoseCard.FieldWhileRotating, both);
            CollectionAssert.DoesNotContain(Fields(PlayerAxesCase.Both, false, true),
                ResponsivePoseCard.FieldCanvasSecondRow, "one shape: one row");

            var fixedOrientation = Fields(PlayerAxesCase.Fixed, false, true);
            Assert.Contains(ResponsivePoseCard.FieldContentRunsFixed, fixedOrientation);
            Assert.Contains(ResponsivePoseCard.FieldWhileRotatingFixed, fixedOrientation);
            CollectionAssert.DoesNotContain(fixedOrientation, ResponsivePoseCard.FieldContentRuns);
            CollectionAssert.DoesNotContain(fixedOrientation, ResponsivePoseCard.FieldWhileRotating);
            CollectionAssert.DoesNotContain(fixedOrientation, ResponsivePoseCard.FieldCanvasSecondRow);
        }

        static List<string> Fields(PlayerAxesCase axes, bool fixedByPlayer, bool oneShape = false) =>
            new List<string>(ResponsivePoseCard.VisibleFields(axes, fixedByPlayer, oneShape));

        #endregion

        #region Storage

        [Test]
        public void FoldableEditorRules_SaveReload_RoundTrip()
        {
            string path = Path.Combine(Path.GetTempPath(), $"FoldableSettings-{System.Guid.NewGuid():N}.json");
            string original = FoldableEditorRules.FilePath;
            try
            {
                FoldableEditorRules.FilePath = path;
                FoldableEditorRules.Reload();
                Assert.IsTrue(FoldableEditorRules.Rules.fold.IsDefault, "no file: defaults");

                FoldableEditorRules.Rules.foldCover.contentRuns = FoldableContentRuns.LeftToRight;
                FoldableEditorRules.Rules.flip.whileRotating = FoldableHalfOpenRotation.Locked;
                FoldableEditorRules.Save();
                Assert.AreEqual(FoldableContentRuns.LeftToRight, FoldableSettings.Rules.foldCover.contentRuns, "runtime rules updated");

                FoldableEditorRules.Reload();
                Assert.AreEqual(FoldableContentRuns.LeftToRight, FoldableEditorRules.Rules.foldCover.contentRuns);
                Assert.AreEqual(FoldableHalfOpenRotation.Locked, FoldableEditorRules.Rules.flip.whileRotating);
                Assert.IsTrue(FoldableEditorRules.Rules.fold.IsDefault);

                FoldableEditorRules.ResetToDefaults();
                FoldableEditorRules.Reload();
                Assert.IsTrue(FoldableEditorRules.Rules.foldCover.IsDefault && FoldableEditorRules.Rules.flip.IsDefault, "reset");
            }
            finally
            {
                FoldableEditorRules.FilePath = original;
                FoldableEditorRules.Reload();
                if (File.Exists(path))
                    File.Delete(path);
            }
        }

        #endregion
    }
}
