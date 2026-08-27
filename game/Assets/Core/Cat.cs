using System;

namespace CatShelter.Core
{
    /// <summary>
    /// The player's cat as a persisted identity: her coat and the name the
    /// player gave her (cat-shelter-mvp.md section 8, "Cat name, traits{},
    /// state(1..3), owned_items[]"). Only the first two fields live here —
    /// state is already computed from rooms cleared by
    /// <see cref="PlayerProgress.CatState"/>, and owned_items belongs to
    /// whichever task first needs it; adding them here ahead of that task
    /// would be guessing at a shape nothing yet uses.
    ///
    /// A name is deliberately not a field on <see cref="CatTraits"/>: that
    /// class is exactly the five fields the coat shader composites from
    /// (its own doc comment), and a name is not drawn — it is read.
    /// </summary>
    public sealed class Cat
    {
        /// <summary>
        /// What a cat is called before the player ever types anything, and
        /// what the skip path (50-photo/10) gives her outright: two players
        /// who never rename their cat, or who skip the photo, must still be
        /// able to talk about "the same" named cat, the way
        /// <see cref="CatTraits.Default"/> already guarantees the same coat.
        /// </summary>
        public const string DefaultName = "Kitty";

        public string Name { get; }
        public CatTraits Traits { get; }

        /// <summary>
        /// A blank or whitespace-only name becomes <see cref="DefaultName"/>
        /// rather than being stored as-is: "named" must hold even for a
        /// player who reaches the name field and leaves it empty, not only
        /// for one who skips the photo outright.
        /// </summary>
        public Cat(string name, CatTraits traits)
        {
            if (traits is null) throw new ArgumentNullException(nameof(traits));
            var trimmed = name?.Trim();
            Name = string.IsNullOrEmpty(trimmed) ? DefaultName : trimmed;
            Traits = traits;
        }

        /// <summary>
        /// The cat a player gets when she skips the photo: named and
        /// complete with no player input at all (task 50-photo/10). Fixed,
        /// like <see cref="CatTraits.Default"/> — every call returns the
        /// same name over the same coat.
        /// </summary>
        public static Cat Skipped => new Cat(DefaultName, CatTraits.Default);
    }
}
