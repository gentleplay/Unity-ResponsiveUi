using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using FDPlus.ResponsiveUI;

using static FDPlus.ResponsiveUI.Tests.ResponsiveEditorTestUtil;

namespace FDPlus.ResponsiveUI.Tests
{
    /// <summary>
    /// Controller widgets keep the Target active when a slot is Disabled.
    /// Slot Enabled chooses layout (with Phone fallback). It does not SetActive the Target.
    /// </summary>
    public class ResponsiveWidgetTargetActiveTests
    {
        [Test]
        public void ApplyFromHub_DisabledExactSlot_DoesNotHideTarget()
        {
            WithControllerWidget(
                targetActive: true,
                CoverDisabledLayout(),
                (hub, controller, widgetGo) =>
                {
                    controller.ApplyFromHub(
                        hub,
                        LayoutKey(ResponsiveDevice.FormFactor.FlipPhoneCover, true),
                        layoutOnly: false);
                    Assert.IsTrue(widgetGo.activeSelf);

                    controller.ApplyFromHub(
                        hub,
                        LayoutKey(ResponsiveDevice.FormFactor.Phone, true),
                        layoutOnly: false);
                    Assert.IsTrue(widgetGo.activeSelf);
                });
        }

        [Test]
        public void ApplyFromHub_AuthoredInactiveTarget_StaysInactive()
        {
            WithControllerWidget(
                targetActive: false,
                new ResponsiveWidgetLayout
                {
                    phoneLandscape = new ResponsiveRectState { enabled = true },
                },
                (hub, controller, widgetGo) =>
                {
                    controller.ApplyFromHub(
                        hub,
                        LayoutKey(ResponsiveDevice.FormFactor.Phone, true),
                        layoutOnly: false);
                    Assert.IsFalse(widgetGo.activeSelf);
                });
        }

        [Test]
        public void ApplyFromHub_LayoutOnly_DoesNotHideDisabledExactSlot()
        {
            WithControllerWidget(
                targetActive: true,
                CoverDisabledLayout(),
                (hub, controller, widgetGo) =>
                {
                    controller.ApplyFromHub(
                        hub,
                        LayoutKey(ResponsiveDevice.FormFactor.FlipPhoneCover, true),
                        layoutOnly: true);
                    Assert.IsTrue(widgetGo.activeSelf);
                });
        }

        [Test]
        public void IsExactLayoutSlot_IgnoresFallback()
        {
            ResponsiveDevice.LayoutKey flipCoverLandscape =
                LayoutKey(ResponsiveDevice.FormFactor.FlipPhoneCover, true);
            Assert.IsTrue(ResponsiveWidgetLayout.IsExactLayoutSlot(
                flipCoverLandscape, "flipPhoneCoverLandscape"));
            Assert.IsFalse(ResponsiveWidgetLayout.IsExactLayoutSlot(
                flipCoverLandscape, "phoneLandscape"));
            Assert.IsTrue(ResponsiveWidgetLayout.IsExactLayoutSlot(
                LayoutKey(ResponsiveDevice.FormFactor.Phone, true), "phoneLandscape"));
        }

        [Test]
        public void IsSeedDisabledSlot_FlipCoverLandscapeOnly()
        {
            string[] names = ResponsiveWidgetLayout.StatePropertyNames;
            for (int i = 0; i < names.Length; i++)
            {
                Assert.AreEqual(
                    names[i] == "flipPhoneCoverLandscape",
                    ResponsiveWidgetLayout.IsSeedDisabledSlot(i),
                    names[i]);
            }
        }

        static ResponsiveWidgetLayout CoverDisabledLayout() =>
            new ResponsiveWidgetLayout
            {
                phoneLandscape = new ResponsiveRectState { enabled = true },
                flipPhoneCoverLandscape = new ResponsiveRectState { enabled = false },
            };

        static void WithControllerWidget(
            bool targetActive,
            ResponsiveWidgetLayout layout,
            Action<ResponsiveUI, ResponsiveUiController, GameObject> body)
        {
            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler));
            var widgetGo = new GameObject("Widget", typeof(RectTransform));
            Run(canvasGo, () =>
            {
                widgetGo.transform.SetParent(canvasGo.transform, false);
                var hub = canvasGo.AddComponent<ResponsiveUI>();
                hub.enabled = true;
                var controller = canvasGo.AddComponent<ResponsiveUiController>();
                layout.target = widgetGo.GetComponent<RectTransform>();
                ResponsiveEditorTestUtil.SetControllerLists(
                    controller,
                    new[] { layout },
                    Array.Empty<ResponsiveBackground>(),
                    Array.Empty<ResponsiveActiveObjectEntry>(),
                    hub);
                widgetGo.SetActive(targetActive);
                body(hub, controller, widgetGo);
            });
        }
    }
}
