using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using FDPlus.ResponsiveUI;
using FDPlus.ResponsiveUI.Editor;

namespace FDPlus.ResponsiveUI.Tests
{
    /// <summary>
    /// Shared EditMode fixtures: hub override, layout keys, platform pin, and new-row seed Enabled.
    /// </summary>
    public static class ResponsiveEditorTestUtil
    {
        public static ResponsiveDevice.LayoutKey LayoutKey(
            ResponsiveDevice.FormFactor form,
            bool landscape) =>
            new ResponsiveDevice.LayoutKey(
                form,
                landscape
                    ? ResponsiveDevice.Orientation.Landscape
                    : ResponsiveDevice.Orientation.Portrait);

        public static ResponsiveDevice.LayoutKey LayoutKey(
            ResponsiveDevice.FormFactor form,
            ResponsiveDevice.Orientation orientation) =>
            new ResponsiveDevice.LayoutKey(form, orientation);

        /// <summary>
        /// Independent oracle for <see cref="ResponsiveDevice.SlotAllowedByPlayerAxes"/>: one
        /// literal row per lock mode, in <see cref="ResponsiveWidgetLayout.StatePropertyNames"/>
        /// order (Portrait then Landscape per form). Fold / Fold Cover Half Opened slots follow
        /// the lock like other slots (their label names the posture). iPhone Duo Seated (both slots) always shows. Wide Screen ignores the lock.
        /// </summary>
        public static bool ExpectedSlotAllowed(int slotIndex, bool portrait, bool landscape)
        {
            // Slot order: Phone, Pad, Fold Cover, Fold Half Opened, Fold Cover Half Opened,
            // Flip Cover, Flip Half Opened, iPhone Duo Cover, iPhone Duo Seated, Wide Screen.
            const string landscapeOnly = "01010101010101011111";
            const string portraitOnly = "10101010101010101111";
            if (portrait && landscape)
                return true;
            string row = landscape ? landscapeOnly : portrait ? portraitOnly : "00000000000000001111";
            return row[slotIndex] == '1';
        }

        /// <summary>
        /// Runs <paramref name="body"/> as <paramref name="platform"/>'s build target with
        /// <see cref="ResponsivePlatforms.ConstrainsForms"/> on (the assembly setup turns it off), then
        /// restores the unconstrained test state.
        /// </summary>
        public static void WithPlatform(ResponsivePlatform platform, Action body)
        {
            ResponsiveDevice.ClearOverride();
            ResponsiveDevice.ClearSimulatorLayout();
            ResponsivePlatforms.SetOverride(platform);
            ResponsivePlatforms.ConstrainsForms = true;
            try
            {
                body();
            }
            finally
            {
                ResponsivePlatforms.ConstrainsForms = false;
                ResponsivePlatforms.SetOverride(null);
                ResponsiveDevice.ClearOverride();
                ResponsiveDevice.ClearSimulatorLayout();
            }
        }

        /// <summary>
        /// Runs <paramref name="body"/> with Auto Rotation allowing only the given Player axes, then
        /// restores every orientation value Player Settings had (the orientation watcher is
        /// event-driven, so it sees no change after the restore).
        /// </summary>
        public static void WithPlayerAxes(bool portrait, bool landscape, Action body)
        {
            UIOrientation defaultOrientation = PlayerSettings.defaultInterfaceOrientation;
            bool portraitUp = PlayerSettings.allowedAutorotateToPortrait;
            bool portraitDown = PlayerSettings.allowedAutorotateToPortraitUpsideDown;
            bool landscapeLeft = PlayerSettings.allowedAutorotateToLandscapeLeft;
            bool landscapeRight = PlayerSettings.allowedAutorotateToLandscapeRight;
            try
            {
                PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
                SetAutorotation(portrait, false, landscape, landscape);
                body();
            }
            finally
            {
                // Default Orientation first: a project with a fixed one can store every flag off,
                // which Unity rejects (forcing Portrait back on) while AutoRotation is still active.
                PlayerSettings.defaultInterfaceOrientation = defaultOrientation;
                SetAutorotation(portraitUp, portraitDown, landscapeLeft, landscapeRight);
            }
        }

        /// <summary>
        /// Enables before it disables: Unity forces Portrait back on (and logs an error) whenever
        /// every autorotation flag is off, which would leave Player Settings changed.
        /// </summary>
        static void SetAutorotation(bool portrait, bool portraitDown, bool landscapeLeft, bool landscapeRight)
        {
            if (portrait)
                PlayerSettings.allowedAutorotateToPortrait = true;
            if (portraitDown)
                PlayerSettings.allowedAutorotateToPortraitUpsideDown = true;
            if (landscapeLeft)
                PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            if (landscapeRight)
                PlayerSettings.allowedAutorotateToLandscapeRight = true;
            if (!portrait)
                PlayerSettings.allowedAutorotateToPortrait = false;
            if (!portraitDown)
                PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            if (!landscapeLeft)
                PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            if (!landscapeRight)
                PlayerSettings.allowedAutorotateToLandscapeRight = false;
        }

