using System;
using System.Text;

namespace CatShelter.Core
{
    /// <summary>
    /// Task 50-photo/07: the last step before the network call that
    /// 50-photo/08 has not written yet — turns a prepared JPEG (already
    /// cropped and downscaled by Shell.CatPhoto) into the exact JSON body
    /// POST /traits expects.
    ///
    /// Engine-free on purpose: the crop is a native plugin call (Shell), but
    /// building this envelope is plain data with no engine dependency, so it
    /// belongs in Core where it can be tested by dotnet test without Unity or
    /// a device (build/core-tests/core-tests.csproj compiles Assets/Core only).
    ///
    /// Field names, the media type and both size ceilings are read off
    /// worker/src/index.ts and worker/test/traits.test.ts, not invented here —
    /// CatTraitsTests.TheAllowedValuesMatchTheWorkerSchema in the sibling test
    /// file does the same cross-check for CatTraits against
    /// tools/traits/schema.json; TraitsRequestTests does it for this file
    /// against worker/src/index.ts.
    ///
    /// No JSON library: Core stays dependency-free the same way GameSave does
    /// (System.Text.Json is IL2CPP-forbidden; Newtonsoft would put a
    /// dependency inside Core). Three fields, one of them free text, does not
    /// need one.
    /// </summary>
    public static class TraitsRequest
    {
        /// <summary>
        /// Mirrors Shell.CatPhoto.MaxBytes. CatPhoto.Prepare already enforces
        /// this on the crop; it is enforced again here because this type has
        /// no reference to Shell and must not trust its caller blindly — a
        /// second check that never fires in the real pipeline is cheap
        /// insurance against the two constants drifting apart.
        /// </summary>
        public const int MaxPreEncodeBytes = 200 * 1024;

        /// <summary>
        /// worker/src/index.ts MAX_BODY_BYTES — the ceiling the Worker checks
        /// on the base64 string itself (index.ts:64, :81), after encoding.
        /// Base64 inflates by roughly a third, so 200 KB in never gets within
        /// reach of this; kept as a second, independent guard rather than
        /// trusted to follow mathematically from <see cref="MaxPreEncodeBytes"/>.
        /// </summary>
        public const int MaxEncodedBytes = 400 * 1024;

        /// <summary>The only media type Shell.CatPhoto ever produces.</summary>
        public const string MediaType = "image/jpeg";

        /// <summary>What the Worker uses when no device id was sent
        /// (index.ts:97-99). Duplicated here so a caller that skips the id
        /// gets the exact same string the Worker would have chosen for it.</summary>
        public const string AnonymousDeviceId = "anonymous";

        /// <summary>
        /// Build the JSON body for POST /traits. Throws
        /// <see cref="ArgumentException"/> for input the Worker would reject
        /// anyway — empty or over either size ceiling — so a bad request never
        /// leaves the device.
        /// </summary>
        public static string BuildJson(byte[] jpegBytes, string deviceId)
        {
            if (jpegBytes == null || jpegBytes.Length == 0)
                throw new ArgumentException("jpegBytes is empty", nameof(jpegBytes));
            if (jpegBytes.Length > MaxPreEncodeBytes)
                throw new ArgumentException(
                    $"jpegBytes is {jpegBytes.Length} bytes, over the {MaxPreEncodeBytes}-byte ceiling",
                    nameof(jpegBytes));

            var base64 = Convert.ToBase64String(jpegBytes);
            if (base64.Length > MaxEncodedBytes)
                throw new ArgumentException(
                    $"encoded image is {base64.Length} bytes, over the {MaxEncodedBytes}-byte ceiling",
                    nameof(jpegBytes));

            var id = string.IsNullOrEmpty(deviceId) ? AnonymousDeviceId : deviceId;

            var sb = new StringBuilder();
            sb.Append('{');
            sb.Append("\"image_base64\":\"").Append(base64).Append("\",");
            sb.Append("\"media_type\":\"").Append(MediaType).Append("\",");
            sb.Append("\"device_id\":").Append(JsonString(id));
            sb.Append('}');
            return sb.ToString();
        }

        private static string JsonString(string value)
        {
            var sb = new StringBuilder();
            sb.Append('"');
            foreach (var c in value)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        // Any other control character has to be escaped too or
                        // the body is not valid JSON at all. A device id is not
                        // expected to carry one, but "not expected" is how the
                        // class's own promise — that a bad request never leaves
                        // the device — gets quietly broken.
                        if (c < ' ')
                            sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else
                            sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
            return sb.ToString();
        }
    }
}
