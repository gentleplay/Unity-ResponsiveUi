using FDPlus.Foldable;
using FDPlus.IphoneDuo.Editor;
using NUnit.Framework;
using UnityEngine;

namespace FDPlus.IphoneDuo.Tests
{
    public class IphoneDuoTests
    {
        static IphoneDuoAutorotateFlags Flags(
            bool portrait, bool portraitDown, bool landscapeLeft, bool landscapeRight) =>
            new IphoneDuoAutorotateFlags(portrait, portraitDown, landscapeLeft, landscapeRight);

        static readonly IphoneDuoAutorotateFlags LandscapeOnly = Flags(false, false, true, true);
        static readonly IphoneDuoAutorotateFlags PortraitOnly = Flags(true, false, false, false);
        static readonly IphoneDuoAutorotateFlags TallHold = Flags(true, false, false, false);
        // Sideways allows both portraits so iOS never has to turn 180°.
        static readonly IphoneDuoAutorotateFlags TallAxis = Flags(true, true, false, false);

        const IphoneDuoPosture Cover = IphoneDuoPosture.Cover;
        const IphoneDuoPosture Seated = IphoneDuoPosture.Seated;
        const IphoneDuoPosture Flat = IphoneDuoPosture.Flat;
        // Inner tall holds: Landscape Left is upright Portrait, Landscape Right upright Upside Down.
        const DeviceOrientation Laptop = DeviceOrientation.LandscapeLeft;
        const DeviceOrientation LaptopFlipped = DeviceOrientation.LandscapeRight;
        const DeviceOrientation Book = DeviceOrientation.Portrait;
        const DeviceOrientation BookFlipped = DeviceOrientation.PortraitUpsideDown;

        static IphoneDuoRules Defaults => new IphoneDuoRules();
        static IphoneDuoRules FollowsHold => With(r => r.seatedRotation = IphoneDuoSeatedRotation.FollowsHold);
        static readonly IphoneDuoAutorotateFlags BothAxes = Flags(true, true, true, true);

        static IphoneDuoRules With(System.Action<IphoneDuoRules> edit)
        {
            var rules = new IphoneDuoRules();
            edit(rules);
            return rules;
        }

        #region Rules

        [Test]
        public void Rules_DefaultsAndJsonRoundTrip()
        {
            var rules = Defaults;
            Assert.AreEqual(IphoneDuoAcrossHold.SystemDefault, rules.coverAcrossLock);
            Assert.AreEqual(IphoneDuoAcrossHold.Rotate, rules.seatedAcrossLock);
            Assert.AreEqual(IphoneDuoAcrossHold.Sideways, rules.flatAcrossLock);
            Assert.IsTrue(rules.flatFlip180);
            Assert.AreEqual(1, rules.SidewaysQuarterTurns, "Left 90 from the upright interface");
            Assert.AreEqual(-1, With(r => r.sidewaysTurn = IphoneDuoSidewaysTurn.Right90).SidewaysQuarterTurns);
            Assert.IsFalse(rules.launchScreenFillsAcrossHold, "the Info.plist change is opt-in");

            var edited = With(r =>
            {
                r.flatAcrossLock = IphoneDuoAcrossHold.Rotate;
                r.flatFlip180 = false;
                r.lockSettleSeconds = 1.25f;
                r.launchScreenFillsAcrossHold = true;
            });
            var loaded = Defaults;
            IphoneDuoSettings.FromJson(IphoneDuoSettings.ToJson(edited), loaded);
            Assert.AreEqual(IphoneDuoAcrossHold.Rotate, loaded.flatAcrossLock);
            Assert.IsFalse(loaded.flatFlip180);
            Assert.AreEqual(1.25f, loaded.lockSettleSeconds);
            Assert.IsTrue(loaded.launchScreenFillsAcrossHold);

            // Fields missing from the JSON keep their defaults.
            var partial = Defaults;
            IphoneDuoSettings.FromJson("{\"seatedAcrossLock\": 0}", partial);
            Assert.AreEqual(IphoneDuoAcrossHold.Sideways, partial.seatedAcrossLock);
            Assert.AreEqual(IphoneDuoAcrossHold.Sideways, partial.flatAcrossLock);
            Assert.AreEqual(IphoneDuoRules.DefaultLeaveSeatedDelaySeconds, partial.leaveSeatedDelaySeconds);

            var clone = edited.Clone();
            clone.flatFlip180 = true;
            Assert.IsFalse(edited.flatFlip180, "Clone is independent");
        }

        [Test]
        public void Rules_SeatedRotation_DefaultsAndJsonRoundTrip()
        {
            var rules = Defaults;
            Assert.AreEqual(IphoneDuoSeatedRotation.Locked, rules.seatedRotation);
            Assert.AreEqual(IphoneDuoSeatedPose.Auto, rules.seatedLockedPose);

            var edited = With(r =>
            {
                r.seatedRotation = IphoneDuoSeatedRotation.FollowsHold;
                r.seatedLockedPose = IphoneDuoSeatedPose.SeatedLandscape;
            });
            var loaded = Defaults;
            IphoneDuoSettings.FromJson(IphoneDuoSettings.ToJson(edited), loaded);
            Assert.AreEqual(IphoneDuoSeatedRotation.FollowsHold, loaded.seatedRotation);
            Assert.AreEqual(IphoneDuoSeatedPose.SeatedLandscape, loaded.seatedLockedPose);

            // A settings file saved before these rules keeps the defaults.
            var old = Defaults;
            IphoneDuoSettings.FromJson("{\"seatedAcrossLock\": 1}", old);
            Assert.AreEqual(IphoneDuoSeatedRotation.Locked, old.seatedRotation);
            Assert.AreEqual(IphoneDuoSeatedPose.Auto, old.seatedLockedPose);
        }

        [Test]
        public void SeatedHoldTall_EverySettingPlayerAxesAndHold()
        {
            var both = Flags(true, true, true, true);
            foreach (bool entryTall in new[] { true, false })
            foreach (bool holdTall in new[] { true, false })
            {
                string at = $"entry tall {entryTall}, hold tall {holdTall}";
                foreach (var project in new[] { LandscapeOnly, PortraitOnly, both })
                {
                    // Follows hold: today's behaviour, the current hold whatever the Player lock or pose.
                    foreach (IphoneDuoSeatedPose pose in System.Enum.GetValues(typeof(IphoneDuoSeatedPose)))
                    {
                        Assert.AreEqual(holdTall, IphoneDuoRules.SeatedHoldTall(
                            IphoneDuoSeatedRotation.FollowsHold, pose, project, entryTall, holdTall), at);
                    }

                    Assert.IsTrue(IphoneDuoRules.SeatedHoldTall(
                        IphoneDuoSeatedRotation.Locked, IphoneDuoSeatedPose.SeatedPortrait, project, entryTall, holdTall), at);
                    Assert.IsFalse(IphoneDuoRules.SeatedHoldTall(
                        IphoneDuoSeatedRotation.Locked, IphoneDuoSeatedPose.SeatedLandscape, project, entryTall, holdTall), at);
                }

                // Auto: landscape-only → Seated Portrait (tall laptop), portrait-only → Seated Landscape
                // (wide book), both axes → the hold Seated was entered with.
                Assert.IsTrue(IphoneDuoRules.SeatedHoldTall(
                    IphoneDuoSeatedRotation.Locked, IphoneDuoSeatedPose.Auto, LandscapeOnly, entryTall, holdTall), at);
                Assert.IsFalse(IphoneDuoRules.SeatedHoldTall(
                    IphoneDuoSeatedRotation.Locked, IphoneDuoSeatedPose.Auto, PortraitOnly, entryTall, holdTall), at);
                Assert.AreEqual(entryTall, IphoneDuoRules.SeatedHoldTall(
                    IphoneDuoSeatedRotation.Locked, IphoneDuoSeatedPose.Auto, both, entryTall, holdTall), at);
            }

            // The instance overload reads the rules' own settings.
            Assert.IsTrue(Defaults.SeatedHoldTall(LandscapeOnly, entryTall: false, holdTall: false));
            Assert.IsFalse(With(r => r.seatedRotation = IphoneDuoSeatedRotation.FollowsHold)
                .SeatedHoldTall(LandscapeOnly, entryTall: true, holdTall: false));
        }

        static IphoneDuoRules FlatLocked(IphoneDuoFlatPose pose = IphoneDuoFlatPose.Auto, System.Action<IphoneDuoRules> edit = null) =>
            With(r =>
            {
                r.flatRotation = IphoneDuoFlatRotation.Locked;
                r.flatLockedPose = pose;
                edit?.Invoke(r);
            });

        [Test]
        public void Rules_FlatRotation_DefaultsAndJsonRoundTrip()
        {
            var rules = Defaults;
            Assert.AreEqual(IphoneDuoFlatRotation.FollowDevice, rules.flatRotation, "the Flat lock is off by default");
            Assert.AreEqual(IphoneDuoFlatPose.Auto, rules.flatLockedPose);
            Assert.IsFalse(rules.FlatKeepsHold(BothAxes));
            Assert.AreEqual(IphoneDuoAcrossHold.Sideways, rules.FlatAcrossLockFor(LandscapeOnly));

            var loaded = Defaults;
            IphoneDuoSettings.FromJson(IphoneDuoSettings.ToJson(FlatLocked(IphoneDuoFlatPose.Tall)), loaded);
            Assert.AreEqual(IphoneDuoFlatRotation.Locked, loaded.flatRotation);
            Assert.AreEqual(IphoneDuoFlatPose.Tall, loaded.flatLockedPose);

            // A settings file saved before these rules (or an empty one) keeps the defaults.
            foreach (string json in new[] { "{}", "{\"flatAcrossLock\": 1}" })
            {
                var old = Defaults;
                IphoneDuoSettings.FromJson(json, old);
                Assert.AreEqual(IphoneDuoFlatRotation.FollowDevice, old.flatRotation, json);
                Assert.AreEqual(IphoneDuoFlatPose.Auto, old.flatLockedPose, json);
            }
        }

