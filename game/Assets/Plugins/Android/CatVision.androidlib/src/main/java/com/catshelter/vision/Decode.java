package com.catshelter.vision;

import android.graphics.Bitmap;
import android.graphics.BitmapFactory;
import android.graphics.Matrix;
import android.media.ExifInterface;

import java.io.ByteArrayInputStream;

/**
 * Task 50-photo/05: JPEG bytes to an upright ARGB_8888 bitmap.
 *
 * <p>Split out of {@link CatVision} because it is the half that has nothing to
 * do with ML Kit and everything to do with the classic cause of "recognition
 * doesn't work": an image fed to a model on its side. Vision on iOS keeps no
 * orientation of its own and mis-detects silently when it is wrong; ML Kit is
 * no different, it just spells the same trap {@code rotationDegrees}. Rather
 * than pass a rotation into ML Kit and hope every downstream coordinate agrees
 * about it, the pixels are rotated once here and everything after this file
 * works in one space.
 *
 * <p>The orientation argument is a CGImagePropertyOrientation raw value, which
 * is the same 1-8 as the EXIF Orientation tag, so
 * {@code Shell/CatVision.cs} passes the identical number to both platforms and
 * 0 means "read the file's own metadata" on both.
 */
final class Decode {

    private Decode() {
    }

    /**
     * Long side of the bitmap handed to ML Kit.
     *
     * <p>This was 2048 and it was about memory: a 4032x3024 photograph is 48 MB
     * as ARGB_8888, and the note here said the labeller resizes to 224x224
     * internally so "nothing downstream sees the difference". Both halves of
     * that were wrong, and the second one cost the owner his cats.
     *
     * <h2>What the labeller actually does with a big photograph</h2>
     *
     * <p>Measured 2026-09-01 on the owner's own two originals, 3000x4000
     * straight out of his camera, one file resized and nothing else changed
     * (tmp/sizecheck):
     *
     * <pre>
     *   long side   IMG...451        IMG...500
     *      800      Cat  0.91        Cat  1.00
     *     1024      Cat  0.88        Cat  0.99
     *     1280      Cat  0.88        Cat  0.99
     *     1600      Cat  0.83        Cat  0.98
     *     2048      Cat  0.82        Cat  0.87
     *     2400      Dog  0.81        Cat  0.78
     *     4000      Dog  0.82        Dog  0.56
     * </pre>
     *
     * <p>Monotonic, on both, with no exception: the bigger the frame, the worse
     * the answer, until the labeller changes species. Those two rows at 4000 are
     * exactly the two complaints — "похоже на собаку" and, through
     * {@code PhotoJudge}'s 0.60 floor, "кошки здесь не видно". The same two
     * files, resized to 960x1280 by a messenger on the way to a laptop, score
     * Cat 0.97 and Cat 0.88 and were used for weeks as proof that nothing was
     * wrong. The bug was never on the owner's phone; it was in the size of what
     * we handed the model.
     *
     * <p>Why: ML Kit's labeller does resize to its own input, but a single
     * step from 4000 px to 224 px is a 18x decimation, and decimating without
     * filtering first turns fur into aliasing noise. Shrinking in two stages —
     * a filtered one here, the model's own after it — is the ordinary answer,
     * and it is what the table above measures.
     *
     * <p>1280 rather than 800, though 800 scores highest. 800 is one photograph
     * ahead on two photographs; 1280 sits on a flat part of both curves, is a
     * real phone photograph size rather than a thumbnail, and leaves the
     * segmenter something to cut a mask out of — {@code Shell/CatVision.cs}
     * asks for a 512 px mask, and a mask cannot be sharper than the bitmap it
     * was measured on.
     *
     * <p>Boxes are scaled back to the real pixel size before they cross to C#,
     * so the C# contract is unaffected.
     */
    static final int ANALYSIS_MAX_SIDE = 1280;

    /** An upright bitmap plus what it took to get there. */
    static final class Result {
        Bitmap bitmap;
        /** Pixel size of the photograph AS ORIENTED, before subsampling. */
        int fullWidth, fullHeight;
        /** bitmap pixels -> full pixels. 1.0 when no subsampling happened. */
        float scale = 1f;
        String error;
    }

    static Result decode(byte[] bytes, int orientation) {
        Result out = new Result();
        if (bytes == null || bytes.length == 0) {
            out.error = "empty image data";
            return out;
        }

        BitmapFactory.Options bounds = new BitmapFactory.Options();
        bounds.inJustDecodeBounds = true;
        BitmapFactory.decodeByteArray(bytes, 0, bytes.length, bounds);
        if (bounds.outWidth <= 0 || bounds.outHeight <= 0) {
            out.error = "not a decodable image";
            return out;
        }

        int exif = orientation > 0 ? orientation : exifOf(bytes);

        BitmapFactory.Options options = new BitmapFactory.Options();
        options.inPreferredConfig = Bitmap.Config.ARGB_8888;
        options.inSampleSize = sampleSize(bounds.outWidth, bounds.outHeight);
        Bitmap raw;
        try {
            raw = BitmapFactory.decodeByteArray(bytes, 0, bytes.length, options);
        } catch (OutOfMemoryError e) {
            out.error = "out of memory decoding image";
            return out;
        }
        if (raw == null) {
            out.error = "not a decodable image";
            return out;
        }

        raw = shrink(raw);

        Bitmap upright = applyOrientation(raw, exif);
        if (upright == null) {
            out.error = "out of memory rotating image";
            raw.recycle();
            return out;
        }

        boolean sideways = exif >= 5 && exif <= 8;
        out.bitmap = upright;
        out.fullWidth = sideways ? bounds.outHeight : bounds.outWidth;
        out.fullHeight = sideways ? bounds.outWidth : bounds.outHeight;
        out.scale = out.fullWidth / (float) upright.getWidth();
        return out;
    }

