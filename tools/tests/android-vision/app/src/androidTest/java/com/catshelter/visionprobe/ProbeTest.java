package com.catshelter.visionprobe;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertNotNull;
import static org.junit.Assert.assertTrue;

import android.content.Context;
import android.os.Handler;
import android.os.Looper;

import androidx.test.ext.junit.runners.AndroidJUnit4;
import androidx.test.platform.app.InstrumentationRegistry;

import com.catshelter.vision.CatVision;

import org.junit.Test;
import org.junit.runner.RunWith;

import java.io.File;
import java.io.FileOutputStream;
import java.io.IOException;
import java.io.OutputStreamWriter;
import java.io.RandomAccessFile;
import java.nio.charset.StandardCharsets;
import java.util.Arrays;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.atomic.AtomicReference;

/**
 * The Android half of tools/vision-probe: run the shipped plug-in over
 * fixtures/reference-photos and print one JSON line per image.
 *
 * <p>The fixtures are pushed to the app's own external files directory by
 * run.sh, because that is the one place adb can write and an app can read with
 * no permission at all - which keeps the harness free of a storage permission
 * the shipped game does not have either.
 *
 * <p>What is written out is the plug-in's answer and nothing else: file name,
 * label, confidence, box, mask geometry. No pixel of any photograph is written
 * anywhere by this test, and the plug-in itself writes nothing at all.
 */
@RunWith(AndroidJUnit4.class)
public class ProbeTest {

    private static final int MASK_MAX_SIDE = 512;

    private Context context() {
        return InstrumentationRegistry.getInstrumentation().getTargetContext();
    }

    /**
     * The app's own external files directory, flat - no subfolder. A folder
     * created there by `adb shell mkdir` belongs to the shell user and the app
     * cannot list it (tried; listFiles() comes back null), whereas the
     * directory the framework created for the app is readable by it and
     * writable by adb, which is the whole reason this location was chosen.
     */
    private File photos() {
        return context().getExternalFilesDir(null);
    }

    /** The whole set, one JSONL line each, into files/out.jsonl for adb pull. */
    @Test
    public void probeReferenceSet() throws Exception {
        File folder = photos();
        File[] files = folder.listFiles((dir, name) -> name.endsWith(".jpg"));
        assertNotNull("no fixtures pushed to " + folder, files);
        Arrays.sort(files);
        assertEquals("expected the 41-image reference set", 41, files.length);

        File out = new File(context().getExternalFilesDir(null), "out.jsonl");
        try (OutputStreamWriter writer = new OutputStreamWriter(
                new FileOutputStream(out, false), StandardCharsets.UTF_8)) {
            for (File file : files) {
                byte[] bytes = read(file);
                long start = System.nanoTime();
                byte[] packed = CatVision.analyse(context(), bytes, 0, MASK_MAX_SIDE);
                long millis = (System.nanoTime() - start) / 1_000_000L;

                Packet unpacked = Packet.of(packed);
                writer.write("{\"file\":\"" + file.getName() + "\",\"ms\":" + millis
                        + ",\"maskBytes\":" + unpacked.maskLength
                        + ",\"answer\":" + unpacked.json + "}\n");
            }
        }
        assertTrue("no output written", out.length() > 0);
    }

