package com.catshelter.picker;

import android.graphics.Bitmap;
import android.graphics.BitmapFactory;
import android.graphics.BitmapRegionDecoder;
import android.graphics.Matrix;
import android.graphics.Rect;
import android.media.ExifInterface;
import android.util.Log;

import java.io.ByteArrayInputStream;
import java.io.ByteArrayOutputStream;

/**
 * Task 90-android/photo: the Android half of Shell/CatPhoto.cs.
 *
 * A line-by-line port of Plugins/iOS/CatPhoto.swift, and deliberately so: the
 * numbers are not ours to re-derive. Image cost is ceil(w/28)*ceil(h/28)
 * visual tokens, so 512x512 is 361 tokens; accuracy falls off below about
 * 200 px on a side (knowledge/vision-model/01-traits-strict-json.md). Under
 * 200 KB before base64, which inflates by roughly a third.
 *
 * SIDE, MIN_CROP_SIDE, MAX_BYTES and the quality ladder all match
 * CatPhoto.swift exactly. Two things do not, and both are documented where
 * they happen:
 *
 *  1. The decode is region-and-subsample rather than whole-image, because
 *     BitmapFactory has no equivalent of CGImage's lazy backing store and a
 *     50 MP photograph decoded whole is 200 MB of ARGB_8888.
 *  2. EXIF orientation IS applied here. On iOS it deliberately is not - the
 *     image arriving has already been through Vision, which oriented it. On
 *     Android there is no Vision, so the bytes still carry whatever the
 *     camera wrote, and a sideways cat is a sideways cat all the way to the
 *     model.
 */
public final class CatPhoto {

    private static final String TAG = "CatPhoto";

    /** Matches Shell/CatPhoto.cs Side and CatPhoto.swift side. */
    public static final int SIDE = 512;

    /** Matches CatPhoto.swift minCropSide. */
    public static final int MIN_CROP_SIDE = 200;

    /** Matches Shell/CatPhoto.cs MaxBytes and Core/TraitsRequest.cs. */
    public static final int MAX_BYTES = 200 * 1024;

    /** CatPhoto.swift's [0.9, 0.8, 0.7, 0.6, 0.5, 0.4], as percentages. */
    private static final int[] QUALITY = { 90, 80, 70, 60, 50, 40 };

    private CatPhoto() {
    }

    /**
     * Crop to the box, square off, scale to 512x512, JPEG under 200 KB.
     * A box of all zeroes means "no box" - use the whole image, which is
     * every Android call today because CatVision is iOS-only.
     *
     * Called from Shell/CatPhoto.cs by name; do not rename. Returns null when
     * the photograph cannot be decoded, which the caller already treats as
     * "not your fault, try another one".
     */
    public static byte[] prepare(byte[] bytes, int boxX, int boxY,
                                 int boxWidth, int boxHeight) {
        if (bytes == null || bytes.length == 0) {
            return null;
        }
        Bitmap cropped = null;
        Bitmap scaled = null;
        Bitmap upright = null;
        try {
            BitmapFactory.Options bounds = new BitmapFactory.Options();
            bounds.inJustDecodeBounds = true;
            BitmapFactory.decodeByteArray(bytes, 0, bytes.length, bounds);
            if (bounds.outWidth <= 0 || bounds.outHeight <= 0) {
                Log.w(TAG, "could not read the image header");
                return null;
            }

            Rect full = new Rect(0, 0, bounds.outWidth, bounds.outHeight);
            // The box arrives in UPRIGHT coordinates and the bytes are not
            // upright. CatVision turns the photograph the right way up before
            // it looks at it (Decode.applyOrientation) and reports boxes in
            // that space; BitmapRegionDecoder below reads the file as written.
            // For a photograph carrying EXIF 6 or 8 — a phone held upright,
            // which is most of them — those two spaces have their axes swapped,
            // so the crop lands somewhere else entirely.
            //
            // This was harmless until 2026-09-01 and the comment on `orient`
            // below still says so: "there is no box without Vision", and Android
            // had no Vision. It has since, and every photograph now carries a
            // box.
            Rect rect = (boxWidth > 0 && boxHeight > 0)
                    ? intoFileSpace(new Rect(boxX, boxY, boxX + boxWidth, boxY + boxHeight),
                                    exifOrientation(bytes), full)
                    : new Rect(full);

            // A cat filling a corner of a large photo can be reported as a
            // 90 px box. Blowing that up to 512 invents detail the model then
            // reads as real, so widen the crop instead and let the cat be
            // smaller in frame.
            if (rect.width() < MIN_CROP_SIDE || rect.height() < MIN_CROP_SIDE) {
                rect = expand(rect, MIN_CROP_SIDE, full);
            }
            // Square around the centre of what we have, clamped to the image:
            // the model gets a square either way, and cropping is better than
            // padding.
            rect = square(rect, full);
            if (rect.width() <= 0 || rect.height() <= 0) {
                Log.w(TAG, "empty crop rect");
                return null;
            }

            cropped = decodeRegion(bytes, rect);
            if (cropped == null) {
                return null;
            }
            scaled = Bitmap.createScaledBitmap(cropped, SIDE, SIDE, true);
            if (scaled == null) {
                return null;
            }

            upright = orient(scaled, bytes);
            byte[] jpeg = encodeJPEG(upright, MAX_BYTES);
            if (jpeg != null) {
                Log.i(TAG, "prepared " + bounds.outWidth + "x" + bounds.outHeight
                        + " -> crop " + rect.width() + "x" + rect.height()
                        + " -> " + SIDE + "x" + SIDE + ", " + jpeg.length
                        + " bytes (cap " + MAX_BYTES + ")");
            }
            return jpeg;
        } catch (Throwable t) {
            // OutOfMemoryError included, on purpose. A photograph this device
            // cannot hold is not a crash; it is one photograph that did not
            // work, and the shell already has words for that.
            Log.w(TAG, "prepare failed", t);
            return null;
        } finally {
            recycle(upright, scaled, cropped);
        }
    }

