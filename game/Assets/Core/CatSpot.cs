using System;

namespace CatShelter.Core
{
    /// <summary>
    /// One distinctive mark on a cat: where it is, and whether it is lighter or
    /// darker than her coat.
    ///
    /// This is the only thing the game knows about a cat that is not also true
    /// of thousands of other cats.
    ///
    /// Forensic identification draws the line this sits on. **Class
    /// characteristics** — colour, build, hair colour, breed — narrow a pool and
    /// never identify anybody. **Individual characteristics** — a scar, a
    /// birthmark, a tattoo — identify one. Marks on a body are formally soft
    /// biometrics: on their own they give a presumptive identification rather
    /// than a positive one, but they are what a person actually recognises
    /// somebody by, and they are stable where colouring and shape are not.
    ///
    /// Measured against that line, every other field of <see cref="CatTraits"/>
    /// is a class characteristic. Six colours, six patterns, two fur lengths,
    /// three eye colours and eight combinations of white marking come to 288
    /// distinguishable cats — a catalogue section, not somebody's cat. A patch
    /// on ONE front paw is worth more than all of them together, because
    /// asymmetry is what recognition runs on.
    ///
    /// No shape here, on purpose. A vocabulary of shapes — heart, star, ring —
    /// would ask the model to name something it mostly cannot see and would
    /// give the player back a shape she never had. A shapeless patch in the
    /// right place is what a marking looks like; the eye reads the place, not
    /// the outline.
    /// </summary>
    public sealed class CatSpot
    {
        /// <summary>Where on the cat. One of <see cref="CatTraits.Allowed"/>
        /// under `spot_place`.</summary>
        public string Place { get; }

        /// <summary>`light` or `dark` — against her own coat, not in absolute
        /// terms. A white patch on a black cat and a black patch on a white one
        /// are the same fact stated from two sides.</summary>
        public string Shade { get; }

        public CatSpot(string place, string shade)
        {
            Place = CatTraits.CheckValue("spot_place", place);
            Shade = CatTraits.CheckValue("spot_shade", shade);
        }

        public override string ToString() => $"{Shade} spot on the {Place.Replace('_', ' ')}";

        public override bool Equals(object obj) =>
            obj is CatSpot other && other.Place == Place && other.Shade == Shade;

        public override int GetHashCode() => (Place, Shade).GetHashCode();
    }
}
