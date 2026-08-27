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

            var title = new Label("coat: colour × state");
            title.style.fontSize = 15;
            title.style.color = (Color)new Color32(0x4A, 0x3B, 0x28, 0xFF);
            title.style.marginBottom = 8;
            root.Add(title);

            // One row per pattern, one column per state — the colour is fixed
            // to ginger because it is the pattern that needs looking at, and
            // six colours by six patterns is a wall nobody reads.
            foreach (var pattern in CatTraits.Allowed["pattern"])
            {
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.alignItems = Align.Center;

                var label = new Label(pattern);
                label.style.fontSize = 10;
                label.style.width = 46;
                label.style.color = (Color)new Color32(0x7C, 0x6A, 0x52, 0xFF);
                row.Add(label);

                for (int state = 1; state <= 3; state++)
                {
                    var traits = new CatTraits("ginger", pattern, "short", "green",
                                               new[] { "chest", "paws" });
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
                root.Add(row);
            }
        }
    }
}
