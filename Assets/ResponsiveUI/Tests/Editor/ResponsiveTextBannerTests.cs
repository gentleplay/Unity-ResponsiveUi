using FDPlus.ResponsiveUI;
using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace FDPlus.ResponsiveUI.Tests
{
    public class ResponsiveTextBannerTests
    {
        [Test]
        public void CalculateWidth_UsesTextAndBothPaddings()
        {
            Assert.AreEqual(
                420f,
                ResponsiveTextBanner.CalculateWidth(300f, 20f, 100f),
                1e-4f);
            Assert.AreEqual(
                0f,
                ResponsiveTextBanner.CalculateWidth(-5f, -10f, -20f),
                1e-4f);
        }

        [TestCase(ResponsiveTextBanner.Align.LeftUp)]
        [TestCase(ResponsiveTextBanner.Align.CenterUp)]
        [TestCase(ResponsiveTextBanner.Align.RightUp)]
        [TestCase(ResponsiveTextBanner.Align.LeftMiddle)]
        [TestCase(ResponsiveTextBanner.Align.CenterMiddle)]
        [TestCase(ResponsiveTextBanner.Align.RightMiddle)]
        [TestCase(ResponsiveTextBanner.Align.LeftDown)]
        [TestCase(ResponsiveTextBanner.Align.CenterDown)]
        [TestCase(ResponsiveTextBanner.Align.RightDown)]
        public void CalculateAlignedPosition_UsesAlignmentAsPivot(
            ResponsiveTextBanner.Align alignment)
        {
            Vector2 slotPosition = new(-685f, -538f);
            Vector2 slotSize = new(710.57f, 135f);
            Vector2 slotPivot = new(0.5f, 0.5f);
            Vector2 alignPivot = ResponsiveTextBanner.AlignmentAnchor(alignment);

            Vector2 aligned = ResponsiveTextBanner.CalculateAlignedPosition(
                slotPosition,
                slotSize,
                slotPivot,
                alignment);

            Assert.AreEqual(
                ResponsiveTextBanner.ConvertAnchoredPositionForPivot(
                    slotPosition,
                    slotSize,
                    slotPivot,
                    alignPivot),
                aligned);
            Assert.AreEqual(alignPivot, ResponsiveTextBanner.AlignmentAnchor(alignment));
        }

        [Test]
        public void CalculateAlignedPosition_DoesNotDependOnFittedWidth()
        {
            Vector2 slotPosition = new(-1070f, -538f);
            Vector2 slotSize = new(612f, 135f);
            Vector2 slotPivot = new(0.5f, 0.5f);

            Vector2 left = ResponsiveTextBanner.CalculateAlignedPosition(
                slotPosition, slotSize, slotPivot, ResponsiveTextBanner.Align.LeftMiddle);
            Vector2 center = ResponsiveTextBanner.CalculateAlignedPosition(
                slotPosition, slotSize, slotPivot, ResponsiveTextBanner.Align.CenterMiddle);
            Vector2 right = ResponsiveTextBanner.CalculateAlignedPosition(
                slotPosition, slotSize, slotPivot, ResponsiveTextBanner.Align.RightMiddle);

            Assert.AreEqual(slotPosition.x - slotSize.x * slotPivot.x, left.x, 1e-4f);
            Assert.AreEqual(slotPosition.x + slotSize.x * (0.5f - slotPivot.x), center.x, 1e-4f);
            Assert.AreEqual(slotPosition.x + slotSize.x * (1f - slotPivot.x), right.x, 1e-4f);
        }

        [Test]
        public void AlignmentAnchor_MapsNinePoints()
        {
            Assert.AreEqual(new Vector2(0f, 1f), ResponsiveTextBanner.AlignmentAnchor(ResponsiveTextBanner.Align.LeftUp));
            Assert.AreEqual(new Vector2(0.5f, 1f), ResponsiveTextBanner.AlignmentAnchor(ResponsiveTextBanner.Align.CenterUp));
            Assert.AreEqual(new Vector2(1f, 1f), ResponsiveTextBanner.AlignmentAnchor(ResponsiveTextBanner.Align.RightUp));
            Assert.AreEqual(new Vector2(0f, 0.5f), ResponsiveTextBanner.AlignmentAnchor(ResponsiveTextBanner.Align.LeftMiddle));
            Assert.AreEqual(new Vector2(0.5f, 0.5f), ResponsiveTextBanner.AlignmentAnchor(ResponsiveTextBanner.Align.CenterMiddle));
            Assert.AreEqual(new Vector2(1f, 0.5f), ResponsiveTextBanner.AlignmentAnchor(ResponsiveTextBanner.Align.RightMiddle));
            Assert.AreEqual(new Vector2(0f, 0f), ResponsiveTextBanner.AlignmentAnchor(ResponsiveTextBanner.Align.LeftDown));
            Assert.AreEqual(new Vector2(0.5f, 0f), ResponsiveTextBanner.AlignmentAnchor(ResponsiveTextBanner.Align.CenterDown));
            Assert.AreEqual(new Vector2(1f, 0f), ResponsiveTextBanner.AlignmentAnchor(ResponsiveTextBanner.Align.RightDown));
        }

        [Test]
        public void ConvertAnchoredPositionForPivot_PreservesRectEdges()
        {
            Vector2 position = new(-725f, -538f);
            Vector2 size = new(450f, 135f);
            Vector2 fromPivot = new(0.5f, 0.5f);
            Vector2 toPivot = new(0f, 0.5f);

            Vector2 converted = ResponsiveTextBanner.ConvertAnchoredPositionForPivot(
                position,
                size,
                fromPivot,
                toPivot);

            Assert.AreEqual(
                position.x - size.x * fromPivot.x,
                converted.x - size.x * toPivot.x,
                1e-4f);
            Assert.AreEqual(
                position.y - size.y * fromPivot.y,
                converted.y - size.y * toPivot.y,
                1e-4f);
        }

        [Test]
        public void Hide_UndoesOwnAlignmentWrite()
        {
            GameObject owner = new("Banner Owner");
            GameObject target = new("Banner", typeof(RectTransform));
            try
            {
                var banner = owner.AddComponent<ResponsiveTextBanner>();
                var rect = target.GetComponent<RectTransform>();
                Vector2 slotPosition = new(25f, -40f);
                Vector2 slotSize = new(600f, 120f);
                rect.anchoredPosition = slotPosition;
                rect.sizeDelta = slotSize;

                banner.SetTargets(rect, null);
                banner.SetAlignment(ResponsiveTextBanner.Align.RightMiddle);
                Assert.AreEqual(new Vector2(1f, 0.5f), rect.pivot);

                banner.Hide();

                Assert.AreEqual(new Vector2(0.5f, 0.5f), rect.pivot);
                Assert.AreEqual(slotPosition, rect.anchoredPosition);
                Assert.AreEqual(slotSize, rect.sizeDelta);
                Assert.IsFalse(target.activeSelf);
            }
            finally
            {
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void Hide_AfterControllerRewrite_KeepsAndAdoptsControllerLayout()
        {
            GameObject owner = new("Banner Owner");
            GameObject target = new("Banner", typeof(RectTransform));
            try
            {
                var banner = owner.AddComponent<ResponsiveTextBanner>();
                var rect = target.GetComponent<RectTransform>();
                rect.anchoredPosition = new Vector2(25f, -40f);
                rect.sizeDelta = new Vector2(600f, 120f);
                banner.SetTargets(rect, null);
                banner.SetAlignment(ResponsiveTextBanner.Align.RightMiddle);

                // Owning controller applies a new device / fold slot over the banner.
                Vector2 newPosition = new(500f, 600f);
                Vector2 newSize = new(900f, 300f);
                Vector2 newPivot = new(0.5f, 0.5f);
                rect.pivot = newPivot;
                rect.anchoredPosition = newPosition;
                rect.sizeDelta = newSize;

                banner.Hide();

                Assert.AreEqual(newPivot, rect.pivot);
                Assert.AreEqual(newPosition, rect.anchoredPosition);
                Assert.AreEqual(newSize, rect.sizeDelta);

                // The new layout is now the slot the next Alignment write starts from.
                banner.SetAlignment(ResponsiveTextBanner.Align.RightMiddle);
                Assert.AreEqual(
                    ResponsiveTextBanner.CalculateAlignedPosition(
                        newPosition,
                        newSize,
                        newPivot,
                        ResponsiveTextBanner.Align.RightMiddle),
                    rect.anchoredPosition);
            }
            finally
            {
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void Hide_OnHiddenBanner_KeepsControllerLayout()
        {
            // Gameplay calls Hide() on an already-hidden banner (ball-type messages).
            // The controller may have moved it since; that layout must not be reverted.
            GameObject owner = new("Banner Owner");
            GameObject target = new("Banner", typeof(RectTransform));
            try
            {
                var banner = owner.AddComponent<ResponsiveTextBanner>();
                var rect = target.GetComponent<RectTransform>();
                rect.anchoredPosition = new Vector2(25f, -40f);
                rect.sizeDelta = new Vector2(600f, 120f);
                banner.SetTargets(rect, null);

                Vector2 newPosition = new(-300f, 80f);
                Vector2 newSize = new(720f, 140f);
                rect.anchoredPosition = newPosition;
                rect.sizeDelta = newSize;

                banner.Hide();

                Assert.AreEqual(newPosition, rect.anchoredPosition);
                Assert.AreEqual(newSize, rect.sizeDelta);
            }
            finally
            {
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void Hide_OnBannerGameObject_KeepsComponentEnabled()
        {
            GameObject go = new("Banner", typeof(RectTransform));
            try
            {
                var rect = go.GetComponent<RectTransform>();
                rect.anchoredPosition = new Vector2(-1070f, -538f);
                rect.sizeDelta = new Vector2(600f, 135f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                var banner = go.AddComponent<ResponsiveTextBanner>();
                banner.SetTargets(rect, null);
                banner.SetAlignment(ResponsiveTextBanner.Align.LeftMiddle);

                banner.Hide();

                Assert.IsTrue(go.activeSelf);
                Assert.IsTrue(banner.enabled);
                Assert.IsTrue(banner.isActiveAndEnabled);
                Assert.AreEqual(new Vector2(0.5f, 0.5f), rect.pivot);
                Assert.AreEqual(new Vector2(-1070f, -538f), rect.anchoredPosition);
                Assert.AreEqual(new Vector2(600f, 135f), rect.sizeDelta);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void RefreshLayout_AfterFittedWidth_DoesNotMoveAlignmentPoint()
        {
            GameObject owner = new("Banner Owner");
            GameObject target = new("Banner", typeof(RectTransform));
            try
            {
                var banner = owner.AddComponent<ResponsiveTextBanner>();
                var rect = target.GetComponent<RectTransform>();
                Vector2 slotPosition = new(-1070f, -538f);
                Vector2 slotSize = new(612f, 135f);
                Vector2 slotPivot = new(0.5f, 0.5f);
                rect.anchoredPosition = slotPosition;
                rect.sizeDelta = slotSize;
                rect.pivot = slotPivot;
                banner.SetTargets(rect, null);
                banner.SetAlignment(ResponsiveTextBanner.Align.RightMiddle);
                Vector2 aligned = rect.anchoredPosition;
                rect.sizeDelta = new Vector2(220f, slotSize.y);

                banner.RefreshLayout();

                Assert.AreEqual(new Vector2(1f, 0.5f), rect.pivot);
                Assert.AreEqual(aligned, rect.anchoredPosition);
            }
            finally
            {
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void Hide_RestoresCapturedPivotAndPosition()
        {
            GameObject owner = new("Banner Owner");
            GameObject target = new("Banner", typeof(RectTransform));
            try
            {
                var banner = owner.AddComponent<ResponsiveTextBanner>();
                var rect = target.GetComponent<RectTransform>();
                Vector2 slotPosition = new(-725f, -538f);
                Vector2 slotSize = new(450f, 135f);
                Vector2 slotPivot = new(0.5f, 0.5f);
                rect.anchoredPosition = slotPosition;
                rect.sizeDelta = slotSize;
                rect.pivot = slotPivot;

                banner.SetTargets(rect, null);
                banner.SetAlignment(ResponsiveTextBanner.Align.RightMiddle);

                banner.Hide();

                Assert.AreEqual(slotPivot, rect.pivot);
                Assert.AreEqual(slotPosition, rect.anchoredPosition);
                Assert.AreEqual(slotSize, rect.sizeDelta);
            }
            finally
            {
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void SetAlignment_WritesMatchingPivotWithoutChangingSlotSize()
        {
            GameObject owner = new("Banner Owner");
            GameObject target = new("Banner", typeof(RectTransform));
            try
            {
                var banner = owner.AddComponent<ResponsiveTextBanner>();
                var rect = target.GetComponent<RectTransform>();
                Vector2 slotPosition = new(-1070f, -538f);
                Vector2 slotSize = new(612f, 135f);
                Vector2 slotPivot = new(0.5f, 0.5f);
                rect.anchoredPosition = slotPosition;
                rect.sizeDelta = slotSize;
                rect.pivot = slotPivot;
                banner.SetTargets(rect, null);

                banner.SetAlignment(ResponsiveTextBanner.Align.RightMiddle);

                Assert.AreEqual(new Vector2(1f, 0.5f), rect.pivot);
                Assert.AreEqual(
                    ResponsiveTextBanner.CalculateAlignedPosition(
                        slotPosition,
                        slotSize,
                        slotPivot,
                        ResponsiveTextBanner.Align.RightMiddle),
                    rect.anchoredPosition);
                Assert.AreEqual(slotSize, rect.sizeDelta);

                Vector2 positionAfterAlign = rect.anchoredPosition;
                rect.sizeDelta = new Vector2(220f, slotSize.y);
                Assert.AreEqual(new Vector2(1f, 0.5f), rect.pivot);
                Assert.AreEqual(positionAfterAlign, rect.anchoredPosition);

                banner.RefreshLayout();
                Assert.AreEqual(new Vector2(1f, 0.5f), rect.pivot);
                Assert.AreEqual(positionAfterAlign, rect.anchoredPosition);
            }
            finally
            {
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void CaptureSlot_UsesLiveAuthoredPivotAndPosition()
        {
            GameObject owner = new("Banner Owner");
            GameObject target = new("Banner", typeof(RectTransform));
            try
            {
                var banner = owner.AddComponent<ResponsiveTextBanner>();
                var rect = target.GetComponent<RectTransform>();
                rect.anchoredPosition = new Vector2(-725f, -538f);
                rect.sizeDelta = new Vector2(450f, 135f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                banner.SetTargets(rect, null);

                Vector2 authoredPosition = new(-1070f, -538f);
                Vector2 authoredSize = new(612f, 135f);
                Vector2 authoredPivot = new(0f, 0.5f);
                rect.anchoredPosition = authoredPosition;
                rect.sizeDelta = authoredSize;
                rect.pivot = authoredPivot;
                banner.CaptureSlot();

                // Banner's own write from the captured slot, then undone by Hide.
                banner.SetAlignment(ResponsiveTextBanner.Align.RightMiddle);
                banner.Hide();

                Assert.AreEqual(authoredPivot, rect.pivot);
                Assert.AreEqual(authoredPosition, rect.anchoredPosition);
                Assert.AreEqual(authoredSize, rect.sizeDelta);
            }
            finally
            {
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void PreviewLock_BlocksTimedShowAndUnlocksCleanly()
        {
            GameObject owner = new("Banner Owner");
            GameObject target = new("Banner", typeof(RectTransform));
            try
            {
                var banner = owner.AddComponent<ResponsiveTextBanner>();
                banner.SetTargets(target.GetComponent<RectTransform>(), null);
                target.SetActive(false);

                banner.SetPreviewLock(true);
                banner.Show("Timed message", autoHide: true);

                Assert.IsTrue(banner.IsPreviewLocked);
                Assert.IsFalse(target.activeSelf);

                banner.SetPreviewLock(false);

                Assert.IsFalse(banner.IsPreviewLocked);
                Assert.IsFalse(banner.IsVisible);
            }
            finally
            {
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void SizeChangeRequiresReapply_OnlyWhileVisible()
        {
            GameObject owner = new("Banner Owner");
            try
            {
                var banner = owner.AddComponent<ResponsiveTextBanner>();
                Assert.IsFalse(banner.SizeChangeRequiresReapply());
                Assert.IsFalse(banner.IsVisible);
            }
            finally
            {
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void PendingHubCapture_DoesNotSampleFittedRectAsSlot()
        {
            GameObject owner = new("Banner Owner");
            GameObject target = new("Banner", typeof(RectTransform));
            GameObject hubObject = new(
                "Banner Hub",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(ResponsiveUI));
            try
            {
                var banner = owner.AddComponent<ResponsiveTextBanner>();
                ResponsiveUI hub = hubObject.GetComponent<ResponsiveUI>();
                hub.enabled = true;
                var rect = target.GetComponent<RectTransform>();
                Vector2 slotPosition = new(25f, -40f);
                Vector2 slotSize = new(600f, 120f);
                rect.anchoredPosition = slotPosition;
                rect.sizeDelta = slotSize;

                banner.SetTargets(rect, null);
                // Banner-written (aligned) rect: must never be sampled as the slot.
                banner.SetAlignment(ResponsiveTextBanner.Align.RightMiddle);
                banner.ApplyFromHub(
                    hub,
                    default,
                    layoutOnly: false);

                banner.CaptureSlot();
                banner.Hide();

                Assert.AreEqual(new Vector2(0.5f, 0.5f), rect.pivot);
                Assert.AreEqual(slotPosition, rect.anchoredPosition);
                Assert.AreEqual(slotSize, rect.sizeDelta);
            }
            finally
            {
                Object.DestroyImmediate(hubObject);
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void PendingHubCapture_HideAfterControllerRewrite_KeepsNewLayout()
        {
            // Device / fold change: the controller writes the new slot, the hub notifies
            // the banner (capture pending until LateUpdate), and gameplay hides the banner
            // in the same frame. The new layout must survive.
            GameObject owner = new("Banner Owner");
            GameObject target = new("Banner", typeof(RectTransform));
            GameObject hubObject = new(
                "Banner Hub",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(ResponsiveUI));
            try
            {
                var banner = owner.AddComponent<ResponsiveTextBanner>();
                ResponsiveUI hub = hubObject.GetComponent<ResponsiveUI>();
                hub.enabled = true;
                var rect = target.GetComponent<RectTransform>();
                rect.anchoredPosition = new Vector2(25f, -40f);
                rect.sizeDelta = new Vector2(600f, 120f);
                banner.SetTargets(rect, null);
                banner.SetAlignment(ResponsiveTextBanner.Align.RightMiddle);

                Vector2 newPosition = new(300f, 200f);
                Vector2 newSize = new(700f, 150f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = newPosition;
                rect.sizeDelta = newSize;
                banner.ApplyFromHub(hub, default, layoutOnly: false);

                banner.Hide();

                Assert.AreEqual(newPosition, rect.anchoredPosition);
                Assert.AreEqual(newSize, rect.sizeDelta);
            }
            finally
            {
                Object.DestroyImmediate(hubObject);
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void Show_FitsWidthToText_AndHideRestoresBannerAndTextSlot()
        {
            GameObject target = new("Banner", typeof(RectTransform));
            GameObject textGo = new("Text", typeof(RectTransform));
            try
            {
                textGo.transform.SetParent(target.transform, false);
                var tmp = textGo.AddComponent<TextMeshProUGUI>();
                var rect = target.GetComponent<RectTransform>();
                Vector2 slotPosition = new(-200f, 50f);
                Vector2 slotSize = new(600f, 120f);
                Vector2 textSlotSize = new(500f, 100f);
                rect.anchoredPosition = slotPosition;
                rect.sizeDelta = slotSize;
                tmp.rectTransform.sizeDelta = textSlotSize;

                var banner = target.AddComponent<ResponsiveTextBanner>();
                banner.SetTargets(rect, tmp);

                banner.Show("Hello banner", autoHide: false);
                float fitted = rect.sizeDelta.x;
                Assert.IsTrue(banner.IsVisible);
                Assert.Greater(fitted, 0f);
                Assert.AreNotEqual(slotSize.x, fitted);
                Assert.AreEqual(slotSize.y, rect.sizeDelta.y, 1e-3f);

                // Same message again while shown: no re-measure, width unchanged.
                banner.Show("Hello banner", autoHide: false);
                Assert.AreEqual(fitted, rect.sizeDelta.x, 1e-3f);

                banner.Hide();

                Assert.AreEqual(slotPosition, rect.anchoredPosition);
                Assert.AreEqual(slotSize, rect.sizeDelta);
                Assert.AreEqual(textSlotSize, tmp.rectTransform.sizeDelta);
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void Show_ThenControllerRewrite_HideKeepsNewLayoutAndRestoresText()
        {
            GameObject target = new("Banner", typeof(RectTransform));
            GameObject textGo = new("Text", typeof(RectTransform));
            try
            {
                textGo.transform.SetParent(target.transform, false);
                var tmp = textGo.AddComponent<TextMeshProUGUI>();
                var rect = target.GetComponent<RectTransform>();
                rect.anchoredPosition = new Vector2(-200f, 50f);
                rect.sizeDelta = new Vector2(600f, 120f);
                Vector2 textSlotSize = new(500f, 100f);
                tmp.rectTransform.sizeDelta = textSlotSize;

                var banner = target.AddComponent<ResponsiveTextBanner>();
                banner.SetTargets(rect, tmp);
                banner.Show("Hello banner", autoHide: false);

                // Controller applies the new device slot while the banner is shown.
                Vector2 newPosition = new(120f, -30f);
                Vector2 newSize = new(800f, 160f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = newPosition;
                rect.sizeDelta = newSize;

                banner.Hide();

                Assert.AreEqual(newPosition, rect.anchoredPosition);
                Assert.AreEqual(newSize, rect.sizeDelta);
                // The fitted TMP width (the banner's own write) is still undone.
                Assert.AreEqual(textSlotSize, tmp.rectTransform.sizeDelta);
            }
            finally
            {
                Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void DestroyedBanner_AfterFit_RefreshAndHideDoNotThrow()
        {
            GameObject owner = new("Banner Owner");
            GameObject target = new("Banner", typeof(RectTransform));
            try
            {
                var banner = owner.AddComponent<ResponsiveTextBanner>();
                var rect = target.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(300f, 100f);
                banner.SetTargets(rect, null);

                var flags = System.Reflection.BindingFlags.Instance |
                            System.Reflection.BindingFlags.NonPublic;
                typeof(ResponsiveTextBanner).GetField("_fittedWidth", flags)
                    .SetValue(banner, 300f);

                Object.DestroyImmediate(target);
                typeof(ResponsiveTextBanner).GetField("_pendingHubCapture", flags)
                    .SetValue(banner, true);

                Assert.DoesNotThrow(() => banner.RefreshLayout());
                Assert.DoesNotThrow(() => banner.SetAlignment(ResponsiveTextBanner.Align.RightDown));
                Assert.DoesNotThrow(() => banner.Hide());
                Assert.IsFalse(banner.IsVisible);
            }
            finally
            {
                if (target != null)
                    Object.DestroyImmediate(target);
                Object.DestroyImmediate(owner);
            }
        }
    }
}
