using System.IO;
using System.Linq;
using CatShelter.Core;
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
                    var built = CoatBuilder.Build(art, traits, state);
                    cell.style.backgroundImage = new StyleBackground(built);
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