    // --- the crop rule, straight out of CatPhoto.swift ---------------------

    /**
     * Widen around the rect's own centre, then slide back inside the image
     * rather than clipping: an off-centre cat should stay whole.
     */
    static Rect expand(Rect rect, int minimum, Rect bounds) {
        int width = Math.max(rect.width(), minimum);
        int height = Math.max(rect.height(), minimum);
        int left = rect.centerX() - width / 2;
        int top = rect.centerY() - height / 2;
        left = Math.min(Math.max(left, bounds.left),
                        Math.max(bounds.right - width, bounds.left));
        top = Math.min(Math.max(top, bounds.top),
                       Math.max(bounds.bottom - height, bounds.top));
        Rect expanded = new Rect(left, top, left + width, top + height);
        if (!expanded.intersect(bounds)) {
            return new Rect(bounds);
        }
        return expanded;
    }

    /**
     * The EXIF Orientation tag, 1-8, or 1 when the file carries none.
     *
     * The same read {@link #orient} does at the other end of this method, kept
     * separate because the box has to be converted BEFORE anything is decoded
     * and the square is rotated AFTER everything is.
     */
    static int exifOrientation(byte[] bytes) {
        try {
            int value = new ExifInterface(new ByteArrayInputStream(bytes))
                    .getAttributeInt(ExifInterface.TAG_ORIENTATION,
                                     ExifInterface.ORIENTATION_NORMAL);
            return value >= 1 && value <= 8 ? value : 1;
        } catch (Throwable t) {
            // No EXIF block, or an unreadable one. A photograph with no
            // orientation tag is already the right way up.
            return 1;
        }
    }

    /**
     * A rectangle measured on the UPRIGHT photograph, expressed in the file's
     * own coordinates.
     *
     * <p>The inverse of {@code Decode.applyOrientation}, corner by corner. The
     * eight cases are the eight EXIF orientations and they are written out
     * rather than composed, because a sign error in a composed version is
     * invisible until somebody photographs a cat sideways.
     *
     * @param upright the box, in the space CatVision reported it in
     * @param exif    1-8
     * @param file    the file's own bounds, i.e. the un-rotated image
     */
    static Rect intoFileSpace(Rect upright, int exif, Rect file) {
        if (exif <= 1 || exif > 8) {
            return new Rect(upright);
        }
        // The upright image's size, which is the file's with the axes swapped
        // for the four sideways orientations.
        boolean sideways = exif >= 5;
        int width = sideways ? file.height() : file.width();
        int height = sideways ? file.width() : file.height();

        int[] a = point(upright.left, upright.top, exif, width, height);
        int[] b = point(upright.right - 1, upright.bottom - 1, exif, width, height);

        Rect out = new Rect(Math.min(a[0], b[0]), Math.min(a[1], b[1]),
                            Math.max(a[0], b[0]) + 1, Math.max(a[1], b[1]) + 1);
        if (!out.intersect(file)) {
            return new Rect(file);
        }
        return out;
    }

    /**
     * One point, from upright coordinates back into the file's.
     *
     * @param width  the UPRIGHT image's width
     * @param height the UPRIGHT image's height
     */
    private static int[] point(int x, int y, int exif, int width, int height) {
        switch (exif) {
            case 2:  return new int[] { width - 1 - x, y };              // mirrored
            case 3:  return new int[] { width - 1 - x, height - 1 - y }; // 180
            case 4:  return new int[] { x, height - 1 - y };             // flipped
            case 5:  return new int[] { y, x };                          // transposed
            case 6:  return new int[] { y, width - 1 - x };              // 90 CW
            case 7:  return new int[] { height - 1 - y, width - 1 - x }; // transverse
            case 8:  return new int[] { height - 1 - y, x };             // 270 CW
            default: return new int[] { x, y };
        }
    }

