using NUnit.Framework;
using UnityEngine;
using FDPlus.ResponsiveUI.Editor;
using FDPlus.ResponsiveUI;
using FDPlus.ResponsiveUI.Shared.Editor;

namespace FDPlus.ResponsiveUI.Tests
{
    public class ResponsiveWidgetSlotResolverTests
    {
        [TestCase(
            ResponsiveDevice.FormFactor.Phone,
            ResponsiveDevice.Orientation.Portrait,
            1)]
        [TestCase(
            ResponsiveDevice.FormFactor.Phone,
            ResponsiveDevice.Orientation.Landscape,
            2)]
        [TestCase(
            ResponsiveDevice.FormFactor.Pad,
            ResponsiveDevice.Orientation.Landscape,
            4)]
        [TestCase(
            ResponsiveDevice.FormFactor.FoldHalfOpened,
            ResponsiveDevice.Orientation.Portrait,
            3)]
        [TestCase(
            ResponsiveDevice.FormFactor.FoldHalfOpened,
            ResponsiveDevice.Orientation.Landscape,
            2)]
        [TestCase(
            ResponsiveDevice.FormFactor.FlipHalfOpened,
            ResponsiveDevice.Orientation.Landscape,
            4)]
        [TestCase(
            ResponsiveDevice.FormFactor.FoldPhoneCover,
            ResponsiveDevice.Orientation.Portrait,
            2)]
        [TestCase(
            ResponsiveDevice.FormFactor.FoldPhoneCover,
            ResponsiveDevice.Orientation.Landscape,
            4)]
        [TestCase(
            ResponsiveDevice.FormFactor.FlipPhoneCover,
            ResponsiveDevice.Orientation.Portrait,
            2)]
        [TestCase(
            ResponsiveDevice.FormFactor.FlipPhoneCover,
            ResponsiveDevice.Orientation.Landscape,
            4)]
        [TestCase(
            ResponsiveDevice.FormFactor.FoldCoverHalfOpened,
            ResponsiveDevice.Orientation.Portrait,
            3)]
        [TestCase(
            ResponsiveDevice.FormFactor.FoldCoverHalfOpened,
            ResponsiveDevice.Orientation.Landscape,
            2)]
        [TestCase(
            ResponsiveDevice.FormFactor.iPhoneDuoSeated,
            ResponsiveDevice.Orientation.Portrait,
            5)]
        [TestCase(
            ResponsiveDevice.FormFactor.iPhoneDuoSeated,
            ResponsiveDevice.Orientation.Landscape,
            5)]
        public void CandidateCount_MatchesFallbackContract(
            ResponsiveDevice.FormFactor form,
            ResponsiveDevice.Orientation orientation,
            int expectedCount)
        {
            var key = new ResponsiveDevice.LayoutKey(form, orientation);
            int count = 0;
            for (int i = 0; i < ResponsiveWidgetSlotResolver.MaxCandidates; i++)
            {
                if (ResponsiveWidgetSlotResolver.GetCandidate(
                        key, i, out _, out _))
                    count++;
            }

            Assert.AreEqual(expectedCount, count);
        }

        // Fold / Fold Cover Half Opened slots name the posture: Landscape is the tall screen,
        // Portrait the wide one. A disabled slot skips the form's other slot (opposite shape)
        // and falls back to Phone by screen shape, never Pad.
        [TestCase(ResponsiveDevice.FormFactor.FoldHalfOpened)]
        [TestCase(ResponsiveDevice.FormFactor.FoldCoverHalfOpened)]
        public void FoldHalfOpenLandscape_TallScreen_FallsBackToPhonePortrait(
            ResponsiveDevice.FormFactor fold)
        {
            var key = new ResponsiveDevice.LayoutKey(fold, ResponsiveDevice.Orientation.Landscape);

            AssertCandidate(key, 0, fold, ResponsiveDevice.Orientation.Landscape);
            AssertCandidate(
                key, 2,
                ResponsiveDevice.FormFactor.Phone,
                ResponsiveDevice.Orientation.Portrait);
            foreach (int i in new[] { 1, 3, 4 })
                Assert.IsFalse(ResponsiveWidgetSlotResolver.GetCandidate(key, i, out _, out _), "#" + i);
        }

