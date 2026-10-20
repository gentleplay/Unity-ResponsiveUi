using System.Reflection;
using FDPlus.Foldable.Editor;
using FDPlus.ResponsiveUI;
using FDPlus.ResponsiveUI.Editor;
using NUnit.Framework;
using UnityEngine;

namespace FDPlus.ResponsiveUI.Tests
{
    /// <summary>
    /// Simulator pipeline contracts that do not require a live Device Simulator window.
    /// </summary>
    public class ResponsiveUiSimulatorIntegrationTests
    {
        [Test]
        public void Scheduler_ExposesCanvasCatchupFlag()
        {
            ResponsiveUiEditorLayoutScheduler.CancelPending();
            Assert.IsFalse(ResponsiveUiEditorLayoutScheduler.CanvasCatchupQueued);
        }

        [Test]
        public void SlotFocus_PublicApplyEntryPointsExist()
        {
            Assert.IsNotNull(
                typeof(ResponsiveUiSimulatorSlotFocus).GetMethod("ApplyFocusedLayoutNow"));
            Assert.IsNotNull(
                typeof(ResponsiveUiSimulatorSlotFocus).GetMethod("ApplyFocusedLayoutAfterCanvas"));
            Assert.IsNotNull(
                typeof(ResponsiveUiSimulatorSlotFocus).GetMethod("GetFocusedLayout"));
        }

        [Test]
        public void SlotFocus_ResolveFoldoutSlotName_MatchesExactForm()
        {
            AssertFocusedSlot(
                ResponsiveDevice.FormFactor.Phone,
                ResponsiveDevice.Orientation.Landscape,
                "phone");
            AssertFocusedSlot(
                ResponsiveDevice.FormFactor.Pad,
                ResponsiveDevice.Orientation.Landscape,
                "pad");
            AssertFocusedSlot(
                ResponsiveDevice.FormFactor.FoldPhoneCover,
                ResponsiveDevice.Orientation.Landscape,
                "foldPhoneCover");
            AssertFocusedSlot(
                ResponsiveDevice.FormFactor.FlipPhoneCover,
                ResponsiveDevice.Orientation.Landscape,
                "flipPhoneCover");
            AssertFocusedSlot(
                ResponsiveDevice.FormFactor.FoldHalfOpened,
                ResponsiveDevice.Orientation.Landscape,
                "foldHalfOpened");
            AssertFocusedSlot(
                ResponsiveDevice.FormFactor.FoldCoverHalfOpened,
                ResponsiveDevice.Orientation.Landscape,
                "foldCoverHalfOpened");
            AssertFocusedSlot(
                ResponsiveDevice.FormFactor.FlipHalfOpened,
                ResponsiveDevice.Orientation.Landscape,
                "flipHalfOpened");
        }

        // Seated Landscape is the wide screen, Seated Portrait the tall one. Seated allows only the hold's
        // axis whatever the lock, so each pose focuses its own slot under any lock.
        [TestCase(false, true, "iPhoneDuoSeatedLandscape", "iPhoneDuoSeatedPortrait", TestName = "SlotFocus_DuoSeated_LandscapeOnly")]
        [TestCase(true, false, "iPhoneDuoSeatedLandscape", "iPhoneDuoSeatedPortrait", TestName = "SlotFocus_DuoSeated_PortraitOnly")]
        [TestCase(true, true, "iPhoneDuoSeatedLandscape", "iPhoneDuoSeatedPortrait", TestName = "SlotFocus_DuoSeated_BothAxes")]
        public void SlotFocus_ResolveFoldoutSlotName_IphoneDuoSeatedOwnSlotUnderAnyLock(
            bool portrait,
            bool landscape,
            string expectedWide,
            string expectedTall)
        {
            ResponsiveEditorTestUtil.WithPlayerAxes(portrait, landscape, () =>
            {
                Assert.AreEqual(
                    expectedWide,
                    ResponsiveUiSimulatorSlotFocus.ResolveFoldoutSlotName(new ResponsiveDevice.LayoutKey(
                        ResponsiveDevice.FormFactor.iPhoneDuoSeated, ResponsiveDevice.Orientation.Landscape)),
                    "wide book pose");
                Assert.AreEqual(
                    expectedTall,
                    ResponsiveUiSimulatorSlotFocus.ResolveFoldoutSlotName(new ResponsiveDevice.LayoutKey(
                        ResponsiveDevice.FormFactor.iPhoneDuoSeated, ResponsiveDevice.Orientation.Portrait)),
                    "tall laptop pose");
            });
        }

