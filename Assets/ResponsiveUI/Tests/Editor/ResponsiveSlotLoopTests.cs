using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;
using FDPlus.ResponsiveUI;

namespace FDPlus.ResponsiveUI.Tests
{
    /// <summary>
    /// Slot-loop helpers (any / migrate / capture / seed) see every
    /// <see cref="ResponsiveWidgetLayout.StatePropertyNames"/> index, so a new form cannot be
    /// skipped silently. Each test enables one slot field (by name) at a time.
    /// </summary>
    public class ResponsiveSlotLoopTests
    {
        static string[] Names => ResponsiveWidgetLayout.StatePropertyNames;

    #region Camera

        [Test]
        public void Camera_FocusHelpers_SeeEachSlot()
        {
            var focus = new GameObject("Focus");
            var bounds = new GameObject("Bounds", typeof(RectTransform)).GetComponent<RectTransform>();
            try
            {
                Assert.IsFalse(new ResponsiveCameraLayout().HasFocus());
                for (int i = 0; i < Names.Length; i++)
                {
                    var state = new ResponsiveCameraState
                    {
                        focusTarget = focus.transform,
                        focusScreen = ResponsiveCameraFocusScreen.Auto,
                        focusBounds = bounds,
                    };
                    ResponsiveCameraLayout layout = WithSlot(new ResponsiveCameraLayout(), i, state);
                    Assert.IsTrue(layout.HasFocus(), Names[i]);
                    Assert.IsTrue(layout.UsesFocusBounds(bounds), Names[i]);
                    Assert.IsTrue(layout.UsesFocusBoundsRect(bounds), Names[i]);
                    Assert.AreSame(bounds, layout.GetSlot(i).focusBounds, Names[i]);
                }
            }
            finally
            {
                Object.DestroyImmediate(focus);
                Object.DestroyImmediate(bounds.gameObject);
            }
        }

        [Test]
        public void Camera_ScaleHelpers_SeeEachSlot()
        {
            Assert.IsFalse(new ResponsiveCameraLayout().UsesUiSizeScale());
            for (int i = 0; i < Names.Length; i++)
            {
                ResponsiveCameraLayout layout = WithSlot(
                    new ResponsiveCameraLayout(), i, new ResponsiveCameraState { scaleFromUiSize = true });
                Assert.IsTrue(layout.UsesUiSizeScale(), Names[i]);
                Assert.IsTrue(layout.GetSlot(i).UsesCanvasScale, Names[i]);
            }
        }