        [TestCase(ResponsiveDevice.FormFactor.FoldHalfOpened)]
        [TestCase(ResponsiveDevice.FormFactor.FoldCoverHalfOpened)]
        public void FoldHalfOpenPortrait_WideScreen_FallsBackToPhoneLandscapeThenPortrait(
            ResponsiveDevice.FormFactor fold)
        {
            var key = new ResponsiveDevice.LayoutKey(fold, ResponsiveDevice.Orientation.Portrait);

            AssertCandidate(key, 0, fold, ResponsiveDevice.Orientation.Portrait);
            AssertCandidate(
                key, 2,
                ResponsiveDevice.FormFactor.Phone,
                ResponsiveDevice.Orientation.Landscape);
            AssertCandidate(
                key, 3,
                ResponsiveDevice.FormFactor.Phone,
                ResponsiveDevice.Orientation.Portrait);
            foreach (int i in new[] { 1, 4 })
                Assert.IsFalse(ResponsiveWidgetSlotResolver.GetCandidate(key, i, out _, out _), "#" + i);
        }

        [Test]
        public void IphoneDuoPortrait_TallScreen_FallsBackToPadThenPhoneByShape()
        {
            var key = new ResponsiveDevice.LayoutKey(
                ResponsiveDevice.FormFactor.iPhoneDuoSeated,
                ResponsiveDevice.Orientation.Portrait);

            AssertCandidate(
                key, 0,
                ResponsiveDevice.FormFactor.iPhoneDuoSeated,
                ResponsiveDevice.Orientation.Portrait);
            AssertCandidate(
                key, 1,
                ResponsiveDevice.FormFactor.Pad,
                ResponsiveDevice.Orientation.Portrait);
            AssertCandidate(
                key, 2,
                ResponsiveDevice.FormFactor.Pad,
                ResponsiveDevice.Orientation.Landscape);
            AssertCandidate(
                key, 3,
                ResponsiveDevice.FormFactor.Phone,
                ResponsiveDevice.Orientation.Portrait);
            AssertCandidate(
                key, 4,
                ResponsiveDevice.FormFactor.Phone,
                ResponsiveDevice.Orientation.Landscape);
            Assert.IsFalse(ResponsiveWidgetSlotResolver.GetCandidate(key, 5, out _, out _));
        }

        [Test]
        public void IphoneDuoSeatedLandscape_FallsBackToPadThenPhoneByShape()
        {
            var key = new ResponsiveDevice.LayoutKey(
                ResponsiveDevice.FormFactor.iPhoneDuoSeated,
                ResponsiveDevice.Orientation.Landscape);

            AssertCandidate(
                key, 0,
                ResponsiveDevice.FormFactor.iPhoneDuoSeated,
                ResponsiveDevice.Orientation.Landscape);
            AssertCandidate(
                key, 1,
                ResponsiveDevice.FormFactor.Pad,
                ResponsiveDevice.Orientation.Landscape);
            AssertCandidate(
                key, 2,
                ResponsiveDevice.FormFactor.Pad,
                ResponsiveDevice.Orientation.Portrait);
            AssertCandidate(
                key, 3,
                ResponsiveDevice.FormFactor.Phone,
                ResponsiveDevice.Orientation.Landscape);
            AssertCandidate(
                key, 4,
                ResponsiveDevice.FormFactor.Phone,
                ResponsiveDevice.Orientation.Portrait);
        }