        [Test]
        public void FlatHoldWide_EverySettingPlayerAxesAndHold()
        {
            foreach (bool entryWide in new[] { true, false })
            foreach (bool holdWide in new[] { true, false })
            {
                string at = $"entry wide {entryWide}, hold wide {holdWide}";
                foreach (var project in new[] { LandscapeOnly, PortraitOnly, BothAxes })
                {
                    // Follow device: today's behaviour, the current hold whatever the Player lock or pose.
                    foreach (IphoneDuoFlatPose pose in System.Enum.GetValues(typeof(IphoneDuoFlatPose)))
                    {
                        Assert.AreEqual(holdWide, IphoneDuoRules.FlatHoldWide(
                            IphoneDuoFlatRotation.FollowDevice, pose, project, entryWide, holdWide), at);
                    }

                    Assert.IsTrue(IphoneDuoRules.FlatHoldWide(
                        IphoneDuoFlatRotation.Locked, IphoneDuoFlatPose.Wide, project, entryWide, holdWide), at);
                    Assert.IsFalse(IphoneDuoRules.FlatHoldWide(
                        IphoneDuoFlatRotation.Locked, IphoneDuoFlatPose.Tall, project, entryWide, holdWide), at);
                }

                // Auto: landscape-only → wide, portrait-only → tall, both axes → the hold Flat was entered with.
                Assert.IsTrue(IphoneDuoRules.FlatHoldWide(
                    IphoneDuoFlatRotation.Locked, IphoneDuoFlatPose.Auto, LandscapeOnly, entryWide, holdWide), at);
                Assert.IsFalse(IphoneDuoRules.FlatHoldWide(
                    IphoneDuoFlatRotation.Locked, IphoneDuoFlatPose.Auto, PortraitOnly, entryWide, holdWide), at);
                Assert.AreEqual(entryWide, IphoneDuoRules.FlatHoldWide(
                    IphoneDuoFlatRotation.Locked, IphoneDuoFlatPose.Auto, BothAxes, entryWide, holdWide), at);
            }

            // The instance overload reads the rules' own settings.
            Assert.IsFalse(Defaults.FlatHoldWide(LandscapeOnly, entryWide: true, holdWide: false));
            Assert.IsTrue(FlatLocked().FlatHoldWide(LandscapeOnly, entryWide: false, holdWide: false));

            // A hold of its own: both axes, or a one-axis pose across the lock. On the game's own axis
            // Rotate keeps the game sideways (a Locked Flat never rotates).
            Assert.IsTrue(FlatLocked().FlatKeepsHold(BothAxes));
            Assert.IsFalse(FlatLocked().FlatKeepsHold(LandscapeOnly));
            Assert.IsTrue(FlatLocked(IphoneDuoFlatPose.Tall).FlatKeepsHold(LandscapeOnly));
            Assert.IsFalse(FlatLocked(IphoneDuoFlatPose.Tall).FlatKeepsHold(PortraitOnly));
            var rotate = FlatLocked(edit: r => r.flatAcrossLock = IphoneDuoAcrossHold.Rotate);
            Assert.AreEqual(IphoneDuoAcrossHold.Sideways, rotate.FlatAcrossLockFor(LandscapeOnly));
            Assert.AreEqual(IphoneDuoAcrossHold.Rotate, rotate.FlatAcrossLockFor(BothAxes));
            rotate.flatLockedPose = IphoneDuoFlatPose.Tall;
            Assert.AreEqual(IphoneDuoAcrossHold.Rotate, rotate.FlatAcrossLockFor(LandscapeOnly), "the tall pose rotates to it");
            Assert.AreEqual(IphoneDuoAcrossHold.SystemDefault,
                FlatLocked(edit: r => r.flatAcrossLock = IphoneDuoAcrossHold.SystemDefault).FlatAcrossLockFor(LandscapeOnly));
        }

        [Test]
        public void HoldFlags_InnerTurned90_CoverLikeAnIphone()
        {
            // On the inner display iOS reports the tall laptop hold as a landscape device orientation.
            Assert.IsTrue(IphoneDuoHoldOrientation.HoldFlags(Seated, Laptop, out var laptop));
            Assert.AreEqual(Flags(true, false, false, false), laptop);
            // The flipped laptop's upright interface is Upside Down, which iOS never gives an iPhone app
            // window: Portrait, and the view turn makes up for it.
            Assert.IsTrue(IphoneDuoHoldOrientation.HoldFlags(Flat, LaptopFlipped, out var flipped));
            Assert.AreEqual(laptop, flipped);
            Assert.IsTrue(IphoneDuoHoldOrientation.HoldFlags(Seated, Book, out var book));
            Assert.AreEqual(LandscapeOnly, book);
            Assert.IsTrue(IphoneDuoHoldOrientation.HoldFlags(Flat, BookFlipped, out var bookFlipped));
            Assert.AreEqual(book, bookFlipped);
            Assert.IsFalse(IphoneDuoHoldOrientation.HoldFlags(Seated, DeviceOrientation.FaceUp, out _));
            Assert.IsFalse(IphoneDuoHoldOrientation.HoldFlags(Flat, DeviceOrientation.Unknown, out _));

            Assert.IsTrue(IphoneDuoHoldOrientation.HoldFlags(Cover, DeviceOrientation.Portrait, out var coverTall));
            Assert.AreEqual(Flags(true, false, false, false), coverTall);
            Assert.IsTrue(IphoneDuoHoldOrientation.HoldFlags(Cover, DeviceOrientation.PortraitUpsideDown, out var coverUpsideDown));
            Assert.AreEqual(coverTall, coverUpsideDown, "an iPhone app stays Portrait held upside down");
            Assert.IsTrue(IphoneDuoHoldOrientation.HoldFlags(Cover, DeviceOrientation.LandscapeLeft, out var coverWide));
            Assert.AreEqual(Flags(false, false, true, false), coverWide);

            Assert.AreEqual(IphoneDuoHold.Tall, IphoneDuoHoldOrientation.Hold(Flat, Laptop));
            Assert.AreEqual(IphoneDuoHold.Wide, IphoneDuoHoldOrientation.Hold(Seated, Book));
            Assert.AreEqual(IphoneDuoHold.Tall, IphoneDuoHoldOrientation.Hold(Cover, DeviceOrientation.Portrait));
            Assert.AreEqual(IphoneDuoHold.Unknown, IphoneDuoHoldOrientation.Hold(Flat, DeviceOrientation.FaceDown));
        }

        [Test]
        public void Resolve_EachPostureFollowsItsAcrossLockRule()
        {
            var rules = Defaults;
            // Across a landscape lock (held tall).
            Assert.AreEqual(IphoneDuoScreenMode.HoldAxis, IphoneDuoHoldOrientation.Resolve(Seated, TallHold, LandscapeOnly, false, rules));
            Assert.AreEqual(IphoneDuoScreenMode.Sideways, IphoneDuoHoldOrientation.Resolve(Flat, TallHold, LandscapeOnly, false, rules));
            Assert.AreEqual(IphoneDuoScreenMode.Native, IphoneDuoHoldOrientation.Resolve(Flat, TallHold, LandscapeOnly, true, rules),
                "the flat lock already keeps the game sideways");
            Assert.AreEqual(IphoneDuoScreenMode.Native, IphoneDuoHoldOrientation.Resolve(Cover, TallHold, LandscapeOnly, false, rules));

            Assert.AreEqual(IphoneDuoScreenMode.Sideways, IphoneDuoHoldOrientation.Resolve(Seated, TallHold, LandscapeOnly, false,
                With(r => r.seatedAcrossLock = IphoneDuoAcrossHold.Sideways)));
            Assert.AreEqual(IphoneDuoScreenMode.Native, IphoneDuoHoldOrientation.Resolve(Seated, TallHold, LandscapeOnly, false,
                With(r => r.seatedAcrossLock = IphoneDuoAcrossHold.SystemDefault)));
            Assert.AreEqual(IphoneDuoScreenMode.HoldAxis, IphoneDuoHoldOrientation.Resolve(Flat, TallHold, LandscapeOnly, false,
                With(r => r.flatAcrossLock = IphoneDuoAcrossHold.Rotate)));
            Assert.AreEqual(IphoneDuoScreenMode.Native, IphoneDuoHoldOrientation.Resolve(Flat, TallHold, LandscapeOnly, false,
                With(r => r.flatAcrossLock = IphoneDuoAcrossHold.SystemDefault)));
            Assert.AreEqual(IphoneDuoScreenMode.HoldAxis, IphoneDuoHoldOrientation.Resolve(Cover, TallHold, LandscapeOnly, false,
                With(r => r.coverAcrossLock = IphoneDuoAcrossHold.Rotate)));
            Assert.AreEqual(IphoneDuoScreenMode.Native, IphoneDuoHoldOrientation.Resolve(Cover, TallHold, LandscapeOnly, false,
                With(r => r.coverAcrossLock = IphoneDuoAcrossHold.Sideways)), "the cover shows it sideways natively");

            // Holds on the game's axis keep the Player lock; Seated rotates a two-axis game to the hold.
            Assert.AreEqual(IphoneDuoScreenMode.Native, IphoneDuoHoldOrientation.Resolve(Seated, LandscapeOnly, LandscapeOnly, false, rules));
            Assert.AreEqual(IphoneDuoScreenMode.Native, IphoneDuoHoldOrientation.Resolve(Flat, TallHold, PortraitOnly, false, rules));
            var both = Flags(true, false, true, true);
            Assert.AreEqual(IphoneDuoScreenMode.HoldAxis, IphoneDuoHoldOrientation.Resolve(Seated, TallHold, both, false, rules));
            Assert.AreEqual(IphoneDuoScreenMode.Native, IphoneDuoHoldOrientation.Resolve(Flat, TallHold, both, false, rules));
            Assert.AreEqual(IphoneDuoScreenMode.Native, IphoneDuoHoldOrientation.Resolve(IphoneDuoPosture.Unknown, TallHold, LandscapeOnly, false, rules));

            // A portrait game held wide is the mirror case.
            Assert.AreEqual(IphoneDuoScreenMode.Sideways, IphoneDuoHoldOrientation.Resolve(Flat, LandscapeOnly, PortraitOnly, false, rules));
        }

        #endregion

        #region Hold lock

        [Test]
        public void HoldLock_SeatedLaptop_AllowsOnlyPortrait_SoIosRotates()
        {
            var hold = new IphoneDuoHoldLock();
            var rules = FollowsHold;
            var laptop = hold.Step(Seated, Laptop, LandscapeOnly, false, 0f, rules);
            Assert.IsTrue(laptop.writeFlags);
            Assert.AreEqual(TallHold, laptop.flags, "landscape must be off or iOS stays letterboxed");
            Assert.AreEqual(IphoneDuoScreenMode.HoldAxis, hold.Mode);

            // Same hold: nothing to write. The live flags are the hold's now and must not be re-captured.
            Assert.IsFalse(hold.Step(Seated, Laptop, laptop.flags, false, 0.1f, rules).writeFlags);

            // Follows hold: book hold, back to the project's flags; laptop again: portrait.
            var book = hold.Step(Seated, Book, laptop.flags, false, 1f, rules);
            Assert.IsTrue(book.writeFlags);
            Assert.AreEqual(LandscapeOnly, book.flags);
            Assert.AreEqual(TallHold, hold.Step(Seated, Laptop, book.flags, false, 2f, rules).flags);
        }

        [Test]
        public void HoldLock_SeatedWithoutHold_StartsInLaptop_ThenKeepsLastHold()
        {
            var hold = new IphoneDuoHoldLock();
            var rules = FollowsHold;
            var start = hold.Step(Seated, DeviceOrientation.FaceUp, LandscapeOnly, false, 0f, rules);
            Assert.IsTrue(start.writeFlags, "Seated defaults to the laptop hold");
            Assert.AreEqual(TallHold, start.flags);

            var book = hold.Step(Seated, Book, start.flags, false, 1f, rules);
            Assert.IsTrue(book.writeFlags);
            Assert.IsFalse(hold.Step(Seated, DeviceOrientation.FaceUp, book.flags, false, 2f, rules).writeFlags, "face up keeps the book hold");
        }

