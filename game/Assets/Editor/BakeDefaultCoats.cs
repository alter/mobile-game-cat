using System.IO;
using CatShelter.Core;
using CatShelter.View;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Bakes the default cat's three states into Resources, so a player who has
/// not given the game a photograph pays nothing to see her.
///
/// The owner put the shape of it plainly: a player who has not uploaded a photo
/// is shown a default, and that needs no generation; a player who has uploaded
/// one is processed once and cached, and never again. This file is the first
/// half. `CoatBuilder.TryBuildFor` is the second — it looks for what this bakes
/// before it builds anything.
///
/// Why it matters, measured on the iOS simulator on 28.08: building one coat
/// from the shipped 1024×1024 silhouette took **21.8 seconds**, and it was the
/// entire delay in opening a room. Level loading was 91ms.
///
/// Run it:
///   Unity -batchmode -quit -projectPath game \
///         -executeMethod BakeDefaultCoats.Bake -logFile bake.log
///
/// Re-run it whenever CoatBuilder's passes or the cat silhouettes change — the
/// baked files are the output of code, and stale output is worse than none.
/// </summary>
public static class BakeDefaultCoats
{
    /// <summary>The size the board draws her at, with room to spare.</summary>
    private const int Size = 256;

    /// <summary>
    /// And the size the cat card draws her at.
    ///
    /// One texture used to serve both, and 256 was chosen for the board's
    /// portrait, which is 52 points across. The card gives her nearly the whole
    /// width of the screen — about a thousand pixels from a 256 source — and
    /// every stair-step in the outline was four pixels tall. Doubling it costs
    /// three files and about 100 KB in a 51 MB build.
    ///
    /// Not larger: 1024 is what the silhouettes ship at, and building one at
    /// that size took 21.8 seconds on the iOS simulator.
    /// </summary>
    private const int CardSize = 512;

    private const string Dir = "Assets/Resources/Art";

    /// <summary>The two sizes, and the name each is baked under.</summary>
    private static readonly (int size, string prefix)[] Wanted =
    {
        (Size, "coat_default"),
        (CardSize, "coat_card"),
    };

    public static void Bake()
    {
        var traits = CatTraits.Default;
        var written = 0;

        for (int state = 1; state <= 3; state++)
        {
            var art = CoatBuilder.LoadBase(traits, state);
            if (art == null)
            {
                Debug.LogError($"[BakeDefaultCoats] no silhouette for state {state}");
                continue;
            }

            foreach (var (size, prefix) in Wanted)
            {
                var built = CoatBuilder.TryBuild(CoatBuilder.Downscale(art, size), traits, state);
                if (built == null)
                {
                    Debug.LogError($"[BakeDefaultCoats] state {state} at {size} did not build");
                    continue;
                }

                var path = $"{Dir}/{prefix}_{state}.png";
                File.WriteAllBytes(path, built.EncodeToPNG());
                written++;
                Debug.Log($"[BakeDefaultCoats] {path} ({built.width}x{built.height})");
            }
        }

        AssetDatabase.Refresh();

        // Imported like every other sprite in this project, and readable —
        // CoatBuilder never reads these back, but a texture that cannot be read
        // is the trap that blanked the iOS simulator once already.
        for (int state = 1; state <= 3; state++)
            foreach (var (_, prefix) in Wanted)
            {
                var path = $"{Dir}/{prefix}_{state}.png";
                if (AssetImporter.GetAtPath(path) is TextureImporter importer)
                {
                    importer.textureType = TextureImporterType.Default;
                    importer.isReadable = true;
                    importer.npotScale = TextureImporterNPOTScale.None;
                    importer.alphaIsTransparency = true;
                    importer.SaveAndReimport();
                }
            }

        Debug.Log($"[BakeDefaultCoats] done, {written} of {3 * Wanted.Length} written");
    }
}