        [Test]
        public void IphoneDuo_CandidatesNeverUseOtherDuoSlot()
        {
            foreach (ResponsiveDevice.Orientation orientation in new[]
                     {
                         ResponsiveDevice.Orientation.Portrait,
                         ResponsiveDevice.Orientation.Landscape,
                     })
            {
                var key = new ResponsiveDevice.LayoutKey(
                    ResponsiveDevice.FormFactor.iPhoneDuoSeated, orientation);
                for (int i = 1; i < ResponsiveWidgetSlotResolver.MaxCandidates; i++)
                {
                    if (!ResponsiveWidgetSlotResolver.GetCandidate(
                            key, i, out var form, out _))
                        continue;
                    Assert.AreNotEqual(
                        ResponsiveDevice.FormFactor.iPhoneDuoSeated, form, key + " #" + i);
                }
            }
        }

        [Test]
        public void RuntimeResolver_DisabledIphoneDuo_UsesPadNotFoldCover()
        {
            var widget = new ResponsiveWidgetLayout
            {
                iPhoneDuoSeatedPortrait = new ResponsiveRectState { enabled = false },
                iPhoneDuoSeatedLandscape = new ResponsiveRectState { enabled = true },
                foldCoverHalfOpenedLandscape = new ResponsiveRectState { enabled = true },
                padLandscape = new ResponsiveRectState { enabled = true },
                padPortrait = new ResponsiveRectState { enabled = true },
                phoneLandscape = new ResponsiveRectState { enabled = true },
            };

            // Duo Portrait is the tall laptop screen: same shape as Pad Portrait.
            bool resolved = widget.ResolveActiveSlot(
                new ResponsiveDevice.LayoutKey(
                    ResponsiveDevice.FormFactor.iPhoneDuoSeated,
                    ResponsiveDevice.Orientation.Portrait),
                out string propertyName,
                out ResponsiveDevice.FormFactor sourceForm,
                out ResponsiveDevice.Orientation sourceOrientation,
                out _);

            Assert.IsTrue(resolved);
            Assert.AreEqual("padPortrait", propertyName);
            Assert.AreEqual(ResponsiveDevice.FormFactor.Pad, sourceForm);
            Assert.AreEqual(ResponsiveDevice.Orientation.Portrait, sourceOrientation);

            // Duo Landscape is the wide book screen: same shape as Pad Landscape.
            widget.iPhoneDuoSeatedLandscape = new ResponsiveRectState { enabled = false };
            widget.ResolveActiveSlot(
                new ResponsiveDevice.LayoutKey(
                    ResponsiveDevice.FormFactor.iPhoneDuoSeated,
                    ResponsiveDevice.Orientation.Landscape),
                out propertyName,
                out sourceForm,
                out _);
            Assert.AreEqual("padLandscape", propertyName);
            Assert.AreEqual(ResponsiveDevice.FormFactor.Pad, sourceForm);

            widget.iPhoneDuoSeatedPortrait = new ResponsiveRectState { enabled = true };
            widget.ResolveActiveSlot(
                new ResponsiveDevice.LayoutKey(
                    ResponsiveDevice.FormFactor.iPhoneDuoSeated,
                    ResponsiveDevice.Orientation.Portrait),
                out propertyName,
                out sourceForm,
                out _);
            Assert.AreEqual("iPhoneDuoSeatedPortrait", propertyName);
            Assert.AreEqual(ResponsiveDevice.FormFactor.iPhoneDuoSeated, sourceForm);
        }

        [Test]
        public void FoldPhoneCoverLandscape_CandidatesFallBackToPhone()
        {
            var key = new ResponsiveDevice.LayoutKey(
                ResponsiveDevice.FormFactor.FoldPhoneCover,
                ResponsiveDevice.Orientation.Landscape);

            AssertCandidate(
                key, 0,
                ResponsiveDevice.FormFactor.FoldPhoneCover,
                ResponsiveDevice.Orientation.Landscape);
            AssertCandidate(
                key, 1,
                ResponsiveDevice.FormFactor.FoldPhoneCover,
                ResponsiveDevice.Orientation.Portrait);
            AssertCandidate(
                key, 2,
                ResponsiveDevice.FormFactor.Phone,
                ResponsiveDevice.Orientation.Landscape);
            AssertCandidate(
                key, 3,
                ResponsiveDevice.FormFactor.Phone,
                ResponsiveDevice.Orientation.Portrait);
        }

