using System;
using CatShelter.Core;

namespace CatShelter.Shell
{
    /// <summary>
    /// Task 50-photo/06: what the player is told, per outcome.
    ///
    /// Separate from <see cref="PhotoJudge"/> on purpose — the decision is a
    /// rule and is tested; the wording is copy and will be rewritten by
    /// 12-copy-english and moved to a table by 16-localisation-ready.
    ///
    /// Tone follows cat-shelter-mvp.md section 4: a rejected photo is not the
    /// player's fault and never sounds like it. Each message says what happened
    /// and what to do next, in that order, and offers a way forward rather than
    /// a verdict.
    /// </summary>
    public static class PhotoMessages
    {
        public static string For(PhotoOutcome outcome)
        {
            switch (outcome)
            {
                case PhotoOutcome.NoAnimal:
                    return Copy.Of("photo.no_animal");

                case PhotoOutcome.Dog:
                    // Naming the dog is the joke and the explanation at once,
                    // and it tells the player the app is looking, not broken.
                    return Copy.Of("photo.dog");

                case PhotoOutcome.UnclearCat:
                    return Copy.Of("photo.unclear");

                case PhotoOutcome.Cat:
                    return Copy.Of("photo.accepted");

                default:
                    // Unreachable while PhotoOutcome has four values, and here
                    // so that adding a fifth breaks loudly instead of showing
                    // the player nothing.
                    throw new ArgumentOutOfRangeException(nameof(outcome));
            }
        }
    }
}
