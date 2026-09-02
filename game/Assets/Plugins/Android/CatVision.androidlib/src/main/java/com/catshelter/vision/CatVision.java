package com.catshelter.vision;

import android.content.Context;
import android.graphics.Bitmap;
import android.graphics.Rect;

import com.google.android.gms.common.moduleinstall.ModuleInstall;
import com.google.android.gms.common.moduleinstall.ModuleInstallClient;
import com.google.android.gms.common.moduleinstall.ModuleInstallRequest;
import com.google.android.gms.tasks.Tasks;
import com.google.mlkit.vision.common.InputImage;
import com.google.mlkit.vision.label.ImageLabel;
import com.google.mlkit.vision.label.ImageLabeler;
import com.google.mlkit.vision.label.ImageLabeling;
import com.google.mlkit.vision.label.defaults.ImageLabelerOptions;
import com.google.mlkit.vision.segmentation.subject.Subject;
import com.google.mlkit.vision.segmentation.subject.SubjectSegmentation;
import com.google.mlkit.vision.segmentation.subject.SubjectSegmentationResult;
import com.google.mlkit.vision.segmentation.subject.SubjectSegmenter;
import com.google.mlkit.vision.segmentation.subject.SubjectSegmenterOptions;

import java.nio.FloatBuffer;
import java.util.ArrayList;
import java.util.Collections;
import java.util.Comparator;
import java.util.List;
import java.util.concurrent.Callable;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.Future;
import java.util.concurrent.TimeUnit;

/**
 * Task 50-photo/05: the Android answer to "is there a cat in this photograph,
 * and where is she". The counterpart of {@code Plugins/iOS/CatVision.swift},
 * wrapped by the same {@code Shell/CatVision.cs}.
 *
 * <p>iOS gets both answers from one framework. Android has no animal
 * recogniser and no animal skeleton, so the job is split across two ML Kit
 * APIs that were never designed to work together:
 *
 * <ul>
 *   <li><b>Image labelling</b>, base model, BUNDLED - 447 labels, of which two
 *       matter. It says "Cat" but has no idea where the cat is.</li>
 *   <li><b>Subject segmentation</b>, UNBUNDLED - a foreground mask and one box
 *       per subject. It says where a subject is but has no idea what it is.</li>
 * </ul>
 *
 * <p>Together: segment first, then label each subject's crop. That is stronger
 * than labelling the whole frame, because a cat occupying a fifth of a
 * photograph competes with the sofa, the window and the rug for the labeller's
 * attention, and on a crop she does not.
 *
 * <h2>Three rungs, and this layer never sends or stores anything</h2>
 *
 * <p>Each rung survives the loss of the one above, the same shape
 * {@code CatMarks.swift} uses:
 *
 * <ol>
 *   <li>{@code subject+label} - segmentation and labelling both ran. Species,
 *       a real box, and a mask.</li>
 *   <li>{@code label} - no Play services, or the segmentation module has not
 *       downloaded yet. Species from the whole frame, box = the whole frame,
 *       no mask. The player still gets her cat.</li>
 *   <li>{@code none} - the bytes are not an image. {@code ok:false}, which is
 *       the state {@code VisionAnswer.Failed} exists to tell apart from "looked
 *       and found nothing".</li>
 * </ol>
 *
 * <p><b>There is not one log statement in this package on purpose.</b> The
 * photograph exists as bytes in memory for the length of one call, is written
 * nowhere by this package, and no error string here names a pixel, a path or a
 * size. Compare the same paragraph in {@code CatMarks.swift}. That is the
 * whole claim this file can make for itself: what the game does with the
 * accepted photo afterwards - {@code Shell/CatPhoto.cs} crops it and
 * {@code Core/TraitsRequest.cs} sends that crop to the traits Worker
 * ({@code worker/src/index.ts}) - is a different layer with a different
 * story, told in full in
 * {@code tasks/50-photo/15-privacy-wording/NOTES.md}.
 *
 * <h2>Threading</h2>
 *
 * <p>ML Kit is {@code Task}-based and {@code Tasks.await} throws if called on
 * the main thread. Unity calls this from the main thread. So every call is
 * submitted to a private single-thread executor and the caller blocks on the
 * {@link Future}; {@code Tasks.await} then runs on a thread that is allowed to
 * block, and its completion listeners do not need the main looper to turn.
 * {@code shouldNotDeadlockOnMainThread} in
 * {@code tools/tests/android-vision} pins that.
 */