        [Test]
        public void FlipPhoneCoverLandscape_CandidatesFallBackToPhone()
        {
            var key = new ResponsiveDevice.LayoutKey(
                ResponsiveDevice.FormFactor.FlipPhoneCover,
                ResponsiveDevice.Orientation.Landscape);

            AssertCandidate(
                key, 0,
                ResponsiveDevice.FormFactor.FlipPhoneCover,
                ResponsiveDevice.Orientation.Landscape);
            AssertCandidate(
                key, 1,
                ResponsiveDevice.FormFactor.FlipPhoneCover,
                ResponsiveDevice.Orientation.Portrait);
            AssertCandidate(
                key, 2,
                ResponsiveDevice.FormFactor.Phone,
                ResponsiveDevice.Orientation.Landscape);
            AssertCandidate(
                key, 3,
                ResponsiveDevice.FormFactor.Phone,
                ResponsiveDevice.Orientation.Portrait);
        }

        [Test]
        public void RuntimeResolver_UsesPhoneLandscapeAfterDisabledFoldSlots()
        {
            var widget = new ResponsiveWidgetLayout
            {
                foldHalfOpenedPortrait = new ResponsiveRectState { enabled = false },
                foldHalfOpenedLandscape = new ResponsiveRectState { enabled = false },
                phonePortrait = new ResponsiveRectState { enabled = true },
                phoneLandscape = new ResponsiveRectState { enabled = true },
            };

            // Fold Half Opened Portrait is the wide screen: Phone Landscape first.
            bool resolved = widget.ResolveActiveSlot(
                new ResponsiveDevice.LayoutKey(
                    ResponsiveDevice.FormFactor.FoldHalfOpened,
                    ResponsiveDevice.Orientation.Portrait),
                out string propertyName,
                out ResponsiveDevice.FormFactor sourceForm,
                out _);

            Assert.IsTrue(resolved);
            Assert.AreEqual("phoneLandscape", propertyName);
            Assert.AreEqual(ResponsiveDevice.FormFactor.Phone, sourceForm);
        }

        [Test]
        public void RuntimeResolver_LandscapeFallback_ReportsPortraitOrientation()
        {
            var widget = new ResponsiveWidgetLayout
            {
                phonePortrait = new ResponsiveRectState { enabled = true },
                phoneLandscape = new ResponsiveRectState { enabled = false },
            };

            bool resolved = widget.ResolveActiveSlot(
                new ResponsiveDevice.LayoutKey(
                    ResponsiveDevice.FormFactor.Phone,
                    ResponsiveDevice.Orientation.Landscape),
                out string propertyName,
                out ResponsiveDevice.FormFactor sourceForm,
                out ResponsiveDevice.Orientation sourceOrientation,
                out _);

            Assert.IsTrue(resolved);
            Assert.AreEqual("phonePortrait", propertyName);
            Assert.AreEqual(ResponsiveDevice.FormFactor.Phone, sourceForm);
            Assert.AreEqual(ResponsiveDevice.Orientation.Portrait, sourceOrientation);
        }

        [Test]
        public void RuntimeResolver_UsesPhoneAfterDisabledFoldPhoneCoverSlots()
        {
            var widget = new ResponsiveWidgetLayout
            {
                foldPhoneCoverPortrait = new ResponsiveRectState { enabled = false },
                foldPhoneCoverLandscape = new ResponsiveRectState { enabled = false },
                phonePortrait = new ResponsiveRectState { enabled = true },
                phoneLandscape = new ResponsiveRectState { enabled = true },
            };

            bool resolved = widget.ResolveActiveSlot(
                new ResponsiveDevice.LayoutKey(
                    ResponsiveDevice.FormFactor.FoldPhoneCover,
                    ResponsiveDevice.Orientation.Landscape),
                out string propertyName,
                out ResponsiveDevice.FormFactor sourceForm,
                out _);

            Assert.IsTrue(resolved);
            Assert.AreEqual("phoneLandscape", propertyName);
            Assert.AreEqual(ResponsiveDevice.FormFactor.Phone, sourceForm);
        }

