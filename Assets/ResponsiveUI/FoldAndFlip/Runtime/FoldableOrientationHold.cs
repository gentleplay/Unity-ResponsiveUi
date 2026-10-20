using System;
using UnityEngine;

namespace FDPlus.Foldable
{
    /// <summary>What the Android half-open hold asks of the screen.</summary>
    public readonly struct FoldableOrientationRequest
    {
        /// <summary>True while the runtime holds the orientation; false hands it back to Player Settings.</summary>
        public readonly bool hold;

        /// <summary>
        /// The axis held under Auto Rotation: true Portrait + Portrait Upside Down, false Landscape Left +
        /// Right, null both axes (Follow device under a one-axis lock). Unused when <see cref="hold"/> is false.
        /// </summary>
        public readonly bool? tallAxis;

        public FoldableOrientationRequest(bool hold, bool? tallAxis)
        {
            this.hold = hold;
            this.tallAxis = tallAxis;
        }
    }

    /// <summary>
    /// Android Fold / Fold Cover / Flip half open with a changed card (<see cref="FoldableSettings.Rules"/>):
    /// holds the screen axis the card asks for when Android alone would not give it
    /// (<see cref="FoldableHalfOpenRules.NeedsRuntimeHold"/>), and restores the Player Settings orientation
    /// once half open ends. Default cards never hold, and neither does a fixed Default Orientation (cards
    /// apply only under Auto Rotation). A hold is an axis, not one orientation: Auto Rotation with that axis's
    /// two flags, so a 180° turn still shows upright. Android names the axes by aspect, so a tall shape is the
    /// portrait axis whatever the natural orientation; the first hold of a session checks the shape it got
    /// once the screen settled, and swaps the mapping once when the device names them the other way. The decisions are pure
    /// (<see cref="Decide"/>, <see cref="AxisMappingNeedsSwap"/>); the <see cref="Screen"/> writes run on
    /// Android players only, and nothing else in Fold &amp; Flip sets <see cref="Screen.orientation"/>.
    /// </summary>
    public static class FoldableOrientationHold
    {
        /// <summary>
        /// Set by Responsive UI on the player: true while the half-open inner display is Fold Cover size
        /// (Fold8-style), so the Fold Cover card applies. Unset: every Fold uses the Fold card.
        /// </summary>
        public static Func<bool> IsFoldCover;

        #region Decision

        /// <summary>
        /// The hold for <paramref name="pose"/>. <paramref name="playerLockedLandscape"/> and
        /// <paramref name="playerOrientation"/> are the Player Settings captured at startup (the hold changes
        /// what <see cref="Screen"/> reports); <paramref name="entryLandscape"/> is the shape (true: wide) half
        /// open began with. The wanted shape picks its axis by aspect (tall: portrait axis);
        /// <paramref name="axisInverted"/> is the session's one-time swap
        /// (<see cref="AxisMappingNeedsSwap"/>).
        /// </summary>
        public static FoldableOrientationRequest Decide(
            FoldableHalfOpenPose pose,
            bool isFold,
            bool halfOpen,
            bool? playerLockedLandscape,
            ScreenOrientation playerOrientation,
            bool entryLandscape,
            bool axisInverted)
        {
            if (!halfOpen || playerOrientation != ScreenOrientation.AutoRotation ||
                !FoldableHalfOpenRules.NeedsRuntimeHold(pose, playerLockedLandscape))
                return new FoldableOrientationRequest(false, null);

            var wanted = FoldableHalfOpenRules.ScreenLandscape(pose, isFold, playerLockedLandscape, entryLandscape);
            if (!wanted.HasValue)
                return new FoldableOrientationRequest(true, null);
            return new FoldableOrientationRequest(true, wanted.Value == axisInverted);
        }

        /// <summary>
        /// True when the screen under a held axis (<paramref name="heldLandscapeAxis"/>) has neither the wanted
        /// shape (<paramref name="wantedLandscape"/>) nor the held axis's aspect: the device names its axes
        /// the other way, so the mapping is swapped (once per session). A square or unknown size gives no answer.
        /// </summary>
        public static bool AxisMappingNeedsSwap(bool wantedLandscape, bool heldLandscapeAxis, int width, int height)
        {
            if (width <= 0 || height <= 0 || width == height)
                return false;
            bool landscape = width > height;
            return landscape != wantedLandscape && landscape != heldLandscapeAxis;
        }

