using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using FDPlus.ResponsiveUI;
using FDPlus.ResponsiveUI.Shared.Editor;
using FDPlus.ResponsiveUI.Editor;

using static FDPlus.ResponsiveUI.Tests.ResponsiveEditorTestUtil;

namespace FDPlus.ResponsiveUI.Tests
{
    /// <summary>
    /// Inspector contracts for slot seeding, Phone copy-on-enable, foldout
    /// clipboard behavior, and exact-slot bulk copy.
    /// </summary>
    public class ResponsiveSlotSeedTests
    {
    #region Widget

        [Test]
        public void CopyFallback_PadLandscape_CopiesPhoneLandscape()
        {
            WithWidget((so, widget) =>
            {
                WriteWidgetLayout(widget, "phoneLandscape", PhoneSample(landscape: true), enabled: true);
                WriteWidgetLayout(widget, "padLandscape", default, enabled: true);
                so.ApplyModifiedPropertiesWithoutUndo();

                SerializedProperty pad = widget.FindPropertyRelative("padLandscape");
                Assert.IsTrue(ResponsiveRectStateEditorUtil.CopyFallback(pad));

                ResponsiveRectState copied = ResponsiveRectStateEditorUtil.ReadState(pad);
                AssertLayoutEquals(PhoneSample(landscape: true), copied);
                Assert.IsTrue(copied.enabled);
                Assert.AreEqual(Vector2.zero, copied.designCanvasSize);
            });
        }

        [Test]
        public void WriteState_UnsetDesignCanvasAndDefaultScale_StaySentinels()
        {
            WithWidget((so, widget) =>
            {
                var state = PhoneSample(landscape: true);
                state.designCanvasSize = Vector2.zero;
                state.defaultScale = Vector2.zero;
                WriteWidgetLayout(widget, "padLandscape", state, enabled: true);
                so.ApplyModifiedPropertiesWithoutUndo();

                ResponsiveRectState written = ResponsiveRectStateEditorUtil.ReadState(
                    widget.FindPropertyRelative("padLandscape"));
                Assert.AreEqual(Vector2.zero, written.designCanvasSize);
                Assert.AreEqual(Vector2.zero, written.defaultScale);
            });
        }

        [Test]
        public void CopyDesignScaleSettingsFrom_KeepsUnsetDesignCanvasSize()
        {
            var source = PhoneSample(landscape: true);
            source.designCanvasSize = Vector2.zero;
            var captured = PhoneSample(landscape: false);
            captured.CopyDesignScaleSettingsFrom(source);
            Assert.AreEqual(Vector2.zero, captured.designCanvasSize);
            Assert.AreEqual(source.scaleFromDesignCanvas, captured.scaleFromDesignCanvas);
            Assert.AreEqual(source.designScaleMode, captured.designScaleMode);
        }

        [Test]
        public void CopyDesignScaleSettingsFrom_ClearsAuthoredDesignCanvasSize()
        {
            var source = PhoneSample(landscape: true);
            source.designCanvasSize = new Vector2(2048f, 1536f);
            var captured = PhoneSample(landscape: false);
            captured.CopyDesignScaleSettingsFrom(source);
            Assert.AreEqual(Vector2.zero, captured.designCanvasSize);
        }

        [Test]
        public void CopyFallback_PadLandscape_UsesPhoneLandscapeNotPortrait()
        {
            WithWidget((so, widget) =>
            {
                WriteWidgetLayout(widget, "phonePortrait", PhoneSample(landscape: false), enabled: true);
                WriteWidgetLayout(widget, "phoneLandscape", PhoneSample(landscape: true), enabled: true);
                WriteWidgetLayout(widget, "padLandscape", PhoneSample(landscape: false), enabled: true);
                so.ApplyModifiedPropertiesWithoutUndo();

                SerializedProperty pad = widget.FindPropertyRelative("padLandscape");
                Assert.IsTrue(ResponsiveRectStateEditorUtil.CopyFallback(pad));
                AssertLayoutEquals(
                    PhoneSample(landscape: true),
                    ResponsiveRectStateEditorUtil.ReadState(pad));
            });
        }

        [Test]
        public void CopyFallback_OverwritesAuthoredPadWithPhonePortrait()
        {
            WithWidget((so, widget) =>
            {
                WriteWidgetLayout(widget, "phonePortrait", PhoneSample(landscape: false), enabled: true);
                var authored = new ResponsiveRectState
                {
                    anchorMin = new Vector2(0.2f, 0.3f),
                    anchorMax = new Vector2(0.4f, 0.5f),
                    anchoredPosition = new Vector2(11f, 22f),
                    sizeDelta = new Vector2(33f, 44f),
                    pivot = new Vector2(0.1f, 0.9f),
                };
                WriteWidgetLayout(widget, "padPortrait", authored, enabled: true);
                so.ApplyModifiedPropertiesWithoutUndo();

                SerializedProperty pad = widget.FindPropertyRelative("padPortrait");
                Assert.IsTrue(ResponsiveRectStateEditorUtil.CopyFallback(pad));
                AssertLayoutEquals(
                    PhoneSample(landscape: false),
                    ResponsiveRectStateEditorUtil.ReadState(pad));
            });
        }

        [Test]
        public void CopyFallback_PhonePortrait_HasNoSource()
        {
            WithWidget((_, widget) =>
            {
                SerializedProperty phone = widget.FindPropertyRelative("phonePortrait");
                Assert.IsFalse(ResponsiveRectStateEditorUtil.CopyFallback(phone));
            });
        }

        [Test]
        public void CopyFallback_PhoneLandscape_HasNoSource()
        {
            WithWidget((so, widget) =>
            {
                WriteWidgetLayout(widget, "phonePortrait", PhoneSample(landscape: false), enabled: true);
                so.ApplyModifiedPropertiesWithoutUndo();
                Assert.IsFalse(ResponsiveRectStateEditorUtil.CopyFallback(
                    widget.FindPropertyRelative("phoneLandscape")));
            });
        }

        [Test]
        public void CopyFallback_IphoneDuoSeated_FollowsRuntimeShapeChain()
        {
            WithWidget((so, widget) =>
            {
                var padWide = new ResponsiveRectState
                {
                    anchorMin = new Vector2(0.2f, 0.3f),
                    anchorMax = new Vector2(0.4f, 0.5f),
                    anchoredPosition = new Vector2(11f, 22f),
                    sizeDelta = new Vector2(33f, 44f),
                    pivot = new Vector2(0.1f, 0.9f),
                };
                var padTall = new ResponsiveRectState
                {
                    anchorMin = new Vector2(0.05f, 0.15f),
                    anchorMax = new Vector2(0.6f, 0.7f),
                    anchoredPosition = new Vector2(55f, 66f),
                    sizeDelta = new Vector2(77f, 88f),
                    pivot = new Vector2(0.5f, 0.25f),
                };
                var duoBook = new ResponsiveRectState
                {
                    anchorMin = new Vector2(0.7f, 0.7f),
                    anchorMax = new Vector2(0.9f, 0.9f),
                    anchoredPosition = new Vector2(1f, 2f),
                    sizeDelta = new Vector2(3f, 4f),
                    pivot = new Vector2(0.5f, 0.5f),
                };
                WriteWidgetLayout(widget, "phonePortrait", PhoneSample(landscape: false), enabled: true);
                WriteWidgetLayout(widget, "phoneLandscape", PhoneSample(landscape: true), enabled: true);
                WriteWidgetLayout(widget, "padLandscape", padWide, enabled: true);
                WriteWidgetLayout(widget, "padPortrait", padTall, enabled: true);
                WriteWidgetLayout(widget, "iPhoneDuoSeatedPortrait", default, enabled: true);
                WriteWidgetLayout(widget, "iPhoneDuoSeatedLandscape", duoBook, enabled: true);
                so.ApplyModifiedPropertiesWithoutUndo();

                // Laptop (tall) Duo Portrait: Pad Portrait, never the wide Duo Landscape sibling.
                SerializedProperty duoPortrait =
                    widget.FindPropertyRelative("iPhoneDuoSeatedPortrait");
                Assert.IsTrue(ResponsiveRectStateEditorUtil.CopyFallback(duoPortrait));
                AssertLayoutEquals(padTall, ResponsiveRectStateEditorUtil.ReadState(duoPortrait));

                // Book (wide) Duo Landscape: Pad Landscape.
                SerializedProperty duoLandscape =
                    widget.FindPropertyRelative("iPhoneDuoSeatedLandscape");
                Assert.IsTrue(ResponsiveRectStateEditorUtil.CopyFallback(duoLandscape));
                AssertLayoutEquals(padWide, ResponsiveRectStateEditorUtil.ReadState(duoLandscape));

                // Pad in the matching shape disabled: the other Pad axis still beats Phone.
                WriteWidgetLayout(widget, "padPortrait", padTall, enabled: false);
                so.ApplyModifiedPropertiesWithoutUndo();
                Assert.IsTrue(ResponsiveRectStateEditorUtil.CopyFallback(duoPortrait));
                AssertLayoutEquals(padWide, ResponsiveRectStateEditorUtil.ReadState(duoPortrait));

                // No enabled Pad: Phone in the matching shape.
                WriteWidgetLayout(widget, "padLandscape", padWide, enabled: false);
                so.ApplyModifiedPropertiesWithoutUndo();
                Assert.IsTrue(ResponsiveRectStateEditorUtil.CopyFallback(duoPortrait));
                AssertLayoutEquals(
                    PhoneSample(landscape: false),
                    ResponsiveRectStateEditorUtil.ReadState(duoPortrait));
                Assert.IsTrue(ResponsiveRectStateEditorUtil.CopyFallback(duoLandscape));
                AssertLayoutEquals(
                    PhoneSample(landscape: true),
                    ResponsiveRectStateEditorUtil.ReadState(duoLandscape));
            });
        }

        // (portrait allowed, landscape allowed) → Duo Seated Portrait (tall laptop),
        // Duo Seated Landscape (wide book) allowed. Seated allows only the hold's axis, so both always show.
        [TestCase(false, true, true, true, TestName = "LandscapeOnly_KeepsBothSeatedSlots")]
        [TestCase(true, false, true, true, TestName = "PortraitOnly_KeepsBothSeatedSlots")]
        [TestCase(true, true, true, true, TestName = "BothAxes_KeepsBothSeatedSlots")]
        public void SlotAllowedByPlayerAxes_IphoneDuoSeatedAlwaysAllowed(
            bool portrait,
            bool landscape,
            bool expectedLaptop,
            bool expectedBook)
        {
            Assert.AreEqual(expectedLaptop, ResponsiveDevice.SlotAllowedByPlayerAxes(
                ResponsiveDevice.FormFactor.iPhoneDuoSeated,
                ResponsiveDevice.Orientation.Portrait,
                portrait,
                landscape), "Seated Portrait (laptop)");
            Assert.AreEqual(expectedBook, ResponsiveDevice.SlotAllowedByPlayerAxes(
                ResponsiveDevice.FormFactor.iPhoneDuoSeated,
                ResponsiveDevice.Orientation.Landscape,
                portrait,
                landscape), "Seated Landscape (book)");
            Assert.AreEqual(landscape, ResponsiveDevice.SlotAllowedByPlayerAxes(
                ResponsiveDevice.FormFactor.FoldHalfOpened,
                ResponsiveDevice.Orientation.Landscape,
                portrait,
                landscape), "Fold Half Opened Landscape follows the lock (it names the posture)");
            Assert.AreEqual(landscape, ResponsiveDevice.SlotAllowedByPlayerAxes(
                ResponsiveDevice.FormFactor.Pad,
                ResponsiveDevice.Orientation.Landscape,
                portrait,
                landscape), "Pad Landscape");
            Assert.AreEqual(portrait, ResponsiveDevice.SlotAllowedByPlayerAxes(
                ResponsiveDevice.FormFactor.Pad,
                ResponsiveDevice.Orientation.Portrait,
                portrait,
                landscape), "Pad Portrait");

            WithPlayerAxes(portrait, landscape, () =>
            {
                Assert.AreEqual(expectedLaptop, ResponsiveUiEditorSettings.SlotAllowed("iPhoneDuoSeatedPortrait"));
                Assert.AreEqual(expectedBook, ResponsiveUiEditorSettings.SlotAllowed("iPhoneDuoSeatedLandscape"));
                for (int i = 0; i < ResponsiveWidgetLayout.StatePropertyNames.Length; i++)
                {
                    Assert.AreEqual(
                        ResponsiveDevice.SlotAllowedByPlayerAxes(
                            ResponsiveWidgetLayout.SlotFormFactor(i),
                            ResponsiveWidgetLayout.SlotOrientation(i),
                            portrait,
                            landscape),
                        ResponsiveUiEditorSettings.SlotAllowed(i),
                        ResponsiveWidgetLayout.StatePropertyNames[i]);
                }
            });
        }

        [TestCase(false, true, true, true, TestName = "LandscapeOnly_SeedsBothSeatedSlots")]
        [TestCase(true, false, true, true, TestName = "PortraitOnly_SeedsBothSeatedSlots")]
        [TestCase(true, true, true, true, TestName = "BothAxes_SeedsBothSeatedSlots")]
        public void Seed_IphoneDuoSeated_SeedsBothSlotsUnderAnyLock(
            bool portrait,
            bool landscape,
            bool expectedLaptop,
            bool expectedBook)
        {
            WithPlayerAxes(portrait, landscape, () =>
            {
                WithWidget((so, widget) =>
                {
                    WriteWidgetLayout(widget, "iPhoneDuoSeatedPortrait", default, enabled: !expectedLaptop);
                    WriteWidgetLayout(widget, "iPhoneDuoSeatedLandscape", default, enabled: !expectedBook);
                    so.ApplyModifiedPropertiesWithoutUndo();

                    ResponsiveRectStateEditorUtil.SeedSlots(widget);
                    so.ApplyModifiedPropertiesWithoutUndo();
                    so.Update();
                    AssertSlotEnabled(
                        widget.FindPropertyRelative("iPhoneDuoSeatedPortrait"), expectedLaptop, "widget laptop");
                    AssertSlotEnabled(
                        widget.FindPropertyRelative("iPhoneDuoSeatedLandscape"), expectedBook, "widget book");
                });

                WithCamera((so, layout) =>
                {
                    ResponsiveCameraEditorUtil.SeedSlots(layout);
                    so.ApplyModifiedPropertiesWithoutUndo();
                    so.Update();
                    AssertSlotEnabled(
                        layout.FindPropertyRelative("iPhoneDuoSeatedPortrait"), expectedLaptop, "camera laptop");
                    AssertSlotEnabled(
                        layout.FindPropertyRelative("iPhoneDuoSeatedLandscape"), expectedBook, "camera book");
                });

                var go = new GameObject("DuoActiveSeedTest");
                try
                {
                    var so = new SerializedObject(go.AddComponent<ResponsiveUiController>());
                    SerializedProperty list = so.FindProperty("activeObjects");
                    list.arraySize = 1;
                    SerializedProperty entry = list.GetArrayElementAtIndex(0);
                    ResponsiveActiveObjectEntryDrawer.SeedAllowed(entry);
                    so.ApplyModifiedPropertiesWithoutUndo();
                    so.Update();
                    SerializedProperty slots = entry.FindPropertyRelative("slots");
                    Assert.AreEqual(
                        expectedLaptop,
                        slots.FindPropertyRelative("iPhoneDuoSeatedPortrait").boolValue,
                        "active laptop");
                    Assert.AreEqual(
                        expectedBook,
                        slots.FindPropertyRelative("iPhoneDuoSeatedLandscape").boolValue,
                        "active book");
                }
                finally
                {
                    Object.DestroyImmediate(go);
                }

                var scroll = new ResponsiveScrollLayout();
                scroll.SeedAll(0.2f, 0.5f, portrait, landscape);
                Assert.AreEqual(expectedLaptop, scroll.iPhoneDuoSeatedPortrait.enabled, "scroll laptop");
                Assert.AreEqual(expectedBook, scroll.iPhoneDuoSeatedLandscape.enabled, "scroll book");
            });
        }

        [Test]
        public void EnableIphoneDuoSeatedLandscape_LandscapeOnly_SeedsFromPadLandscape()
        {
            // Landscape-only game: the user enables the Seated Landscape (book) slot in the
            // Inspector; it starts from the authored Pad Landscape panel (same wide shape).
            WithPlayerAxes(portrait: false, landscape: true, () =>
            {
                WithWidget((so, widget) =>
                {
                    var padWide = new ResponsiveRectState
                    {
                        anchorMin = new Vector2(0.2f, 0.3f),
                        anchorMax = new Vector2(0.4f, 0.5f),
                        anchoredPosition = new Vector2(11f, 22f),
                        sizeDelta = new Vector2(33f, 44f),
                        pivot = new Vector2(0.1f, 0.9f),
                    };
                    WriteWidgetLayout(widget, "phoneLandscape", PhoneSample(landscape: true), enabled: true);
                    WriteWidgetLayout(widget, "padPortrait", default, enabled: false);
                    WriteWidgetLayout(widget, "padLandscape", padWide, enabled: true);
                    WriteWidgetLayout(widget, "iPhoneDuoSeatedLandscape", default, enabled: true);
                    so.ApplyModifiedPropertiesWithoutUndo();

                    Assert.IsTrue(ResponsiveUiEditorSettings.SlotAllowed("iPhoneDuoSeatedLandscape"));
                    SerializedProperty book = widget.FindPropertyRelative("iPhoneDuoSeatedLandscape");
                    Assert.IsTrue(ResponsiveRectStateEditorUtil.CopyFallback(book));
                    AssertLayoutEquals(padWide, ResponsiveRectStateEditorUtil.ReadState(book));
                });
            });
        }

