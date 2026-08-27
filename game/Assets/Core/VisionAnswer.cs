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

        // 50-photo/06 VERIFY item 2: this used to be detections[0] on the
        // strength of a comment ("plugin sorts by confidence") — true in
        // Plugins/iOS/CatVision.swift today, checked nowhere. Picking the max
        // here means correctness no longer depends on the Swift ordering, or
        // on remembering to re-check it if that file ever changes.
        public AnimalBox Best => detections.OrderByDescending(d => d.confidence).First();
    }
}
