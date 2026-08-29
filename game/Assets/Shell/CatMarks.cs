using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using CatShelter.Core;
using UnityEngine;

namespace CatShelter.Shell
{
    /// <summary>
    /// Task 50-photo/05: her individuating marks, measured on the device.
    /// Wraps <c>Assets/Plugins/iOS/CatMarks.swift</c>.
    ///
    /// <see cref="CatSpot"/> makes the argument this exists for: the other five
    /// traits are class characteristics and describe 288 interchangeable cats,
    /// while a white sock on ONE paw is what a person recognises her own animal
    /// by. Until now those marks could only come from a language model looking
    /// at the photograph and saying so. This measures them instead —
    /// free, offline, and without the photo leaving the phone.
    ///
    /// The native side deliberately returns NUMBERS and not verdicts: for each
    /// place, how far that patch of coat sits from her own median lightness, in
    /// CIE L* points. <see cref="Threshold"/> lives here, on the managed side,
    /// so it can be tuned against <c>fixtures/reference-photos</c> without
    /// another native build. That is the whole reason the boundary is drawn
    /// where it is.
    ///
    /// Outside iOS — the editor, a desktop build — there is no Vision, so every
    /// call answers "not available" rather than throwing, exactly as
    /// <see cref="CatVision"/> does.
    ///
    /// The data types below would sit better in <c>Core/</c> beside
    /// <see cref="VisionAnswer"/>, which moved there for being plain data with
    /// no engine reference. They are the same shape and the same argument
    /// applies. They are here only because this change was scoped to two files;
    /// moving them is a separate, mechanical commit.
    /// </summary>
    public static class CatMarks
    {
        public static bool Available =>
            Application.platform == RuntimePlatform.IPhonePlayer;

        /// <summary>
        /// How many CIE L* points a patch must sit from her body median before
        /// it counts as a mark.
        ///
        /// **Not measured. A starting value, and it must not be treated as
        /// anything else.** L* runs 0–100; about 1 point is the smallest
        /// difference an eye resolves on a flat field, and a white sock on a
        /// grey cat is tens of points. 18 is set high enough that ordinary
        /// shading — the shadow under a chin, the sheen along a flank — should
        /// not clear it, and that guess is exactly what
        /// <c>fixtures/reference-photos</c> is for.
        ///
        /// Measured 2026-08-29 over all 41 reference photographs, by compiling
        /// this very plugin for macOS and running it there
        /// (`tools/marks-probe`). The earlier note here said the reference set
        /// "cannot settle it yet" because neither iOS 17 request runs in the
        /// simulator. That was true about the simulator and wrong about the
        /// question: Vision is a macOS framework too, both requests are
        /// macOS 14+, and `CatMarks.swift` compiles against the macOS SDK
        /// unchanged. 31 of the 41 photographs reach the full rung there.
        ///
        /// **And the number is not a threshold on `delta` at all.** The run
        /// showed why: every place carries a baseline that has nothing to do
        /// with the individual cat.
        ///
        ///   paw_right +23   eye_left  +17   chin     +11   chest    +2
        ///   paw_left  +22   eye_right +16   flank     +6   tail_tip -2
        ///   paws      +17   muzzle    +16   forehead  +3
        ///
        /// A muzzle is paler than a back on essentially every cat; paws catch
        /// the light; an eye is not fur at all. Judged against the cat's own
        /// body median, those places fire on nearly every photograph — 12.2 L*
        /// was the MEDIAN of all 250 measurements, so a flat threshold there
        /// marks half of every cat. That is the class-versus-individual
        /// distinction again, one level down: "lighter than her own back" is
        /// still a fact about cats, not about this cat.
        ///
        /// What identifies is the residual: how far this cat's muzzle is from
        /// what a muzzle usually is. Hence <see cref="Baseline"/> and a
        /// threshold in spreads rather than in L*.
        /// </summary>
        public const double Threshold = 1.5;