        /// <summary>Frames the screen size must stay the same before its shape counts as settled.</summary>
        public const int ShapeStableFrames = 10;

        /// <summary>Seconds after a hold write before a settled wrong shape counts.</summary>
        public const float ShapeCheckSeconds = 1.5f;

        /// <summary>True when the screen <paramref name="width"/> × <paramref name="height"/> has the wanted shape.</summary>
        public static bool ShapeIsWanted(bool wantedLandscape, int width, int height) =>
            width > 0 && height > 0 && width != height && (width > height) == wantedLandscape;

        /// <summary>
        /// True when, still half open, the screen kept a shape other than the wanted one for
        /// <see cref="ShapeStableFrames"/> frames (<paramref name="stableFrames"/>) and at least
        /// <see cref="ShapeCheckSeconds"/> passed since the hold write (<paramref name="elapsed"/>). A display
        /// switch or a turn still under way resets the frame count, so it never counts.
        /// </summary>
        public static bool ShapeSettledWrong(bool wantedLandscape, int width, int height, int stableFrames,
            float elapsed, bool halfOpen) =>
            halfOpen && stableFrames >= ShapeStableFrames && elapsed >= ShapeCheckSeconds &&
            width > 0 && height > 0 && width != height && (width > height) != wantedLandscape;

        /// <summary>
        /// The one-time shape check swaps the axis mapping: the wrong shape settled
        /// (<see cref="ShapeSettledWrong"/>) and it is not the held axis's own aspect
        /// (<see cref="AxisMappingNeedsSwap"/>).
        /// </summary>
        public static bool ShapeCheckConcludesSwap(bool wantedLandscape, bool heldLandscapeAxis, int width, int height,
            int stableFrames, float elapsed, bool halfOpen) =>
            ShapeSettledWrong(wantedLandscape, width, height, stableFrames, elapsed, halfOpen) &&
            AxisMappingNeedsSwap(wantedLandscape, heldLandscapeAxis, width, height);

        #endregion

#if UNITY_ANDROID && !UNITY_EDITOR
        #region Android

        const int CheckNone = 0, CheckFirst = 1, CheckSwapped = 2;

        static ScreenOrientation s_PlayerOrientation;
        static bool? s_PlayerLockedLandscape;
        static bool s_Portrait, s_PortraitUpsideDown, s_LandscapeLeft, s_LandscapeRight;

        static bool s_HalfOpen;
        static bool s_EntryLandscape;
        static bool s_EntryPending;
        static bool s_Holding;
        static bool? s_HeldAxis;
        static bool? s_WantedLandscape;
        static FoldableLayoutState s_Last;

        static bool s_AxisInverted;
        static bool s_AxisChecked;
        static int s_Check;
        static float s_WriteTime;

        static bool s_FramesHooked;
        static int s_FrameWidth, s_FrameHeight, s_StableFrames;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            // Player Settings once, before any hold write: the hold changes what Screen reports.
            s_PlayerOrientation = ReadPlayerOrientation();
            s_Portrait = Screen.autorotateToPortrait;
            s_PortraitUpsideDown = Screen.autorotateToPortraitUpsideDown;
            s_LandscapeLeft = Screen.autorotateToLandscapeLeft;
            s_LandscapeRight = Screen.autorotateToLandscapeRight;
            s_PlayerLockedLandscape = PlayerLockedLandscape();

            FoldablePostureSource.LayoutChanged += OnLayoutChanged;
            Application.focusChanged += OnFocusChanged;
            var state = FoldablePostureSource.FindActiveLayoutState();
            if (state != null)
                OnLayoutChanged(state);
        }

        static void OnLayoutChanged(FoldableLayoutState state)
        {
            s_Last = state;
            Apply(Evaluate(state), force: false);
        }

        static void OnFocusChanged(bool focused)
        {
            // Resume: re-decide (half open may have ended while paused) and re-write a kept hold.
            if (!focused)
                return;
            var state = FoldablePostureSource.FindActiveLayoutState() ?? s_Last;
            if (state != null)
            {
                s_Last = state;
                Apply(Evaluate(state), force: true);
            }
        }