        [Test]
        public void SeedSlots_EnablesAllowedOrientationsExceptFlipCoverLandscape()
        {
            WithWidget((so, widget) =>
            {
                string[] states = ResponsiveWidgetLayout.StatePropertyNames;
                for (int i = 0; i < states.Length; i++)
                {
                    widget.FindPropertyRelative(states[i]).FindPropertyRelative("enabled").boolValue = false;
                    widget.FindPropertyRelative(states[i])
                        .FindPropertyRelative("scaleFromDesignCanvas").boolValue = false;
                }
                so.ApplyModifiedPropertiesWithoutUndo();

                ResponsiveRectStateEditorUtil.SeedSlots(widget);
                so.ApplyModifiedPropertiesWithoutUndo();
                so.Update();

                for (int i = 0; i < states.Length; i++)
                {
                    SerializedProperty slot = widget.FindPropertyRelative(states[i]);
                    AssertSlotEnabled(slot, ExpectedSeedEnabled(i), states[i]);
                    Assert.IsTrue(
                        slot.FindPropertyRelative("scaleFromDesignCanvas").boolValue,
                        states[i] + " Design Scale");
                }
            });
        }

        [Test]
        public void SeedSlots_CopiesPhoneIntoEmptyOptInSlots()
        {
            WithWidget((so, widget) =>
            {
                WriteWidgetLayout(widget, "phonePortrait", PhoneSample(landscape: false), enabled: true);
                WriteWidgetLayout(widget, "phoneLandscape", PhoneSample(landscape: true), enabled: true);
                string[] states = ResponsiveWidgetLayout.StatePropertyNames;
                for (int i = 2; i < states.Length; i++)
                {
                    WriteWidgetLayout(widget, states[i], default, enabled: false);
                }
                so.ApplyModifiedPropertiesWithoutUndo();

                ResponsiveRectStateEditorUtil.SeedSlots(widget);
                so.ApplyModifiedPropertiesWithoutUndo();
                so.Update();

                for (int i = 2; i < states.Length; i++)
                {
                    bool expectedEnabled = ExpectedSeedEnabled(i);
                    SerializedProperty slot = widget.FindPropertyRelative(states[i]);
                    AssertSlotEnabled(slot, expectedEnabled, states[i]);
                    if (!expectedEnabled)
                        continue;

                    bool landscape = ExpectedSeedSourceIsLandscape(i);
                    AssertLayoutEquals(
                        PhoneSample(landscape),
                        ResponsiveRectStateEditorUtil.ReadState(slot),
                        states[i]);
                }
            });
        }

        [Test]
        public void CopyFallback_WideScreenLandscape_CopiesPadNotPhone()
        {
            WithWidget((so, widget) =>
            {
                var padWide = new ResponsiveRectState
                {
                    anchorMin = new Vector2(0.2f, 0.3f),
                    anchorMax = new Vector2(0.4f, 0.5f),
                    anchoredPosition = new Vector2(11f, 22f),
                    sizeDelta = new Vector2(33f, 44f),
                    pivot = new Vector2(0.1f, 0.9f),
                };
                WriteWidgetLayout(widget, "phoneLandscape", PhoneSample(landscape: true), enabled: true);
                WriteWidgetLayout(widget, "padLandscape", padWide, enabled: true);
                WriteWidgetLayout(widget, "wideScreenLandscape", default, enabled: true);
                so.ApplyModifiedPropertiesWithoutUndo();

                SerializedProperty wide = widget.FindPropertyRelative("wideScreenLandscape");
                Assert.IsTrue(ResponsiveRectStateEditorUtil.CopyFallback(wide));
                ResponsiveRectState copied = ResponsiveRectStateEditorUtil.ReadState(wide);
                AssertLayoutEquals(padWide, copied);
                Assert.AreEqual(Vector2.zero, copied.designCanvasSize, "keeps the Wide Screen canvas default");
            });
        }

        [Test]
        public void CopyFallback_WideScreenFromPad_CopiesHubPadScalerRefs()
        {
            WithHubWidget((hub, so, widget) =>
            {
                hub.FindProperty("padReferenceResolution").vector2Value = new Vector2(2048f, 2732f);
                hub.FindProperty("padMatchWidthOrHeight").floatValue = 0.3f;
                hub.ApplyModifiedPropertiesWithoutUndo();
                WriteWidgetLayout(widget, "padLandscape", PhoneSample(landscape: true), enabled: true);
                WriteWidgetLayout(widget, "wideScreenLandscape", default, enabled: true);
                so.ApplyModifiedPropertiesWithoutUndo();

                Assert.IsTrue(ResponsiveRectStateEditorUtil.CopyFallback(
                    widget.FindPropertyRelative("wideScreenLandscape")));

                hub.Update();
                Assert.AreEqual(new Vector2(2048f, 2732f), hub.FindProperty("wideScreenReferenceResolution").vector2Value);
                Assert.AreEqual(0.3f, hub.FindProperty("wideScreenMatchWidthOrHeight").floatValue, 1e-6f);
            });
        }

        [Test]
        public void CopyFallback_WideScreenFromPad_KeepsAuthoredScalerRefs()
        {
            WithHubWidget((hub, so, widget) =>
            {
                hub.FindProperty("wideScreenReferenceResolution").vector2Value = new Vector2(900f, 1600f);
                hub.ApplyModifiedPropertiesWithoutUndo();
                WriteWidgetLayout(widget, "padLandscape", PhoneSample(landscape: true), enabled: true);
                WriteWidgetLayout(widget, "wideScreenLandscape", default, enabled: true);
                so.ApplyModifiedPropertiesWithoutUndo();

                Assert.IsTrue(ResponsiveRectStateEditorUtil.CopyFallback(
                    widget.FindPropertyRelative("wideScreenLandscape")));

                hub.Update();
                Assert.AreEqual(new Vector2(900f, 1600f), hub.FindProperty("wideScreenReferenceResolution").vector2Value);
            });
        }

        [Test]
        public void CopyFallback_WideScreenFromPhone_KeepsDefaultScalerRefs()
        {
            WithHubWidget((hub, so, widget) =>
            {
                WriteWidgetLayout(widget, "phoneLandscape", PhoneSample(landscape: true), enabled: true);
                WriteWidgetLayout(widget, "wideScreenLandscape", default, enabled: true);
                so.ApplyModifiedPropertiesWithoutUndo();

                Assert.IsTrue(ResponsiveRectStateEditorUtil.CopyFallback(
                    widget.FindPropertyRelative("wideScreenLandscape")));

                hub.Update();
                Assert.AreEqual(
                    ResponsiveRectState.BuiltinWideScreenDesignCanvasSize,
                    hub.FindProperty("wideScreenReferenceResolution").vector2Value);
            });
        }

        /// <summary>
        /// A Pad-seeded Wide Screen slot, with the hub refs the seed copies and the Pad design canvas
        /// (Use Pad Design Canvas), lays out like Pad at 16:9: same rect as a fraction of the canvas,
        /// so a 2150-wide centre widget stays inside it. Without Design Scale that match comes from
        /// the copied refs alone; with it, from the shared design canvas.
        /// </summary>
        [TestCase(true, TestName = "WideScreenPadSeed_DesignScale_MatchesPadAt16x9")]
        [TestCase(false, TestName = "WideScreenPadSeed_NoDesignScale_MatchesPadAt16x9")]
        public void WideScreenPadSeed_MatchesPadAt16x9(bool designScale)
        {
            Vector4 pad = default, wide = default;
            WithPadDesignCanvas(() =>
                SeedWideScreenFromPad(designScale, (hub, scaler, widget) =>
                {
                    pad = NormalizedRect(hub, scaler, widget, "padLandscape", ResponsiveDevice.FormFactor.Pad);
                    wide = NormalizedRect(hub, scaler, widget, "wideScreenLandscape", ResponsiveDevice.FormFactor.WideScreen);
                }));

            for (int i = 0; i < 4; i++)
                Assert.AreEqual(pad[i], wide[i], 1e-4f, "rect component " + i);
            Assert.Less(wide.x, 1f, "2150-wide centre widget fits the canvas width");
        }

        /// <summary>
        /// Runs <paramref name="body"/> with the built-in Pad design canvas for Pad and Wide Screen
        /// (what Use Pad Design Canvas sets).
        /// </summary>
        static void WithPadDesignCanvas(System.Action body)
        {
            var previous = ResponsiveRectState.DesignCanvasSizeProvider;
            ResponsiveRectState.DesignCanvasSizeProvider = form =>
                form == ResponsiveDevice.FormFactor.Pad || form == ResponsiveDevice.FormFactor.WideScreen
                    ? ResponsiveRectState.BuiltinPadDesignCanvasSize
                    : Vector2.zero;
            try
            {
                body();
            }
            finally
            {
                ResponsiveRectState.DesignCanvasSizeProvider = previous;
            }
        }

        /// <summary>
        /// Hub with Pad refs 2048×2732 and a centre Pad Landscape widget (2150×750, scale 1.15, like
        /// Main.unity's), then the Wide Screen Landscape seed from Pad.
        /// </summary>
        static void SeedWideScreenFromPad(
            bool designScale,
            System.Action<ResponsiveUI, CanvasScaler, SerializedProperty> body)
        {
            WithHubWidget((hub, so, widget) =>
            {
                hub.FindProperty("applyCanvasScaler").boolValue = true;
                hub.FindProperty("padReferenceResolution").vector2Value = new Vector2(2048f, 2732f);
                hub.ApplyModifiedPropertiesWithoutUndo();
                WriteWidgetLayout(
                    widget,
                    "padLandscape",
                    new ResponsiveRectState
                    {
                        scaleFromDesignCanvas = designScale,
                        designScaleMode = ResponsiveDesignScaleMode.Auto,
                        defaultScale = new Vector2(1.15f, 1.15f),
                        anchorMin = new Vector2(0.5f, 0.5f),
                        anchorMax = new Vector2(0.5f, 0.5f),
                        pivot = new Vector2(0.5f, 0.5f),
                        anchoredPosition = new Vector2(120f, -80f),
                        sizeDelta = new Vector2(2150f, 750f),
                    },
                    enabled: true);
                WriteWidgetLayout(widget, "wideScreenLandscape", default, enabled: true);
                so.ApplyModifiedPropertiesWithoutUndo();
                Assert.IsTrue(ResponsiveRectStateEditorUtil.CopyFallback(
                    widget.FindPropertyRelative("wideScreenLandscape")));
                so.ApplyModifiedPropertiesWithoutUndo();

                body(
                    (ResponsiveUI)hub.targetObject,
                    (CanvasScaler)hub.FindProperty("canvasScaler").objectReferenceValue,
                    widget);
            });
        }

        /// <summary>
        /// Applies the hub's scaler refs for <paramref name="form"/> Landscape and returns the slot's
        /// width, height and anchored position as fractions of the Scale With Screen Size canvas
        /// on a 1920×1080 screen.
        /// </summary>
        static Vector4 NormalizedRect(
            ResponsiveUI hub,
            CanvasScaler scaler,
            SerializedProperty widget,
            string slotName,
            ResponsiveDevice.FormFactor form)
        {
            var screen = new Vector2(1920f, 1080f);
            SetHubOverride(hub, form, ResponsiveDevice.Orientation.Landscape);
            hub.Apply();
            Vector2 reference = scaler.referenceResolution;
            float log = Mathf.Lerp(
                Mathf.Log(screen.x / reference.x, 2f),
                Mathf.Log(screen.y / reference.y, 2f),
                scaler.matchWidthOrHeight);
            Vector2 canvas = screen / Mathf.Pow(2f, log);

            ResponsiveRectState state = ResponsiveRectStateEditorUtil.ReadState(
                widget.FindPropertyRelative(slotName));
            Vector2 ratio = state.ComputeLayoutRatio(canvas, form, ResponsiveDevice.Orientation.Landscape);
            Vector2 scale = state.ComputeAppliedLocalScale(canvas, form, ResponsiveDevice.Orientation.Landscape);
            return new Vector4(
                state.sizeDelta.x * scale.x / canvas.x,
                state.sizeDelta.y * scale.y / canvas.y,
                state.anchoredPosition.x * ratio.x / canvas.x,
                state.anchoredPosition.y * ratio.y / canvas.y);
        }

        [TestCase(ResponsivePlatform.Android, TestName = "SyncWithoutSeed_Android_KeepsAuthoredWideScreen")]
        [TestCase(ResponsivePlatform.Ios, TestName = "SyncWithoutSeed_Ios_KeepsAuthoredWideScreen")]
        public void SyncWithoutSeed_MobileTarget_KeepsAuthoredWideScreen(ResponsivePlatform platform)
        {
            WithPlatform(platform, () =>
            {
                WithWidget((so, widget) =>
                {
                    WriteWidgetLayout(widget, "phonePortrait", PhoneSample(landscape: false), enabled: true);
                    WriteWidgetLayout(widget, "wideScreenLandscape", PhoneSample(landscape: true), enabled: true);
                    WriteWidgetLayout(widget, "wideScreenPortrait", PhoneSample(landscape: false), enabled: false);
                    so.ApplyModifiedPropertiesWithoutUndo();

                    ResponsiveRectStateEditorUtil.SyncSlotEnabled(widget, seedNewWidget: false);
                    so.Update();
                    AssertSlotEnabled(widget.FindPropertyRelative("wideScreenLandscape"), true, "authored Wide Screen stays on");
                    AssertSlotEnabled(widget.FindPropertyRelative("wideScreenPortrait"), false, "disabled Wide Screen stays off");
                    AssertLayoutEquals(
                        PhoneSample(landscape: true),
                        ResponsiveRectStateEditorUtil.ReadState(widget.FindPropertyRelative("wideScreenLandscape")));
                });

                WithCamera((so, layout) =>
                {
                    layout.FindPropertyRelative("wideScreenLandscape").FindPropertyRelative("enabled").boolValue = true;
                    layout.FindPropertyRelative("wideScreenPortrait").FindPropertyRelative("enabled").boolValue = false;
                    so.ApplyModifiedPropertiesWithoutUndo();

                    ResponsiveRectStateEditorUtil.SyncCameras(so);
                    so.Update();
                    AssertSlotEnabled(layout.FindPropertyRelative("wideScreenLandscape"), true, "camera Wide Screen stays on");
                    AssertSlotEnabled(layout.FindPropertyRelative("wideScreenPortrait"), false, "camera Wide Screen stays off");
                });
            });
        }

        [TestCase(ResponsivePlatform.Android, false, TestName = "SeedSlots_Android_WideScreenDisabled")]
        [TestCase(ResponsivePlatform.Ios, false, TestName = "SeedSlots_Ios_WideScreenDisabled")]
        [TestCase(ResponsivePlatform.WideScreen, true, TestName = "SeedSlots_WideScreen_WideScreenEnabled")]
        public void SeedSlots_WideScreenSlots_EnabledOnlyOnWideScreenTargets(
            ResponsivePlatform platform,
            bool expected)
        {
            WithPlatform(platform, () =>
            {
                WithWidget((so, widget) =>
                {
                    ResponsiveRectStateEditorUtil.SeedSlots(widget);
                    so.ApplyModifiedPropertiesWithoutUndo();
                    so.Update();
                    AssertSlotEnabled(widget.FindPropertyRelative("wideScreenPortrait"), expected, "wideScreenPortrait");
                    AssertSlotEnabled(widget.FindPropertyRelative("wideScreenLandscape"), expected, "wideScreenLandscape");
                });
            });
        }

        [Test]
        public void SyncWithoutSeed_LeavesExistingCoverDisabled()
        {
            WithWidget((so, widget) =>
            {
                WriteWidgetLayout(widget, "phonePortrait", PhoneSample(landscape: false), enabled: true);
                widget.FindPropertyRelative("foldPhoneCoverPortrait")
                    .FindPropertyRelative("scaleFromDesignCanvas").boolValue = false;
                widget.FindPropertyRelative("foldPhoneCoverLandscape")
                    .FindPropertyRelative("scaleFromDesignCanvas").boolValue = false;
                string[] states = ResponsiveWidgetLayout.StatePropertyNames;
                for (int i = 0; i < states.Length; i++)
                {
                    bool enable = !ResponsiveWidgetLayout.IsOptInSlot(i) &&
                        ResponsiveUiEditorSettings.SlotAllowed(i);
                    widget.FindPropertyRelative(states[i])
                        .FindPropertyRelative("enabled").boolValue = enable;
                }
                so.ApplyModifiedPropertiesWithoutUndo();

                ResponsiveRectStateEditorUtil.SyncSlotEnabled(
                    widget, seedNewWidget: false);
                so.Update();
                Assert.IsFalse(
                    widget.FindPropertyRelative("foldPhoneCoverPortrait")
                        .FindPropertyRelative("enabled").boolValue);
                Assert.IsFalse(
                    widget.FindPropertyRelative("foldPhoneCoverLandscape")
                        .FindPropertyRelative("enabled").boolValue);
                Assert.IsFalse(
                    widget.FindPropertyRelative("foldPhoneCoverPortrait")
                        .FindPropertyRelative("scaleFromDesignCanvas").boolValue);
                Assert.IsFalse(
                    widget.FindPropertyRelative("foldPhoneCoverLandscape")
                        .FindPropertyRelative("scaleFromDesignCanvas").boolValue);
            });
        }