        /// <summary>
        /// What each place looks like on an ordinary cat, in L* against her own
        /// body median, and how much it varies. Both measured over the 25 real
        /// and blurry cats of the reference set; the spread has a floor of 6 so
        /// a place with few samples cannot produce a huge score from noise.
        ///
        /// A table of eleven numbers rather than a model. It ships as a
        /// constant because it describes cats, not this player's cat, and it is
        /// re-derivable by anyone with a Mac in one command.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, (double Median, double Spread)>
            Baseline = new Dictionary<string, (double, double)>
            {
                ["muzzle"] = (15.7, 22.8),
                ["forehead"] = (3.1, 18.9),
                ["eye_left"] = (17.5, 21.6),
                ["eye_right"] = (16.1, 20.7),
                ["chin"] = (11.0, 20.2),
                ["chest"] = (2.4, 17.0),
                ["paw_left"] = (21.8, 19.5),
                ["paw_right"] = (22.7, 18.9),
                ["flank"] = (6.5, 12.5),
                ["tail_tip"] = (-2.4, 27.4),
                ["paws"] = (16.7, 28.4),
            };

        /// <summary>
        /// How unusual this place is on this cat, in spreads. Zero means she is
        /// an ordinary cat there; the sign says lighter or darker than ordinary.
        /// A place with no baseline scores 0 — unknown is not evidence.
        /// </summary>
        public static double Unusualness(MeasuredMark mark) =>
            Baseline.TryGetValue(mark.place, out var norm)
                ? (mark.delta - norm.Median) / Math.Max(norm.Spread, 6.0)
                : 0.0;

        public const double MinLandmarkConfidence = 0.5;

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern IntPtr CatMarks_measure(byte[] bytes, int length,
            int orientation, double minLandmarkConfidence);

        [DllImport("__Internal")]
        private static extern void CatMarks_free(IntPtr text);
#endif

        /// <summary>
        /// Measure the marks in an encoded image — the same 512×512 JPEG
        /// <see cref="CatPhoto.Prepare"/> makes for the Worker.
        /// </summary>
        /// <param name="orientation">
        /// A CGImagePropertyOrientation value; 0 reads it from the file's own
        /// metadata. Required for the same reason as in
        /// <see cref="CatVision.Recognise"/>: Vision keeps no orientation of
        /// its own and mis-detects silently when it is wrong. A photo from
        /// <see cref="CatPhoto.Prepare"/> is already upright, so 0 is right for
        /// the live path.
        /// </param>
        public static MarksAnswer Measure(byte[] imageBytes, int orientation = 0)
        {
            if (imageBytes == null || imageBytes.Length == 0)
                return new MarksAnswer { ok = false, error = "empty image data" };

#if UNITY_IOS && !UNITY_EDITOR
            var pointer = CatMarks_measure(imageBytes, imageBytes.Length,
                orientation, MinLandmarkConfidence);
            if (pointer == IntPtr.Zero)
                return new MarksAnswer { ok = false, error = "plugin returned nothing" };
            try
            {
                var json = Marshal.PtrToStringAnsi(pointer);
                return JsonUtility.FromJson<MarksAnswer>(json);
            }
            finally
            {
                // Swift allocated this with strdup; the marshaller will not.
                CatMarks_free(pointer);
            }
#else
            return new MarksAnswer { ok = false, error = "marks are iOS-only" };
#endif
        }
    }

    /// <summary>
    /// One measured place. Not a mark yet — <see cref="CatMarks.Threshold"/>
    /// decides that.
    /// </summary>
    [Serializable]
    public struct MeasuredMark
    {
        /// <summary>A <c>spot_place</c> from <see cref="CatTraits.Allowed"/>,
        /// or <c>paws</c> when <see cref="grouped"/> — which is not a valid
        /// place and must never reach <see cref="CatSpot"/>.</summary>
        public string place;

        /// <summary>Median CIE L* inside the sampled disc, 0–100.</summary>
        public double lightness;

        /// <summary><see cref="lightness"/> minus her body median, in L*
        /// points. Positive is lighter than her own coat, negative darker. This
        /// is the number the whole plugin exists to produce.</summary>
        public double delta;

        /// <summary>Pixels that were both inside the disc and inside the cat
        /// mask. A small count is a place half off the animal.</summary>
        public int samples;