    static Rect square(Rect rect, Rect bounds) {
        int size = Math.min(Math.max(rect.width(), rect.height()),
                            Math.min(bounds.width(), bounds.height()));
        int left = rect.centerX() - size / 2;
        int top = rect.centerY() - size / 2;
        left = Math.min(Math.max(left, bounds.left),
                        Math.max(bounds.right - size, bounds.left));
        top = Math.min(Math.max(top, bounds.top),
                       Math.max(bounds.bottom - size, bounds.top));
        return new Rect(left, top, left + size, top + size);
    }

    // --- decoding ----------------------------------------------------------

    /**
     * Decode only the square we are going to keep, and no more finely than
     * 512 px needs.
     *
     * CGImage.cropping() on iOS is nearly free because the pixels are already
     * there and lazily backed. BitmapFactory has no such thing: decoding a
     * 4032x3024 photograph whole is 48 MB of ARGB_8888 before the crop, and
     * a 50 MP one is 200 MB. BitmapRegionDecoder with inSampleSize is the
     * documented answer - it decodes the rectangle asked for, at the
     * subsampling asked for, and nothing else
     * (developer.android.com/topic/performance/graphics/load-bitmap).
     *
     * inSampleSize is the largest power of two that still leaves at least
     * SIDE pixels on a side, so the scale down to 512 is always a shrink and
     * never an upscale of subsampled pixels.
     */
    private static Bitmap decodeRegion(byte[] bytes, Rect rect) {
        BitmapFactory.Options options = new BitmapFactory.Options();
        options.inSampleSize = sampleSize(rect.width(), SIDE);
        options.inPreferredConfig = Bitmap.Config.ARGB_8888;

        BitmapRegionDecoder decoder = null;
        try {
            // The three-argument overload, not the one taking a trailing
            // boolean: that one is deprecated as of API 31 and this app's
            // floor is 33.
            decoder = BitmapRegionDecoder.newInstance(bytes, 0, bytes.length);
            Bitmap region = decoder.decodeRegion(rect, options);
            if (region != null) {
                return region;
            }
            Log.w(TAG, "decodeRegion returned nothing");
        } catch (Throwable t) {
            // BitmapRegionDecoder only knows JPEG, PNG and WebP. A picker can
            // still hand back a HEIC or a GIF, and those land here.
            Log.w(TAG, "region decode failed, decoding whole", t);
        } finally {
            if (decoder != null) {
                decoder.recycle();
            }
        }

        // Whole-image fallback, still subsampled, then cropped in memory.
        Bitmap whole = null;
        try {
            BitmapFactory.Options wholeOptions = new BitmapFactory.Options();
            wholeOptions.inSampleSize = options.inSampleSize;
            wholeOptions.inPreferredConfig = Bitmap.Config.ARGB_8888;
            whole = BitmapFactory.decodeByteArray(bytes, 0, bytes.length, wholeOptions);
            if (whole == null) {
                return null;
            }
            int sample = Math.max(1, wholeOptions.inSampleSize);
            int left = clamp(rect.left / sample, 0, whole.getWidth() - 1);
            int top = clamp(rect.top / sample, 0, whole.getHeight() - 1);
            int size = Math.max(1, Math.min(rect.width() / sample,
                    Math.min(whole.getWidth() - left, whole.getHeight() - top)));
            Bitmap region = Bitmap.createBitmap(whole, left, top, size, size);
            if (region != whole) {
                whole.recycle();
                whole = null;
            }
            return region;
        } catch (Throwable t) {
            Log.w(TAG, "whole decode failed", t);
            if (whole != null) {
                whole.recycle();
            }
            return null;
        }
    }

    static int sampleSize(int source, int wanted) {
        int sample = 1;
        while (source / (sample * 2) >= wanted) {
            sample *= 2;
        }
        return sample;
    }

    private static int clamp(int value, int low, int high) {
        return Math.max(low, Math.min(value, high));
    }