        // With no Simulator profile, focus follows the build target.
        [TestCase(ResponsivePlatform.Ios, ResponsiveDevice.FormFactor.FoldPhoneCover, ResponsiveDevice.FormFactor.Phone, TestName = "SlotFocus_Ios_FoldCoverFocusesPhone")]
        [TestCase(ResponsivePlatform.Android, ResponsiveDevice.FormFactor.iPhoneDuoSeated, ResponsiveDevice.FormFactor.Pad, TestName = "SlotFocus_Android_DuoSeatedFocusesPad")]
        [TestCase(ResponsivePlatform.WideScreen, ResponsiveDevice.FormFactor.Pad, ResponsiveDevice.FormFactor.WideScreen, TestName = "SlotFocus_WideScreen_PadFocusesWideScreen")]
        public void SlotFocus_ConstrainFocusKey_UsesBuildTargetForm(
            ResponsivePlatform platform,
            ResponsiveDevice.FormFactor form,
            ResponsiveDevice.FormFactor expected)
        {
            ResponsiveEditorTestUtil.WithPlayerAxes(true, true, () =>
            {
                ResponsiveEditorTestUtil.WithPlatform(platform, () =>
                {
                    var key = (ResponsiveDevice.LayoutKey)FocusMethod("ConstrainFocusKey").Invoke(
                        null,
                        new object[] { new ResponsiveDevice.LayoutKey(form, ResponsiveDevice.Orientation.Landscape) });
                    Assert.AreEqual(
                        new ResponsiveDevice.LayoutKey(expected, ResponsiveDevice.Orientation.Landscape),
                        key);
                    Assert.AreEqual(
                        ResponsiveWidgetLayout.StatePropertyName(key),
                        ResponsiveUiSimulatorSlotFocus.ResolveFoldoutSlotName(key));
                });
            });
        }

        [Test]
        public void ResolveLiveLayout_SimulatorPin_BeatsStaleFocus()
        {
            FieldInfo[] fields =
            {
                FocusField("_hasFocus"),
                FocusField("_focusedSlotName"),
                FocusField("_focusedLayout"),
                FocusField("_fingerprint"),
            };
            object[] saved = new object[fields.Length];
            for (int i = 0; i < fields.Length; i++)
                saved[i] = fields[i].GetValue(null);
            try
            {
                ResponsiveEditorTestUtil.WithPlayerAxes(true, true, () =>
                {
                    ResponsiveEditorTestUtil.WithPlatform(ResponsivePlatform.Ios, () =>
                    {
                        SetStaleFocus(
                            fields,
                            new ResponsiveDevice.LayoutKey(
                                ResponsiveDevice.FormFactor.Phone,
                                ResponsiveDevice.Orientation.Landscape));
                        ResponsiveDevice.SetSimulatorLayout(
                            ResponsiveDevice.FormFactor.Phone,
                            ResponsiveDevice.Orientation.Landscape,
                            1972,
                            1248);
                        ResponsiveDevice.LayoutKey live =
                            ResponsiveUiDetectionDrawer.ResolveLiveLayout(null);
                        Assert.AreEqual(ResponsiveDevice.FormFactor.FoldPhoneCover, live.Form);
                        Assert.AreEqual(ResponsiveDevice.Orientation.Landscape, live.Orientation);
                    });

                    ResponsiveEditorTestUtil.WithPlatform(ResponsivePlatform.Android, () =>
                    {
                        SetStaleFocus(
                            fields,
                            new ResponsiveDevice.LayoutKey(
                                ResponsiveDevice.FormFactor.Pad,
                                ResponsiveDevice.Orientation.Landscape));
                        ResponsiveDevice.SetSimulatorLayout(
                            ResponsiveDevice.FormFactor.iPhoneDuoCover,
                            ResponsiveDevice.Orientation.Portrait,
                            1398,
                            2034);
                        Assert.AreEqual(
                            ResponsiveDevice.FormFactor.iPhoneDuoCover,
                            ResponsiveUiDetectionDrawer.ResolveLiveLayout(null).Form);
                    });
                });
            }
            finally
            {
                for (int i = 0; i < fields.Length; i++)
                    fields[i].SetValue(null, saved[i]);
                ResponsiveDevice.ClearSimulatorLayout();
            }
        }

