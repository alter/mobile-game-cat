using System;
using System.IO;
using CatShelter.Core;
using NUnit.Framework;

namespace CatShelter.Core.Tests
{
    /// <summary>
    /// Task 50-photo/07: the payload gap the audit found — the crop and the
    /// 200 KB ceiling existed, but nothing turned prepared bytes into the
    /// image_base64 body worker/src/index.ts requires. This is that envelope,
    /// checked against the Worker's own source and tests rather than against
    /// an assumption of what it wants.
    /// </summary>
    [TestFixture]
    public class TraitsRequestTests
    {
        private static byte[] Jpegish(int length, byte fill = 0x42)
        {
            var bytes = new byte[length];
            // A real JPEG starts 0xFF 0xD8; not decoded by anything here, so
            // the marker is cosmetic, but it keeps the fixture honest about
            // what kind of bytes this type is meant to carry.
            if (length >= 2) { bytes[0] = 0xFF; bytes[1] = 0xD8; }
            for (var i = 2; i < length; i++) bytes[i] = fill;
            return bytes;
        }

        private static string ExtractField(string json, string field)
        {
            var marker = $"\"{field}\":\"";
            var start = json.IndexOf(marker, StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), $"{field} not found in {json}");
            start += marker.Length;
            var end = json.IndexOf('"', start);
            Assert.That(end, Is.GreaterThan(start));
            return json.Substring(start, end - start);
        }

        [Test]
        public void ARoundTrip_BytesInBase64OutDecodesBackToTheSameBytes()
        {
            var original = Jpegish(4096);

            var json = TraitsRequest.BuildJson(original, "device-1");
            var base64 = ExtractField(json, "image_base64");
            var decoded = Convert.FromBase64String(base64);

            Assert.That(decoded, Is.EqualTo(original));
        }

        [Test]
        public void TheBodyCarriesExactlyTheThreeFieldsTheWorkerReads()
        {
            var json = TraitsRequest.BuildJson(Jpegish(1024), "device-1");

            Assert.That(json, Does.Contain("\"image_base64\":"));
            Assert.That(json, Does.Contain("\"media_type\":\"image/jpeg\""));
            Assert.That(json, Does.Contain("\"device_id\":\"device-1\""));
        }

        // The Worker itself answers 400 to an empty image_base64
        // (worker/test/traits.test.ts: "rejects an empty image with 400",
        // "rejects a missing image with 400"). Refusing here means that
        // request never leaves the device at all.
        [Test]
        public void ANullPhotoIsRejected_TheSameWayTheWorkerWouldRejectIt()
        {
            Assert.Throws<ArgumentException>(() => TraitsRequest.BuildJson(null, "device-1"));
        }

        [Test]
        public void AnEmptyPhotoIsRejected_TheSameWayTheWorkerWouldRejectIt()
        {
            Assert.Throws<ArgumentException>(
                () => TraitsRequest.BuildJson(Array.Empty<byte>(), "device-1"));
        }

        [Test]
        public void APhotoOverThePreEncodeCeilingIsRejected()
        {
            // Mirrors Shell.CatPhoto.MaxBytes (200 KB) — the ceiling
            // CatPhoto.Prepare is supposed to have already enforced. Checked
            // again here so this type never trusts an oversized crop through.
            var tooLarge = Jpegish(TraitsRequest.MaxPreEncodeBytes + 1);
            Assert.Throws<ArgumentException>(() => TraitsRequest.BuildJson(tooLarge, "device-1"));
        }

        [Test]
        public void APhotoAtExactlyThePreEncodeCeilingIsAccepted()
        {
            var exact = Jpegish(TraitsRequest.MaxPreEncodeBytes);
            Assert.DoesNotThrow(() => TraitsRequest.BuildJson(exact, "device-1"));
        }

        [Test]
        public void EncodingNeverReachesTheWorkersPostEncodeCeiling()
        {
            // worker/src/index.ts MAX_BODY_BYTES is 400 KB, checked on the
            // base64 string. Base64 inflates by roughly a third, so the
            // largest input this type accepts (200 KB) never gets close —
            // recorded as a test so the two constants drifting apart would be
            // caught here rather than only in the Worker's own suite.
            var json = TraitsRequest.BuildJson(Jpegish(TraitsRequest.MaxPreEncodeBytes), "device-1");
            var base64 = ExtractField(json, "image_base64");
            Assert.That(base64.Length, Is.LessThan(TraitsRequest.MaxEncodedBytes));
        }