        [Test]
        public void HoldLock_SeatedToCover_WaitsTheLeaveDelay_IgnoresAFlicker()
        {
            var rules = Defaults;
            var hold = new IphoneDuoHoldLock();
            var laptop = hold.Step(Seated, Laptop, LandscapeOnly, false, 0f, rules);

            Assert.IsFalse(hold.Step(Cover, DeviceOrientation.Portrait, laptop.flags, false, 1f, rules).writeFlags);
            Assert.IsFalse(hold.Step(Seated, Laptop, laptop.flags, false, 1.1f, rules).writeFlags, "flicker: back in Seated");
            Assert.IsFalse(hold.Step(Cover, DeviceOrientation.Portrait, laptop.flags, false, 2f, rules).writeFlags, "timer restarted");

            var restore = hold.Step(Cover, DeviceOrientation.Portrait, laptop.flags, false, 2.01f + rules.leaveSeatedDelaySeconds, rules);
            Assert.IsTrue(restore.writeFlags);
            Assert.AreEqual(LandscapeOnly, restore.flags, "the cover keeps the Player lock by default");
            Assert.IsFalse(hold.IsActive);

            // Custom delay.
            var quick = new IphoneDuoHoldLock();
            var quickRules = With(r => r.leaveSeatedDelaySeconds = 0f);
            quick.Step(Seated, Laptop, LandscapeOnly, false, 0f, quickRules);
            Assert.IsTrue(quick.Step(Cover, DeviceOrientation.Portrait, TallHold, false, 0f, quickRules).writeFlags);
        }

        [Test]
        public void HoldLock_FlattenedInLaptopHold_TurnsSidewaysUntilTurnedWide()
        {
            var hold = new IphoneDuoHoldLock();
            var laptop = hold.Step(Seated, Laptop, LandscapeOnly, false, 0f, Defaults);

            // Flat while still tall: Portrait stays (no rotation), the view turn now turns the game sideways.
            Assert.IsTrue(laptop.writeTurn && laptop.turn);
            var flat = hold.Step(Flat, Laptop, laptop.flags, false, 1f, Defaults);
            Assert.IsFalse(flat.writeFlags || flat.writeTurn);
            Assert.AreEqual(IphoneDuoScreenMode.Sideways, hold.Mode);
            Assert.IsTrue(hold.IsSideways && hold.IsTurning);

            // Face up or tall the other way: nothing to write (the view turn adapts every frame).
            Assert.IsFalse(hold.Step(Flat, DeviceOrientation.FaceUp, laptop.flags, false, 2f, Defaults).writeTurn);
            var other = hold.Step(Flat, LaptopFlipped, laptop.flags, false, 3f, Defaults);
            Assert.IsFalse(other.writeTurn || other.writeFlags);

            // Turned wide: plain view and the project's flags back (iOS then rotates full screen).
            var wide = hold.Step(Flat, Book, TallHold, false, 4f, Defaults);
            Assert.IsTrue(wide.writeTurn && !wide.turn);
            Assert.IsTrue(wide.writeFlags);
            Assert.AreEqual(LandscapeOnly, wide.flags);
            Assert.IsFalse(hold.IsActive || hold.IsSideways);
        }

        [Test]
        public void HoldLock_Sideways_EndsWhenFoldedClosedOrDetached()
        {
            var hold = new IphoneDuoHoldLock();
            var laptop = hold.Step(Seated, Laptop, LandscapeOnly, false, 0f, Defaults);
            hold.Step(Flat, Laptop, laptop.flags, false, 1f, Defaults);

            // Back to Seated: Rotate, still Portrait; the view turn follows the hold (straight on this side).
            var seatedAgain = hold.Step(Seated, Laptop, TallHold, false, 2f, Defaults);
            Assert.IsFalse(seatedAgain.writeTurn || seatedAgain.writeFlags);
            Assert.AreEqual(IphoneDuoScreenMode.HoldAxis, hold.Mode);

            // Flat again, then closed: plain view and the Player lock at once (no Seated delay from flat).
            hold.Step(Flat, Laptop, laptop.flags, false, 3f, Defaults);
            var closed = hold.Step(Cover, DeviceOrientation.Portrait, laptop.flags, false, 4f, Defaults);
            Assert.IsTrue(closed.writeTurn && !closed.turn && closed.writeFlags);
            Assert.AreEqual(LandscapeOnly, closed.flags);

            var other = new IphoneDuoHoldLock();
            other.Step(Flat, Laptop, LandscapeOnly, false, 0f, Defaults);
            var detach = other.Restore();
            Assert.IsTrue(detach.writeTurn && !detach.turn && detach.writeFlags);
            Assert.IsFalse(other.Restore().writeFlags, "restores once");
        }

        [Test]
        public void HoldLock_ColdStartFlatTall_TurnsSideways_UnlessFlatLockHolds()
        {
            var coldStart = new IphoneDuoHoldLock();
            var start = coldStart.Step(Flat, Laptop, LandscapeOnly, false, 0f, Defaults);
            Assert.IsTrue(start.writeFlags && start.writeTurn && start.turn);
            Assert.AreEqual(TallHold, start.flags, "iOS gets the full tall screen");

            var idle = new IphoneDuoHoldLock();
            Assert.IsFalse(idle.Step(Flat, Book, LandscapeOnly, false, 0f, Defaults).writeFlags);
            Assert.IsFalse(idle.Step(Flat, DeviceOrientation.FaceUp, LandscapeOnly, false, 0f, Defaults).writeFlags);
            Assert.IsFalse(idle.Step(Flat, Laptop, LandscapeOnly, true, 0f, Defaults).writeFlags, "wide turned tall: the flat lock holds it");
            Assert.IsFalse(idle.IsActive);
        }

        [Test]
        public void HoldLock_FlatRules_RotateOrSystemDefault()
        {
            var rotate = new IphoneDuoHoldLock();
            var rotateRules = With(r => r.flatAcrossLock = IphoneDuoAcrossHold.Rotate);
            var tall = rotate.Step(Flat, Laptop, LandscapeOnly, false, 0f, rotateRules);
            Assert.IsTrue(tall.writeFlags && tall.writeTurn && tall.turn, "turned 180° where Upside Down is upright");
            Assert.AreEqual(TallHold, tall.flags);
            Assert.AreEqual(IphoneDuoScreenMode.HoldAxis, rotate.Mode);
            Assert.IsFalse(rotate.IsSideways);

            var system = new IphoneDuoHoldLock();
            var systemRules = With(r => r.flatAcrossLock = IphoneDuoAcrossHold.SystemDefault);
            var laptop = system.Step(Seated, Laptop, LandscapeOnly, false, 0f, systemRules);
            var flat = system.Step(Flat, Laptop, laptop.flags, false, 1f, systemRules);
            Assert.IsTrue(flat.writeFlags && flat.writeTurn && !flat.turn);
            Assert.AreEqual(LandscapeOnly, flat.flags, "hands the Player lock back (iOS letterboxes)");
        }

        [Test]
        public void HoldLock_SeatedSideways_AndCoverRotate()
        {
            var sideways = new IphoneDuoHoldLock();
            var seatedRules = With(r => r.seatedAcrossLock = IphoneDuoAcrossHold.Sideways);
            var laptop = sideways.Step(Seated, Laptop, LandscapeOnly, false, 0f, seatedRules);
            Assert.IsTrue(laptop.writeTurn && laptop.turn && laptop.writeFlags);
            Assert.AreEqual(TallHold, laptop.flags);
            Assert.IsTrue(sideways.IsSideways);

            var cover = new IphoneDuoHoldLock();
            var coverRules = With(r => r.coverAcrossLock = IphoneDuoAcrossHold.Rotate);
            var tall = cover.Step(Cover, DeviceOrientation.Portrait, LandscapeOnly, false, 0f, coverRules);
            Assert.IsTrue(tall.writeFlags && !tall.writeTurn, "the cover is an iPhone: no view turn");
            Assert.AreEqual(Flags(true, false, false, false), tall.flags);
            var wide = cover.Step(Cover, DeviceOrientation.LandscapeLeft, tall.flags, false, 1f, coverRules);
            Assert.AreEqual(LandscapeOnly, wide.flags);
        }

        [Test]
        public void HoldLock_AdoptsTheNativeLaunchHold_ThenFollowsTheRules()
        {
            // UIKit masks: Portrait = 1 << 1, Upside Down = 1 << 2, Landscape Right = 1 << 3 (Unity Landscape Left).
            Assert.AreEqual(TallHold, IphoneDuoHoldOrientation.FromInterfaceMask(1 << 1));
            Assert.AreEqual(Flags(false, true, false, false), IphoneDuoHoldOrientation.FromInterfaceMask(1 << 2));
            Assert.AreEqual(Flags(false, false, true, false), IphoneDuoHoldOrientation.FromInterfaceMask(1 << 3));
            Assert.AreEqual(LandscapeOnly, IphoneDuoHoldOrientation.FromInterfaceMask((1 << 3) | (1 << 4)));
            Assert.AreEqual(default(IphoneDuoAutorotateFlags), IphoneDuoHoldOrientation.FromInterfaceMask(0));

            // The splash ran sideways: adopt it (Unity's flags follow the native mask), keep it while tall.
            var hold = new IphoneDuoHoldLock();
            var adopt = hold.AdoptHold(LandscapeOnly, TallHold, turns: 1);
            Assert.IsTrue(adopt.writeFlags && !adopt.writeTurn, "the view is already turned");
            Assert.AreEqual(TallHold, adopt.flags);
            Assert.IsTrue(hold.IsSideways && hold.IsTurning);
            Assert.IsFalse(hold.Step(Flat, Laptop, TallHold, false, 0f, Defaults).writeTurn, "stays sideways");

            // Turned wide: straight view and the Player lock back.
            var wide = hold.Step(Flat, Book, TallHold, false, 1f, Defaults);
            Assert.IsTrue(wide.writeTurn && !wide.turn && wide.writeFlags);
            Assert.AreEqual(LandscapeOnly, wide.flags);

            // Seated at launch (laptop) after a sideways splash: Rotate keeps Portrait and the view turn,
            // which now follows the hold (straight on this side).
            var seated = new IphoneDuoHoldLock();
            seated.AdoptHold(LandscapeOnly, TallHold, turns: 1);
            var rotate = seated.Step(Seated, Laptop, TallHold, false, 0f, Defaults);
            Assert.IsFalse(rotate.writeTurn || rotate.writeFlags);
            Assert.AreEqual(IphoneDuoScreenMode.HoldAxis, seated.Mode);

            // Seated at launch under Rotate, flipped laptop: adopted turned 180°, not sideways.
            var rotated = new IphoneDuoHoldLock();
            rotated.AdoptHold(LandscapeOnly, TallHold, turns: 2);
            Assert.IsTrue(rotated.IsTurning && !rotated.IsSideways);
            var keep = rotated.Step(Seated, LaptopFlipped, TallHold, false, 0f, Defaults);
            Assert.IsFalse(keep.writeTurn || keep.writeFlags);

            // Adopted straight: the Rotate rule starts the view turn (0 on the laptop side).
            var straight = new IphoneDuoHoldLock();
            straight.AdoptHold(LandscapeOnly, TallHold, turns: 0);
            var start = straight.Step(Seated, Laptop, TallHold, false, 0f, Defaults);
            Assert.IsTrue(start.writeTurn && start.turn && !start.writeFlags);
        }

