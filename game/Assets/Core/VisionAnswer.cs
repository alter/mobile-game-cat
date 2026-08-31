using System;
using System.Linq;

namespace CatShelter.Core
{
    /// <summary>
    /// Task 50-photo/06 VERIFY (closed 2026-08-27, after
    /// <see cref="TraitsRequest"/> and <see cref="PhotoMessageKey"/> made the
    /// same move for the same reason): what `Plugins/iOS/CatVision.swift`
    /// hands back, decoded. Plain data plus two rules on it
    /// (<see cref="VisionAnswer.Best"/>, <see cref="VisionAnswer.Failed"/>),
    /// no engine reference anywhere in it — the `[Serializable]` attribute is
    /// `System.SerializableAttribute`, not Unity's, so it costs nothing to
    /// move. What cannot move is `Shell.CatVision`: the `DllImport`, the
    /// marshalling, and `JsonUtility.FromJson` all need the engine and stay
    /// there, now referencing this file instead of defining it.
    /// </summary>
    [Serializable]
    public struct AnimalBox
    {
        public string identifier;   // "Cat" or "Dog" — Vision knows no other animal
        public float confidence;
        public int x, y, width, height;

        public bool IsCat => string.Equals(identifier, "Cat", StringComparison.OrdinalIgnoreCase);
    }

    [Serializable]
    public struct VisionAnswer
    {
        public bool ok;
        public string error;
        public int imageWidth, imageHeight;
        public AnimalBox[] detections;

        public bool FoundAnimal => ok && detections != null && detections.Length > 0;

        // 50-photo/05 VERIFY item: "found nothing" and "could not look" used
        // to be the same thing to every caller — both left FoundAnimal false,
        // and nothing read `ok` or `error`. They are different: this is not a
        // judgement about the photo, and the player should not be told her
        // cat wasn't recognised when the truth is the device couldn't run
        // Vision at all (decode failure, empty bytes, not iOS, or
        // handler.perform threw).
        public bool Failed => !ok;

        /// <summary>
        /// The cat, if this photograph holds one; otherwise whatever the
        /// recogniser was most sure of.
        ///
        /// It was the plain maximum over every detection until 2026-09-01, and
        /// that is a different question from the one this game asks. Android
        /// does not label the frame — it cuts every subject out of it and
        /// labels each crop separately (`CatVision.java`, the loop over
        /// `subjects`), so a photograph of a cat on a sofa arrives here as
        /// several detections. Taking the largest number across all of them
        /// answers "what is the recogniser surest about in this picture",
        /// which a cushion can win.
        ///
        /// The owner's report is what it looks like from the outside: "похоже
        /// на собаку" on many of his cats. Three of the photographs he sent
        /// score Cat 0.99, 0.97 and 0.88 here on their own — one of them had
        /// already been accepted by this same build on an emulator — so
        /// nothing was wrong with his cats or with the labeller. Something
        /// ELSE in his rooms scored higher and was called a dog, and his cat
        /// lost a contest she should never have been entered in.
        ///
        /// So a cat outranks everything. We are a cat shelter: the question is
        /// whether there is a cat in this photograph, not what the largest
        /// animal in it is. The asymmetry is deliberate and it is the cheap
        /// direction to be wrong in — a dog admitted beside a cat costs us a
        /// cat drawn from a dog's colours, while a cat turned away costs the
        /// player the only thing the game promised her.
        ///
        /// A real dog photograph is unaffected: no Cat detection, so the
        /// maximum still decides, and `PhotoJudge` still has the kind message
        /// about somebody's dog.
        ///
        /// (Before that it was `detections[0]`, on the strength of a comment
        /// saying the plugin sorts by confidence — true in
        /// `Plugins/iOS/CatVision.swift`, checked nowhere. Ordering here means
        /// correctness does not depend on the Swift order or on remembering to
        /// re-check it. That still holds; only the key has changed.)
        /// </summary>
        public AnimalBox Best =>
            detections.OrderByDescending(d => d.IsCat)
                      .ThenByDescending(d => d.confidence)
                      .First();
    }
}
