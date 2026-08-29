package com.catshelter.picker;

import android.app.Activity;
import android.content.Context;
import android.content.Intent;
import android.content.pm.PackageManager;
import android.provider.MediaStore;
import android.util.Log;

import com.unity3d.player.UnityPlayer;

import java.io.File;

/**
 * Task 90-android/photo: the Android half of Shell/CatPicker.cs.
 *
 * The contract is Plugins/iOS/CatPicker.swift's, to the letter, because
 * View/CaptureScreen.cs must not know which phone it is on:
 *
 *   - openGallery() / openCamera() are fire-and-forget; the answer arrives
 *     through UnitySendMessage to the "CatPickerListener" GameObject.
 *   - Exactly one of OnPicked / OnPickCancelled / OnPickFailed /
 *     OnPickUnavailable is sent per call, so the screen never sits busy with
 *     nothing coming.
 *   - OnPicked carries a FILE PATH, not bytes. UnitySendMessage carries a
 *     string; a JPEG in the app's own cache avoids inventing a second
 *     callback channel for binary data. CatPicker.cs reads it and deletes it,
 *     exactly as it already does on iOS.
 *   - Every failure reason is a fixed lowercase code, never a sentence. That
 *     rule was bought the hard way on the iOS side (60-shell-build/16
 *     VERIFY): prose crossing a native boundary is untranslatable by
 *     construction, and an OS error string follows the device's language, not
 *     the game's. Log.w keeps the detail; only the code crosses.
 *
 * Nothing here asks for a permission, and nothing here writes outside
 * getCacheDir()/catpick. See AndroidManifest.xml for why both are true.
 */
public final class CatPicker {

    static final String TAG = "CatPicker";

    /** The GameObject Shell/CatPicker.cs creates before any pick starts. */
    static final String LISTENER = "CatPickerListener";

    /** Must match path= in res/xml/catpick_paths.xml. */
    static final String DIRECTORY = "catpick";

    /**
     * Must match android:authorities in AndroidManifest.xml, where it is
     * written as ${applicationId} + this suffix. Same fragile-but-loud
     * arrangement CatShare uses: if an applicationIdSuffix is ever added,
     * getUriForFile throws IllegalArgumentException rather than silently
     * pointing the camera somewhere else.
     */
    static final String AUTHORITY_SUFFIX = ".catpick";

    private CatPicker() {
    }

    // --- called from Shell/CatPicker.cs by name; do not rename -------------

    /**
     * Open the system photo picker. Needs no storage permission: the picker
     * runs out of process and hands over a URI already granted to us.
     */
    public static void openGallery() {
        start(CatPickActivity.MODE_GALLERY);
    }

    /**
     * Open the camera app. Needs no camera permission either, because we
     * delegate rather than open the camera ourselves - see the manifest.
     */
    public static void openCamera() {
        start(CatPickActivity.MODE_CAMERA);
    }

    /**
     * Whether taking a photograph is possible at all, so the shell can hide
     * the button rather than let the player press it and get nothing.
     *
     * Two questions, both of which have to be yes: is there a camera in this
     * device, and is there an app willing to drive it? A tablet can answer no
     * to the first; a stripped Android image can answer no to the second. The
     * resolveActivity half needs the <queries> block in the manifest to see
     * anything at all on API 30+.
     */
    public static boolean hasCamera() {
        return hasCamera(UnityPlayer.currentActivity);
    }

    /**
     * The same question asked from inside the proxy activity, where
     * UnityPlayer.currentActivity is not the context to be asking with.
     */
    static boolean hasCamera(Context context) {
        if (context == null) {
            return false;
        }
        try {
            PackageManager packages = context.getPackageManager();
            if (!packages.hasSystemFeature(PackageManager.FEATURE_CAMERA_ANY)) {
                return false;
            }
            return new Intent(MediaStore.ACTION_IMAGE_CAPTURE)
                    .resolveActivity(packages) != null;
        } catch (Exception e) {
            // A question that cannot be answered is answered "no": a hidden
            // button costs the player one alternative, a dead one costs her
            // the screen.
            Log.w(TAG, "hasCamera failed", e);
            return false;
        }
    }

    // --- internals ---------------------------------------------------------

    private static void start(final String mode) {
        final Activity activity = UnityPlayer.currentActivity;
        if (activity == null) {
            // The same code iOS sends when there is no window to present
            // from. CaptureScreen maps it to "our fault", which it is.
            Log.w(TAG, "no_window: UnityPlayer.currentActivity is null");
            send("OnPickFailed", "no_window");
            return;
        }
        // startActivity has to be called from the UI thread, and Unity's
        // scripting thread is not it.
        activity.runOnUiThread(new Runnable() {
            @Override
            public void run() {
                try {
                    Intent proxy = new Intent(activity, CatPickActivity.class);
                    proxy.putExtra(CatPickActivity.EXTRA_MODE, mode);
                    activity.startActivity(proxy);
                } catch (Exception e) {
                    Log.w(TAG, "could not start the proxy activity", e);
                    send("OnPickFailed", "no_window");
                }
            }
        });
    }

    /** The directory every photograph passes through, created on demand. */
    static File directory(Context context) {
        File directory = new File(context.getCacheDir(), DIRECTORY);
        if (!directory.exists() && !directory.mkdirs()) {
            return null;
        }
        return directory;
    }

    /**
     * Empty the directory. Called before a pick as well as after one: the
     * photograph must not outlive the run, and a process killed between the
     * camera writing and Unity reading would otherwise leave a stranger's cat
     * on disk until the OS felt like reclaiming the cache.
     */
    static void purge(Context context) {
        try {
            File directory = new File(context.getCacheDir(), DIRECTORY);
            File[] leftovers = directory.listFiles();
            if (leftovers == null) {
                return;
            }
            for (File leftover : leftovers) {
                if (!leftover.delete()) {
                    Log.w(TAG, "could not delete " + leftover.getName());
                }
            }
        } catch (Exception e) {
            Log.w(TAG, "purge failed", e);
        }
    }

    static void send(String method, String payload) {
        try {
            UnityPlayer.UnitySendMessage(LISTENER, method, payload);
        } catch (Exception e) {
            // A missing listener is a silent no-op on Unity's side; anything
            // else here is not worth taking the app down for.
            Log.w(TAG, "UnitySendMessage(" + method + ") failed", e);
        }
    }
}
