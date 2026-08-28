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
            // which one.
            //
            // How many rooms go across is not decided here any more — see
            // Placements, where it comes from the width the house actually
            // offers at each height.
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
                var total = pilesPerRoom[room - 1];
                var cleared = progress.PilesClearedIn(room);
                var cell = Cell(room, progress.AccessFor(room),
                                total > 0 ? (float)cleared / total : 0f);
                Place(cell, room);
                houseBox.Add(cell);
            }

            root.Add(background);

            var legend = new Label(
                "the lit number is the room to play   ·   ticked rooms are done   " +
                "·   dim rooms are still locked");
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
        /// One room, drawn as its number.
        ///
        /// It used to be the room's own photograph. Twelve of those at cell
        /// size, all desaturated because a dirty room is drawn desaturated,
        /// came out as twelve near-identical grey-green smudges with a white
        /// number nobody could read on them — the owner's verdict on seeing it
        /// running was that you could not tell what any of them were. The
        /// pictures were carrying information the player cannot use at that
        /// size, and hiding the information they can: **which room am I
        /// allowed to play?**
        ///
        /// So the cell answers that first, and by shape rather than by shade,
        /// because a map has to be readable at a glance (art-brief.md
        /// section 9's own requirement, which three tints of one colour do not
        /// meet):
        ///
        /// - **open** — the room to play now. Cream plaque, dark ink number,
        ///   a heavy ring around it. Exactly one room is ever open, which
        ///   `PlayerProgress.AccessFor` guarantees and its tests pin.
        /// - **done** — cleared. Sage plaque, a tick above the number.
        /// - **locked** — ahead of the cursor. Sunk into the wood, thin, dim,
        ///   no ring, and drawn smaller than the other two so the eye skips it.
        ///
        /// How far along a room is still shows, but only where it can mean
        /// something: as a bar under the number of the room being played, and
        /// only when it is neither none nor all. A locked room's dirtiness is
        /// not the player's business yet, and a finished room's is always
        /// clean — drawing either would be decoration.
        ///
        /// The room art is untouched on disk and still named the way
        /// art-brief.md section 9 says; `60-shell-build/02-room-piles` is where
        /// a room's picture belongs, at a size where it can be seen.
        /// </summary>
        private static VisualElement Cell(int room, RoomAccess access, float cleared)
        {
            var ink = (Color)new Color32(0x33, 0x2A, 0x1E, 0xFF);
            var cream = (Color)new Color32(0xF6, 0xEE, 0xDC, 0xFF);
            var sage = (Color)new Color32(0x9D, 0xB3, 0x93, 0xFF);
            var shut = (Color)new Color32(0x6B, 0x4B, 0x30, 0xFF);

            var wrapper = new VisualElement();
            wrapper.style.alignItems = Align.Center;
            wrapper.style.justifyContent = Justify.Center;

            var plaque = new VisualElement();
            plaque.style.alignItems = Align.Center;
            plaque.style.justifyContent = Justify.Center;
            // A locked room is drawn smaller as well as dimmer. Size is the
            // one difference that survives being looked at sideways on a
            // phone, and it makes the open room the largest thing inside the
            // house without any colour doing the work.
            var fill = access == RoomAccess.Locked ? 74f : 100f;
            plaque.style.width = Length.Percent(fill);
            plaque.style.height = Length.Percent(fill);
            Round(plaque, access == RoomAccess.Done ? 999 : 14);

            var number = new Label(
                room.ToString(System.Globalization.CultureInfo.InvariantCulture));
            number.style.unityFontStyleAndWeight = FontStyle.Bold;
            number.style.unityTextAlign = TextAnchor.MiddleCenter;

            switch (access)
            {
                case RoomAccess.Open:
                    plaque.style.backgroundColor = cream;
                    Border(plaque, ink, 3);
                    number.style.color = ink;
                    number.style.fontSize = 22;
                    break;

                case RoomAccess.Done:
                    plaque.style.backgroundColor = sage;
                    Border(plaque, new Color(0.42f, 0.50f, 0.38f), 1);
                    number.style.color = ink;
                    number.style.fontSize = 16;
                    break;

                default: // Locked
                    plaque.style.backgroundColor = shut;
                    number.style.color = new Color(0.85f, 0.78f, 0.68f, 0.45f);
                    number.style.fontSize = 15;
                    break;
            }

            // The tick sits above the digit, so the digit moves down to meet
            // it rather than the two fighting over the middle.
            if (access == RoomAccess.Done) number.style.marginTop = 10;
            plaque.Add(number);

            if (access == RoomAccess.Done)
            {
                // A tick, drawn rather than typed: the font on a device is not
                // guaranteed to carry ✓, and a missing glyph would leave the
                // one state that should read instantly reading as nothing.
                var tick = new VisualElement();
                tick.style.position = Position.Absolute;
                tick.style.width = 14;
                tick.style.height = 7;
                // High enough to clear the digit: rotating the element moves
                // its ink but not its box, so the two overlapped at the
                // obvious value and had to be measured on screen.
                tick.style.top = Length.Percent(9);
                tick.style.borderLeftWidth = 3;
                tick.style.borderBottomWidth = 3;
                tick.style.borderLeftColor = tick.style.borderBottomColor = ink;
                tick.style.rotate = new Rotate(-45f);
                plaque.Add(tick);
            }

            if (access == RoomAccess.Open && cleared > 0f && cleared < 1f)
            {
                // Only the open room shows how far along it is, and only when
                // that is neither none nor all — the two cases the plaque
                // already says.
                var track = new VisualElement();
                track.style.position = Position.Absolute;
                track.style.bottom = Length.Percent(14);
                track.style.width = Length.Percent(56);
                track.style.height = 4;
                track.style.backgroundColor = new Color(0.80f, 0.74f, 0.62f);
                Round(track, 2);

                var filled = new VisualElement();
                filled.style.width = Length.Percent(Mathf.Clamp01(cleared) * 100f);
                filled.style.height = Length.Percent(100);
                filled.style.backgroundColor = ink;
                Round(filled, 2);
                track.Add(filled);
                plaque.Add(track);
            }

            wrapper.Add(plaque);
            return wrapper;
        }

        private static void Round(VisualElement e, float radius)
        {
            e.style.borderTopLeftRadius = e.style.borderTopRightRadius =
                e.style.borderBottomLeftRadius = e.style.borderBottomRightRadius = radius;
        }

        private static void Border(VisualElement e, Color colour, float width)
        {
            e.style.borderTopWidth = e.style.borderBottomWidth =
                e.style.borderLeftWidth = e.style.borderRightWidth = width;
            e.style.borderTopColor = e.style.borderBottomColor =
                e.style.borderLeftColor = e.style.borderRightColor = colour;
        }

        private static Texture2D LoadNamed(string path)
        {
            var art = Resources.Load<Texture2D>(path);
            if (art == null && _warned.Add(path))
                Debug.LogWarning($"[HouseMapView] no {path} — the map falls back " +
                                 "to a painted panel");
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