public final class CatVision {

    /** ML Kit's own labels. Not localised - the model's English label set. */
    private static final String CAT = "Cat";
    private static final String DOG = "Dog";

    /**
     * Below this the labeller's own answer is not worth returning. Deliberately
     * far under the 0.6 {@code PhotoJudge} applies: the threshold that decides
     * anything belongs in C# where it can be re-tuned against
     * {@code fixtures/reference-photos} without another native build, and a
     * test on the iOS side already forbids the native half from having an
     * opinion. This only keeps the JSON from carrying noise.
     */
    private static final float LABEL_FLOOR = 0.05f;

    /** One photograph, one player, one hand. Anything slower is a failure. */
    private static final long CALL_TIMEOUT_SECONDS = 30;
    private static final long MODULE_TIMEOUT_SECONDS = 8;

    private static final ExecutorService WORKER = Executors.newSingleThreadExecutor(runnable -> {
        Thread thread = new Thread(runnable, "CatVision");
        thread.setDaemon(true);
        return thread;
    });

    private static ImageLabeler labeler;
    private static SubjectSegmenter segmenter;
    private static boolean segmenterUnavailable;

    private CatVision() {
    }

    /**
     * Recognise, and optionally cut out, the animal in an encoded image.
     *
     * @param context      any Context. Passed in rather than fetched from
     *                     UnityPlayer so this class can be instrumented.
     * @param image        JPEG or PNG bytes.
     * @param orientation  a CGImagePropertyOrientation raw value, i.e. the EXIF
     *                     Orientation tag, 1-8. 0 reads the file's own.
     * @param maskMaxSide  longest side of the returned mask, in pixels. 0 asks
     *                     for no mask at all and skips segmentation entirely.
     * @return the packed answer described in {@link Packet}. Never null.
     */
    public static byte[] analyse(final Context context, final byte[] image,
                                 final int orientation, final int maskMaxSide) {
        if (context == null) {
            return Packet.failure("no context");
        }
        if (image == null || image.length == 0) {
            return Packet.failure("empty image data");
        }
        try {
            Future<byte[]> future = WORKER.submit(new Callable<byte[]>() {
                @Override
                public byte[] call() {
                    return run(context, image, orientation, maskMaxSide);
                }
            });
            return future.get(CALL_TIMEOUT_SECONDS, TimeUnit.SECONDS);
        } catch (Throwable t) {
            return Packet.failure("vision failed: " + t.getClass().getSimpleName());
        }
    }

    /**
     * Ask Google Play services to fetch the subject-segmentation module now.
     *
     * <p>Fire this from the capture screen while the player is still choosing a
     * photograph, so the first one she picks does not pay for the download. The
     * manifest's {@code com.google.mlkit.vision.DEPENDENCIES} hint usually gets
     * there first; this is what covers the device where it did not.
     *
     * @return one of {@code ready}, {@code requested}, {@code unavailable}.
     *         Never throws.
     */
    public static String prepare(final Context context) {
        if (context == null) {
            return "unavailable";
        }
        try {
            return WORKER.submit(new Callable<String>() {
                @Override
                public String call() {
                    return requestModule(context);
                }
            }).get(MODULE_TIMEOUT_SECONDS + 4, TimeUnit.SECONDS);
        } catch (Throwable t) {
            return "unavailable";
        }
    }

    /** Drop the cached detectors. Safe to call at any time. */
    public static synchronized void release() {
        if (labeler != null) {
            try {
                labeler.close();
            } catch (Throwable ignored) {
                // A detector that will not close is not a reason to crash.
            }
            labeler = null;
        }
        if (segmenter != null) {
            try {
                segmenter.close();
            } catch (Throwable ignored) {
            }
            segmenter = null;
        }
        segmenterUnavailable = false;
    }

    // ---------------------------------------------------------------- the run

