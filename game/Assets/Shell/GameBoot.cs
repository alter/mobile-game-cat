using CatShelter.View;
using UnityEngine;
using UnityEngine.UIElements;

namespace CatShelter.Shell
{
    /// <summary>
    /// Builds the entire scene from code at startup — per cat-shelter-tech.md:
    /// "scenes assembled by code — so an agent never has to touch scene YAML".
    /// The only thing the scene asset needs is this one component.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class GameBoot : MonoBehaviour
    {
        private void Awake()
        {
            // Analytics sink: GameAnalytics when analytics-keys.txt exists
            // beside the save (70-analytics/01 NOTES.md has the one-minute
            // setup instruction), the same no-op Configure(null, null) as
            // before otherwise — nothing thrown, nothing logged repeatedly,
            // identical to today's behaviour until a key exists.
            var (designSink, progressionSink) =
                GameAnalyticsSink.TryConfigure(gameObject);
            Core.Analytics.Configure(designSink, progressionSink);

            // app:open, the denominator every other number is read against.
            Core.Analytics.AppOpen();

            // Does nothing unless a `visiontest` folder was pushed into the
            // app container; see VisionSelfTest.
            VisionSelfTest.RunIfRequested();

            // Opening the game today means no reminder about today: the next
            // one moves to tomorrow evening.
            EveningReminder.Reschedule();
            Feedback.Attach(gameObject);
            StartCoroutine(EveningReminder.DebugRequestNow(this));
        }

        private static bool CaptureRequested()
        {
            try
            {
                return System.IO.File.Exists(System.IO.Path.Combine(
                    Application.persistentDataPath, "capture.txt"));
            }
            catch (System.Exception)
            {
                return false;
            }
        }

        private static CatShelter.Shell.VisionAnswer? CaptureStub()
        {
            try
            {
                var flag = System.IO.Path.Combine(Application.persistentDataPath, "capture.txt");
                var lines = System.IO.File.ReadAllLines(flag);
                if (lines.Length < 2) return null;
                var parts = lines[1].Split(' ');
                if (parts.Length < 3 || parts[0] != "fake") return null;
                var confidence = float.Parse(parts[2],
                    System.Globalization.CultureInfo.InvariantCulture);
                if (parts[1] == "none")
                    return new VisionAnswer { ok = true, detections = new AnimalBox[0] };
                return new VisionAnswer
                {
                    ok = true,
                    imageWidth = 512,
                    imageHeight = 512,
                    detections = new[]
                    {
                        new AnimalBox
                        {
                            identifier = parts[1], confidence = confidence,
                            x = 0, y = 0, width = 400, height = 400,
                        },
                    },
                };
            }
            catch (System.Exception)
            {
                return null;
            }
        }

        private static string CaptureSubject()
        {
            try
            {
                var flag = System.IO.Path.Combine(Application.persistentDataPath, "capture.txt");
                // First line only: the second, when present, stubs the Vision
                // answer.
                var lines = System.IO.File.ReadAllLines(flag);
                var name = lines.Length > 0 ? lines[0].Trim() : "";
                if (name.Length == 0) return null;
                var path = System.IO.Path.Combine(Application.persistentDataPath, name);
                return System.IO.File.Exists(path) ? path : null;
            }
            catch (System.Exception)
            {
                return null;
            }
        }

        private System.Collections.IEnumerator RunAndReport(
            CatShelter.View.CaptureScreen screen, string path)
        {
            yield return screen.Handle(System.IO.File.ReadAllBytes(path));
            Report($"{System.IO.Path.GetFileName(path)} -> \"{screen.CurrentMessage}\"");
        }

        private static void Report(string line)
        {
            Debug.Log($"[CaptureScreen] {line}");
            try
            {
                System.IO.File.AppendAllText(
                    System.IO.Path.Combine(Application.persistentDataPath, "capture-state.txt"),
                    line + "\n");
            }
            catch (System.Exception)
            {
            }
        }

        private void OnEnable()
        {
            var uid = GetComponent<UIDocument>();
            // Both screens live in this panel, so the inset is applied once
            // here rather than by each screen.
            if (GetComponent<SafeArea>() == null) gameObject.AddComponent<SafeArea>();
            if (uid.rootVisualElement == null)
            {
                // No UXML assigned: build a bare root so DebugGameView can
                // populate it. Everything is code-generated.
                uid.visualTreeAsset = null;
                var root = uid.rootVisualElement;
                root?.Clear();
            }
            // The capture screen replaces the board when asked for. It is not
            // yet the game's first screen — that arrives with 09-meet-your-cat
            // and 10-skip-default-cat, which decide what happens after a photo
            // is accepted. Until then it is reachable for checking, by dropping
            // a `capture.txt` next to the save.
            if (CaptureRequested())
            {
                var screen = gameObject.AddComponent<CatShelter.View.CaptureScreen>();
                screen.Build(uid.rootVisualElement);
                screen.OnAccepted = photo => Report($"accepted a {photo.Length}-byte photo");
                screen.OnCatReady = cat => Report($"cat ready ({cat.Origin}): {cat}");

                // capture.txt may name a photo in the same folder: the pipeline
                // then runs on it without anyone tapping, which is the only way
                // to exercise the four messages from outside the app.
                // A second line in capture.txt, "fake Dog 0.73", stands in for
                // the Vision answer. The simulator cannot run Vision at all
                // (05-vision-plugin), so without this only one of the four
                // messages is ever reachable outside a device.
                var stub = CaptureStub();
                if (stub != null)
                {
                    screen.Recognise = _ => stub.Value;
                }

                var named = CaptureSubject();
                if (named != null)
                {
                    StartCoroutine(RunAndReport(screen, named));
                }
                return;
            }

            // The coat harness (60-shell-build/18) replaces the board when
            // asked for, same as the capture screen: drop a `coat.txt` beside
            // the save. A checking tool, not a screen in the game.
            if (CatShelter.View.CoatGridView.Requested)
            {
                if (GetComponent<CatShelter.View.CoatGridView>() == null)
                    gameObject.AddComponent<CatShelter.View.CoatGridView>();
                return;
            }

            if (GetComponent<DebugGameView>() == null)
                gameObject.AddComponent<DebugGameView>();
        }
    }
}
