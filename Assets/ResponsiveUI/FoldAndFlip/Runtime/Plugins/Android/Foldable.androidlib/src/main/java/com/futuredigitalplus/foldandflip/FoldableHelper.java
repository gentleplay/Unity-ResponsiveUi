package com.futuredigitalplus.foldandflip;

import android.app.Activity;
import android.content.Context;
import android.graphics.Point;
import android.graphics.Rect;
import android.hardware.display.DisplayManager;
import android.os.Build;
import android.os.Handler;
import android.os.Looper;
import android.util.Log;
import android.view.Display;

import androidx.core.util.Consumer;
import androidx.window.java.layout.WindowInfoTrackerCallbackAdapter;
import androidx.window.layout.DisplayFeature;
import androidx.window.layout.FoldingFeature;
import androidx.window.layout.WindowInfoTracker;
import androidx.window.layout.WindowLayoutInfo;
import androidx.window.layout.WindowMetricsCalculator;

import java.lang.ref.WeakReference;
import java.util.concurrent.Executor;

/**
 * OEM-agnostic foldable bridge (Jetpack WindowManager).
 *
 * <ul>
 *   <li>{@link FoldingFeature} — {@code FLAT} / {@code HALF_OPENED}</li>
 *   <li>{@link FoldableWindowAreaTracker} — rear-facing transfer ACTIVE</li>
 *   <li>Session peak + largest physical display — size-ratio cover fallback</li>
 * </ul>
 *
 * See Docs/DETECTION.md.
 */
public final class FoldableHelper {
    private static final String TAG = "FoldAndFlip";
    private static final Executor UI_EXECUTOR = FoldableWindowAreaTracker.uiExecutor();
    private static final Object LOCK = new Object();
    private static final String DISPLAY_CATEGORY_REAR = "android.hardware.display.category.REAR";

    private static WindowInfoTrackerCallbackAdapter windowInfoTracker;
    private static WindowMetricsCalculator metricsCalculator;
    private static LayoutInfoCallback layoutInfoCallback;
    private static FoldableWindowAreaTracker windowAreaTracker;

    private static WindowLayoutInfo lastLayoutInfo;
    private static WindowInfoLayoutListener listener;
    private static WeakReference<Activity> boundActivity = new WeakReference<>(null);
    private static boolean started;
    // True from (re)registration until the first fresh WindowLayoutInfo callback. Snapshots
    // published meanwhile carry cached / empty folding data; Unity holds the previous product.
    private static boolean awaitingFreshLayout = true;
    private static int peakMaxWidth;
    private static int peakMaxHeight;

    private FoldableHelper() {
    }

    public interface WindowInfoLayoutListener {
        /**
         * Packed snapshot from {@link FoldableLayoutInfo#toPayload()}. Delivered on the main
         * looper, never nested inside the Unity JNI call that registered the listener.
         */
        void onChanged(String payload);
    }

    private static Handler mainHandler;
    private static final Runnable deliverCallback = new Runnable() {
        @Override
        public void run() {
            WindowInfoLayoutListener target;
            String payload;
            synchronized (LOCK) {
                Activity activity = boundActivity.get();
                target = listener;
                if (target == null || activity == null) {
                    return;
                }
                payload = buildLayoutInfo(lastLayoutInfo, activity).toPayload();
            }
            target.onChanged(payload);
        }
    };

    public static boolean attachNativeListener(
            WindowInfoLayoutListener nativeListener, Activity activity) {
        synchronized (LOCK) {
            if (listener != null && listener != nativeListener) {
                Log.w(TAG, "Android bridge already has an owner; duplicate attachment rejected");
                return false;
            }
            listener = nativeListener;
            configure(activity);
        }
        publish();
        return true;
    }

    public static boolean detachNativeListener(WindowInfoLayoutListener nativeListener) {
        synchronized (LOCK) {
            if (listener != nativeListener) {
                Log.w(TAG, "Ignoring detach from a non-owner listener");
                return false;
            }
            stop();
            resetSessionPeakMetrics();
            listener = null;
            lastLayoutInfo = null;
            awaitingFreshLayout = true;
            if (mainHandler != null) {
                mainHandler.removeCallbacks(deliverCallback);
            }
            layoutInfoCallback = null;
            windowInfoTracker = null;
            metricsCalculator = null;
            windowAreaTracker = null;
            boundActivity.clear();
            return true;
        }
    }