        static FoldableOrientationRequest Evaluate(FoldableLayoutState state)
        {
            bool isFold = state.deviceKind == FoldableDeviceKind.FoldablePhone;
            bool halfOpen = state.productState == FoldableProductState.HalfOpened &&
                            (isFold || state.deviceKind == FoldableDeviceKind.FlipablePhone);
            if (halfOpen && !s_HalfOpen)
            {
                // The event can come before Screen shows the inner display: the entry shape waits for a
                // settled screen (OnFrame), and no hold is written until then.
                s_EntryPending = true;
                s_EntryLandscape = Screen.width > Screen.height;
                ResetStableFrames();
                HookFrames();
            }
            else if (!halfOpen)
            {
                s_EntryPending = false;
            }
            s_HalfOpen = halfOpen;

            var pose = FoldableSettings.Rules.For(state.deviceKind, isFold && IsFoldCover?.Invoke() == true);
            s_WantedLandscape = FoldableHalfOpenRules.ScreenLandscape(pose, isFold, s_PlayerLockedLandscape, s_EntryLandscape);
            return Decide(pose, isFold, halfOpen, s_PlayerLockedLandscape, s_PlayerOrientation,
                s_EntryLandscape, s_AxisInverted);
        }

        static void Apply(FoldableOrientationRequest request, bool force)
        {
            if (!request.hold)
            {
                // Nothing held: Player Settings own the screen, never touch it.
                if (!s_Holding)
                    return;
                WriteFlags(s_Portrait, s_PortraitUpsideDown, s_LandscapeLeft, s_LandscapeRight);
                Screen.orientation = s_PlayerOrientation;
                s_Holding = false;
                s_HeldAxis = null;
                return;
            }

            if (s_EntryPending || (s_Holding && s_HeldAxis == request.tallAxis && !force))
                return;
            bool tall = request.tallAxis != false;
            bool wide = request.tallAxis != true;
            WriteFlags(tall, tall, wide, wide);
            Screen.orientation = ScreenOrientation.AutoRotation;
            s_Holding = true;
            s_HeldAxis = request.tallAxis;
            if (request.tallAxis.HasValue && !s_AxisChecked)
                BeginCheck();
        }

        /// <summary>Starts (or re-times) the one-time shape check of an axis hold.</summary>
        static void BeginCheck()
        {
            s_WriteTime = Time.realtimeSinceStartup;
            if (s_Check == CheckNone)
                s_Check = CheckFirst;
            ResetStableFrames();
            HookFrames();
        }

        /// <summary>
        /// Per frame while the entry shape or the shape check is pending: counts frames of an unchanged
        /// screen size, settles the entry shape, and steps the check.
        /// </summary>
        static void OnFrame()
        {
            int width = Screen.width, height = Screen.height;
            if (width == s_FrameWidth && height == s_FrameHeight)
                s_StableFrames++;
            else
            {
                s_FrameWidth = width;
                s_FrameHeight = height;
                s_StableFrames = 0;
            }

            if (s_EntryPending && s_StableFrames >= ShapeStableFrames)
            {
                s_EntryPending = false;
                s_EntryLandscape = width > height;
                if (s_Last != null)
                    Apply(Evaluate(s_Last), force: false);
            }
            if (s_Check != CheckNone)
                StepCheck(width, height);
            if (!s_EntryPending && s_Check == CheckNone)
                UnhookFrames();
        }

        /// <summary>
        /// The wanted shape at any point concludes with no swap. A settled wrong shape swaps the mapping once;
        /// if the swapped axis also settles wrong, the OS ignores the request and the mapping goes back.
        /// At most two holds, so it never loops.
        /// </summary>
        static void StepCheck(int width, int height)
        {
            if (!s_Holding || !s_HeldAxis.HasValue || !s_WantedLandscape.HasValue)
            {
                // Released before an answer: the next hold checks again from the default mapping.
                if (s_Check == CheckSwapped)
                    s_AxisInverted = false;
                s_Check = CheckNone;
                return;
            }

            bool wanted = s_WantedLandscape.Value;
            if (ShapeIsWanted(wanted, width, height))
            {
                s_Check = CheckNone;
                s_AxisChecked = true;
                return;
            }

            float elapsed = Time.realtimeSinceStartup - s_WriteTime;
            if (!ShapeSettledWrong(wanted, width, height, s_StableFrames, elapsed, s_HalfOpen))
                return;

            if (s_Check == CheckFirst &&
                ShapeCheckConcludesSwap(wanted, !s_HeldAxis.Value, width, height, s_StableFrames, elapsed, s_HalfOpen))
            {
                s_AxisInverted = true;
                s_Check = CheckSwapped;
            }
            else
            {
                if (s_Check == CheckSwapped)
                    s_AxisInverted = false;
                s_Check = CheckNone;
                s_AxisChecked = true;
            }
            if (s_Last != null)
                Apply(Evaluate(s_Last), force: true);
        }