    private static byte[] run(Context context, byte[] image, int orientation, int maskMaxSide) {
        Decode.Result decoded = Decode.decode(image, orientation);
        if (decoded.bitmap == null) {
            return Packet.failure(decoded.error == null ? "not a decodable image" : decoded.error);
        }

        Bitmap bitmap = decoded.bitmap;
        List<Packet.Detection> detections = Packet.emptyDetections();
        byte[] mask = null;
        int maskWidth = 0, maskHeight = 0;
        String maskSource = "none";
        String rung = "label";
        String note = null;

        try {
            List<Subject> subjects = Collections.emptyList();
            SubjectSegmentationResult segmented = null;
            if (maskMaxSide > 0) {
                try {
                    segmented = segment(context, bitmap);
                } catch (Throwable t) {
                    note = "segmentation unavailable: " + reason(t);
                }
            }
            if (segmented != null) {
                subjects = segmented.getSubjects();
            }

            if (subjects != null && !subjects.isEmpty()) {
                rung = "subject+label";
                Subject best = null;
                float bestConfidence = 0f;
                for (Subject subject : subjects) {
                    Rect box = clamp(subject, bitmap);
                    if (box == null) {
                        continue;
                    }
                    Packet.Detection detection = labelCrop(context, bitmap, box, decoded.scale);
                    if (detection == null) {
                        continue;
                    }
                    detections.add(detection);
                    if (CAT.equals(detection.identifier) && detection.confidence > bestConfidence) {
                        bestConfidence = detection.confidence;
                        best = subject;
                    }
                }
                // Her mask, not everything the segmenter found: what a later
                // step measures markings against has to be one animal. Falls
                // back to the best dog-or-nothing subject only when no cat was
                // named, so an unrecognised cat still gets a silhouette.
                Subject carry = best != null ? best : subjects.get(0);
                Mask cut = Mask.from(carry, bitmap.getWidth(), bitmap.getHeight(), maskMaxSide);
                if (cut != null) {
                    mask = cut.bytes;
                    maskWidth = cut.width;
                    maskHeight = cut.height;
                    maskSource = best != null ? "subject" : "subject-unlabelled";
                }
            }

            // The whole frame, ALWAYS — not only when the subject crops came
            // back empty.
            //
            // It was `if (detections.isEmpty())` until 2026-09-01, and the
            // reasoning went as far as it went: the segmenter can miss an
            // animal that fills the picture edge to edge, so look at everything
            // before saying "no animal". What it missed is that a subject crop
            // can be WRONG rather than absent, and a wrong answer suppressed
            // the second look exactly as a right one did.
            //
            // That is what the owner hit. On his phone ML Kit hands back his
            // cat and the armchair she sits in as ONE subject — measured, 52 %
            // of the crop against Vision's 33 % on the same photograph. The
            // labeller is then shown a cat-in-a-chair and answers "Dog", or
            // something unrecognisable. `detections` is not empty, so the frame
            // was never looked at. Asked directly, that same photograph is
            // `Cat 0.97`. Two of the photographs he sent came back "кошки здесь
            // не видно" and "похоже на собаку" on his phone while scoring 0.97
            // and 0.88 as whole frames here.
            //
            // So both questions are asked now: "is any cut-out subject a cat"
            // and "is there a cat in this picture at all". `VisionAnswer.Best`
            // prefers a cat over anything else, so finding one either way is
            // enough. A photograph of a real dog is untouched — no cat is found
            // in either view and confidence decides, as before.
            //
            // The cost is one extra labelling call: 18–130 ms measured over the
            // reference set, against roughly 250 ms for the mask that has
            // already been paid for. It buys the difference between accepting
            // her cat and turning it away.
            Packet.Detection whole = labelCrop(
                    context, bitmap,
                    new Rect(0, 0, bitmap.getWidth(), bitmap.getHeight()),
                    decoded.scale);
            if (whole != null) {
                detections.add(whole);
            }
        } catch (Throwable t) {
            return Packet.failure("vision failed: " + t.getClass().getSimpleName());
        } finally {
            bitmap.recycle();
        }

        Collections.sort(detections, new Comparator<Packet.Detection>() {
            @Override
            public int compare(Packet.Detection a, Packet.Detection b) {
                return Float.compare(b.confidence, a.confidence);
            }
        });

        return Packet.success(decoded.fullWidth, decoded.fullHeight, detections,
                rung, note, mask, maskWidth, maskHeight, maskSource);
    }

    // ------------------------------------------------------------- the models

