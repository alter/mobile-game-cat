package com.catshelter.vision;

import static org.junit.Assert.assertEquals;
import static org.junit.Assert.assertNotNull;
import static org.junit.Assert.assertTrue;

import androidx.test.ext.junit.runners.AndroidJUnit4;

import com.google.mlkit.vision.segmentation.subject.Subject;

import org.junit.Test;
import org.junit.runner.RunWith;

import java.nio.FloatBuffer;

/**
 * The mask half of the plug-in, without Google Play services.
 *
 * <p>Task 50-photo/05 on Android has one hole it cannot close on an emulator:
 * the subject-segmentation optional module never downloads, so no real mask has
 * ever been produced (see NOTES-android.md). That leaves
 * {@code CatVision.Mask.from} — the code that takes a subject-sized confidence
 * buffer and places it into whole-image coordinates — never executed, and it is
 * exactly the kind of arithmetic that is wrong the first time.
 *
 * <p>{@link Subject} has a public constructor, so a mask can be built by hand
 * and the placement checked without any model at all. This does not prove
 * segmentation finds a cat. It proves that when it does, her silhouette lands
 * where the C# side will look for it.
 *
 * <p>In package {@code com.catshelter.vision} on purpose: {@code Mask} is
 * package-private, and widening it so a test could reach it would be letting
 * the test decide the plug-in's public surface.
 */
@RunWith(AndroidJUnit4.class)
public class MaskGeometryTest {

    /** A solid subject occupying a known rectangle of a known image. */
    private static Subject subject(int startX, int startY, int width, int height) {
        FloatBuffer confidence = FloatBuffer.allocate(width * height);
        for (int i = 0; i < width * height; i++) {
            confidence.put(i, 1f);
        }
        return new Subject(confidence, null, width, height, startX, startY);
    }

    /** The constructor's int order is undocumented; pin it before relying on it. */
    @Test
    public void subjectConstructorArgumentOrder() {
        Subject s = subject(11, 22, 33, 44);
        assertEquals(11, s.getStartX());
        assertEquals(22, s.getStartY());
        assertEquals(33, s.getWidth());
        assertEquals(44, s.getHeight());
    }

    /** No downscale: every pixel of the box is set, everything outside is 0. */
    @Test
    public void placesTheSubjectWhereItSits() {
        CatVision.Mask mask = CatVision.Mask.from(subject(10, 20, 30, 40), 100, 100, 100);
        assertNotNull(mask);
        assertEquals(100, mask.width);
        assertEquals(100, mask.height);

        assertEquals(255, mask.bytes[20 * 100 + 10] & 0xFF);
        assertEquals(255, mask.bytes[59 * 100 + 39] & 0xFF);
        assertEquals(0, mask.bytes[19 * 100 + 10] & 0xFF);
        assertEquals(0, mask.bytes[20 * 100 + 9] & 0xFF);
        assertEquals(0, mask.bytes[60 * 100 + 39] & 0xFF);
        assertEquals(0, mask.bytes[59 * 100 + 40] & 0xFF);

        int inside = 0;
        for (byte b : mask.bytes) {
            if ((b & 0xFF) > 0) {
                inside++;
            }
        }
        assertEquals("the whole box and nothing else", 30 * 40, inside);
    }

    /**
     * Downscaled, which is the case that actually ships: 512 on the long side
     * of a 2048x1536 photograph. The box must still land in the right quarter
     * and cover about the right share.
     */
    @Test
    public void survivesTheDownscale() {
        int imageWidth = 2048, imageHeight = 1536;
        CatVision.Mask mask = CatVision.Mask.from(
                subject(1024, 768, 512, 384), imageWidth, imageHeight, 512);
        assertNotNull(mask);
        assertEquals(512, mask.width);
        assertEquals(384, mask.height);

        // The subject occupies the bottom-right quarter's top-left corner.
        assertEquals(0, mask.bytes[10 * 512 + 10] & 0xFF);
        assertEquals(255, mask.bytes[200 * 512 + 270] & 0xFF);

        float coverage = Packet.coverage(mask.bytes);
        // 512*384 of 2048*1536 is exactly a sixteenth.
        assertTrue("coverage " + coverage, coverage > 0.055f && coverage < 0.07f);
    }

    /** A subject with no confidence mask is not a mask, and must not throw. */
    @Test
    public void noConfidenceBufferGivesNoMask() {
        assertEquals(null, CatVision.Mask.from(
                new Subject(null, null, 10, 10, 0, 0), 100, 100, 100));
    }

    /** Coverage is a share, and an absent mask is not 100% of anything. */
    @Test
    public void coverageOfNothingIsZero() {
        assertEquals(0f, Packet.coverage(null), 0f);
        assertEquals(0f, Packet.coverage(new byte[0]), 0f);
        assertEquals(0.5f, Packet.coverage(new byte[]{(byte) 255, 0}), 0f);
        // 127 is below half confidence, 128 is at it.
        assertEquals(0f, Packet.coverage(new byte[]{127}), 0f);
        assertEquals(1f, Packet.coverage(new byte[]{(byte) 128}), 0f);
    }
}
