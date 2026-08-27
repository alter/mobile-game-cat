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
    /// - the lose screen offers "one more shelf" but NEVER calls Shelf.AddSlots —
    ///   the booster is a fake door in the MVP (D4).
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

            _levels.Clear();
            _levels.AddRange(LevelAssets.LoadAll().OrderBy(l => l.Number));
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
                return;

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

                // Task 6.11: the last pile of the last room ends the house, and
                // it gets the ending screen rather than the ordinary win card
                // followed by one. Reached by playing, shown once, and it does
                // not offer to start over — that is out of scope on purpose.
                if (_plan.Next(_level) == null)
                {
                    Shell.SaveFile.Clear();
                    ShowCard(Shell.Copy.Of("house.complete.title"),
                        Shell.Copy.Of("house.complete.body"),
                        null, null, null, null);
                    return;
                }

                // Permission is asked here, after a level was actually cleared,
                // and only once ever — see Shell/EveningReminder.
                StartCoroutine(Shell.EveningReminder.OnLevelCompleted(this, _level.Number));
                ShowCard(
                    Shell.Copy.Of(lastPileOfRoom ? "win.room_clean.title" : "win.corner.title"),
                    lastPileOfRoom
                        ? Shell.Copy.Of("win.room_clean.body")
                        : Shell.Copy.Of("win.corner.body"),
                    Shell.Copy.Of("win.next"), () => { HideCard(); StartLevel(_levelIndex + 1); },
                    null, null);
            }
            else
            {
                Analytics.LevelFail(_level.Number);
                // D4: fake door — count intent, grant nothing. AddSlots is NOT called.
                ShowCard(Shell.Copy.Of("lose.title"),
                    Shell.Copy.Of("lose.body", _levelIndex),
                    Shell.Copy.Of("lose.replay"), () =>
                    {
                        HideCard();
                        StartLevel(_levelIndex);   // replay the lost level
                    },
                    Shell.Copy.Of("lose.booster"), () =>
                    {
                        Analytics.BoosterTap();     // counted...
                        _overlayBody.text = Shell.Copy.Of("lose.booster.soon");  // ...stub shown...
                        _secondaryButton.SetEnabled(false); // ...level stays lost.
                    });
            }
        }

        private void ShowCard(string title, string body,
                              string primaryText, Action onPrimary,
                              string secondaryText, Action onSecondary)
        {
            _overlayTitle.text = title;
            _overlayBody.text = body;
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

        private void Update()
        {
            // R for replay, N for next — quick manual testing on desktop builds.
            if (Input.GetKeyDown(KeyCode.R)) StartLevel(_levelIndex);
            if (Input.GetKeyDown(KeyCode.N)) StartLevel(_levelIndex + 1);
        }
    }
}
