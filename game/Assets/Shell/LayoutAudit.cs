using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

namespace CatShelter.Shell
{
    /// <summary>
    /// Asks the running game, in whatever language it is in, whether any text
    /// no longer fits where it was put.
    ///
    /// Seventeen languages across half a dozen screens on two platforms is
    /// upward of a hundred screenshots, and a person looking at a hundred
    /// screenshots misses the twelfth. Worse, the failure that matters most is
    /// the one hardest to see: a German word one pixel too wide is not obviously
    /// wrong in a picture, it is just slightly clipped, and it reads as a font
    /// quirk until someone measures it.
    ///
    /// So the measuring is done by the thing that already knows the answer. UI
    /// Toolkit has both numbers after layout — how much room an element was
    /// given, and how much its text actually needs — and this compares them.
    ///
    /// Three faults are reported:
    ///
    ///   clipped   text needs more width than the element has, and cannot wrap
    ///   tall      text needs more height than the element has (it wrapped to
    ///             more lines than the box was built for)
    ///   offscreen the element itself sticks out past the panel
    ///
    /// Runs on every layout pass, so swapping the map for the board or opening
    /// the cat card is audited without anyone asking. Each finding is reported
    /// once — the same overflow re-reported on every frame would bury the second
    /// one.
    ///
    /// Findings go to the log and to `layout-audit.txt` beside the save, because
    /// a capture run has no console attached, in the same spirit as
    /// `errors.txt` and `boot-state.txt` (see AGENT-BRIEF).
    ///
    /// Only switched on when asked for: drop a `lang.txt` beside the save. It is
    /// a measuring tool for the language sweep, and walking the whole visual
    /// tree on every layout pass is not something a player should pay for.
    /// </summary>
    public static class LayoutAudit
    {
        /// <summary>
        /// How far past its box text may run before it counts. Half a pixel is
        /// rounding; two pixels is a clipped letter.
        /// </summary>
        private const float Slack = 1.5f;

        private static readonly HashSet<string> Reported = new();
        private static bool _attached;
        private static int _lastChecked = -1;

        public static bool Requested =>
            System.IO.File.Exists(System.IO.Path.Combine(
                Application.persistentDataPath, "lang.txt"));

        public static void Attach(VisualElement root)
        {
            if (_attached || root == null || !Requested) return;
            _attached = true;

            // Debounced onto the next frame: during a layout pass the numbers
            // are half-computed, and an audit that runs mid-pass invents faults
            // that are not there.
            root.RegisterCallback<GeometryChangedEvent>(_ =>
                root.schedule.Execute(() => Walk(root)).ExecuteLater(0));

            Debug.Log("[Layout] audit on");
        }