    /**
     * Not an assertion - a report. Subject segmentation is the one rung that
     * can be absent for reasons outside the app (no Play services, module not
     * downloaded yet), and "unavailable" is useless without the reason. This
     * writes the raw stack trace beside out.jsonl so NOTES-android.md can quote
     * what the device actually said.
     */
    @Test
    public void segmentationDiagnostic() throws Exception {
        StringBuilder report = new StringBuilder();
        report.append("play services: ")
                .append(com.google.android.gms.common.GoogleApiAvailability.getInstance()
                        .isGooglePlayServicesAvailable(context()))
                .append('\n');
        report.append("prepare(): ").append(CatVision.prepare(context())).append('\n');
        try {
            com.google.mlkit.vision.segmentation.subject.SubjectSegmenter client =
                    com.google.mlkit.vision.segmentation.subject.SubjectSegmentation.getClient(
                            new com.google.mlkit.vision.segmentation.subject.SubjectSegmenterOptions
                                    .Builder()
                                    .enableMultipleSubjects(
                                            new com.google.mlkit.vision.segmentation.subject
                                                    .SubjectSegmenterOptions.SubjectResultOptions
                                                    .Builder().enableConfidenceMask().build())
                                    .build());
            report.append("getClient: ok\n");
            try {
                com.google.android.gms.tasks.Tasks.await(client.getInitTask(), 120,
                        TimeUnit.SECONDS);
                report.append("initTask: ok\n");
            } catch (Throwable t) {
                report.append("initTask: ").append(trace(t));
            }
            File[] files = photos().listFiles((dir, name) -> name.equals("cat_01.jpg"));
            byte[] bytes = read(files[0]);
            android.graphics.Bitmap bitmap =
                    android.graphics.BitmapFactory.decodeByteArray(bytes, 0, bytes.length);
            try {
                Object result = com.google.android.gms.tasks.Tasks.await(
                        client.process(com.google.mlkit.vision.common.InputImage
                                .fromBitmap(bitmap, 0)), 120, TimeUnit.SECONDS);
                report.append("process: ok, subjects=")
                        .append(((com.google.mlkit.vision.segmentation.subject
                                .SubjectSegmentationResult) result).getSubjects().size())
                        .append('\n');
            } catch (Throwable t) {
                report.append("process: ").append(trace(t));
            }
        } catch (Throwable t) {
            report.append("getClient: ").append(trace(t));
        }
        File out = new File(context().getExternalFilesDir(null), "seg-error.txt");
        try (OutputStreamWriter writer = new OutputStreamWriter(
                new FileOutputStream(out, false), StandardCharsets.UTF_8)) {
            writer.write(report.toString());
        }
    }

    private void write(String name, String kind, int[] pixels, int width, int height)
            throws IOException {
        android.graphics.Bitmap out = android.graphics.Bitmap.createBitmap(
                pixels, width, height, android.graphics.Bitmap.Config.ARGB_8888);
        File file = new File(context().getExternalFilesDir(null), kind + "-" + name + ".png");
        try (FileOutputStream stream = new FileOutputStream(file)) {
            out.compress(android.graphics.Bitmap.CompressFormat.PNG, 100, stream);
        }
    }

    private static String trace(Throwable t) {
        java.io.StringWriter sw = new java.io.StringWriter();
        t.printStackTrace(new java.io.PrintWriter(sw));
        return sw.toString();
    }

    /**
     * The iOS side's VERIFY item 3 was "the reported rectangles drawn back onto
     * the photos, checked by eye" (`box-check.jpg`). A mask deserves the same
     * treatment and cannot get it from a coverage number: 0.46 is what a cat
     * fills and also what a sofa fills. This paints the mask over the
     * photograph in red and leaves the PNGs beside out.jsonl for a person to
     * look at.
     *
     * <p>Writes only fixture images, never a player's. Skipped silently when
     * no mask came back, which is every device without the optional module.
     */
    @Test
    public void maskOverlaysForEyeChecking() throws Exception {
        for (String name : new String[]{"cat_01.jpg", "cat_09.jpg", "multi_03.jpg",
                                        "dog_01.jpg", "ofphoto_02.jpg"}) {
            File[] files = photos().listFiles((dir, n) -> n.equals(name));
            if (files == null || files.length == 0) {
                continue;
            }
            byte[] bytes = read(files[0]);
            Packet unpacked = Packet.of(CatVision.analyse(context(), bytes, 0, MASK_MAX_SIDE));
            if (unpacked.maskLength == 0) {
                continue;
            }
            int maskWidth = intField(unpacked.json, "maskWidth");
            int maskHeight = intField(unpacked.json, "maskHeight");

            // Two pictures. The overlay is the one a person judges - is that
            // the cat and only the cat - and it paints everything outside her
            // flat magenta, because "slightly darker" is not a judgement anyone
            // can make by eye. The bare mask is the one that settles an
            // argument about whether the overlay itself is drawing correctly.
            int[] pixels = new int[maskWidth * maskHeight];
            android.graphics.Bitmap photo = android.graphics.Bitmap.createScaledBitmap(
                    android.graphics.BitmapFactory.decodeByteArray(bytes, 0, bytes.length),
                    maskWidth, maskHeight, true);
            photo.getPixels(pixels, 0, maskWidth, 0, 0, maskWidth, maskHeight);

            int[] bare = new int[maskWidth * maskHeight];
            for (int i = 0; i < pixels.length; i++) {
                int alpha = unpacked.mask[i] & 0xFF;
                bare[i] = 0xFF000000 | (alpha << 16) | (alpha << 8) | alpha;
                if (alpha < 128) {
                    pixels[i] = 0xFFFF00FF;
                }
            }
            write(name, "overlay", pixels, maskWidth, maskHeight);
            write(name, "mask", bare, maskWidth, maskHeight);
        }
    }