    private static void configure(Activity activity) {
        Activity previous = boundActivity.get();
        if (previous == activity && windowInfoTracker != null) {
            return;
        }
        if (started) {
            stop();
        }
        // Never publish folding data captured for a previous Activity instance.
        lastLayoutInfo = null;
        awaitingFreshLayout = true;
        // Keep session peak across Activity recreate (common on Expand↔Normal). Resetting
        // peak here made cover detection depend only on C# state and failed after cold rebind.
        boundActivity = new WeakReference<>(activity);
        windowInfoTracker = new WindowInfoTrackerCallbackAdapter(
                WindowInfoTracker.Companion.getOrCreate(activity));
        metricsCalculator = WindowMetricsCalculator.Companion.getOrCreate();
        windowAreaTracker = new FoldableWindowAreaTracker(UI_EXECUTOR, active -> publish());
    }

    public static void start(Activity activity) {
        synchronized (LOCK) {
            configure(activity);
            if (started) {
                return;
            }
            // Until the listener delivers, lastLayoutInfo may predate a fold/unfold that happened
            // while stopped. Publish it flagged instead of wiping it (no Expand→Undefined flicker).
            awaitingFreshLayout = true;
            // Fresh callback identity per registration: a delivery already posted for the previous
            // (removed) registration must not count as fresh data.
            layoutInfoCallback = new LayoutInfoCallback(activity);
            windowInfoTracker.addWindowLayoutInfoListener(activity, UI_EXECUTOR, layoutInfoCallback);
            windowAreaTracker.start();
            started = true;
        }
        publish();
    }

    public static void stop() {
        synchronized (LOCK) {
            if (!started) {
                return;
            }
            try {
                windowInfoTracker.removeWindowLayoutInfoListener(layoutInfoCallback);
            } catch (Throwable t) {
                Log.w(TAG, "Failed to remove WindowLayoutInfo listener", t);
            } finally {
                windowAreaTracker.stop();
                started = false;
                // Posture may change while paused (fold/unfold in background). Keep the cached
                // layout but mark it unconfirmed; Unity keeps the previous product until the
                // fresh listener callback (or its own bounded timeout).
                awaitingFreshLayout = true;
            }
        }
    }

    public static void refresh() {
        publish();
    }

    // Always describes the currently bound activity: deliverCallback reads boundActivity at
    // delivery time, and removeCallbacks coalesces bursts into one delivery.
    private static void publish() {
        // Queue rather than call the Unity proxy here. start/refresh run inside
        // AndroidJavaClass.CallStatic; a synchronous proxy that reads Java fields
        // re-enters JNI on the main thread and ANRs (WindowInfoLayoutListener.onChanged).
        Looper main = Looper.getMainLooper();
        if (main == null) {
            return;
        }
        Handler handler;
        synchronized (LOCK) {
            if (listener == null) {
                return;
            }
            if (mainHandler == null) {
                mainHandler = new Handler(main);
            }
            handler = mainHandler;
        }
        handler.removeCallbacks(deliverCallback);
        handler.post(deliverCallback);
    }

    private static FoldableLayoutInfo buildLayoutInfo(WindowLayoutInfo layoutInfo, Activity activity) {
        FoldableLayoutInfo out = new FoldableLayoutInfo();
        out.awaitingFreshLayout = awaitingFreshLayout;
        out.isInMultiWindowMode =
                Build.VERSION.SDK_INT >= Build.VERSION_CODES.N && activity.isInMultiWindowMode();
        out.isInPictureInPictureMode =
                Build.VERSION.SDK_INT >= Build.VERSION_CODES.O && activity.isInPictureInPictureMode();
        fillMetrics(out, activity);
        fillLargestDisplay(out, activity);
        fillFoldingFeature(out, layoutInfo);
        // Expand/Flex→Normal: Unity Screen can update before WindowInfoTracker clears the hinge.
        // A stale FLAT/HALF_OPENED in lastLayoutInfo must not win over cover-sized metrics, but
        // only when the hinge also no longer spans the window (letterboxed live postures stay).
        if (out.state != FoldableLayoutInfo.UNDEFINED && isStaleFoldingFeatureForCover(out)) {
            clearFoldingFeature(out);
        }
        out.isRearDisplayActive =
                windowAreaTracker != null && windowAreaTracker.isRearDisplayActive();
        out.isCoverOrRearDisplay = out.isRearDisplayActive && corroboratesRearDisplay(out);
        if (out.state != FoldableLayoutInfo.UNDEFINED) {
            notePeak(out.currentMetrics);
            notePeak(out.maxMetrics);
        }
        // Always remember the largest BUILT-IN display as a cover-ratio reference (survives
        // cover-only DisplayManager lists after Activity recreate when FoldingFeature is gone).
        // External / virtual / presentation displays never reach the session peak.
        notePeak(out.largestDisplayMetrics);
        out.peakMaxMetrics = new Rect(0, 0, peakMaxWidth, peakMaxHeight);
        return out;
    }