        [TestCase(null)]
        [TestCase("")]
        public void AMissingDeviceIdBecomesAnonymous_TheSameDefaultTheWorkerUses(string deviceId)
        {
            // worker/src/index.ts:97-99 falls back to "anonymous" itself when
            // device_id is missing or empty; this type matches that default
            // rather than sending a request the Worker would treat differently.
            var json = TraitsRequest.BuildJson(Jpegish(64), deviceId);
            Assert.That(ExtractField(json, "device_id"), Is.EqualTo("anonymous"));
        }

        [Test]
        public void AHostileDeviceIdCannotBreakTheJson()
        {
            // The escaping branches had no test and the coverage report showed
            // it (JsonString 11/16 lines). A device id is not expected to carry
            // a quote, a backslash or a control character — but the class
            // promises that a bad request never leaves the device, and an
            // unescaped one produces a body the Worker answers 400 to for a
            // reason nobody could read from the game's side.
            var nasty = "d\"1\\2\n3\t4\a5";   // \a is U+0007, a bare control char
            var json = TraitsRequest.BuildJson(Jpegish(64), nasty);

            Assert.That(json, Does.Contain("\\\""), "the quote is not escaped");
            Assert.That(json, Does.Contain("\\\\"), "the backslash is not escaped");
            Assert.That(json, Does.Contain("\\n"), "the newline is not escaped");
            Assert.That(json, Does.Contain("\\t"), "the tab is not escaped");
            Assert.That(json, Does.Contain("\\u0007"),
                "a bare control character is not escaped, so the body is not JSON");

            // No raw control character survives anywhere in the body.
            foreach (var c in json)
                Assert.That(c, Is.GreaterThanOrEqualTo(' '),
                    $"raw control character U+{(int)c:x4} in the request body");
        }

        [Test]
        public void TheMediaTypeIsAlwaysAWorkerAllowedType()
        {
            // ALLOWED_MEDIA in worker/src/index.ts is {"image/jpeg", "image/png"}.
            // CatPhoto only ever produces JPEG, so that is the only value this
            // type ever emits — checked against the Worker's own list rather
            // than assumed.
            var indexTsPath = FindWorkerFile("src/index.ts");
            var source = File.ReadAllText(indexTsPath);
            Assert.That(source, Does.Contain($"\"{TraitsRequest.MediaType}\""),
                "TraitsRequest.MediaType is not one of the Worker's ALLOWED_MEDIA values");
        }

        [Test]
        public void TheFieldNamesMatchWhatTheWorkerActuallyReads()
        {
            // Cross-language check, the same idiom as
            // CatTraitsTests.TheAllowedValuesMatchTheWorkerSchema: read the
            // Worker's own source rather than trust that this file still
            // agrees with it.
            var indexTsPath = FindWorkerFile("src/index.ts");

            var source = File.ReadAllText(indexTsPath);
            Assert.That(source, Does.Contain("payload.image_base64"),
                "worker/src/index.ts no longer reads image_base64");
            Assert.That(source, Does.Contain("payload.media_type"),
                "worker/src/index.ts no longer reads media_type");
            Assert.That(source, Does.Contain("payload.device_id"),
                "worker/src/index.ts no longer reads device_id");

            var json = TraitsRequest.BuildJson(Jpegish(64), "d1");
            Assert.That(json, Does.Contain("\"image_base64\":"));
            Assert.That(json, Does.Contain("\"media_type\":"));
            Assert.That(json, Does.Contain("\"device_id\":"));
        }

        [Test]
        public void TheEncodedFieldStaysUnderTheWorkersPostEncodeCeilingConstant()
        {
            // The 400 KB figure itself is read out of the Worker's source
            // rather than assumed to still be 400*1024 here.
            var indexTsPath = FindWorkerFile("src/index.ts");
            var source = File.ReadAllText(indexTsPath);
            Assert.That(source, Does.Contain("MAX_BODY_BYTES = 400 * 1024"),
                "the Worker's post-encode ceiling moved; TraitsRequest.MaxEncodedBytes needs to follow");
        }

        private static string FindWorkerFile(string relative)
        {
            var path = Path.GetFullPath(Path.Combine(
                TestContext.CurrentContext.TestDirectory,
                "..", "..", "..", "..", "..", "worker", relative));
            // Fail here; never Assert.Ignore. A cross-language check that skips
            // itself when its path is wrong is a check that has silently
            // stopped running — the same shape of defect as the coverage gate
            // nobody ever invoked (tasks/AUDIT-2026-08-27.md, item 4). If
            // worker/ is not where this expects it, that is a finding, and a
            // suite reporting green on a check it did not perform is worse
            // than a red one.
            Assert.That(File.Exists(path), Is.True,
                $"worker/{relative} not found at {path} — either the repository "
                + "layout moved or this walk-up is wrong, and the cross-language "
                + "check below is not running.");
            return path;
        }
    }
}