        [Test]
        public void SyncWithoutSeed_LeavesExistingDuoSeatedSlotsAsAuthored()
        {
            // Scenes carry an authored, enabled Seated Portrait and some disabled Seated Landscape
            // slots. Inspecting a widget must not flip either.
            WithWidget((so, widget) =>
            {
                WriteWidgetLayout(widget, "phonePortrait", PhoneSample(landscape: false), enabled: true);
                widget.FindPropertyRelative("iPhoneDuoSeatedPortrait")
                    .FindPropertyRelative("enabled").boolValue = true;
                widget.FindPropertyRelative("iPhoneDuoSeatedLandscape")
                    .FindPropertyRelative("enabled").boolValue = false;
                so.ApplyModifiedPropertiesWithoutUndo();

                ResponsiveRectStateEditorUtil.SyncSlotEnabled(
                    widget, seedNewWidget: false);
                so.Update();
                Assert.IsTrue(
                    widget.FindPropertyRelative("iPhoneDuoSeatedPortrait")
                        .FindPropertyRelative("enabled").boolValue,
                    "authored Seated Portrait stays enabled");
                Assert.IsFalse(
                    widget.FindPropertyRelative("iPhoneDuoSeatedLandscape")
                        .FindPropertyRelative("enabled").boolValue,
                    "disabled Seated Landscape stays disabled (falls back to Pad Landscape)");
            });
        }