        [Test]
        public void Camera_CaptureFromTarget_WritesOnlyEnabledSlot()
        {
            var go = new GameObject("Cam", typeof(Camera));
            try
            {
                Camera camera = go.GetComponent<Camera>();
                go.transform.localPosition = new Vector3(1f, 2f, -10f);
                for (int i = 0; i < Names.Length; i++)
                {
                    ResponsiveCameraLayout layout = WithSlot(
                        new ResponsiveCameraLayout { target = camera },
                        i,
                        new ResponsiveCameraState { enabled = true });
                    layout.CaptureFromTarget(Vector2.zero);
                    for (int j = 0; j < Names.Length; j++)
                    {
                        Assert.AreEqual(
                            j == i ? go.transform.localPosition : Vector3.zero,
                            layout.GetSlot(j).localPosition,
                            Names[i] + " / " + Names[j]);
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

    #endregion

    #region Widget / Scroll / Events / Active Objects

        [Test]
        public void Widget_UsesDesignScale_SeesEachSlot()
        {
            Assert.IsFalse(new ResponsiveWidgetLayout().UsesDesignScale());
            for (int i = 0; i < Names.Length; i++)
            {
                ResponsiveWidgetLayout layout = WithSlot(
                    new ResponsiveWidgetLayout(), i, new ResponsiveRectState { scaleFromDesignCanvas = true });
                Assert.IsTrue(layout.UsesDesignScale(), Names[i]);
            }
        }

        [Test]
        public void SlotIndex_RoundTripsFormOrientationAndName()
        {
            for (int i = 0; i < Names.Length; i++)
            {
                var key = new ResponsiveDevice.LayoutKey(
                    ResponsiveWidgetLayout.SlotFormFactor(i),
                    ResponsiveWidgetLayout.SlotOrientation(i));
                Assert.AreEqual(i, ResponsiveWidgetLayout.StatePropertyIndex(key), Names[i]);
                Assert.AreEqual(Names[i], ResponsiveWidgetLayout.StatePropertyName(key), Names[i]);
            }
        }

        [Test]
        public void Widget_ResolveActiveSlot_ExactSlotMatchesSlotName()
        {
            for (int i = 0; i < Names.Length; i++)
            {
                var state = new ResponsiveRectState { enabled = true, anchoredPosition = new Vector2(i + 1, 0f) };
                ResponsiveWidgetLayout layout = WithSlot(new ResponsiveWidgetLayout(), i, state);
                var key = new ResponsiveDevice.LayoutKey(
                    ResponsiveWidgetLayout.SlotFormFactor(i),
                    ResponsiveWidgetLayout.SlotOrientation(i));

                Assert.IsTrue(layout.ResolveActiveSlot(key, out string name, out _, out ResponsiveRectState resolved), Names[i]);
                Assert.AreEqual(Names[i], name, Names[i]);
                Assert.AreEqual(i + 1f, resolved.anchoredPosition.x, Names[i]);
            }
        }

        [Test]
        public void Scroll_SeedIfEmpty_SeesEachAuthoredSlot()
        {
            var empty = new ResponsiveScrollLayout();
            Assert.IsTrue(empty.SeedIfEmpty(0.3f, 0.5f, enablePortrait: true, enableLandscape: true));
            for (int i = 0; i < Names.Length; i++)
            {
                Assert.AreEqual(
                    0.3f,
                    ReadSlot<ResponsiveScrollLayout, ResponsiveScrollState>(empty, i).cellInterval,
                    1e-5f,
                    Names[i]);

                ResponsiveScrollLayout layout = WithSlot(
                    new ResponsiveScrollLayout(), i, new ResponsiveScrollState { cellInterval = 0.25f });
                Assert.IsFalse(layout.SeedIfEmpty(0.3f, 0.5f, enablePortrait: true, enableLandscape: true), Names[i]);
            }
        }

        [Test]
        public void LayoutEvents_Get_MatchesSlotName()
        {
            var events = new ResponsiveLayoutEvents();
            for (int i = 0; i < Names.Length; i++)
                events = WithSlot(events, i, new UnityEvent());
            for (int i = 0; i < Names.Length; i++)
            {
                Assert.AreSame(
                    ReadSlot<ResponsiveLayoutEvents, UnityEvent>(events, i),
                    events.Get(i),
                    Names[i]);
            }
        }

        [Test]
        public void ActiveObjectSlots_SetGet_TouchOnlyThatSlot()
        {
            for (int i = 0; i < Names.Length; i++)
            {
                var slots = new ResponsiveActiveObjectSlots();
                slots.Set(i, true);
                for (int j = 0; j < Names.Length; j++)
                {
                    Assert.AreEqual(j == i, slots.Get(j), Names[i] + " / " + Names[j]);
                    Assert.AreEqual(
                        j == i,
                        ReadSlot<ResponsiveActiveObjectSlots, bool>(slots, j),
                        Names[i] + " / " + Names[j]);
                }
            }
        }

    #endregion

    #region Helpers

        static T WithSlot<T, TState>(T layout, int slotIndex, TState state)
        {
            object boxed = layout;
            typeof(T).GetField(Names[slotIndex]).SetValue(boxed, state);
            return (T)boxed;
        }

        static TState ReadSlot<T, TState>(T layout, int slotIndex) =>
            (TState)typeof(T).GetField(Names[slotIndex]).GetValue(layout);

    #endregion
    }
}