        private static void Walk(VisualElement root)
        {
            var panel = root.worldBound;
            if (panel.width <= 0f || float.IsNaN(panel.width)) return;

            var found = 0;
            var checkedCount = 0;
            var tightest = float.MaxValue;
            var shortest = float.MaxValue;
            foreach (var element in Descendants(root))
            {
                if (element is not TextElement text) continue;
                if (string.IsNullOrEmpty(text.text)) continue;
                if (text.resolvedStyle.display == DisplayStyle.None) continue;
                checkedCount++;

                var box = text.contentRect;
                if (box.width <= 0f || float.IsNaN(box.width)) continue;

                // What the text would take if nothing constrained it, and what
                // it takes at the width it was given. The first catches a line
                // that cannot wrap; the second catches one that wrapped into
                // more lines than the box has room for.
                var natural = text.MeasureTextSize(text.text,
                    0, VisualElement.MeasureMode.Undefined,
                    0, VisualElement.MeasureMode.Undefined);

                var wrapped = text.MeasureTextSize(text.text,
                    box.width, VisualElement.MeasureMode.Exactly,
                    0, VisualElement.MeasureMode.Undefined);

                var wraps = text.resolvedStyle.whiteSpace != WhiteSpace.NoWrap;

                // Against the PARENT, not against itself.
                //
                // The first version compared a label with its own box and found
                // nothing anywhere, which is exactly right and exactly useless:
                // a Label in UI Toolkit sizes itself to its text, so it always
                // fits itself to the pixel. "narrowest 0pt spare" on every screen
                // in every language is what that mistake looks like in a log.
                //
                // What can actually go wrong is a label outgrowing the thing
                // meant to hold it — a word wider than its button, a sentence
                // wider than the card — and that is a comparison between two
                // different rectangles.
                var world = text.worldBound;
                var holder = text.hierarchy.parent;
                if (holder != null)
                {
                    var box2 = holder.worldBound;
                    if (box2.width > 0f && !float.IsNaN(box2.width))
                    {
                        var over = Mathf.Max(box2.xMin - world.xMin, world.xMax - box2.xMax);
                        var down = Mathf.Max(box2.yMin - world.yMin, world.yMax - box2.yMax);
                        var spare = -over;
                        if (tightest > spare) tightest = spare;
                        if (shortest > -down) shortest = -down;

                        if (over > Slack)
                            found += Report(text, "wider than its box",
                                $"by {over:F0}pt — {world.width:F0} in a {box2.width:F0} " +
                                $"{holder.GetType().Name}");
                        if (down > Slack)
                            found += Report(text, "taller than its box",
                                $"by {down:F0}pt — {world.height:F0} in a {box2.height:F0} " +
                                $"{holder.GetType().Name}");
                    }
                }

                // And a line that cannot wrap and is given a fixed width: the
                // one case where a label does not get to size itself.
                if (!wraps && text.resolvedStyle.width > 0f &&
                    natural.x > text.resolvedStyle.width + Slack)
                    found += Report(text, "clipped",
                        $"needs {natural.x:F0}pt, the style allows " +
                        $"{text.resolvedStyle.width:F0}");

                if (world.xMin < panel.xMin - Slack || world.xMax > panel.xMax + Slack ||
                    world.yMin < panel.yMin - Slack || world.yMax > panel.yMax + Slack)
                    found += Report(text, "offscreen",
                        $"at {world.xMin:F0},{world.yMin:F0} " +
                        $"{world.width:F0}x{world.height:F0} in a " +
                        $"{panel.width:F0}x{panel.height:F0} panel");
            }

            // Always, not only on a fault: the count is what makes a silent
            // run believable.
            var report = $"[Layout] checked {checkedCount} labels, {found} new, " +
                         $"narrowest {Spare(tightest)}, shortest {Spare(shortest)}";
            if (checkedCount != _lastChecked || found > 0)
            {
                _lastChecked = checkedCount;
                Debug.Log(report);
                Append(report);
            }
        }

        /// <summary>"never measured" and "no room left" must not print alike.</summary>
        private static string Spare(float value) =>
            value == float.MaxValue ? "n/a" : $"{value:F0}pt spare";

        private static int Report(TextElement text, string fault, string detail)
        {
            // The text itself is the key, not the element: the same Label is
            // rebuilt on every screen swap and would otherwise report its one
            // fault a dozen times.
            var key = $"{fault}|{text.text}";
            if (!Reported.Add(key)) return 0;

            var shortened = text.text.Length > 60
                ? text.text.Substring(0, 57) + "..."
                : text.text;
            shortened = shortened.Replace("\n", " ");

            var line = string.Format(CultureInfo.InvariantCulture,
                "[Layout] {0}: \"{1}\" — {2}", fault, shortened, detail);
            Debug.LogWarning(line);
            Append(line);
            return 1;
        }

        private static void Append(string line)
        {
            try
            {
                var path = System.IO.Path.Combine(
                    Application.persistentDataPath, "layout-audit.txt");
                System.IO.File.AppendAllText(path, line + "\n", Encoding.UTF8);
            }
            catch (System.Exception e)
            {
                // A file that cannot be written is not worth losing the run over
                // — the same line already went to the log.
                Debug.LogWarning($"[Layout] could not write the audit file: {e.Message}");
            }
        }

        private static IEnumerable<VisualElement> Descendants(VisualElement from)
        {
            foreach (var child in from.Children())
            {
                yield return child;
                foreach (var deeper in Descendants(child)) yield return deeper;
            }
        }
    }
}