    /**
     * True when a FoldingFeature is lagging behind a fold-closed window (Expand/Flex→Normal).
     * Requires BOTH a cover-sized current window (vs session peak / built-in display) AND a
     * feature that no longer spans the current window along its hinge axis (same length AND centre
     * inside the window-relative bounds). Jetpack always
     * reports a live feature spanning the window it was computed for, so a letterboxed /
     * size-compat window with a live FLAT or HALF_OPENED keeps its feature; a stale inner-display
     * feature does not fit the smaller cover window. Area alone is never enough.
     */
    static boolean isStaleFoldingFeatureForCover(FoldableLayoutInfo out) {
        if (out.isInMultiWindowMode || out.isInPictureInPictureMode) {
            return false;
        }
        if (!isCoverSized(out)) {
            return false;
        }
        return !featureSpansWindow(out.hingeOrientation, out.bounds, out.currentMetrics);
    }

    static boolean featureSpansWindow(int hingeOrientation, Rect feature, Rect window) {
        if (feature == null || window == null) {
            return true;
        }
        int featureLength = hingeOrientation == FoldableLayoutInfo.HINGE_ORIENTATION_HORIZONTAL
                ? feature.width()
                : feature.height();
        int windowLength = hingeOrientation == FoldableLayoutInfo.HINGE_ORIENTATION_HORIZONTAL
                ? window.width()
                : window.height();
        if (featureLength <= 0 || windowLength <= 0) {
            // Unknown geometry: do not discard a live posture.
            return true;
        }
        if (featureLength != windowLength) {
            return false;
        }
        // Same length is not enough (Razr+: main short side 1080 == cover side 1080). Feature
        // bounds are window-relative; a live feature lies inside the window, a stale main-display
        // hinge (centre ~1320) falls beyond a 1272 px cover edge.
        return isCentreInside(feature.left, feature.right, window.width())
                && isCentreInside(feature.top, feature.bottom, window.height());
    }

    private static boolean isCentreInside(int start, int end, int windowLength) {
        long doubledCentre = (long) start + end;
        return doubledCentre >= 0 && doubledCentre <= 2L * windowLength;
    }

    private static boolean isCoverSized(FoldableLayoutInfo out) {
        long currentArea = area(out.currentMetrics);
        if (currentArea <= 0) {
            return false;
        }
        // Built-in display (same pixel space as WindowMetrics) and session peak only. The
        // (possibly sandboxed) max window metrics are not a reference here.
        long refArea = Math.max(
                (long) peakMaxWidth * peakMaxHeight, area(out.largestDisplayMetrics));
        if (refArea <= 0) {
            return false;
        }
        // Same 70% area rule as C# FoldableCoverHeuristics / rear corroboration.
        return currentArea * 10 < refArea * 7;
    }

    private static void clearFoldingFeature(FoldableLayoutInfo out) {
        out.state = FoldableLayoutInfo.UNDEFINED;
        out.hingeOrientation = FoldableLayoutInfo.UNDEFINED;
        out.occlusionType = FoldableLayoutInfo.UNDEFINED;
        out.isSeparating = false;
        out.bounds = new Rect();
    }

    private static boolean corroboratesRearDisplay(FoldableLayoutInfo out) {
        if (out.isInMultiWindowMode || out.isInPictureInPictureMode) {
            return false;
        }
        long currentArea = area(out.currentMetrics);
        long referenceArea = Math.max(
                (long) peakMaxWidth * peakMaxHeight,
                Math.max(area(out.maxMetrics), area(out.largestDisplayMetrics)));
        return currentArea > 0 && referenceArea > 0
                && currentArea * 10 < referenceArea * 7;
    }

    private static long area(Rect bounds) {
        if (bounds == null) {
            return 0;
        }
        return (long) Math.max(0, bounds.width()) * Math.max(0, bounds.height());
    }

    private static void fillMetrics(FoldableLayoutInfo out, Activity activity) {
        if (metricsCalculator == null || activity == null) {
            return;
        }
        out.currentMetrics = metricsCalculator.computeCurrentWindowMetrics(activity).getBounds();
        out.maxMetrics = metricsCalculator.computeMaximumWindowMetrics(activity).getBounds();
    }

