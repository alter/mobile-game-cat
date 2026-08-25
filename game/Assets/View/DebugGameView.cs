using System;
using System.Collections.Generic;
using System.Linq;
using CatShelter.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace CatShelter.View
{
    /// <summary>
    /// Task 20-rules-core/06: debug view of plain rectangles — a build playable
    /// by hand before any art exists. Scene is assembled from code; no scene
    /// YAML, no prefabs.
    ///
    /// Conventions (per DECISIONS.md):
    /// - hidden kinds render as BLANK tiles (D3), not faded;
    /// - locked kinds show a lock glyph and ignore taps until unlocked (D4-adjacent, 3.11);
    /// - the lose screen offers "one more shelf" but NEVER calls Shelf.AddSlots —
    ///   the booster is a fake door in the MVP (D4).
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
        private bool _finished;

        private void OnEnable()
        {
            _levels.AddRange(LevelAssets.LoadAll().OrderBy(l => l.Number));
            BuildUI();
            StartLevel(0);
        }

        private void BuildUI()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            root.style.backgroundColor = new Color(0.96f, 0.92f, 0.85f);
            root.style.paddingTop = 12;
            root.style.paddingBottom = 12;

            var column = new VisualElement();
            column.style.flexGrow = 1;
            column.style.alignItems = Align.Center;
            root.Add(column);

            _title = new Label();
            _title.style.fontSize = 18;
            _title.style.unityFontStyleAndWeight = FontStyle.Bold;
            _title.style.marginBottom = 4;
            column.Add(_title);

            _status = new Label();
            _status.style.fontSize = 13;
            _status.style.marginBottom = 6;
            column.Add(_status);

            // pile area: wrap of tiles
            _pileArea = new VisualElement();
            _pileArea.style.flexDirection = FlexDirection.Row;
            _pileArea.style.flexWrap = Wrap.Wrap;
            _pileArea.style.justifyContent = Justify.Center;
            _pileArea.style.maxWidth = 380;
            _pileArea.style.marginBottom = 14;
            column.Add(_pileArea);

            // shelf area: nine slots in one row
            _shelfArea = new VisualElement();
            _shelfArea.style.flexDirection = FlexDirection.Row;
            column.Add(_shelfArea);

            // overlay for win/lose cards
            _overlay = new VisualElement();
            _overlay.style.position = Position.Absolute;
            _overlay.style.left = 0;
            _overlay.style.right = 0;
            _overlay.style.top = 0;
            _overlay.style.bottom = 0;
            _overlay.style.backgroundColor = new Color(0, 0, 0, 0.55f);
            _overlay.style.alignItems = Align.Center;
            _overlay.style.justifyContent = Justify.Center;
            _overlay.style.display = DisplayStyle.None;
            root.Add(_overlay);

            var card = new VisualElement();
            card.style.backgroundColor = Color.white;
            card.style.borderTopLeftRadius = 16;
            card.style.borderTopRightRadius = 16;
            card.style.borderBottomLeftRadius = 16;
            card.style.borderBottomRightRadius = 16;
            card.style.paddingTop = 24;
            card.style.paddingBottom = 24;
            card.style.paddingLeft = 28;
            card.style.paddingRight = 28;
            card.style.maxWidth = 320;
            _overlay.Add(card);

            _overlayTitle = new Label();
            _overlayTitle.style.fontSize = 17;
            _overlayTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            card.Add(_overlayTitle);

            _overlayBody = new Label();
            _overlayBody.style.fontSize = 13;
            _overlayBody.style.whiteSpace = WhiteSpace.Normal;
            _overlayBody.style.marginTop = 8;
            card.Add(_overlayBody);

            _primaryButton = new Button(OnPrimary);
            _primaryButton.text = "Continue";
            _primaryButton.style.marginTop = 14;
            card.Add(_primaryButton);

            _secondaryButton = new Button(OnSecondary);
            _secondaryButton.text = "One more shelf";
            _secondaryButton.style.marginTop = 8;
            card.Add(_secondaryButton);
        }

        private void StartLevel(int index)
        {
            if (index >= _levels.Count)
            {
                ShowCard("House complete!",
                    "Every room is tidy.",
                    "Play again", () => StartLevel(0),
                    secondaryText: null, onSecondary: null);
                return;
            }
            _levelIndex = index;
            _level = _levels[index];
            _board = new Board(_level);
            _finished = false;
            Analytics.LevelStart(_level.Number);
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
            foreach (var entry in _level.Pile)
            {
                if (_board.TakenOrder.Contains(entry.Item.Id)) continue;
                _pileArea.Add(MakeTile(entry, clickable: true));
            }
        }

        private void RenderShelf()
        {
            _shelfArea.Clear();
            for (int i = 0; i < _board.Shelf.Capacity; i++)
            {
                var slot = new VisualElement();
                slot.style.width = 34;
                slot.style.height = 38;
                slot.style.marginLeft = 2;
                slot.style.marginRight = 2;
                slot.style.borderTopLeftRadius = 7;
                slot.style.borderTopRightRadius = 7;
                slot.style.borderBottomLeftRadius = 7;
                slot.style.borderBottomRightRadius = 7;
                slot.style.backgroundColor = new Color(1, 1, 1, 0.5f);
                slot.style.alignItems = Align.Center;
                slot.style.justifyContent = Justify.Center;

                var item = _board.Shelf.Slots[i];
                if (item != null)
                    slot.Add(MakeLabel(item.Kind.Id));

                _shelfArea.Add(slot);
            }
        }

        private VisualElement MakeTile(PileEntry entry, bool clickable)
        {
            var revealed = IsRevealed(entry);
            var available = !clickable || _board.GetAvailable()
                .Any(a => a.Id == entry.Item.Id);

            var tile = new VisualElement();
            tile.style.width = 52;
            tile.style.height = 52;
            tile.style.marginLeft = 3;
            tile.style.marginRight = 3;
            tile.style.marginBottom = 3;
            tile.style.borderTopLeftRadius = 10;
            tile.style.borderTopRightRadius = 10;
            tile.style.borderBottomLeftRadius = 10;
            tile.style.borderBottomRightRadius = 10;
            tile.style.alignItems = Align.Center;
            tile.style.justifyContent = Justify.Center;

            var locked = _board.IsLockedByComplication(entry.Item);

            if (!revealed)
            {
                // D3: buried items hide their kind — blank tile, not faded
                tile.style.backgroundColor = new Color(0.73f, 0.66f, 0.55f);
                return tile;
            }

            tile.style.backgroundColor = HueFor(entry.Item.Kind.Id);
            tile.Add(MakeLabel(entry.Item.Kind.Id, locked ? "🔒" : null));

            if (available && !locked && clickable)
            {
                tile.RegisterCallback<ClickEvent>(_ => Take(entry.Item.Id));
                tile.style.opacity = 1f;
            }
            else
            {
                tile.style.opacity = 0.45f;
            }
            return tile;
        }

        private static Label MakeLabel(string kindId, string overrideText = null)
        {
            var label = new Label(overrideText ?? KindShort(kindId));
            label.style.color = Color.white;
            label.style.fontSize = 11;
            return label;
        }

        private static string KindShort(string kindId)
        {
            var digits = kindId.Where(char.IsDigit).ToArray();
            return digits.Length > 0 ? new string(digits) : kindId[..2];
        }

        private bool IsRevealed(PileEntry entry) =>
            !_board.TakenOrder.Contains(entry.Item.Id)
            && entry.BlockedBy.All(b => _board.TakenOrder.Contains(b))
            && !_board.IsLockedByComplication(entry.Item);

        private void Take(int itemId)
        {
            if (_finished || _board.IsOver) return;
            if (!_board.TakeItem(itemId))
                return;

            if (_board.IsOver)
            {
                _finished = true;
                Finish();
                return;
            }
            Render();
        }

        private void Finish()
        {
            var roomNow = RoomNumberOf(_level.RoomId);
            var next = _levelIndex + 1 < _levels.Count
                ? RoomNumberOf(_levels[_levelIndex + 1].RoomId)
                : -1;
            var lastPileOfRoom = next != roomNow;

            if (_board.Outcome == GameOutcome.Win)
            {
                Analytics.LevelWin(_level.Number);
                ShowCard(
                    lastPileOfRoom ? "Room clean!" : "Corner cleared!",
                    lastPileOfRoom
                        ? "The kitten likes it better already."
                        : "The room still has another pile.",
                    "Next", () => { HideCard(); StartLevel(_levelIndex + 1); },
                    secondaryText: null, onSecondary: null);
            }
            else
            {
                Analytics.LevelFail(_level.Number);
                // D4: fake door — count intent, grant nothing. AddSlots is NOT called.
                ShowCard("Shelf jammed",
                    $"Levels finished: {_levelIndex}.\n\n" +
                    "Would you keep playing if this were the real game?",
                    "Send answer", () =>
                    {
                        HideCard();
                        StartLevel(_levelIndex);   // replay the lost level
                    },
                    secondaryText: "One more shelf",
                    onSecondary: () =>
                    {
                        Analytics.BoosterTap();     // counted...
                        _overlayBody.text = "Coming soon.";  // ...stub shown...
                        _secondaryButton.SetEnabled(false); // ...level stays lost.
                        _primaryButton.text = "Replay";
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
            _overlay.style.display = DisplayStyle.Flex;
        }

        private void HideCard() => _overlay.style.display = DisplayStyle.None;

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
