using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CatShelter.Core;
using UnityEngine;

namespace CatShelter.Shell
{
    /// <summary>
    /// Task 50-photo/05, VERIFY 1-3: run the plugin over the whole reference
    /// set inside a real iOS build, rather than trusting that a macOS probe of
    /// the same framework says the same thing.
    ///
    /// Dormant unless a folder named `visiontest` exists next to the save file.
    /// The images are pushed there from outside — no fixture ships inside the
    /// app, because the set is third-party photos under their own licences.
    /// Results land in `vision-results.json` beside it, one row per image.
    ///
    ///     xcrun simctl get_app_container booted &lt;bundle id&gt; data
    ///     cp fixtures/reference-photos/*.jpg "$C/Documents/visiontest/"
    /// </summary>
    public static class VisionSelfTest
    {
        private const string FolderName = "visiontest";
        private const string ResultName = "vision-results.json";

        public static void RunIfRequested()
        {
            var folder = Path.Combine(Application.persistentDataPath, FolderName);
            if (!Directory.Exists(folder))
                return;

            var files = Directory.GetFiles(folder, "*.jpg").OrderBy(f => f).ToList();
            if (files.Count == 0)
            {
                Debug.Log("[VisionSelfTest] folder is present but empty");
                return;
            }

            var rows = new List<string>();
            var started = DateTime.UtcNow;
            foreach (var path in files)
            {
                var name = Path.GetFileName(path);
                VisionAnswer answer;
                var before = DateTime.UtcNow;
                try
                {
                    answer = CatVision.Recognise(File.ReadAllBytes(path));
                }
                catch (Exception e)
                {
                    answer = new VisionAnswer { ok = false, error = e.GetType().Name };
                }
                var ms = (DateTime.UtcNow - before).TotalMilliseconds;

                var best = answer.FoundAnimal ? answer.Best : default;
                rows.Add("{" +
                    $"\"file\":\"{name}\"," +
                    $"\"ok\":{answer.ok.ToString().ToLowerInvariant()}," +
                    $"\"error\":\"{answer.error}\"," +
                    $"\"animals\":{(answer.detections?.Length ?? 0)}," +
                    $"\"top\":\"{(answer.FoundAnimal ? best.identifier : "none")}\"," +
                    $"\"confidence\":{(answer.FoundAnimal ? best.confidence : 0f).ToString("0.000", System.Globalization.CultureInfo.InvariantCulture)}," +
                    $"\"box\":[{best.x},{best.y},{best.width},{best.height}]," +
                    $"\"image\":[{answer.imageWidth},{answer.imageHeight}]," +
                    $"\"ms\":{ms.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)}" +
                    "}");

                Debug.Log($"[VisionSelfTest] {name}: {(answer.FoundAnimal ? best.identifier : "none")} " +
                          $"{(answer.FoundAnimal ? best.confidence : 0f):0.00} in {ms:0}ms");
            }

            var output = new StringBuilder("[\n  ")
                .Append(string.Join(",\n  ", rows))
                .Append("\n]\n");
            File.WriteAllText(Path.Combine(Application.persistentDataPath, ResultName),
                              output.ToString());
            Debug.Log($"[VisionSelfTest] {files.Count} images in " +
                      $"{(DateTime.UtcNow - started).TotalMilliseconds:0}ms -> {ResultName}");
        }
    }
}
