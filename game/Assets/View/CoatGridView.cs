using System.IO;
using System.Linq;
using CatShelter.Core;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace CatShelter.View
{
    /// <summary>
    /// The harness task 60-shell-build/18 asks for: every coat colour against
    /// every state, on one screen, so the result can be looked at without
    /// playing to room 9.
    ///
    /// Reached the same way as the capture screen — drop a `coat.txt` beside
    /// the save — because it is a checking tool, not a screen in the game.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class CoatGridView : MonoBehaviour
    {
        public static bool Requested =>
            File.Exists(Path.Combine(Application.persistentDataPath, "coat.txt"));

        private void OnEnable()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            root.Clear();
            root.style.backgroundColor = (Color)new Color32(0xF4, 0xEA, 0xD8, 0xFF);
            root.style.flexDirection = FlexDirection.Column;
            root.style.alignItems = Align.Center;
            root.style.paddingTop = 24;

            var title = new Label("coat: pattern × state, then markings");
            title.style.fontSize = 15;
            title.style.color = (Color)new Color32(0x4A, 0x3B, 0x28, 0xFF);
            title.style.marginBottom = 8;
            root.Add(title);

            // One row per pattern, one column per state. Colour is fixed to
            // ginger because it is the pattern that needs looking at, and six
            // colours by six patterns is a wall nobody reads.
            //
            // Markings are EMPTY on the pattern rows. They used to be
            // {chest, paws} on every row, which made the tuxedo row impossible
            // to judge — a tuxedo IS a white chest and white paws, so it was
            // being compared against five other cats wearing it too. Every row
            // looked the same at the front and the pattern underneath went
            // unread. Markings get their own row instead.
            foreach (var pattern in CatTraits.Allowed["pattern"])
                root.Add(Row(pattern, pattern, System.Array.Empty<string>()));

            var gap = new VisualElement();
            gap.style.height = 10;
            root.Add(gap);

            root.Add(Row("marks", "solid", new[] { "chest", "paws", "face" }));

            // A grid of 27 identical untinted silhouettes is a confusing sight
            // with no explanation attached. If any coat failed to build, the
            // reason goes on the screen — this harness is only ever looked at
            // by someone checking, and the console they would otherwise read is
            // not available on a device.
            if (CoatBuilder.LastFailure != null)
            {
                var why = new Label($"coat not built — {CoatBuilder.LastFailure}");
                why.style.marginTop = 10;
                why.style.whiteSpace = WhiteSpace.Normal;
                why.style.color = new Color(0.60f, 0.20f, 0.16f);
                root.Add(why);
            }
        }

        /// <summary>
        /// A small copy of the silhouette, cached per source.
        ///
        /// The harness builds 27 coats in one pass, and `CoatBuilder.Build`
        /// walks every pixel of the source several times. At the shipped
        /// 1024×1024 that is 27 million pixels through six passes on the main
        /// thread: measured on the iOS simulator on 28.08, the app sat at 99.9%
        /// CPU with a blank screen for over three minutes and had not finished.
        /// The screen was only ever opened on Android before, where it is
        /// merely slow rather than useless.
        ///
        /// The cells are 96 points wide. 256 is already more than they can
        /// show, and it is 16× less work.
        /// </summary>
        private static readonly Dictionary<string, Texture2D> _small = new();

        private static Texture2D Small(Texture2D src, int size = 256)
        {
            if (src == null || src.width <= size) return src;
            if (_small.TryGetValue(src.name, out var cached) && cached != null) return cached;

            var w = src.width; var h = src.height;
            var px = src.isReadable ? src.GetPixels32() : null;
            if (px == null) return src;   // let Build take its own path and complain

            int oh = Mathf.Max(1, h * size / w);
            var outPx = new Color32[size * oh];
            int bx = w / size, by = h / oh;
            for (int y = 0; y < oh; y++)
                for (int x = 0; x < size; x++)
                {
                    int r = 0, g = 0, b = 0, a = 0, n = 0;
                    for (int sy = y * by; sy < (y + 1) * by && sy < h; sy++)
                        for (int sx = x * bx; sx < (x + 1) * bx && sx < w; sx++)
                        {
                            var c = px[sy * w + sx];
                            r += c.r; g += c.g; b += c.b; a += c.a; n++;
                        }
                    if (n == 0) n = 1;
                    outPx[y * size + x] = new Color32(
                        (byte)(r / n), (byte)(g / n), (byte)(b / n), (byte)(a / n));
                }

            var tex = new Texture2D(size, oh, TextureFormat.RGBA32, mipChain: false)
            {
                name = src.name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            tex.SetPixels32(outPx);
            tex.Apply(updateMipmaps: false);
            _small[src.name] = tex;
            return tex;
        }

        private static VisualElement Row(string label, string pattern,
                                         string[] markings)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;

            var caption = new Label(label);
            caption.style.fontSize = 10;
            caption.style.width = 46;
            caption.style.color = (Color)new Color32(0x7C, 0x6A, 0x52, 0xFF);
            row.Add(caption);

            for (int state = 1; state <= 3; state++)
            {
                var traits = new CatTraits("ginger", pattern, "short", "green",
                                           markings);
                var cell = new VisualElement();
                cell.style.width = 96;
                cell.style.height = 96;

                var art = CoatBuilder.LoadBase(traits, state);
                if (art != null)
                {
                    // Untinted silhouette when the coat cannot be built, so a
                    // harness whose whole job is showing 27 coats does not come
                    // up blank without saying why — CoatBuilder.LastFailure is
                    // shown once at the foot of the grid.
                    var built = CoatBuilder.TryBuild(Small(art), traits, state);
                    cell.style.backgroundImage = new StyleBackground(built != null ? built : art);
                    cell.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
                }
                else
                {
                    cell.Add(new Label("no art"));
                }
                row.Add(cell);
            }
            return row;
        }
    }
}