    /** Rung 3: bytes that are not an image come back cleanly, not thrown. */
    @Test
    public void garbageBytesDegrade() {
        byte[] packed = CatVision.analyse(context(), new byte[]{1, 2, 3, 4, 5}, 0, MASK_MAX_SIDE);
        Packet unpacked = Packet.of(packed);
        assertTrue(unpacked.json, unpacked.json.contains("\"ok\":false"));
        assertEquals(0, unpacked.maskLength);
    }

    @Test
    public void emptyBytesDegrade() {
        Packet unpacked = Packet.of(CatVision.analyse(context(), new byte[0], 0, MASK_MAX_SIDE));
        assertTrue(unpacked.json, unpacked.json.contains("empty image data"));
    }

    /**
     * Unity calls this from the main thread, and ML Kit's Tasks.await refuses
     * to run there. If the executor hop in CatVision.analyse were ever removed
     * this test would hang rather than fail quietly on a player's phone.
     */
    @Test
    public void shouldNotDeadlockOnMainThread() throws Exception {
        File[] files = photos().listFiles((dir, name) -> name.equals("cat_01.jpg"));
        assertNotNull(files);
        assertEquals(1, files.length);
        byte[] bytes = read(files[0]);

        AtomicReference<String> result = new AtomicReference<>();
        CountDownLatch done = new CountDownLatch(1);
        new Handler(Looper.getMainLooper()).post(() -> {
            try {
                result.set(Packet.of(CatVision.analyse(context(), bytes, 0, MASK_MAX_SIDE)).json);
            } catch (Throwable t) {
                result.set("threw " + t);
            } finally {
                done.countDown();
            }
        });
        assertTrue("main-thread call did not return within 60s",
                done.await(60, TimeUnit.SECONDS));
        assertTrue(String.valueOf(result.get()), result.get().contains("\"ok\":true"));
    }

    /** EXIF 6 is a phone held sideways; the answer must be in upright pixels. */
    @Test
    public void orientationSwapsReportedSize() throws Exception {
        File[] files = photos().listFiles((dir, name) -> name.equals("cat_01.jpg"));
        assertNotNull(files);
        byte[] bytes = read(files[0]);
        Packet upright = Packet.of(CatVision.analyse(context(), bytes, 1, 0));
        Packet sideways = Packet.of(CatVision.analyse(context(), bytes, 6, 0));
        int w = intField(upright.json, "imageWidth");
        int h = intField(upright.json, "imageHeight");
        assertEquals(h, intField(sideways.json, "imageWidth"));
        assertEquals(w, intField(sideways.json, "imageHeight"));
    }

    private static int intField(String json, String name) {
        int at = json.indexOf('"' + name + "\":");
        assertTrue(name + " missing from " + json, at >= 0);
        int from = at + name.length() + 3;
        int to = from;
        while (to < json.length() && (Character.isDigit(json.charAt(to)))) {
            to++;
        }
        return Integer.parseInt(json.substring(from, to));
    }

    private static byte[] read(File file) throws IOException {
        try (RandomAccessFile handle = new RandomAccessFile(file, "r")) {
            byte[] bytes = new byte[(int) handle.length()];
            handle.readFully(bytes);
            return bytes;
        }
    }

    /** The packed reply of Packet.java, read back the way C# will read it. */
    private static final class Packet {
        String json;
        int maskLength;
        byte[] mask;

        static Packet of(byte[] packed) {
            assertNotNull("plug-in returned null", packed);
            assertTrue("packet too short: " + packed.length, packed.length >= 8);
            assertEquals('C', packed[0]);
            assertEquals('V', packed[1]);
            assertEquals('S', packed[2]);
            assertEquals('1', packed[3]);
            int length = ((packed[4] & 0xFF) << 24) | ((packed[5] & 0xFF) << 16)
                    | ((packed[6] & 0xFF) << 8) | (packed[7] & 0xFF);
            assertTrue("json length out of range: " + length, length >= 0 && 8 + length <= packed.length);
            Packet out = new Packet();
            out.json = new String(packed, 8, length, StandardCharsets.UTF_8);
            out.maskLength = packed.length - 8 - length;
            out.mask = Arrays.copyOfRange(packed, 8 + length, packed.length);
            return out;
        }
    }
}
