using CatShelter.Core;
using UnityEngine;

namespace CatShelter.Shell
{
    /// <summary>
    /// Whose cat this is. Rolled once on the first launch, kept for good.
    ///
    /// The owner's argument, and it is about the product rather than the code:
    /// if every player meets the same grey tabby then a shared picture says
    /// nothing, and nobody learns that the cats vary. A kitten that is fat or
    /// thin, ginger or olive, striped or plain gives a player something that is
    /// theirs before they have given the game anything.
    ///
    /// This is deliberately **not** a claim to be her real cat. The traits carry
    /// <see cref="TraitsOrigin.Skipped"/> because that is still the truth —
    /// nobody has handed the game a photograph. The moment one arrives,
    /// 50-photo replaces this and the rolled cat is gone; it is a placeholder
    /// with a face, not a substitute for the promise the game makes.
    ///
    /// Stored in the same file the photo flow uses (<see cref="CatSaveFile"/>),
    /// so there is one answer to "who is the cat" and not two that can disagree.
    /// </summary>
    public static class CatIdentity
    {
        private static CatTraits _cached;

        /// <summary>
        /// The player's cat. Reads the save; rolls and writes one if there is
        /// none. Never returns null — a launch that cannot read or write a file
        /// still has to show somebody.
        /// </summary>
        public static CatTraits Traits
        {
            get
            {
                if (_cached != null) return _cached;

                var saved = CatSave.Read(CatSaveFile.Read());
                if (saved?.Traits != null)
                {
                    _cached = saved.Traits;
                    return _cached;
                }

                // The seed is the device's own id, so the same phone gets the
                // same cat across reinstalls, and two phones almost never get
                // the same one. `Random.Range` would do, but a seed that can be
                // written down is a seed that can be reproduced in a bug report.
                var seed = Application.systemLanguage.GetHashCode()
                           ^ SystemInfo.deviceUniqueIdentifier.GetHashCode();
                _cached = CatTraits.Roll(seed);
                Debug.Log($"[CatIdentity] rolled a cat: {_cached}");

                // Written straight away, not on the next save: a player who
                // opens the game and closes it should not meet a different
                // animal next time.
                try
                {
                    CatSaveFile.Write(CatSave.Write(new Cat(null, _cached)));
                }
                catch (System.Exception e)
                {
                    // A cat that cannot be written is still a cat for this run.
                    Debug.LogWarning($"[CatIdentity] could not save the rolled cat: {e.Message}");
                }

                return _cached;
            }
        }

        /// <summary>Used by tests and by the photo flow when it replaces her.</summary>
        public static void Forget() => _cached = null;
    }
}
