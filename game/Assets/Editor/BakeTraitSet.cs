using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CatShelter.Core;
using CatShelter.View;
using UnityEditor;
using UnityEngine;
using Newtonsoft.Json;

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
        var readings = JsonConvert.DeserializeObject<List<Reading>>(File.ReadAllText(input));
        if (readings == null)
        {
            Debug.LogError($"[Traits] {input} did not parse as a list of readings");
            return;
        }

        foreach (var reading in readings)
        {
            CatTraits traits;
            try
            {
                traits = new CatTraits(reading.BaseColor, reading.Pattern,
                                       reading.FurLength, reading.EyeColor,
                                       reading.WhiteMarkings,
                                       TraitsOrigin.Photo,
                                       reading.Spots?.Select(s => new CatSpot(s.Place, s.Shade))
                                                     .ToArray());
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
            var marks = traits.Spots.Count == 0
                ? "no distinctive marks"
                : string.Join("; ", traits.Spots.Select(s => s.ToString()));
            Debug.Log($"[Traits] {name}  {traits}  —  {marks}");
        }

        Debug.Log($"[Traits] done, {built} built" +
                  (failed.Count > 0 ? $", {failed.Count} failed: {string.Join(", ", failed)}"
                                    : ", none failed"));
    }

    /// <summary>
    /// One entry of `tools/traits/reference-readings.json`, which is the
    /// worker's own response shape.
    ///
    /// Read with Newtonsoft, which this project already depends on
    /// (`com.unity.nuget.newtonsoft-json`). The first version of this file
    /// split the text on braces by hand — the argument copied from Core, where
    /// a JSON dependency really is unwelcome. It does not apply here: this is an
    /// editor script, it runs on a desktop, and nothing it does ships. The
    /// hand-rolled reader worked exactly until the data grew a nested object
    /// (`spots`), at which point it cut every entry short and silently produced
    /// unmarked cats with nothing in the log.
    /// </summary>
    private sealed class Reading
    {
        [JsonProperty("file")] public string File { get; set; }
        [JsonProperty("base_color")] public string BaseColor { get; set; }
        [JsonProperty("pattern")] public string Pattern { get; set; }
        [JsonProperty("fur_length")] public string FurLength { get; set; }
        [JsonProperty("eye_color")] public string EyeColor { get; set; }
        [JsonProperty("white_markings")] public string[] WhiteMarkings { get; set; }
        [JsonProperty("spots")] public SpotJson[] Spots { get; set; }
    }

    private sealed class SpotJson
    {
        [JsonProperty("place")] public string Place { get; set; }
        [JsonProperty("shade")] public string Shade { get; set; }
    }
}