        [Test]
        public void ViewTurns_ReadUprightOnEitherPortrait()
        {
            const IphoneDuoScreenMode sideways = IphoneDuoScreenMode.Sideways;
            const IphoneDuoScreenMode holdAxis = IphoneDuoScreenMode.HoldAxis;
            const IphoneDuoInterfaceOrientation portrait = IphoneDuoInterfaceOrientation.Portrait;
            var rules = Defaults;
            // Laptop (Landscape Left): upright is Portrait. Flipped laptop (Landscape Right): Upside Down.
            Assert.AreEqual(1, IphoneDuoHoldOrientation.ViewTurns(sideways, rules, Laptop, portrait));
            Assert.AreEqual(-1, IphoneDuoHoldOrientation.ViewTurns(sideways, rules, Laptop, IphoneDuoInterfaceOrientation.PortraitUpsideDown));
            Assert.AreEqual(1, IphoneDuoHoldOrientation.ViewTurns(sideways, rules, LaptopFlipped, IphoneDuoInterfaceOrientation.PortraitUpsideDown));
            Assert.AreEqual(-1, IphoneDuoHoldOrientation.ViewTurns(sideways, rules, LaptopFlipped, portrait),
                "iOS shows the other portrait: the opposite turn reads the same");
            Assert.AreEqual(1, IphoneDuoHoldOrientation.ViewTurns(sideways, rules, DeviceOrientation.Unknown, portrait));

            var right = With(r => r.sidewaysTurn = IphoneDuoSidewaysTurn.Right90);
            Assert.AreEqual(-1, IphoneDuoHoldOrientation.ViewTurns(sideways, right, Laptop, portrait));
            Assert.AreEqual(1, IphoneDuoHoldOrientation.ViewTurns(sideways, right, LaptopFlipped, portrait));

            // Rotate: Portrait on the flipped laptop is upside down for the user, half a turn fixes it.
            Assert.AreEqual(0, IphoneDuoHoldOrientation.ViewTurns(holdAxis, rules, Laptop, portrait));
            Assert.AreEqual(2, IphoneDuoHoldOrientation.ViewTurns(holdAxis, rules, LaptopFlipped, portrait));
            Assert.AreEqual(0, IphoneDuoHoldOrientation.ViewTurns(IphoneDuoScreenMode.Native, rules, LaptopFlipped, portrait));

            Assert.IsTrue(IphoneDuoHoldOrientation.TurnsView(Flat, sideways, TallHold));
            Assert.IsTrue(IphoneDuoHoldOrientation.TurnsView(Seated, holdAxis, TallHold));
            Assert.IsFalse(IphoneDuoHoldOrientation.TurnsView(Cover, holdAxis, TallHold), "the cover is an iPhone");
            Assert.IsFalse(IphoneDuoHoldOrientation.TurnsView(Seated, holdAxis, LandscapeOnly), "iOS shows either landscape");
            Assert.IsFalse(IphoneDuoHoldOrientation.TurnsView(Flat, IphoneDuoScreenMode.Native, TallHold));
        }

        [Test]
        public void HoldLock_BookHold_NeverSideways_PortraitGameMirrors()
        {
            var hold = new IphoneDuoHoldLock();
            hold.Step(Seated, Book, LandscapeOnly, false, 0f, FollowsHold);
            Assert.IsFalse(hold.IsActive, "the book hold uses the game's own axis");
            Assert.IsFalse(hold.Step(Flat, Book, LandscapeOnly, false, 1f, Defaults).writeTurn);

            var mirror = new IphoneDuoHoldLock();
            var book = mirror.Step(Seated, Book, PortraitOnly, false, 0f, Defaults);
            Assert.AreEqual(LandscapeOnly, book.flags);
            Assert.IsTrue(mirror.Step(Flat, Book, book.flags, false, 1f, Defaults).turn);
        }

        [Test]
        public void HoldLock_FoldedToCoverFaceUp_DropsTheOverride()
        {
            var hold = new IphoneDuoHoldLock();
            var tall = hold.Step(Flat, Laptop, LandscapeOnly, false, 0f, Defaults);
            Assert.IsTrue(tall.turn);

            // The cover reports no hold yet (face up): the inner display's hold no longer applies.
            var cover = hold.Step(Cover, DeviceOrientation.FaceUp, tall.flags, false, 1f, Defaults);
            Assert.IsTrue(cover.writeTurn && !cover.turn && cover.writeFlags);
            Assert.AreEqual(LandscapeOnly, cover.flags);
            Assert.IsFalse(hold.IsActive);
            Assert.AreEqual(DeviceOrientation.Unknown, hold.HoldDevice);

            // Seated, then folded closed face up: after the leave delay the Player lock is back.
            var seated = new IphoneDuoHoldLock();
            var laptop = seated.Step(Seated, Laptop, LandscapeOnly, false, 0f, Defaults);
            Assert.IsFalse(seated.Step(Cover, DeviceOrientation.FaceUp, laptop.flags, false, 1f, Defaults).writeFlags, "leave delay");
            var closed = seated.Step(Cover, DeviceOrientation.FaceUp, laptop.flags, false, 1.01f + Defaults.leaveSeatedDelaySeconds, Defaults);
            Assert.IsTrue(closed.writeFlags);
            Assert.AreEqual(LandscapeOnly, closed.flags);
        }

        [Test]
        public void HoldLock_SeatedWithoutHold_SidewaysTurnsFromTheLaptopHold()
        {
            var rules = With(r => r.seatedAcrossLock = IphoneDuoAcrossHold.Sideways);
            var hold = new IphoneDuoHoldLock();
            var start = hold.Step(Seated, DeviceOrientation.FaceUp, LandscapeOnly, false, 0f, rules);
            Assert.IsTrue(start.writeTurn && start.turn);
            Assert.AreEqual(Laptop, hold.HoldDevice, "the view turn needs the assumed laptop hold");

            // Flat face up keeps it; flat face up at launch has none (the native launch turn stays).
            hold.Step(Flat, DeviceOrientation.FaceUp, start.flags, false, 1f, rules);
            Assert.AreEqual(Laptop, hold.HoldDevice);
            var launch = new IphoneDuoHoldLock();
            launch.AdoptHold(LandscapeOnly, TallHold, turns: 1);
            Assert.IsFalse(launch.Step(Flat, DeviceOrientation.FaceUp, TallHold, false, 0f, rules).writeTurn);
            Assert.AreEqual(DeviceOrientation.Unknown, launch.HoldDevice);
        }

        [Test]
        public void HoldLock_State_FollowsWhatTheRulesApply()
        {
            var hold = new IphoneDuoHoldLock();
            var tall = hold.Step(Flat, Laptop, LandscapeOnly, false, 0f, Defaults);
            var sideways = new IphoneDuoState(Flat, IphoneDuoHold.Tall, true, IphoneDuoScreenMode.Sideways);
            Assert.AreEqual(sideways, hold.State(Flat, false));
            hold.Step(Flat, DeviceOrientation.FaceUp, tall.flags, false, 1f, Defaults);
            Assert.AreEqual(sideways, hold.State(Flat, false), "face up keeps the hold");

            var wide = new IphoneDuoHoldLock();
            wide.Step(Flat, Book, LandscapeOnly, true, 0f, Defaults);
            Assert.AreEqual(new IphoneDuoState(Flat, IphoneDuoHold.Wide, false, IphoneDuoScreenMode.Locked), wide.State(Flat, true));

            var seated = new IphoneDuoHoldLock();
            seated.Step(Seated, DeviceOrientation.FaceUp, LandscapeOnly, false, 0f, Defaults);
            Assert.AreEqual(new IphoneDuoState(Seated, IphoneDuoHold.Tall, true, IphoneDuoScreenMode.HoldAxis), seated.State(Seated, false),
                "Seated without a hold publishes the laptop hold it applies");

            var none = new IphoneDuoHoldLock();
            none.Step(Flat, DeviceOrientation.FaceUp, LandscapeOnly, false, 0f, Defaults);
            Assert.AreEqual(new IphoneDuoState(Flat, IphoneDuoHold.Unknown, false, IphoneDuoScreenMode.Native), none.State(Flat, false));
        }

        [Test]
        public void SeatedHoldDevice_KeepsTheRulesHold()
        {
            // On the kept hold the device itself counts (either side), so the view turn stays upright.
            Assert.AreEqual(Laptop, IphoneDuoHoldOrientation.SeatedHoldDevice(true, Laptop, Book));
            Assert.AreEqual(LaptopFlipped, IphoneDuoHoldOrientation.SeatedHoldDevice(true, LaptopFlipped, Laptop));
            Assert.AreEqual(BookFlipped, IphoneDuoHoldOrientation.SeatedHoldDevice(false, BookFlipped, Laptop));
            // Turned to the other hold: the last one kept, else the hold's default.
            Assert.AreEqual(LaptopFlipped, IphoneDuoHoldOrientation.SeatedHoldDevice(true, Book, LaptopFlipped));
            Assert.AreEqual(Laptop, IphoneDuoHoldOrientation.SeatedHoldDevice(true, Book, DeviceOrientation.Unknown));
            Assert.AreEqual(Laptop, IphoneDuoHoldOrientation.SeatedHoldDevice(true, BookFlipped, Book), "a kept book hold is not a laptop hold");
            Assert.AreEqual(BookFlipped, IphoneDuoHoldOrientation.SeatedHoldDevice(false, Laptop, BookFlipped));
            Assert.AreEqual(Book, IphoneDuoHoldOrientation.SeatedHoldDevice(false, LaptopFlipped, DeviceOrientation.FaceUp));
        }

        [Test]
        public void HoldLock_SeatedLocked_TurningToTheBookKeepsSeatedPortrait_UntilFlattened()
        {
            var hold = new IphoneDuoHoldLock();
            var laptop = hold.Step(Seated, Laptop, LandscapeOnly, false, 0f, Defaults);
            Assert.AreEqual(TallHold, laptop.flags);

            // Book hold: nothing changes, the laptop hold stays the one applied.
            var book = hold.Step(Seated, Book, laptop.flags, false, 1f, Defaults);
            Assert.IsFalse(book.writeFlags || book.writeTurn, "turning the device must not re-orient the game");
            Assert.AreEqual(Laptop, hold.HoldDevice);
            Assert.AreEqual(new IphoneDuoState(Seated, IphoneDuoHold.Tall, true, IphoneDuoScreenMode.HoldAxis), hold.State(Seated, false));

            // The laptop hold's other side still counts (the view turns 180° to stay upright), and the
            // book hold then keeps that side.
            Assert.IsFalse(hold.Step(Seated, LaptopFlipped, laptop.flags, false, 2f, Defaults).writeFlags);
            Assert.AreEqual(LaptopFlipped, hold.HoldDevice);
            hold.Step(Seated, BookFlipped, laptop.flags, false, 3f, Defaults);
            Assert.AreEqual(LaptopFlipped, hold.HoldDevice);

            // Flattened in the book hold: the flat rules apply to the real hold (Player lock back).
            var flat = hold.Step(Flat, Book, laptop.flags, false, 4f, Defaults);
            Assert.IsTrue(flat.writeFlags && flat.writeTurn && !flat.turn);
            Assert.AreEqual(LandscapeOnly, flat.flags);
            Assert.AreEqual(Book, hold.HoldDevice);
        }

