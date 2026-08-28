using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CatShelter.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace CatShelter.View
{
    /// <summary>
    /// Task 20-rules-core/06: debug view of plain rectangles — a build playable
    /// by hand before any art exists. The scene asset carries a UIDocument with
    /// DebugGame.uxml/uss assigned; this component populates the root.
    ///
    /// Conventions (DECISIONS.md):
    /// - buried kinds hide what they are (D3) and render as prop_unknown;
    /// - locked kinds are SEEN (D15) with a rope badge in the tile corner, and
    ///   ignore taps until unlocked (3.11);
    /// - the lose screen shows only a levels-finished count and Replay; the
    ///   "one more shelf" fake-door offer it carried until 2026-08-27 was
    ///   removed when D4 was revised — a tap on a free rescue was not
    ///   evidence anyone would pay, so the offer is gone until it can
    ///   charge for itself (full reasoning at the lose-card call site
    ///   below, and in DECISIONS.md D4 and 60-shell-build/07's NOTES.md).
    /// Styling lives in DebugGame.uss; inline styles only for the per-tile
    /// sprite, and for the colour fallback when a kind has no art file.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class DebugGameView : MonoBehaviour
    {
        // Golden-angle hues so any ten kinds stay distinguishable at tile size
        // (same rule as the HTML prototype and art-brief section 6).
        private static Color HueFor(string kindId)
        {
            int n = 0;
            var digits = kindId.Where(char.IsDigit).ToArray();
            if (digits.Length > 0)
                n = int.Parse(new string(digits));
            else
                foreach (var c in kindId) n = n * 31 + c;
            float hue = (n * 137.508f) % 360f;
            return Color.HSVToRGB(hue / 360f, 0.38f, 0.62f);
        }

        private Board _board;
        private Level _level;
        private VisualElement _pileArea;
        private VisualElement _shelfArea;
        private Label _status;
        private Label _title;
        private VisualElement _overlay;
        private Label _overlayTitle;
        private Label _overlayBody;
        private Button _primaryButton;
        private Button _secondaryButton;

        // Loaded once per kind and kept: a pile is 60 tiles and a room is
        // redrawn on every move, so Resources.Load per tile per frame would be
        // sixty lookups a tap.
        private readonly Dictionary<string, Texture2D> _sprites = new();

        private readonly List<Level> _levels = new();
        private int _levelIndex;
        private RoomPlan _plan;
        private PlayerProgress _progress;

        // --- 60-shell-build/04: the cat on the board, whose pose follows
        // PlayerProgress.CatState. No photo flow is wired into this view yet
        // (50-photo/09 is todo, and nothing hands a saved Cat to GameBoot),
        // so this is the fixed default coat CatTraits.Default already gives
        // a player who skips the photo — "whatever cat was built" is, today,
        // nobody's cat. Task 50-photo owns swapping this for the real one.
        private static readonly CatTraits CatStateTraits = CatTraits.Default;
        private VisualElement _catPortrait;
        private Texture2D _catTexture;
        private int _catTextureState = -1;

        // --- 60-shell-build/06: the win screen's before/after, built once
        // per room close from the props that room actually held. No room art
        // exists yet (40-art/07 is todo), so a drawn dirty/clean pair is not
        // available; this stands in with the real prop sprites of the room
        // that just closed, scattered for "before" and lined up for "after".
        private VisualElement _beforeAfter;
        private VisualElement _beforeCollage;
        private VisualElement _afterCollage;
        private Label _beforeLabel;
        private Label _afterLabel;

        private void OnEnable()
        {
            var uid = GetComponent<UIDocument>();

            // The UXML carries its own <Style src="DebugGame.uss" />; all
            // named elements are declared there, so Q() resolves on first run.
            var gameRoot = uid.rootVisualElement.Q("game-root") ?? uid.rootVisualElement;

            _pileArea = gameRoot.Q("pile");
            _shelfArea = gameRoot.Q("shelf");
            _title = gameRoot.Q<Label>("title");
            _status = gameRoot.Q<Label>("status");
            _overlay = gameRoot.Q("overlay");
            _overlayTitle = gameRoot.Q<Label>("overlay-title");
            _overlayBody = gameRoot.Q<Label>("overlay-body");
            _primaryButton = gameRoot.Q<Button>("primary");
            _secondaryButton = gameRoot.Q<Button>("secondary");

            if (_pileArea == null)
                throw new InvalidOperationException(
                    "DebugGame.uxml skeleton not found in UIDocument source");

            Debug.Log("[Board] enabled, skeleton found");
            BuildCatPortrait(gameRoot);
            BuildBeforeAfter(gameRoot);

            _levels.Clear();
            var loaded = LevelAssets.LoadAll();
            if (!loaded.CanStart)
            {
                // Every room came back incomplete — LevelAssets already
                // logged which ones and why (Core.LevelLoadPolicy,
                // 30-levels-solver/06). There is nothing safe to hand
                // RoomPlan, so an honest stop replaces the blank screen this
                // used to leave behind when LoadAll threw here, uncaught,
                // before _plan/_progress ever existed.
                ShowCard(Shell.Copy.Of("levels.unavailable.title"),
                    Shell.Copy.Of("levels.unavailable.body"),
                    null, null, null, null);
                return;
            }
            _levels.AddRange(loaded.Levels);
            _plan = new RoomPlan(_levels);
            _progress = new PlayerProgress(_plan.PilesPerRoomInOrder());
            if (!Resume())
                StartLevel(0);
        }

        /// <summary>
        /// Task 6.8: pick up the saved position, or report that there is none.
        /// Anything unreadable — missing file, damaged text, a save from a level
        /// that no longer ships, a position the rules reject — starts a fresh
        /// board instead of throwing. Losing a pile is a setback; a launch
        /// crash loses the player.
        /// </summary>
        private bool Resume()
        {
            var board = SaveResume.TryResume(Shell.SaveFile.Read(), _levels, out var reason);
            if (board == null)
            {
                if (reason != "no readable save")
                    Debug.LogWarning($"[DebugGameView] starting fresh: {reason}");
                return false;
            }

            _board = board;
            _levelIndex = SaveResume.IndexOf(_levels, board);
            _level = board.Level;

            // Progress is not saved (60-shell-build/08 scope excludes it), so
            // it is replayed: every level before the resumed one was won to get
            // here. Without this, finishing the resumed level would ask
            // PlayerProgress to complete a pile it is not standing on, and it
            // refuses that by design.
            for (int i = 0; i < _levelIndex; i++)
                _progress.CompletePile(_levels[i].PileIndex);
            Analytics.LevelStart(_level.Number);
            Render();
            return true;
        }

        /// <summary>Every move-completing path goes through here (VERIFY 3).</summary>
        private void Save() => Shell.SaveFile.Write(GameSave.Write(_board, null));

        private void StartLevel(int index)
        {
            if (index >= _levels.Count)
            {
                // Unreachable by play — Finish() ends the house on the last
                // pile — but a save naming a level past the end would land
                // here, and showing the ending screen is the right answer.
                Shell.SaveFile.Clear();
                ShowCard(Shell.Copy.Of("house.complete.title"),
                    Shell.Copy.Of("house.complete.body"),
                    null, null, null, null);
                return;
            }
            _levelIndex = index;
            _level = _levels[index];
            _board = new Board(_level);
            Analytics.LevelStart(_level.Number);
            Save();
            Render();
        }

        private void Render()
        {
            var roomNo = RoomPlan.RoomNumber(_level.RoomId);
            _title.text = Shell.Copy.Of("board.title", roomNo, _plan.RoomCount,
                _level.PileIndex + 1, _plan.PilesIn(_level.RoomId));
            _status.text = Shell.Copy.Of("board.items_left",
                _level.Pile.Count - _board.TakenOrder.Count);

            RenderPile();
            RenderShelf();
            RenderCat();
        }

        private void RenderPile()
        {
            _pileArea.Clear();
            // One lookup for the whole pass: MakeTile used to ask the board for
            // the full available list per tile.
            var available = _board.GetAvailable().Select(i => i.Id).ToHashSet();
            foreach (var entry in _level.Pile)
            {
                if (_board.IsTaken(entry.Item.Id)) continue;
                _pileArea.Add(MakeTile(entry, available.Contains(entry.Item.Id)));
            }
        }

        private void RenderShelf()
        {
            _shelfArea.Clear();
            for (int i = 0; i < _board.Shelf.Capacity; i++)
            {
                var slot = new VisualElement();
                slot.AddToClassList("game__slot");

                var item = _board.Shelf.Slots[i];
                if (item != null)
                {
                    var art = SpriteFor(item.Kind.Id);
                    if (art != null) Paint(slot, art);
                    else slot.Add(MakeLabel(item.Kind.Id));
                }

                _shelfArea.Add(slot);
            }
        }

        // =====================================================================
        // 60-shell-build/04: cat states — the cat's pose on the board follows
        // PlayerProgress.CatState, which changes only after the 4th and 8th
        // completed room (Core/PlayerProgress.CatStateFor). Everything below
        // this point until RenderPile/RenderShelf's counterpart section is
        // this task's code; the before/after block further down belongs to
        // 06-win-screen instead.
        // =====================================================================

        /// <summary>A small portrait, always on the board, in front of the
        /// overlay so a room-clean card does not have to carry the pose
        /// change — it is already visible in the room behind the card the
        /// instant the boundary is crossed (task wording: "visible in the
        /// room the transition happens in").</summary>
        private void BuildCatPortrait(VisualElement gameRoot)
        {
            if (_catPortrait != null) return; // OnEnable can re-run; do not double-insert
            _catPortrait = new VisualElement { name = "cat-portrait" };
            _catPortrait.AddToClassList("game__cat");
            gameRoot.Insert(gameRoot.IndexOf(_overlay), _catPortrait);
        }

        /// <summary>Rebuilds the cat texture only when her state actually
        /// changed — CoatBuilder's pass walks every pixel of the silhouette,
        /// and Render() runs on every tap.</summary>
        private void RenderCat()
        {
            if (_catPortrait == null || _progress == null) return;
            int state = _progress.CatState;
            // The recorded state is the one last *attempted*, not last built. A
            // coat that failed will fail again for the same reason, and the old
            // `_catTexture != null` guard would have retried a whole-silhouette
            // pass on every tap for a picture that is not going to appear.
            if (_catTextureState == state) return;

            var baseArt = CoatBuilder.LoadBase(CatStateTraits, state);
            if (baseArt == null) return; // art not shipped yet; portrait stays blank

            var built = CoatBuilder.TryBuild(baseArt, CatStateTraits, state);
            if (_catTexture != null) UnityEngine.Object.Destroy(_catTexture);
            // Null when the coat could not be built: own nothing, and paint the
            // untinted silhouette. `baseArt` is the Resources asset itself, so
            // destroying it on the next state change would take the art out of
            // the game for the rest of the run.
            _catTexture = built;
            _catTextureState = state;
            Paint(_catPortrait, built != null ? built : baseArt);
        }

        /// <summary>
        /// The prop's art, by the kind id — level files name the sprite
        /// directly (`prop_vase`), so there is no lookup table between what a
        /// level says and what is drawn. Null when the file is missing, and
        /// the tile then falls back to its old coloured square rather than
        /// vanishing.
        /// </summary>
        private Texture2D SpriteFor(string kindId)
        {
            if (_sprites.TryGetValue(kindId, out var cached)) return cached;
            var texture = Resources.Load<Texture2D>($"Art/{kindId}");
            if (texture == null)
                Debug.LogWarning($"[DebugGameView] no art for '{kindId}'");
            _sprites[kindId] = texture;
            return texture;
        }

        private VisualElement MakeTile(PileEntry entry, bool available)
        {
            // Whether a kind is visible is a rule, and the rule lives in Core.
            // This used to be a hand-copied duplicate of Board.IsRevealed.
            var revealed = _board.IsRevealed(entry.Item);
            var locked = _board.IsLockedByComplication(entry.Item);

            var tile = new VisualElement();
            tile.AddToClassList("game__tile");

            if (!revealed)
            {
                // D3: buried items hide their kind. prop_unknown is the drawn
                // version of that — a covered shape, not a grey square.
                tile.AddToClassList("game__tile--hidden");
                Paint(tile, SpriteFor("prop_unknown"));
                return tile;
            }

            var art = SpriteFor(entry.Item.Kind.Id);
            if (art != null)
            {
                Paint(tile, art);
                // The lock sits in the CORNER, over the prop, not across it.
                // prop_locked is drawn as a coil of rope on its own — laid
                // over the whole tile it hides the prop completely, which is
                // the thing the lock must not do: the player has to see which
                // kind is being withheld to plan around it (D15, 40-art/02).
                // At corner size it reads as "this one is tied up" and the
                // prop underneath stays whole.
                if (locked)
                {
                    var badge = new VisualElement();
                    badge.AddToClassList("game__tile-lock");
                    Paint(badge, SpriteFor("prop_locked"));
                    badge.pickingMode = PickingMode.Ignore;
                    tile.Add(badge);
                }
            }
            else
            {
                // No art for this kind: the coloured square the game used
                // before any of it existed, so a missing file is visible
                // rather than invisible.
                tile.style.backgroundColor = HueFor(entry.Item.Kind.Id);
                tile.Add(MakeLabel(entry.Item.Kind.Id, locked ? "🔒" : null));
            }

            if (available && !locked)
                tile.RegisterCallback<ClickEvent>(_ => Take(entry.Item.Id));
            else
                tile.AddToClassList("game__tile--dim");

            return tile;
        }

        private static void Paint(VisualElement element, Texture2D art)
        {
            if (art == null) return;
            element.style.backgroundImage = new StyleBackground(art);
            // The props are drawn on a transparent square with their own
            // margin, so scale-to-fit keeps every prop the same size on the
            // board regardless of its shape.
            element.style.backgroundColor = Color.clear;
            element.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
        }

        private static Label MakeLabel(string kindId, string overrideText = null)
        {
            var label = new Label(overrideText ?? KindShort(kindId));
            label.AddToClassList("game__tile-label");
            return label;
        }

        private static string KindShort(string kindId)
        {
            var digits = kindId.Where(char.IsDigit).ToArray();
            return digits.Length > 0 ? new string(digits) : kindId[..2];
        }

        private void Take(int itemId)
        {
            var triplesBefore = _board.TriplesCompleted;
            if (_board.IsOver || !_board.TakeItem(itemId))
            {
                // A tap that changes nothing is worth a line: it is either a
                // locked tile behaving correctly or a bug, and from outside the
                // app those look the same.
                Debug.Log($"[Board] tap {itemId} refused " +
                          $"(over={_board.IsOver})");
                return;
            }
            Debug.Log($"[Board] took {itemId}, shelf={_board.Shelf.Occupied}, " +
                      $"triples={_board.TriplesCompleted}, " +
                      $"available={_board.GetAvailable().Count}");

            // Feedback before the redraw: the tap should answer the finger, not
            // wait for a frame of layout. A match speaks louder than a
            // placement, which is the only difference the player needs to hear.
            if (_board.TriplesCompleted > triplesBefore)
                Shell.Feedback.Match();
            else
                Shell.Feedback.Place();

            // Written on the move, not on OnApplicationPause: iOS kills
            // backgrounded apps without warning and the pause callback is not a
            // reliable last chance (DECISIONS.md D12).
            Save();

            // Draw first, then judge: the last move used to jump straight to the
            // card, so the player never saw the tile that ended the level.
            Render();
            if (_board.IsOver)
                Finish();
        }

        private void Finish()
        {
            // Which corner was cleared and whether the room is finished are
            // both RoomPlan's to answer; the view used to work it out by
            // comparing room numbers of adjacent levels.
            var lastPileOfRoom = _plan.IsLastPileOfRoom(_level);

            // The move that ended the level was already saved by Take, and that
            // position is useless to resume into: a finished board with no card
            // on screen is a dead end on a phone. Overwrite it with where the
            // player should land — the next level after a win, the start of this
            // one after a jam, which is what "Replay" does anyway.
            if (_board.Outcome == GameOutcome.Win && _levelIndex + 1 < _levels.Count)
                Shell.SaveFile.Write(GameSave.Write(new Board(_levels[_levelIndex + 1]), null));
            else
                Shell.SaveFile.Clear();

            if (_board.Outcome == GameOutcome.Win)
            {
                // Progress advances on a win and only on a win, so the cat's
                // state follows completed rooms rather than levels played.
                _progress.CompletePile(_level.PileIndex);
                Analytics.LevelWin(_level.Number);

                // 04-cat-states: Render() already drew this frame before
                // Finish() ran (Take() draws, then judges), against the state
                // from before this pile completed. Redraw the portrait now so
                // the room behind the card already shows the new pose the
                // instant the 4th or 8th room closes — not on the level after.
                RenderCat();

                // Task 6.11: the last pile of the last room ends the house, and
                // it gets the ending screen rather than the ordinary win card
                // followed by one. Reached by playing, shown once, and it does
                // not offer to start over — that is out of scope on purpose.
                if (_plan.Next(_level) == null)
                {
                    Shell.SaveFile.Clear();
                    Debug.Log("[Board] house complete");
                    // Room 12's own before/after, on the ending card. It used to
                    // be the one room whose pair a player never saw: this branch
                    // returned before the transformation was shown, so the last
                    // room — the one they worked hardest for — was the only one
                    // that ended with words alone. A verifier found it by
                    // reading; nobody had played that far.
                    ShowCard(Shell.Copy.Of("house.complete.title"),
                        Shell.Copy.Of("house.complete.body"),
                        null, null, null, null);
                    ShowRoomTransformation(_level);
                    return;
                }

                // Permission is asked here, after a level was actually cleared,
                // and only once ever — see Shell/EveningReminder.
                StartCoroutine(Shell.EveningReminder.OnLevelCompleted(this, _level.Number));
                Debug.Log($"[Board] win: level {_level.Number}, " +
                          $"lastPileOfRoom={lastPileOfRoom}");
                ShowCard(
                    Shell.Copy.Of(lastPileOfRoom ? "win.room_clean.title" : "win.corner.title"),
                    lastPileOfRoom
                        ? Shell.Copy.Of("win.room_clean.body")
                        : Shell.Copy.Of("win.corner.body"),
                    Shell.Copy.Of("win.next"), () => { HideCard(); StartLevel(_levelIndex + 1); },
                    null, null);

                // 06-win-screen: the before/after only for the room's last
                // pile (VERIFY/SCOPE — not shown for a corner clear, which
                // already has its own feedback). ShowCard above already hid
                // it by default; this turns it back on for this one card.
                if (lastPileOfRoom)
                    ShowRoomTransformation(_level);
            }
            else
            {
                Analytics.LevelFail(_level.Number);
                // The "one more shelf" fake door was removed on 2026-08-27 —
                // D4 revised. It offered something, refused it, and measured a
                // tap on a FREE button, which answers "do you want to not
                // lose" (everyone does) rather than "would you pay". The
                // annoyance was real and the number was not going to decide
                // anything. Analytics.BoosterTap and Board.AddShelfSlots both
                // stay: the button comes back when there is a price on it.
                Debug.Log("[Board] lose");
                ShowCard(Shell.Copy.Of("lose.title"),
                    Shell.Copy.Of("lose.body", _levelIndex),
                    Shell.Copy.Of("lose.replay"), () =>
                    {
                        HideCard();
                        StartLevel(_levelIndex);   // replay the lost level
                    },
                    null, null);
            }
        }

        private void ShowCard(string title, string body,
                              string primaryText, Action onPrimary,
                              string secondaryText, Action onSecondary)
        {
            _overlayTitle.text = title;
            _overlayBody.text = body;

            // 06-win-screen: every card hides the before/after by default;
            // Finish() turns it back on right after this call, only for a
            // room's last pile. Centralised here so the ordinary corner-win,
            // lose, and house-complete cards never have to remember to hide it.
            HideRoomTransformation();

            if (primaryText == null)
            {
                // A card with nothing to press: the end of the house has
                // nowhere to go next, and a button would have to invent one.
                _primaryButton.style.display = DisplayStyle.None;
            }
            else
            {
                _primaryButton.style.display = DisplayStyle.Flex;
                _primaryButton.text = primaryText;
                _primaryButton.clickable = new Clickable(onPrimary);
            }
            if (secondaryText != null)
            {
                _secondaryButton.text = secondaryText;
                _secondaryButton.style.display = DisplayStyle.Flex;
                _secondaryButton.SetEnabled(true);
                _secondaryButton.clickable = new Clickable(onSecondary);
            }
            else
            {
                _secondaryButton.style.display = DisplayStyle.None;
            }
            _overlay.AddToClassList("game__overlay--shown");
        }

        private void HideCard() => _overlay.RemoveFromClassList("game__overlay--shown");

        private void OnPrimary() { /* wired per-card via clickable */ }
        private void OnSecondary() { /* wired per-card via clickable */ }

        // =====================================================================
        // 60-shell-build/06: win screen before/after. Everything from here to
        // the end of the class is this task's code; it shares the overlay
        // card with the room-clean win text above but owns none of it.
        //
        // No room art exists (40-art/07-rooms is status:todo), so there is no
        // dirty/clean frame pair to show. What stands in: the actual prop
        // sprites the room that just closed was built from — scattered for
        // "before", the same sprites lined up for "after". It is real data
        // from the room (which kinds it held), drawn with real art (the
        // shipped 30 props), not a placeholder image — but it is not the
        // drawn room pair the task asks for either, and that gap belongs on
        // record rather than papered over. See NOTES.md.
        // =====================================================================

        private const int MaxShownProps = 9;

        private void BuildBeforeAfter(VisualElement gameRoot)
        {
            if (_beforeAfter != null) return; // OnEnable can re-run; do not double-insert
            var card = gameRoot.Q("card");

            _beforeAfter = new VisualElement { name = "before-after" };
            _beforeAfter.AddToClassList("game__before-after");

            var beforePane = MakeBeforeAfterPane("game__ba-pane--before",
                out _beforeCollage, out _beforeLabel);
            var afterPane = MakeBeforeAfterPane("game__ba-pane--after",
                out _afterCollage, out _afterLabel);

            _beforeAfter.Add(beforePane);
            _beforeAfter.Add(afterPane);

            // Between the title and the body: the spectacle comes first, the
            // sentence explains it, same order as D8's "was — became" pitch.
            card.Insert(card.IndexOf(_overlayTitle) + 1, _beforeAfter);

            // Static captions, set once like every other label in this view
            // (Copy.Of at build time, not per-frame).
            _beforeLabel.text = Shell.Copy.Of("win.before");
            _afterLabel.text = Shell.Copy.Of("win.after");

            HideRoomTransformation();
        }

        private static VisualElement MakeBeforeAfterPane(string paneClass,
            out VisualElement collage, out Label label)
        {
            var pane = new VisualElement();
            pane.AddToClassList("game__ba-pane");
            pane.AddToClassList(paneClass);

            collage = new VisualElement();
            collage.AddToClassList("game__ba-collage");

            label = new Label();
            label.AddToClassList("game__ba-label");

            pane.Add(collage);
            pane.Add(label);
            return pane;
        }

        /// <summary>
        /// Populates and shows the before/after for the room that just
        /// closed, from the props that room actually held — gathered across
        /// every pile of <paramref name="closedLevel"/>'s room, not just its
        /// last one, so a four-pile room shows what the whole room was made
        /// of. Capped and shuffled by a seed fixed to the room number: every
        /// player sees the same room the same way, and the same room shows
        /// the same set on a replay.
        /// </summary>
        private void ShowRoomTransformation(Level closedLevel)
        {
            var kinds = _levels.Where(l => l.RoomId == closedLevel.RoomId)
                                .SelectMany(l => l.Pile)
                                .Select(e => e.Item.Kind.Id)
                                .Distinct()
                                .OrderBy(id => id, StringComparer.Ordinal)
                                .ToList();

            if (kinds.Count > MaxShownProps)
            {
                var rng = new System.Random(RoomPlan.RoomNumber(closedLevel.RoomId));
                kinds = kinds.OrderBy(_ => rng.Next())
                             .Take(MaxShownProps)
                             .OrderBy(id => id, StringComparer.Ordinal)
                             .ToList();
            }

            _beforeCollage.Clear();
            _afterCollage.Clear();

            // Both branches below style these frames, and the room branch used
            // to leave its inline sizes behind. Unreachable today — the branch
            // is chosen once per room and never switches — but a leak that
            // depends on nobody ever changing the order is a trap, not a
            // safeguard. Reset first, then let the branch style them.
            foreach (var frame in new[] { _beforeCollage, _afterCollage })
            {
                frame.style.width = StyleKeyword.Null;
                frame.style.height = StyleKeyword.Null;
                frame.style.backgroundColor = StyleKeyword.Null;
                frame.style.backgroundImage = StyleKeyword.Null;
                frame.style.borderLeftWidth = frame.style.borderRightWidth =
                    frame.style.borderTopWidth = frame.style.borderBottomWidth =
                        StyleKeyword.Null;
                frame.style.paddingTop = frame.style.paddingLeft = StyleKeyword.Null;
            }

            // The room itself, when it has been drawn. This is what the task
            // asked for from the start; the prop collage below was written
            // when 40-art/07-rooms had delivered nothing, and it survived the
            // delivery because nobody went back. The owner played the game and
            // said it plainly: "мы рисовали комнаты грязные и чистые, почему
            // просто не показать комнату до и после?" — the whole promise of
            // this game is that pair of pictures.
            var roomNo = RoomPlan.RoomNumber(closedLevel.RoomId)
                                 .ToString("00", CultureInfo.InvariantCulture);
            var dirty = SpriteNamed($"Art/room_{roomNo}_dirty");
            var clean = SpriteNamed($"Art/room_{roomNo}_clean");
            if (dirty != null && clean != null)
            {
                Debug.Log($"[Board] before/after: room {roomNo} art");

                // The frames are square (116×116) because they were built to
                // hold a scatter of props. A room is 1856×3328 — portrait, and
                // scale-to-fit would shrink it into a letterboxed sliver. So
                // the frames become portrait for a room, and lose the painted
                // background and border they used to need: the picture is the
                // whole panel now, and a frame around it only competes.
                foreach (var frame in new[] { _beforeCollage, _afterCollage })
                {
                    frame.style.width = 104;
                    frame.style.height = 186;
                    frame.style.backgroundColor = Color.clear;
                    frame.style.borderLeftWidth = frame.style.borderRightWidth =
                        frame.style.borderTopWidth = frame.style.borderBottomWidth = 0;
                    frame.style.paddingTop = frame.style.paddingLeft = 0;
                }

                Paint(_beforeCollage, dirty);
                Paint(_afterCollage, clean);

                _beforeAfter.style.display = DisplayStyle.Flex;
                return;
            }

            // No pair drawn for this room: fall back to the props it held,
            // scattered and then lined up. Real data, real art, and honest
            // about being second best.
            Debug.Log($"[Board] before/after: room {roomNo} has no art, using props");
            var scatter = new System.Random(RoomPlan.RoomNumber(closedLevel.RoomId));
            foreach (var kindId in kinds)
            {
                var art = SpriteFor(kindId);
                if (art == null) continue; // missing art stays missing, not a blank crash

                // "Before": jumbled and overlapping, at odd angles — clutter.
                var messy = new VisualElement();
                messy.AddToClassList("game__ba-item");
                messy.AddToClassList("game__ba-item--messy");
                Paint(messy, art);
                messy.style.left = scatter.Next(0, 82);
                messy.style.top = scatter.Next(0, 82);
                messy.style.rotate = new Rotate(new Angle(scatter.Next(-28, 28), AngleUnit.Degree));
                _beforeCollage.Add(messy);

                // "After": the same sprites, upright and in a tidy row — order.
                var tidy = new VisualElement();
                tidy.AddToClassList("game__ba-item");
                tidy.AddToClassList("game__ba-item--tidy");
                Paint(tidy, art);
                _afterCollage.Add(tidy);
            }

            _beforeAfter.style.display = DisplayStyle.Flex;
        }

        /// <summary>
        /// A room's own picture, or null. Separate from <see cref="SpriteFor"/>
        /// because that one caches by prop kind and warns about a missing prop;
        /// a room without art is an ordinary state here, not a defect.
        /// </summary>
        private static Texture2D SpriteNamed(string path) =>
            Resources.Load<Texture2D>(path);

        private void HideRoomTransformation()
        {
            if (_beforeAfter != null) _beforeAfter.style.display = DisplayStyle.None;
        }

        private void Update()
        {
            // R for replay, N for next — quick manual testing on desktop
            // builds. No-op on the "levels unavailable" card: _plan is null
            // there (OnEnable returned before building one).
            if (_plan == null) return;
            if (Input.GetKeyDown(KeyCode.R)) StartLevel(_levelIndex);
            if (Input.GetKeyDown(KeyCode.N)) StartLevel(_levelIndex + 1);
        }
    }
}
