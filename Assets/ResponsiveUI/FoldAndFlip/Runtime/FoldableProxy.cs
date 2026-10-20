using System;
using System.Globalization;
using UnityEngine;

namespace FDPlus.Foldable
{
    /// <summary>
    /// Android Jetpack WindowManager bridge. Construct only on Android player builds.
    /// Mirrors <c>FoldableHelper.FoldableLayoutInfo</c> into <see cref="FoldableLayoutState"/>.
    /// </summary>
    public class FoldableProxy : AndroidJavaProxy
    {
        #region Constants

        const string ListenerClass =
            "com.futuredigitalplus.foldandflip.FoldableHelper$WindowInfoLayoutListener";

        #endregion

        #region Runtime state

        readonly object snapshotLock = new object();
        FoldableLayoutState latestSnapshot;
        bool latestAwaitingFresh;
        int publishedVersion;
        int consumedVersion;

        // One warning per session when Java and C# payload formats drift apart.
        static bool s_LoggedRejectedPayload;

        // Version 1 of FoldableLayoutInfo.toPayload. Keep in lockstep with the Java writer.
        const int PayloadVersion = 1;
        const int PayloadFieldCount = 29;

        #endregion

        #region Detection

        public FoldableProxy() : base(ListenerClass)
        {
        }

        // Called from Java FoldableHelper.WindowInfoLayoutListener. The argument is a packed
        // string so this callback does not call back into JNI (that re-entry ANRs on the main thread).
        public void onChanged(string payload)
        {
            if (!ParseCallbackPayload(payload, out FoldableLayoutState snapshot, out bool awaitingFresh))
            {
                LogRejectedPayload(payload);
                return;
            }

            // No peak merge here: Java's session peak only grows while attached, and the detector's
            // MergeNativeSnapshot keeps the larger peak across consumed snapshots.
            lock (snapshotLock)
            {
                latestSnapshot = snapshot;
                latestAwaitingFresh = awaitingFresh;
                publishedVersion++;
            }
        }

        /// <summary>
        /// Parses <c>FoldableLayoutInfo.toPayload</c> (version 1). Hinge bounds are converted
        /// from Android top-left origin here.
        /// </summary>
        public static bool ParseCallbackPayload(
            string payload,
            out FoldableLayoutState snapshot,
            out bool awaitingFresh)
        {
            snapshot = null;
            awaitingFresh = false;
            if (string.IsNullOrEmpty(payload))
                return false;

            string[] parts = payload.Split(',');
            if (parts.Length != PayloadFieldCount)
                return false;

            if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int version) ||
                version != PayloadVersion)
                return false;

            int[] values = new int[PayloadFieldCount];
            for (int i = 0; i < PayloadFieldCount; i++)
            {
                if (!int.TryParse(parts[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out values[i]))
                    return false;
            }

            RectInt currentMetrics = ReadPackedRect(values, 9);
            snapshot = new FoldableLayoutState
            {
                foldBounds = FoldableLayoutState.FromAndroidTopOrigin(ReadPackedRect(values, 25), currentMetrics.height),
                currentMetrics = currentMetrics,
                maxMetrics = ReadPackedRect(values, 13),
                peakMaxMetrics = ReadPackedRect(values, 17),
                largestDisplayMetrics = ReadPackedRect(values, 21),
                orientation = (FoldableOrientation)values[6],
                state = (FoldableState)values[7],
                occlusionType = (FoldableOcclusionType)values[8],
                isSeparating = values[5] != 0,
                isCoverScreen = values[4] != 0,
                isInMultiWindowMode = values[2] != 0,
                isInPictureInPictureMode = values[3] != 0,
                productState = FoldableProductState.Undefined,
                needsUpdate = true
            };
            awaitingFresh = values[1] != 0;
            return true;
        }

        #endregion

        #region Helpers