    private static synchronized ImageLabeler labeler() {
        if (labeler == null) {
            labeler = ImageLabeling.getClient(new ImageLabelerOptions.Builder()
                    .setConfidenceThreshold(LABEL_FLOOR)
                    .build());
        }
        return labeler;
    }

    /**
     * @throws Exception when the module is not on the device, which is a rung,
     *                   not a failure - see {@link #run}.
     */
    private static SubjectSegmentationResult segment(Context context, Bitmap bitmap)
            throws Exception {
        SubjectSegmenter client = segmenterOrNull(context);
        if (client == null) {
            throw new IllegalStateException("module unavailable");
        }
        // NOT latched on failure, and that was a real bug on the way here. The
        // first thing a device says when the optional module has not arrived is
        // MlKitException "Waiting for the subject segmentation optional module
        // to be downloaded. Please wait." - measured, see NOTES-android.md. A
        // latch would turn "not yet" into "never" and the mask would stay
        // missing for the life of the process even after Play services finished
        // the download. Retrying is free: the failure comes back immediately,
        // not on a timeout (the 41-image run averaged 67 ms per photograph with
        // exactly this failure on every one of them).
        return Tasks.await(client.process(InputImage.fromBitmap(bitmap, 0)),
                CALL_TIMEOUT_SECONDS - 5, TimeUnit.SECONDS);
    }

    private static synchronized SubjectSegmenter segmenterOrNull(Context context) {
        if (segmenterUnavailable) {
            return null;
        }
        if (segmenter == null) {
            try {
                segmenter = SubjectSegmentation.getClient(new SubjectSegmenterOptions.Builder()
                        .enableMultipleSubjects(
                                new SubjectSegmenterOptions.SubjectResultOptions.Builder()
                                        .enableConfidenceMask()
                                        .build())
                        .build());
            } catch (Throwable t) {
                segmenterUnavailable = true;
                return null;
            }
        }
        return segmenter;
    }

    /**
     * Why a rung was lost, in a form C# can branch on and a log can never
     * embarrass anyone with.
     *
     * <p>ML Kit's error CODE, not its message. The message is English prose
     * written by Google ("Waiting for the subject segmentation optional module
     * to be downloaded. Please wait.") and would end up shown to a Russian
     * player or pasted into an analytics event; the code is a small integer -
     * 14 is UNAVAILABLE, which is the one that means "not yet, try later" - and
     * {@code PhotoMessages} already owns the wording.
     */
    private static String reason(Throwable t) {
        Throwable cause = t;
        for (int depth = 0; depth < 4 && cause != null; depth++) {
            if (cause instanceof com.google.mlkit.common.MlKitException) {
                return "MlKitException/"
                        + ((com.google.mlkit.common.MlKitException) cause).getErrorCode();
            }
            cause = cause.getCause();
        }
        return t.getClass().getSimpleName();
    }

    private static String requestModule(Context context) {
        SubjectSegmenter client = segmenterOrNull(context);
        if (client == null) {
            return "unavailable";
        }
        try {
            ModuleInstallClient install = ModuleInstall.getClient(context);
            Boolean available = Tasks.await(install.areModulesAvailable(client),
                    MODULE_TIMEOUT_SECONDS, TimeUnit.SECONDS).areModulesAvailable();
            if (Boolean.TRUE.equals(available)) {
                return "ready";
            }
            install.installModules(
                    ModuleInstallRequest.newBuilder().addApi(client).build());
            return "requested";
        } catch (Throwable t) {
            return "unavailable";
        }
    }

    // ------------------------------------------------------------ the species

