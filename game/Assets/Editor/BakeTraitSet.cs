using System;
using System.Collections.Generic;
using System.IO;
using CatShelter.Core;
using CatShelter.View;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Builds a coat for every set of traits in a file, so a list of real cats can
/// be put through the real pipeline without a device, a photo picker or a
/// network call.
///
/// What this is for. The game promises the player her own cat, and until
/// 2026-08-29 nobody had ever seen what that produces for a cat other than the
/// three the coat grid ships with. The reference photo set (41 images,
/// `fixtures/reference-photos`) exists for exactly this and had only ever been
/// used to test whether a photo is *accepted*, not what it turns into.
///
/// The traits come from a file rather than from the worker, because the worker
/// needs an API key and a live endpoint. The file's format is the worker's own
/// response schema (`tools/traits/schema.json`), so what is fed in here is
/// exactly what would arrive over the wire — nothing about the coat pipeline is
/// stubbed or simplified.
///
///   Unity -batchmode -quit -projectPath game \
///         -executeMethod BakeTraitSet.Bake -logFile traits.log
///
/// Reads `tools/traits/reference-readings.json`, writes one PNG per entry into
/// `tools/traits/out/`. Outside Assets on purpose: these are a report, not
/// content, and they must not end up in the build.
/// </summary>
public static class BakeTraitSet
{
    private const int Size = 512;

    /// <summary>The standing pose. She is legible in it, and it is the one the
    /// player meets first.</summary>
    private const int State = 2;

    public static void Bake()
    {
        var root = Directory.GetParent(Application.dataPath)?.Parent?.FullName;
        if (root == null) { Debug.LogError("[Traits] cannot find the repo root"); return; }

        var input = Path.Combine(root, "tools/traits/reference-readings.json");
        if (!File.Exists(input))
        {
            Debug.LogError($"[Traits] no readings at {input}");
            return;
        }

        var outDir = Path.Combine(root, "tools/traits/out");
        Directory.CreateDirectory(outDir);

        var built = 0;
        var failed = new List<string>();
        foreach (var reading in Parse(File.ReadAllText(input)))
        {
            CatTraits traits;
            try
            {
                traits = new CatTraits(reading.BaseColor, reading.Pattern,
                                       reading.FurLength, reading.EyeColor,
                                       reading.WhiteMarkings);
            }
            catch (Exception e)
            {
                // A value the worker's schema allows and CatTraits does not is
                // a real defect, not a bad photo — say which.
                Debug.LogError($"[Traits] {reading.File}: rejected — {e.Message}");
                failed.Add(reading.File);
                continue;
            }

            var art = CoatBuilder.LoadBase(traits, State);
            if (art == null)
            {
                Debug.LogError($"[Traits] {reading.File}: no silhouette");
                failed.Add(reading.File);
                continue;
            }

            var coat = CoatBuilder.TryBuild(CoatBuilder.Downscale(art, Size), traits, State);
            if (coat == null)
            {
                Debug.LogError($"[Traits] {reading.File}: coat did not build — " +
                               $"{CoatBuilder.LastFailure}");
                failed.Add(reading.File);
                continue;
            }

            var name = Path.GetFileNameWithoutExtension(reading.File) + ".png";
            File.WriteAllBytes(Path.Combine(outDir, name), coat.EncodeToPNG());
            built++;
            Debug.Log($"[Traits] {name}  {traits}");
        }

        Debug.Log($"[Traits] done, {built} built" +
                  (failed.Count > 0 ? $", {failed.Count} failed: {string.Join(", ", failed)}"
                                    : ", none failed"));
    }

    private readonly struct Reading
    {
        public readonly string File, BaseColor, Pattern, FurLength, EyeColor;
        public readonly string[] WhiteMarkings;

        public Reading(string file, string baseColor, string pattern,
                       string furLength, string eyeColor, string[] markings)
        {
            File = file; BaseColor = baseColor; Pattern = pattern;
            FurLength = furLength; EyeColor = eyeColor; WhiteMarkings = markings;
        }
    }

    /// <summary>
    /// A small reader for this one shape, rather than a JSON dependency. The
    /// same argument as Core/GameSave and Core/TraitsRequest: five string fields
    /// and one string array does not justify pulling Newtonsoft into the editor
    /// assembly, and JsonUtility cannot do arrays of strings inside a list
    /// without a wrapper type anyway.
    /// </summary>
    private static IEnumerable<Reading> Parse(string json)
    {
        foreach (var chunk in json.Split('{'))
        {
            if (!chunk.Contains("\"file\"")) continue;
            yield return new Reading(
                Field(chunk, "file"), Field(chunk, "base_color"),
                Field(chunk, "pattern"), Field(chunk, "fur_length"),
                Field(chunk, "eye_color"), Markings(chunk));
        }
    }

    private static string Field(string chunk, string name)
    {
        var at = chunk.IndexOf($"\"{name}\"", StringComparison.Ordinal);
        if (at < 0) return null;
        var open = chunk.IndexOf('"', chunk.IndexOf(':', at) + 1);
        var close = chunk.IndexOf('"', open + 1);
        return open < 0 || close < 0 ? null : chunk.Substring(open + 1, close - open - 1);
    }

    private static string[] Markings(string chunk)
    {
        var at = chunk.IndexOf("\"white_markings\"", StringComparison.Ordinal);
        if (at < 0) return Array.Empty<string>();
        var open = chunk.IndexOf('[', at);
        var close = chunk.IndexOf(']', open + 1);
        if (open < 0 || close < 0) return Array.Empty<string>();

        var inside = chunk.Substring(open + 1, close - open - 1).Trim();
        if (inside.Length == 0) return Array.Empty<string>();

        var parts = inside.Split(',');
        var list = new List<string>();
        foreach (var part in parts)
        {
            var value = part.Trim().Trim('"');
            if (value.Length > 0) list.Add(value);
        }
        return list.ToArray();
    }
}
