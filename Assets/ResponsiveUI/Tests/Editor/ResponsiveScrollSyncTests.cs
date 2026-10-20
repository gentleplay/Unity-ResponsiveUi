using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using FDPlus.ResponsiveUI;
using FDPlus.ResponsiveUI.Editor;

using static FDPlus.ResponsiveUI.Tests.ResponsiveEditorTestUtil;

namespace FDPlus.ResponsiveUI.Tests
{
    public class ResponsiveScrollMetricsHostStub : MonoBehaviour
    {
        public float cellInterval = 0.2f;
        public float scrollOffset = 0.5f;
    }

    public class ResponsiveScrollSyncTests
    {
        [Test]
        public void ScrollDriver_Unbind_ClearsQueuedPostLoadSoRebindRequeues()
        {
            var go = new GameObject("ScrollDriverPostLoad");
            try
            {
                var host = go.AddComponent<ResponsiveScrollMetricsHostStub>();
                var driver = new ResponsiveScrollDriver();
                driver.Bind(host, () => { });

                var queued = typeof(ResponsiveScrollDriver).GetField(
                    "_postLoadQueued",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic);
                Assert.IsNotNull(queued);

                // Simulates a post-load coroutine killed by SetActive(false) before it ran.
                queued.SetValue(driver, true);
                go.SetActive(false);
                driver.Unbind();
                Assert.IsFalse((bool)queued.GetValue(driver),
                    "Unbind (OnDisable) must clear the post-load queue");

                // Unbind on an already-unbound driver still clears a stale queue.
                queued.SetValue(driver, true);
                driver.Unbind();
                Assert.IsFalse((bool)queued.GetValue(driver));

                go.SetActive(true);
                driver.Bind(host, () => { });
                Assert.IsFalse((bool)queued.GetValue(driver),
                    "Edit Mode Bind does not queue the Play Mode post-load pass");
                driver.Unbind();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void SameAuthoring_TrueWhenMetricsMatch()
        {
            var a = ResponsiveScrollState.From(0.19f, 0.5f);
            Assert.IsTrue(ResponsiveScrollEditorUtil.SameAuthoring(
                a, ResponsiveScrollState.From(0.19f, 0.5f)));
            Assert.IsFalse(ResponsiveScrollEditorUtil.SameAuthoring(
                a, ResponsiveScrollState.From(0.2f, 0.5f)));
        }

        [Test]
        public void FindHost_ReturnsSiblingWithCellInterval()
        {
            using (var fx = Fixture.HostOnly())
                Assert.AreSame(fx.Host, ResponsiveScrollEditorUtil.FindHost(fx.Scroll));
        }

        [Test]
        public void CopySlotToHost_WritesSiblingHostMetrics()
        {
            using (var fx = Fixture.HostOnly())
            {
                fx.Scroll.TakeLayout(UniformScrollMetrics(0.19f, 0.5f), hub: null);
                fx.CopyPhoneLandscapeToHost(0.33f, 0.44f);
                fx.AssertHost(0.33f, 0.44f);
            }
        }

        [Test]
        public void CopySlotToHost_DisabledComponent_DoesNotWriteHost()
        {
            using (var fx = Fixture.HostOnly())
            {
                fx.Scroll.TakeLayout(UniformScrollMetrics(0.19f, 0.5f), hub: null);
                fx.Scroll.enabled = false;
                fx.CopyPhoneLandscapeToHost(0.33f, 0.44f);
                fx.AssertHost(0.2f, 0.5f);
            }
        }

        [Test]
        public void CopySlotToHost_DisabledHub_DoesNotWriteHost()
        {
            using (var fx = Fixture.WithHub(hubEnabled: false))
            {
                fx.Scroll.TakeLayout(UniformScrollMetrics(0.19f, 0.5f), fx.Hub);
                fx.CopyPhoneLandscapeToHost(0.33f, 0.44f);
                fx.AssertHost(0.2f, 0.5f);
            }
        }

        [Test]
        public void ApplyFromHub_WritesHostMetrics()
        {
            using (var fx = Fixture.WithHub(
                form: ResponsiveDevice.FormFactor.Pad,
                orientation: ResponsiveDevice.Orientation.Landscape))
            {
                fx.Scroll.TakeLayout(DistinctScrollMetrics(), fx.Hub);
                fx.Scroll.ApplyFromHub(
                    fx.Hub,
                    LayoutKey(
                        ResponsiveDevice.FormFactor.Pad,
                        ResponsiveDevice.Orientation.Landscape),
                    layoutOnly: false);
                fx.AssertHost(0.77f, 0.31f);
            }
        }

        [Test]
        public void CaptureHostToActiveSlot_WritesEnabledApplySlot()
        {
            using (var fx = Fixture.WithHub())
            {
                fx.Scroll.TakeLayout(UniformScrollMetrics(0.19f, 0.5f), fx.Hub);
                fx.WriteHost(0.33f, 0.44f);
                Assert.IsTrue(ResponsiveScrollEditorUtil.CaptureHostToActiveSlot(fx.Scroll));
                fx.AssertPhoneLandscapeSlot(0.33f, 0.44f, enabled: true);
            }
        }

        [Test]
        public void CaptureHostToActiveSlot_PreservesDisabledFlag()
        {
            using (var fx = Fixture.WithHub())
            {
                fx.Scroll.TakeLayout(
                    new ResponsiveScrollLayout
                    {
                        phoneLandscape = ResponsiveScrollState.From(0.19f, 0.5f, enabled: false),
                    },
                    fx.Hub);
                fx.WriteHost(0.33f, 0.44f);
                Assert.IsTrue(ResponsiveScrollEditorUtil.CaptureHostToActiveSlot(fx.Scroll));
                fx.AssertPhoneLandscapeSlot(0.33f, 0.44f, enabled: false);
            }
        }

        sealed class Fixture : IDisposable
        {
            public readonly GameObject Root;
            public readonly ResponsiveScrollMetricsHostStub Host;
            public readonly ResponsiveScrollView Scroll;
            public readonly ResponsiveUI Hub;

            Fixture(
                GameObject root,
                ResponsiveScrollMetricsHostStub host,
                ResponsiveScrollView scroll,
                ResponsiveUI hub)
            {
                Root = root;
                Host = host;
                Scroll = scroll;
                Hub = hub;
            }

            public static Fixture HostOnly()
            {
                var go = new GameObject("ScrollHost");
                return new Fixture(
                    go,
                    go.AddComponent<ResponsiveScrollMetricsHostStub>(),
                    go.AddComponent<ResponsiveScrollView>(),
                    hub: null);
            }

            public static Fixture WithHub(
                bool hubEnabled = true,
                ResponsiveDevice.FormFactor form = ResponsiveDevice.FormFactor.Phone,
                ResponsiveDevice.Orientation orientation = ResponsiveDevice.Orientation.Landscape)
            {
                var canvas = new GameObject("ScrollHubCanvas", typeof(Canvas));
                var go = new GameObject("ScrollHost");
                go.transform.SetParent(canvas.transform);
                var hub = canvas.AddComponent<ResponsiveUI>();
                hub.enabled = hubEnabled;
                if (hubEnabled)
                {
                    SetHubOverride(hub, form, orientation);
                    hub.Apply();
                }

                return new Fixture(
                    canvas,
                    go.AddComponent<ResponsiveScrollMetricsHostStub>(),
                    go.AddComponent<ResponsiveScrollView>(),
                    hub);
            }

            public void CopyPhoneLandscapeToHost(float interval, float offset)
            {
                SerializedProperty state = PhoneLandscapeState();
                state.FindPropertyRelative("enabled").boolValue = true;
                state.FindPropertyRelative("cellInterval").floatValue = interval;
                state.FindPropertyRelative("scrollOffset").floatValue = offset;
                ResponsiveScrollEditorUtil.CopySlotToHost(
                    state.serializedObject.FindProperty("scrollMetrics"),
                    state);
            }

            public void WriteHost(float interval, float offset)
            {
                var so = new SerializedObject(Host);
                so.FindProperty("cellInterval").floatValue = interval;
                so.FindProperty("scrollOffset").floatValue = offset;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            public void AssertHost(float interval, float offset)
            {
                var so = new SerializedObject(Host);
                so.Update();
                Assert.AreEqual(interval, so.FindProperty("cellInterval").floatValue);
                Assert.AreEqual(offset, so.FindProperty("scrollOffset").floatValue);
            }

            public void AssertPhoneLandscapeSlot(float interval, float offset, bool enabled)
            {
                SerializedProperty state = PhoneLandscapeState();
                Assert.AreEqual(interval, state.FindPropertyRelative("cellInterval").floatValue);
                Assert.AreEqual(offset, state.FindPropertyRelative("scrollOffset").floatValue);
                Assert.AreEqual(enabled, state.FindPropertyRelative("enabled").boolValue);
            }

            SerializedProperty PhoneLandscapeState()
            {
                var so = new SerializedObject(Scroll);
                so.Update();
                return so.FindProperty("scrollMetrics").FindPropertyRelative("phoneLandscape");
            }

            public void Dispose() => UnityEngine.Object.DestroyImmediate(Root);
        }
    }
}
