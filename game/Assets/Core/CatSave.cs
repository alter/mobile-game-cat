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
                CatTraits traits = null;

                foreach (var raw in lines.Skip(1))
                {
                    if (raw.StartsWith("name ", StringComparison.Ordinal))
                    {
                        name = raw.Substring("name ".Length);
                    }
                    else if (raw.StartsWith("traits ", StringComparison.Ordinal))
                    {
                        var parts = raw.Split(' ');
                        // traits <base> <pattern> <furLength> <eyeColor> <markings> <origin>
                        if (parts.Length != 7) return null;
                        if (!Enum.TryParse<TraitsOrigin>(parts[6], out var origin))
                            return null;
                        var markings = parts[5].Length == 0
                            ? Array.Empty<string>()
                            : parts[5].Split(',');
                        traits = new CatTraits(parts[1], parts[2], parts[3], parts[4],
                            markings, origin);
                    }
                }

                if (name == null || traits == null) return null;
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
