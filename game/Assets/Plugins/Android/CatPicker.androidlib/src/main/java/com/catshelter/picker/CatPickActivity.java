package com.catshelter.picker;

import android.app.Activity;
import android.content.Intent;
import android.graphics.BitmapFactory;
import android.net.Uri;
import android.os.Build;
import android.os.Bundle;
import android.provider.MediaStore;
import android.util.Log;

import androidx.core.content.FileProvider;

import java.io.File;
import java.io.FileOutputStream;
import java.io.InputStream;
import java.io.OutputStream;
import java.util.UUID;

/**
 * The invisible activity that owns the result.
 *
 * onActivityResult belongs to whichever activity called
 * startActivityForResult, and in a Unity game that is
 * UnityPlayerGameActivity - Unity's, not ours, and not subclassable from a
 * plug-in without replacing the manifest entry Unity generates. The standard
 * shape for a Unity Android plug-in that needs a result back is this: a
 * translucent activity with no UI of its own, started from
 * UnityPlayer.currentActivity, which launches the real picker, catches the
 * result, and forwards it through UnitySendMessage before finishing.
 *
 * It is deliberately dumb. Everything it knows how to do is: launch one
 * intent, turn whatever comes back into one JPEG in the app's own cache, send
 * one message, and go away.
 */
public final class CatPickActivity extends Activity {

    static final String EXTRA_MODE = "com.catshelter.picker.MODE";
    static final String MODE_GALLERY = "gallery";
    static final String MODE_CAMERA = "camera";

    private static final String TAG = CatPicker.TAG;
    private static final int REQUEST = 0xCA7;

    private static final String STATE_LAUNCHED = "catpick.launched";
    private static final String STATE_TARGET = "catpick.target";

    /**
     * A photograph big enough to be a mistake. A 50 MP JPEG is around 15 MB;
     * this is generous, and it exists so that a content provider handing back
     * a video, or a 400 MB raw file, degrades into "read_failed" instead of
     * an OutOfMemoryError halfway through a copy.
     */
    private static final int MAX_INPUT_BYTES = 32 * 1024 * 1024;

    private static final int COPY_BUFFER = 64 * 1024;

    /** Where the camera was told to write, when the mode is camera. */
    private File target;

    private boolean launched;

