package com.catshelter.share;

import android.app.Activity;
import android.app.PendingIntent;
import android.content.BroadcastReceiver;
import android.content.ClipData;
import android.content.Context;
import android.content.Intent;
import android.content.IntentFilter;
import android.net.Uri;
import android.os.Handler;
import android.os.Looper;
import android.util.Log;

import androidx.core.content.FileProvider;

import com.unity3d.player.UnityPlayer;

import java.io.File;
import java.io.FileOutputStream;

/**
 * Task 60-shell-build/15: the Android half of Shell/Share.cs.
 *
 * ACTION_SEND through Intent.createChooser, which is the Android Sharesheet -
 * the system one. Android's own guidance is explicit about not replacing it:
 * "We strongly recommend using the Android Sharesheet to create consistency
 * for your users across apps. Don't display your app's own list of share
 * targets or create your own Sharesheet variations."
 * (developer.android.com/training/sharing/send)
 *
 * The picture cannot be handed over as a file path. Since Android 7 a
 * file:// URI in an Intent raises FileUriExposedException, and every modern
 * target expects a content:// URI it has been granted read access to. That is
 * what FileProvider is for, and it is why this plugin needs a manifest
 * declaration and an XML paths file as well as this class - see
 * ../../AndroidManifest.xml and ../../res/xml/catshare_paths.xml, and the
 * task NOTES for the one line that must reach gradleTemplate.properties.
 *
 * Bytes arrive rather than a path, and the file is written here, on purpose:
 * the FileProvider paths file has to name a directory ahead of time, and
 * getCacheDir()/share is a directory this class controls. Unity's
 * Application.temporaryCachePath resolves to different places on Android
 * depending on the project's write-permission setting, so a path chosen on
 * the C# side could land outside anything the provider is allowed to serve.
 *
 * Task 50-photo/13 VERIFY: run the Android build and drive a share on an
 * emulator or device - stale claim removed, the build pipeline
 * (90-android/02-build-pipeline) exists now.
 */
public final class CatShare {

    private static final String TAG = "CatShare";

    /** Must match path= in res/xml/catshare_paths.xml. */
    private static final String DIRECTORY = "share";

    private static final String FILE_NAME = "kitten-card.png";

    /**
     * Must match android:authorities in AndroidManifest.xml, where it is
     * written as ${applicationId} + this suffix. getPackageName() and
     * applicationId are the same string as long as no applicationIdSuffix is
     * set; Unity sets none, and if one is ever added this is the line that
     * breaks - loudly, with an IllegalArgumentException from getUriForFile,
     * not silently.
     */
    private static final String AUTHORITY_SUFFIX = ".catshare";

    private static final String MIME = "image/png";

    /**
     * Task 50-photo/13: how long to wait, after the player has picked a share
     * target, before the card is deleted.
     *
     * <p>iOS deletes the file the instant its bytes are copied into a UIImage
     * (CatShare.swift:85), before the sheet is even shown — the file has done
     * its one job as a way across the C boundary by then. Android cannot do
     * that: the FileProvider URI has to keep resolving for as long as the
     * chosen app is reading it, and there is no OS callback for "finished
     * reading". EXTRA_CHOSEN_COMPONENT_INTENT_SENDER only says a target was
     * picked, not that it is done with the stream. A short delay after that
     * signal is the accepted compromise (developer.android.com/training/sharing/send);
     * the purge in {@link #send} below is the defensive half, for the run
     * where the player backs out of the chooser and no target is ever chosen.
     */
    private static final long CLEANUP_DELAY_MS = 3_000;

    private CatShare() {
    }

    /**
     * Open the system share sheet on {@code png}, offering {@code text}
     * alongside it. Called from Shell/Share.cs by name; do not rename.
     *
     * Never throws back into Unity. A share that fails is a log line: the card
     * it was opened from is still on screen and the player can tap again.
     */
    public static void image(final byte[] png, final String text) {
        final Activity activity = UnityPlayer.currentActivity;
        if (activity == null) {
            Log.w(TAG, "no_activity");
            return;
        }
        // startActivity has to be called from the UI thread, and Unity's
        // scripting thread is not it.
        activity.runOnUiThread(new Runnable() {
            @Override
            public void run() {
                send(activity, png, text);
            }
        });
    }