    /**
     * Turn the square the right way up.
     *
     * Not in CatPhoto.swift, and it should not be: on iOS the bytes have
     * already been through Vision, which oriented them, and re-applying
     * orientation there would turn the crop on its side. Here nothing has,
     * and a phone held upright writes a landscape JPEG with an EXIF tag
     * saying "rotate me".
     *
     * Rotating the finished 512x512 square rather than the source is exact,
     * and the old reason given here was narrower than the truth: "with no box
     * the crop is the centred square, and there is no box without Vision".
     * Both halves have expired — Android has had Vision since 50-photo/05, and
     * every photograph now arrives with a box.
     *
     * The real reason it is exact is that a quarter turn is a bijection on a
     * pixel grid, so ANY square crop holds the same set of pixels whichever
     * multiple of 90 degrees you turn it through; where the square sits does
     * not come into it. What DID depend on there being no box is the box
     * itself, which is measured on the upright photograph and has to be
     * carried back into the file's own axes before anything is decoded — see
     * {@link #intoFileSpace}, which is the bug that comment was hiding.
     *
     * android.media.ExifInterface rather than androidx, and that is a
     * deliberate choice against Google's own advice. The platform class
     * carries a note recommending the AndroidX library, and Android Lint has
     * a check named ExifInterface saying the same
     * (googlesamples.github.io/android-custom-lint-rules/checks/
     * ExifInterface.md.html). What that advice buys is bug fixes on old
     * platform versions and formats the old class could not read; this app's
     * floor is 33, one tag is read, and the tag is the most boring one there
     * is. So the androidx artefact is not added. If a device is ever seen
     * getting orientation wrong, that decision is the first thing to revisit.
     */
    private static Bitmap orient(Bitmap square, byte[] bytes) {
        int rotation;
        int mirror = 1;
        try {
            ExifInterface exif = new ExifInterface(new ByteArrayInputStream(bytes));
            switch (exif.getAttributeInt(ExifInterface.TAG_ORIENTATION,
                                         ExifInterface.ORIENTATION_NORMAL)) {
                case ExifInterface.ORIENTATION_ROTATE_90: rotation = 90; break;
                case ExifInterface.ORIENTATION_ROTATE_180: rotation = 180; break;
                case ExifInterface.ORIENTATION_ROTATE_270: rotation = 270; break;
                case ExifInterface.ORIENTATION_FLIP_HORIZONTAL: rotation = 0; mirror = -1; break;
                case ExifInterface.ORIENTATION_FLIP_VERTICAL: rotation = 180; mirror = -1; break;
                case ExifInterface.ORIENTATION_TRANSPOSE: rotation = 90; mirror = -1; break;
                case ExifInterface.ORIENTATION_TRANSVERSE: rotation = 270; mirror = -1; break;
                default: rotation = 0; break;
            }
        } catch (Throwable t) {
            // No EXIF, or unreadable EXIF. A photograph with no orientation
            // tag is a photograph that is already the right way up.
            Log.w(TAG, "no usable EXIF orientation", t);
            return square;
        }
        if (rotation == 0 && mirror == 1) {
            return square;
        }
        try {
            Matrix matrix = new Matrix();
            matrix.postScale(mirror, 1, square.getWidth() / 2f, square.getHeight() / 2f);
            matrix.postRotate(rotation);
            Bitmap turned = Bitmap.createBitmap(square, 0, 0, square.getWidth(),
                                                square.getHeight(), matrix, true);
            Log.i(TAG, "EXIF orientation applied: rotate " + rotation
                    + (mirror < 0 ? ", mirrored" : ""));
            return turned == null ? square : turned;
        } catch (Throwable t) {
            Log.w(TAG, "could not rotate", t);
            return square;
        }
    }

    /**
     * Encode, dropping quality until it fits. Quality is stepped rather than
     * fixed because a busy photo at 90 can exceed the cap while a plain one
     * never will, and re-encoding is cheaper than sending too much. The last
     * rung is returned whether it fits or not - the same
     * `|| quality == 0.4` CatPhoto.swift has - because 512x512 at quality 40
     * has never come near 200 KB and a null here costs the player her cat.
     */
    static byte[] encodeJPEG(Bitmap image, int limit) {
        for (int i = 0; i < QUALITY.length; i++) {
            ByteArrayOutputStream out = new ByteArrayOutputStream(64 * 1024);
            if (!image.compress(Bitmap.CompressFormat.JPEG, QUALITY[i], out)) {
                Log.w(TAG, "compress failed at quality " + QUALITY[i]);
                return null;
            }
            byte[] jpeg = out.toByteArray();
            if (jpeg.length <= limit || i == QUALITY.length - 1) {
                if (jpeg.length > limit) {
                    Log.w(TAG, "still " + jpeg.length + " bytes at quality "
                            + QUALITY[i] + "; sending it anyway");
                }
                return jpeg;
            }
        }
        return null;
    }

    private static void recycle(Bitmap... bitmaps) {
        for (Bitmap bitmap : bitmaps) {
            // createScaledBitmap and createBitmap can both hand back the very
            // object they were given, so the same Bitmap can appear here
            // twice; isRecycled() is what makes that safe. By the time this
            // runs the answer is already a byte[], so nothing here is still
            // needed.
            if (bitmap != null && !bitmap.isRecycled()) {
                bitmap.recycle();
            }
        }
    }
}