        [Test]
        public void HoldLock_SeatedLocked_AutoPicksThePoseFromThePlayerAxes()
        {
            // Landscape-only game entering Seated in the book hold: Seated Portrait (tall laptop hold).
            var landscape = new IphoneDuoHoldLock();
            var tall = landscape.Step(Seated, Book, LandscapeOnly, false, 0f, Defaults);
            Assert.IsTrue(tall.writeFlags);
            Assert.AreEqual(TallHold, tall.flags);
            Assert.AreEqual(Laptop, landscape.HoldDevice, "the laptop hold's default side");

            // Portrait-only game entering in the laptop hold: Seated Landscape (wide book hold).
            var portrait = new IphoneDuoHoldLock();
            var wide = portrait.Step(Seated, Laptop, PortraitOnly, false, 0f, Defaults);
            Assert.AreEqual(LandscapeOnly, wide.flags);
            Assert.AreEqual(Book, portrait.HoldDevice);
            Assert.IsFalse(portrait.Step(Seated, LaptopFlipped, wide.flags, false, 1f, Defaults).writeFlags);

            // Both axes: the hold Seated was entered with, also when turned.
            var enteredBook = new IphoneDuoHoldLock();
            var book = enteredBook.Step(Seated, Book, BothAxes, false, 0f, Defaults);
            Assert.AreEqual(LandscapeOnly, book.flags);
            Assert.IsFalse(enteredBook.Step(Seated, Laptop, book.flags, false, 1f, Defaults).writeFlags);
            var enteredLaptop = new IphoneDuoHoldLock();
            var laptop = enteredLaptop.Step(Seated, Laptop, BothAxes, false, 0f, Defaults);
            Assert.AreEqual(TallHold, laptop.flags);
            Assert.IsFalse(enteredLaptop.Step(Seated, Book, laptop.flags, false, 1f, Defaults).writeFlags);

            // Entered face up (laptop assumed): the first hold reported is the entry hold.
            var faceUp = new IphoneDuoHoldLock();
            var assumed = faceUp.Step(Seated, DeviceOrientation.FaceUp, BothAxes, false, 0f, Defaults);
            Assert.AreEqual(TallHold, assumed.flags);
            var reported = faceUp.Step(Seated, Book, assumed.flags, false, 1f, Defaults);
            Assert.AreEqual(LandscapeOnly, reported.flags);
            Assert.IsFalse(faceUp.Step(Seated, Laptop, reported.flags, false, 2f, Defaults).writeFlags);

            // Leaving Seated ends the period: entering again takes the new hold.
            faceUp.Step(Flat, Laptop, reported.flags, false, 3f, Defaults);
            var again = faceUp.Step(Seated, Laptop, BothAxes, false, 4f, Defaults);
            Assert.AreEqual(TallHold, again.flags);
        }

        [Test]
        public void HoldLock_SeatedLocked_FixedPoseAndRuntimeRuleChanges()
        {
            // A fixed pose: Seated Landscape for a landscape game is its own axis (the Player lock).
            var fixedPose = new IphoneDuoHoldLock();
            var rules = With(r => r.seatedLockedPose = IphoneDuoSeatedPose.SeatedLandscape);
            fixedPose.Step(Seated, Laptop, LandscapeOnly, false, 0f, rules);
            Assert.IsFalse(fixedPose.IsActive);
            Assert.AreEqual(Book, fixedPose.HoldDevice);

            // Rules changed at runtime take effect on the next frame.
            var hold = new IphoneDuoHoldLock();
            rules = Defaults;
            var tall = hold.Step(Seated, Book, LandscapeOnly, false, 0f, rules);
            Assert.AreEqual(TallHold, tall.flags);
            rules.seatedRotation = IphoneDuoSeatedRotation.FollowsHold;
            var follows = hold.Step(Seated, Book, tall.flags, false, 1f, rules);
            Assert.IsTrue(follows.writeFlags);
            Assert.AreEqual(LandscapeOnly, follows.flags, "follows the book hold");
            rules.seatedRotation = IphoneDuoSeatedRotation.Locked;
            Assert.AreEqual(TallHold, hold.Step(Seated, Book, follows.flags, false, 2f, rules).flags);
            rules.seatedLockedPose = IphoneDuoSeatedPose.SeatedLandscape;
            Assert.AreEqual(LandscapeOnly, hold.Step(Seated, Book, TallHold, false, 3f, rules).flags);
        }

        [Test]
        public void FlatHoldDevice_KeepsHoldAndFollowsFlippedSide()
        {
            // On the kept hold the device itself counts (either side), so the game stays upright.
            Assert.AreEqual(Book, IphoneDuoHoldOrientation.FlatHoldDevice(true, Book, Laptop));
            Assert.AreEqual(BookFlipped, IphoneDuoHoldOrientation.FlatHoldDevice(true, BookFlipped, Book));
            Assert.AreEqual(LaptopFlipped, IphoneDuoHoldOrientation.FlatHoldDevice(false, LaptopFlipped, Book));
            // Turned to the other hold: the last one kept, else the hold's default.
            Assert.AreEqual(BookFlipped, IphoneDuoHoldOrientation.FlatHoldDevice(true, Laptop, BookFlipped));
            Assert.AreEqual(Book, IphoneDuoHoldOrientation.FlatHoldDevice(true, Laptop, DeviceOrientation.Unknown));
            Assert.AreEqual(Book, IphoneDuoHoldOrientation.FlatHoldDevice(true, LaptopFlipped, Laptop), "a kept tall hold is not a wide hold");
            Assert.AreEqual(LaptopFlipped, IphoneDuoHoldOrientation.FlatHoldDevice(false, Book, LaptopFlipped));
            Assert.AreEqual(Laptop, IphoneDuoHoldOrientation.FlatHoldDevice(false, BookFlipped, DeviceOrientation.FaceUp));
        }

        // A Flat session: cold start tall, turned wide, flipped, tall again, face up, Seated and back.
        static readonly (IphoneDuoPosture posture, DeviceOrientation device)[] FlatScript =
        {
            (Flat, Laptop), (Flat, Book), (Flat, BookFlipped), (Flat, LaptopFlipped), (Flat, DeviceOrientation.FaceUp),
            (Flat, Book), (Seated, Laptop), (Flat, Laptop), (Flat, Book), (Cover, DeviceOrientation.Portrait), (Flat, Book),
        };

        static string RunFlatScript(IphoneDuoRules rules, IphoneDuoAutorotateFlags project, bool flatLocked)
        {
            var hold = new IphoneDuoHoldLock();
            var current = project;
            var log = new System.Text.StringBuilder();
            float now = 0f;
            foreach (var (posture, device) in FlatScript)
            {
                var writes = hold.Step(posture, device, current, flatLocked, now, rules);
                if (writes.writeFlags)
                    current = writes.flags;
                var f = writes.flags;
                log.Append($"{writes.writeFlags}/{f.portrait}{f.portraitUpsideDown}{f.landscapeLeft}{f.landscapeRight}/" +
                           $"{writes.writeTurn}/{writes.turn}/{hold.Mode}/{hold.HoldDevice}; ");
                now += 1f;
            }
            return log.ToString();
        }

        [Test]
        public void HoldLock_FlatFollowDevice_MatchesToday()
        {
            // Follow device, whatever the (hidden) Locked pose, applies the Flat rules exactly as before.
            foreach (IphoneDuoAcrossHold rule in System.Enum.GetValues(typeof(IphoneDuoAcrossHold)))
            foreach (var project in new[] { LandscapeOnly, PortraitOnly, BothAxes })
            foreach (bool flatLocked in new[] { false, true })
            {
                string today = RunFlatScript(With(r => r.flatAcrossLock = rule), project, flatLocked);
                foreach (IphoneDuoFlatPose pose in System.Enum.GetValues(typeof(IphoneDuoFlatPose)))
                {
                    var follow = With(r =>
                    {
                        r.flatAcrossLock = rule;
                        r.flatRotation = IphoneDuoFlatRotation.FollowDevice;
                        r.flatLockedPose = pose;
                    });
                    Assert.AreEqual(today, RunFlatScript(follow, project, flatLocked), $"{rule} {project} {pose} {flatLocked}");
                }
            }

            // And a Locked Flat on a one-axis game's own axis under the Sideways rule is today's Flat too
            // (cold start tall turns sideways, the flat lock keeps the turn).
            Assert.AreEqual(RunFlatScript(Defaults, LandscapeOnly, false), RunFlatScript(FlatLocked(), LandscapeOnly, false));
            Assert.AreEqual(RunFlatScript(Defaults, PortraitOnly, true), RunFlatScript(FlatLocked(), PortraitOnly, true));
        }

        [Test]
        public void HoldLock_FlatLocked_IgnoresTurnToOtherHold()
        {
            // Both axes, entered wide: only the wide axis stays allowed when turned tall.
            var both = new IphoneDuoHoldLock();
            var rules = FlatLocked();
            var wide = both.Step(Flat, Book, BothAxes, false, 0f, rules);
            Assert.IsTrue(wide.writeFlags);
            Assert.AreEqual(LandscapeOnly, wide.flags);
            var turned = both.Step(Flat, Laptop, wide.flags, false, 1f, rules);
            Assert.IsFalse(turned.writeFlags || turned.writeTurn, "turning the device must not re-orient the game");
            Assert.AreEqual(Book, both.HoldDevice);
            Assert.AreEqual(new IphoneDuoState(Flat, IphoneDuoHold.Wide, false, IphoneDuoScreenMode.HoldAxis), both.State(Flat, false));
            // The wide hold's other side still counts (iOS turns 180° within the axis); tall then keeps it.
            Assert.IsFalse(both.Step(Flat, BookFlipped, wide.flags, false, 2f, rules).writeFlags);
            Assert.AreEqual(BookFlipped, both.HoldDevice);
            both.Step(Flat, LaptopFlipped, wide.flags, false, 3f, rules);
            Assert.AreEqual(BookFlipped, both.HoldDevice);
            // Folded to Seated (laptop), then flat again in that tall hold: a new Flat period keeps it.
            Assert.AreEqual(TallHold, both.Step(Seated, Laptop, wide.flags, false, 4f, rules).flags);
            Assert.IsFalse(both.Step(Flat, Laptop, TallHold, false, 5f, rules).writeFlags, "the laptop hold stays");
            Assert.IsFalse(both.Step(Flat, Book, TallHold, false, 6f, rules).writeFlags);
            Assert.AreEqual(Laptop, both.HoldDevice);
            // The tall hold's upside-down side is upright with the view turned 180° (kept tall).
            Assert.IsFalse(both.Step(Flat, LaptopFlipped, TallHold, false, 7f, rules).writeFlags);
            Assert.AreEqual(LaptopFlipped, both.HoldDevice);
            Assert.IsTrue(both.IsTurning);

            // Entered face up: the first hold reported is the entry hold, no hold before it.
            var faceUp = new IphoneDuoHoldLock();
            Assert.IsFalse(faceUp.Step(Flat, DeviceOrientation.FaceUp, BothAxes, false, 0f, rules).writeFlags);
            Assert.AreEqual(TallHold, faceUp.Step(Flat, Laptop, BothAxes, false, 1f, rules).flags);
            Assert.IsFalse(faceUp.Step(Flat, Book, TallHold, false, 2f, rules).writeFlags);

            // One-axis landscape game with Rotate: Locked keeps the game's axis, held tall it turns
            // sideways instead of rotating.
            var rotate = FlatLocked(edit: r => r.flatAcrossLock = IphoneDuoAcrossHold.Rotate);
            var oneAxis = new IphoneDuoHoldLock();
            var tall = oneAxis.Step(Flat, Laptop, LandscapeOnly, false, 0f, rotate);
            Assert.IsTrue(tall.writeFlags && tall.writeTurn && tall.turn);
            Assert.AreEqual(IphoneDuoScreenMode.Sideways, oneAxis.Mode);
            var follow = new IphoneDuoHoldLock();
            follow.Step(Flat, Laptop, LandscapeOnly, false, 0f, With(r => r.flatAcrossLock = IphoneDuoAcrossHold.Rotate));
            Assert.AreEqual(IphoneDuoScreenMode.HoldAxis, follow.Mode, "Follow device rotates, as today");

            // The tall pose for a landscape game: rotated to tall, also when held wide.
            var tallPose = FlatLocked(IphoneDuoFlatPose.Tall, r => r.flatAcrossLock = IphoneDuoAcrossHold.Rotate);
            var keptTall = new IphoneDuoHoldLock();
            var rotated = keptTall.Step(Flat, Book, LandscapeOnly, false, 0f, tallPose);
            Assert.AreEqual(TallHold, rotated.flags);
            Assert.AreEqual(Laptop, keptTall.HoldDevice, "the tall hold's default side");
            Assert.IsFalse(keptTall.Step(Flat, BookFlipped, TallHold, false, 1f, tallPose).writeFlags);
        }