    private static void send(Activity activity, byte[] png, String text) {
        try {
            File directory = new File(activity.getCacheDir(), DIRECTORY);
            if (!directory.exists() && !directory.mkdirs()) {
                Log.w(TAG, "mkdir_failed");
                return;
            }

            File file = new File(directory, FILE_NAME);
            // Whatever a previous share left behind — the player backed out
            // of the chooser, or the process died before the callback below
            // fired — must not survive into a second share under the same
            // fixed name. Same purge-before idiom CatPicker.purge uses.
            if (file.exists() && !file.delete()) {
                Log.w(TAG, "could not delete the previous card");
            }
            FileOutputStream out = new FileOutputStream(file);
            try {
                out.write(png);
            } finally {
                out.close();
            }

            Uri uri = FileProvider.getUriForFile(
                    activity, activity.getPackageName() + AUTHORITY_SUFFIX, file);

            Intent send = new Intent(Intent.ACTION_SEND);
            send.setType(MIME);
            send.putExtra(Intent.EXTRA_STREAM, uri);
            if (text != null && text.length() > 0) {
                // Offered, not guaranteed. Every target decides what it keeps:
                // some take the picture and drop the words.
                send.putExtra(Intent.EXTRA_TEXT, text);
            }
            // Without this the chosen app gets a URI it is not allowed to
            // read, and the share arrives as a blank or an error inside
            // somebody else's app. createChooser propagates the flag to
            // whichever target the player picks.
            send.addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION);

            // The picture the player is about to send, shown to her before she
            // sends it.
            //
            // EXTRA_STREAM alone is enough for the share to WORK — the target
            // gets the file either way — but the Sharesheet draws its thumbnail
            // from the ClipData, and without one it offers a sheet with the
            // caption and no picture. Found in a full playthrough on
            // 2026-08-29: the sheet came up reading "В Sootpaw чисто во всех
            // комнатах" over blank space. For a feature whose entire purpose is
            // showing somebody her cat, sending it unseen is the wrong last
            // step.
            //
            // The label is what a target may show beside the file; the system's
            // own wording surrounds it, so it stays a plain description rather
            // than a sentence needing translation.
            send.setClipData(ClipData.newUri(activity.getContentResolver(), "cat", uri));

            // null title: the Sharesheet supplies the system's own wording, in
            // the device's language. A title of ours would be one more English
            // string outside Copy.cs, crossing the native boundary, which is
            // the exact fault 60-shell-build/16 went and fixed in CatPicker.
            activity.startActivity(Intent.createChooser(
                    send, null, chosenTargetSender(activity, file).getIntentSender()));
        } catch (Exception e) {
            // Diagnostic only. Nothing here reaches the player: an OS message
            // follows the device's language, not the game's.
            Log.w(TAG, "share_failed", e);
        }
    }

    /**
     * A PendingIntent the Sharesheet fires once, with the chosen target's
     * ComponentName, as soon as the player picks one. The broadcast itself
     * only schedules the delete (see {@link #CLEANUP_DELAY_MS}); the receiver
     * unregisters itself immediately so it cannot fire twice.
     *
     * <p>RECEIVER_NOT_EXPORTED and FLAG_MUTABLE are both mandatory rather than
     * defensive: minSdk 33 means every device here predates neither
     * requirement — registerReceiver throws without the flags argument, and
     * the system needs write access to stamp EXTRA_CHOSEN_COMPONENT onto an
     * immutable PendingIntent's intent.
     */
    private static PendingIntent chosenTargetSender(final Activity activity, final File file) {
        final String action = activity.getPackageName() + ".CATSHARE_CHOSEN";
        activity.registerReceiver(new BroadcastReceiver() {
            @Override
            public void onReceive(Context context, Intent intent) {
                try {
                    context.unregisterReceiver(this);
                } catch (Exception ignored) {
                    // Already gone is fine; the point was exactly-once.
                }
                new Handler(Looper.getMainLooper()).postDelayed(new Runnable() {
                    @Override
                    public void run() {
                        if (!file.exists()) {
                            return;
                        }
                        if (file.delete()) {
                            // Worth a Log.i, unlike the rest of this class:
                            // task 50-photo/13 VERIFY needed a signal reachable
                            // without root on a non-debuggable build, since
                            // run-as and a private cache ls are both closed off
                            // on a Play-services emulator image.
                            Log.i(TAG, "deleted the sent card");
                        } else {
                            Log.w(TAG, "could not delete the sent card");
                        }
                    }
                }, CLEANUP_DELAY_MS);
            }
        }, new IntentFilter(action), Context.RECEIVER_NOT_EXPORTED);

        Intent chosen = new Intent(action).setPackage(activity.getPackageName());
        return PendingIntent.getBroadcast(activity, 0, chosen,
                PendingIntent.FLAG_UPDATE_CURRENT | PendingIntent.FLAG_MUTABLE);
    }
}
