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
        /// The player's cat. Reads the save; rolls one in memory if there is
        /// none. Never returns null — a launch with no saved cat still has to
        /// show somebody.
        ///
        /// 60-shell-build/20: this used to write the rolled cat to
        /// <see cref="CatSaveFile"/> as well, "so a player who opens the game
        /// and closes it should not meet a different animal next time" — true
        /// when this was written (28.08), before the saved cat became the
        /// first-run gate itself (<c>GameBoot.HasACat</c>, 50-photo/10). A
        /// write here fired the moment anything read <see cref="Traits"/> —
        /// which board.txt's debug harness does on a device that has never met
        /// a cat, through <c>DebugGameView.CatStateTraits</c> — and planted a
        /// nameless `cat.save` that made every later launch think the first
        /// run was already answered.
        ///
        /// The reason still stands but needs no write to hold: the seed below
        /// is a pure function of the device and its language, so the same
        /// phone rolls the same cat every time it asks, on disk or not — see
        /// <see cref="CatTraits.Roll"/>'s own doc. Persisting it is not this
        /// property's job any more; the one write that should close the gate
        /// happens where the player actually answers it, in
        /// <c>GameBoot.ShowMeetYourCat</c>'s <c>OnNamed</c>.
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

                return _cached;
            }
        }

        /// <summary>Used by tests and by the photo flow when it replaces her.</summary>
        public static void Forget() => _cached = null;
    }
}
