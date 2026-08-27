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
        // The outcome->key mapping and its totality moved to
        // Core.PhotoMessageKey (50-photo/06 VERIFY item 1), tested by
        // dotnet test; this is the one thing that still needs the engine,
        // the lookup into Copy's string table.
        public static string For(PhotoOutcome outcome) =>
            Copy.Of(PhotoMessageKey.For(outcome));
    }
}
