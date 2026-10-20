using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using FDPlus.ResponsiveUI;
using FDPlus.ResponsiveUI.Editor;

namespace FDPlus.ResponsiveUI.Tests
{
    /// <summary>
    /// Active Objects: per-slot visibility, last-entry wins.
    /// </summary>
    public class ResponsiveActiveObjectTests
    {
    #region Slots

        [Test]
        public void IsActive_UsesExactSlot_NoPhoneFallback()
        {
            var slots = new ResponsiveActiveObjectSlots { phonePortrait = true };
            Assert.IsTrue(slots.IsActive(Key(ResponsiveDevice.FormFactor.Phone, false)));
            Assert.IsFalse(slots.IsActive(Key(ResponsiveDevice.FormFactor.Phone, true)));
            Assert.IsFalse(slots.IsActive(Key(ResponsiveDevice.FormFactor.FoldPhoneCover, false)));
        }

        [Test]
        public void IphoneDuoSeated_OwnSlots_DoNotFollowPad()
        {
            var slots = new ResponsiveActiveObjectSlots { padPortrait = true, padLandscape = true };
            Assert.IsFalse(slots.IsActive(Key(ResponsiveDevice.FormFactor.iPhoneDuoSeated, false)));
            Assert.IsFalse(slots.IsActive(Key(ResponsiveDevice.FormFactor.iPhoneDuoSeated, true)));

            slots.Set(
                ResponsiveWidgetLayout.StatePropertyIndex("iPhoneDuoSeatedLandscape"),
                true);
            Assert.IsTrue(slots.iPhoneDuoSeatedLandscape);
            Assert.IsFalse(slots.iPhoneDuoSeatedPortrait, "No seeding from Pad");
            Assert.IsTrue(slots.IsActive(Key(ResponsiveDevice.FormFactor.iPhoneDuoSeated, true)));
        }

        [Test]
        public void EditorReadWriteSlot_TouchesOnlyThatField()
        {
            var go = new GameObject("ActiveSlotsEditorUtil");
            try
            {
                var controller = go.AddComponent<ResponsiveUiController>();
                var so = new SerializedObject(controller);
                SerializedProperty list = so.FindProperty("activeObjects");
                list.arraySize = 1;
                SerializedProperty slots = list.GetArrayElementAtIndex(0).FindPropertyRelative("slots");
                string[] names = ResponsiveWidgetLayout.StatePropertyNames;
                for (int i = 0; i < names.Length; i++)
                {
                    for (int j = 0; j < names.Length; j++)
                        slots.FindPropertyRelative(names[j]).boolValue = false;
                    ResponsiveActiveObjectSlotsEditorUtil.WriteSlot(slots, i, true);
                    for (int j = 0; j < names.Length; j++)
                    {
                        Assert.AreEqual(j == i, slots.FindPropertyRelative(names[j]).boolValue, names[j]);
                        Assert.AreEqual(
                            j == i,
                            ResponsiveActiveObjectSlotsEditorUtil.ReadSlot(slots, j),
                            names[j]);
                    }
                    Assert.IsFalse(ResponsiveActiveObjectSlotsEditorUtil.AllOff(slots), names[i]);
                }
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void StatePropertyIndex_LayoutKey_MatchesSlotName()
        {
            var padLandscape = Key(ResponsiveDevice.FormFactor.Pad, true);
            Assert.AreEqual(
                ResponsiveWidgetLayout.StatePropertyIndex("padLandscape"),
                ResponsiveWidgetLayout.StatePropertyIndex(padLandscape));
            Assert.AreEqual(
                "padLandscape",
                ResponsiveWidgetLayout.StatePropertyName(padLandscape));
        }

    #endregion

    #region Apply

        [Test]
        public void Apply_TogglesListedObjects_LeavesUnlistedAlone()
        {
            var listed = new GameObject("listed");
            var unlisted = new GameObject("unlisted");
            try
            {
                listed.SetActive(true);
                unlisted.SetActive(true);
                var entries = new[]
                {
                    new ResponsiveActiveObjectEntry
                    {
                        target = listed,
                        slots = new ResponsiveActiveObjectSlots { padLandscape = true },
                    },
                };

                ResponsiveActiveObjects.Apply(entries, Key(ResponsiveDevice.FormFactor.Phone, true));
                Assert.IsFalse(listed.activeSelf);
                Assert.IsTrue(unlisted.activeSelf);

                ResponsiveActiveObjects.Apply(entries, Key(ResponsiveDevice.FormFactor.Pad, true));
                Assert.IsTrue(listed.activeSelf);
                Assert.IsTrue(unlisted.activeSelf);
            }
            finally
            {
                Object.DestroyImmediate(listed);
                Object.DestroyImmediate(unlisted);
            }
        }

        [Test]
        public void Apply_DuplicateTargets_LastEntryWins()
        {
            var go = new GameObject("dup");
            try
            {
                var entries = new[]
                {
                    new ResponsiveActiveObjectEntry
                    {
                        target = go,
                        slots = Mask("phonePortrait", "phoneLandscape"),
                    },
                    new ResponsiveActiveObjectEntry
                    {
                        target = go,
                        slots = Mask("padPortrait", "padLandscape"),
                    },
                };

                ResponsiveActiveObjects.Apply(entries, Key(ResponsiveDevice.FormFactor.Phone, true));
                Assert.IsFalse(go.activeSelf);
                ResponsiveActiveObjects.Apply(entries, Key(ResponsiveDevice.FormFactor.Pad, true));
                Assert.IsTrue(go.activeSelf);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

    #endregion

    #region Helpers

        static ResponsiveDevice.LayoutKey Key(ResponsiveDevice.FormFactor form, bool landscape) =>
            new ResponsiveDevice.LayoutKey(
                form,
                landscape
                    ? ResponsiveDevice.Orientation.Landscape
                    : ResponsiveDevice.Orientation.Portrait);

        static ResponsiveActiveObjectSlots Mask(params string[] slotNames)
        {
            var slots = new ResponsiveActiveObjectSlots();
            foreach (string name in slotNames)
                slots.Set(ResponsiveWidgetLayout.StatePropertyIndex(name), true);
            return slots;
        }

    #endregion
    }
}