        [Test]
        public void SlotFocus_SimulatorPin_KeepsDuoOnAndroid_AndFoldOnWideScreen()
        {
            ResponsiveEditorTestUtil.WithPlayerAxes(true, true, () =>
            {
                try
                {
                    ResponsiveEditorTestUtil.WithPlatform(ResponsivePlatform.Android, () =>
                    {
                        ResponsiveDevice.SetSimulatorLayout(
                            ResponsiveDevice.FormFactor.iPhoneDuoCover,
                            ResponsiveDevice.Orientation.Portrait,
                            1398,
                            2034);
                        var key = (ResponsiveDevice.LayoutKey)FocusMethod("ConstrainFocusKey").Invoke(
                            null,
                            new object[]
                            {
                                new ResponsiveDevice.LayoutKey(
                                    ResponsiveDevice.FormFactor.iPhoneDuoCover,
                                    ResponsiveDevice.Orientation.Portrait),
                            });
                        Assert.AreEqual(ResponsiveDevice.FormFactor.iPhoneDuoCover, key.Form);
                    });
                    ResponsiveEditorTestUtil.WithPlatform(ResponsivePlatform.WideScreen, () =>
                    {
                        ResponsiveDevice.SetSimulatorLayout(
                            ResponsiveDevice.FormFactor.FoldPhoneCover,
                            ResponsiveDevice.Orientation.Landscape,
                            1972,
                            1248);
                        var key = (ResponsiveDevice.LayoutKey)FocusMethod("ConstrainFocusKey").Invoke(
                            null,
                            new object[]
                            {
                                new ResponsiveDevice.LayoutKey(
                                    ResponsiveDevice.FormFactor.FoldPhoneCover,
                                    ResponsiveDevice.Orientation.Landscape),
                            });
                        Assert.AreEqual(ResponsiveDevice.FormFactor.FoldPhoneCover, key.Form);
                    });
                }
                finally
                {
                    ResponsiveDevice.ClearSimulatorLayout();
                }
            });
        }

        [Test]
        public void SlotFocus_QuietUpdate_DropsStaleSeatedFocusOnIos()
        {
            FieldInfo[] fields =
            {
                FocusField("_hasFocus"),
                FocusField("_focusedSlotName"),
                FocusField("_focusedLayout"),
                FocusField("_fingerprint"),
            };
            object[] saved = new object[fields.Length];
            for (int i = 0; i < fields.Length; i++)
                saved[i] = fields[i].GetValue(null);
            MethodInfo quietUpdate = FocusMethod("QuietUpdateFocus");
            var seated = new ResponsiveDevice.LayoutKey(
                ResponsiveDevice.FormFactor.iPhoneDuoSeated,
                ResponsiveDevice.Orientation.Portrait);
            try
            {
                ResponsiveEditorTestUtil.WithPlatform(ResponsivePlatform.Ios, () =>
                {
                    // A Fold Cover on iOS focuses an iOS slot, never the previous Seated one.
                    ResponsiveEditorTestUtil.WithPlayerAxes(true, true, () =>
                    {
                        SetStaleFocus(fields, seated);
                        quietUpdate.Invoke(null, new object[]
                        {
                            new ResponsiveDevice.LayoutKey(
                                ResponsiveDevice.FormFactor.FoldPhoneCover,
                                ResponsiveDevice.Orientation.Portrait),
                        });
                        Assert.IsTrue(ResponsiveUiSimulatorSlotFocus.GetFocusedLayout(out var layout));
                        Assert.AreNotEqual(seated, layout);
                        Assert.IsTrue(
                            ResponsivePlatforms.Owns(ResponsivePlatform.Ios, layout.Form),
                            layout.Form.ToString());
                    });

                    // No visible slot (portrait hidden by a landscape-only lock): focus is dropped.
                    ResponsiveEditorTestUtil.WithPlayerAxes(false, true, () =>
                    {
                        SetStaleFocus(fields, seated);
                        int revision = ResponsiveUiSimulatorSlotFocus.Revision;
                        quietUpdate.Invoke(null, new object[]
                        {
                            new ResponsiveDevice.LayoutKey(
                                ResponsiveDevice.FormFactor.Phone,
                                ResponsiveDevice.Orientation.Portrait),
                        });
                        Assert.IsFalse(ResponsiveUiSimulatorSlotFocus.GetFocusedLayout(out _));
                        Assert.IsFalse(ResponsiveUiSimulatorSlotFocus.GetFocusedSlotName(out _));
                        Assert.AreNotEqual(revision, ResponsiveUiSimulatorSlotFocus.Revision);
                    });
                });
            }
            finally
            {
                for (int i = 0; i < fields.Length; i++)
                    fields[i].SetValue(null, saved[i]);
            }
        }