        static void LogRejectedPayload(string payload)
        {
            if (s_LoggedRejectedPayload)
                return;
            s_LoggedRejectedPayload = true;

            string[] parts = string.IsNullOrEmpty(payload) ? Array.Empty<string>() : payload.Split(',');
            string head = parts.Length > 0 ? parts[0] : "<empty>";
            Debug.LogWarning(
                $"FoldAndFlip: rejected Android payload (fields={parts.Length}, version={head}; " +
                $"expected {PayloadFieldCount} v{PayloadVersion}). FoldableHelper.toPayload and FoldableProxy are out of sync.");
        }

        /// <param name="awaitingFreshLayout">
        /// True when Java published cached / empty folding data after (re)registration, before the
        /// first fresh WindowLayoutInfo (see <see cref="FoldableFreshLayoutGate"/>).
        /// </param>
        public bool ConsumeLatest(out FoldableLayoutState snapshot, out bool awaitingFreshLayout)
        {
            lock (snapshotLock)
            {
                if (latestSnapshot == null || consumedVersion == publishedVersion)
                {
                    snapshot = null;
                    awaitingFreshLayout = false;
                    return false;
                }

                consumedVersion = publishedVersion;
                snapshot = latestSnapshot.CreateSnapshot();
                awaitingFreshLayout = latestAwaitingFresh;
                return true;
            }
        }

        static RectInt ReadPackedRect(int[] values, int index) =>
            new RectInt(values[index], values[index + 1], values[index + 2], values[index + 3]);

        #endregion
    }

    /// <summary>
    /// Resume gate for native snapshots. After the Android bridge (re)registers, Java publishes the
    /// cached (possibly pre-pause) or empty folding data flagged <c>awaitingFreshLayout</c>. Merging
    /// it would flicker Expand→Undefined→Expand or replay a stale posture, so such snapshots are
    /// held (the previous product stays published) until a fresh snapshot arrives or
    /// <see cref="TimeoutSeconds"/> elapses, after which the newest held snapshot is released and
    /// normal heuristics resume. With no fresh baseline yet (cold start) nothing is held.
    /// Pure C# (Editor-testable); the Android detector feeds it unscaled time.
    /// </summary>
    public sealed class FoldableFreshLayoutGate
    {
        public const float DefaultTimeoutSeconds = 0.5f;

        readonly float timeoutSeconds;
        FoldableLayoutState pending;
        float holdStartTime;
        bool hasFreshBaseline;
        bool timedOut;

        public FoldableFreshLayoutGate(float timeoutSeconds = DefaultTimeoutSeconds)
        {
            this.timeoutSeconds = timeoutSeconds > 0f ? timeoutSeconds : 0f;
        }

        public float TimeoutSeconds => timeoutSeconds;

        /// <summary>True while an unconfirmed snapshot is being held back. Tests.</summary>
        internal bool IsHolding => pending != null;

        /// <summary>
        /// Offers a consumed snapshot. Returns true when <paramref name="release"/> should be merged
        /// now (the snapshot itself, or nothing is held); false while it is held.
        /// </summary>
        public bool Offer(
            FoldableLayoutState snapshot,
            bool awaitingFreshLayout,
            float now,
            out FoldableLayoutState release)
        {
            release = null;
            if (snapshot == null)
                return false;

            if (!awaitingFreshLayout)
            {
                hasFreshBaseline = true;
                timedOut = false;
                pending = null;
                release = snapshot;
                return true;
            }

            if (!hasFreshBaseline || timedOut)
            {
                pending = null;
                release = snapshot;
                return true;
            }

            if (pending == null)
                holdStartTime = now;
            pending = snapshot;
            return Poll(now, out release);
        }

        /// <summary>Releases the newest held snapshot once the timeout has elapsed.</summary>
        public bool Poll(float now, out FoldableLayoutState release)
        {
            release = null;
            if (pending == null || now - holdStartTime < timeoutSeconds)
                return false;

            release = pending;
            pending = null;
            timedOut = true;
            return true;
        }

        /// <summary>Tracking paused: the next resume starts a new hold episode.</summary>
        public void NotifyTrackingStopped()
        {
            pending = null;
            timedOut = false;
        }

        /// <summary>Bridge detached: forget the baseline (next attach behaves like a cold start).</summary>
        public void Reset()
        {
            pending = null;
            timedOut = false;
            hasFreshBaseline = false;
        }
    }
}
