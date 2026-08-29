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
     * Long side of the bitmap handed to ML Kit. A modern phone photograph is
     * 4032x3024 and would be 48 MB as ARGB_8888; the labeller resizes to
     * 224x224 internally and the segmenter to its own input size, so nothing
     * downstream sees the difference. Boxes are scaled back to the real pixel
     * size before they cross to C#, so the C# contract is unaffected.
     */
    static final int ANALYSIS_MAX_SIDE = 2048;

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

    private static int sampleSize(int width, int height) {
        int longSide = Math.max(width, height);
        int sample = 1;
        while (longSide / (sample * 2) >= ANALYSIS_MAX_SIDE) {
            sample *= 2;
        }
        return sample;
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