    /**
     * Label one rectangle of the bitmap and return it as a detection if the
     * labeller called it a cat or a dog.
     *
     * <p>Only Cat and Dog cross the boundary. Vision on iOS recognises exactly
     * those two animals and nothing else, and {@code AnimalBox.IsCat} compares
     * against the string "Cat"; letting "Rug" or "Whiskers" through would give
     * {@code VisionAnswer.FoundAnimal} a meaning it does not have on iOS.
     */
    private static Packet.Detection labelCrop(Context context, Bitmap bitmap,
                                              Rect box, float scale) {
        Bitmap crop = null;
        try {
            boolean whole = box.left == 0 && box.top == 0
                    && box.width() == bitmap.getWidth() && box.height() == bitmap.getHeight();
            crop = whole
                    ? bitmap
                    : Bitmap.createBitmap(bitmap, box.left, box.top, box.width(), box.height());

            List<ImageLabel> labels = Tasks.await(
                    labeler().process(InputImage.fromBitmap(crop, 0)),
                    CALL_TIMEOUT_SECONDS - 5, TimeUnit.SECONDS);

            String identifier = null;
            float confidence = 0f;
            for (ImageLabel label : labels) {
                String text = label.getText();
                if ((CAT.equals(text) || DOG.equals(text)) && label.getConfidence() > confidence) {
                    identifier = text;
                    confidence = label.getConfidence();
                }
            }
            if (identifier == null) {
                return null;
            }

            Packet.Detection detection = new Packet.Detection();
            detection.identifier = identifier;
            detection.confidence = confidence;
            detection.x = Math.round(box.left * scale);
            detection.y = Math.round(box.top * scale);
            detection.width = Math.round(box.width() * scale);
            detection.height = Math.round(box.height() * scale);
            return detection;
        } catch (Throwable t) {
            return null;
        } finally {
            if (crop != null && crop != bitmap) {
                crop.recycle();
            }
        }
    }

    /** A subject's box, trimmed to the bitmap and rejected if it is a sliver. */
    private static Rect clamp(Subject subject, Bitmap bitmap) {
        int left = Math.max(0, subject.getStartX());
        int top = Math.max(0, subject.getStartY());
        int right = Math.min(bitmap.getWidth(), subject.getStartX() + subject.getWidth());
        int bottom = Math.min(bitmap.getHeight(), subject.getStartY() + subject.getHeight());
        if (right - left < 8 || bottom - top < 8) {
            return null;
        }
        return new Rect(left, top, right, bottom);
    }

    // ------------------------------------------------------------- the mask

    /** A subject's confidence mask, redrawn over the whole image, downscaled. */
    static final class Mask {
        byte[] bytes;
        int width, height;

        /**
         * ML Kit hands back a mask the size of the SUBJECT'S BOX. C# needs one
         * it can index by image coordinate, so the box's values are placed into
         * a full-image grid and everything outside stays zero - the same shape
         * as Vision's {@code generateScaledMaskForImage(forInstances:)}.
         *
         * <p>Point-sampled, not area-averaged. The mask is already soft at the
         * edges and a later step thresholds it; averaging would only blur a
         * boundary that is the one thing worth keeping sharp.
         */
        static Mask from(Subject subject, int imageWidth, int imageHeight, int maxSide) {
            FloatBuffer confidence = subject.getConfidenceMask();
            if (confidence == null) {
                return null;
            }
            int subjectWidth = subject.getWidth();
            int subjectHeight = subject.getHeight();
            if (subjectWidth <= 0 || subjectHeight <= 0
                    || confidence.capacity() < subjectWidth * subjectHeight) {
                return null;
            }

            float shrink = Math.min(1f, maxSide / (float) Math.max(imageWidth, imageHeight));
            int width = Math.max(1, Math.round(imageWidth * shrink));
            int height = Math.max(1, Math.round(imageHeight * shrink));

            byte[] bytes = new byte[width * height];
            for (int y = 0; y < height; y++) {
                int imageY = (int) ((y + 0.5f) / shrink);
                int localY = imageY - subject.getStartY();
                if (localY < 0 || localY >= subjectHeight) {
                    continue;
                }
                int row = y * width;
                int sourceRow = localY * subjectWidth;
                for (int x = 0; x < width; x++) {
                    int imageX = (int) ((x + 0.5f) / shrink);
                    int localX = imageX - subject.getStartX();
                    if (localX < 0 || localX >= subjectWidth) {
                        continue;
                    }
                    float value = confidence.get(sourceRow + localX);
                    if (value <= 0f) {
                        continue;
                    }
                    bytes[row + x] = (byte) Math.min(255, Math.round(value * 255f));
                }
            }

            Mask mask = new Mask();
            mask.bytes = bytes;
            mask.width = width;
            mask.height = height;
            return mask;
        }
    }

    /** Visible for the probe: the rungs, so a test can assert on the names. */
    public static List<String> rungs() {
        List<String> names = new ArrayList<>(3);
        names.add("subject+label");
        names.add("label");
        names.add("none");
        return names;
    }
}