        static void SetStaleFocus(FieldInfo[] fields, ResponsiveDevice.LayoutKey key)
        {
            fields[0].SetValue(null, true);
            fields[1].SetValue(null, ResponsiveWidgetLayout.StatePropertyName(key));
            fields[2].SetValue(null, key);
            fields[3].SetValue(null, "stale");
        }

        static MethodInfo FocusMethod(string name)
        {
            MethodInfo method = typeof(ResponsiveUiSimulatorSlotFocus).GetMethod(
                name, BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(method, name);
            return method;
        }

        static FieldInfo FocusField(string name)
        {
            FieldInfo field = typeof(ResponsiveUiSimulatorSlotFocus).GetField(
                name, BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(field, name);
            return field;
        }

        static void AssertFocusedSlot(
            ResponsiveDevice.FormFactor form,
            ResponsiveDevice.Orientation orientation,
            string prefix)
        {
            var key = new ResponsiveDevice.LayoutKey(form, orientation);
            string slot = ResponsiveUiSimulatorSlotFocus.ResolveFoldoutSlotName(key);
            string exact = ResponsiveWidgetLayout.StatePropertyName(key);
            string portrait = ResponsiveWidgetLayout.StatePropertyName(
                form, ResponsiveDevice.Orientation.Portrait);
            Assert.IsNotNull(slot);
            Assert.IsTrue(slot == exact || slot == portrait, slot);
            Assert.IsTrue(slot.StartsWith(prefix, System.StringComparison.Ordinal), slot);
        }

        [Test]
        public void Bridge_PrefersActiveSelectionApi()
        {
            Assert.IsNotNull(
                typeof(SimulatorDeviceBridge).GetMethod("GetActiveSelection"));
            Assert.IsNotNull(
                typeof(SimulatorDeviceBridge).GetMethod("RequestSelectionPublish"));
            Assert.IsNotNull(
                typeof(SimulatorDeviceBridge).GetProperty("IsSelectionPending"));
            Assert.IsFalse(Application.isPlaying);
        }

        [Test]
        public void Session_ExposesScreenSyncedAfterCoverToMain()
        {
            Assert.IsNotNull(
                typeof(FoldableEditorSession).GetEvent("ScreenSynced"));
            Assert.IsNotNull(
                typeof(FoldableEditorSession).GetMethod("PublishScreenSynced"));
        }

        [Test]
        public void ScreenCommit_ExposesDeferredAndImmediatePaths()
        {
            Assert.IsNotNull(
                typeof(FoldableSimulatorScreenCommit).GetMethod("QueueDeferred"));
            Assert.IsNotNull(
                typeof(FoldableSimulatorScreenCommit).GetMethod("CommitNowThenCatchup"));
            Assert.IsNotNull(
                typeof(FoldableSimulatorScreenCommit).GetMethod("CommitNow"));
            Assert.IsNotNull(
                typeof(FoldableSimulatorScreenCommit).GetProperty("InFlight"));
        }

        [Test]
        public void SlotFocus_CoverToMainSize_RequiresLayoutApply()
        {
            Vector2 cover = ResponsiveCatalogDevices.FlipCoverLandscape;
            Vector2 main = ResponsiveCatalogDevices.FlipMainLandscape;
            Assert.IsTrue(
                ResponsiveUiSimulatorSlotFocus.ScreenSizeRequiresLayoutApply(cover, main));
            Assert.IsFalse(
                ResponsiveUiSimulatorSlotFocus.ScreenSizeRequiresLayoutApply(
                    main,
                    main + new Vector2(1f, 0f)));
        }
    }
}
