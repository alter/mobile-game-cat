using System;
using System.Collections.Generic;
using System.Linq;

namespace CatShelter.Core
{
    /// <summary>
    /// The other half of <see cref="TraitsRequest"/>: what the Worker sends
    /// back, turned into a <see cref="CatTraits"/>.
    ///
    /// This did not exist until 2026-08-29, and its absence was the quiet kind
    /// of gap. `TraitsRequest` builds the body, `CaptureScreen` has an
    /// `AskWorker` hook, the Worker validates its answer against a schema and
    /// has tests — and nothing anywhere turned the reply into traits. The whole
    /// path was a request with no reader.
    ///
    /// Engine-free like the request half, so `dotnet test` can exercise it
    /// without Unity or a device, and hand-parsed for the same reason
    /// `GameSave` is: `Core` takes no dependencies, `System.Text.Json` is
    /// forbidden under IL2CPP, and Newtonsoft inside Core would be one. That
    /// argument is real HERE and was wrongly copied into an editor script
    /// earlier the same day — see `Editor/BakeTraitSet.cs`, which now uses
    /// Newtonsoft because nothing stops it.
    ///
    /// Every failure returns null rather than throwing. A Worker that answers
    /// nonsense must leave the player with the fallback cat, never with an
    /// error screen: she took a perfectly good photograph.
    ///
    /// That principle used to stop at the whole reply: one extra spot, one
    /// repeated place or one place the schema doesn't know threw inside
    /// <see cref="CatTraits"/>'s constructor, and the catch below discarded a
    /// base colour, pattern, fur length and eye colour that were all read
    /// correctly. <see cref="Spots"/> now applies CatTraits.cs:186-191's own
    /// rule — stay silent about one field, not the whole answer — one layer
    /// earlier, by filtering before construction instead of after a throw.
    /// </summary>
    public static class TraitsResponse
    {
        /// <summary>
        /// Parse the Worker's body. Null when anything is missing, unknown or
        /// malformed — the caller falls back to the colour estimate.
        /// </summary>
        public static CatTraits Read(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;

            try
            {
                var baseColor = String(json, "base_color");
                var pattern = String(json, "pattern");
                var furLength = String(json, "fur_length");
                var eyeColor = String(json, "eye_color");
                if (baseColor == null || pattern == null ||
                    furLength == null || eyeColor == null) return null;

                return new CatTraits(baseColor, pattern, furLength, eyeColor,
                                     Strings(json, "white_markings"),
                                     TraitsOrigin.Photo, Spots(json));
            }
            catch (ArgumentException)
            {
                // A value outside CatTraits.Allowed. The Worker validates the
                // same schema, so this means the two drifted — and a drifted
                // pair is exactly when the game must not crash.
                return null;
            }
        }

        /// <summary>`"name": "value"` — the value, or null.</summary>
        private static string String(string json, string name)
        {
            var at = Value(json, name);
            if (at < 0 || IsNull(json, at)) return null;
            var open = json.IndexOf('"', at);
            if (open < 0) return null;
            var close = json.IndexOf('"', open + 1);
            return close < 0 ? null : json.Substring(open + 1, close - open - 1);
        }

        /// <summary>`"name": ["a", "b"]` — empty when absent, which is a
        /// legitimate answer for both list fields.</summary>
        private static string[] Strings(string json, string name)
        {
            var at = Value(json, name);
            if (at < 0 || IsNull(json, at)) return Array.Empty<string>();
            var open = json.IndexOf('[', at);
            var close = open < 0 ? -1 : json.IndexOf(']', open + 1);
            if (open < 0 || close < 0) return Array.Empty<string>();

            var items = new List<string>();
            var inside = json.Substring(open + 1, close - open - 1);
            foreach (var piece in inside.Split(','))
            {
                var value = piece.Trim().Trim('"').Trim();
                if (value.Length > 0) items.Add(value);
            }
            return items.ToArray();
        }

        /// <summary>
        /// `"spots": [{"place": "paw_left", "shade": "light"}]`.
        ///
        /// An empty list is the ordinary answer and the one the prompt asks for
        /// when nothing stands out: most cats have no distinctive mark, and a
        /// mark reported on every cat identifies nobody.
        ///
        /// Every candidate is checked against <see cref="CatTraits.Allowed"/>
        /// here, before <see cref="CatSpot"/> ever sees it, so a spot the model
        /// invented, a place already used or a third mark past
        /// <see cref="CatTraits.MaxSpots"/> is dropped one at a time instead of
        /// throwing out of the constructor and taking the whole reply with it —
        /// the schema and CatTraits itself stay exactly as they were.
        /// </summary>
        private static CatSpot[] Spots(string json)
        {
            var at = Value(json, "spots");
            if (at < 0 || IsNull(json, at)) return Array.Empty<CatSpot>();
            var open = json.IndexOf('[', at);
            var close = open < 0 ? -1 : json.IndexOf(']', open + 1);
            if (open < 0 || close < 0) return Array.Empty<CatSpot>();

            var found = new List<CatSpot>();
            var places = new HashSet<string>();
            var inside = json.Substring(open + 1, close - open - 1);
            foreach (var piece in inside.Split('}'))
            {
                if (found.Count >= CatTraits.MaxSpots) break;

                var place = String(piece, "place");
                var shade = String(piece, "shade");
                if (place == null || shade == null) continue;
                if (!CatTraits.Allowed["spot_place"].Contains(place)) continue;
                if (!CatTraits.Allowed["spot_shade"].Contains(shade)) continue;
                if (!places.Add(place)) continue; // same place reported twice

                found.Add(new CatSpot(place, shade));
            }
            return found.ToArray();
        }

        /// <summary>Whether the value at <paramref name="at"/> is the JSON
        /// literal `null`. `String`/`Strings` used to skip straight to the
        /// next quote or bracket for a value that has neither — on
        /// `"base_color":null,"pattern":"tabby"` that quote belongs to the
        /// *next* key, so the reader silently swallowed a neighbour's name (or,
        /// for `Strings`, a neighbour's whole array) as if it were this field's
        /// own value. `null` means the field is absent, the same as if the
        /// Worker had left it out entirely.</summary>
        private static bool IsNull(string json, int at)
        {
            var i = at;
            while (i < json.Length && char.IsWhiteSpace(json[i])) i++;
            return i + 4 <= json.Length && string.CompareOrdinal(json, i, "null", 0, 4) == 0 &&
                   (i + 4 == json.Length || !char.IsLetterOrDigit(json[i + 4]));
        }

        /// <summary>Where a field's value starts, or -1. Matches the key with
        /// its quotes so `"pattern"` cannot be found inside a value.</summary>
        private static int Value(string json, string name)
        {
            var key = json.IndexOf($"\"{name}\"", StringComparison.Ordinal);
            if (key < 0) return -1;
            var colon = json.IndexOf(':', key);
            return colon < 0 ? -1 : colon + 1;
        }
    }
}
