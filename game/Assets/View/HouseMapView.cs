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
    /// **This is the game's first screen.** A launch with no debug flag file
    /// beside the save comes here, not to the board — see `GameBoot.OnEnable`,
    /// which now falls through to this instead of to `DebugGameView`. The
    /// `housemap.txt` flag that used to be the only way in is retired: a flag
    /// that selects what the app already does is a switch with one position.
    /// A device still carrying the old file behaves exactly as it did.
    ///
    /// It is a hub in both directions. <see cref="StartPlaying"/> swaps the
    /// board in when the open room is tapped; the board carries a plaque in
    /// its top-left corner (<see cref="AddReturnToMap"/>) that swaps this back.
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
        // Mirrors CoatBuilder's own _warned set: log the first miss per asset
        // name, not once per cell per frame.
        private static readonly HashSet<string> _warned = new();

        private void OnEnable() => Build();

        /// <summary>
        /// Draw the map into the panel.
        ///
        /// Separate from <see cref="OnEnable"/> because the way back from the
        /// board has three cases and only two of them go through it: the
        /// component may be absent (added, and OnEnable builds), disabled
        /// (re-enabled, and OnEnable builds), or already enabled — and that
        /// last one gets nothing from `enabled = true`. Calling this covers
        /// all three the same way.
        /// </summary>
        private void Build()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;
            root.Clear();
            // The game's cream. This was near-white while the delivered
            // background still carried its opaque white surround and the page
            // had to match it; the background was cropped and its corners made
            // transparent on 28.08, so the page is the page again.
            root.style.backgroundColor = (Color)new Color32(0xF4, 0xEA, 0xD8, 0xFF);
            root.style.flexDirection = FlexDirection.Column;
            root.style.alignItems = Align.Center;
            // No padding set here. `Shell/SafeArea` owns the panel root's
            // padding — it is how the notch and the home indicator are kept
            // clear — and it re-applies only when the safe area or the screen
            // size changes. A `paddingTop = 20` stood here until 2026-08-28 and
            // was harmless only by luck: SafeArea's first successful pass, one
            // frame after layout, overwrote it on every platform. On the way
            // *back* from the board there is no such pass, and the 20 would
            // have stuck — pulling the map up under the Dynamic Island on the
            // second visit and not the first, which is the kind of bug that
            // gets blamed on the screenshot.

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
            var progress = LoadProgress(pilesPerRoom, loaded.Levels);

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
            // Left pickable. It carries no handler of its own, and marking a
            // parent Ignore is the first thing to suspect when a child stops
            // answering taps — which is exactly what happened here.
            houseBox.pickingMode = PickingMode.Position;
            background.Add(houseBox);

            var artWidth = backgroundArt != null ? backgroundArt.width : 0;
            var artHeight = backgroundArt != null ? backgroundArt.height : 0;
            background.RegisterCallback<GeometryChangedEvent>(_ =>
                FitToPicture(background, houseBox, artWidth, artHeight));

            var openRoom = 0;
            for (int room = 1; room <= pilesPerRoom.Count; room++)
            {
                var total = pilesPerRoom[room - 1];
                var cleared = progress.PilesClearedIn(room);
                var access = progress.AccessFor(room);
                if (access == RoomAccess.Open) openRoom = room;
                var cell = Cell(room, access, total > 0 ? (float)cleared / total : 0f,
                                access == RoomAccess.Open ? StartPlaying : null);
                Place(cell, room);
                houseBox.Add(cell);
            }

            Debug.Log($"[HouseMap] built {pilesPerRoom.Count} rooms, " +
                      $"cursor={progress.CurrentRoom}/{progress.CurrentPile}, " +
                      $"done=[{string.Join(",", progress.RoomsDone)}], " +
                      $"open={openRoom}");
            root.Add(background);

            var legend = new Label(
                "tap the lit number to play it   ·   ticked rooms are done   " +
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
        /// Where the player actually stands, restored through
        /// <see cref="PlayerProgress.Restore"/> — or a fresh, all-dirty
        /// progress when there is no save yet, or the save does not match
        /// the currently shipped room plan (SaveResume's own reasoning: an
        /// unreadable position starts fresh rather than crashing the screen).
        ///
        /// **Read from the saved level, not from the saved cursor**, and that
        /// is not a preference. `GameSave` can carry a cursor, but nothing in
        /// the game ever writes one: every production call is
        /// `GameSave.Write(board, null)` (DebugGameView.cs:172, :436), and
        /// `GameSave.Read` defaults an absent cursor to room 1, pile 0. So the
        /// cursor this screen used to trust said "room 1" for every save ever
        /// written by playing. That was invisible while the map was a checking
        /// tool reached by a flag file with a hand-written save beside it. It
        /// stops being invisible the moment the map is the first screen: a
        /// player four rooms in would have been shown room 1 lit and eleven
        /// rooms locked, tapped it, and landed in room 5 — the same "I chose
        /// nothing and do not know where I am" this task exists to end,
        /// wearing a map.
        ///
        /// The saved level identity is always present and is the same fact the
        /// board resumes from (`SaveResume.TryResume` → `DebugGameView.Resume`,
        /// which replays one `CompletePile` per level before it). Deriving from
        /// it makes the map promise exactly what the board delivers, by
        /// construction rather than by two things being kept in step. If a
        /// cursor is ever written, it will agree with this or the save is
        /// self-contradictory.
        ///
        /// The room is taken as its **ordinal** in the shipped plan, not as the
        /// digits in `room_07`: `PilesPerRoomInOrder` is a list in play order,
        /// and if `LevelLoadPolicy` drops an incomplete room the two stop
        /// matching. The plaques are numbered by the same ordinal, so the map
        /// stays self-consistent either way.
        /// </summary>
        private static PlayerProgress LoadProgress(IReadOnlyList<int> pilesPerRoom,
                                                   IReadOnlyList<Level> levels)
        {
            var text = Shell.SaveFile.Read();
            var saved = GameSave.Read(text);
            if (saved == null)
                return new PlayerProgress(pilesPerRoom);

            var room = RoomOrdinal(levels, saved.RoomId);
            if (room < 1)
            {
                // The save names a room this build does not ship. Same answer
                // as an unreadable save.
                Debug.LogWarning($"[HouseMap] save names {saved.RoomId}, " +
                                 "which is not in the shipped plan — starting fresh");
                return new PlayerProgress(pilesPerRoom);
            }

            // Rooms are played in order, so everything before the cursor is
            // finished. Derived here rather than read, for the same reason as
            // the cursor: nothing writes `roomsdone` either.
            var done = new List<int>();
            for (int i = 1; i < room; i++) done.Add(i);

            try
            {
                return PlayerProgress.Restore(pilesPerRoom, room,
                    saved.PileIndex, done);
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
        /// 1-based position of a room in play order, or 0 when this build
        /// ships no such room. Mirrors <see cref="RoomPlan.PilesPerRoomInOrder"/>'s
        /// own ordering — levels by number, room ids distinct in that order —
        /// so an index into one is an index into the other.
        /// </summary>
        private static int RoomOrdinal(IReadOnlyList<Level> levels, string roomId)
        {
            if (levels == null || string.IsNullOrEmpty(roomId)) return 0;
            var seen = new List<string>();
            foreach (var level in OrderedByNumber(levels))
                if (!seen.Contains(level.RoomId))
                {
                    seen.Add(level.RoomId);
                    if (level.RoomId == roomId) return seen.Count;
                }
            return 0;
        }

        private static List<Level> OrderedByNumber(IReadOnlyList<Level> levels)
        {
            var ordered = new List<Level>(levels);
            ordered.Sort((a, b) => a.Number.CompareTo(b.Number));
            return ordered;
        }

        /// <summary>
        /// <summary>
        /// Where each room sits inside the house, as percentages of the house's
        /// own bounding box: centre x, centre y, width, height. Indexed by room
        /// number, so entry 0 is room 1.
        ///
        /// **The numbers climb.** Odd rooms on the left, even on the right,
        /// bottom to top, and the last two alone under the roof. That is the
        /// whole rule, and it is the rule because the owner read the previous
        /// version and could not follow it: "9 на самом верху, 12 под ней" —
        /// the map had 9 at the apex and 12 beneath it, and nothing about the
        /// order made sense from the outside.
        ///
        /// It made sense from the inside, which is exactly the trap. The
        /// earlier table placed each room where its *picture* belonged in a
        /// house: the attic art at the top, the kitchen on the ground floor,
        /// measured off the background and written up in
        /// tasks/40-art/06-house-map/ROOM-PLACEMENT.md. That is a real property
        /// and it was right while the cells were photographs. The moment the
        /// cells became numbers, the number became the only thing on the
        /// screen, and a scattered sequence of numbers reads as a mistake no
        /// matter how principled the scatter is.
        ///
        /// What was given up, said plainly so nobody "restores" it by accident:
        /// room 9's art is an attic and it now sits mid-house, and room 12's is
        /// a reading nook which happens to still land under the roof. Getting
        /// both properties needs the sloped-ceiling pictures to *be* rooms 11
        /// and 12 — a reassignment of which picture belongs to which room
        /// number, not a layout change, and not one to make quietly.
        ///
        /// The geometry is still measured, not guessed: the house is centred on
        /// local x 50.5%, its walls stand at local x 10.9%–90.1% from local y
        /// 34% down, and above that the roof narrows to about a third of the
        /// width by local y 12% — which is why the top two rooms are single and
        /// centred. Rows are 11% apart and cells 10% tall, so they do not touch.
        /// </summary>
        private static readonly float[][] Placements =
        {
            new[] { 35.0f, 85.0f, 17f, 10f }, // 01
            new[] { 65.0f, 85.0f, 17f, 10f }, // 02
            new[] { 35.0f, 74.0f, 17f, 10f }, // 03
            new[] { 65.0f, 74.0f, 17f, 10f }, // 04
            new[] { 35.0f, 63.0f, 17f, 10f }, // 05
            new[] { 65.0f, 63.0f, 17f, 10f }, // 06
            new[] { 35.0f, 52.0f, 17f, 10f }, // 07
            new[] { 65.0f, 52.0f, 17f, 10f }, // 08
            new[] { 35.0f, 41.0f, 17f, 10f }, // 09
            new[] { 65.0f, 41.0f, 17f, 10f }, // 10
            new[] { 50.5f, 28.5f, 17f, 10f }, // 11 — under the roof
            new[] { 50.5f, 17.0f, 17f, 10f }, // 12 — under the roof, the last room
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
            // cropped on 28.08 from the delivered 928×1664 to the 809×1385 the
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

            if (_warned.Add("house-box"))
                Debug.Log($"[HouseMap] picture {pictureWidth}x{pictureHeight} " +
                          $"at {pictureLeft},{pictureTop} in element {r.width}x{r.height}");
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
        private static VisualElement Cell(int room, RoomAccess access, float cleared,
                                          System.Action onTap)
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

            if (onTap != null)
            {
                // Only the open room answers a tap. A lit plaque that does
                // nothing is worse than no plaque: the owner tapped it, the
                // game did not start, and the screen had promised that it
                // would. The other eleven stay inert on purpose — a locked
                // room that reacts is a different lie.
                // Fires once however the tap arrives. ClickEvent alone was
                // not enough to trust: it worked on Android and did nothing on
                // the iOS simulator, and the difference was not visible from
                // the code. PointerUp is the lower-level event underneath it,
                // and the guard keeps a tap that produces both from starting
                // the game twice.
                var fired = false;
                void Fire(string via)
                {
                    if (fired) return;
                    fired = true;
                    Debug.Log($"[HouseMap] tap room {room} via {via}");
                    onTap();
                }

                plaque.pickingMode = PickingMode.Position;
                plaque.RegisterCallback<ClickEvent>(_ => Fire("click/plaque"));
                plaque.RegisterCallback<PointerUpEvent>(_ => Fire("up/plaque"));
                // The whole cell is the target, not just the plaque, so a
                // thumb landing near the edge still counts.
                wrapper.pickingMode = PickingMode.Position;
                wrapper.RegisterCallback<ClickEvent>(_ => Fire("click/cell"));
                wrapper.RegisterCallback<PointerUpEvent>(_ => Fire("up/cell"));
            }
            else
            {
                plaque.pickingMode = PickingMode.Ignore;
                wrapper.pickingMode = PickingMode.Ignore;
            }

            wrapper.Add(plaque);
            return wrapper;
        }

        /// <summary>
        /// Record a failure of the map's own doing, beside the save.
        ///
        /// Kept from the hunt that produced it: "the button does nothing" has
        /// two very different causes — the tap never arrived, or it arrived and
        /// what it triggered failed — and from outside the app they look
        /// identical. UI Toolkit swallows what an event callback throws, so
        /// without a line like this the second case is silent on a device.
        /// </summary>
        private static void Tapped(string what)
        {
            try
            {
                System.IO.File.AppendAllText(
                    System.IO.Path.Combine(Application.persistentDataPath, "tap.txt"),
                    what + "\n");
            }
            catch (System.Exception)
            {
            }
        }

        /// <summary>
        /// Leave the map and play the open room.
        ///
        /// There is no room to pass along and no new plumbing needed for one:
        /// at most one room is ever open — exactly one until the house is
        /// finished, none after — and it is always the save's cursor,
        /// which is where the board starts anyway. `PlayerProgress.AccessFor`
        /// is what makes that true and a test walks the whole game asserting
        /// it, so "tap the open room" and "start the board" are the same
        /// instruction.
        ///
        /// The map cleared the panel when it took over, so the UXML skeleton
        /// DebugGameView needs — `game-root`, `pile` and the rest — has to be
        /// cloned back before the board is added, or the board finds nothing
        /// and throws.
        /// </summary>
        private void StartPlaying()
        {
            var uid = GetComponent<UIDocument>();
            var root = uid != null ? uid.rootVisualElement : null;
            if (root == null) return;

            // Answer the finger before doing anything else. Building the board
            // takes about a second and a half — levels and prop sprites — and
            // until this was added the screen simply sat there. The owner
            // played it and read that silence exactly as a person would: "кликаю
            // - ничего не происходит... юзер не понимает что происходит, кликает
            // и раздражается, что все зависло." A tap that produces nothing
            // visible is indistinguishable from a tap that was not registered.
            ShowOpening(root, Shell.Copy.Of("map.opening"));

            // Not here, and not on the very next tick either.
            //
            // Two separate reasons, and the second one was measured after the
            // first version shipped looking finished:
            //
            // 1. This runs inside a pointer callback, and the element under the
            //    finger is in the tree the swap destroys — UI Toolkit is still
            //    walking the propagation path through it. Clearing the panel
            //    mid-dispatch is how the tap came to fire, log itself, throw
            //    nothing, and leave the map exactly where it was.
            //
            // 2. `ShowOpening` above is pointless without this delay. Scheduled
            //    for the next tick, the swap ran before the panel repainted, so
            //    the veil was created and destroyed without ever reaching the
            //    screen — six screenshots taken in the second after a tap all
            //    showed the map, unchanged. A loading indicator nobody can see
            //    is worse than none: it looks like the problem is solved.
            //
            // 120ms is about seven frames — enough that the veil is certainly
            // painted. It then stays on screen through the swap for free: the
            // board build blocks the main thread for roughly a second and a
            // half, no repaint happens during it, and the last painted frame is
            // the veil.
            root.schedule.Execute(() => SwapInBoard(uid, root)).ExecuteLater(SwapDelayMs);
        }

        /// <summary>
        /// The measured delay above, named so the way back cannot drift from
        /// the way in. Both directions rebuild the panel from inside a pointer
        /// callback and both put a veil up first, so both need exactly this.
        /// The reasoning is at <see cref="StartPlaying"/>; do not change the
        /// number without re-reading it.
        /// </summary>
        private const long SwapDelayMs = 120;

        /// <summary>
        /// A word and a moving bar over whatever is on screen, the instant a
        /// tap lands. Not a percentage: the work behind it is a level load and
        /// a sprite load with no measurable progress, and a number that jumps
        /// 0→100 lies more than no number at all. What it has to say is "the
        /// tap landed, something is happening", and it says that honestly.
        ///
        /// <paramref name="word"/> may be null, and is on the way back to the
        /// map: `Shell/Copy.cs` holds every string a player reads and I was not
        /// allowed to edit it in this pass, so rather than hard-code an English
        /// literal past the copy table — or print `[map.returning]` at a
        /// player — the return veil is the bar alone. It still answers the
        /// finger, which is the job. The missing key is in NOTES.md.
        /// </summary>
        private void ShowOpening(VisualElement root, string word)
        {
            var veil = new VisualElement { name = "opening" };
            veil.style.position = Position.Absolute;
            veil.style.left = veil.style.right = veil.style.top = veil.style.bottom = 0;
            veil.style.backgroundColor = new Color(0.957f, 0.918f, 0.847f, 0.86f);
            veil.style.alignItems = Align.Center;
            veil.style.justifyContent = Justify.Center;
            veil.pickingMode = PickingMode.Position; // swallow further taps

            var ink = (Color)new Color32(0x33, 0x2A, 0x1E, 0xFF);
            if (word != null)
            {
                var line = new Label(word);
                line.style.fontSize = 17;
                line.style.color = ink;
                line.style.marginBottom = 14;
                veil.Add(line);
            }

            var track = new VisualElement();
            track.style.width = 168;
            track.style.height = 5;
            track.style.backgroundColor = new Color(0.84f, 0.78f, 0.66f);
            Round(track, 3);

            var bar = new VisualElement();
            bar.style.height = Length.Percent(100);
            bar.style.width = Length.Percent(34);
            bar.style.backgroundColor = ink;
            Round(bar, 3);
            track.Add(bar);
            veil.Add(track);
            root.Add(veil);

            // Slides back and forth rather than filling: honest about not
            // knowing how far along it is.
            var step = 0;
            veil.schedule.Execute(() =>
            {
                step = (step + 1) % 40;
                var t = step < 20 ? step : 40 - step;   // 0..20..0
                bar.style.left = Length.Percent(t * 3.3f);
            }).Every(28);
        }

        /// <summary>
        /// Replace the map with the board. Runs a frame after the tap, never
        /// during it.
        /// </summary>
        private void SwapInBoard(UIDocument uid, VisualElement root)
        {
            try
            {
                Debug.Log($"[HouseMap] swap: clearing {root.childCount} children");
                root.Clear();

                // Hand the panel back the way it was found. `Build` styles the
                // root itself — column, centred — and `Clear` removes children,
                // not styles, so the board used to inherit the map's cross-axis
                // centring: `game-root` is `flex-grow: 1` with no width, and
                // centred it is as wide as its contents instead of as wide as
                // the screen. Nobody saw it while this path was a debug flag
                // and the board was normally reached without the map. It is now
                // the only way a player reaches the board, so it has to render
                // identically to the `board.txt` route.
                //
                // `StyleKeyword.Null` removes the inline override rather than
                // setting a value I would have to be sure about — the same
                // idiom `DebugGameView.ShowRoomTransformation` uses to hand a
                // frame back to its stylesheet. Unity's USS defaults differ
                // from the web's in more places than one would guess (this
                // project has already been bitten by `flex-shrink: 0` —
                // DebugGame.uss), so the right move is to un-set, not to guess.
                root.style.flexDirection = StyleKeyword.Null;
                root.style.alignItems = StyleKeyword.Null;

                if (uid.visualTreeAsset == null)
                {
                    root.Add(Message("the board's layout is missing — " +
                                     "DebugGame.uxml is not assigned to the UIDocument"));
                    return;
                }

                uid.visualTreeAsset.CloneTree(root);
                Debug.Log($"[HouseMap] swap: cloned skeleton, game-root=" +
                          $"{root.Q("game-root") != null}, pile={root.Q("pile") != null}");
                enabled = false;
                if (GetComponent<DebugGameView>() == null)
                    gameObject.AddComponent<DebugGameView>();
                Debug.Log("[HouseMap] swap: board added");

                // After the board, not before: DebugGameView builds the cat
                // portrait and the win card's panes into this same tree, and
                // the corner belongs to whoever is drawn last.
                AddReturnToMap(root);
            }
            catch (System.Exception e)
            {
                // The event dispatcher swallows what a callback throws. This
                // one does not get to be silent.
                Debug.LogError($"[HouseMap] swap failed — {e}");
                Tapped($"swap failed — {e.GetType().Name}: {e.Message}");
                root.Clear();
                root.Add(Message($"could not open the room: {e.Message}"));
            }
        }

        /// <summary>
        /// The way back to the map, added to a board that has just been built.
        ///
        /// **Where it sits.** Top-left corner of the board, mirroring the cat
        /// portrait in the top-right (`DebugGame.uss` `.game__cat`). The two
        /// corners above the title are the only space on this screen that is
        /// not the pile, the shelf or the header, and the left one is where a
        /// phone player's thumb already looks for "back". It is the same cream
        /// plaque with the same heavy ink ring as the open room on the map, so
        /// the thing it returns to is drawn on the thing that returns to it.
        ///
        /// **Why it is a child of `game-root` and sits before the overlay.**
        /// Exactly the trick `BuildCatPortrait` uses. The win/lose card is an
        /// absolutely-positioned sibling that covers the whole board, so any
        /// element inserted before it is both dimmed by the card's scrim and
        /// unable to receive a tap through it. That is the behaviour I want and
        /// it costs no code: while a card is up, this button is visibly not the
        /// thing to press, and pressing it does nothing.
        ///
        /// That last part is not cosmetic. `DebugGameView.Finish` **clears the
        /// save** when the player loses (DebugGameView.cs:438) and when the
        /// house is finished, so leaving to the map at that moment would put a
        /// player who has cleared four rooms back at room 1 with no way to
        /// argue. Behind the card, the only exits are the card's own — Replay,
        /// which rewrites the save — and that is the correct set.
        ///
        /// **The arrow is drawn, not typed**, for the reason the map's tick
        /// already records: a device font is not guaranteed to carry ‹ or ←,
        /// and a missing glyph would leave an empty plaque.
        /// </summary>
        private void AddReturnToMap(VisualElement root)
        {
            var gameRoot = root.Q("game-root") ?? root;
            var ink = (Color)new Color32(0x33, 0x2A, 0x1E, 0xFF);
            var cream = (Color)new Color32(0xF6, 0xEE, 0xDC, 0xFF);

            var plaque = new VisualElement { name = "to-map" };
            plaque.style.position = Position.Absolute;
            plaque.style.top = 4;
            plaque.style.left = 4;
            // 44 units against the ~390-unit panel (Shell/PanelSettings.asset)
            // is the 44pt minimum touch target on a phone, and a shade smaller
            // than the 56-unit cat: utility, not the reward.
            plaque.style.width = 44;
            plaque.style.height = 44;
            plaque.style.backgroundColor = cream;
            plaque.style.alignItems = Align.Center;
            plaque.style.justifyContent = Justify.Center;
            Round(plaque, 14);
            Border(plaque, ink, 3);

            // A "<" chevron: an L of two borders, turned 45°. Rotation moves
            // the ink but not the box — the same thing that had to be measured
            // for the done-room tick — so the ink lands about 0.35 of the box
            // to the left of centre and the margin puts it back. That figure is
            // derived, not measured on a screen: check it in the screenshot.
            var chevron = new VisualElement();
            chevron.style.width = 12;
            chevron.style.height = 12;
            chevron.style.marginLeft = 4;
            chevron.style.borderLeftWidth = 3;
            chevron.style.borderBottomWidth = 3;
            chevron.style.borderLeftColor = chevron.style.borderBottomColor = ink;
            chevron.style.rotate = new Rotate(45f);
            chevron.pickingMode = PickingMode.Ignore;
            plaque.Add(chevron);

            // Both events with one guard, for the reason the room plaques
            // carry the same pair: ClickEvent alone worked on Android and did
            // nothing on the iOS simulator, and `up/plaque` is the line the
            // real trace shows firing. Do not "simplify" it away.
            var fired = false;
            void Fire(string via)
            {
                if (fired) return;
                fired = true;
                Debug.Log($"[HouseMap] back to the map via {via}");
                ReturnToMap();
            }

            plaque.pickingMode = PickingMode.Position;
            plaque.RegisterCallback<ClickEvent>(_ => Fire("click"));
            plaque.RegisterCallback<PointerUpEvent>(_ => Fire("up"));

            var overlay = gameRoot.Q("overlay");
            if (overlay != null && overlay.parent == gameRoot)
                gameRoot.Insert(gameRoot.IndexOf(overlay), plaque);
            else
                gameRoot.Add(plaque);
        }

        /// <summary>
        /// Leave the board and show the map.
        ///
        /// **Nothing is saved here, and that is the design.** The board writes
        /// the whole position on every move — `DebugGameView.Take` calls
        /// `Save()` after each tap, and `SaveResume.TryResume` restores taken
        /// order, shelf and triples exactly — so a half-cleared pile is already
        /// on disk before the player reaches for this button. Leaving is a
        /// look at the map, not a decision: tapping the lit room afterwards
        /// resumes the same pile with the same shelf. Writing anything from
        /// here would be a second author of the save file, which is how the
        /// two views end up disagreeing about where the player is.
        ///
        /// The map redraws from that save, so it shows the room just left as
        /// the open one, with its part-cleared bar under the number.
        /// </summary>
        private void ReturnToMap()
        {
            var uid = GetComponent<UIDocument>();
            var root = uid != null ? uid.rootVisualElement : null;
            if (root == null) return;

            // The same courtesy the forward path gets, for the same reason: the
            // map is not free either — it parses all 37 level files through
            // LevelAssets.LoadAll before it can say which room is open — and a
            // tap with no visible answer reads as a frozen game.
            ShowOpening(root, null);
            root.schedule.Execute(() => SwapInMap(uid, root)).ExecuteLater(SwapDelayMs);
        }

        /// <summary>
        /// Replace the board with the map. Runs a frame after the tap, never
        /// during it — <see cref="StartPlaying"/> records what clearing the
        /// panel mid-dispatch costs.
        /// </summary>
        private void SwapInMap(UIDocument uid, VisualElement root)
        {
            try
            {
                var board = GetComponent<DebugGameView>();
                Debug.Log($"[HouseMap] back: clearing {root.childCount} children, " +
                          $"board={(board != null)}");
                root.Clear();

                // Destroyed rather than disabled. Re-enabling it would re-run
                // its OnEnable against a freshly cloned skeleton while its
                // `_catPortrait != null` and `_beforeAfter != null` guards
                // still point at the tree that was just thrown away — so the
                // cat and the win card's before/after would never be inserted
                // into the new one. A fresh component has no such memory.
                // (The board's built cat texture goes unreleased when it dies;
                // DebugGameView owns it and has no OnDestroy. One texture per
                // return trip — recorded in NOTES.md for whoever holds that
                // file.)
                if (board != null) Destroy(board);

                // Three cases, one line each. Absent is impossible here (this
                // instance is running), disabled is what SwapInBoard left
                // behind, and enabled would mean the map never left.
                var wasEnabled = enabled;
                enabled = true;          // fires OnEnable → Build when it was off
                if (wasEnabled) Build(); // ...and OnEnable does not fire when it was on
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[HouseMap] back failed — {e}");
                Tapped($"back failed — {e.GetType().Name}: {e.Message}");
                root.Clear();
                root.Add(Message($"could not open the map: {e.Message}"));
            }
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
            // Paper-on-dark, and it carries its own dark: the page under it is
            // cream (Build sets it, and SafeArea paints the panel cream too),
            // so the old bare cream text was an error message nobody could
            // read on any of the three screens that show one.
            label.style.color = (Color)new Color32(0xF4, 0xEA, 0xD8, 0xFF);
            label.style.backgroundColor = new Color(0.35f, 0.12f, 0.10f);
            label.style.paddingLeft = label.style.paddingRight = 10;
            label.style.paddingTop = label.style.paddingBottom = 8;
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.fontSize = 13;
            return label;
        }
    }
}
