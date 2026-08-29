package com.catshelter.vision;

import java.io.ByteArrayOutputStream;
import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.List;

/**
 * Task 50-photo/05: what crosses the JNI boundary, and how.
 *
 * <p>iOS returns one JSON string. Android has to return that same JSON AND a
 * mask of a few hundred thousand bytes, and a mask is not something to
 * base64 into a string. So one {@code byte[]} carries both:
 *
 * <pre>
 *   "CVS1"          4 bytes, magic
 *   jsonLength      4 bytes, big-endian int32
 *   json            jsonLength bytes, UTF-8, the iOS shape plus mask fields
 *   mask            maskWidth * maskHeight bytes, one per pixel, 0..255
 * </pre>
 *
 * <p>One call, one marshalling, no static state holding a photograph between
 * calls. The JSON half is byte-identical in shape to what
 * {@code Plugins/iOS/CatVision.swift} encodes, so
 * {@code Core/VisionAnswer} deserialises both without knowing which platform
 * produced it.
 *
 * <p>Hand-built JSON rather than org.json: the field set is six names long and
 * fixed, and org.json would put no ordering guarantee on it. Every string that
 * reaches {@link #escape} is one of this file's own literals or an ML Kit
 * label, never anything read off the photograph.
 */
final class Packet {

    static final byte[] MAGIC = {'C', 'V', 'S', '1'};

    /** One Cat-or-Dog box, in full-resolution pixels, origin top-left. */
    static final class Detection {
        String identifier;
        float confidence;
        int x, y, width, height;
    }

    static byte[] failure(String message) {
        StringBuilder json = new StringBuilder(96);
        json.append("{\"ok\":false,\"error\":\"").append(escape(message)).append('"');
        json.append(",\"imageWidth\":0,\"imageHeight\":0,\"detections\":[]");
        json.append(",\"maskWidth\":0,\"maskHeight\":0,\"maskSource\":\"none\"");
        json.append(",\"maskCoverage\":0,\"rung\":\"none\"}");
        return pack(json.toString(), null);
    }

    static byte[] success(int imageWidth, int imageHeight,
                          List<Detection> detections,
                          String rung, String note,
                          byte[] mask, int maskWidth, int maskHeight,
                          String maskSource) {
        StringBuilder json = new StringBuilder(256);
        json.append("{\"ok\":true,\"error\":");
        json.append(note == null ? "null" : "\"" + escape(note) + "\"");
        json.append(",\"imageWidth\":").append(imageWidth);
        json.append(",\"imageHeight\":").append(imageHeight);
        json.append(",\"detections\":[");
        for (int i = 0; i < detections.size(); i++) {
            Detection d = detections.get(i);
            if (i > 0) {
                json.append(',');
            }
            json.append("{\"identifier\":\"").append(escape(d.identifier)).append('"');
            json.append(",\"confidence\":").append(round(d.confidence));
            json.append(",\"x\":").append(d.x).append(",\"y\":").append(d.y);
            json.append(",\"width\":").append(d.width);
            json.append(",\"height\":").append(d.height).append('}');
        }
        json.append(']');
        json.append(",\"maskWidth\":").append(mask == null ? 0 : maskWidth);
        json.append(",\"maskHeight\":").append(mask == null ? 0 : maskHeight);
        json.append(",\"maskSource\":\"").append(escape(maskSource)).append('"');
        json.append(",\"maskCoverage\":").append(round(coverage(mask)));
        json.append(",\"rung\":\"").append(escape(rung)).append("\"}");
        return pack(json.toString(), mask);
    }

    /**
     * Share of the mask at or above half confidence. Not a verdict - C# decides
     * whether 0.9 means "the cat fills the frame" or "the segmenter grabbed the
     * sofa" - but it is the one number that tells the two apart cheaply, and it
     * costs one pass over bytes already in hand.
     */
    static float coverage(byte[] mask) {
        if (mask == null || mask.length == 0) {
            return 0f;
        }
        int hits = 0;
        for (byte b : mask) {
            if ((b & 0xFF) >= 128) {
                hits++;
            }
        }
        return hits / (float) mask.length;
    }

    private static byte[] pack(String json, byte[] mask) {
        byte[] text = json.getBytes(StandardCharsets.UTF_8);
        ByteArrayOutputStream out =
                new ByteArrayOutputStream(8 + text.length + (mask == null ? 0 : mask.length));
        out.write(MAGIC, 0, MAGIC.length);
        out.write((text.length >>> 24) & 0xFF);
        out.write((text.length >>> 16) & 0xFF);
        out.write((text.length >>> 8) & 0xFF);
        out.write(text.length & 0xFF);
        out.write(text, 0, text.length);
        if (mask != null) {
            out.write(mask, 0, mask.length);
        }
        return out.toByteArray();
    }

    /** Three decimals. Enough for a confidence, and it keeps the JSON short. */
    private static String round(float value) {
        return String.format(java.util.Locale.US, "%.3f", value);
    }

    private static String escape(String value) {
        if (value == null) {
            return "";
        }
        StringBuilder out = new StringBuilder(value.length() + 8);
        for (int i = 0; i < value.length(); i++) {
            char c = value.charAt(i);
            switch (c) {
                case '"': out.append("\\\""); break;
                case '\\': out.append("\\\\"); break;
                case '\n': out.append("\\n"); break;
                case '\r': out.append("\\r"); break;
                case '\t': out.append("\\t"); break;
                default:
                    if (c < 0x20) {
                        out.append(String.format(java.util.Locale.US, "\\u%04x", (int) c));
                    } else {
                        out.append(c);
                    }
            }
        }
        return out.toString();
    }

    static List<Detection> emptyDetections() {
        return new ArrayList<>(2);
    }

    private Packet() {
    }
}