        [Test]
        public void RuntimeResolver_UsesPhoneAfterDisabledFlipPhoneCoverSlots()
        {
            var widget = new ResponsiveWidgetLayout
            {
                flipPhoneCoverPortrait = new ResponsiveRectState { enabled = false },
                flipPhoneCoverLandscape = new ResponsiveRectState { enabled = false },
                phonePortrait = new ResponsiveRectState { enabled = true },
                phoneLandscape = new ResponsiveRectState { enabled = true },
            };

            bool resolved = widget.ResolveActiveSlot(
                new ResponsiveDevice.LayoutKey(
                    ResponsiveDevice.FormFactor.FlipPhoneCover,
                    ResponsiveDevice.Orientation.Landscape),
                out string propertyName,
                out ResponsiveDevice.FormFactor sourceForm,
                out _);

            Assert.IsTrue(resolved);
            Assert.AreEqual("phoneLandscape", propertyName);
            Assert.AreEqual(ResponsiveDevice.FormFactor.Phone, sourceForm);
        }

        [Test]
        public void RuntimeResolver_UsesPhoneAfterDisabledPadSlots()
        {
            var widget = new ResponsiveWidgetLayout
            {
                padPortrait = new ResponsiveRectState { enabled = false },
                padLandscape = new ResponsiveRectState { enabled = false },
                phonePortrait = new ResponsiveRectState { enabled = true },
                phoneLandscape = new ResponsiveRectState { enabled = true },
            };

            bool resolved = widget.ResolveActiveSlot(
                new ResponsiveDevice.LayoutKey(
                    ResponsiveDevice.FormFactor.Pad,
                    ResponsiveDevice.Orientation.Landscape),
                out string propertyName,
                out ResponsiveDevice.FormFactor sourceForm,
                out _);

            Assert.IsTrue(resolved);
            Assert.AreEqual("phoneLandscape", propertyName);
            Assert.AreEqual(ResponsiveDevice.FormFactor.Phone, sourceForm);
        }