        static void ResetStableFrames()
        {
            s_FrameWidth = Screen.width;
            s_FrameHeight = Screen.height;
            s_StableFrames = 0;
        }

        static void HookFrames()
        {
            if (s_FramesHooked)
                return;
            s_FramesHooked = true;
            Application.onBeforeRender += OnFrame;
        }

        static void UnhookFrames()
        {
            if (!s_FramesHooked)
                return;
            s_FramesHooked = false;
            Application.onBeforeRender -= OnFrame;
        }

        static void WriteFlags(bool portrait, bool portraitUpsideDown, bool landscapeLeft, bool landscapeRight)
        {
            // Enable before disable: Unity forces Portrait back on when every flag is off.
            if (portrait) Screen.autorotateToPortrait = true;
            if (portraitUpsideDown) Screen.autorotateToPortraitUpsideDown = true;
            if (landscapeLeft) Screen.autorotateToLandscapeLeft = true;
            if (landscapeRight) Screen.autorotateToLandscapeRight = true;
            if (!portrait) Screen.autorotateToPortrait = false;
            if (!portraitUpsideDown) Screen.autorotateToPortraitUpsideDown = false;
            if (!landscapeLeft) Screen.autorotateToLandscapeLeft = false;
            if (!landscapeRight) Screen.autorotateToLandscapeRight = false;
        }

        static bool? PlayerLockedLandscape()
        {
            if (s_PlayerOrientation != ScreenOrientation.AutoRotation)
                return s_PlayerOrientation == ScreenOrientation.LandscapeLeft ||
                       s_PlayerOrientation == ScreenOrientation.LandscapeRight;
            bool portrait = s_Portrait || s_PortraitUpsideDown;
            bool landscape = s_LandscapeLeft || s_LandscapeRight;
            if (portrait == landscape)
                return null;
            return landscape;
        }

        /// <summary>
        /// Player Settings Default Orientation: fixed or Auto Rotation. An Android player reports the
        /// concrete orientation from <see cref="Screen.orientation"/> even under Auto Rotation, so a fixed one
        /// is read from the activity's requested orientation (Unity requests a sensor / user mode for Auto
        /// Rotation; one allowed orientation reads as fixed, so the hold stays off).
        /// </summary>
        static ScreenOrientation ReadPlayerOrientation()
        {
            if (Screen.orientation == ScreenOrientation.AutoRotation)
                return ScreenOrientation.AutoRotation;
            try
            {
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                {
                    if (activity == null)
                        return Screen.orientation;
                    switch (activity.Call<int>("getRequestedOrientation"))
                    {
                        case 0: return ScreenOrientation.LandscapeLeft;   // SCREEN_ORIENTATION_LANDSCAPE
                        case 1: return ScreenOrientation.Portrait;        // SCREEN_ORIENTATION_PORTRAIT
                        case 8: return ScreenOrientation.LandscapeRight;  // SCREEN_ORIENTATION_REVERSE_LANDSCAPE
                        case 9: return ScreenOrientation.PortraitUpsideDown; // SCREEN_ORIENTATION_REVERSE_PORTRAIT
                        default: return ScreenOrientation.AutoRotation;
                    }
                }
            }
            catch (Exception e)
            {
                // Unknown: treat as fixed so the hold never engages (default behaviour).
                Debug.LogWarning("FoldAndFlip: requested orientation unreadable; half-open hold off. " + e.Message);
                return Screen.orientation;
            }
        }

        #endregion
#endif
    }
}