        #endregion

        #region Flat lock

        [Test]
        public void FlatUpright_DuoInnerAxesAreTurned90Degrees()
        {
            // Wide hold reports Portrait; tall hold reports a landscape device orientation.
            Assert.AreEqual(IphoneDuoInterfaceOrientation.LandscapeLeft, IphoneDuoFlatOrientation.UprightInterface(DeviceOrientation.Portrait));
            Assert.AreEqual(IphoneDuoInterfaceOrientation.LandscapeRight, IphoneDuoFlatOrientation.UprightInterface(DeviceOrientation.PortraitUpsideDown));
            Assert.AreEqual(IphoneDuoInterfaceOrientation.Portrait, IphoneDuoFlatOrientation.UprightInterface(DeviceOrientation.LandscapeLeft));
            Assert.AreEqual(IphoneDuoInterfaceOrientation.PortraitUpsideDown, IphoneDuoFlatOrientation.UprightInterface(DeviceOrientation.LandscapeRight));
            Assert.AreEqual(IphoneDuoInterfaceOrientation.Unknown, IphoneDuoFlatOrientation.UprightInterface(DeviceOrientation.FaceUp));
            // UIKit Landscape Right is Unity's Landscape Left.
            Assert.IsTrue(IphoneDuoFlatOrientation.Allows(Flags(false, false, true, false), IphoneDuoInterfaceOrientation.LandscapeRight));
            Assert.IsFalse(IphoneDuoFlatOrientation.Allows(Flags(false, false, true, false), IphoneDuoInterfaceOrientation.LandscapeLeft));
        }

        const IphoneDuoInterfaceOrientation Left = IphoneDuoInterfaceOrientation.LandscapeLeft;
        const IphoneDuoInterfaceOrientation Right = IphoneDuoInterfaceOrientation.LandscapeRight;
        const IphoneDuoInterfaceOrientation None = IphoneDuoInterfaceOrientation.Unknown;

        static IphoneDuoFlatLock.Writes FlatStep(IphoneDuoFlatLock flat, float now, DeviceOrientation device,
            IphoneDuoInterfaceOrientation current, IphoneDuoRules rules = null) =>
            flat.Step(true, now, LandscapeOnly, device, current, rules ?? Defaults);

        // Wide hold, upright, held until the lock engages.
        static void SettleWide(IphoneDuoFlatLock flat, float now, IphoneDuoRules rules = null)
        {
            rules = rules ?? Defaults;
            FlatStep(flat, now, Book, Left, rules);
            var lockOn = FlatStep(flat, now + rules.lockSettleSeconds, Book, Left, rules);
            Assert.IsTrue(lockOn.writeLock && lockOn.locked);
        }

        [Test]
        public void FlatLock_LocksOnceUprightAndSettled_NeverALetterboxedStart()
        {
            var flat = new IphoneDuoFlatLock();

            // Held tall while iOS letterboxes the landscape game: never lock that state.
            Assert.IsFalse(FlatStep(flat, 0f, Laptop, Right).writeLock);

            // Wide and upright: lock after the rotation settles.
            Assert.IsFalse(FlatStep(flat, 1f, Book, Left).writeLock);
            Assert.IsFalse(FlatStep(flat, 1.2f, Book, Left).writeLock, "settling");
            var lockOn = FlatStep(flat, 1f + Defaults.lockSettleSeconds, Book, Left);
            Assert.IsTrue(lockOn.writeLock && lockOn.locked);
            Assert.IsFalse(FlatStep(flat, 3f, Book, Left).writeLock, "locks once");

            // Custom settle time.
            var slow = new IphoneDuoFlatLock();
            var slowRules = With(r => r.lockSettleSeconds = 2f);
            FlatStep(slow, 0f, Book, Left, slowRules);
            Assert.IsFalse(FlatStep(slow, 1f, Book, Left, slowRules).writeLock);
            Assert.IsTrue(FlatStep(slow, 2f, Book, Left, slowRules).locked);
        }

        [Test]
        public void FlatLock_TallHold_KeepsTheGameAsItIs()
        {
            var flat = new IphoneDuoFlatLock();
            SettleWide(flat, 0f);

            // Turned tall: stay locked (full screen, sideways), no request.
            var tall = FlatStep(flat, 1f, Laptop, Left);
            Assert.IsFalse(tall.writeLock);
            Assert.AreEqual(None, tall.request);
            Assert.IsTrue(flat.IsLocked);

            // Back to the same wide hold: re-syncs (unlock, already upright, lock again).
            var wide = FlatStep(flat, 2f, Book, Left);
            Assert.IsTrue(wide.writeLock && !wide.locked);
            Assert.AreEqual(None, wide.request);
            // The settle time counts from the first upright frame with the lock off.
            Assert.IsFalse(FlatStep(flat, 2.1f, Book, Left).writeLock);
            Assert.IsTrue(FlatStep(flat, 2.1f + Defaults.lockSettleSeconds, Book, Left).locked);
        }

        [Test]
        public void FlatLock_OppositeWide_UnlocksFlips180ThenLocks()
        {
            var flat = new IphoneDuoFlatLock();
            SettleWide(flat, 0f);

            // While locked iOS reports a 180° request but keeps showing the old orientation: unlock first.
            var unlock = FlatStep(flat, 1f, BookFlipped, Left);
            Assert.IsTrue(unlock.writeLock && !unlock.locked);
            Assert.AreEqual(None, unlock.request);

            Assert.AreEqual(Right, FlatStep(flat, 1f, BookFlipped, Left).request);
            Assert.AreEqual(None, FlatStep(flat, 1.1f, BookFlipped, Left).request, "waits for iOS");
            Assert.IsFalse(FlatStep(flat, 1.3f, BookFlipped, Right).writeLock, "settling");
            var lockOn = FlatStep(flat, 1.3f + Defaults.lockSettleSeconds, BookFlipped, Right);
            Assert.IsTrue(lockOn.writeLock && lockOn.locked);

            // Tall from the flipped side keeps it; face up keeps the hold.
            Assert.IsFalse(FlatStep(flat, 3f, LaptopFlipped, Right).writeLock);
            Assert.IsFalse(FlatStep(flat, 3.5f, DeviceOrientation.FaceUp, Right).writeLock);

            // And back: unlock, then flip to Landscape Left.
            Assert.IsTrue(FlatStep(flat, 4f, Book, Right).writeLock);
            Assert.AreEqual(Left, FlatStep(flat, 4f, Book, Right).request);
        }

        [Test]
        public void FlatLock_RepeatsFlipRequestUntilIosReachesIt()
        {
            var flat = new IphoneDuoFlatLock();
            SettleWide(flat, 0f);
            FlatStep(flat, 1f, BookFlipped, Left);

            Assert.AreEqual(Right, FlatStep(flat, 1f, BookFlipped, Left).request);
            Assert.AreEqual(None, FlatStep(flat, 1.2f, BookFlipped, Left).request, "waits for iOS");
            Assert.AreEqual(Right, FlatStep(flat, 1.6f, BookFlipped, Left).request,
                "iOS did not rotate (request arrived mid-rotation): ask again");
            Assert.AreEqual(None, FlatStep(flat, 2.5f, BookFlipped, Right).request, "reached");

            var patient = new IphoneDuoFlatLock();
            var patientRules = With(r => r.requestRetrySeconds = 2f);
            SettleWide(patient, 0f, patientRules);
            FlatStep(patient, 1f, BookFlipped, Left, patientRules);
            FlatStep(patient, 1f, BookFlipped, Left, patientRules);
            Assert.AreEqual(None, FlatStep(patient, 2.5f, BookFlipped, Left, patientRules).request);
            Assert.AreEqual(Right, FlatStep(patient, 3f, BookFlipped, Left, patientRules).request);
        }

        [Test]
        public void FlatLock_NoFlip_KeepsTheLockInEveryHold()
        {
            var noFlip = With(r => r.flatFlip180 = false);
            var flat = new IphoneDuoFlatLock();
            SettleWide(flat, 0f, noFlip);

            Assert.IsFalse(FlatStep(flat, 1f, BookFlipped, Left, noFlip).writeLock, "opposite wide: stays as it is");
            Assert.AreEqual(None, FlatStep(flat, 1f, BookFlipped, Left, noFlip).request);
            Assert.IsFalse(FlatStep(flat, 2f, Laptop, Left, noFlip).writeLock, "tall: stays as it is");

            // Unlocked start: any landscape of the game's axis is fine to lock, no request.
            var fresh = new IphoneDuoFlatLock();
            Assert.AreEqual(None, FlatStep(fresh, 0f, BookFlipped, Left, noFlip).request);
            Assert.IsTrue(FlatStep(fresh, noFlip.lockSettleSeconds, BookFlipped, Left, noFlip).locked);
        }

        [Test]
        public void FlatLock_RotateOrSystemDefault_LeavesTheTallHoldToIos()
        {
            // Flip on, no Sideways: iOS rotates natively, the lock is never used.
            var rotate = With(r => r.flatAcrossLock = IphoneDuoAcrossHold.Rotate);
            var flat = new IphoneDuoFlatLock();
            Assert.IsFalse(FlatStep(flat, 0f, Book, Left, rotate).writeLock);
            Assert.IsFalse(FlatStep(flat, 1f, Book, Left, rotate).writeLock);
            Assert.IsFalse(flat.IsLocked);

            // Flip off with Rotate: locked on the game's axis, released when held across.
            var keep = With(r =>
            {
                r.flatAcrossLock = IphoneDuoAcrossHold.Rotate;
                r.flatFlip180 = false;
            });
            var held = new IphoneDuoFlatLock();
            SettleWide(held, 0f, keep);
            var tall = FlatStep(held, 1f, Laptop, Left, keep);
            Assert.IsTrue(tall.writeLock && !tall.locked, "the Rotate rule takes the tall hold");
        }

        [Test]
        public void FlatLock_UnlocksWhenLeavingOpened_AndIgnoresTwoAxisProjects()
        {
            var flat = new IphoneDuoFlatLock();
            SettleWide(flat, 0f);
            Assert.IsTrue(flat.IsLocked);
            var off = flat.Step(false, 1f, LandscapeOnly, Book, Left, Defaults);
            Assert.IsTrue(off.writeLock);
            Assert.IsFalse(off.locked);
            Assert.IsFalse(flat.Step(false, 1f, LandscapeOnly, Book, Left, Defaults).writeLock, "unlocks once");
            Assert.IsFalse(flat.Unlock().writeLock);

            // Back in Opened while tall: not upright yet this time, so no lock.
            Assert.IsFalse(FlatStep(flat, 2f, Laptop, Left).writeLock);

            var both = Flags(true, false, true, true);
            Assert.IsFalse(flat.Step(true, 3f, both, Book, Left, Defaults).writeLock, "both axes rotate freely; no lock");
            Assert.IsFalse(flat.Step(true, 5f, both, Book, Left, Defaults).writeLock);
        }