        [Test]
        public void RuntimeApply_PhoneFallback_RetainsPhoneDesignSpace()
        {
            var go = new GameObject("FoldFallbackWidget", typeof(RectTransform));
            try
            {
                RectTransform target = go.GetComponent<RectTransform>();
                var phone = ResponsiveRectState.CreateDefault();
                phone.scaleFromDesignCanvas = true;
                phone.designCanvasSize = Vector2.zero;
                phone.defaultScale = Vector2.one;
                phone.anchoredPosition = new Vector2(100f, 50f);

                var widget = new ResponsiveWidgetLayout
                {
                    target = target,
                    phoneLandscape = phone,
                    foldHalfOpenedPortrait = new ResponsiveRectState { enabled = false },
                };

                // Fold Half Opened Portrait is the wide screen, so it falls back to Phone Landscape.
                Vector2 foldLandscape = new Vector2(
                    ResponsiveRectState.BuiltinFoldDesignCanvasSize.y,
                    ResponsiveRectState.BuiltinFoldDesignCanvasSize.x);
                widget.Apply(
                    new ResponsiveDevice.LayoutKey(
                        ResponsiveDevice.FormFactor.FoldHalfOpened,
                        ResponsiveDevice.Orientation.Portrait),
                    foldLandscape);

                Vector2 expectedRatio = phone.ComputeLayoutRatio(
                    foldLandscape,
                    ResponsiveDevice.FormFactor.Phone,
                    ResponsiveDevice.Orientation.Landscape);
                Assert.AreEqual(expectedRatio.x, target.localScale.x, 1e-4f);
                Assert.AreEqual(expectedRatio.y, target.localScale.y, 1e-4f);
                Assert.AreEqual(
                    phone.anchoredPosition.x * expectedRatio.x,
                    target.anchoredPosition.x,
                    1e-3f);
                Assert.AreEqual(
                    phone.anchoredPosition.y * expectedRatio.y,
                    target.anchoredPosition.y,
                    1e-3f);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void RuntimeApply_LandscapeToPortraitFallback_OrientsLiveSize()
        {
            var go = new GameObject("PortraitFallbackWidget", typeof(RectTransform));
            try
            {
                RectTransform target = go.GetComponent<RectTransform>();
                var portrait = ResponsiveRectState.CreateDefault();
                portrait.scaleFromDesignCanvas = true;
                portrait.designScaleMode = ResponsiveDesignScaleMode.Auto;
                portrait.designCanvasSize = Vector2.zero;
                portrait.defaultScale = Vector2.one;

                var widget = new ResponsiveWidgetLayout
                {
                    target = target,
                    phonePortrait = portrait,
                    phoneLandscape = new ResponsiveRectState { enabled = false },
                };

                var landscapeLive = new Vector2(2000f, 1000f);
                widget.Apply(
                    new ResponsiveDevice.LayoutKey(
                        ResponsiveDevice.FormFactor.Phone,
                        ResponsiveDevice.Orientation.Landscape),
                    landscapeLive);

                Vector2 expected = portrait.ComputeLayoutRatio(
                    new Vector2(1000f, 2000f),
                    ResponsiveDevice.FormFactor.Phone,
                    ResponsiveDevice.Orientation.Portrait);
                Vector2 unoriented = portrait.ComputeLayoutRatio(
                    landscapeLive,
                    ResponsiveDevice.FormFactor.Phone,
                    ResponsiveDevice.Orientation.Portrait);
                Assert.Greater(Mathf.Abs(expected.x - unoriented.x), 0.05f);
                Assert.AreEqual(expected.x, target.localScale.x, 1e-4f);
                Assert.AreEqual(expected.y, target.localScale.y, 1e-4f);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void RuntimeApply_IphoneDuoPortrait_UsesTallDesignSpace()
        {
            var go = new GameObject("DuoLaptopWidget", typeof(RectTransform));
            try
            {
                RectTransform target = go.GetComponent<RectTransform>();
                var duo = ResponsiveRectState.CreateDefault();
                duo.scaleFromDesignCanvas = true;
                duo.designScaleMode = ResponsiveDesignScaleMode.IndependentXY;
                duo.designCanvasSize = Vector2.zero;
                duo.defaultScale = Vector2.one;

                var widget = new ResponsiveWidgetLayout
                {
                    target = target,
                    iPhoneDuoSeatedPortrait = duo,
                };

                // Laptop pose: the Duo Portrait slot is the tall inner screen.
                Vector2 tall = ResponsiveUI.OrientSize(
                    ResponsiveRectState.GetDefaultDesignCanvasSize(
                        ResponsiveDevice.FormFactor.iPhoneDuoSeated),
                    ResponsiveDevice.Orientation.Portrait);
                widget.Apply(
                    new ResponsiveDevice.LayoutKey(
                        ResponsiveDevice.FormFactor.iPhoneDuoSeated,
                        ResponsiveDevice.Orientation.Portrait),
                    tall);

                Assert.AreEqual(1f, target.localScale.x, 1e-4f);
                Assert.AreEqual(1f, target.localScale.y, 1e-4f);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void RuntimeApply_DisabledIphoneDuoLandscape_PadLandscapeKeepsWideLiveSize()
        {
            var go = new GameObject("DuoBookFallbackWidget", typeof(RectTransform));
            try
            {
                RectTransform target = go.GetComponent<RectTransform>();
                var pad = ResponsiveRectState.CreateDefault();
                pad.scaleFromDesignCanvas = true;
                pad.designScaleMode = ResponsiveDesignScaleMode.IndependentXY;
                pad.designCanvasSize = Vector2.zero;
                pad.defaultScale = Vector2.one;

                var widget = new ResponsiveWidgetLayout
                {
                    target = target,
                    padLandscape = pad,
                    padPortrait = new ResponsiveRectState { enabled = false },
                    iPhoneDuoSeatedLandscape = new ResponsiveRectState { enabled = false },
                };

                // Book pose: the Duo Landscape slot is the wide inner screen, same shape as
                // Pad Landscape, so the live size is not re-oriented.
                var wide = new Vector2(2853f, 2007f);
                widget.Apply(
                    new ResponsiveDevice.LayoutKey(
                        ResponsiveDevice.FormFactor.iPhoneDuoSeated,
                        ResponsiveDevice.Orientation.Landscape),
                    wide);

                Vector2 expected = pad.ComputeLayoutRatio(
                    wide,
                    ResponsiveDevice.FormFactor.Pad,
                    ResponsiveDevice.Orientation.Landscape);
                Vector2 reoriented = pad.ComputeLayoutRatio(
                    new Vector2(wide.y, wide.x),
                    ResponsiveDevice.FormFactor.Pad,
                    ResponsiveDevice.Orientation.Landscape);
                Assert.Greater(Mathf.Abs(expected.x - reoriented.x), 0.05f);
                Assert.AreEqual(expected.x, target.localScale.x, 1e-4f);
                Assert.AreEqual(expected.y, target.localScale.y, 1e-4f);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void MultipleHandlers_DoNotOverwriteGlobalDeviceOverrides()
        {
            ResponsiveDevice.SetFormOverride(ResponsiveDevice.FormFactor.Pad);
            ResponsiveDevice.SetOrientationOverride(ResponsiveDevice.Orientation.Landscape);
            var first = new GameObject("ResponsiveHandlerA");
            var second = new GameObject("ResponsiveHandlerB");
            try
            {
                first.AddComponent<ResponsiveUiHandler>().Apply();
                second.AddComponent<ResponsiveUiHandler>().Apply();

                Assert.AreEqual(
                    ResponsiveDevice.FormFactor.Pad,
                    ResponsiveDevice.CurrentFormFactor);
                Assert.AreEqual(
                    ResponsiveDevice.Orientation.Landscape,
                    ResponsiveDevice.CurrentOrientation);
            }
            finally
            {
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(second);
                ResponsiveDevice.ClearOverride();
            }
        }

        [TestCase("★ Fold / Galaxy Z Fold8-like", "Galaxy Z Fold8-like")]
        [TestCase("● Phone / iPhone 17 Pro Max", "iPhone 17 Pro Max")]
        [TestCase("\u0001iPad Pro 13 M5", "iPad Pro 13 M5")]
        [TestCase("◆ Phone / Reference", "Reference")]
        [TestCase("\u0001Fold Cover / Galaxy Z Fold8-like", "Galaxy Z Fold8-like")]
        [TestCase("\u0001Fold / Galaxy Z Fold7-like", "Galaxy Z Fold7-like")]
        [TestCase("\u0001Flip / Galaxy Z Flip8-like", "Galaxy Z Flip8-like")]
        public void SimulatorName_NormalizesToolkitPrefixes(string value, string expected)
        {
            Assert.AreEqual(expected, ResponsiveUiSimulatorName.Normalize(value));
        }

        static void AssertCandidate(
            ResponsiveDevice.LayoutKey key,
            int index,
            ResponsiveDevice.FormFactor expectedForm,
            ResponsiveDevice.Orientation expectedOrientation)
        {
            Assert.IsTrue(ResponsiveWidgetSlotResolver.GetCandidate(
                key, index, out var form, out var orientation));
            Assert.AreEqual(expectedForm, form);
            Assert.AreEqual(expectedOrientation, orientation);
        }
    }
}
