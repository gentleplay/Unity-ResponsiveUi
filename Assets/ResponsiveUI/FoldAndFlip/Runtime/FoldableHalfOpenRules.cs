using System;
using UnityEngine;

namespace FDPlus.Foldable
{
    /// <summary>Which way the content runs on a half-opened screen.</summary>
    public enum FoldableContentRuns
    {
        /// <summary>The shape the OS gives (official Android / Samsung behaviour).</summary>
        AsAndroid = 0,
        /// <summary>Content runs top to down: a tall screen, the hinge across it.</summary>
        TopToDown = 1,
        /// <summary>Content runs left to right: a wide screen.</summary>
        LeftToRight = 2,
    }

    /// <summary>What turning the device does while half opened.</summary>
    public enum FoldableHalfOpenRotation
    {
        /// <summary>What Android does with the Player Settings orientation.</summary>
        AsAndroid = 0,
        /// <summary>The screen keeps one shape while the device turns.</summary>
        Locked = 1,
        /// <summary>The screen follows the device as it turns.</summary>
        FollowDevice = 2,
    }

    /// <summary>One half-open card: how the content runs and what turning the device does.</summary>
    [Serializable]
    public sealed class FoldableHalfOpenPose
    {
        [Tooltip("Which way the content runs while half opened. As Android: the shape the OS gives. Top to down: always tall, e.g. a landscape game gets its tall layout. Left to right: always wide.")]
        public FoldableContentRuns contentRuns = FoldableContentRuns.AsAndroid;

        [Tooltip("Turning the device while half opened. As Android: Player Settings decide. Locked: the screen keeps its shape, e.g. held tall it stays tall. Follow device: the screen turns with the device.")]
        public FoldableHalfOpenRotation whileRotating = FoldableHalfOpenRotation.AsAndroid;

        /// <summary>True when this card behaves exactly like the platform.</summary>
        public bool IsDefault =>
            contentRuns == FoldableContentRuns.AsAndroid && whileRotating == FoldableHalfOpenRotation.AsAndroid;
    }

    /// <summary>
    /// How the game behaves on Android half-open postures: Fold, Fold Cover and Flip each have a card.
    /// Edited in Project Settings › Responsive UI (saved by <see cref="FoldableSettings"/>); the defaults are
    /// the platform's own, so a project that never saved anything behaves as Android does.
    /// </summary>
    [Serializable]
    public sealed class FoldableHalfOpenRules
    {
        [Tooltip("Android Fold (inner display) half opened.")]
        public FoldableHalfOpenPose fold = new FoldableHalfOpenPose();

        [Tooltip("Android Fold with a Cover display half opened.")]
        public FoldableHalfOpenPose foldCover = new FoldableHalfOpenPose();

        [Tooltip("Android Flip half opened.")]
        public FoldableHalfOpenPose flip = new FoldableHalfOpenPose();

        /// <summary>The card for <paramref name="kind"/>: Flip, else Fold Cover or Fold.</summary>
        public FoldableHalfOpenPose For(FoldableDeviceKind kind, bool foldCover)
        {
            if (kind == FoldableDeviceKind.FlipablePhone)
                return flip;
            return foldCover ? this.foldCover : fold;
        }

        public FoldableHalfOpenRules Clone() => new FoldableHalfOpenRules
        {
            fold = Copy(fold),
            foldCover = Copy(foldCover),
            flip = Copy(flip),
        };

        static FoldableHalfOpenPose Copy(FoldableHalfOpenPose pose) =>
            new FoldableHalfOpenPose { contentRuns = pose.contentRuns, whileRotating = pose.whileRotating };

        /// <summary>
        /// The half-open screen shape under <paramref name="pose"/> (true: wide), or null when it follows the
        /// device's rotation. <paramref name="lockedLandscape"/> is the Player Settings one-axis lock
        /// (true: landscape only, false: portrait only, null: both axes); <paramref name="entryLandscape"/>
        /// is the shape the screen had when half open began. The OS shape under a one-axis lock is the
        /// lock's own on a Flip and its opposite on a Fold.
        /// </summary>
        public static bool? ScreenLandscape(FoldableHalfOpenPose pose, bool isFold, bool? lockedLandscape, bool entryLandscape)
        {
            if (pose.contentRuns == FoldableContentRuns.TopToDown)
                return false;
            if (pose.contentRuns == FoldableContentRuns.LeftToRight)
                return true;

            var rotation = pose.whileRotating;
            if (rotation == FoldableHalfOpenRotation.AsAndroid)
                rotation = lockedLandscape.HasValue ? FoldableHalfOpenRotation.Locked : FoldableHalfOpenRotation.FollowDevice;
            if (rotation == FoldableHalfOpenRotation.FollowDevice)
                return null;

            if (lockedLandscape.HasValue)
                return isFold ? !lockedLandscape.Value : lockedLandscape.Value;
            return entryLandscape;
        }

        /// <summary>
        /// True when the card needs the runtime to hold the orientation itself, because Android alone would
        /// not give this shape under the Player Settings lock <paramref name="lockedLandscape"/>.
        /// </summary>
        public static bool NeedsRuntimeHold(FoldableHalfOpenPose pose, bool? lockedLandscape)
        {
            if (pose.contentRuns != FoldableContentRuns.AsAndroid)
                return true;
            var locked = lockedLandscape.HasValue;
            return (pose.whileRotating == FoldableHalfOpenRotation.Locked && !locked)
                || (pose.whileRotating == FoldableHalfOpenRotation.FollowDevice && locked);
        }
    }
}
