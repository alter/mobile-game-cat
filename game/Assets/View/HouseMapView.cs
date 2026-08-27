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

            // The rooms are placed where they belong in the house, not in a
            // numbered grid. Two earlier versions laid twelve cells out as a
            // flex-wrap grid, which put the attic — sloped ceiling and dormer
            // window, unmistakable once the art existed — in the middle of the
            // house and the kitchen under the roof. A map whose rooms are not
            // where they are in the building is a numbered list drawn on a
            // picture of a house. See tasks/40-art/06-house-map/ROOM-PLACEMENT.md
            // for what each room is and how the coordinates were measured.
            //
            // `houseBox` is the load-bearing part. The background paints with
            // ScaleToFit, so the drawn house is letterboxed inside this element
            // and a percentage of the element is *not* a percentage of the
            // picture — on a tall screen the difference is most of the gap that
            // made the old grid drift over the roofline. The box is therefore
            // sized from the image's own aspect ratio when the layout resolves,
            // and every cell is a percentage of that box.
            var houseBox = new VisualElement();
            houseBox.style.position = Position.Absolute;
            houseBox.pickingMode = PickingMode.Ignore;
            background.Add(houseBox);

            var artWidth = backgroundArt != null ? backgroundArt.width : 0;
            var artHeight = backgroundArt != null ? backgroundArt.height : 0;
            background.RegisterCallback<GeometryChangedEvent>(_ =>
                FitToPicture(background, houseBox, artWidth, artHeight));

            for (int room = 1; room <= pilesPerRoom.Count; room++)
            {
                var state = progress.CellStateFor(room);
                var cell = Cell(room, state);
                Place(cell, room);
                houseBox.Add(cell);
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
        /// <summary>
        /// Where each room sits inside the house, as percentages of the house's
        /// own bounding box: centre x, centre y, width, height.
        ///
        /// Indexed by room number, so entry 0 is room 1. Measured from the art
        /// rather than chosen: `map_background.png` is 928×1664 and the painted
        /// house occupies x 6–93%, y 9–92% of it, which is the box these
        /// percentages are relative to. Full detail and the row-by-row scan
        /// behind the roof line are in tasks/40-art/06-house-map/ROOM-PLACEMENT.md.
        ///
        /// The first version of this table came from the identification pass
        /// and was measured against the file, but its rows were 12% apart with
        /// cells 16% tall — so every cell overlapped the two beside it, and the
        /// columns ran out over the painted frame. The numbers below are the
        /// corrected ones, taken from a row-by-row scan of the silhouette:
        /// the house is centred on local x 50.5%, its walls stand at local x
        /// 10.9%–90.1% from local y 34% down, and above that the roof narrows —
        /// at local y 12% the interior is only about a third of the width.
        /// Rows are 11% apart and cells 10% tall, which is what leaves a gap
        /// between them, and a cell is near enough square (17% × 10% of a box
        /// 807×1381) that its number sits on the picture rather than beside it.
        ///
        /// Two things in here are not arbitrary and should not be "tidied":
        ///
        /// **The top two rows are single-column.** A scan across the background
        /// shows the silhouette reaching its full 640px wall width only at
        /// about 38% down the file; above that it is roof slope. Rooms 09 (the
        /// attic) and 12 (the reading nook) are the only two rooms drawn with a
        /// sloped ceiling, and they are the two placed there. Ten rooms below,
        /// two under the roof — nothing dropped.
        ///
        /// **Room 10 takes the right-hand column.** It is a balcony, the one
        /// room with an outdoor view, so it sits against open air rather than
        /// boxed between two interiors.
        /// </summary>
        private static readonly float[][] Placements =
        {
            new[] { 35.0f, 85.0f, 17f, 10f }, // 01 entry hall
            new[] { 65.0f, 85.0f, 17f, 10f }, // 02 kitchen
            new[] { 35.0f, 63.0f, 17f, 10f }, // 03 living room
            new[] { 65.0f, 41.0f, 17f, 10f }, // 04 bedroom
            new[] { 65.0f, 52.0f, 17f, 10f }, // 05 bedroom
            new[] { 35.0f, 41.0f, 17f, 10f }, // 06 study
            new[] { 65.0f, 74.0f, 17f, 10f }, // 07 bathroom
            new[] { 35.0f, 74.0f, 17f, 10f }, // 08 pantry
            new[] { 50.5f, 17.0f, 17f, 10f }, // 09 attic — under the roof
            new[] { 65.0f, 63.0f, 17f, 10f }, // 10 balcony — outer column
            new[] { 35.0f, 52.0f, 17f, 10f }, // 11 corridor
            new[] { 50.5f, 28.5f, 17f, 10f }, // 12 reading nook — under the roof
        };

        /// <summary>
        /// Position one cell inside the house box from <see cref="Placements"/>.
        ///
        /// A room with no entry — a level file with more than twelve rooms in
        /// it — is laid along the base rather than dropped, so it is visible
        /// and obviously unplaced instead of silently missing. The map is drawn
        /// of this house, and this house has twelve rooms.
        /// </summary>
        private static void Place(VisualElement cell, int room)
        {
            float cx, cy, w, h;
            if (room >= 1 && room <= Placements.Length)
            {
                var p = Placements[room - 1];
                cx = p[0]; cy = p[1]; w = p[2]; h = p[3];
            }
            else
            {
                cx = 8f + (room - Placements.Length - 1) * 12f;
                cy = 97f; w = 10f; h = 6f;
            }

            cell.style.position = Position.Absolute;
            // The cell was built for a flex row and carries 4px of margin on
            // every side. Absolute placement is exact, and a margin would move
            // it off the mark it was measured onto.
            cell.style.marginLeft = 0;
            cell.style.marginRight = 0;
            cell.style.marginTop = 0;
            cell.style.marginBottom = 0;
            cell.style.left = Length.Percent(cx - w / 2f);
            cell.style.top = Length.Percent(cy - h / 2f);
            cell.style.width = Length.Percent(w);
            cell.style.height = Length.Percent(h);
        }

        /// <summary>
        /// Size the house box to the part of the element the picture actually
        /// covers, then inset it to the painted house within that picture.
        ///
        /// ScaleToFit letterboxes: the image keeps its aspect ratio and the
        /// leftover is empty element. Percentages of the element therefore drift
        /// from percentages of the image by however much letterboxing there is,
        /// and that drift is what put cells over the roofline on a 1080×2340
        /// phone. Recomputed on every geometry change, so a rotation or a
        /// different screen is the same code path rather than a new bug.
        ///
        /// With no background art the element is a plain painted panel and
        /// there is nothing to letterbox against, so the box is the element.
        /// </summary>
        private static void FitToPicture(VisualElement background, VisualElement houseBox,
                                         int artWidth, int artHeight)
        {
            var r = background.contentRect;
            if (r.width <= 0f || r.height <= 0f) return;

            float pictureLeft = 0f, pictureTop = 0f;
            float pictureWidth = r.width, pictureHeight = r.height;
            if (artWidth > 0 && artHeight > 0)
            {
                var scale = Mathf.Min(r.width / artWidth, r.height / artHeight);
                pictureWidth = artWidth * scale;
                pictureHeight = artHeight * scale;
                pictureLeft = (r.width - pictureWidth) * 0.5f;
                pictureTop = (r.height - pictureHeight) * 0.5f;
            }

            // The painted house fills the picture: `map_background.png` was
            // cropped on 28.08 from the delivered 928×1664 to the 807×1381 the
            // house actually occupies. It arrived sitting on a white rectangle
            // that showed as a white card behind the map, on a cream screen.
            // The uncropped file is kept at Art/delivery-originals/ — outside
            // Resources, so it is not loaded — and these four numbers are what
            // the crop was taken from, left here so it can be undone.
            //
            // Cropping rather than insetting also means the house is as large
            // as the screen allows instead of 87% of it.
            const float BoxLeft = 0f, BoxRight = 1f;
            const float BoxTop = 0f, BoxBottom = 1f;

            houseBox.style.left = pictureLeft + pictureWidth * BoxLeft;
            houseBox.style.top = pictureTop + pictureHeight * BoxTop;
            houseBox.style.width = pictureWidth * (BoxRight - BoxLeft);
            houseBox.style.height = pictureHeight * (BoxBottom - BoxTop);
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
