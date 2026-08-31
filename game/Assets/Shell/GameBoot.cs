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

            // Start Google Play services fetching the subject-segmentation
            // module now, at the first instruction of the app's life, because
            // the download is what decides whether the player's FIRST cat is
            // read properly or guessed.
            //
            // The model is not in our APK — it cannot be, ML Kit publishes
            // subject segmentation only as a Play services optional module
            // (see CatVision.androidlib/build.gradle). Until it lands, every
            // call throws "Waiting for the subject segmentation optional module
            // to be downloaded", CatVision reports no mask, and CatCoat falls
            // back to the old centre-of-frame colour mean — one of six colours,
            // pattern always "solid".
            //
            // Which is exactly what the owner saw on 2026-08-30: a black, a
            // white and a brown cat photographed in one room came back as three
            // identical ginger cats. The whole point of the reading is the first
            // photograph, and the first photograph was the one guaranteed to
            // miss it.
            //
            // `Prepare` was written for this — its own summary says "so the
            // first photograph a player picks does not pay for the download" —
            // and until now it was called from nowhere. Third time tonight that
            // a finished piece of this game turned out to have no caller:
            // CaptureScreen.AskWorker and CatCardScreen's Hide were the others.
            //
            // Here rather than on the capture screen, though its summary
            // suggests that: boot is earlier by the whole of the loading screen
            // and the room choice, and the download needs every second it can
            // get. It does not block, it returns ready/requested/unavailable,
            // and off Android it is a no-op.
            Debug.Log($"[GameBoot] subject segmentation: {CatVision.Prepare()}");

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
        /// Puts the languages that need Unity 6's other text engine onto it, and
        /// leaves the rest exactly where they were.
        ///
        /// Two different faults, one switch. Arabic came out backwards — every
        /// letter present, the sentence reversed, because the standard generator
        /// does no bidirectional reordering. Chinese came out with a full stop
        /// alone at the start of a line, because the standard generator has no
        /// rule against breaking there. Both are what the advanced generator,
        /// rebased on HarfBuzz and ICU, is for.
        ///
        /// Applied at the panel root so it reaches every screen, and applied to
        /// **those languages only**. It is a different engine with its own
        /// metrics: switching the whole game to it would silently re-lay out
        /// thirteen languages that were photographed on the standard one, to fix
        /// four. Korean and Thai were checked and stay where they are — the
        /// reasons are beside the list below.
        ///
        /// Costs nothing for everyone else: a stylesheet never loaded and a class
        /// never added.
        /// </summary>
        private static void ApplyTextDirection(UIDocument uid)
        {
            var root = uid != null ? uid.rootVisualElement : null;
            if (root == null) return;

            // Which languages need the other text engine, and why each one does.
            //
            // Arabic: it reads right to left and the standard generator does no
            // reordering. The glyph harness draws a probe of four alifs and a
            // meem — an alif is a bare stroke, a meem a loop, so nobody needs to
            // read Arabic to judge it — and on a device the standard generator
            // put the loop on the wrong side. The sentence was backwards.
            //
            // Chinese and Japanese: line breaking. These scripts break between
            // any two characters, and the rule that a line may not BEGIN with a
            // full stop or a comma is a rule the standard generator does not
            // have. The seventeen-language sweep on 2026-08-29 photographed the
            // result on the "shelf jammed" card: 。 alone at the head of line
            // two, in both simplified and traditional. The advanced generator
            // carries ICU's line-breaking rules and does not do that.
            //
            // Korean is deliberately absent: it breaks on spaces like a European
            // language, and the sweep found nothing wrong with it. So is Thai,
            // which has no spaces at all — it needs a dictionary this build does
            // not ship (`m_ICUDataAsset: {fileID: 0}`), and the advanced
            // generator would not supply one. Its strings are clause-spaced by
            // hand instead; see NOTES-scripts.md.
            var rtl = false;
            var advanced = false;

            foreach (var language in new[]
                     {
                         SystemLanguage.Arabic,
                         SystemLanguage.ChineseSimplified,
                         SystemLanguage.ChineseTraditional,
                         SystemLanguage.Chinese,
                         SystemLanguage.Japanese,
                     })
            {
                var table = Copy.For(language);
                // `For` answers English for a language it has no table for, so
                // comparing against it alone would put every English player on
                // the other engine the day one of these tables is deleted. The
                // check has to establish that the table exists as well as that
                // we are in it.
                if (ReferenceEquals(table, Copy.English)) continue;
                if (!ReferenceEquals(Copy.Current, table)) continue;

                advanced = true;
                if (language == SystemLanguage.Arabic) rtl = true;
                break;
            }

            if (!advanced) return;

            var sheet = Resources.Load<StyleSheet>("UI/AdvancedText");
            if (sheet == null)
            {
                // Loud: the alternative is shipping Arabic backwards and finding
                // out from a review in a language nobody here reads.
                Debug.LogError("[GameBoot] Resources/UI/AdvancedText.uss is missing — " +
                               "Arabic will read backwards and CJK will break lines " +
                               "on a full stop");
                return;
            }

            root.styleSheets.Add(sheet);
            root.AddToClassList("advanced-text");
            if (rtl) root.AddToClassList("rtl");
            Debug.Log($"[GameBoot] advanced text generator on, rtl={rtl}");
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
            // The capture screen on demand, for checking: drop a `capture.txt`
            // next to the save. Since 50-photo/10 it is also the game's own
            // first screen — the first-run branch further down — and this flag
            // now means "offer the photograph again, whatever is already
            // saved", which is exactly what a first-run gate takes away.
            // A photo accepted here leads straight into meet-your-cat
            // (50-photo/09): traits in, her named cat out.
            if (CaptureRequested())
            {
                Debug.Log("[GameBoot] branch=capture");
                var screen = ShowCapture(uid.rootVisualElement);

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
                BootState("capture", uid);
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

            // **The photograph comes first, and it comes once** (50-photo/10).
            //
            // Everything behind it worked and nobody could reach it. The
            // capture screen, the animal recognition, the colour estimate, the
            // crop and the marks have been finished and measured since 28.08,
            // and until today the only way to see any of it was to push a
            // `capture.txt` into the app container by hand. The game promises
            // "it is her cat" and no player was ever asked for a cat. A full
            // playthrough on both platforms on 2026-08-29 is what said so out
            // loud.
            //
            // **Asked once, not every launch.** The gate is the saved cat, not
            // a "have we asked" flag: the two would be one fact kept in two
            // places, and the day they disagree the game either asks a player
            // who already has a cat or shows the map to one who has none. The
            // save is written the moment she confirms a name
            // (<see cref="ShowMeetYourCat"/>), so quitting before that means
            // being asked again — which is right, because she never answered.
            //
            // **Skipping is a supported path, not a dead end.** The skip
            // control is on the capture screen already; what it lacked was a
            // screen to be on. It hands `CatTraits.Default` to the same
            // meet-your-cat screen a photographed cat reaches, so a player who
            // skips is named, saved and playing with no camera, no permission
            // and no network — the two conditions this task's VERIFY names.
            // The share of players who take it is one of the numbers this
            // project watches (cat-shelter-mvp.md section 5); recording that
            // number is 70-analytics and not this.
            //
            // Placed after every debug flag above and before the map below, so
            // `board.txt`, `coat.txt`, `meet.txt` and `glyphs.txt` still open
            // what they name on a device that has never met a cat.
            if (!HasACat())
            {
                Debug.Log("[GameBoot] branch=first-run");
                ShowCapture(uid.rootVisualElement);
                BootState("first-run", uid);
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
        /// Has this player already answered the question the first run asks?
        ///
        /// The saved cat IS the answer, which is why nothing else is stored to
        /// remember that it was asked. `CatSave.Read` returns null for a
        /// missing file and for a corrupt one alike (its own promise), and both
        /// mean the same thing here: there is no cat to play as, so offer the
        /// photograph. A player whose save was damaged is asked once more
        /// rather than dropped into the house with `CatTraits.Default` and no
        /// idea where her cat went.
        /// </summary>
        private static bool HasACat()
        {
            return CatShelter.Core.CatSave.Read(CatShelter.Shell.CatSaveFile.Read()) != null;
        }

        /// <summary>
        /// Build the capture screen and wire what comes after it.
        ///
        /// One method for both callers — the `capture.txt` flag and the first
        /// run — so the checking route exercises the player's route rather than
        /// a parallel copy of it. The flag adds a stubbed Vision answer and a
        /// photo to push through the pipeline; everything else, including where
        /// an accepted or skipped cat goes next, is the same code.
        /// </summary>
        private CatShelter.View.CaptureScreen ShowCapture(VisualElement root)
        {
            var screen = gameObject.AddComponent<CatShelter.View.CaptureScreen>();
            SafeBuild("the capture screen", root, () => screen.Build(root));
            screen.OnAccepted = photo => Report($"accepted a {photo.Length}-byte photo");
            // Every step of the picker, success or not, into capture-state.txt.
            // See CaptureScreen.OnTrouble for why: the file used to record only
            // outcomes that worked, so a phone where nothing works wrote an
            // empty file and looked exactly like a phone nobody had touched.
            screen.OnTrouble = note => Report(note);
            screen.OnCatReady = traits =>
            {
                // Both ways off this screen arrive here: a photograph the
                // pipeline finished with, and the skip control's fixed
                // `CatTraits.Default`. They are indistinguishable from this
                // point on except by `traits.Origin`, which is the whole point
                // of the skip path — she gets a real cat, not a lesser one.
                Report($"cat ready ({traits.Origin}): {traits}");

                // Not in this call, and not on the next tick either — the same
                // rule, and the same 120ms, as GoToTheHouse below.
                //
                // MeetYourCatScreen.Build runs CoatBuilder.TryBuild
                // synchronously, and on a first run there is nothing cached to
                // shorten it. Hiding the capture screen and building the meet
                // screen right here therefore did both halves of the wrong
                // thing at once: the frame the player was left staring at
                // during the build was whatever the capture screen looked like
                // with its waiting indicator already cleared, and the panel had
                // nothing else in it. The screen simply stopped.
                //
                // Deferring means the capture screen's waiting block — which
                // CaptureScreen.Handle now leaves standing for exactly this
                // reason — gets painted and gets to move for a few sweeps
                // before the main thread goes under. She sees "Copying the
                // colours…" over a bar that was moving a moment ago, rather
                // than a dead screen; the words are true of the coat build, so
                // this is not a decoration over a lie.
                //
                // Hidden AFTER the meet screen is built, never before. Both
                // roots are absolute and full-screen and the newer one is added
                // last, so it covers the older one; hiding first would open a
                // window of blank panel exactly as wide as the build.
                if (root == null)
                {
                    ShowMeetYourCat(root, traits);
                    screen.Hide();
                    return;
                }
                root.schedule.Execute(() =>
                {
                    ShowMeetYourCat(root, traits);
                    screen.Hide();
                }).ExecuteLater(120);
            };
            return screen;
        }

        /// <summary>
        /// Build and show 50-photo/09, wired to persist the named cat
        /// beside the board save the moment she confirms it — and then to
        /// take her into the house.
        ///
        /// Confirming used to do the first half only. Measured by a pixel diff
        /// across the tap on 2026-08-29: the screen before and the screen after
        /// were identical files. The name was saved and the player was left
        /// looking at the cat she had just named with nothing to press.
        /// </summary>
        private void ShowMeetYourCat(VisualElement root, CatShelter.Core.CatTraits traits,
                                     string initialName = null)
        {
            var screen = gameObject.AddComponent<CatShelter.View.MeetYourCatScreen>();
            screen.Build(root, traits, initialName);
            screen.OnNamed = cat =>
            {
                // Saved BEFORE the swap, not after it. The house map's own
                // build reads the disk, and a cat written afterwards would be a
                // cat the first screen of the game does not know about; worse,
                // anything thrown on the way to the map would cost her the name
                // she just typed.
                CatShelter.Shell.CatSaveFile.Write(CatShelter.Core.CatSave.Write(cat));
                Report($"cat named: {cat.Name}");
                GoToTheHouse(root);
            };
        }

        /// <summary>
        /// Leave meet-your-cat for the house map — the game's first screen for
        /// everyone who already has a cat, and now the screen the photo flow
        /// hands over to.
        ///
        /// **Not during the tap, and not on the next tick either.** Both rules
        /// are HouseMapView.StartPlaying's, learned there and holding here for
        /// the same two reasons: the element under the finger is inside the
        /// tree this swap destroys, and UI Toolkit is still walking the
        /// propagation path through it; and a waiting indicator that is created
        /// and destroyed before a repaint is worse than none, because it looks
        /// like the problem is solved. 120ms is that file's measured number and
        /// this is the same panel — do not change one without the other.
        ///
        /// The map's own Build() clears the panel, which is what takes the
        /// meet-your-cat tree and the veil off screen; the two screens'
        /// components are destroyed here so nothing is left holding elements
        /// that no longer exist.
        /// </summary>
        private void GoToTheHouse(VisualElement root)
        {
            if (root == null) return;
            Waiting(root);
            root.schedule.Execute(() =>
            {
                foreach (var s in GetComponents<CatShelter.View.MeetYourCatScreen>())
                    Destroy(s);
                foreach (var s in GetComponents<CatShelter.View.CaptureScreen>())
                    Destroy(s);

                Debug.Log("[GameBoot] branch=map (after the photograph)");
                SafeBuild("the house map", root, () =>
                {
                    if (GetComponent<CatShelter.View.HouseMapView>() == null)
                        gameObject.AddComponent<CatShelter.View.HouseMapView>();
                });
            }).ExecuteLater(120);
        }

        /// <summary>
        /// A bar that slides back and forth over whatever is on screen, and no
        /// words on it.
        ///
        /// The house map is not free — it parses all thirty-seven level files
        /// through `LevelAssets.LoadAll` before it can draw a single room, and
        /// that blocks the main thread for over a second. The owner played a
        /// build where a tap bought that silence and read it exactly as a
        /// person would: "кликаю - ничего не происходит... юзер не понимает что
        /// происходит, кликает и раздражается, что все зависло."
        ///
        /// Wordless on purpose, and it is the same choice
        /// `HouseMapView.ShowOpening(root, null)` already makes on the way back
        /// from the board: there is no honest one-word label for this moment
        /// that is not either "Loading" — engine vocabulary the game uses
        /// nowhere — or a new copy key in seventeen tables for something on
        /// screen for a second and a half. Not a percentage either: the work
        /// behind it has no measurable progress and a number that jumps 0 to
        /// 100 lies more than no number does.
        /// </summary>
        private static void Waiting(VisualElement root)
        {
            var veil = new VisualElement { name = "waiting" };
            veil.style.position = Position.Absolute;
            veil.style.left = veil.style.right = veil.style.top = veil.style.bottom = 0;
            veil.style.backgroundColor = new Color(0.957f, 0.918f, 0.847f, 0.92f);
            veil.style.alignItems = Align.Center;
            veil.style.justifyContent = Justify.Center;
            // Swallows further taps: the name field is still under this until
            // the map replaces it, and a second "That's her" would write the
            // save twice and schedule a second map.
            veil.pickingMode = PickingMode.Position;

            var track = new VisualElement();
            track.style.width = 168;
            track.style.height = 5;
            track.style.backgroundColor = new Color(0.84f, 0.78f, 0.66f);
            track.style.borderTopLeftRadius = track.style.borderTopRightRadius =
                track.style.borderBottomLeftRadius =
                    track.style.borderBottomRightRadius = 3;

            // Offset with `left` on a relatively-positioned child, not with an
            // absolute one: the same shape HouseMapView's veil uses, and it is
            // the shape that was actually seen moving on a device.
            var bar = new VisualElement();
            bar.style.height = Length.Percent(100);
            bar.style.width = Length.Percent(34);
            bar.style.backgroundColor = new Color(0.20f, 0.16f, 0.12f);
            bar.style.borderTopLeftRadius = bar.style.borderTopRightRadius =
                bar.style.borderBottomLeftRadius =
                    bar.style.borderBottomRightRadius = 3;
            track.Add(bar);
            veil.Add(track);
            root.Add(veil);

            var step = 0;
            veil.schedule.Execute(() =>
            {
                step = (step + 1) % 40;
                var t = step < 20 ? step : 40 - step;   // 0..20..0
                bar.style.left = Length.Percent(t * 3.3f);
            }).Every(28);
        }
    }
}