        /// <summary>The weakest joint the place was built from. 0 on the
        /// mask-only rung, where no joint was involved.</summary>
        public double confidence;

        /// <summary>False when the place IS a landmark — an eye, the nose, a
        /// front paw, the tail tip. True when it was constructed from several,
        /// which is the case for muzzle-adjacent places Vision has no joint
        /// for: forehead, chin, chest, flank.</summary>
        public bool derived;

        /// <summary>True only for <c>paws</c>: both front paws as one number,
        /// which throws away the asymmetry that does the identifying.</summary>
        public bool grouped;

        public double Strength => Math.Abs(delta);
        public string Shade => delta > 0 ? "light" : "dark";
    }

    /// <summary>One of the 25 animal-pose joints, in pixels, origin top-left —
    /// the same convention <see cref="AnimalBox"/> uses.</summary>
    [Serializable]
    public struct PoseLandmark
    {
        public string name;
        public int x, y;
        public double confidence;
    }

    [Serializable]
    public struct MarksAnswer
    {
        public bool ok;
        public string error;
        public int imageWidth, imageHeight;

        public bool foundAnimal;
        public string identifier;
        public float confidence;

        /// <summary>Which rung of the ladder ran. See <see cref="Rung"/>.</summary>
        public string rung;

        /// <summary>Everything the plugin wanted and could not have, in plain
        /// words. Worth logging on a device build: it is the difference between
        /// "this cat has no marks" and "nothing could be measured".</summary>
        public string[] notes;

        public PoseLandmark[] landmarks;

        /// <summary>Her median lightness over the whole body, 0–100, or −1 when
        /// there was no mask and nothing was measured.</summary>
        public double bodyLightness;

        public int bodyPixels;
        public MeasuredMark[] marks;

        /// <summary>Same distinction <see cref="VisionAnswer.Failed"/> draws:
        /// "could not look" is not "found nothing".</summary>
        public bool Failed => !ok;

        public bool IsCat => foundAnimal &&
            string.Equals(identifier, "Cat", StringComparison.OrdinalIgnoreCase);

        /// <summary>True when any lightness was measured at all.</summary>
        public bool Measured => ok && bodyLightness >= 0;

        /// <summary>
        /// The measured places turned into the marks the renderer draws.
        ///
        /// Three rules, and each is a refusal rather than a guess:
        /// grouped places are dropped, because <c>paws</c> is not a
        /// <c>spot_place</c> and a mark on both front paws is not a mark;
        /// anything under <paramref name="threshold"/> is dropped; and at most
        /// <see cref="CatTraits.MaxSpots"/> survive, the strongest first,
        /// because a cat with a list of marks is a cat nobody looked at.
        /// </summary>
        public IReadOnlyList<CatSpot> ToSpots(double threshold = CatMarks.Threshold)
        {
            if (!Measured || marks == null) return Array.Empty<CatSpot>();
            return marks
                .Where(m => !m.grouped && Math.Abs(CatMarks.Unusualness(m)) >= threshold)
                .OrderByDescending(m => m.Strength)
                .Take(CatTraits.MaxSpots)
                .Select(m => new CatSpot(m.place, m.Shade))
                .ToArray();
        }
    }

    /// <summary>
    /// The rungs of the ladder, as the native side names them. Every one works
    /// when the one above does not, and the bottom two measure nothing rather
    /// than measuring something wrong.
    /// </summary>
    public static class Rung
    {
        /// <summary>Mask and pose: all ten places, each one attempted.</summary>
        public const string PoseAndMask = "pose_and_mask";

        /// <summary>Mask but no pose: three coarse bands down the silhouette —
        /// chest, flank and <c>paws</c> as a group — on the assumption that the
        /// cat is upright in frame, which is wrong for one lying down.</summary>
        public const string MaskOnly = "mask_only";

        /// <summary>Pose but no mask: landmarks are reported and nothing is
        /// measured. Without a mask there is no telling her coat from the sofa,
        /// so a body median would be a median of the room.</summary>
        public const string PoseOnly = "pose_only";

        /// <summary>Neither. Nothing measured, and <c>notes</c> says why.</summary>
        public const string None = "none";
    }
}
