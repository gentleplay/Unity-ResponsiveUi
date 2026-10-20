package com.futuredigitalplus.foldandflip;

import android.os.Handler;
import android.os.Looper;
import android.util.Log;

import androidx.core.util.Consumer;
import androidx.window.area.WindowAreaCapability;
import androidx.window.area.WindowAreaController;
import androidx.window.area.WindowAreaInfo;
import androidx.window.core.ExperimentalWindowApi;
import androidx.window.java.area.WindowAreaControllerCallbackAdapter;

import java.util.List;
import java.util.concurrent.Executor;

/**
 * Tracks WindowArea rear-facing transfer ACTIVE (cover / Closed hint).
 */
final class FoldableWindowAreaTracker {
    private static final String TAG = "FoldAndFlip";

    interface Listener {
        void onRearDisplayActiveChanged(boolean active);
    }

    private final Executor executor;
    private final Listener listener;
    // Guards registration state and the ACTIVE flag so a callback's "still registered?" check
    // and its write are atomic with start()/stop(). The listener is invoked outside the lock
    // (it re-enters FoldableHelper.LOCK; stop() runs under that lock and then takes this one).
    private final Object stateLock = new Object();
    private WindowAreaControllerCallbackAdapter adapter;
    private Consumer<List<WindowAreaInfo>> callback;
    private int generation;
    private boolean listening;
    // Written by the main-looper callback and by start()/stop() (Unity's thread); read on the main
    // looper when FoldableHelper builds a snapshot.
    private volatile boolean rearDisplayActive;

    FoldableWindowAreaTracker(Executor executor, Listener listener) {
        this.executor = executor;
        this.listener = listener;
    }

    boolean isRearDisplayActive() {
        return rearDisplayActive;
    }

    @ExperimentalWindowApi
    void start() {
        synchronized (stateLock) {
            if (listening) {
                return;
            }

            // Never carry an ACTIVE flag across a stop/start; wait for this registration's value.
            rearDisplayActive = false;
            final int registration = ++generation;
            try {
                adapter = new WindowAreaControllerCallbackAdapter(WindowAreaController.getOrCreate());
                callback = infos -> onWindowAreasChanged(registration, infos);
                listening = true;
                adapter.addWindowAreaInfoListListener(executor, callback);
            } catch (Throwable t) {
                Log.w(TAG, "WindowArea unavailable; rear-display Closed flag disabled", t);
                adapter = null;
                callback = null;
                listening = false;
                generation++;
            }
        }
    }

    @ExperimentalWindowApi
    void stop() {
        WindowAreaControllerCallbackAdapter oldAdapter;
        Consumer<List<WindowAreaInfo>> oldCallback;
        synchronized (stateLock) {
            oldAdapter = listening ? adapter : null;
            oldCallback = listening ? callback : null;
            listening = false;
            generation++;
            callback = null;
            adapter = null;
            rearDisplayActive = false;
        }

        if (oldAdapter == null || oldCallback == null) {
            return;
        }
        try {
            oldAdapter.removeWindowAreaInfoListListener(oldCallback);
        } catch (Throwable t) {
            Log.w(TAG, "Failed to remove WindowArea listener", t);
        }
    }

    @ExperimentalWindowApi
    private void onWindowAreasChanged(int registration, List<WindowAreaInfo> infos) {
        boolean active = isRearTransferActive(infos);
        synchronized (stateLock) {
            // Callback posted for a removed registration: do not resurrect a stale ACTIVE flag.
            if (!listening || registration != generation || rearDisplayActive == active) {
                return;
            }
            rearDisplayActive = active;
        }
        listener.onRearDisplayActiveChanged(active);
    }

    @ExperimentalWindowApi
    private static boolean isRearTransferActive(List<WindowAreaInfo> infos) {
        if (infos == null) {
            return false;
        }

        for (WindowAreaInfo info : infos) {
            if (info == null || !WindowAreaInfo.Type.TYPE_REAR_FACING.equals(info.getType())) {
                continue;
            }

            WindowAreaCapability capability = info.getCapability(
                    WindowAreaCapability.Operation.OPERATION_TRANSFER_ACTIVITY_TO_AREA);
            if (capability != null
                    && WindowAreaCapability.Status.WINDOW_AREA_STATUS_ACTIVE.equals(capability.getStatus())) {
                return true;
            }
        }
        return false;
    }

    static Executor uiExecutor() {
        return new UiThreadExecutor();
    }

    private static final class UiThreadExecutor implements Executor {
        private final Handler handler = new Handler(Looper.getMainLooper());

        @Override
        public void execute(Runnable command) {
            handler.post(command);
        }
    }
}
