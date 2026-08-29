using System;
using System.Collections.Generic;

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
            if (at < 0) return null;
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
            if (at < 0) return Array.Empty<string>();
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
        /// </summary>
        private static CatSpot[] Spots(string json)
        {
            var at = Value(json, "spots");
            if (at < 0) return Array.Empty<CatSpot>();
            var open = json.IndexOf('[', at);
            var close = open < 0 ? -1 : json.IndexOf(']', open + 1);
            if (open < 0 || close < 0) return Array.Empty<CatSpot>();

            var found = new List<CatSpot>();
            var inside = json.Substring(open + 1, close - open - 1);
            foreach (var piece in inside.Split('}'))
            {
                var place = String(piece, "place");
                var shade = String(piece, "shade");
                if (place != null && shade != null) found.Add(new CatSpot(place, shade));
            }
            return found.ToArray();
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