    /**
     * The subsampling that gets us CLOSE to the target without going under it.
     * inSampleSize is powers of two only, so this is half the job.
     */
    static int sampleSize(int width, int height) {
        int longSide = Math.max(width, height);
        int sample = 1;
        while (longSide / (sample * 2) >= ANALYSIS_MAX_SIDE) {
            sample *= 2;
        }
        return sample;
    }

    /**
     * The other half: scale exactly onto {@link #ANALYSIS_MAX_SIDE}.
     *
     * <p>Without this the cap is not a cap. inSampleSize only halves, so the
     * bitmap lands on whatever power of two happens to be at least as big as
     * the target, and for the two commonest phone photographs there is nothing
     * between "full size" and "far too small":
     *
     * <pre>
     *   3000x4000, cap 2048 -> 4000/2 = 2000, under the cap, so sample stays 1
     *                          and the labeller is handed all 4000 px
     *   3000x4000, cap 1280 -> 4000/2 = 2000, over it, so sample is 2
     *                          and the labeller is handed 2000 px
     * </pre>
     *
     * <p>The first line is the bug the owner reported for weeks: the guard was
     * written, the number was chosen, and on a 4000 px photograph the guard did
     * nothing at all. Lowering the number alone would not have fixed it either
     * — 2400 px is a real photo size and would still have sailed through, and
     * 2400 px is already inside the range where the labeller says "Dog".
     *
     * <p>So the power-of-two step is treated as what it is, a cheap way to get
     * most of the way there without decoding 48 MB, and the last stretch is a
     * filtered scale. Two filtered shrinks in a row, which is the whole point:
     * see the table on {@link #ANALYSIS_MAX_SIDE}.
     */
    private static Bitmap shrink(Bitmap source) {
        int longSide = Math.max(source.getWidth(), source.getHeight());
        if (longSide <= ANALYSIS_MAX_SIDE) {
            return source;
        }
        float factor = ANALYSIS_MAX_SIDE / (float) longSide;
        int width = Math.max(1, Math.round(source.getWidth() * factor));
        int height = Math.max(1, Math.round(source.getHeight() * factor));
        try {
            // Filtered, not nearest: the filtering is the reason this exists.
            Bitmap scaled = Bitmap.createScaledBitmap(source, width, height, true);
            if (scaled != null && scaled != source) {
                source.recycle();
                return scaled;
            }
            return source;
        } catch (OutOfMemoryError e) {
            // A frame we could decode but cannot scale is still a frame worth
            // looking at; it just gets the worse answer this method exists to
            // avoid. Never a failure.
            return source;
        }
    }

    /**
     * The EXIF Orientation tag, or 1 ("up") when the file carries none. Read
     * from the bytes already in memory - the plug-in never opens the file the
     * photograph came from, so there is no path here to leak.
     */
    private static int exifOf(byte[] bytes) {
        try {
            ExifInterface exif = new ExifInterface(new ByteArrayInputStream(bytes));
            int value = exif.getAttributeInt(ExifInterface.TAG_ORIENTATION,
                    ExifInterface.ORIENTATION_NORMAL);
            return value >= 1 && value <= 8 ? value : 1;
        } catch (Throwable t) {
            // A PNG, or a JPEG with no EXIF block. Not an error.
            return 1;
        }
    }

    /**
     * The eight EXIF orientations, mirrored ones included. Values 2, 4, 5 and 7
     * are flips; a cat is not symmetrical and her white sock is on one side, so
     * dropping the flip would put the mark on the wrong paw.
     */
    private static Bitmap applyOrientation(Bitmap source, int orientation) {
        if (orientation <= 1) {
            return source;
        }
        Matrix m = new Matrix();
        switch (orientation) {
            case 2: m.setScale(-1f, 1f); break;
            case 3: m.setRotate(180f); break;
            case 4: m.setRotate(180f); m.postScale(-1f, 1f); break;
            case 5: m.setRotate(90f); m.postScale(-1f, 1f); break;
            case 6: m.setRotate(90f); break;
            case 7: m.setRotate(270f); m.postScale(-1f, 1f); break;
            case 8: m.setRotate(270f); break;
            default: return source;
        }
        try {
            Bitmap rotated = Bitmap.createBitmap(
                    source, 0, 0, source.getWidth(), source.getHeight(), m, true);
            if (rotated != source) {
                source.recycle();
            }
            return rotated;
        } catch (OutOfMemoryError e) {
            return null;
        }
    }
}