        [Test]
        public void WriteCaptureToSlots_FirstAssign_LeavesFlipCoverLandscapeDisabled()
        {
            var root = new GameObject("CaptureRoot", typeof(RectTransform));
            try
            {
                RectTransform rt = root.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.1f, 0.2f);
                rt.anchorMax = new Vector2(0.3f, 0.4f);
                rt.anchoredPosition = new Vector2(50f, 60f);
                rt.sizeDelta = new Vector2(70f, 80f);
                rt.pivot = new Vector2(0.25f, 0.75f);

                WithWidget((so, widget) =>
                {
                    var controller = so.targetObject as ResponsiveUiController;
                    ResponsiveRectStateEditorUtil.WriteCaptureToSlots(
                        widget, rt, controller != null ? controller.Hub : null, applyEnabledDefaults: true);
                    so.ApplyModifiedPropertiesWithoutUndo();
                    so.Update();

                    string[] states = ResponsiveWidgetLayout.StatePropertyNames;
                    for (int i = 0; i < states.Length; i++)
                    {
                        bool expectedEnabled = ExpectedSeedEnabled(i);
                        SerializedProperty slot = widget.FindPropertyRelative(states[i]);
                        AssertSlotEnabled(slot, expectedEnabled, states[i]);
                        if (!expectedEnabled)
                            continue;

                        ResponsiveRectState captured = ResponsiveRectStateEditorUtil.ReadState(slot);
                        Assert.AreEqual(rt.anchorMin, captured.anchorMin);
                        Assert.AreEqual(rt.sizeDelta, captured.sizeDelta);
                        Assert.AreEqual(rt.pivot, captured.pivot);
                    }
                });
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ResetSlots_ClearsAuthoredLayoutAndReseedsEnabled()
        {
            WithWidget((so, widget) =>
            {
                string[] states = ResponsiveWidgetLayout.StatePropertyNames;
                for (int i = 0; i < states.Length; i++)
                {
                    bool landscape = ResponsiveWidgetLayout.SlotOrientation(i) ==
                        ResponsiveDevice.Orientation.Landscape;
                    ResponsiveRectState dirty = PhoneSample(landscape);
                    dirty.designScaleMode = ResponsiveDesignScaleMode.IndependentXY;
                    dirty.designCanvasSize = new Vector2(100f, 200f);
                    dirty.defaultScale = new Vector2(2f, 3f);
                    WriteWidgetLayout(widget, states[i], dirty, enabled: true);
                }
                so.ApplyModifiedPropertiesWithoutUndo();

                ResponsiveRectStateEditorUtil.ResetSlots(widget);
                so.ApplyModifiedPropertiesWithoutUndo();
                so.Update();

                var expected = ResponsiveRectState.CreateDefault();
                expected.scaleFromDesignCanvas = true;
                for (int i = 0; i < states.Length; i++)
                {
                    SerializedProperty slot = widget.FindPropertyRelative(states[i]);
                    bool expectedEnabled = ExpectedSeedEnabled(i);
                    AssertSlotEnabled(slot, expectedEnabled, states[i]);
                    ResponsiveRectState actual = ResponsiveRectStateEditorUtil.ReadState(slot);
                    AssertLayoutEquals(expected, actual, states[i]);
                    Assert.AreEqual(
                        ResponsiveDesignScaleMode.Auto,
                        actual.designScaleMode,
                        states[i]);
                    Assert.AreEqual(Vector2.zero, actual.designCanvasSize, states[i]);
                    Assert.AreEqual(Vector2.one, actual.defaultScale, states[i]);
                }
            });
        }

        [Test]
        public void ResetNewListRow_ClearsDuplicatedWidgetTargetAndLayout()
        {
            WithWidget((so, widget) =>
            {
                var go = new GameObject("DupWidgetTarget", typeof(RectTransform));
                try
                {
                    widget.FindPropertyRelative("target").objectReferenceValue =
                        go.GetComponent<RectTransform>();
                    WriteWidgetLayout(
                        widget, "phoneLandscape", PhoneSample(landscape: true), enabled: true);
                    so.ApplyModifiedPropertiesWithoutUndo();

                    ResponsiveRectStateEditorUtil.ResetNewListRow(widget);
                    so.ApplyModifiedPropertiesWithoutUndo();
                    so.Update();

                    Assert.IsNull(widget.FindPropertyRelative("target").objectReferenceValue);
                    var expected = ResponsiveRectState.CreateDefault();
                    expected.scaleFromDesignCanvas = true;
                    string[] states = ResponsiveWidgetLayout.StatePropertyNames;
                    for (int i = 0; i < states.Length; i++)
                    {
                        SerializedProperty slot = widget.FindPropertyRelative(states[i]);
                        bool expectedEnabled = ExpectedSeedEnabled(i);
                        AssertSlotEnabled(slot, expectedEnabled, states[i]);
                        AssertLayoutEquals(
                            expected,
                            ResponsiveRectStateEditorUtil.ReadState(slot),
                            states[i]);
                    }
                }
                finally
                {
                    Object.DestroyImmediate(go);
                }
            });
        }

        [Test]
        public void ApplyInspectorTargetChange_ClearToNone_ResetsSlots()
        {
            WithWidget((so, widget) =>
            {
                var go = new GameObject("WidgetTargetClear", typeof(RectTransform));
                try
                {
                    SerializedProperty target = widget.FindPropertyRelative("target");
                    target.objectReferenceValue = go.GetComponent<RectTransform>();
                    so.ApplyModifiedPropertiesWithoutUndo();

                    string[] states = ResponsiveWidgetLayout.StatePropertyNames;
                    for (int i = 0; i < states.Length; i++)
                    {
                        ResponsiveRectState dirty = PhoneSample(landscape: false);
                        dirty.designScaleMode = ResponsiveDesignScaleMode.Height;
                        dirty.designCanvasSize = new Vector2(50f, 60f);
                        WriteWidgetLayout(widget, states[i], dirty, enabled: true);
                    }
                    so.ApplyModifiedPropertiesWithoutUndo();

                    Object previous = target.objectReferenceValue;
                    int previousId = target.objectReferenceInstanceIDValue;
                    target.objectReferenceValue = null;
                    ResponsiveRectStateEditorUtil.ApplyInspectorTargetChange(
                        widget, previous, previousId, ui: null);
                    so.ApplyModifiedPropertiesWithoutUndo();
                    so.Update();

                    Assert.IsNull(widget.FindPropertyRelative("target").objectReferenceValue);
                    var expected = ResponsiveRectState.CreateDefault();
                    expected.scaleFromDesignCanvas = true;
                    for (int i = 0; i < states.Length; i++)
                    {
                        ResponsiveRectState actual = ResponsiveRectStateEditorUtil.ReadState(
                            widget.FindPropertyRelative(states[i]));
                        AssertLayoutEquals(expected, actual, states[i]);
                    }
                }
                finally
                {
                    Object.DestroyImmediate(go);
                }
            });
        }

        [Test]
        public void ApplyInspectorTargetChange_ClearMissing_ResetsSlots()
        {
            WithWidget((so, widget) =>
            {
                var go = new GameObject("WidgetTargetMissing", typeof(RectTransform));
                SerializedProperty target = widget.FindPropertyRelative("target");
                target.objectReferenceValue = go.GetComponent<RectTransform>();
                so.ApplyModifiedPropertiesWithoutUndo();

                string[] states = ResponsiveWidgetLayout.StatePropertyNames;
                WriteWidgetLayout(
                    widget,
                    states[0],
                    PhoneSample(landscape: false),
                    enabled: true);
                so.ApplyModifiedPropertiesWithoutUndo();

                int previousId = target.objectReferenceInstanceIDValue;
                Object.DestroyImmediate(go);
                so.Update();

                Assert.IsTrue(ResponsiveUiObjectReference.IsFirstAssign(target.objectReferenceValue));
                Assert.IsTrue(
                    ResponsiveUiObjectReference.WasClearedToNone(previousId, nextValue: null));

                target.objectReferenceValue = null;
                ResponsiveRectStateEditorUtil.ApplyInspectorTargetChange(
                    widget,
                    previousTarget: null,
                    previousId,
                    ui: null);
                so.ApplyModifiedPropertiesWithoutUndo();
                so.Update();

                ResponsiveRectState actual = ResponsiveRectStateEditorUtil.ReadState(
                    widget.FindPropertyRelative(states[0]));
                Assert.AreEqual(ResponsiveDesignScaleMode.Auto, actual.designScaleMode);
                Assert.AreEqual(Vector2.zero, actual.designCanvasSize);
            });
        }

    #endregion

    #region List

        [Test]
        public void BulkCopyResult_LogsSourceDestinationCountAndContext()
        {
            var context = new GameObject("BulkCopyLogTest");
            try
            {
                LogAssert.Expect(
                    LogType.Log,
                    "[ResponsiveUI] Bulk Copy Widget: Pad Portrait → Fold Half Opened Portrait; " +
                    "copied 3 widgets on 'BulkCopyLogTest'.");
                ResponsiveUiSlotClipboard.LogBulkCopyResult(
                    ResponsiveUiSlotClipboard.Kind.Widget,
                    context,
                    sourceSlotIndex: ResponsiveWidgetLayout.StatePropertyIndex("padPortrait"),
                    destinationSlotIndex: ResponsiveWidgetLayout.StatePropertyIndex("foldHalfOpenedPortrait"),
                    copiedCount: 3);
            }
            finally
            {
                Object.DestroyImmediate(context);
            }
        }

        [Test]
        public void ResetNewListRow_ActiveObject_ClearsDuplicatedTarget()
        {
            var go = new GameObject("ActiveSeedTest");
            var listed = new GameObject("Listed");
            try
            {
                var controller = go.AddComponent<ResponsiveUiController>();
                var so = new SerializedObject(controller);
                SerializedProperty list = so.FindProperty("activeObjects");
                list.arraySize = 1;
                SerializedProperty entry = list.GetArrayElementAtIndex(0);
                entry.FindPropertyRelative("target").objectReferenceValue = listed;
                SerializedProperty slots = entry.FindPropertyRelative("slots");
                string[] names = ResponsiveWidgetLayout.StatePropertyNames;
                for (int i = 0; i < names.Length; i++)
                    slots.FindPropertyRelative(names[i]).boolValue = false;
                so.ApplyModifiedPropertiesWithoutUndo();

                ResponsiveActiveObjectEntryDrawer.ResetNewListRow(entry);
                so.ApplyModifiedPropertiesWithoutUndo();
                so.Update();

                Assert.IsNull(entry.FindPropertyRelative("target").objectReferenceValue);
                for (int i = 0; i < names.Length; i++)
                {
                    Assert.AreEqual(
                        ResponsiveUiEditorSettings.SlotAllowed(i),
                        slots.FindPropertyRelative(names[i]).boolValue,
                        names[i]);
                }
            }
            finally
            {
                Object.DestroyImmediate(listed);
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void ResetNewListRow_Background_ClearsDuplicatedImage()
        {
            var go = new GameObject("BgSeed", typeof(RectTransform), typeof(Image));
            try
            {
                var controller = go.AddComponent<ResponsiveUiController>();
                var so = new SerializedObject(controller);
                SerializedProperty list = so.FindProperty("backgrounds");
                list.arraySize = 1;
                SerializedProperty row = list.GetArrayElementAtIndex(0);
                row.FindPropertyRelative("image").objectReferenceValue = go.GetComponent<Image>();
                row.FindPropertyRelative("coverMode").enumValueIndex =
                    (int)ResponsiveBgCoverMode.Stretch;
                so.ApplyModifiedPropertiesWithoutUndo();

                ResponsiveBackgroundDrawer.ResetNewListRow(row);
                so.ApplyModifiedPropertiesWithoutUndo();
                so.Update();

                Assert.IsNull(row.FindPropertyRelative("image").objectReferenceValue);
                Assert.AreEqual(
                    (int)ResponsiveBgCoverMode.Cover,
                    row.FindPropertyRelative("coverMode").enumValueIndex);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void GetNewRows_FirstCall_CapturesSizeWithoutReset()
        {
            WithWidgetList(2, widgets =>
            {
                int last = -1;
                Assert.IsFalse(ResponsiveRectStateEditorUtil.GetNewRows(
                    widgets, ref last, out int start, out int end, canMutate: true));
                Assert.AreEqual(2, last);
                Assert.AreEqual(0, start);
                Assert.AreEqual(0, end);
            });
        }

        [Test]
        public void GetNewRows_MouseDownGrowth_IsDeferredUntilLayout()
        {
            WithWidgetList(1, widgets =>
            {
                int last = 1;
                widgets.arraySize = 2;

                Assert.IsFalse(ResponsiveRectStateEditorUtil.GetNewRows(
                    widgets, ref last, out _, out _, canMutate: false));
                Assert.AreEqual(1, last);

                Assert.IsTrue(ResponsiveRectStateEditorUtil.GetNewRows(
                    widgets, ref last, out int start, out int end, canMutate: true));
                Assert.AreEqual(1, start);
                Assert.AreEqual(2, end);
                Assert.AreEqual(2, last);
            });
        }

        [Test]
        public void GetNewRows_EditorRebuild_StillFindsUnityDuplicate()
        {
            WithWidgetList(1, widgets =>
            {
                int last = -1;
                Assert.IsFalse(ResponsiveRectStateEditorUtil.GetNewRows(
                    widgets, ref last, out _, out _, canMutate: true));
                Assert.AreEqual(1, last);

                widgets.arraySize = 2;
                last = -1;

                Assert.IsTrue(ResponsiveRectStateEditorUtil.GetNewRows(
                    widgets, ref last, out int start, out int end, canMutate: true));
                Assert.AreEqual(1, start);
                Assert.AreEqual(2, end);
                Assert.AreEqual(2, last);
            });
        }

        [Test]
        public void ForEachNewRow_InspectorRebuild_ClearsDuplicatedTarget()
        {
            WithWidgetList(1, widgets =>
            {
                var go = new GameObject("RebuildWidgetTarget", typeof(RectTransform));
                try
                {
                    RectTransform rt = go.GetComponent<RectTransform>();
                    widgets.GetArrayElementAtIndex(0)
                        .FindPropertyRelative("target").objectReferenceValue = rt;
                    widgets.serializedObject.ApplyModifiedPropertiesWithoutUndo();

                    int last = -1;
                    Assert.IsFalse(ResponsiveRectStateEditorUtil.ForEachNewRow(
                        widgets,
                        ref last,
                        ResponsiveRectStateEditorUtil.ResetNewListRow,
                        canMutate: true));

                    widgets.arraySize = 2;
                    widgets.serializedObject.ApplyModifiedPropertiesWithoutUndo();
                    widgets.serializedObject.Update();
                    last = -1;

                    Assert.IsTrue(ResponsiveRectStateEditorUtil.ForEachNewRow(
                        widgets,
                        ref last,
                        ResponsiveRectStateEditorUtil.ResetNewListRow,
                        canMutate: true));
                    widgets.serializedObject.ApplyModifiedPropertiesWithoutUndo();
                    widgets.serializedObject.Update();

                    Assert.AreEqual(
                        rt,
                        widgets.GetArrayElementAtIndex(0)
                            .FindPropertyRelative("target").objectReferenceValue);
                    Assert.IsNull(
                        widgets.GetArrayElementAtIndex(1)
                            .FindPropertyRelative("target").objectReferenceValue);
                }
                finally
                {
                    Object.DestroyImmediate(go);
                }
            });
        }

        [Test]
        public void GetNewRows_ResetNewListRow_ClearsUnityDuplicateKeepsPrevious()
        {
            WithWidgetList(1, widgets =>
            {
                var go = new GameObject("KeepWidgetTarget", typeof(RectTransform));
                try
                {
                    SerializedProperty first = widgets.GetArrayElementAtIndex(0);
                    RectTransform rt = go.GetComponent<RectTransform>();
                    first.FindPropertyRelative("target").objectReferenceValue = rt;
                    WriteWidgetLayout(
                        first, "phoneLandscape", PhoneSample(landscape: true), enabled: true);
                    widgets.serializedObject.ApplyModifiedPropertiesWithoutUndo();

                    int last = 1;
                    widgets.arraySize = 2;
                    widgets.serializedObject.ApplyModifiedPropertiesWithoutUndo();
                    widgets.serializedObject.Update();

                    Assert.IsTrue(ResponsiveRectStateEditorUtil.GetNewRows(
                        widgets, ref last, out int start, out int end, canMutate: true));
                    for (int i = start; i < end; i++)
                        ResponsiveRectStateEditorUtil.ResetNewListRow(
                            widgets.GetArrayElementAtIndex(i));
                    widgets.serializedObject.ApplyModifiedPropertiesWithoutUndo();
                    widgets.serializedObject.Update();

                    first = widgets.GetArrayElementAtIndex(0);
                    SerializedProperty added = widgets.GetArrayElementAtIndex(1);
                    Assert.AreEqual(rt, first.FindPropertyRelative("target").objectReferenceValue);
                    AssertLayoutEquals(
                        PhoneSample(landscape: true),
                        ResponsiveRectStateEditorUtil.ReadState(
                            first.FindPropertyRelative("phoneLandscape")));
                    Assert.IsNull(added.FindPropertyRelative("target").objectReferenceValue);
                    var expected = ResponsiveRectState.CreateDefault();
                    expected.scaleFromDesignCanvas = true;
                    AssertLayoutEquals(
                        expected,
                        ResponsiveRectStateEditorUtil.ReadState(
                            added.FindPropertyRelative("phoneLandscape")));
                }
                finally
                {
                    Object.DestroyImmediate(go);
                }
            });
        }

        [Test]
        public void GetNewRows_Shrink_UpdatesCountOnMutate()
        {
            WithWidgetList(3, widgets =>
            {
                int last = 3;
                widgets.arraySize = 1;
                Assert.IsFalse(ResponsiveRectStateEditorUtil.GetNewRows(
                    widgets, ref last, out _, out _, canMutate: true));
                Assert.AreEqual(1, last);
            });
        }

    #endregion

    #region Camera / scroll

        [Test]
        public void CopyFallback_Pad_CopiesPhoneTransform()
        {
            WithCamera((so, layout) =>
            {
                SerializedProperty phone = layout.FindPropertyRelative("phoneLandscape");
                phone.FindPropertyRelative("enabled").boolValue = true;
                phone.FindPropertyRelative("applyLocalPosition").boolValue = true;
                phone.FindPropertyRelative("applyLocalRotation").boolValue = false;
                phone.FindPropertyRelative("localPosition").vector3Value = new Vector3(1f, 2f, 3f);
                phone.FindPropertyRelative("orthographicSize").floatValue = 9f;
                phone.FindPropertyRelative("scaleProjection").boolValue = true;
                phone.FindPropertyRelative("viewport").rectValue = new Rect(0.1f, 0.2f, 0.3f, 0.4f);

                SerializedProperty pad = layout.FindPropertyRelative("padLandscape");
                pad.FindPropertyRelative("enabled").boolValue = true;
                so.ApplyModifiedPropertiesWithoutUndo();

                Assert.IsTrue(ResponsiveCameraEditorUtil.CopyFallback(pad));
                ResponsiveCameraState copied = ResponsiveCameraEditorUtil.ReadState(pad);
                Assert.IsTrue(copied.enabled);
                Assert.IsTrue(copied.applyLocalPosition);
                Assert.IsFalse(copied.applyLocalRotation);
                Assert.AreEqual(new Vector3(1f, 2f, 3f), copied.localPosition);
                Assert.AreEqual(9f, copied.orthographicSize);
                Assert.IsTrue(copied.scaleProjection);
                Assert.AreEqual(new Rect(0.1f, 0.2f, 0.3f, 0.4f), copied.viewport);
            });
        }

        [Test]
        public void CopyFallback_CopiesFocusFields()
        {
            WithCamera((so, layout) =>
            {
                var table = new GameObject("FocusTable");
                var boundsGo = new GameObject("Play", typeof(RectTransform));
                try
                {
                    RectTransform playRt = boundsGo.GetComponent<RectTransform>();
                    SerializedProperty phone = layout.FindPropertyRelative("phoneLandscape");
                    phone.FindPropertyRelative("enabled").boolValue = true;
                    phone.FindPropertyRelative("focusTarget").objectReferenceValue = table.transform;
                    phone.FindPropertyRelative("focusScreen").intValue =
                        (int)ResponsiveCameraFocusScreen.Auto;
                    phone.FindPropertyRelative("focusFit").enumValueIndex =
                        (int)ResponsiveCameraFocusFit.Cover;
                    phone.FindPropertyRelative("focusPadding").floatValue = 1.5f;
                    phone.FindPropertyRelative("focusBounds").objectReferenceValue = playRt;
                    SerializedProperty phoneBounds =
                        phone.FindPropertyRelative("focusBoundsLayout");
                    phoneBounds.FindPropertyRelative("enabled").boolValue = true;
                    phoneBounds.FindPropertyRelative("anchoredPosition").vector2Value =
                        new Vector2(12f, -40f);
                    phoneBounds.FindPropertyRelative("sizeDelta").vector2Value =
                        new Vector2(0f, -80f);

                    // Fold Half Opened Portrait is the wide screen: it falls back to Phone Landscape.
                    SerializedProperty fold = layout.FindPropertyRelative("foldHalfOpenedPortrait");
                    fold.FindPropertyRelative("enabled").boolValue = true;
                    so.ApplyModifiedPropertiesWithoutUndo();

                    Assert.IsTrue(ResponsiveCameraEditorUtil.CopyFallback(fold));
                    ResponsiveCameraState copied = ResponsiveCameraEditorUtil.ReadState(fold);
                    Assert.AreSame(table.transform, copied.focusTarget);
                    Assert.AreEqual(ResponsiveCameraFocusScreen.Auto, copied.focusScreen);
                    Assert.AreEqual(ResponsiveCameraFocusFit.Cover, copied.focusFit);
                    Assert.AreEqual(1.5f, copied.focusPadding, 1e-4f);
                    Assert.AreSame(playRt, copied.focusBounds);
                    Assert.IsTrue(copied.focusBoundsLayout.enabled);
                    Assert.AreEqual(new Vector2(12f, -40f), copied.focusBoundsLayout.anchoredPosition);
                    Assert.AreEqual(new Vector2(0f, -80f), copied.focusBoundsLayout.sizeDelta);
                }
                finally
                {
                    Object.DestroyImmediate(boundsGo);
                    Object.DestroyImmediate(table);
                }
            });
        }

        [Test]
        public void WriteCapturedLive_SkipsWhenHudAlreadyMatches()
        {
            WithCamera((so, layout) =>
            {
                var hudGo = new GameObject("Play", typeof(RectTransform));
                try
                {
                    RectTransform hud = hudGo.GetComponent<RectTransform>();
                    hud.anchorMin = new Vector2(0.5f, 0.5f);
                    hud.anchorMax = new Vector2(0.5f, 0.5f);
                    hud.pivot = new Vector2(0.5f, 0.5f);
                    hud.anchoredPosition = new Vector2(12f, -8f);
                    hud.sizeDelta = new Vector2(200f, 90f);
                    hud.localScale = Vector3.one;

                    SerializedProperty phone = layout.FindPropertyRelative("phoneLandscape");
                    phone.FindPropertyRelative("enabled").boolValue = true;
                    phone.FindPropertyRelative("focusBounds").objectReferenceValue = hud;
                    SerializedProperty boundsLayout = phone.FindPropertyRelative("focusBoundsLayout");
                    ResponsiveRectStateEditorUtil.WriteState(
                        boundsLayout,
                        new ResponsiveRectState
                        {
                            enabled = true,
                            anchorMin = hud.anchorMin,
                            anchorMax = hud.anchorMax,
                            pivot = hud.pivot,
                            anchoredPosition = hud.anchoredPosition,
                            sizeDelta = hud.sizeDelta,
                            defaultScale = Vector2.one,
                        });
                    so.ApplyModifiedPropertiesWithoutUndo();
                    so.Update();

                    int slotIndex = ResponsiveWidgetLayout.StatePropertyIndex(phone.name);
                    Assert.IsFalse(
                        ResponsiveRectStateEditorUtil.WriteCapturedLive(
                            boundsLayout, hud, null, slotIndex));

                    hud.anchoredPosition = new Vector2(30f, -8f);
                    Assert.IsTrue(
                        ResponsiveRectStateEditorUtil.WriteCapturedLive(
                            boundsLayout, hud, null, slotIndex));
                    Assert.AreEqual(
                        new Vector2(30f, -8f),
                        ResponsiveRectStateEditorUtil.ReadState(boundsLayout).anchoredPosition);
                    Assert.AreEqual(
                        Vector2.one,
                        ResponsiveRectStateEditorUtil.ReadState(boundsLayout).defaultScale);
                }
                finally
                {
                    Object.DestroyImmediate(hudGo);
                }
            });
        }

        [Test]
        public void CaptureFocusBoundsLayoutInto_ScaleToggle_ForcesRecapture()
        {
            WithCamera((so, layout) =>
            {
                var hudGo = new GameObject("Play", typeof(RectTransform));
                try
                {
                    RectTransform hud = hudGo.GetComponent<RectTransform>();
                    hud.anchorMin = new Vector2(0.5f, 0.5f);
                    hud.anchorMax = new Vector2(0.5f, 0.5f);
                    hud.localScale = new Vector3(1.5f, 0.8f, 1f);

                    SerializedProperty phone = layout.FindPropertyRelative("phoneLandscape");
                    phone.FindPropertyRelative("enabled").boolValue = true;
                    phone.FindPropertyRelative("focusBounds").objectReferenceValue = hud;
                    SerializedProperty boundsLayout = phone.FindPropertyRelative("focusBoundsLayout");
                    ResponsiveRectState matching = ResponsiveRectState.From(hud);
                    matching.enabled = true;
                    ResponsiveRectStateEditorUtil.WriteState(
                        boundsLayout,
                        matching);
                    so.ApplyModifiedPropertiesWithoutUndo();
                    so.Update();

                    Assert.IsTrue(
                        ResponsiveCameraEditorUtil.CaptureFocusBoundsLayoutInto(
                            phone, skipIfMatches: false));
                    so.ApplyModifiedPropertiesWithoutUndo();
                    so.Update();
                    Assert.IsFalse(
                        ResponsiveCameraEditorUtil.CaptureFocusBoundsLayoutInto(phone));
                    Assert.IsTrue(
                        ResponsiveCameraEditorUtil.CaptureFocusBoundsLayoutInto(
                            phone, skipIfMatches: false));
                    Assert.AreEqual(
                        new Vector2(1.5f, 0.8f),
                        ResponsiveRectStateEditorUtil.ReadState(boundsLayout).defaultScale);
                }
                finally
                {
                    Object.DestroyImmediate(hudGo);
                }
            });
        }

        [Test]
        public void WriteCapturedState_DisabledSlot_StaysDisabled()
        {
            WithCamera((so, layout) =>
            {
                SerializedProperty pad = layout.FindPropertyRelative("padLandscape");
                pad.FindPropertyRelative("enabled").boolValue = false;
                so.ApplyModifiedPropertiesWithoutUndo();

                var live = new ResponsiveCameraState
                {
                    viewport = ResponsiveCameraState.FullViewport,
                    localPosition = new Vector3(1f, 2f, 3f),
                    localEulerAngles = new Vector3(90f, 270f, 0f),
                    orthographicSize = 0.87f,
                };
                ResponsiveCameraEditorUtil.WriteCapturedState(
                    pad,
                    live,
                    enabled: false,
                    Vector2.zero,
                    ResponsiveDevice.FormFactor.Pad);
                so.ApplyModifiedPropertiesWithoutUndo();

                Assert.IsFalse(ResponsiveCameraEditorUtil.ReadState(pad).enabled);
            });
        }

        [Test]
        public void WriteCapturedState_ScaleProjectionAlone_UnscalesOrthoByAspectNotPosition()
        {
            WithCamera((so, layout) =>
            {
                SerializedProperty phone = layout.FindPropertyRelative("phoneLandscape");
                phone.FindPropertyRelative("enabled").boolValue = true;
                phone.FindPropertyRelative("scaleFromUiSize").boolValue = false;
                phone.FindPropertyRelative("scaleProjection").boolValue = true;
                phone.FindPropertyRelative("uiScaleMode").enumValueIndex =
                    (int)ResponsiveDesignScaleMode.Width;
                so.ApplyModifiedPropertiesWithoutUndo();

                Vector2 design = ResponsiveUI.OrientSize(
                    ResponsiveRectState.GetDefaultDesignCanvasSize(
                        ResponsiveDevice.FormFactor.Phone),
                    ResponsiveDevice.Orientation.Landscape);
                Vector2 liveUi = ResponsiveCatalogDevices.FoldCoverLandscape;
                float liveOrtho = 0.87f * (design.x / design.y) / (liveUi.x / liveUi.y);
                var live = new ResponsiveCameraState
                {
                    viewport = ResponsiveCameraState.FullViewport,
                    localPosition = new Vector3(20f, 40f, 60f),
                    orthographic = true,
                    orthographicSize = liveOrtho,
                    applyOrthographicSize = true,
                };
                ResponsiveCameraEditorUtil.WriteCapturedState(
                    phone,
                    live,
                    enabled: true,
                    liveUi,
                    ResponsiveDevice.FormFactor.Phone);
                so.ApplyModifiedPropertiesWithoutUndo();

                ResponsiveCameraState written = ResponsiveCameraEditorUtil.ReadState(phone);
                Assert.AreEqual(new Vector3(20f, 40f, 60f), written.localPosition);
                Assert.AreEqual(0.87f, written.orthographicSize, 1e-3f);
                Assert.IsFalse(written.scaleFromUiSize);
                Assert.IsTrue(written.scaleProjection);
                Assert.IsTrue(written.orthographic);
            });
        }

        [Test]
        public void SameAuthoring_DetectsOrthoAndPositionEdits()
        {
            var a = ResponsiveCameraState.CreateDefault();
            var b = a;
            Assert.IsTrue(ResponsiveCameraEditorUtil.SameAuthoring(a, b));
            b.orthographicSize = a.orthographicSize + 1f;
            Assert.IsFalse(ResponsiveCameraEditorUtil.SameAuthoring(a, b));
            b = a;
            b.localPosition = new Vector3(1f, 2f, 3f);
            Assert.IsFalse(ResponsiveCameraEditorUtil.SameAuthoring(a, b));
            b = a;
            b.focusScreen = ResponsiveCameraFocusScreen.Auto;
            Assert.IsFalse(ResponsiveCameraEditorUtil.SameAuthoring(a, b));
            b = a;
            var boundsGo = new GameObject("FocusBounds", typeof(RectTransform));
            try
            {
                b.focusBounds = boundsGo.GetComponent<RectTransform>();
                Assert.IsFalse(ResponsiveCameraEditorUtil.SameAuthoring(a, b));
                b = a;
                var boundsLayout = a.focusBoundsLayout;
                boundsLayout.enabled = true;
                boundsLayout.anchoredPosition = new Vector2(8f, -16f);
                b.focusBoundsLayout = boundsLayout;
                Assert.IsFalse(ResponsiveCameraEditorUtil.SameAuthoring(a, b));
            }
            finally
            {
                Object.DestroyImmediate(boundsGo);
            }
        }

        [Test]
        public void WriteCapturedState_DoesNotForceApplyWhenNotSeeding()
        {
            WithCamera((so, layout) =>
            {
                SerializedProperty phone = layout.FindPropertyRelative("phoneLandscape");
                phone.FindPropertyRelative("enabled").boolValue = true;
                phone.FindPropertyRelative("applyLocalPosition").boolValue = false;
                phone.FindPropertyRelative("localPosition").vector3Value = new Vector3(1f, 2f, 3f);
                phone.FindPropertyRelative("applyLocalRotation").boolValue = false;
                phone.FindPropertyRelative("localEulerAngles").vector3Value = new Vector3(10f, 20f, 30f);
                so.ApplyModifiedPropertiesWithoutUndo();

                var live = new ResponsiveCameraState
                {
                    viewport = ResponsiveCameraState.FullViewport,
                    localPosition = new Vector3(9f, 8f, 7f),
                    localEulerAngles = new Vector3(90f, 0f, 0f),
                };
                ResponsiveCameraEditorUtil.WriteCapturedState(
                    phone,
                    live,
                    enabled: true,
                    Vector2.zero,
                    ResponsiveDevice.FormFactor.Phone,
                    seedTransform: false);
                so.ApplyModifiedPropertiesWithoutUndo();

                ResponsiveCameraState written = ResponsiveCameraEditorUtil.ReadState(phone);
                Assert.IsFalse(written.applyLocalPosition);
                Assert.AreEqual(new Vector3(1f, 2f, 3f), written.localPosition);
                Assert.IsFalse(written.applyLocalRotation);
                Assert.AreEqual(new Vector3(10f, 20f, 30f), written.localEulerAngles);
            });
        }

        [Test]
        public void WriteCapturedState_DoesNotForceProjectionApplyWhenNotSeeding()
        {
            WithCamera((so, layout) =>
            {
                SerializedProperty phone = layout.FindPropertyRelative("phoneLandscape");
                phone.FindPropertyRelative("enabled").boolValue = true;
                phone.FindPropertyRelative("orthographic").boolValue = true;
                phone.FindPropertyRelative("applyOrthographicSize").boolValue = false;
                phone.FindPropertyRelative("orthographicSize").floatValue = 2f;
                phone.FindPropertyRelative("applyFieldOfView").boolValue = false;
                phone.FindPropertyRelative("fieldOfView").floatValue = 50f;
                so.ApplyModifiedPropertiesWithoutUndo();

                var live = new ResponsiveCameraState
                {
                    viewport = ResponsiveCameraState.FullViewport,
                    orthographic = true,
                    applyOrthographicSize = true,
                    orthographicSize = 9f,
                    applyFieldOfView = true,
                    fieldOfView = 12f,
                };
                ResponsiveCameraEditorUtil.WriteCapturedState(
                    phone,
                    live,
                    enabled: true,
                    Vector2.zero,
                    ResponsiveDevice.FormFactor.Phone,
                    seedTransform: false);
                so.ApplyModifiedPropertiesWithoutUndo();

                ResponsiveCameraState written = ResponsiveCameraEditorUtil.ReadState(phone);
                Assert.IsFalse(written.applyOrthographicSize);
                Assert.AreEqual(2f, written.orthographicSize, 1e-3f);
                Assert.IsFalse(written.applyFieldOfView);
                Assert.AreEqual(50f, written.fieldOfView, 1e-3f);
            });
        }

        [Test]
        public void WriteCapturedState_SceneCapture_PreservesFlagsAndUpdatesLiveValues()
        {
            WithCamera((so, layout) =>
            {
                SerializedProperty phone = layout.FindPropertyRelative("phoneLandscape");
                phone.FindPropertyRelative("enabled").boolValue = true;
                phone.FindPropertyRelative("applyLocalPosition").boolValue = false;
                phone.FindPropertyRelative("applyLocalRotation").boolValue = true;
                phone.FindPropertyRelative("applyOrthographicSize").boolValue = false;
                phone.FindPropertyRelative("applyFieldOfView").boolValue = false;
                so.ApplyModifiedPropertiesWithoutUndo();

                var live = new ResponsiveCameraState
                {
                    viewport = new Rect(0.1f, 0.2f, 0.7f, 0.6f),
                    localPosition = new Vector3(9f, 8f, 7f),
                    localEulerAngles = new Vector3(10f, 20f, 30f),
                    orthographic = true,
                    orthographicSize = 9f,
                    fieldOfView = 42f,
                };
                ResponsiveCameraEditorUtil.WriteCapturedState(
                    phone,
                    live,
                    enabled: true,
                    Vector2.zero,
                    ResponsiveDevice.FormFactor.Phone,
                    seedTransform: false,
                    captureLiveValues: true);
                so.ApplyModifiedPropertiesWithoutUndo();

                ResponsiveCameraState written = ResponsiveCameraEditorUtil.ReadState(phone);
                Assert.IsFalse(written.applyLocalPosition);
                Assert.IsTrue(written.applyLocalRotation);
                Assert.IsFalse(written.applyOrthographicSize);
                Assert.IsFalse(written.applyFieldOfView);
                Assert.AreEqual(live.viewport, written.viewport);
                Assert.AreEqual(live.localPosition, written.localPosition);
                Assert.AreEqual(live.localEulerAngles, written.localEulerAngles);
                Assert.AreEqual(live.orthographicSize, written.orthographicSize, 1e-3f);
                Assert.AreEqual(live.fieldOfView, written.fieldOfView, 1e-3f);
            });
        }

        [Test]
        public void WriteCapturedState_FocusCapture_UpdatesNonFramingValuesOnly()
        {
            WithCamera((so, layout) =>
            {
                SerializedProperty phone = layout.FindPropertyRelative("phoneLandscape");
                phone.FindPropertyRelative("localPosition").vector3Value =
                    new Vector3(1f, 2f, 3f);
                phone.FindPropertyRelative("localEulerAngles").vector3Value =
                    new Vector3(10f, 20f, 30f);
                phone.FindPropertyRelative("orthographic").boolValue = true;
                phone.FindPropertyRelative("orthographicSize").floatValue = 2f;
                phone.FindPropertyRelative("fieldOfView").floatValue = 50f;
                so.ApplyModifiedPropertiesWithoutUndo();

                var live = new ResponsiveCameraState
                {
                    viewport = new Rect(0.1f, 0.2f, 0.7f, 0.6f),
                    localPosition = new Vector3(9f, 8f, 7f),
                    localEulerAngles = new Vector3(40f, 50f, 60f),
                    orthographic = false,
                    orthographicSize = 9f,
                    fieldOfView = 75f,
                };
                ResponsiveCameraEditorUtil.WriteCapturedState(
                    phone,
                    live,
                    enabled: true,
                    Vector2.zero,
                    ResponsiveDevice.FormFactor.Phone,
                    seedTransform: false,
                    captureLiveValues: true,
                    preserveFocusPosition: true,
                    preserveFocusProjection: true);
                so.ApplyModifiedPropertiesWithoutUndo();

                ResponsiveCameraState written = ResponsiveCameraEditorUtil.ReadState(phone);
                Assert.AreEqual(live.viewport, written.viewport);
                Assert.AreEqual(new Vector3(1f, 2f, 3f), written.localPosition);
                Assert.AreEqual(live.localEulerAngles, written.localEulerAngles);
                Assert.IsFalse(written.orthographic);
                Assert.AreEqual(2f, written.orthographicSize, 1e-3f);
                Assert.AreEqual(50f, written.fieldOfView, 1e-3f);
            });
        }

        [Test]
        public void WriteCapturedState_CenterFocus_CapturesProjection()
        {
            WithCamera((so, layout) =>
            {
                SerializedProperty phone = layout.FindPropertyRelative("phoneLandscape");
                phone.FindPropertyRelative("localPosition").vector3Value =
                    new Vector3(1f, 2f, 3f);
                phone.FindPropertyRelative("orthographicSize").floatValue = 2f;
                phone.FindPropertyRelative("fieldOfView").floatValue = 50f;
                so.ApplyModifiedPropertiesWithoutUndo();

                var live = new ResponsiveCameraState
                {
                    viewport = ResponsiveCameraState.FullViewport,
                    localPosition = new Vector3(9f, 8f, 7f),
                    orthographic = true,
                    orthographicSize = 9f,
                    fieldOfView = 75f,
                };
                ResponsiveCameraEditorUtil.WriteCapturedState(
                    phone,
                    live,
                    enabled: true,
                    Vector2.zero,
                    ResponsiveDevice.FormFactor.Phone,
                    seedTransform: false,
                    captureLiveValues: true,
                    preserveFocusPosition: true,
                    preserveFocusProjection: false);
                so.ApplyModifiedPropertiesWithoutUndo();

                ResponsiveCameraState written = ResponsiveCameraEditorUtil.ReadState(phone);
                Assert.AreEqual(new Vector3(1f, 2f, 3f), written.localPosition);
                Assert.AreEqual(9f, written.orthographicSize, 1e-3f);
                Assert.AreEqual(75f, written.fieldOfView, 1e-3f);
            });
        }

        [Test]
        public void WriteCapturedState_SceneSync_DoesNotOverwriteApplyLocalPose()
        {
            WithCamera((so, layout) =>
            {
                SerializedProperty phone = layout.FindPropertyRelative("phoneLandscape");
                phone.FindPropertyRelative("enabled").boolValue = true;
                phone.FindPropertyRelative("applyLocalPosition").boolValue = true;
                phone.FindPropertyRelative("applyLocalRotation").boolValue = true;
                phone.FindPropertyRelative("localPosition").vector3Value = new Vector3(0f, 4.5f, 0f);
                phone.FindPropertyRelative("localEulerAngles").vector3Value = new Vector3(90f, 270f, 0f);
                so.ApplyModifiedPropertiesWithoutUndo();

                var live = new ResponsiveCameraState
                {
                    viewport = ResponsiveCameraState.FullViewport,
                    localPosition = Vector3.zero,
                    localEulerAngles = Vector3.zero,
                };
                ResponsiveCameraEditorUtil.WriteCapturedState(
                    phone,
                    live,
                    enabled: true,
                    Vector2.zero,
                    ResponsiveDevice.FormFactor.Phone,
                    seedTransform: false);
                so.ApplyModifiedPropertiesWithoutUndo();

                ResponsiveCameraState written = ResponsiveCameraEditorUtil.ReadState(phone);
                Assert.IsTrue(written.applyLocalPosition);
                Assert.IsTrue(written.applyLocalRotation);
                Assert.AreEqual(new Vector3(0f, 4.5f, 0f), written.localPosition);
                Assert.AreEqual(new Vector3(90f, 270f, 0f), written.localEulerAngles);
            });
        }

        [Test]
        public void WriteCapturedState_SeedWithoutFocus_EnablesApplyLocal()
        {
            WithCamera((so, layout) =>
            {
                SerializedProperty phone = layout.FindPropertyRelative("phoneLandscape");
                phone.FindPropertyRelative("enabled").boolValue = true;
                phone.FindPropertyRelative("applyLocalPosition").boolValue = false;
                phone.FindPropertyRelative("applyLocalRotation").boolValue = false;
                so.ApplyModifiedPropertiesWithoutUndo();

                var live = new ResponsiveCameraState
                {
                    viewport = ResponsiveCameraState.FullViewport,
                    localPosition = new Vector3(9f, 8f, 7f),
                    localEulerAngles = new Vector3(90f, 0f, 0f),
                };
                ResponsiveCameraEditorUtil.WriteCapturedState(
                    phone,
                    live,
                    enabled: true,
                    Vector2.zero,
                    ResponsiveDevice.FormFactor.Phone);
                so.ApplyModifiedPropertiesWithoutUndo();

                ResponsiveCameraState written = ResponsiveCameraEditorUtil.ReadState(phone);
                Assert.IsTrue(written.applyLocalPosition);
                Assert.IsTrue(written.applyLocalRotation);
                Assert.AreEqual(new Vector3(9f, 8f, 7f), written.localPosition);
                Assert.AreEqual(new Vector3(90f, 0f, 0f), written.localEulerAngles);
            });
        }

        [Test]
        public void WriteCapturedState_Focus_DoesNotEnableApplyLocal()
        {
            WithCamera((so, layout) =>
            {
                var table = new GameObject("FocusTable");
                try
                {
                    SerializedProperty phone = layout.FindPropertyRelative("phoneLandscape");
                    phone.FindPropertyRelative("enabled").boolValue = true;
                    phone.FindPropertyRelative("applyLocalPosition").boolValue = false;
                    phone.FindPropertyRelative("applyLocalRotation").boolValue = false;
                    phone.FindPropertyRelative("localPosition").vector3Value = new Vector3(1f, 2f, 3f);
                    phone.FindPropertyRelative("localEulerAngles").vector3Value = new Vector3(10f, 20f, 30f);
                    phone.FindPropertyRelative("focusTarget").objectReferenceValue = table.transform;
                    phone.FindPropertyRelative("focusScreen").intValue =
                        (int)ResponsiveCameraFocusScreen.Auto;
                    so.ApplyModifiedPropertiesWithoutUndo();

                    var live = new ResponsiveCameraState
                    {
                        viewport = ResponsiveCameraState.FullViewport,
                        localPosition = new Vector3(9f, 8f, 7f),
                        localEulerAngles = new Vector3(90f, 0f, 0f),
                    };
                    ResponsiveCameraEditorUtil.WriteCapturedState(
                        phone,
                        live,
                        enabled: true,
                        Vector2.zero,
                        ResponsiveDevice.FormFactor.Phone,
                        seedTransform: false);
                    so.ApplyModifiedPropertiesWithoutUndo();

                    ResponsiveCameraState written = ResponsiveCameraEditorUtil.ReadState(phone);
                    Assert.IsFalse(written.applyLocalPosition);
                    Assert.IsFalse(written.applyLocalRotation);
                    Assert.AreEqual(new Vector3(1f, 2f, 3f), written.localPosition);
                    Assert.AreEqual(new Vector3(10f, 20f, 30f), written.localEulerAngles);
                }
                finally
                {
                    Object.DestroyImmediate(table);
                }
            });
        }

        [Test]
        public void WriteCapturedState_Focus_SynchronizesLiveTransform()
        {
            WithCamera((so, layout) =>
            {
                var table = new GameObject("FocusTable");
                try
                {
                    SerializedProperty phone = layout.FindPropertyRelative("phoneLandscape");
                    phone.FindPropertyRelative("enabled").boolValue = true;
                    phone.FindPropertyRelative("applyLocalPosition").boolValue = true;
                    phone.FindPropertyRelative("applyLocalRotation").boolValue = true;
                    phone.FindPropertyRelative("localPosition").vector3Value = new Vector3(1f, 2f, 3f);
                    phone.FindPropertyRelative("localEulerAngles").vector3Value = new Vector3(10f, 20f, 30f);
                    phone.FindPropertyRelative("focusTarget").objectReferenceValue = table.transform;
                    phone.FindPropertyRelative("focusScreen").intValue =
                        (int)ResponsiveCameraFocusScreen.Auto;
                    so.ApplyModifiedPropertiesWithoutUndo();

                    var live = new ResponsiveCameraState
                    {
                        viewport = ResponsiveCameraState.FullViewport,
                        localPosition = new Vector3(9f, 8f, 7f),
                        localEulerAngles = new Vector3(90f, 0f, 0f),
                    };
                    ResponsiveCameraEditorUtil.WriteCapturedState(
                        phone,
                        live,
                        enabled: true,
                        Vector2.zero,
                        ResponsiveDevice.FormFactor.Phone);
                    so.ApplyModifiedPropertiesWithoutUndo();

                    ResponsiveCameraState written = ResponsiveCameraEditorUtil.ReadState(phone);
                    Assert.IsTrue(written.applyLocalPosition);
                    Assert.IsTrue(written.applyLocalRotation);
                    Assert.AreEqual(new Vector3(9f, 8f, 7f), written.localPosition);
                    Assert.AreEqual(new Vector3(90f, 0f, 0f), written.localEulerAngles);
                }
                finally
                {
                    Object.DestroyImmediate(table);
                }
            });
        }

        [Test]
        public void WriteCapturedState_Focus_SkipsOrthographicSize()
        {
            WithCamera((so, layout) =>
            {
                var table = new GameObject("FocusTable");
                try
                {
                    SerializedProperty phone = layout.FindPropertyRelative("phoneLandscape");
                    phone.FindPropertyRelative("enabled").boolValue = true;
                    phone.FindPropertyRelative("applyOrthographicSize").boolValue = true;
                    phone.FindPropertyRelative("orthographicSize").floatValue = 0.82f;
                    phone.FindPropertyRelative("applyFieldOfView").boolValue = true;
                    phone.FindPropertyRelative("fieldOfView").floatValue = 60f;
                    phone.FindPropertyRelative("focusTarget").objectReferenceValue = table.transform;
                    phone.FindPropertyRelative("focusScreen").intValue =
                        (int)ResponsiveCameraFocusScreen.Bounds;
                    so.ApplyModifiedPropertiesWithoutUndo();

                    var live = new ResponsiveCameraState
                    {
                        viewport = ResponsiveCameraState.FullViewport,
                        orthographic = true,
                        applyOrthographicSize = true,
                        orthographicSize = 0.4f,
                        applyFieldOfView = true,
                        fieldOfView = 12f,
                    };
                    ResponsiveCameraEditorUtil.WriteCapturedState(
                        phone,
                        live,
                        enabled: true,
                        Vector2.zero,
                        ResponsiveDevice.FormFactor.Phone,
                        seedTransform: false);
                    so.ApplyModifiedPropertiesWithoutUndo();

                    ResponsiveCameraState written = ResponsiveCameraEditorUtil.ReadState(phone);
                    Assert.AreEqual(0.82f, written.orthographicSize, 1e-3f);
                    Assert.AreEqual(60f, written.fieldOfView, 1e-3f);
                }
                finally
                {
                    Object.DestroyImmediate(table);
                }
            });
        }

        [Test]
        public void WriteCapturedState_Focus_SeedSynchronizesProjection()
        {
            WithCamera((so, layout) =>
            {
                var table = new GameObject("FocusTable");
                try
                {
                    SerializedProperty phone = layout.FindPropertyRelative("phoneLandscape");
                    phone.FindPropertyRelative("enabled").boolValue = true;
                    phone.FindPropertyRelative("applyOrthographicSize").boolValue = true;
                    phone.FindPropertyRelative("orthographicSize").floatValue = 0.82f;
                    phone.FindPropertyRelative("focusTarget").objectReferenceValue = table.transform;
                    phone.FindPropertyRelative("focusScreen").intValue =
                        (int)ResponsiveCameraFocusScreen.Bounds;
                    so.ApplyModifiedPropertiesWithoutUndo();

                    var live = new ResponsiveCameraState
                    {
                        viewport = ResponsiveCameraState.FullViewport,
                        orthographic = true,
                        applyOrthographicSize = true,
                        orthographicSize = 0.4f,
                        fieldOfView = 12f,
                    };
                    ResponsiveCameraEditorUtil.WriteCapturedState(
                        phone,
                        live,
                        enabled: true,
                        Vector2.zero,
                        ResponsiveDevice.FormFactor.Phone,
                        seedTransform: true);
                    so.ApplyModifiedPropertiesWithoutUndo();

                    ResponsiveCameraState written = ResponsiveCameraEditorUtil.ReadState(phone);
                    Assert.AreEqual(0.4f, written.orthographicSize, 1e-3f);
                    Assert.AreEqual(12f, written.fieldOfView, 1e-3f);
                }
                finally
                {
                    Object.DestroyImmediate(table);
                }
            });
        }

        [Test]
        public void MatchesLive_ScaleProjection_ComparesAspectScaledOrtho()
        {
            WithCamera((so, layout) =>
            {
                var go = new GameObject("LiveCam");
                try
                {
                    Vector2 design = ResponsiveUI.OrientSize(
                        ResponsiveRectState.GetDefaultDesignCanvasSize(
                            ResponsiveDevice.FormFactor.Phone),
                        ResponsiveDevice.Orientation.Landscape);
                    Vector2 liveUi = ResponsiveCatalogDevices.FoldCoverLandscape;
                    float liveOrtho = 0.87f * (design.x / design.y) / (liveUi.x / liveUi.y);

                    Camera cam = go.AddComponent<Camera>();
                    cam.orthographic = true;
                    cam.orthographicSize = liveOrtho;
                    cam.rect = ResponsiveCameraState.FullViewport;

                    SerializedProperty phone = layout.FindPropertyRelative("phoneLandscape");
                    phone.FindPropertyRelative("enabled").boolValue = true;
                    phone.FindPropertyRelative("orthographic").boolValue = true;
                    phone.FindPropertyRelative("scaleProjection").boolValue = true;
                    phone.FindPropertyRelative("uiScaleMode").enumValueIndex =
                        (int)ResponsiveDesignScaleMode.Width;
                    phone.FindPropertyRelative("applyOrthographicSize").boolValue = true;
                    phone.FindPropertyRelative("orthographicSize").floatValue = 0.87f;
                    phone.FindPropertyRelative("viewport").rectValue = ResponsiveCameraState.FullViewport;
                    so.ApplyModifiedPropertiesWithoutUndo();

                    Assert.IsTrue(ResponsiveCameraEditorUtil.MatchesLive(
                        phone, cam, liveUi, ResponsiveDevice.FormFactor.Phone));

                    cam.orthographicSize = 0.87f;
                    Assert.IsFalse(ResponsiveCameraEditorUtil.MatchesLive(
                        phone, cam, liveUi, ResponsiveDevice.FormFactor.Phone));
                }
                finally
                {
                    Object.DestroyImmediate(go);
                }
            });
        }

        [Test]
        public void MatchesLive_DetectsLiveOrthoWhenApplyOrthographicSizeOff()
        {
            WithCamera((so, layout) =>
            {
                var go = new GameObject("LiveCam");
                try
                {
                    Camera cam = go.AddComponent<Camera>();
                    cam.orthographic = true;
                    cam.orthographicSize = 9f;
                    cam.rect = ResponsiveCameraState.FullViewport;

                    SerializedProperty phone = layout.FindPropertyRelative("phoneLandscape");
                    phone.FindPropertyRelative("enabled").boolValue = true;
                    phone.FindPropertyRelative("orthographic").boolValue = true;
                    phone.FindPropertyRelative("applyOrthographicSize").boolValue = false;
                    phone.FindPropertyRelative("orthographicSize").floatValue = 0.87f;
                    phone.FindPropertyRelative("viewport").rectValue = ResponsiveCameraState.FullViewport;
                    so.ApplyModifiedPropertiesWithoutUndo();

                    Assert.IsFalse(ResponsiveCameraEditorUtil.MatchesLive(
                        phone, cam, Vector2.zero, ResponsiveDevice.FormFactor.Phone));
                }
                finally
                {
                    Object.DestroyImmediate(go);
                }
            });
        }

        [Test]
        public void MatchesLive_DetectsLivePositionWhenApplyLocalOff()
        {
            WithCamera((so, layout) =>
            {
                var go = new GameObject("LiveCam");
                try
                {
                    Camera cam = go.AddComponent<Camera>();
                    cam.transform.localPosition = new Vector3(9f, 8f, 7f);
                    cam.orthographic = true;
                    cam.orthographicSize = 0.87f;
                    cam.rect = ResponsiveCameraState.FullViewport;

                    SerializedProperty phone = layout.FindPropertyRelative("phoneLandscape");
                    phone.FindPropertyRelative("enabled").boolValue = true;
                    phone.FindPropertyRelative("applyLocalPosition").boolValue = false;
                    phone.FindPropertyRelative("applyLocalRotation").boolValue = false;
                    phone.FindPropertyRelative("localPosition").vector3Value = Vector3.zero;
                    phone.FindPropertyRelative("localEulerAngles").vector3Value = Vector3.zero;
                    phone.FindPropertyRelative("orthographic").boolValue = true;
                    phone.FindPropertyRelative("applyOrthographicSize").boolValue = true;
                    phone.FindPropertyRelative("orthographicSize").floatValue = 0.87f;
                    phone.FindPropertyRelative("viewport").rectValue = ResponsiveCameraState.FullViewport;
                    so.ApplyModifiedPropertiesWithoutUndo();

                    Assert.IsFalse(ResponsiveCameraEditorUtil.MatchesLive(
                        phone, cam, Vector2.zero, ResponsiveDevice.FormFactor.Phone));
                }
                finally
                {
                    Object.DestroyImmediate(go);
                }
            });
        }

        [Test]
        public void MatchesLive_FocusFraming_StillComparesRotationAndViewport()
        {
            WithCamera((so, layout) =>
            {
                var go = new GameObject("LiveCam");
                try
                {
                    Camera cam = go.AddComponent<Camera>();
                    cam.orthographic = true;
                    cam.orthographicSize = 9f;
                    cam.rect = ResponsiveCameraState.FullViewport;

                    SerializedProperty phone = layout.FindPropertyRelative("phoneLandscape");
                    phone.FindPropertyRelative("localPosition").vector3Value = Vector3.zero;
                    phone.FindPropertyRelative("localEulerAngles").vector3Value = Vector3.zero;
                    phone.FindPropertyRelative("orthographic").boolValue = true;
                    phone.FindPropertyRelative("orthographicSize").floatValue = 2f;
                    phone.FindPropertyRelative("viewport").rectValue =
                        ResponsiveCameraState.FullViewport;
                    so.ApplyModifiedPropertiesWithoutUndo();

                    Assert.IsTrue(ResponsiveCameraEditorUtil.MatchesLive(
                        phone,
                        cam,
                        Vector2.zero,
                        ResponsiveDevice.FormFactor.Phone,
                        ignoreFraming: true));

                    cam.transform.localEulerAngles = new Vector3(10f, 20f, 30f);
                    Assert.IsFalse(ResponsiveCameraEditorUtil.MatchesLive(
                        phone,
                        cam,
                        Vector2.zero,
                        ResponsiveDevice.FormFactor.Phone,
                        ignoreFraming: true));

                    cam.transform.localEulerAngles = Vector3.zero;
                    cam.rect = new Rect(0.1f, 0.2f, 0.7f, 0.6f);
                    Assert.IsFalse(ResponsiveCameraEditorUtil.MatchesLive(
                        phone,
                        cam,
                        Vector2.zero,
                        ResponsiveDevice.FormFactor.Phone,
                        ignoreFraming: true));
                }
                finally
                {
                    Object.DestroyImmediate(go);
                }
            });
        }

        [Test]
        public void MatchesLive_CenterFocus_StillComparesProjection()
        {
            WithCamera((so, layout) =>
            {
                var go = new GameObject("LiveCam");
                try
                {
                    Camera cam = go.AddComponent<Camera>();
                    cam.orthographic = true;
                    cam.orthographicSize = 9f;
                    cam.rect = ResponsiveCameraState.FullViewport;

                    SerializedProperty phone = layout.FindPropertyRelative("phoneLandscape");
                    phone.FindPropertyRelative("localEulerAngles").vector3Value = Vector3.zero;
                    phone.FindPropertyRelative("orthographic").boolValue = true;
                    phone.FindPropertyRelative("orthographicSize").floatValue = 2f;
                    phone.FindPropertyRelative("viewport").rectValue =
                        ResponsiveCameraState.FullViewport;
                    so.ApplyModifiedPropertiesWithoutUndo();

                    Assert.IsFalse(ResponsiveCameraEditorUtil.MatchesLive(
                        phone,
                        cam,
                        Vector2.zero,
                        ResponsiveDevice.FormFactor.Phone,
                        ignoreFocusPosition: true,
                        ignoreFocusProjection: false));
                    Assert.IsTrue(ResponsiveCameraEditorUtil.MatchesLive(
                        phone,
                        cam,
                        Vector2.zero,
                        ResponsiveDevice.FormFactor.Phone,
                        ignoreFocusPosition: true,
                        ignoreFocusProjection: true));
                }
                finally
                {
                    Object.DestroyImmediate(go);
                }
            });
        }

        [Test]
        public void SeedSlots_EnablesAllowedExceptFlipCoverLandscapeAndCopiesPhone()
        {
            WithCamera((so, layout) =>
            {
                SerializedProperty phone = layout.FindPropertyRelative("phonePortrait");
                phone.FindPropertyRelative("enabled").boolValue = true;
                phone.FindPropertyRelative("applyLocalPosition").boolValue = true;
                phone.FindPropertyRelative("localPosition").vector3Value = new Vector3(4f, 5f, 6f);

                SerializedProperty phoneLandscape = layout.FindPropertyRelative("phoneLandscape");
                phoneLandscape.FindPropertyRelative("enabled").boolValue = true;
                phoneLandscape.FindPropertyRelative("applyLocalPosition").boolValue = true;
                phoneLandscape.FindPropertyRelative("localPosition").vector3Value = new Vector3(7f, 8f, 9f);

                string[] states = ResponsiveCameraLayout.StatePropertyNames;
                for (int i = 1; i < states.Length; i++)
                    layout.FindPropertyRelative(states[i]).FindPropertyRelative("enabled").boolValue = false;
                so.ApplyModifiedPropertiesWithoutUndo();

                ResponsiveCameraEditorUtil.SeedSlots(layout);
                so.ApplyModifiedPropertiesWithoutUndo();
                so.Update();

                for (int i = 0; i < states.Length; i++)
                {
                    bool expectedEnabled = ExpectedSeedEnabled(i);
                    SerializedProperty slot = layout.FindPropertyRelative(states[i]);
                    AssertSlotEnabled(slot, expectedEnabled, states[i]);
                    if (!expectedEnabled || i == 0)
                        continue;

                    Vector3 copied = slot.FindPropertyRelative("localPosition").vector3Value;
                    bool landscape = ExpectedSeedSourceIsLandscape(i);
                    Vector3 expected = landscape
                        ? new Vector3(7f, 8f, 9f)
                        : new Vector3(4f, 5f, 6f);
                    Assert.AreEqual(expected.x, copied.x, 1e-4f, states[i]);
                    Assert.AreEqual(expected.y, copied.y, 1e-4f, states[i]);
                    Assert.AreEqual(expected.z, copied.z, 1e-4f, states[i]);
                }
            });
        }

        [Test]
        public void ResetSlots_Camera_ClearsAuthoredAndReseedsEnabled()
        {
            WithCamera((so, layout) =>
            {
                string[] states = ResponsiveCameraLayout.StatePropertyNames;
                for (int i = 0; i < states.Length; i++)
                {
                    SerializedProperty slot = layout.FindPropertyRelative(states[i]);
                    slot.FindPropertyRelative("enabled").boolValue = true;
                    slot.FindPropertyRelative("applyLocalPosition").boolValue = true;
                    slot.FindPropertyRelative("localPosition").vector3Value =
                        new Vector3(i + 1f, i + 2f, i + 3f);
                    slot.FindPropertyRelative("scaleFromUiSize").boolValue = true;
                    slot.FindPropertyRelative("scaleProjection").boolValue = true;
                    slot.FindPropertyRelative("uiScaleMode").enumValueIndex =
                        (int)ResponsiveDesignScaleMode.IndependentXY;
                }
                so.ApplyModifiedPropertiesWithoutUndo();

                ResponsiveCameraEditorUtil.ResetSlots(layout);
                so.ApplyModifiedPropertiesWithoutUndo();
                so.Update();

                ResponsiveCameraState expected = ResponsiveCameraState.CreateDefault();
                for (int i = 0; i < states.Length; i++)
                {
                    SerializedProperty slot = layout.FindPropertyRelative(states[i]);
                    bool expectedEnabled = ExpectedSeedEnabled(i);
                    AssertSlotEnabled(slot, expectedEnabled, states[i]);
                    ResponsiveCameraState actual = ResponsiveCameraEditorUtil.ReadState(slot);
                    Assert.AreEqual(expected.localPosition, actual.localPosition, states[i]);
                    Assert.IsFalse(actual.applyLocalPosition, states[i]);
                    Assert.IsFalse(actual.scaleFromUiSize, states[i]);
                    Assert.IsFalse(actual.scaleProjection, states[i]);
                    Assert.AreEqual(
                        ResponsiveDesignScaleMode.Auto,
                        actual.uiScaleMode,
                        states[i]);
                }
            });
        }

        [Test]
        public void ResetNewListRow_Camera_ClearsDuplicatedTargetBackgroundAndSlots()
        {
            WithCamera((so, layout) =>
            {
                var camGo = new GameObject("DupCamera", typeof(Camera));
                var spriteGo = new GameObject("DupSprite", typeof(SpriteRenderer));
                var boundsGo = new GameObject("DupBounds", typeof(RectTransform));
                try
                {
                    layout.FindPropertyRelative("target").objectReferenceValue =
                        camGo.GetComponent<Camera>();
                    layout.FindPropertyRelative("spriteFitMode").enumValueIndex =
                        (int)ResponsiveCameraSpriteFitMode.Cover;
                    layout.FindPropertyRelative("background").objectReferenceValue =
                        spriteGo.GetComponent<SpriteRenderer>();

                    SerializedProperty phone = layout.FindPropertyRelative("phoneLandscape");
                    phone.FindPropertyRelative("enabled").boolValue = true;
                    phone.FindPropertyRelative("applyLocalPosition").boolValue = true;
                    phone.FindPropertyRelative("localPosition").vector3Value =
                        new Vector3(4f, 5f, 6f);
                    phone.FindPropertyRelative("focusTarget").objectReferenceValue =
                        spriteGo.transform;
                    phone.FindPropertyRelative("focusScreen").intValue =
                        (int)ResponsiveCameraFocusScreen.Auto;
                    phone.FindPropertyRelative("focusBounds").objectReferenceValue =
                        boundsGo.GetComponent<RectTransform>();
                    SerializedProperty phoneBounds =
                        phone.FindPropertyRelative("focusBoundsLayout");
                    phoneBounds.FindPropertyRelative("enabled").boolValue = true;
                    phoneBounds.FindPropertyRelative("sizeDelta").vector2Value =
                        new Vector2(40f, 80f);
                    so.ApplyModifiedPropertiesWithoutUndo();

                    ResponsiveCameraEditorUtil.ResetNewListRow(layout);
                    so.ApplyModifiedPropertiesWithoutUndo();
                    so.Update();

                    Assert.IsNull(layout.FindPropertyRelative("target").objectReferenceValue);
                    Assert.IsNull(layout.FindPropertyRelative("background").objectReferenceValue);
                    Assert.AreEqual(
                        (int)ResponsiveCameraSpriteFitMode.Cover,
                        layout.FindPropertyRelative("spriteFitMode").enumValueIndex);

                    string[] states = ResponsiveCameraLayout.StatePropertyNames;
                    ResponsiveCameraState expected = ResponsiveCameraState.CreateDefault();
                    for (int i = 0; i < states.Length; i++)
                    {
                        SerializedProperty slot = layout.FindPropertyRelative(states[i]);
                        bool expectedEnabled = ExpectedSeedEnabled(i);
                        AssertSlotEnabled(slot, expectedEnabled, states[i]);
                        ResponsiveCameraState actual =
                            ResponsiveCameraEditorUtil.ReadState(slot);
                        Assert.AreEqual(expected.localPosition, actual.localPosition, states[i]);
                        Assert.IsFalse(actual.applyLocalPosition, states[i]);
                        Assert.IsNull(actual.focusTarget, states[i]);
                        Assert.AreEqual(
                            ResponsiveCameraFocusScreen.None, actual.focusScreen, states[i]);
                        Assert.IsNull(actual.focusBounds, states[i]);
                        Assert.IsFalse(actual.focusBoundsLayout.enabled, states[i]);
                    }
                }
                finally
                {
                    Object.DestroyImmediate(boundsGo);
                    Object.DestroyImmediate(spriteGo);
                    Object.DestroyImmediate(camGo);
                }
            });
        }

        [Test]
        public void CopyFallback_CopiesIntervalAndOffset()
        {
            var host = ScriptableObject.CreateInstance<ResponsiveSlotSeedScrollHost>();
            try
            {
                var so = new SerializedObject(host);
                SerializedProperty layout = so.FindProperty("layout");
                SerializedProperty phone = layout.FindPropertyRelative("phonePortrait");
                phone.FindPropertyRelative("enabled").boolValue = true;
                phone.FindPropertyRelative("cellInterval").floatValue = 0.18f;
                phone.FindPropertyRelative("scrollOffset").floatValue = 0.42f;

                SerializedProperty pad = layout.FindPropertyRelative("padPortrait");
                pad.FindPropertyRelative("enabled").boolValue = true;
                pad.FindPropertyRelative("cellInterval").floatValue = 0.99f;
                so.ApplyModifiedPropertiesWithoutUndo();

                Assert.IsTrue(ResponsiveScrollEditorUtil.CopyFallback(pad));
                Assert.AreEqual(0.18f, pad.FindPropertyRelative("cellInterval").floatValue);
                Assert.AreEqual(0.42f, pad.FindPropertyRelative("scrollOffset").floatValue);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void WriteState_PreservesIndependentApplyLocalFlags()
        {
            WithCamera((so, layout) =>
            {
                ResponsiveCameraState source = ResponsiveCameraState.CreateDefault(enabled: true);
                source.applyLocalPosition = true;
                source.applyLocalRotation = false;
                SerializedProperty slot = layout.FindPropertyRelative("phonePortrait");
                ResponsiveCameraEditorUtil.WriteState(slot, source);
                so.ApplyModifiedPropertiesWithoutUndo();

                ResponsiveCameraState written = ResponsiveCameraEditorUtil.ReadState(slot);
                Assert.IsTrue(written.applyLocalPosition);
                Assert.IsFalse(written.applyLocalRotation);

                source.applyLocalPosition = false;
                source.applyLocalRotation = true;
                ResponsiveCameraEditorUtil.WriteState(slot, source);
                so.ApplyModifiedPropertiesWithoutUndo();

                written = ResponsiveCameraEditorUtil.ReadState(slot);
                Assert.IsFalse(written.applyLocalPosition);
                Assert.IsTrue(written.applyLocalRotation);
            });
        }

        [Test]
        public void BulkCopyCamera_CopiesAllSlotValues()
        {
            WithCamera((so, _) =>
            {
                SerializedProperty layouts = so.FindProperty("cameras");
                layouts.arraySize = 2;
                var expected = new ResponsiveCameraState[2];
                for (int i = 0; i < layouts.arraySize; i++)
                {
                    ResponsiveCameraState source =
                        ResponsiveCameraState.CreateDefault(enabled: false);
                    source.viewport = new Rect(0.1f, 0.2f, 0.3f, 0.4f);
                    source.applyLocalPosition = true;
                    source.localPosition = new Vector3(i + 1f, 2f, 3f);
                    source.applyLocalRotation = true;
                    source.localEulerAngles = new Vector3(4f, 5f, 6f);
                    source.scaleFromUiSize = true;
                    source.scaleProjection = true;
                    source.uiScaleMode = ResponsiveDesignScaleMode.IndependentXY;
                    source.applyFieldOfView = true;
                    source.fieldOfView = 72f;
                    source.applyOrthographicSize = true;
                    source.orthographicSize = 8f;
                    ResponsiveCameraEditorUtil.WriteState(
                        layouts.GetArrayElementAtIndex(i)
                            .FindPropertyRelative("padLandscape"),
                        source);
                    expected[i] = source;
                }

                Assert.AreEqual(
                    2,
                    ResponsiveCameraEditorUtil.CopySlotAcrossLayouts(
                        layouts,
                        sourceSlotIndex: ResponsiveWidgetLayout.StatePropertyIndex("padLandscape"),
                        destinationSlotIndex: ResponsiveWidgetLayout.StatePropertyIndex("foldHalfOpenedLandscape")));
                for (int i = 0; i < layouts.arraySize; i++)
                {
                    Assert.AreEqual(
                        expected[i],
                        ResponsiveCameraEditorUtil.ReadState(
                            layouts.GetArrayElementAtIndex(i)
                                .FindPropertyRelative("foldHalfOpenedLandscape")));
                }
            });
        }

        [Test]
        public void BulkCopyScroll_CopiesAllSlotValues()
        {
            var host = ScriptableObject.CreateInstance<ResponsiveSlotSeedScrollHost>();
            try
            {
                var so = new SerializedObject(host);
                SerializedProperty layout = so.FindProperty("layout");
                var source = new ResponsiveScrollState
                {
                    enabled = false,
                    cellInterval = 0.18f,
                    scrollOffset = 0.42f,
                };
                ResponsiveScrollEditorUtil.WriteState(
                    layout.FindPropertyRelative("padLandscape"),
                    source);

                Assert.IsTrue(ResponsiveScrollEditorUtil.CopySlot(
                    layout,
                    ResponsiveWidgetLayout.StatePropertyIndex("padLandscape"),
                    ResponsiveWidgetLayout.StatePropertyIndex("foldHalfOpenedLandscape")));
                Assert.AreEqual(
                    source,
                    ResponsiveScrollEditorUtil.ReadState(
                        layout.FindPropertyRelative("foldHalfOpenedLandscape")));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void BulkCopyCameraAndScroll_SameSlot_DoNothing()
        {
            WithCamera((_, layout) =>
            {
                Assert.AreEqual(
                    0,
                    ResponsiveCameraEditorUtil.CopySlotAcrossLayouts(
                        layout.serializedObject.FindProperty("cameras"),
                        sourceSlotIndex: ResponsiveWidgetLayout.StatePropertyIndex("padLandscape"),
                        destinationSlotIndex: ResponsiveWidgetLayout.StatePropertyIndex("padLandscape")));
            });

            var host = ScriptableObject.CreateInstance<ResponsiveSlotSeedScrollHost>();
            try
            {
                var so = new SerializedObject(host);
                Assert.IsFalse(
                    ResponsiveScrollEditorUtil.CopySlot(
                        so.FindProperty("layout"),
                        sourceSlotIndex: ResponsiveWidgetLayout.StatePropertyIndex("padLandscape"),
                        destinationSlotIndex: ResponsiveWidgetLayout.StatePropertyIndex("padLandscape")));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void ScrollBind_ExposesInstanceCallbackOwnerForUndo()
        {
            var go = new GameObject("ResponsiveScrollUndoTargetTest");
            var host = ScriptableObject.CreateInstance<ResponsiveSlotSeedScrollHost>();
            try
            {
                var scroll = go.AddComponent<ResponsiveScrollView>();
                scroll.Bind(host.Apply);
                Assert.AreSame(host, scroll.EditorApplyTarget);
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(host);
            }
        }

    #endregion

    #region Clipboard

        [Test]
        public void BulkCopy_CopiesEachWidgetSourceToDestinationOnly()
        {
            WithWidgetList(2, widgets =>
            {
                var sources = new ResponsiveRectState[2];
                var unrelated = new ResponsiveRectState[2];
                for (int i = 0; i < widgets.arraySize; i++)
                {
                    SerializedProperty widget = widgets.GetArrayElementAtIndex(i);
                    ResponsiveRectState source = PhoneSample(landscape: true);
                    source.enabled = i == 1;
                    source.scaleFromDesignCanvas = i == 0;
                    source.designScaleMode = i == 0
                        ? ResponsiveDesignScaleMode.UniformMin
                        : ResponsiveDesignScaleMode.IndependentXY;
                    source.designCanvasSize = new Vector2(2048f + i, 1536f + i);
                    source.defaultScale = new Vector2(1.1f + i, 1.2f + i);
                    source.anchoredPosition += Vector2.one * (100f * i);
                    sources[i] = source;

                    ResponsiveRectState keep = PhoneSample(landscape: false);
                    keep.anchoredPosition += Vector2.one * (300f + i);
                    unrelated[i] = keep;

                    WriteWidgetLayout(widget, "padLandscape", source, source.enabled);
                    WriteWidgetLayout(widget, "foldHalfOpenedLandscape", default, enabled: true);
                    WriteWidgetLayout(widget, "flipHalfOpenedPortrait", keep, enabled: true);
                }

                int copied = ResponsiveRectStateEditorUtil.CopySlotAcrossWidgets(
                    widgets,
                    sourceSlotIndex: ResponsiveWidgetLayout.StatePropertyIndex("padLandscape"),
                    destinationSlotIndex: ResponsiveWidgetLayout.StatePropertyIndex("foldHalfOpenedLandscape"));

                Assert.AreEqual(2, copied);
                for (int i = 0; i < widgets.arraySize; i++)
                {
                    SerializedProperty widget = widgets.GetArrayElementAtIndex(i);
                    Assert.IsTrue(ResponsiveRectStateEditorUtil.SameAuthoring(
                        sources[i],
                        ResponsiveRectStateEditorUtil.ReadState(
                            widget.FindPropertyRelative("foldHalfOpenedLandscape"))));
                    Assert.IsTrue(ResponsiveRectStateEditorUtil.SameAuthoring(
                        sources[i],
                        ResponsiveRectStateEditorUtil.ReadState(
                            widget.FindPropertyRelative("padLandscape"))));
                    Assert.IsTrue(ResponsiveRectStateEditorUtil.SameAuthoring(
                        unrelated[i],
                        ResponsiveRectStateEditorUtil.ReadState(
                            widget.FindPropertyRelative("flipHalfOpenedPortrait"))));
                }
            });
        }

        [Test]
        public void BulkCopy_SameSlot_DoesNothing()
        {
            WithWidgetList(1, widgets =>
            {
                Assert.AreEqual(
                    0,
                    ResponsiveRectStateEditorUtil.CopySlotAcrossWidgets(widgets, 4, 4));
            });
        }

        [Test]
        public void Copy_Paste_CopiesAllWidgetValues()
        {
            WithWidget((so, widget) =>
            {
                WriteWidgetLayout(widget, "phoneLandscape", PhoneSample(landscape: true), enabled: true);
                so.ApplyModifiedPropertiesWithoutUndo();

                SerializedProperty phone = widget.FindPropertyRelative("phoneLandscape");
                SerializedProperty pad = widget.FindPropertyRelative("padLandscape");
                ResponsiveUiSlotClipboard.Clear();
                ResponsiveUiSlotClipboard.Copy(phone, ResponsiveUiSlotClipboard.Kind.Widget);
                Assert.IsTrue(ResponsiveUiSlotClipboard.Paste(pad, ResponsiveUiSlotClipboard.Kind.Widget));

                ResponsiveRectState copied = ResponsiveRectStateEditorUtil.ReadState(pad);
                AssertLayoutEquals(PhoneSample(landscape: true), copied);
                Assert.IsTrue(copied.enabled);
            });
        }

        [Test]
        public void Reset_KeepsSizeDelta_RestoresUnityRectDefaults()
        {
            WithWidget((so, widget) =>
            {
                WriteWidgetLayout(widget, "padLandscape", PhoneSample(landscape: true), enabled: true);
                so.ApplyModifiedPropertiesWithoutUndo();

                SerializedProperty pad = widget.FindPropertyRelative("padLandscape");
                Vector2 size = pad.FindPropertyRelative("sizeDelta").vector2Value;
                ResponsiveUiSlotClipboard.Reset(pad, ResponsiveUiSlotClipboard.Kind.Widget);

                ResponsiveRectState reset = ResponsiveRectStateEditorUtil.ReadState(pad);
                Assert.AreEqual(size, reset.sizeDelta);
                Assert.AreEqual(new Vector2(0.5f, 0.5f), reset.anchorMin);
                Assert.AreEqual(new Vector2(0.5f, 0.5f), reset.anchorMax);
                Assert.AreEqual(Vector2.zero, reset.anchoredPosition);
                Assert.AreEqual(new Vector2(0.5f, 0.5f), reset.pivot);
                Assert.IsFalse(reset.scaleFromDesignCanvas);
                Assert.AreEqual(Vector2.one, reset.defaultScale);
                Assert.IsTrue(reset.enabled);
            });
        }

        [Test]
        public void RecaptureSlotFromLive_NestedCanvas_UnscalesFromContainingCanvasRect()
        {
            var previousDesignProvider =
                ResponsiveRectState.DesignCanvasSizeProvider;
            ResponsiveRectState.DesignCanvasSizeProvider =
                _ => new Vector2(400f, 800f);
            var root = new GameObject(
                "RootCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(ResponsiveUI));
            var nested = new GameObject("NestedCanvas", typeof(RectTransform), typeof(Canvas));
            var target = new GameObject("StretchTarget", typeof(RectTransform));
            try
            {
                nested.transform.SetParent(root.transform, false);
                target.transform.SetParent(nested.transform, false);

                root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                nested.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                CanvasScaler scaler = root.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

                ResponsiveUI hub = root.GetComponent<ResponsiveUI>();
                hub.enabled = false;
                var hubSo = new SerializedObject(hub);
                SerializedProperty applyScaler = hubSo.FindProperty("applyCanvasScaler");
                Assert.NotNull(applyScaler, "applyCanvasScaler is serialized on the hub");
                hubSo.FindProperty("canvasScaler").objectReferenceValue = scaler;
                applyScaler.boolValue = true;
                hubSo.ApplyModifiedPropertiesWithoutUndo();

                RectTransform rootRt = root.GetComponent<RectTransform>();
                rootRt.anchorMin = rootRt.anchorMax = new Vector2(0.5f, 0.5f);
                rootRt.pivot = new Vector2(0.5f, 0.5f);
                rootRt.sizeDelta = new Vector2(1000f, 1000f);

                RectTransform nestedRt = nested.GetComponent<RectTransform>();
                nestedRt.anchorMin = nestedRt.anchorMax = new Vector2(0.5f, 0.5f);
                nestedRt.pivot = new Vector2(0.5f, 0.5f);
                nestedRt.sizeDelta = new Vector2(200f, 400f);
                nestedRt.ForceUpdateRectTransforms();
                Canvas.ForceUpdateCanvases();
                Assert.AreEqual(200f, nestedRt.rect.width, 1e-3f, "nested canvas width");
                Assert.AreEqual(400f, nestedRt.rect.height, 1e-3f, "nested canvas height");

                RectTransform rt = target.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.pivot = new Vector2(0.25f, 0.75f);
                rt.anchoredPosition = new Vector2(40f, -20f);
                rt.sizeDelta = new Vector2(-80f, -40f);
                rt.localScale = new Vector3(1.2f, 0.8f, 1f);

                var state = new ResponsiveRectState
                {
                    enabled = true,
                    scaleFromDesignCanvas = true,
                    designScaleMode = ResponsiveDesignScaleMode.IndependentXY,
                    designCanvasSize = new Vector2(400f, 800f),
                    defaultScale = Vector2.one,
                    anchorMin = Vector2.zero,
                    anchorMax = Vector2.one,
                    anchoredPosition = Vector2.zero,
                    sizeDelta = Vector2.zero,
                    pivot = rt.pivot,
                };

                var controller = root.AddComponent<ResponsiveUiController>();
                var so = new SerializedObject(controller);
                SerializedProperty widgets = so.FindProperty("widgets");
                widgets.arraySize = 1;
                SerializedProperty slot = widgets.GetArrayElementAtIndex(0)
                    .FindPropertyRelative("phonePortrait");
                ResponsiveRectStateEditorUtil.WriteState(slot, state);
                so.ApplyModifiedPropertiesWithoutUndo();
                so.Update();

                Assert.IsTrue(hub.UsesCanvasScalerResolutionFitFor(rt));
                Vector2 livePos = rt.anchoredPosition;
                Vector2 liveSize = rt.sizeDelta;
                Vector3 liveScale = rt.localScale;

                ResponsiveRectStateEditorUtil.RecaptureSlotFromLive(slot, rt, hub);
                ResponsiveRectState captured = ResponsiveRectStateEditorUtil.ReadState(slot);

                Assert.AreEqual(80f, captured.anchoredPosition.x, 1e-3f);
                Assert.AreEqual(-40f, captured.anchoredPosition.y, 1e-3f);
                Assert.AreEqual(2.4f, captured.defaultScale.x, 1e-4f);
                Assert.AreEqual(1.6f, captured.defaultScale.y, 1e-4f);
                Assert.AreEqual(-280f, captured.sizeDelta.x, 1e-3f);
                Assert.AreEqual(-440f, captured.sizeDelta.y, 1e-3f);

                Vector2 ratio = hub.GetLayoutRatio(
                    captured,
                    ResponsiveDevice.FormFactor.Phone,
                    rt,
                    ResponsiveDevice.Orientation.Portrait);
                Vector2 localScale = hub.GetAppliedWidgetScale(
                    captured,
                    ResponsiveDevice.FormFactor.Phone,
                    ratio);
                captured.Apply(rt, ratio, localScale);

                Assert.AreEqual(0.5f, ratio.x, 1e-4f);
                Assert.AreEqual(0.5f, ratio.y, 1e-4f);
                Assert.AreEqual(livePos.x, rt.anchoredPosition.x, 1e-3f);
                Assert.AreEqual(livePos.y, rt.anchoredPosition.y, 1e-3f);
                Assert.AreEqual(liveSize.x, rt.sizeDelta.x, 1e-3f);
                Assert.AreEqual(liveSize.y, rt.sizeDelta.y, 1e-3f);
                Assert.AreEqual(liveScale.x, rt.localScale.x, 1e-4f);
                Assert.AreEqual(liveScale.y, rt.localScale.y, 1e-4f);
            }
            finally
            {
                ResponsiveRectState.DesignCanvasSizeProvider =
                    previousDesignProvider;
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void RecaptureSlotFromLive_ChangingScaleMode_KeepsLiveTarget()
        {
            var previousDesignProvider =
                ResponsiveRectState.DesignCanvasSizeProvider;
            ResponsiveRectState.DesignCanvasSizeProvider =
                _ => new Vector2(500f, 1000f);
            var root = new GameObject(
                "ModeRecaptureCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(ResponsiveUI));
            var target = new GameObject("ModeRecaptureTarget", typeof(RectTransform));
            try
            {
                target.transform.SetParent(root.transform, false);

                root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
                CanvasScaler scaler = root.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

                ResponsiveUI hub = root.GetComponent<ResponsiveUI>();
                hub.enabled = false;
                var hubSo = new SerializedObject(hub);
                hubSo.FindProperty("canvasScaler").objectReferenceValue = scaler;
                hubSo.FindProperty("applyCanvasScaler").boolValue = true;
                hubSo.ApplyModifiedPropertiesWithoutUndo();

                RectTransform canvasRt = root.GetComponent<RectTransform>();
                canvasRt.anchorMin = canvasRt.anchorMax = new Vector2(0.5f, 0.5f);
                canvasRt.pivot = new Vector2(0.5f, 0.5f);
                canvasRt.sizeDelta = new Vector2(1000f, 500f);
                canvasRt.ForceUpdateRectTransforms();
                Canvas.ForceUpdateCanvases();

                RectTransform rt = target.GetComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(40f, -20f);
                rt.sizeDelta = new Vector2(100f, 80f);
                rt.localScale = Vector3.one;

                var state = new ResponsiveRectState
                {
                    enabled = true,
                    scaleFromDesignCanvas = true,
                    designScaleMode = ResponsiveDesignScaleMode.Width,
                    designCanvasSize = new Vector2(500f, 1000f),
                    defaultScale = Vector2.one,
                    anchorMin = rt.anchorMin,
                    anchorMax = rt.anchorMax,
                    pivot = rt.pivot,
                };

                var controller = root.AddComponent<ResponsiveUiController>();
                var so = new SerializedObject(controller);
                SerializedProperty widgets = so.FindProperty("widgets");
                widgets.arraySize = 1;
                SerializedProperty slot = widgets.GetArrayElementAtIndex(0)
                    .FindPropertyRelative("phonePortrait");
                ResponsiveRectStateEditorUtil.WriteState(slot, state);
                so.ApplyModifiedPropertiesWithoutUndo();
                so.Update();

                ResponsiveRectStateEditorUtil.RecaptureSlotFromLive(slot, rt, hub);

                slot.FindPropertyRelative("designScaleMode").intValue =
                    (int)ResponsiveDesignScaleMode.Height;
                so.ApplyModifiedPropertiesWithoutUndo();
                so.Update();

                Vector2 livePos = rt.anchoredPosition;
                Vector3 liveScale = rt.localScale;
                ResponsiveRectStateEditorUtil.RecaptureSlotFromLive(slot, rt, hub);
                ResponsiveRectState captured = ResponsiveRectStateEditorUtil.ReadState(slot);

                Vector2 ratio = hub.GetLayoutRatio(
                    captured,
                    ResponsiveDevice.FormFactor.Phone,
                    rt,
                    ResponsiveDevice.Orientation.Portrait);
                captured.Apply(
                    rt,
                    ratio,
                    hub.GetAppliedWidgetScale(
                        captured, ResponsiveDevice.FormFactor.Phone, ratio));

                Assert.AreEqual(0.5f, ratio.x, 1e-4f);
                Assert.AreEqual(livePos.x, rt.anchoredPosition.x, 1e-3f);
                Assert.AreEqual(livePos.y, rt.anchoredPosition.y, 1e-3f);
                Assert.AreEqual(liveScale.x, rt.localScale.x, 1e-4f);
                Assert.AreEqual(liveScale.y, rt.localScale.y, 1e-4f);
            }
            finally
            {
                ResponsiveRectState.DesignCanvasSizeProvider =
                    previousDesignProvider;
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void MatchesLayout_FullStretch_UsesScaledOffsets()
        {
            WithWidget((so, widget) =>
            {
                var parent = new GameObject("StretchParent", typeof(RectTransform));
                var target = new GameObject("StretchTarget", typeof(RectTransform));
                try
                {
                    parent.GetComponent<RectTransform>().sizeDelta = new Vector2(500f, 750f);
                    target.transform.SetParent(parent.transform, false);
                    var state = new ResponsiveRectState
                    {
                        enabled = true,
                        scaleFromDesignCanvas = true,
                        designScaleMode = ResponsiveDesignScaleMode.IndependentXY,
                        designCanvasSize = new Vector2(1000f, 1000f),
                        defaultScale = Vector2.one,
                        anchorMin = Vector2.zero,
                        anchorMax = Vector2.one,
                        anchoredPosition = new Vector2(20f, -30f),
                        sizeDelta = new Vector2(-100f, -80f),
                        pivot = new Vector2(0.25f, 0.75f),
                    };
                    SerializedProperty stateProp = widget.FindPropertyRelative("phonePortrait");
                    ResponsiveRectStateEditorUtil.WriteState(
                        stateProp,
                        state);
                    so.ApplyModifiedPropertiesWithoutUndo();
                    so.Update();

                    Vector2 currentSize = new Vector2(500f, 750f);
                    Vector2 ratio = state.ComputeLayoutRatio(
                        currentSize,
                        ResponsiveDevice.FormFactor.Phone);
                    Vector2 scale = state.ComputeAppliedLocalScale(
                        currentSize,
                        ResponsiveDevice.FormFactor.Phone);
                    RectTransform rt = target.GetComponent<RectTransform>();
                    state.Apply(rt, ratio, scale);

                    Assert.IsTrue(ResponsiveRectStateEditorUtil.MatchesLayout(
                        stateProp,
                        rt,
                        ratio,
                        scale));
                }
                finally
                {
                    Object.DestroyImmediate(parent);
                }
            });
        }

        [Test]
        public void Copy_Paste_CopiesCameraValues()
        {
            WithCamera((so, layout) =>
            {
                SerializedProperty phone = layout.FindPropertyRelative("phoneLandscape");
                phone.FindPropertyRelative("enabled").boolValue = true;
                phone.FindPropertyRelative("applyLocalPosition").boolValue = true;
                phone.FindPropertyRelative("localPosition").vector3Value = new Vector3(1f, 2f, 3f);
                phone.FindPropertyRelative("orthographicSize").floatValue = 9f;
                phone.FindPropertyRelative("scaleProjection").boolValue = true;
                phone.FindPropertyRelative("viewport").rectValue = new Rect(0.1f, 0.2f, 0.3f, 0.4f);
                so.ApplyModifiedPropertiesWithoutUndo();

                SerializedProperty pad = layout.FindPropertyRelative("padLandscape");
                ResponsiveUiSlotClipboard.Clear();
                ResponsiveUiSlotClipboard.Copy(phone, ResponsiveUiSlotClipboard.Kind.Camera);
                Assert.IsTrue(ResponsiveUiSlotClipboard.Paste(pad, ResponsiveUiSlotClipboard.Kind.Camera));

                ResponsiveCameraState copied = ResponsiveCameraEditorUtil.ReadState(pad);
                Assert.AreEqual(new Vector3(1f, 2f, 3f), copied.localPosition);
                Assert.AreEqual(9f, copied.orthographicSize);
                Assert.IsTrue(copied.scaleProjection);
                Assert.AreEqual(new Rect(0.1f, 0.2f, 0.3f, 0.4f), copied.viewport);
            });
        }

        [Test]
        public void Reset_KeepsCameraViewportSizeAndOrtho()
        {
            WithCamera((so, layout) =>
            {
                SerializedProperty pad = layout.FindPropertyRelative("padLandscape");
                pad.FindPropertyRelative("enabled").boolValue = true;
                pad.FindPropertyRelative("applyLocalPosition").boolValue = true;
                pad.FindPropertyRelative("localPosition").vector3Value = new Vector3(7f, 8f, 9f);
                pad.FindPropertyRelative("orthographicSize").floatValue = 11f;
                pad.FindPropertyRelative("scaleProjection").boolValue = true;
                pad.FindPropertyRelative("viewport").rectValue = new Rect(0.1f, 0.2f, 0.3f, 0.4f);
                so.ApplyModifiedPropertiesWithoutUndo();

                ResponsiveUiSlotClipboard.Reset(pad, ResponsiveUiSlotClipboard.Kind.Camera);
                ResponsiveCameraState reset = ResponsiveCameraEditorUtil.ReadState(pad);
                Assert.IsTrue(reset.enabled);
                Assert.IsFalse(reset.applyLocalPosition);
                Assert.AreEqual(Vector3.zero, reset.localPosition);
                Assert.AreEqual(11f, reset.orthographicSize);
                Assert.IsFalse(reset.scaleProjection);
                Assert.AreEqual(0.3f, reset.viewport.width, 1e-4f);
                Assert.AreEqual(0.4f, reset.viewport.height, 1e-4f);
            });
        }

        [Test]
        public void Copy_Paste_CopiesScrollMetrics()
        {
            var host = ScriptableObject.CreateInstance<ResponsiveSlotSeedScrollHost>();
            try
            {
                var so = new SerializedObject(host);
                SerializedProperty layout = so.FindProperty("layout");
                SerializedProperty phone = layout.FindPropertyRelative("phonePortrait");
                phone.FindPropertyRelative("enabled").boolValue = true;
                phone.FindPropertyRelative("cellInterval").floatValue = 0.18f;
                phone.FindPropertyRelative("scrollOffset").floatValue = 0.42f;
                so.ApplyModifiedPropertiesWithoutUndo();

                SerializedProperty pad = layout.FindPropertyRelative("padPortrait");
                ResponsiveUiSlotClipboard.Clear();
                ResponsiveUiSlotClipboard.Copy(phone, ResponsiveUiSlotClipboard.Kind.Scroll);
                Assert.IsTrue(ResponsiveUiSlotClipboard.Paste(pad, ResponsiveUiSlotClipboard.Kind.Scroll));
                Assert.AreEqual(0.18f, pad.FindPropertyRelative("cellInterval").floatValue);
                Assert.AreEqual(0.42f, pad.FindPropertyRelative("scrollOffset").floatValue);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void Reset_KeepsScrollEnabled_RestoresDefaultMetrics()
        {
            var host = ScriptableObject.CreateInstance<ResponsiveSlotSeedScrollHost>();
            try
            {
                var so = new SerializedObject(host);
                SerializedProperty pad = so.FindProperty("layout").FindPropertyRelative("padPortrait");
                pad.FindPropertyRelative("enabled").boolValue = true;
                pad.FindPropertyRelative("cellInterval").floatValue = 0.18f;
                pad.FindPropertyRelative("scrollOffset").floatValue = 0.42f;
                so.ApplyModifiedPropertiesWithoutUndo();

                ResponsiveUiSlotClipboard.Reset(pad, ResponsiveUiSlotClipboard.Kind.Scroll);
                Assert.IsTrue(pad.FindPropertyRelative("enabled").boolValue);
                Assert.AreEqual(ResponsiveScrollState.DefaultCellInterval, pad.FindPropertyRelative("cellInterval").floatValue);
                Assert.AreEqual(ResponsiveScrollState.DefaultScrollOffset, pad.FindPropertyRelative("scrollOffset").floatValue);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void Paste_WithoutCopy_ReturnsFalse()
        {
            WithWidget((_, widget) =>
            {
                ResponsiveUiSlotClipboard.Clear();
                Assert.IsFalse(ResponsiveUiSlotClipboard.CanPaste(ResponsiveUiSlotClipboard.Kind.Widget));
                Assert.IsFalse(ResponsiveUiSlotClipboard.Paste(
                    widget.FindPropertyRelative("padLandscape"),
                    ResponsiveUiSlotClipboard.Kind.Widget));
            });
        }

        [Test]
        public void Copy_DoesNotWriteOtherSlots()
        {
            WithWidget((so, widget) =>
            {
                WriteWidgetLayout(widget, "phoneLandscape", PhoneSample(landscape: true), enabled: true);
                so.ApplyModifiedPropertiesWithoutUndo();

                SerializedProperty pad = widget.FindPropertyRelative("padLandscape");
                ResponsiveRectState before = ResponsiveRectStateEditorUtil.ReadState(pad);
                ResponsiveUiSlotClipboard.Clear();
                ResponsiveUiSlotClipboard.Copy(
                    widget.FindPropertyRelative("phoneLandscape"),
                    ResponsiveUiSlotClipboard.Kind.Widget);
                AssertLayoutEquals(before, ResponsiveRectStateEditorUtil.ReadState(pad));
            });
        }

        [Test]
        public void CanPaste_IsSeparatePerKind()
        {
            WithWidget((_, widget) =>
            {
                ResponsiveUiSlotClipboard.Clear();
                ResponsiveUiSlotClipboard.Copy(
                    widget.FindPropertyRelative("phonePortrait"),
                    ResponsiveUiSlotClipboard.Kind.Widget);
                Assert.IsTrue(ResponsiveUiSlotClipboard.CanPaste(ResponsiveUiSlotClipboard.Kind.Widget));
                Assert.IsFalse(ResponsiveUiSlotClipboard.CanPaste(ResponsiveUiSlotClipboard.Kind.Camera));
                Assert.IsFalse(ResponsiveUiSlotClipboard.CanPaste(ResponsiveUiSlotClipboard.Kind.Scroll));
            });
        }

    #endregion

    #region Helpers

        static ResponsiveRectState PhoneSample(bool landscape) =>
            new ResponsiveRectState
            {
                enabled = true,
                scaleFromDesignCanvas = true,
                designScaleMode = ResponsiveDesignScaleMode.Width,
                designCanvasSize = ResponsiveCatalogDevices.PhonePortrait,
                defaultScale = Vector2.one,
                anchorMin = landscape ? new Vector2(0.1f, 0.2f) : new Vector2(0.05f, 0.15f),
                anchorMax = landscape ? new Vector2(0.9f, 0.8f) : new Vector2(0.95f, 0.85f),
                anchoredPosition = landscape ? new Vector2(12f, 34f) : new Vector2(56f, 78f),
                sizeDelta = landscape ? new Vector2(100f, 200f) : new Vector2(300f, 400f),
                pivot = landscape ? new Vector2(0.25f, 0.75f) : new Vector2(0.5f, 0.5f),
            };

        static void WriteWidgetLayout(
            SerializedProperty widget,
            string slotName,
            ResponsiveRectState state,
            bool enabled)
        {
            state.enabled = enabled;
            ResponsiveRectStateEditorUtil.WriteState(
                widget.FindPropertyRelative(slotName),
                state);
        }

        static void AssertLayoutEquals(
            in ResponsiveRectState expected,
            in ResponsiveRectState actual,
            string message = null)
        {
            Assert.AreEqual(expected.anchorMin, actual.anchorMin, message);
            Assert.AreEqual(expected.anchorMax, actual.anchorMax, message);
            Assert.AreEqual(expected.anchoredPosition, actual.anchoredPosition, message);
            Assert.AreEqual(expected.sizeDelta, actual.sizeDelta, message);
            Assert.AreEqual(expected.pivot, actual.pivot, message);
            Assert.AreEqual(expected.scaleFromDesignCanvas, actual.scaleFromDesignCanvas, message);
            Assert.AreEqual(expected.defaultScale, actual.defaultScale, message);
        }

        static void WithWidget(System.Action<SerializedObject, SerializedProperty> body) =>
            WithWidgetList(1, widgets => body(widgets.serializedObject, widgets.GetArrayElementAtIndex(0)));

        static void WithWidgetList(int count, System.Action<SerializedProperty> body)
        {
            var go = new GameObject("ResponsiveUISeedTest");
            try
            {
                var controller = go.AddComponent<ResponsiveUiController>();
                var so = new SerializedObject(controller);
                SerializedProperty widgets = so.FindProperty("widgets");
                widgets.arraySize = count;
                so.ApplyModifiedPropertiesWithoutUndo();
                so.Update();
                body(so.FindProperty("widgets"));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        /// <summary>
        /// Canvas hub (with a Canvas Scaler) plus a child controller holding one widget row.
        /// </summary>
        static void WithHubWidget(System.Action<SerializedObject, SerializedObject, SerializedProperty> body)
        {
            var go = new GameObject("ResponsiveUIHubSeedTest", typeof(Canvas), typeof(CanvasScaler));
            try
            {
                var hub = new SerializedObject(go.AddComponent<ResponsiveUI>());
                hub.FindProperty("canvasScaler").objectReferenceValue = go.GetComponent<CanvasScaler>();
                hub.ApplyModifiedPropertiesWithoutUndo();

                var child = new GameObject("Controller", typeof(RectTransform));
                child.transform.SetParent(go.transform, false);
                var so = new SerializedObject(child.AddComponent<ResponsiveUiController>());
                so.FindProperty("widgets").arraySize = 1;
                so.ApplyModifiedPropertiesWithoutUndo();
                so.Update();
                body(hub, so, so.FindProperty("widgets").GetArrayElementAtIndex(0));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        static void WithCamera(System.Action<SerializedObject, SerializedProperty> body)
        {
            var go = new GameObject("ResponsiveCameraSeedTest");
            try
            {
                var camera = go.AddComponent<ResponsiveCamera>();
                var so = new SerializedObject(camera);
                SerializedProperty cameras = so.FindProperty("cameras");
                cameras.arraySize = 1;
                so.ApplyModifiedPropertiesWithoutUndo();
                so.Update();
                body(so, so.FindProperty("cameras").GetArrayElementAtIndex(0));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

    #endregion
    }

    class ResponsiveSlotSeedScrollHost : ScriptableObject
    {
        public ResponsiveScrollLayout layout;

        public void Apply()
        {
        }
    }
}