        static void AssertFlatLockYields(IphoneDuoAutorotateFlags project, IphoneDuoRules rules,
            DeviceOrientation onAxis, IphoneDuoInterfaceOrientation upright)
        {
            // The game's own axis held upright: no request, no lock (the kept hold's mask rules it out).
            var flat = new IphoneDuoFlatLock();
            for (float now = 0f; now < 5f; now += 0.25f)
            {
                var writes = flat.Step(true, now, project, onAxis, IphoneDuoInterfaceOrientation.Portrait, rules);
                Assert.IsFalse(writes.writeLock, $"t={now}");
                Assert.AreEqual(None, writes.request, $"t={now}");
            }
            Assert.IsFalse(flat.IsLocked);

            // Already locked when the rule changes at runtime: released once.
            var follow = Defaults;
            follow.flatAcrossLock = rules.flatAcrossLock;
            var locked = new IphoneDuoFlatLock();
            locked.Step(true, 0f, project, onAxis, upright, follow);
            Assert.IsTrue(locked.Step(true, follow.lockSettleSeconds, project, onAxis, upright, follow).locked);
            var release = locked.Step(true, 1f, project, onAxis, upright, rules);
            Assert.IsTrue(release.writeLock && !release.locked);
            Assert.IsFalse(locked.Step(true, 2f, project, onAxis, upright, rules).writeLock);
        }

        [Test]
        public void FlatLock_LockedTallSideways_YieldsToKeptHold()
        {
            var rules = FlatLocked(IphoneDuoFlatPose.Tall);
            Assert.IsTrue(rules.FlatKeepsHold(LandscapeOnly));
            AssertFlatLockYields(LandscapeOnly, rules, Book, Left);

            // The hold rules own the tall screen (Sideways, turned), also when held wide.
            var hold = new IphoneDuoHoldLock();
            var wide = hold.Step(Flat, Book, LandscapeOnly, false, 0f, rules);
            Assert.IsTrue(wide.writeFlags && wide.turn);
            Assert.AreEqual(TallHold, wide.flags);
            Assert.AreEqual(IphoneDuoScreenMode.Sideways, hold.Mode);
        }

        [Test]
        public void FlatLock_LockedWideSideways_PortraitOnly_YieldsToKeptHold()
        {
            var rules = FlatLocked(IphoneDuoFlatPose.Wide);
            Assert.IsTrue(rules.FlatKeepsHold(PortraitOnly));
            AssertFlatLockYields(PortraitOnly, rules, Laptop, IphoneDuoInterfaceOrientation.Portrait);

            var hold = new IphoneDuoHoldLock();
            var tall = hold.Step(Flat, Laptop, PortraitOnly, false, 0f, rules);
            Assert.IsTrue(tall.writeFlags && tall.turn);
            Assert.AreEqual(LandscapeOnly, tall.flags);
            Assert.AreEqual(IphoneDuoScreenMode.Sideways, hold.Mode);
            Assert.AreEqual(Book, hold.HoldDevice);
        }

        [Test]
        public void FlatLock_LockedOnTheGamesAxis_KeepsTheTallHoldUnderRotate()
        {
            // Follow device with Rotate leaves the tall hold to the Rotate rule; Locked keeps the game as it is.
            var locked = FlatLocked(edit: r => r.flatAcrossLock = IphoneDuoAcrossHold.Rotate);
            var flat = new IphoneDuoFlatLock();
            SettleWide(flat, 0f, locked);
            Assert.IsFalse(FlatStep(flat, 1f, Laptop, Left, locked).writeLock, "tall: stays locked");
            Assert.IsTrue(flat.IsLocked);
            // The opposite wide side still flips upright.
            Assert.IsTrue(FlatStep(flat, 2f, BookFlipped, Left, locked).writeLock);
            Assert.AreEqual(Right, FlatStep(flat, 2f, BookFlipped, Left, locked).request);
        }

        #endregion

        #region State

        [Test]
        public void State_EqualityAndSidewaysEvents()
        {
            var flatTall = new IphoneDuoState(Flat, IphoneDuoHold.Tall, true, IphoneDuoScreenMode.Sideways);
            Assert.AreEqual(flatTall, new IphoneDuoState(Flat, IphoneDuoHold.Tall, true, IphoneDuoScreenMode.Sideways));
            Assert.AreNotEqual(flatTall, new IphoneDuoState(Flat, IphoneDuoHold.Tall, true, IphoneDuoScreenMode.Locked));
            Assert.AreEqual(default(IphoneDuoState).posture, IphoneDuoPosture.Unknown);

            Assert.IsTrue(IphoneDuoEvents.IsSideways(flatTall));
            Assert.IsTrue(IphoneDuoEvents.IsSideways(new IphoneDuoState(Flat, IphoneDuoHold.Tall, true, IphoneDuoScreenMode.Locked)),
                "the flat lock across the Player lock shows the game sideways");
            Assert.IsFalse(IphoneDuoEvents.IsSideways(new IphoneDuoState(Flat, IphoneDuoHold.Wide, false, IphoneDuoScreenMode.Locked)));
        }

        [Test]
        public void State_BuildState_AndToPosture()
        {
            Assert.AreEqual(new IphoneDuoState(Flat, IphoneDuoHold.Tall, true, IphoneDuoScreenMode.Sideways),
                IphoneDuoHoldOrientation.BuildState(Flat, TallAxis, LandscapeOnly, IphoneDuoScreenMode.Sideways, false));
            Assert.AreEqual(new IphoneDuoState(Flat, IphoneDuoHold.Wide, false, IphoneDuoScreenMode.Locked),
                IphoneDuoHoldOrientation.BuildState(Flat, LandscapeOnly, LandscapeOnly, IphoneDuoScreenMode.Native, true));
            Assert.AreEqual(new IphoneDuoState(Flat, IphoneDuoHold.Wide, true, IphoneDuoScreenMode.Sideways),
                IphoneDuoHoldOrientation.BuildState(Flat, LandscapeOnly, PortraitOnly, IphoneDuoScreenMode.Sideways, false),
                "a portrait game held wide is the mirror case");
            Assert.AreEqual(new IphoneDuoState(Cover, IphoneDuoHold.Unknown, false, IphoneDuoScreenMode.Native),
                IphoneDuoHoldOrientation.BuildState(Cover, default, LandscapeOnly, IphoneDuoScreenMode.Native, false),
                "no hold is never across the lock");

            Assert.AreEqual(Cover, IphoneDuoHinge.ToPosture(FoldableProductState.Closed));
            Assert.AreEqual(Seated, IphoneDuoHinge.ToPosture(FoldableProductState.HalfOpened));
            Assert.AreEqual(Flat, IphoneDuoHinge.ToPosture(FoldableProductState.Opened));
            Assert.AreEqual(IphoneDuoPosture.Unknown, IphoneDuoHinge.ToPosture(FoldableProductState.Undefined));
        }

        [Test]
        public void Device_Publish_RaisesOnlyOnChange()
        {
            IphoneDuoState previous = IphoneDuoDevice.State;
            int raised = 0;
            System.Action<IphoneDuoState> count = _ => raised++;
            IphoneDuoDevice.Publish(default);
            IphoneDuoDevice.StateChanged += count;
            try
            {
                var seated = new IphoneDuoState(Seated, IphoneDuoHold.Tall, true, IphoneDuoScreenMode.HoldAxis);
                IphoneDuoDevice.Publish(seated);
                IphoneDuoDevice.Publish(seated);
                Assert.AreEqual(1, raised);
                Assert.AreEqual(seated, IphoneDuoDevice.State);
                IphoneDuoDevice.Publish(default);
                Assert.AreEqual(2, raised);
            }
            finally
            {
                IphoneDuoDevice.StateChanged -= count;
                IphoneDuoDevice.Publish(previous);
            }
        }

