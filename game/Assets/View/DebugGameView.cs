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
    /// - hidden kinds render as BLANK tiles (D3), not faded;
    /// - locked kinds show a lock glyph and ignore taps until unlocked (3.11);
    /// - the lose screen offers "one more shelf" but NEVER calls Shelf.AddSlots —
    ///   the booster is a fake door in the MVP (D4).
    /// Styling lives in DebugGame.uss; inline styles only for per-tile colours.
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

        private readonly List<Level> _levels = new();
        private int _levelIndex;

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
                Shell.SaveFile.Clear();
                ShowCard("House complete!",
                    "Every room is tidy.",
                    "Play again", () => StartLevel(0),
                    null, null);
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
            var roomNo = RoomNumberOf(_level.RoomId);
            _title.text = $"Room {roomNo} of 12 · pile {_level.PileIndex + 1}";
            _status.text =
                $"Items left: {_level.Pile.Count - _board.TakenOrder.Count}";

            RenderPile();
            RenderShelf();
        }

        private static int RoomNumberOf(string roomId)
        {
            var digits = roomId.Where(char.IsDigit).ToArray();
            return digits.Length > 0 ? int.Parse(new string(digits)) : 1;
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
                    slot.Add(MakeLabel(item.Kind.Id));

                _shelfArea.Add(slot);
            }
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
                // D3: buried items hide their kind — blank tile, not faded
                tile.AddToClassList("game__tile--hidden");
                return tile;
            }

            // Per-tile hue is genuinely one-off: computed from the kind id.
            tile.style.backgroundColor = HueFor(entry.Item.Kind.Id);
            tile.Add(MakeLabel(entry.Item.Kind.Id, locked ? "🔒" : null));

            if (available && !locked)
                tile.RegisterCallback<ClickEvent>(_ => Take(entry.Item.Id));
            else
                tile.AddToClassList("game__tile--dim");

            return tile;
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
            if (_board.IsOver || !_board.TakeItem(itemId))
                return;

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
            var roomNow = RoomNumberOf(_level.RoomId);
            var next = _levelIndex + 1 < _levels.Count
                ? RoomNumberOf(_levels[_levelIndex + 1].RoomId)
                : -1;
            var lastPileOfRoom = next != roomNow;

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
                Analytics.LevelWin(_level.Number);
                // Permission is asked here, after a level was actually cleared,
                // and only once ever — see Shell/EveningReminder.
                StartCoroutine(Shell.EveningReminder.OnLevelCompleted(this, _level.Number));
                ShowCard(
                    lastPileOfRoom ? "Room clean!" : "Corner cleared!",
                    lastPileOfRoom
                        ? "The kitten likes it better already."
                        : "The room still has another pile.",
                    "Next", () => { HideCard(); StartLevel(_levelIndex + 1); },
                    null, null);
            }
            else
            {
                Analytics.LevelFail(_level.Number);
                // D4: fake door — count intent, grant nothing. AddSlots is NOT called.
                ShowCard("Shelf jammed",
                    $"Levels finished: {_levelIndex}.\n\n" +
                    "Would you keep playing if this were the real game?",
                    "Replay", () =>
                    {
                        HideCard();
                        StartLevel(_levelIndex);   // replay the lost level
                    },
                    "One more shelf", () =>
                    {
                        Analytics.BoosterTap();     // counted...
                        _overlayBody.text = "Coming soon.";  // ...stub shown...
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
            _primaryButton.text = primaryText;
            _primaryButton.clickable = new Clickable(onPrimary);
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