    /**
     * Largest BUILT-IN display in the same (logical) pixel space as WindowMetrics: the default
     * display and (API 34+) rear displays only. The Activity's own display is not used when it
     * differs (DeX / external / virtual / presentation), so a connected monitor never becomes a
     * cover reference.
     */
    private static void fillLargestDisplay(FoldableLayoutInfo out, Activity activity) {
        out.largestDisplayMetrics = new Rect();
        if (activity == null) {
            return;
        }

        try {
            DisplayManager dm = (DisplayManager) activity.getSystemService(Context.DISPLAY_SERVICE);
            if (dm == null) {
                return;
            }

            Point best = new Point();
            Display builtIn = dm.getDisplay(Display.DEFAULT_DISPLAY);
            if (builtIn != null) {
                considerDisplay(best, builtIn);
            }
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.UPSIDE_DOWN_CAKE) {
                // Literal, not DisplayManager.DISPLAY_CATEGORY_REAR: that constant is
                // @TestApi/@hide and is not in the public compileSdk android.jar.
                for (Display display : dm.getDisplays(DISPLAY_CATEGORY_REAR)) {
                    if (display != null) {
                        considerDisplay(best, display);
                    }
                }
            }
            if ((long) best.x * best.y > 0) {
                out.largestDisplayMetrics.set(0, 0, best.x, best.y);
            }
        } catch (Throwable t) {
            Log.w(TAG, "largest display metrics unavailable", t);
        }
    }

    @SuppressWarnings("deprecation")
    private static void considerDisplay(Point best, Display display) {
        // Logical (current mode / override) size, never the panel's native physical mode:
        // matches WindowMetrics when a lower resolution or wm size override is active.
        Point size = new Point();
        display.getRealSize(size);
        if ((long) size.x * size.y > (long) best.x * best.y) {
            best.set(size.x, size.y);
        }
    }

    private static void notePeak(Rect metrics) {
        if (metrics == null) {
            return;
        }
        int w = Math.max(0, metrics.width());
        int h = Math.max(0, metrics.height());
        long area = (long) w * h;
        long peak = (long) peakMaxWidth * peakMaxHeight;
        if (area > peak) {
            peakMaxWidth = w;
            peakMaxHeight = h;
        }
    }

    private static void resetSessionPeakMetrics() {
        peakMaxWidth = 0;
        peakMaxHeight = 0;
    }

    private static void fillFoldingFeature(FoldableLayoutInfo out, WindowLayoutInfo layoutInfo) {
        if (layoutInfo == null) {
            return;
        }

        FoldingFeature selected = null;
        for (DisplayFeature feature : layoutInfo.getDisplayFeatures()) {
            if (feature instanceof FoldingFeature
                    && isPreferred((FoldingFeature) feature, selected, out.currentMetrics)) {
                selected = (FoldingFeature) feature;
            }
        }
        if (selected == null) {
            return;
        }

        out.hingeOrientation = selected.getOrientation() == FoldingFeature.Orientation.HORIZONTAL
                    ? FoldableLayoutInfo.HINGE_ORIENTATION_HORIZONTAL
                    : FoldableLayoutInfo.HINGE_ORIENTATION_VERTICAL;
        out.state = selected.getState() == FoldingFeature.State.FLAT
                    ? FoldableLayoutInfo.STATE_FLAT
                    : FoldableLayoutInfo.STATE_HALF_OPENED;
        out.occlusionType = selected.getOcclusionType() == FoldingFeature.OcclusionType.NONE
                    ? FoldableLayoutInfo.OCCLUSION_TYPE_NONE
                    : FoldableLayoutInfo.OCCLUSION_TYPE_FULL;
        out.isSeparating = selected.isSeparating();
        out.bounds = selected.getBounds();
    }

    // Select one stable primary hinge when OEMs expose multiple FoldingFeatures.
    private static boolean isPreferred(
            FoldingFeature candidate, FoldingFeature current, Rect windowBounds) {
        if (current == null) {
            return true;
        }
        int comparison = Integer.compare(featurePriority(candidate), featurePriority(current));
        if (comparison != 0) {
            return comparison > 0;
        }

        Rect candidateBounds = candidate.getBounds();
        Rect currentBounds = current.getBounds();
        comparison = Integer.compare(
                Math.max(candidateBounds.width(), candidateBounds.height()),
                Math.max(currentBounds.width(), currentBounds.height()));
        if (comparison != 0) {
            return comparison > 0;
        }

        comparison = Long.compare(area(candidateBounds), area(currentBounds));
        if (comparison != 0) {
            return comparison > 0;
        }

        comparison = Long.compare(
                distanceFromCenter(currentBounds, windowBounds),
                distanceFromCenter(candidateBounds, windowBounds));
        if (comparison != 0) {
            return comparison > 0;
        }
        if (candidateBounds.top != currentBounds.top) {
            return candidateBounds.top < currentBounds.top;
        }
        if (candidateBounds.left != currentBounds.left) {
            return candidateBounds.left < currentBounds.left;
        }
        if (candidateBounds.bottom != currentBounds.bottom) {
            return candidateBounds.bottom < currentBounds.bottom;
        }
        return candidateBounds.right < currentBounds.right;
    }

    private static int featurePriority(FoldingFeature feature) {
        int priority = feature.getState() == FoldingFeature.State.HALF_OPENED ? 4 : 0;
        priority += feature.isSeparating() ? 2 : 0;
        priority += feature.getOcclusionType() == FoldingFeature.OcclusionType.FULL ? 1 : 0;
        return priority;
    }

    private static long distanceFromCenter(Rect feature, Rect window) {
        long dx = Math.abs((long) feature.left + feature.right - window.left - window.right);
        long dy = Math.abs((long) feature.top + feature.bottom - window.top - window.bottom);
        return dx + dy;
    }

    private static final class LayoutInfoCallback implements Consumer<WindowLayoutInfo> {
        private final WeakReference<Activity> activity;

        LayoutInfoCallback(Activity activity) {
            this.activity = new WeakReference<>(activity);
        }

        @Override
        public void accept(WindowLayoutInfo windowLayoutInfo) {
            Activity target = activity.get();
            synchronized (LOCK) {
                if (!started || this != layoutInfoCallback || target != boundActivity.get()) {
                    return;
                }
                lastLayoutInfo = windowLayoutInfo;
                awaitingFreshLayout = false;
            }
            publish();
        }
    }

    /** Payload mirrored into Unity {@code FoldableProxy}. */
    public static final class FoldableLayoutInfo {
        public static final int UNDEFINED = -1;
        public static final int HINGE_ORIENTATION_HORIZONTAL = 0;
        public static final int HINGE_ORIENTATION_VERTICAL = 1;
        public static final int STATE_FLAT = 0;
        public static final int STATE_HALF_OPENED = 1;
        public static final int OCCLUSION_TYPE_NONE = 0;
        public static final int OCCLUSION_TYPE_FULL = 1;

        public Rect currentMetrics = new Rect();
        public Rect maxMetrics = new Rect();
        public Rect peakMaxMetrics = new Rect();
        public Rect largestDisplayMetrics = new Rect();
        public int hingeOrientation = UNDEFINED;
        public int state = UNDEFINED;
        public int occlusionType = UNDEFINED;
        public boolean isSeparating;
        public Rect bounds = new Rect();
        public boolean isCoverOrRearDisplay;
        public boolean isRearDisplayActive;
        public boolean isInMultiWindowMode;
        public boolean isInPictureInPictureMode;
        /** True while the folding data is cached / empty and not yet confirmed after (re)start. */
        public boolean awaitingFreshLayout;

        /**
         * Comma-separated ints, version 1. Order is the contract with
         * {@code FoldableProxy.ParseCallbackPayload}.
         */
        String toPayload() {
            StringBuilder sb = new StringBuilder(160);
            sb.append('1');
            appendFlag(sb, awaitingFreshLayout);
            appendFlag(sb, isInMultiWindowMode);
            appendFlag(sb, isInPictureInPictureMode);
            appendFlag(sb, isCoverOrRearDisplay);
            appendFlag(sb, isSeparating);
            sb.append(',').append(hingeOrientation);
            sb.append(',').append(state);
            sb.append(',').append(occlusionType);
            appendRect(sb, currentMetrics);
            appendRect(sb, maxMetrics);
            appendRect(sb, peakMaxMetrics);
            appendRect(sb, largestDisplayMetrics);
            appendRect(sb, bounds);
            return sb.toString();
        }

        private static void appendFlag(StringBuilder sb, boolean value) {
            sb.append(',').append(value ? '1' : '0');
        }

        private static void appendRect(StringBuilder sb, Rect rect) {
            if (rect == null) {
                sb.append(",-1,-1,-1,-1");
                return;
            }
            sb.append(',').append(rect.left);
            sb.append(',').append(rect.top);
            sb.append(',').append(rect.width());
            sb.append(',').append(rect.height());
        }
    }
}