    /** Exactly one callback per pick; this is what makes that true. */
    private boolean answered;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);

        if (savedInstanceState != null) {
            launched = savedInstanceState.getBoolean(STATE_LAUNCHED, false);
            String saved = savedInstanceState.getString(STATE_TARGET, null);
            target = saved == null ? null : new File(saved);
        }
        if (launched) {
            // Recreated while the picker is in front of us. Wait for the
            // result rather than opening a second picker behind the first.
            return;
        }

        String mode = getIntent() == null ? null : getIntent().getStringExtra(EXTRA_MODE);
        // Nothing from a previous run has any business still being here, and
        // the photograph must not outlive the run.
        CatPicker.purge(this);

        if (!launch(MODE_CAMERA.equals(mode))) {
            finish();
        }
    }

    @Override
    protected void onSaveInstanceState(Bundle outState) {
        super.onSaveInstanceState(outState);
        outState.putBoolean(STATE_LAUNCHED, launched);
        if (target != null) {
            outState.putString(STATE_TARGET, target.getAbsolutePath());
        }
    }

    // --- launching ---------------------------------------------------------

    private boolean launch(boolean camera) {
        try {
            Intent intent = camera ? cameraIntent() : galleryIntent();
            if (intent == null) {
                // Not a failure and not the player's doing: this device
                // cannot do this. iOS sends the same code when the simulator
                // has no camera, and the shell shows the other way in.
                CatPicker.send("OnPickUnavailable", camera ? "camera" : "gallery");
                answered = true;
                return false;
            }
            startActivityForResult(intent, REQUEST);
            launched = true;
            return true;
        } catch (Exception e) {
            Log.w(TAG, "could not open the picker", e);
            answer("OnPickFailed", "unavailable");
            return false;
        }
    }

    /**
     * The system photo picker. One intent, no branches.
     *
     * MediaStore.ACTION_PICK_IMAGES is API 33 and this app's floor is 33, so
     * it is simply present. What that deleted is worth naming, because the
     * absent code is the point:
     *
     *  - No isPhotoPickerAvailable() shim, and no SdkExtensions check for the
     *    Android 11/12 mainline rollout.
     *  - No Google Play services backport branch. It answers a different
     *    action entirely (androidx.activity.result.contract.action.PICK_IMAGES)
     *    and needs a ModuleDependencies <service> in the manifest.
     *  - No ACTION_GET_CONTENT fallback, and no reliance on newer
     *    MediaProvider builds rerouting an image-typed GET_CONTENT to the
     *    picker - that is a mainline rollout behind a device_config flag, not
     *    an API-level guarantee, and nothing here needs it now.
     *
     * The two that lost, and why:
     *
     *  - ACTION_PICK against MediaStore reads the player's whole media
     *    library and needs READ_MEDIA_IMAGES (READ_EXTERNAL_STORAGE before
     *    33) to do anything with what comes back. One permission prompt, and
     *    a broad one, to fetch a single photograph.
     *  - ACTION_GET_CONTENT needs no permission either, but it is a document
     *    browser: it offers every provider on the device, cloud drives
     *    included, and its result is a document URI that may not be an image
     *    at all.
     *  - ACTION_PICK_IMAGES shows photographs and nothing else, runs in the
     *    MediaProvider's process, and gives "the caller [...] read access to
     *    user picked items even without storage permissions"
     *    (developer.android.com/reference/android/provider/MediaStore
     *    #ACTION_PICK_IMAGES).
     *
     * resolveActivity stays, but as error handling rather than as a branch:
     * an Android image with the API level and no picker activity is not
     * something to fall back from, it is something to decline cleanly. That
     * check needs the <queries> block in the manifest to see anything at all.
     */
    private Intent galleryIntent() {
        Intent picker = new Intent(MediaStore.ACTION_PICK_IMAGES);
        picker.setType("image/*");
        // No EXTRA_PICK_IMAGES_MAX: without it the picker is single-select
        // and returns one URI in getData(), which is exactly the one
        // photograph this screen wants.
        if (picker.resolveActivity(getPackageManager()) == null) {
            Log.w(TAG, "no gallery: ACTION_PICK_IMAGES resolves to nothing, sdk="
                    + Build.VERSION.SDK_INT);
            return null;
        }
        Log.i(TAG, "gallery: system photo picker (ACTION_PICK_IMAGES), sdk="
                + Build.VERSION.SDK_INT);
        return picker;
    }

    /**
     * Hand the camera app a file of ours to write into.
     *
     * Without EXTRA_OUTPUT the camera returns a thumbnail in the result
     * extras - a couple of hundred pixels on a side, which is below the
     * 200 px floor CatPhoto already refuses to upscale from. So EXTRA_OUTPUT
     * it is, which means a content:// URI, which means FileProvider: since
     * Android 7 a file:// URI in an intent raises FileUriExposedException.
     */
    private Intent cameraIntent() {
        if (!CatPicker.hasCamera(this)) {
            return null;
        }
        File directory = CatPicker.directory(this);
        if (directory == null) {
            Log.w(TAG, "mkdir_failed");
            return null;
        }
        target = new File(directory, "catpick-" + UUID.randomUUID() + ".jpg");
        Uri output = FileProvider.getUriForFile(
                this, getPackageName() + CatPicker.AUTHORITY_SUFFIX, target);

        Intent capture = new Intent(MediaStore.ACTION_IMAGE_CAPTURE);
        capture.putExtra(MediaStore.EXTRA_OUTPUT, output);
        // The camera app is another process: it can only write where we let
        // it, for the length of this one intent. Read as well as write,
        // because some camera apps read the file back to show a preview.
        capture.addFlags(Intent.FLAG_GRANT_WRITE_URI_PERMISSION
                | Intent.FLAG_GRANT_READ_URI_PERMISSION);
        Log.i(TAG, "camera: ACTION_IMAGE_CAPTURE into " + target.getName());
        return capture;
    }

    // --- the result --------------------------------------------------------

    @Override
    protected void onActivityResult(int requestCode, int resultCode, Intent data) {
        super.onActivityResult(requestCode, resultCode, data);
        if (requestCode != REQUEST) {
            return;
        }
        try {
            if (resultCode != RESULT_OK) {
                // She changed her mind. That is allowed and it is not a
                // failure; the shell says so in different words.
                CatPicker.purge(this);
                answer("OnPickCancelled", "");
                return;
            }
            deliver(data == null ? null : data.getData());
        } finally {
            finish();
        }
    }

    private void deliver(Uri picked) {
        File file;
        if (target != null) {
            // Camera. The bytes are already in our own cache directory
            // because that is where we told the camera to put them; there is
            // nothing to copy.
            file = target;
            if (!file.exists() || file.length() == 0) {
                Log.w(TAG, "camera returned OK but wrote nothing");
                CatPicker.purge(this);
                answer("OnPickFailed", "read_failed");
                return;
            }
        } else {
            if (picked == null) {
                Log.w(TAG, "picker returned OK with no URI");
                answer("OnPickFailed", "read_failed");
                return;
            }
            file = copy(picked);
            if (file == null) {
                CatPicker.purge(this);
                answer("OnPickFailed", "read_failed");
                return;
            }
        }

        // Header only - inJustDecodeBounds allocates no pixels. A file that
        // is not an image gets turned away here rather than three steps
        // later, and a picker that hands back a video degrades into one
        // honest message instead of an OutOfMemoryError.
        BitmapFactory.Options bounds = new BitmapFactory.Options();
        bounds.inJustDecodeBounds = true;
        BitmapFactory.decodeFile(file.getAbsolutePath(), bounds);
        if (bounds.outWidth <= 0 || bounds.outHeight <= 0) {
            Log.w(TAG, "not an image: " + bounds.outMimeType);
            CatPicker.purge(this);
            answer("OnPickFailed", "read_failed");
            return;
        }

        Log.i(TAG, "picked " + bounds.outWidth + "x" + bounds.outHeight + " "
                + bounds.outMimeType + ", " + file.length() + " bytes -> "
                + file.getName());
        // Unity reads this path and deletes the file, exactly as it does on
        // iOS. CatPicker.purge on the next pick is the belt to that braces.
        answer("OnPicked", file.getAbsolutePath());
    }

    /**
     * Copy the picked stream into our own cache. The URI is a grant, not a
     * file: it is only readable while this task is alive, and Unity reads the
     * bytes on its own thread some milliseconds later.
     */
    private File copy(Uri picked) {
        File directory = CatPicker.directory(this);
        if (directory == null) {
            Log.w(TAG, "mkdir_failed");
            return null;
        }
        File file = new File(directory, "catpick-" + UUID.randomUUID() + ".jpg");
        InputStream in = null;
        OutputStream out = null;
        try {
            in = getContentResolver().openInputStream(picked);
            if (in == null) {
                Log.w(TAG, "openInputStream returned null");
                return null;
            }
            out = new FileOutputStream(file);
            byte[] buffer = new byte[COPY_BUFFER];
            long total = 0;
            int read;
            while ((read = in.read(buffer)) > 0) {
                total += read;
                if (total > MAX_INPUT_BYTES) {
                    Log.w(TAG, "picked file is over " + MAX_INPUT_BYTES + " bytes");
                    close(out);
                    out = null;
                    if (!file.delete()) {
                        Log.w(TAG, "could not delete the oversized copy");
                    }
                    return null;
                }
                out.write(buffer, 0, read);
            }
            return total > 0 ? file : null;
        } catch (Exception e) {
            Log.w(TAG, "read_failed", e);
            return null;
        } finally {
            close(in);
            close(out);
        }
    }

    private static void close(java.io.Closeable stream) {
        if (stream == null) {
            return;
        }
        try {
            stream.close();
        } catch (Exception e) {
            Log.w(TAG, "close failed", e);
        }
    }

    /** One message per pick, whatever route got us here. */
    private void answer(String method, String payload) {
        if (answered) {
            Log.w(TAG, "second answer suppressed: " + method);
            return;
        }
        answered = true;
        CatPicker.send(method, payload);
    }

    @Override
    protected void onDestroy() {
        super.onDestroy();
        if (!answered && launched && isFinishing()) {
            // Going away for good before the picker came back. isFinishing()
            // is the difference that matters: it is false when the system is
            // merely recreating us around a configuration change, and
            // answering "cancelled" there would race a result still on its
            // way. Better a clean "not this time" than a screen that stays
            // busy forever, but only when there is genuinely nothing coming.
            Log.w(TAG, "destroyed before a result arrived");
            CatPicker.send("OnPickCancelled", "");
            answered = true;
        }
    }
}