        [Test]
        public void Events_FireOnTransitionsOnly()
        {
            var host = new GameObject("iPhone Duo Events test");
            try
            {
                var events = host.AddComponent<IphoneDuoEvents>();
                int cover = 0, flat = 0, wide = 0, tall = 0, started = 0, ended = 0, any = 0;
                events.onCover.AddListener(() => cover++);
                events.onFlat.AddListener(() => flat++);
                events.onWideHold.AddListener(() => wide++);
                events.onTallHold.AddListener(() => tall++);
                events.onSidewaysStarted.AddListener(() => started++);
                events.onSidewaysEnded.AddListener(() => ended++);
                events.onStateChanged.AddListener((p, h, m) => any++);

                events.Raise(new IphoneDuoState(Flat, IphoneDuoHold.Tall, true, IphoneDuoScreenMode.Sideways));
                Assert.AreEqual((1, 1, 1, 0, 1), (flat, tall, started, ended, any));

                // Turned view → flat lock across the Player lock: still sideways, nothing else changed.
                events.Raise(new IphoneDuoState(Flat, IphoneDuoHold.Tall, true, IphoneDuoScreenMode.Locked));
                Assert.AreEqual((1, 1, 1, 0, 2), (flat, tall, started, ended, any));

                events.Raise(new IphoneDuoState(Flat, IphoneDuoHold.Wide, false, IphoneDuoScreenMode.Locked));
                Assert.AreEqual((1, 1, 1, 1, 3), (wide, tall, started, ended, any));

                events.Raise(new IphoneDuoState(Cover, IphoneDuoHold.Wide, false, IphoneDuoScreenMode.Native));
                Assert.AreEqual((1, 1, 1, 1, 4), (cover, flat, wide, ended, any));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        #endregion

        #region Editor rules and build

        [Test]
        public void EditorRules_Resolve_PreviewsEachPostureAndHold()
        {
            var rules = Defaults;
            var flatTall = IphoneDuoEditorRules.Resolve(Flat, true, rules, LandscapeOnly, autoRotation: true);
            Assert.AreEqual(new IphoneDuoState(Flat, IphoneDuoHold.Tall, true, IphoneDuoScreenMode.Sideways), flatTall.state);
            Assert.IsTrue(flatTall.landscape, "Sideways renders the game's own landscape size");
            StringAssert.StartsWith("Sideways", flatTall.summary);

            var seatedLaptop = IphoneDuoEditorRules.Resolve(Seated, true, rules, LandscapeOnly, autoRotation: true);
            Assert.AreEqual(IphoneDuoScreenMode.HoldAxis, seatedLaptop.state.mode);
            Assert.IsFalse(seatedLaptop.landscape);

            var coverTall = IphoneDuoEditorRules.Resolve(Cover, true, rules, LandscapeOnly, autoRotation: true);
            Assert.AreEqual(new IphoneDuoState(Cover, IphoneDuoHold.Tall, true, IphoneDuoScreenMode.Native), coverTall.state);
            Assert.IsTrue(coverTall.landscape);

            var flatWide = IphoneDuoEditorRules.Resolve(Flat, false, rules, LandscapeOnly, autoRotation: true);
            Assert.AreEqual(new IphoneDuoState(Flat, IphoneDuoHold.Wide, false, IphoneDuoScreenMode.Native), flatWide.state);

            var mirror = IphoneDuoEditorRules.Resolve(Flat, false, rules, PortraitOnly, autoRotation: true);
            Assert.AreEqual(IphoneDuoScreenMode.Sideways, mirror.state.mode);
            Assert.IsFalse(mirror.landscape, "a portrait game held wide renders its portrait size");

            var both = IphoneDuoEditorRules.Resolve(Seated, false, rules, Flags(true, false, true, true), autoRotation: true);
            Assert.AreEqual(IphoneDuoScreenMode.HoldAxis, both.state.mode);
            Assert.IsTrue(both.landscape);

            var fixedOrientation = IphoneDuoEditorRules.Resolve(Seated, true, rules, LandscapeOnly, autoRotation: false);
            Assert.AreEqual(IphoneDuoScreenMode.Native, fixedOrientation.state.mode, "a fixed Default Orientation ignores the rules");
            Assert.IsTrue(fixedOrientation.landscape);
        }

        [Test]
        public void BuildPlist_LaunchOrientations_AddTheOtherAxisWhenOptedIn()
        {
            string[] landscape = { "UIInterfaceOrientationLandscapeLeft", "UIInterfaceOrientationLandscapeRight" };
            var on = With(r => r.launchScreenFillsAcrossHold = true);
            Assert.IsNull(IphoneDuoBuildPlist.LaunchOrientations(Defaults, LandscapeOnly, true, landscape), "opt-in");
            Assert.IsNull(IphoneDuoBuildPlist.LaunchOrientations(on, LandscapeOnly, false, landscape), "needs Auto Rotation");
            Assert.IsNull(IphoneDuoBuildPlist.LaunchOrientations(on, Flags(true, false, true, true), true, landscape), "two axes: nothing across");

            CollectionAssert.AreEqual(
                new[]
                {
                    "UIInterfaceOrientationLandscapeLeft", "UIInterfaceOrientationLandscapeRight",
                    "UIInterfaceOrientationPortrait", "UIInterfaceOrientationPortraitUpsideDown",
                },
                IphoneDuoBuildPlist.LaunchOrientations(on, LandscapeOnly, true, landscape));
            CollectionAssert.AreEqual(
                new[] { "UIInterfaceOrientationPortrait", "UIInterfaceOrientationLandscapeLeft", "UIInterfaceOrientationLandscapeRight" },
                IphoneDuoBuildPlist.LaunchOrientations(on, PortraitOnly, true, new[] { "UIInterfaceOrientationPortrait" }));
        }

        #endregion

        #region Hinge

        [Test]
        public void Hinge_BuildLayout_TallInner_HorizontalLaptopFold()
        {
            var layout = IphoneDuoHinge.BuildLayout(
                IphoneDuoHingeStatus.PartiallyOpen, 669f, 951f, 2007, 2853);

            Assert.AreEqual(FoldableProductState.HalfOpened, layout.productState);
            Assert.AreEqual(FoldableOrientation.Horizontal, layout.orientation);
            Assert.AreEqual(FoldablePosture.Tabletop, layout.ResolvePosture());
            // 40 pt division at 3 px/pt, centred on the inner display.
            Assert.AreEqual(2007, layout.foldBounds.width);
            Assert.AreEqual(120, layout.foldBounds.height);
            Assert.AreEqual(2853 / 2f, layout.foldBounds.y + layout.foldBounds.height / 2f, 1f);
        }

        [Test]
        public void Hinge_BuildLayout_CoverPortraitAndLandscape_AreClosed()
        {
            var portrait = IphoneDuoHinge.BuildLayout(
                IphoneDuoHingeStatus.Closed, 466f, 678f, 1398, 2034);
            var landscape = IphoneDuoHinge.BuildLayout(
                IphoneDuoHingeStatus.Closed, 678f, 466f, 2034, 1398);

            Assert.AreEqual(FoldableProductState.Closed, portrait.productState);
            Assert.AreEqual(FoldableProductState.Closed, landscape.productState);
            Assert.IsTrue(portrait.isCoverScreen);
            Assert.IsTrue(landscape.isCoverScreen);
            Assert.IsFalse(landscape.isSeparating);
        }

        [Test]
        public void Hinge_IsFoldableModel_OnlyIphoneDuo()
        {
            Assert.IsTrue(IphoneDuoHinge.IsFoldableModel("iPhone19,4"));
            Assert.IsFalse(IphoneDuoHinge.IsFoldableModel("iPhone18,4"));
            Assert.IsFalse(IphoneDuoHinge.IsFoldableModel("iPad16,3"));
            Assert.IsFalse(IphoneDuoHinge.IsFoldableModel(""));
        }

        [Test]
        public void Hinge_ResolveModelIdentifier_SimulatorUsesSimulatedModel()
        {
            // The Xcode Simulator reports the host CPU as deviceModel.
            Assert.AreEqual("iPhone19,4", IphoneDuoHinge.ResolveModelIdentifier("arm64", "iPhone19,4"));
            Assert.AreEqual("iPhone19,4", IphoneDuoHinge.ResolveModelIdentifier("iPhone19,4", null));
            Assert.AreEqual("iPhone18,4", IphoneDuoHinge.ResolveModelIdentifier("iPhone18,4", ""));
            Assert.IsFalse(IphoneDuoHinge.IsFoldableModel(
                IphoneDuoHinge.ResolveModelIdentifier("arm64", null)));
        }

        [Test]
        public void HingeStatus_MirrorsNativeCodes()
        {
            // Must match FDPHinge* in Runtime/Plugins/iOS/IphoneDuoHinge.mm.
            Assert.AreEqual(-1, (int)IphoneDuoHingeStatus.None);
            Assert.AreEqual(0, (int)IphoneDuoHingeStatus.Unknown);
            Assert.AreEqual(1, (int)IphoneDuoHingeStatus.Closed);
            Assert.AreEqual(2, (int)IphoneDuoHingeStatus.PartiallyOpen);
            Assert.AreEqual(3, (int)IphoneDuoHingeStatus.FullyOpen);
        }

        [Test]
        public void Hinge_CoverView_IsClosedWhateverTheHingeSays()
        {
            // Cover 2034x1398 px = 678x466 pt. Hinge status can lag the display switch.
            Assert.AreEqual(
                FoldableProductState.Closed,
                IphoneDuoHinge.MapProduct(IphoneDuoHingeStatus.Closed, 678f, 466f));
            Assert.AreEqual(
                FoldableProductState.Closed,
                IphoneDuoHinge.MapProduct(IphoneDuoHingeStatus.FullyOpen, 466f, 678f));
            Assert.AreEqual(
                FoldableProductState.Closed,
                IphoneDuoHinge.MapProduct(IphoneDuoHingeStatus.None, 678f, 466f));
        }

        [Test]
        public void Hinge_CoverPartiallyOpen_IsStanding_NotSeated()
        {
            // Standing: cover view, hinge partly open → Closed (iPhone Duo Cover), not Seated.
            Assert.AreEqual(
                FoldableProductState.Closed,
                IphoneDuoHinge.MapProduct(IphoneDuoHingeStatus.PartiallyOpen, 678f, 466f));
            Assert.AreEqual(
                FoldableProductState.Closed,
                IphoneDuoHinge.MapProduct(IphoneDuoHingeStatus.PartiallyOpen, 466f, 678f));
            Assert.AreEqual(
                FoldableProductState.HalfOpened,
                IphoneDuoHinge.MapProduct(IphoneDuoHingeStatus.PartiallyOpen, 951f, 669f));
        }

        [Test]
        public void Hinge_IsInnerViewSize_SplitsCoverAndInner()
        {
            // Duo cover 466×678 pt, inner 669×951 pt (either orientation).
            Assert.IsFalse(IphoneDuoHinge.IsInnerViewSize(466f, 678f));
            Assert.IsFalse(IphoneDuoHinge.IsInnerViewSize(678f, 466f));
            Assert.IsTrue(IphoneDuoHinge.IsInnerViewSize(669f, 951f));
            Assert.IsTrue(IphoneDuoHinge.IsInnerViewSize(951f, 669f));
        }

        [Test]
        public void Hinge_InnerView_HingeSplitsOpenedAndHalfOpened()
        {
            Assert.AreEqual(
                FoldableProductState.Opened,
                IphoneDuoHinge.MapProduct(IphoneDuoHingeStatus.FullyOpen, 951f, 669f));
            Assert.AreEqual(
                FoldableProductState.HalfOpened,
                IphoneDuoHinge.MapProduct(IphoneDuoHingeStatus.PartiallyOpen, 951f, 669f));
            // iOS < 27.1 / unknown status: inner size alone is Opened.
            Assert.AreEqual(
                FoldableProductState.Opened,
                IphoneDuoHinge.MapProduct(IphoneDuoHingeStatus.None, 669f, 951f));
            Assert.AreEqual(
                FoldableProductState.Opened,
                IphoneDuoHinge.MapProduct(IphoneDuoHingeStatus.Unknown, 951f, 669f));
        }

        [Test]
        public void Hinge_BuildLayout_WideInner_VerticalBookFold()
        {
            var layout = IphoneDuoHinge.BuildLayout(
                IphoneDuoHingeStatus.PartiallyOpen, 951f, 669f, 2853, 2007);

            Assert.AreEqual(FoldableDeviceKind.FoldablePhone, layout.deviceKind);
            Assert.AreEqual(FoldableProductState.HalfOpened, layout.productState);
            Assert.AreEqual(FoldableState.HalfOpened, layout.state);
            Assert.IsTrue(layout.isSeparating);
            Assert.IsFalse(layout.isCoverScreen);
            Assert.AreEqual(FoldableOrientation.Vertical, layout.orientation);
            Assert.AreEqual(FoldablePosture.Book, layout.ResolvePosture());
            // 40 pt division at 3 px/pt, centred on the inner display.
            Assert.AreEqual(120, layout.foldBounds.width);
            Assert.AreEqual(2007, layout.foldBounds.height);
            Assert.AreEqual(2853 / 2f, layout.foldBounds.x + layout.foldBounds.width / 2f, 1f);
        }

        [Test]
        public void Hinge_BuildLayout_Cover_IsClosedCoverScreen()
        {
            var layout = IphoneDuoHinge.BuildLayout(
                IphoneDuoHingeStatus.Closed, 678f, 466f, 2034, 1398);

            Assert.AreEqual(FoldableProductState.Closed, layout.productState);
            Assert.IsTrue(layout.isCoverScreen);
            Assert.IsFalse(layout.isSeparating);
            Assert.AreEqual(new RectInt(0, 0, 2034, 1398), layout.currentMetrics);
        }

        [Test]
        public void Hinge_BuildLayout_ScaledRender_KeepsFoldInRenderPixels()
        {
            // Unity Target DPI can render below native; points stay the same.
            var layout = IphoneDuoHinge.BuildLayout(
                IphoneDuoHingeStatus.PartiallyOpen, 951f, 669f, 1335, 939);

            Assert.AreEqual(FoldableProductState.HalfOpened, layout.productState);
            Assert.AreEqual(56, layout.foldBounds.width);
        }

        [Test]
        public void Hinge_SurvivesEnrichWithPreviousProduct()
        {
            var layout = IphoneDuoHinge.BuildLayout(
                IphoneDuoHingeStatus.FullyOpen, 951f, 669f, 2853, 2007);
            FoldableProductStateResolver.Enrich(
                layout, FoldableDeviceKind.Unknown, FoldableProductState.HalfOpened);
            Assert.AreEqual(FoldableProductState.Opened, layout.productState);

            var cover = IphoneDuoHinge.BuildLayout(
                IphoneDuoHingeStatus.FullyOpen, 678f, 466f, 2034, 1398);
            FoldableProductStateResolver.Enrich(
                cover, FoldableDeviceKind.Unknown, FoldableProductState.Opened);
            Assert.AreEqual(FoldableProductState.Closed, cover.productState);
            Assert.IsTrue(cover.isCoverScreen);
        }

        #endregion
    }
}
