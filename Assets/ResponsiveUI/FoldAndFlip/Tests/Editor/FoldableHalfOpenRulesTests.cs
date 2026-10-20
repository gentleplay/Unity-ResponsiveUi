using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace FDPlus.Foldable.Tests
{
    public class FoldableHalfOpenRulesTests
    {
        static FoldableHalfOpenPose Pose(FoldableContentRuns runs = FoldableContentRuns.AsAndroid,
            FoldableHalfOpenRotation rotation = FoldableHalfOpenRotation.AsAndroid) =>
            new FoldableHalfOpenPose { contentRuns = runs, whileRotating = rotation };

        [Test]
        public void Defaults_AreAsAndroid_AndJsonRoundTrips()
        {
            var rules = new FoldableHalfOpenRules();
            Assert.IsTrue(rules.fold.IsDefault);
            Assert.IsTrue(rules.foldCover.IsDefault);
            Assert.IsTrue(rules.flip.IsDefault);

            FoldableSettings.FromJson("{}", rules);
            Assert.IsTrue(rules.fold.IsDefault && rules.foldCover.IsDefault && rules.flip.IsDefault);

            rules.foldCover.contentRuns = FoldableContentRuns.LeftToRight;
            rules.flip.whileRotating = FoldableHalfOpenRotation.Locked;
            var back = new FoldableHalfOpenRules();
            FoldableSettings.FromJson(FoldableSettings.ToJson(rules), back);
            Assert.AreEqual(FoldableContentRuns.LeftToRight, back.foldCover.contentRuns);
            Assert.AreEqual(FoldableHalfOpenRotation.Locked, back.flip.whileRotating);
            Assert.IsTrue(back.fold.IsDefault);
        }

        // A corrupt settings file must not throw on every posture change: defaults and one warning.
        [Test]
        public void FromJson_InvalidOrPartial_FallsBackToDefaults()
        {
            var rules = new FoldableHalfOpenRules();
            rules.fold.contentRuns = FoldableContentRuns.LeftToRight;
            LogAssert.Expect(LogType.Warning, new Regex("not valid JSON"));
            Assert.DoesNotThrow(() => FoldableSettings.FromJson("{ \"fold\": { contentRuns: ", rules));
            Assert.IsTrue(rules.fold.IsDefault && rules.foldCover.IsDefault && rules.flip.IsDefault, "invalid: defaults");

            var partial = new FoldableHalfOpenRules();
            FoldableSettings.FromJson("{\"flip\":{\"whileRotating\":1}}", partial);
            Assert.IsTrue(partial.fold.IsDefault, "fold default");
            Assert.IsTrue(partial.foldCover.IsDefault, "foldCover default");
            Assert.AreEqual(FoldableContentRuns.AsAndroid, partial.flip.contentRuns);
            Assert.AreEqual(FoldableHalfOpenRotation.Locked, partial.flip.whileRotating);
        }

        [Test]
        public void For_PicksPoseByKind()
        {
            var rules = new FoldableHalfOpenRules();
            Assert.AreSame(rules.flip, rules.For(FoldableDeviceKind.FlipablePhone, false));
            Assert.AreSame(rules.fold, rules.For(FoldableDeviceKind.FoldablePhone, false));
            Assert.AreSame(rules.foldCover, rules.For(FoldableDeviceKind.FoldablePhone, true));
        }

        [TestCase(true, true, false, false)]
        [TestCase(true, false, false, true)]
        [TestCase(true, null, false, null)]
        [TestCase(false, true, false, true)]
        [TestCase(false, null, false, null)]
        public void ScreenLandscape_DefaultPose(bool isFold, bool? locked, bool entry, bool? expected)
        {
            Assert.AreEqual(expected, FoldableHalfOpenRules.ScreenLandscape(Pose(), isFold, locked, entry));
        }

        [Test]
        public void ScreenLandscape_ContentRunsWins()
        {
            foreach (var isFold in new[] { true, false })
            foreach (var locked in new bool?[] { true, false, null })
            {
                Assert.AreEqual(false, FoldableHalfOpenRules.ScreenLandscape(Pose(FoldableContentRuns.TopToDown), isFold, locked, true));
                Assert.AreEqual(true, FoldableHalfOpenRules.ScreenLandscape(Pose(FoldableContentRuns.LeftToRight), isFold, locked, false));
            }
        }

        [Test]
        public void ScreenLandscape_ExplicitLockedBothAxes_KeepsEntry()
        {
            var locked = Pose(rotation: FoldableHalfOpenRotation.Locked);
            Assert.AreEqual(true, FoldableHalfOpenRules.ScreenLandscape(locked, true, null, true));
            Assert.AreEqual(false, FoldableHalfOpenRules.ScreenLandscape(locked, true, null, false));
            var follow = Pose(rotation: FoldableHalfOpenRotation.FollowDevice);
            Assert.IsNull(FoldableHalfOpenRules.ScreenLandscape(follow, true, true, true));
            Assert.IsNull(FoldableHalfOpenRules.ScreenLandscape(follow, false, false, false));
        }

        [Test]
        public void NeedsRuntimeHold_OnlyForChangedCards()
        {
            foreach (var locked in new bool?[] { true, false, null })
                Assert.IsFalse(FoldableHalfOpenRules.NeedsRuntimeHold(Pose(), locked));
            Assert.IsTrue(FoldableHalfOpenRules.NeedsRuntimeHold(Pose(FoldableContentRuns.LeftToRight), true));
            Assert.IsTrue(FoldableHalfOpenRules.NeedsRuntimeHold(Pose(rotation: FoldableHalfOpenRotation.Locked), null));
            Assert.IsTrue(FoldableHalfOpenRules.NeedsRuntimeHold(Pose(rotation: FoldableHalfOpenRotation.FollowDevice), true));
            Assert.IsFalse(FoldableHalfOpenRules.NeedsRuntimeHold(Pose(rotation: FoldableHalfOpenRotation.Locked), true));
        }
    }
}