        /// <summary>
        /// New / seeded rows: Player-allowed slots Enabled except Flip Cover Landscape.
        /// </summary>
        public static bool ExpectedSeedEnabled(int slotIndex) =>
            ResponsiveUiEditorSettings.SlotAllowed(slotIndex) &&
            !ResponsiveWidgetLayout.IsSeedDisabledSlot(slotIndex);

        /// <summary>
        /// After a full seed from Phone, which Phone orientation a seeded slot holds: the first
        /// other-form fallback candidate that is seed-Enabled or Phone (same walk as
        /// <see cref="ResponsiveRectStateEditorUtil.FindFallbackSource"/>, the seed chain). Seeded Pad
        /// slots hold Phone of their own orientation, so iPhone Duo and Wide Screen resolve by shape.
        /// </summary>
        public static bool ExpectedSeedSourceIsLandscape(int slotIndex)
        {
            var key = new ResponsiveDevice.LayoutKey(
                ResponsiveWidgetLayout.SlotFormFactor(slotIndex),
                ResponsiveWidgetLayout.SlotOrientation(slotIndex));
            for (int i = 1; i < ResponsiveWidgetSlotResolver.MaxCandidates; i++)
            {
                if (!ResponsiveWidgetSlotResolver.GetSeedCandidate(
                        key, i, out var form, out var orientation) ||
                    form == key.Form)
                    continue;
                if (form == ResponsiveDevice.FormFactor.Phone ||
                    ExpectedSeedEnabled(ResponsiveWidgetLayout.StatePropertyIndex(
                        new ResponsiveDevice.LayoutKey(form, orientation))))
                    return orientation == ResponsiveDevice.Orientation.Landscape;
            }

            return key.Orientation == ResponsiveDevice.Orientation.Landscape;
        }

        public static void AssertSlotEnabled(
            SerializedProperty slot,
            bool expected,
            string message) =>
            Assert.AreEqual(
                expected,
                slot.FindPropertyRelative("enabled").boolValue,
                message);

        public static void SetHubOverride(
            ResponsiveUI hub,
            ResponsiveDevice.FormFactor form,
            ResponsiveDevice.Orientation orientation)
        {
            var so = new SerializedObject(hub);
            so.FindProperty("overrideInEditor").boolValue = true;
            so.FindProperty("editorFormFactor").enumValueIndex = (int)form;
            so.FindProperty("editorOrientation").enumValueIndex = (int)orientation;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static ResponsiveScrollLayout DistinctScrollMetrics() =>
            new ResponsiveScrollLayout
            {
                phonePortrait = ResponsiveScrollState.From(0.11f, 0.21f),
                phoneLandscape = ResponsiveScrollState.From(0.11f, 0.21f),
                padPortrait = ResponsiveScrollState.From(0.77f, 0.31f),
                padLandscape = ResponsiveScrollState.From(0.77f, 0.31f),
            };

        public static ResponsiveScrollLayout UniformScrollMetrics(float interval, float offset)
        {
            ResponsiveScrollState state = ResponsiveScrollState.From(interval, offset);
            return new ResponsiveScrollLayout
            {
                phonePortrait = state,
                phoneLandscape = state,
                padPortrait = state,
                padLandscape = state,
            };
        }

        /// <summary>
        /// Fills a controller's serialized lists, then binds it to <paramref name="hub"/>
        /// (Apply runs when the controller is enabled).
        /// </summary>
        public static void SetControllerLists(
            ResponsiveUiController controller,
            ResponsiveWidgetLayout[] widgets,
            ResponsiveBackground[] backgrounds,
            ResponsiveActiveObjectEntry[] activeObjects,
            ResponsiveUI hub)
        {
            SetPrivateField(controller, "widgets", widgets ?? Array.Empty<ResponsiveWidgetLayout>());
            SetPrivateField(controller, "backgrounds", backgrounds ?? Array.Empty<ResponsiveBackground>());
            SetPrivateField(
                controller,
                "activeObjects",
                activeObjects ?? Array.Empty<ResponsiveActiveObjectEntry>());
            controller.AssignHub(hub);
        }

        static void SetPrivateField(object target, string name, object value)
        {
            System.Reflection.FieldInfo field = target.GetType().GetField(
                name,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(field, name);
            field.SetValue(target, value);
        }

        public static void Run(GameObject root, Action body)
        {
            try
            {
                body();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
    }
}
