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
            // First line of Awake on purpose: everything below it can fail, and
            // an error nobody can read is how a black screen costs a day. See
            // DeviceLog for what that day looked like.
            DeviceLog.Attach();
            ForceLanguageIfRequested();

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

        /// <summary>
        /// Play in a chosen language without touching the device's own.
        ///
        /// Drop a `lang.txt` beside the save holding one `SystemLanguage` name —
        /// `Japanese`, `Arabic`, `ChineseSimplified`. Anything unreadable or
        /// unknown is ignored and the device decides, as always.
        ///
        /// This exists because of what checking seventeen languages costs
        /// otherwise. The only other way in is the device's own language, and
        /// changing that on an Android emulator means `setprop` plus a shell
        /// restart plus waiting for it to come back — about a minute and a half
        /// each, before a single screenshot. Seventeen languages across four
        /// screens on two platforms is not a thing anyone does at that price,
        /// which means it does not get done, which is how a game ships with its
        /// German button text running off the edge.
        ///
        /// A checking tool, on the same convention as `capture.txt`, `coat.txt`
        /// and `glyphs.txt`: no player has a way to create this file, and if one
        /// somehow did, the worst case is the game speaking a language they
        /// asked for.
        /// </summary>
        private static void ForceLanguageIfRequested()
        {
            try
            {
                var path = System.IO.Path.Combine(Application.persistentDataPath, "lang.txt");
                if (!System.IO.File.Exists(path)) return;

                var wanted = System.IO.File.ReadAllText(path).Trim();
                if (wanted.Length == 0) return;

                if (!System.Enum.TryParse<SystemLanguage>(wanted, ignoreCase: true,
                        out var language))
                {
                    Debug.LogWarning($"[GameBoot] lang.txt says '{wanted}', which is " +
                                     "not a SystemLanguage — using the device's own");
                    return;
                }

                // Through `For`, not by naming a table: a language with no table
                // gets English here exactly as it would on a real device, so the
                // fallback is exercised rather than bypassed.
                Copy.Current = Copy.For(language);
                Debug.Log($"[GameBoot] lang.txt: {language}");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[GameBoot] could not read lang.txt: {e.Message}");
            }
        }

        /// <summary>
        /// Arabic reads right to left, and by default this game draws it left to
        /// left — every letter present, the sentence backwards.
        ///
        /// Measured, not assumed. The glyph harness draws a probe of four alifs
        /// and a meem, "اااام", chosen so nobody needs to read Arabic to judge
        /// it: an alif is a bare stroke, a meem is a small loop, and laid out
        /// correctly the loop sits on the LEFT. On an Android device on
        /// 2026-08-29 the standard text generator put the loop on the RIGHT and
        /// the advanced one put it on the left. That is the whole reason this
        /// method exists.
        ///
        /// Applied at the panel root so it cascades to every screen, and applied
        /// **only for Arabic**. The advanced generator is a different text engine
        /// with its own metrics; switching the whole game to it to fix one
        /// language would silently re-lay out sixteen others that were tested on
        /// the standard one. Thai and Devanagari were checked on the same screen
        /// and came out identical under both, so they stay where they are.
        ///
        /// Costs nothing for everyone else: a stylesheet that is never loaded
        /// and a class that is never added.
        /// </summary>
        private static void ApplyTextDirection(UIDocument uid)
        {
            var root = uid != null ? uid.rootVisualElement : null;
            if (root == null) return;

            // The language the copy actually resolved to, not the device's, so a
            // table removed from Copy takes its text direction with it.
            //
            // Both halves matter. `For` answers English for any language it has
            // no table for, so comparing against it alone would turn every
            // English player's screen right-to-left the day the Arabic table is
            // deleted — the check has to establish that Arabic exists as well as
            // that we are in it.
            var arabic = Copy.For(SystemLanguage.Arabic);
            if (ReferenceEquals(arabic, Copy.English)) return;
            if (!ReferenceEquals(Copy.Current, arabic)) return;

            var sheet = Resources.Load<StyleSheet>("UI/AdvancedText");
            if (sheet == null)
            {
                // Loud: the alternative is shipping Arabic backwards and finding
                // out from a review in a language nobody here reads.
                Debug.LogError("[GameBoot] Arabic needs Resources/UI/AdvancedText.uss " +
                               "and it is missing — text will read backwards");
                return;
            }

            root.styleSheets.Add(sheet);
            root.AddToClassList("advanced-text");
            root.AddToClassList("rtl");
            Debug.Log("[GameBoot] arabic: advanced text generator, direction rtl");
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

        private static Core.VisionAnswer? CaptureStub()
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
                    return new Core.VisionAnswer { ok = true, detections = new Core.AnimalBox[0] };
                return new Core.VisionAnswer
                {
                    ok = true,
                    imageWidth = 512,
                    imageHeight = 512,
                    detections = new[]
                    {
                        new Core.AnimalBox
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

        /// <summary>
        /// Positive evidence about what the screen ended up being, written
        /// beside the save as `boot-state.txt`.
        ///
        /// An error log only ever says what went wrong. Twice now the screen
        /// was blank with nothing wrong: no exception, no error, a save file
        /// written normally, and no way to tell whether the tree was empty,
        /// off-screen, or drawn in a colour nobody can see. This records the
        /// facts that separate those — which branch ran, whether the UXML
        /// skeleton the board needs was found, how many children the root has,
        /// and the panel's own size — so the next blank screen is a lookup
        /// rather than another round of guessing. Every branch writes one, not
        /// just the board: a flag-file screen that comes up empty is exactly
        /// the case worth having evidence for.
        /// </summary>
        private static void BootState(string branch, UIDocument uid)
        {
            try
            {
                var root = uid != null ? uid.rootVisualElement : null;
                if (root == null)
                {
                    WriteBootState($"branch={branch} root=null");
                    return;
                }

                // Written from the geometry callback, not from here. Layout has
                // not run when OnEnable returns, so every size read at this
                // point is NaN — which is exactly the misleading answer the
                // first version of this recorded, and it looked like the bug.
                // The sizes that matter are the ones after layout.
                root.RegisterCallback<GeometryChangedEvent>(_ =>
                {
                    var gameRoot = root.Q("game-root");
                    var pile = root.Q("pile");
                    var tile = pile != null && pile.childCount > 0 ? pile[0] : null;
                    // The tile is the question the sizes above cannot answer: a
                    // laid-out tree that draws nothing is either transparent or
                    // zero-sized, and only the leaf says which.
                    var leaf =
                        tile == null
                            ? "tile=none"
                            : $"tile={tile.layout.width}x{tile.layout.height} " +
                              $"tile-bg={tile.resolvedStyle.backgroundColor} " +
                              $"tile-img={(tile.resolvedStyle.backgroundImage.texture != null)} " +
                              $"tile-vis={tile.resolvedStyle.visibility} " +
                              $"tile-op={tile.resolvedStyle.opacity} " +
                              $"tile-disp={tile.resolvedStyle.display}";
                    var rootBg = $"root-bg={root.resolvedStyle.backgroundColor} " +
                                 $"gr-bg={(gameRoot == null ? "n/a" : gameRoot.resolvedStyle.backgroundColor.ToString())} " +
                                 $"sheets={root.styleSheets.count}";
                    WriteBootState(
                        $"branch={branch} children={root.childCount} {leaf} {rootBg} " +
                        $"panel={root.layout.width}x{root.layout.height} " +
                        $"game-root={(gameRoot == null ? "missing" : gameRoot.layout.width + "x" + gameRoot.layout.height)} " +
                        $"pile={(pile == null ? "missing" : pile.layout.width + "x" + pile.layout.height)} " +
                        $"pile-children={(pile == null ? -1 : pile.childCount)} " +
                        $"screen={Screen.width}x{Screen.height} safe={Screen.safeArea} " +
                        $"dpi={Screen.dpi} scale={(uid.panelSettings == null ? -1f : uid.panelSettings.scale)} " +
                        $"coat-blit={CatShelter.View.CoatBuilder.LastReadWasBlit} " +
                        $"coat-note={CatShelter.View.CoatBuilder.LastReadNote ?? "-"}");
                });
            }
            catch (System.Exception)
            {
            }
        }

        private static void WriteBootState(string line)
        {
            Debug.Log($"[GameBoot] {line}");
            try
            {
                System.IO.File.WriteAllText(
                    System.IO.Path.Combine(Application.persistentDataPath, "boot-state.txt"),
                    line + "\n");
            }
            catch (System.Exception)
            {
            }
        }

        /// <summary>
        /// Build a screen, and if it throws, say so on the screen instead of
        /// leaving a black one.
        ///
        /// Written on 28.08 after the board and meet-your-cat both came up
        /// black on the iOS simulator with nothing anywhere to say why. A
        /// screen builds its whole tree before attaching it to the panel, so an
        /// exception halfway through attaches nothing at all, and an empty
        /// panel is black.
        ///
        /// The reason goes three places on purpose: the panel, so whoever is
        /// holding the phone can read it; `screen-failure.txt` beside the save,
        /// so a capture run can pull it off a device with no console attached;
        /// and the log, which **does** reach the simulator —
        /// `xcrun simctl launch --console booted <bundle-id>` prints every
        /// Debug.Log line. An earlier version of this comment claimed it did
        /// not, and that claim is why a day was spent inferring from
        /// screenshots what one line of log would have said.
        /// </summary>
        private static void SafeBuild(string what, VisualElement root, System.Action build)
        {
            try
            {
                build();
            }
            catch (System.Exception e)
            {
                var line = $"{what} did not build — {e.GetType().Name}: {e.Message}";
                Debug.LogError($"[GameBoot] {line}\n{e.StackTrace}");
                try
                {
                    System.IO.File.AppendAllText(
                        System.IO.Path.Combine(Application.persistentDataPath,
                                               "screen-failure.txt"),
                        $"{line}\n{e.StackTrace}\n");
                }
                catch (System.Exception)
                {
                }

                if (root == null) return;
                var note = new Label(line);
                note.style.position = Position.Absolute;
                note.style.left = 12;
                note.style.right = 12;
                note.style.top = 24;
                note.style.whiteSpace = WhiteSpace.Normal;
                note.style.color = new Color(0.95f, 0.85f, 0.80f);
                note.style.backgroundColor = new Color(0.35f, 0.12f, 0.10f);
                note.style.paddingLeft = note.style.paddingRight = 10;
                note.style.paddingTop = note.style.paddingBottom = 10;
                root.Add(note);
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
            FontFallbacks.Attach(uid.rootVisualElement);
            ApplyTextDirection(uid);
            LayoutAudit.Attach(uid.rootVisualElement);
            // The capture screen replaces the board when asked for. It is not
            // yet the game's first screen — that arrives with 10-skip-default-cat,
            // which decides what happens with no photo at all. Until then it is
            // reachable for checking, by dropping a `capture.txt` next to the save.
            // A photo accepted here now leads straight into meet-your-cat
            // (50-photo/09): traits in, her named cat out.
            if (CaptureRequested())
            {
                Debug.Log("[GameBoot] branch=capture");
                var screen = gameObject.AddComponent<CatShelter.View.CaptureScreen>();
                SafeBuild("the capture screen", uid.rootVisualElement,
                          () => screen.Build(uid.rootVisualElement));
                screen.OnAccepted = photo => Report($"accepted a {photo.Length}-byte photo");
                screen.OnCatReady = traits =>
                {
                    Report($"cat ready ({traits.Origin}): {traits}");
                    screen.Hide();
                    ShowMeetYourCat(uid.rootVisualElement, traits);
                };

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

            // Which alphabets this build can actually draw, asked for the same
            // way: drop a `glyphs.txt` beside the save. First of the harnesses,
            // because a font that cannot draw a language makes every other
            // screen in that language a lie.
            if (CatShelter.View.GlyphCheckView.Requested)
            {
                Debug.Log("[GameBoot] branch=glyphs");
                SafeBuild("the glyph check", uid.rootVisualElement, () =>
                {
                    if (GetComponent<CatShelter.View.GlyphCheckView>() == null)
                        gameObject.AddComponent<CatShelter.View.GlyphCheckView>();
                });
                BootState("glyphs", uid);
                return;
            }

            // The coat harness (60-shell-build/18) replaces the board when
            // asked for, same as the capture screen: drop a `coat.txt` beside
            // the save. A checking tool, not a screen in the game.
            if (CatShelter.View.CoatGridView.Requested)
            {
                Debug.Log("[GameBoot] branch=coat");
                SafeBuild("the coat harness", uid.rootVisualElement, () =>
                {
                    if (GetComponent<CatShelter.View.CoatGridView>() == null)
                        gameObject.AddComponent<CatShelter.View.CoatGridView>();
                });
                BootState("coat", uid);
                return;
            }

            // Meet-your-cat (50-photo/09) reachable on its own, same
            // convention: drop a `meet.txt` beside the save. Whatever cat is
            // already saved is what she meets — an existing name comes back
            // pre-filled, which is the one thing a fresh capture run cannot
            // exercise (there is never a prior save on that path).
            if (CatShelter.View.MeetYourCatScreen.Requested)
            {
                SafeBuild("meet-your-cat", uid.rootVisualElement, () =>
                {
                    if (GetComponent<CatShelter.View.MeetYourCatScreen>() == null)
                    {
                        var saved = CatShelter.Core.CatSave.Read(CatShelter.Shell.CatSaveFile.Read());
                        var traits = saved?.Traits ?? CatShelter.Core.CatTraits.Default;
                        ShowMeetYourCat(uid.rootVisualElement, traits, saved?.Name);
                    }
                });
                BootState("meet", uid);
                return;
            }

            // The board with no map in front of it: drop a `board.txt` beside
            // the save. This is the last of the checking flags and the newest,
            // and it exists because the branch below stopped being the default
            // on 2026-08-28 — without it, anyone working on the board would
            // have to tap through the map on every launch, which on the iOS
            // simulator means finding a window and posting a synthetic tap.
            //
            // It gets no way back to the map on purpose: the flag says "give me
            // the board", and a corner that leaves it would be answering a
            // question nobody asked. The way back belongs to the route a player
            // takes, which is the one below.
            if (BoardRequested())
            {
                Debug.Log("[GameBoot] branch=board");
                SafeBuild("the board", uid.rootVisualElement, () =>
                {
                    if (GetComponent<DebugGameView>() == null)
                        gameObject.AddComponent<DebugGameView>();
                });
                BootState("board", uid);
                return;
            }

            // **The house map is the game's first screen** (60-shell-build/03).
            //
            // Until 2026-08-28 this line built the board, and the map was
            // reachable only by dropping a `housemap.txt` beside the save. The
            // owner played that build twice and said the same thing both times:
            // he was dropped into a room having chosen nothing and did not know
            // where he was. A map that exists, reads well and answers taps is
            // worth nothing behind a debug flag.
            //
            // `housemap.txt` is retired rather than kept. With the map as the
            // default, a flag that selects the map is a switch with one
            // position — and `HouseMapView.Requested` would then be a property
            // nothing calls, which is precisely the shape that made this screen
            // unreachable for two builds in the first place. A device still
            // carrying the old file gets exactly the screen it got before.
            //
            // **This is a product decision, not a technical one**, and the
            // owner asked for it as an open question in tasks/OWNER-TODO.md
            // ("Должна ли карта дома быть входом в игру?"). Reverting it is one
            // `if` — see this task's NOTES.md.
            Debug.Log("[GameBoot] branch=map");
            SafeBuild("the house map", uid.rootVisualElement, () =>
            {
                if (GetComponent<CatShelter.View.HouseMapView>() == null)
                    gameObject.AddComponent<CatShelter.View.HouseMapView>();
            });
            BootState("map", uid);
        }

        /// <summary>
        /// `board.txt` beside the save: skip the map and build the board, the
        /// same convention as `capture.txt`, `coat.txt` and `meet.txt`.
        /// </summary>
        private static bool BoardRequested()
        {
            try
            {
                return System.IO.File.Exists(System.IO.Path.Combine(
                    Application.persistentDataPath, "board.txt"));
            }
            catch (System.Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Build and show 50-photo/09, wired to persist the named cat
        /// beside the board save the moment she confirms it.
        /// </summary>
        private void ShowMeetYourCat(VisualElement root, CatShelter.Core.CatTraits traits,
                                     string initialName = null)
        {
            var screen = gameObject.AddComponent<CatShelter.View.MeetYourCatScreen>();
            screen.Build(root, traits, initialName);
            screen.OnNamed = cat =>
            {
                CatShelter.Shell.CatSaveFile.Write(CatShelter.Core.CatSave.Write(cat));
                Report($"cat named: {cat.Name}");
            };
        }
    }
}
