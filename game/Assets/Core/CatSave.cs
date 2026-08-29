using System;
using System.Collections.Generic;
using System.Linq;

namespace CatShelter.Core
{
    /// <summary>
    /// Task 50-photo/10: the cat's name and coat, persisted the same way the
    /// board is (<see cref="GameSave"/>) — plain ASCII-safe text, zero
    /// dependencies, identical under Unity and under `dotnet test`
    /// (DECISIONS.md D13). A name typed once must still be there after the
    /// app is killed and reopened; that is what "survives a restart" means,
    /// and a string sitting only in a MonoBehaviour field does not do it.
    ///
    /// Kept as its own file rather than folded into GameSave: a cat exists
    /// before any level does — the skip path reaches a named cat before the
    /// first Board is ever built — so tying its persistence to a Board or
    /// BoardSnapshot would make the two lifetimes agree by accident rather
    /// than by design. Where the two save files end up living on disk is a
    /// Shell concern (see Shell/SaveFile.cs for the board's half); this class
    /// only knows how to turn a Cat into a string and back.
    /// </summary>
    public static class CatSave
    {
        public const string Header = "catshelter-cat-v1";

        public static string Write(Cat cat)
        {
            if (cat is null) throw new ArgumentNullException(nameof(cat));
            var t = cat.Traits;
            // A newline inside a typed name would be read back as a second
            // line and break the format; strip rather than reject; a name
            // is player data and must not be the reason a save gets thrown
            // away.
            var safeName = cat.Name.Replace("\r", " ").Replace("\n", " ");
            var lines = new List<string>
            {
                Header,
                $"name {safeName}",
                $"traits {t.BaseColor} {t.Pattern} {t.FurLength} {t.EyeColor} "
                    + string.Join(",", t.WhiteMarkings) + $" {t.Origin}",
            };

            // Her distinctive marks, on a line of their own.
            //
            // A line rather than more fields on `traits`, because the reader
            // below skips lines it does not know: a save written today is read
            // by yesterday's build as a cat without marks, and a save written
            // yesterday is read today the same way. No version bump, nothing
            // thrown away.
            //
            // Written only when there are any. Most cats have none, and an
            // empty line in a format this small is noise.
            //
            // This was missing until 2026-08-29, and it mattered more than the
            // other fields put together: the five class traits describe a kind
            // of cat and would have come back the same anyway, while the marks
            // are the only thing that says WHICH cat. Losing them on a restart
            // meant the player's own cat quietly became a generic one.
            if (t.Spots.Count > 0)
                lines.Add("spots " + string.Join(",",
                    t.Spots.Select(m => $"{m.Place}:{m.Shade}")));

            return string.Join("\n", lines) + "\n";
        }

        /// <summary>
        /// Parse a saved cat. Returns null on anything malformed — the same
        /// promise <see cref="GameSave.Read"/> makes about the board: a
        /// corrupt file falls back to <see cref="Cat.Skipped"/> at the call
        /// site, it never crashes the app on launch.
        /// </summary>
        public static Cat Read(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            try
            {
                var lines = text.Replace("\r\n", "\n")
                                 .Split('\n', StringSplitOptions.RemoveEmptyEntries);
                if (lines.Length == 0 || lines[0].Trim() != Header) return null;

                string name = null;
                string[] traitParts = null;
                var spots = new List<CatSpot>();

                foreach (var raw in lines.Skip(1))
                {
                    if (raw.StartsWith("name ", StringComparison.Ordinal))
                    {
                        name = raw.Substring("name ".Length);
                    }
                    else if (raw.StartsWith("traits ", StringComparison.Ordinal))
                    {
                        // traits <base> <pattern> <furLength> <eyeColor> <markings> <origin>
                        traitParts = raw.Split(' ');
                        if (traitParts.Length != 7) return null;
                    }
                    else if (raw.StartsWith("spots ", StringComparison.Ordinal))
                    {
                        foreach (var piece in raw.Substring("spots ".Length).Split(','))
                        {
                            var pair = piece.Split(':');
                            if (pair.Length != 2) return null;
                            spots.Add(new CatSpot(pair[0], pair[1]));
                        }
                    }
                }

                // Built at the end, not on the `traits` line: the marks arrive
                // on a line of their own and the format does not promise an
                // order.
                if (name == null || traitParts == null) return null;
                if (!Enum.TryParse<TraitsOrigin>(traitParts[6], out var origin))
                    return null;
                var markings = traitParts[5].Length == 0
                    ? Array.Empty<string>()
                    : traitParts[5].Split(',');
                var traits = new CatTraits(traitParts[1], traitParts[2], traitParts[3],
                                           traitParts[4], markings, origin, spots);

                return new Cat(name, traits);
            }
            catch (FormatException)
            {
                return null;
            }
            catch (IndexOutOfRangeException)
            {
                return null;
            }
            catch (ArgumentException)
            {
                // CatTraits rejects a field outside its allowed palette; a
                // save naming one is corrupt, not a game state to resume.
                return null;
            }
        }
    }
}
