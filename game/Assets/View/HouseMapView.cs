using System;
using System.Collections.Generic;
using System.IO;
using CatShelter.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace CatShelter.View
{
    /// <summary>
    /// Task 60-shell-build/03: the house map — twelve rooms, each dirty,
    /// partial or clean, so the twelve separate room improvements read as
    /// one accumulating thing (cat-shelter-mvp.md section 4, "completeness").
    ///
    /// Reached the same way as <see cref="CoatGridView"/> and the capture
    /// screen: drop a `housemap.txt` beside the save. It is a checking tool
    /// for a screen that has no navigation entry point yet — wiring it into
    /// the real room-select flow is 60-shell-build's job elsewhere, once
    /// there is a flow to wire it into.
    ///
    /// Every cell state comes from <see cref="PlayerProgress.CellStateFor"/>,
    /// read off the real save when one exists. There is no second "which
    /// rooms are done" list in this file — that was the task's own condition
    /// (cell state derived from piles cleared vs pile count, nothing kept in
    /// sync by hand).
    ///
    /// 40-art/06-house-map has not delivered art yet: no map_background.png,
    /// no map_room_&lt;nn&gt;_&lt;state&gt;.png. Every load falls back the way
    /// <see cref="CoatBuilder.LoadBase"/> does — a plain painted stand-in and
    /// one warning, not a missing-texture screen.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class HouseMapView : MonoBehaviour
    {
        public static bool Requested =>
            File.Exists(Path.Combine(Application.persistentDataPath, "housemap.txt"));

        // Mirrors CoatBuilder's own _warned set: log the first miss per asset
        // name, not once per cell per frame.
        private static readonly HashSet<string> _warned = new();

        private void OnEnable()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            root.Clear();
            // The delivered map_background.png is opaque and its surround is
            // white — measured at three corners: 255,255,254 / 251,252,252 /
            // 254,254,254. A dark page behind it framed the house in a white
            // rectangle. Match the image rather than fight it.
            root.style.backgroundColor = (Color)new Color32(0xFC, 0xFC, 0xFC, 0xFF);
            root.style.flexDirection = FlexDirection.Column;
            root.style.alignItems = Align.Center;
            root.style.paddingTop = 20;

            var title = new Label("house map: 12 rooms");
            title.style.fontSize = 15;
            title.style.color = (Color)new Color32(0x4A, 0x3B, 0x28, 0xFF);
            title.style.marginBottom = 12;
            root.Add(title);

            var loaded = LevelAssets.LoadAll();
            if (!loaded.CanStart)
            {
                root.Add(Message("no levels loaded — nothing to map"));
                return;
            }

            var plan = new RoomPlan(loaded.Levels);
            var pilesPerRoom = plan.PilesPerRoomInOrder();
            var progress = LoadProgress(pilesPerRoom);

            // Sized in percent, not points. The first version fixed this at
            // 480×420 against placeholder cells, and when the real art arrived
            // on 2026-08-28 the house drew over the grid and the outer columns
            // ran off both edges of a 1080-wide phone. A layout in absolute
            // units is a layout that fits exactly one screen, and nobody knows
            // which one. The delivered background is 928×1664 — portrait — so
            // the rooms go three across and four down inside it rather than
            // four across.
            var background = new VisualElement();
            background.style.width = Length.Percent(94);
            background.style.height = Length.Percent(78);
            background.style.marginBottom = 4;
            background.style.alignItems = Align.Center;
            background.style.justifyContent = Justify.Center;
            var backgroundArt = LoadNamed("Art/map_background");
            if (backgroundArt != null)
            {
                background.style.backgroundImage = new StyleBackground(backgroundArt);
                background.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
            }
            else
            {
                // Painted stand-in: same fallback shape as CoatBuilder.LoadBase
                // when a coat asset is missing — a flat panel instead of a
                // hole in the screen.
                background.style.backgroundColor = (Color)new Color32(0x3C, 0x33, 0x27, 0xFF);
                background.style.borderTopWidth = 2;
                background.style.borderBottomWidth = 2;
                background.style.borderLeftWidth = 2;
                background.style.borderRightWidth = 2;
                background.style.borderTopColor = background.style.borderBottomColor =
                    background.style.borderLeftColor = background.style.borderRightColor =
                    (Color)new Color32(0x55, 0x49, 0x38, 0xFF);
            }

            // Three across, four down, inside the house rather than beside it.
            var grid = new VisualElement();
            grid.style.flexDirection = FlexDirection.Row;
            grid.style.flexWrap = Wrap.Wrap;
            grid.style.width = Length.Percent(52);
            // An explicit height, because a child's percentage height resolves
            // against its parent and an auto-height parent gives it nothing —
            // which is exactly what happened on the first run: the twelve cells
            // collapsed to thumbnails a few pixels tall.
            grid.style.height = Length.Percent(52);
            grid.style.marginTop = Length.Percent(14);
            grid.style.justifyContent = Justify.Center;
            grid.style.alignContent = Align.Center;
            background.Add(grid);

            for (int room = 1; room <= pilesPerRoom.Count; room++)
            {
                var state = progress.CellStateFor(room);
                grid.Add(Cell(room, state));
            }

            root.Add(background);

            var legend = new Label(
                "dirty = square, untouched   ·   partial = split tile, clear boundary   " +
                "·   clean = circle, checked");
            legend.style.fontSize = 10;
            legend.style.whiteSpace = WhiteSpace.Normal;
            legend.style.maxWidth = Length.Percent(92);
            legend.style.unityTextAlign = TextAnchor.MiddleCenter;
            legend.style.color = (Color)new Color32(0x7C, 0x6A, 0x52, 0xFF);
            legend.style.marginTop = 8;
            root.Add(legend);
        }

        /// <summary>
        /// The real save's cursor and finished-rooms list, restored through
        /// <see cref="PlayerProgress.Restore"/> — or a fresh, all-dirty
        /// progress when there is no save yet, or the save does not match
        /// the currently shipped room plan (SaveResume's own reasoning: an
        /// unreadable position starts fresh rather than crashing the screen).
        /// </summary>
        private static PlayerProgress LoadProgress(IReadOnlyList<int> pilesPerRoom)
        {
            var text = Shell.SaveFile.Read();
            var saved = GameSave.Read(text);
            if (saved == null)
                return new PlayerProgress(pilesPerRoom);

            try
            {
                return PlayerProgress.Restore(pilesPerRoom, saved.CursorRoom,
                    saved.CursorPile, saved.RoomsDone);
            }
            catch (ArgumentOutOfRangeException)
            {
                // Save describes a cursor the current room plan cannot hold
                // (e.g. shipped levels changed under it) — same fallback as
                // an unreadable save.
                return new PlayerProgress(pilesPerRoom);
            }
        }

        /// <summary>
        /// One room cell. Real art is `map_room_&lt;nn&gt;_&lt;state&gt;.png`
        /// (art-brief.md section 9); until it exists, the three states are
        /// told apart by silhouette — square / split-tile / circle — not by
        /// a tint of the same shape, because a shade difference alone does
        /// not survive "read the whole house in one glance" (art-brief.md
        /// section 9's own requirement, unmet by three tints of one colour).
        /// </summary>
        private static VisualElement Cell(int room, RoomCellState state)
        {
            var roomNo = room.ToString("00", System.Globalization.CultureInfo.InvariantCulture);
            var stateName = state switch
            {
                RoomCellState.Dirty => "dirty",
                RoomCellState.Partial => "partial",
                _ => "clean",
            };

            var wrapper = new VisualElement();
            // A third of the grid's width, minus its own margins, so twelve
            // cells land as three columns whatever the screen is.
            wrapper.style.width = Length.Percent(31);
            wrapper.style.height = Length.Percent(23);
            wrapper.style.marginLeft = 4;
            wrapper.style.marginRight = 4;
            wrapper.style.marginTop = 4;
            wrapper.style.marginBottom = 4;
            wrapper.style.alignItems = Align.Center;
            wrapper.style.justifyContent = Justify.Center;

            var art = LoadNamed($"Art/map_room_{roomNo}_{stateName}");
            var cell = new VisualElement();
            cell.style.width = Length.Percent(86);
            cell.style.height = Length.Percent(86);

            if (art != null)
            {
                cell.style.backgroundImage = new StyleBackground(art);
                cell.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
            }
            else
            {
                PaintPlaceholder(cell, state);
            }

            var caption = new Label(room.ToString(System.Globalization.CultureInfo.InvariantCulture));
            caption.style.position = Position.Absolute;
            caption.style.color = (Color)new Color32(0xF4, 0xEA, 0xD8, 0xFF);
            caption.style.fontSize = 9;
            caption.style.top = 1;
            caption.style.left = 3;

            wrapper.Add(cell);
            wrapper.Add(caption);
            return wrapper;
        }

        /// <summary>
        /// Shape-coded placeholder — the piece this task owns rather than
        /// generated or faked art (40-art/06-house-map is still `status:todo`).
        ///
        /// Silhouette carries the difference, so it survives greyscale and a
        /// glance from across the room, per art-brief.md section 9:
        ///   dirty   — plain square, flat fill, no icon: nothing here yet.
        ///   partial — split tile, two halves across a hard boundary: matches
        ///             art-prompts.md's own "half-lit, clear boundary" for
        ///             this state, not a blend.
        ///   clean   — full circle with a check mark: a different outline
        ///             from the other two, not just a lighter square.
        ///
        /// What the real art has to preserve: the partial cell needs a crisp
        /// boundary between its two halves (not a gradient — a blend reads as
        /// one smudged tile at cell size, not "half done"), and the three
        /// states must stay tellable apart with the colour removed, the same
        /// property 40-art/06's own QA check verifies on the real files.
        /// </summary>
        private static void PaintPlaceholder(VisualElement cell, RoomCellState state)
        {
            var dark = (Color)new Color32(0x33, 0x2B, 0x22, 0xFF);
            var light = (Color)new Color32(0xF0, 0xD9, 0x9A, 0xFF);

            switch (state)
            {
                case RoomCellState.Dirty:
                    cell.style.backgroundColor = dark;
                    cell.style.borderTopLeftRadius = cell.style.borderTopRightRadius =
                        cell.style.borderBottomLeftRadius = cell.style.borderBottomRightRadius = 2;
                    break;

                case RoomCellState.Partial:
                    cell.style.backgroundColor = dark;
                    cell.style.borderTopLeftRadius = cell.style.borderTopRightRadius =
                        cell.style.borderBottomLeftRadius = cell.style.borderBottomRightRadius = 2;
                    var litHalf = new VisualElement();
                    litHalf.style.position = Position.Absolute;
                    litHalf.style.right = 0;
                    litHalf.style.top = 0;
                    litHalf.style.bottom = 0;
                    litHalf.style.width = new StyleLength(new Length(50, LengthUnit.Percent));
                    litHalf.style.backgroundColor = light;
                    // The hard edge at 50% IS the "clear boundary" — no
                    // gradient between the two children.
                    cell.Add(litHalf);
                    break;

                case RoomCellState.Clean:
                    cell.style.backgroundColor = light;
                    cell.style.borderTopLeftRadius = cell.style.borderTopRightRadius =
                        cell.style.borderBottomLeftRadius = cell.style.borderBottomRightRadius = 36;
                    // Plain ASCII, not a unicode glyph — IL2CPP's default UI
                    // Toolkit font does not carry every codepoint, and a tofu
                    // box here would be worse than no mark at all. The circle
                    // is what has to read from a distance; this is a bonus
                    // up close, and 40-art/06 replaces the whole cell anyway.
                    var check = new Label("OK");
                    check.style.position = Position.Absolute;
                    check.style.color = dark;
                    check.style.fontSize = 18;
                    check.style.left = 22;
                    check.style.top = 24;
                    cell.Add(check);
                    break;
            }
        }

        private static Texture2D LoadNamed(string path)
        {
            var art = Resources.Load<Texture2D>(path);
            if (art == null && _warned.Add(path))
                Debug.LogWarning($"[HouseMapView] no {path} — using a painted " +
                                 "placeholder until 40-art/06-house-map delivers it");
            return art;
        }

        private static VisualElement Message(string text)
        {
            var label = new Label(text);
            label.style.color = (Color)new Color32(0xF4, 0xEA, 0xD8, 0xFF);
            label.style.fontSize = 13;
            return label;
        }
    }
}
