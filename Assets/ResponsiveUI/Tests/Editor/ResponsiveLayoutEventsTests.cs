using NUnit.Framework;
using UnityEngine.Events;
using FDPlus.ResponsiveUI;

namespace FDPlus.ResponsiveUI.Tests
{
    /// <summary>
    /// Per-slot Play Mode events: exact live layout, no Phone fallback.
    /// </summary>
    public class ResponsiveLayoutEventsTests
    {
        [Test]
        public void Invoke_FiresExactSlot_NotPhoneFallback()
        {
            int phone = 0;
            int cover = 0;
            var events = new ResponsiveLayoutEvents
            {
                phonePortrait = new UnityEvent(),
                foldPhoneCoverPortrait = new UnityEvent(),
            };
            events.phonePortrait.AddListener(() => phone++);
            events.foldPhoneCoverPortrait.AddListener(() => cover++);

            events.Invoke(Key(ResponsiveDevice.FormFactor.FoldPhoneCover, false));
            Assert.AreEqual(0, phone);
            Assert.AreEqual(1, cover);
        }

        [Test]
        public void Invoke_Landscape_DoesNotFirePortrait()
        {
            int portrait = 0;
            int landscape = 0;
            var events = new ResponsiveLayoutEvents
            {
                padPortrait = new UnityEvent(),
                padLandscape = new UnityEvent(),
            };
            events.padPortrait.AddListener(() => portrait++);
            events.padLandscape.AddListener(() => landscape++);

            events.Invoke(Key(ResponsiveDevice.FormFactor.Pad, true));
            Assert.AreEqual(0, portrait);
            Assert.AreEqual(1, landscape);
        }

        [Test]
        public void Get_MatchesStatePropertyIndex()
        {
            var events = new ResponsiveLayoutEvents
            {
                foldHalfOpenedLandscape = new UnityEvent(),
                flipPhoneCoverPortrait = new UnityEvent(),
            };
            int index = ResponsiveWidgetLayout.StatePropertyIndex("foldHalfOpenedLandscape");
            Assert.AreSame(events.foldHalfOpenedLandscape, events.Get(index));
            Assert.IsNull(events.Get(-1));
            Assert.AreSame(
                events.flipPhoneCoverPortrait,
                events.Get(ResponsiveWidgetLayout.StatePropertyIndex("flipPhoneCoverPortrait")));
            Assert.IsNull(events.Get(ResponsiveWidgetLayout.StatePropertyNames.Length));
        }

        [Test]
        public void Invoke_IphoneDuoSeated_FiresOwnSlotNotFoldCover()
        {
            int duo = 0;
            int foldCover = 0;
            var events = new ResponsiveLayoutEvents
            {
                iPhoneDuoSeatedLandscape = new UnityEvent(),
                foldCoverHalfOpenedLandscape = new UnityEvent(),
            };
            events.iPhoneDuoSeatedLandscape.AddListener(() => duo++);
            events.foldCoverHalfOpenedLandscape.AddListener(() => foldCover++);

            events.Invoke(Key(ResponsiveDevice.FormFactor.iPhoneDuoSeated, true));
            Assert.AreEqual(1, duo);
            Assert.AreEqual(0, foldCover);
            Assert.AreSame(
                events.iPhoneDuoSeatedLandscape,
                events.Get(ResponsiveWidgetLayout.StatePropertyIndex("iPhoneDuoSeatedLandscape")));
        }

        static ResponsiveDevice.LayoutKey Key(
            ResponsiveDevice.FormFactor form,
            bool landscape) =>
            new ResponsiveDevice.LayoutKey(
                form,
                landscape
                    ? ResponsiveDevice.Orientation.Landscape
                    : ResponsiveDevice.Orientation.Portrait);
    }
}
