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
        // 60-shell-build/01, 2026-08-28: the header is numerals, pips and a
        // bar instead of "Room 1 of 12 · pile 1 of 1" / "Items left: 36".
        // _title and _status are gone with the two Labels they pointed at.
        private VisualElement _header;
        private Label _roomCount;
        private VisualElement _pips;
        private VisualElement _barFill;
        private Label _leftCount;
        private string _pipsShown;
        private VisualElement _overlay;
        private Label _overlayTitle;
        private Label _overlayBody;
        private Button _primaryButton;
        private Button _secondaryButton;

        // --- the ending card, 60-shell-build/11 -------------------------------
        /// <summary>
        /// Composes the picture the ending card shares. Set from outside; this
        /// view never draws a share image itself, exactly as CatCardScreen
        /// takes its `renderCard` rather than composing one.
        ///
        /// Null until it is wired, and the "Show someone" button is not drawn
        /// while it is null — a button that produces nothing is the fake door
        /// D4 already threw out once.
        /// </summary>
        public Func<byte[]> RenderEndingCard;

        private VisualElement _endingKitten;
        private VisualElement _endingActions;

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
        // The player's own cat, rolled on her first launch and kept — see
        // Shell/CatIdentity. This was `CatTraits.Default` and every player in
        // the world met the same grey tabby, which made a shared picture say
        // nothing about anybody.
        private static CatTraits CatStateTraits => Shell.CatIdentity.Traits;
        private VisualElement _catPortrait;
        // The disc she sits on, and the tap target 60-shell-build/15 will use.
        private VisualElement _catSeat;
        private Texture2D _catTexture;
        private int _catTextureState = -1;

        // --- 60-shell-build/06: the win screen's before/after, built once
        // per room close from the props that room actually held. No room art
        // exists yet (40-art/07 is todo), so a drawn dirty/clean pair is not
        // available; this stands in with the real prop sprites of the room
        // that just closed, scattered for "before" and lined up for "after".
        // --- 60-shell-build/01: motion. Durations mirror DebugGame.uss
        // (.game__fly and .game__pop); change both together or the copy is
        // removed from the screen mid-flight.
        private const int FlyMs = 150;
        private const int PopMs = 170;
        private const float TileHalf = 26f;   // .game__tile is 52
        private const float SlotHalf = 16f;   // .game__slot is 32

        // A layer over the board that Render() never clears. Everything that
        // moves lives here as a throwaway copy — see the header comment in
        // DebugGame.uss for why it cannot live on the real elements.
        private VisualElement _fxLayer;

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
            _header = gameRoot.Q("header");
            _roomCount = gameRoot.Q<Label>("room-count");
            _pips = gameRoot.Q("pips");
            _barFill = gameRoot.Q("bar-fill");
            _leftCount = gameRoot.Q<Label>("left-count");
            _overlay = gameRoot.Q("overlay");
            _overlayTitle = gameRoot.Q<Label>("overlay-title");
            _overlayBody = gameRoot.Q<Label>("overlay-body");
            _primaryButton = gameRoot.Q<Button>("primary");
            _secondaryButton = gameRoot.Q<Button>("secondary");

            if (_pileArea == null)
                throw new InvalidOperationException(
                    "DebugGame.uxml skeleton not found in UIDocument source");

            var _t0 = System.Diagnostics.Stopwatch.StartNew();
            Debug.Log("[Board] enabled, skeleton found");
            // The ending card composes its own picture through the same
            // composer as the kitten's card, with the heart pose instead of her
            // current one: this is the frame a player posts when they finish.
            RenderEndingCard = () => RenderShareCard(SpriteNamed("Art/cat_4_short_base"));

            BuildRoom(uid.rootVisualElement, gameRoot);
            BuildCatPortrait(gameRoot);
            BuildFxLayer(gameRoot);
            BuildBeforeAfter(gameRoot);

            Debug.Log($"[Perf] layers {_t0.ElapsedMilliseconds}ms");
            _levels.Clear();
            var loaded = LevelAssets.LoadAll();
            Debug.Log($"[Perf] LoadAll {_t0.ElapsedMilliseconds}ms");
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
            Debug.Log($"[Perf] board ready {_t0.ElapsedMilliseconds}ms");
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
            RenderRoom();
            RenderHeader();
            RenderPile();
            RenderShelf();
            RenderCat();
        }

        /// <summary>
        /// 60-shell-build/01, 2026-08-28. The three facts the header has to
        /// carry, with the words taken out of all three:
        ///
        ///  - which room of twelve -> "5/12". A fraction, not a clause.
        ///  - which pile of this room's piles -> one pip per pile, filled up
        ///    to the one being played. The same count the cleaned quadrants
        ///    behind the pile already draw (RenderRoom), so it is a legend for
        ///    a picture that is on screen rather than a second sentence.
        ///  - how many items are left -> a bar that empties, and the number
        ///    beside it. The number is the only value on this screen that
        ///    changes on every tap and the only one a player cannot count.
        ///
        /// Nothing here goes through Shell.Copy: there is no longer a word in
        /// the header to translate. That leaves "board.title" and
        /// "board.items_left" unused in Copy.cs — see NOTES.md; deleting them
        /// belongs to whoever owns that file.
        /// </summary>
        private void RenderHeader()
        {
            if (_roomCount == null) return; // older skeleton; nothing to fill

            var roomNo = RoomPlan.RoomNumber(_level.RoomId);
            _roomCount.text =
                $"{roomNo.ToString(CultureInfo.InvariantCulture)}/" +
                _plan.RoomCount.ToString(CultureInfo.InvariantCulture);

            // Rebuilt only when the room or the pile actually changes: Render
            // runs on every tap and these elements never move within a pile.
            var key = $"{_level.RoomId}/{_level.PileIndex}";
            if (_pips != null && _pipsShown != key)
            {
                _pipsShown = key;
                _pips.Clear();
                var piles = _plan.PilesIn(_level.RoomId);
                for (int i = 0; i < piles; i++)
                {
                    var pip = new VisualElement();
                    pip.AddToClassList("game__pip");
                    if (i <= _level.PileIndex) pip.AddToClassList("game__pip--done");
                    _pips.Add(pip);
                }
            }

            var total = _level.Pile.Count;
            var left = total - _board.TakenOrder.Count;
            if (_leftCount != null)
                _leftCount.text = left.ToString(CultureInfo.InvariantCulture);
            if (_barFill != null)
            {
                // Length.Percent, not a pixel width: the bar's track is sized
                // in USS and this never has to know what that size resolved
                // to. Same call BuildRoom already relies on.
                var fraction = total > 0 ? (float)left / total : 0f;
                _barFill.style.width = Length.Percent(Mathf.Clamp01(fraction) * 100f);
            }
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
        // --- 60-shell-build/02: the room behind the pile -----------------
        //
        // The board was a cream page with props on it. The game's promise is a
        // room getting better, and the room was absent from the screen where
        // that work happens.
        //
        // Two layers, both the same size and both ScaleToFit, so they line up
        // pixel for pixel: the dirty room underneath, and up to four quadrant
        // windows onto the clean one over it. A window is 50%×50% with
        // overflow hidden, holding a child sized 200%×200% and offset by a
        // quadrant — which makes that child exactly the size and position of
        // the dirty layer, clipped. Only position, size and overflow, no
        // background-position or background-size: those signatures churn
        // between Unity versions and this has to survive one.
        //
        // Which quadrants are clean comes from the pile index, not from the
        // level data. The task's SCOPE says corner assignment comes from the
        // level files; it does not — they carry id, kind and blocked_by and
        // nothing spatial. A room has 1–4 piles and four quadrants, so pile n
        // cleans quadrant n. Written up in the task's NOTES.
        private VisualElement _room;
        private VisualElement _gameRoot;
        private VisualElement _panelRoot;
        private VisualElement _roomDirty;
        private readonly VisualElement[] _roomClean = new VisualElement[4];
        private string _roomShown;

        private void BuildRoom(VisualElement panelRoot, VisualElement gameRoot)
        {
            if (_room != null) return; // OnEnable can re-run; do not double-insert
            _gameRoot = gameRoot;

            _room = new VisualElement { name = "room" };
            _room.style.position = Position.Absolute;
            _room.style.left = _room.style.right = _room.style.top = _room.style.bottom = 0;
            _room.pickingMode = PickingMode.Ignore;

            _roomDirty = new VisualElement();
            _roomDirty.style.position = Position.Absolute;
            _roomDirty.style.left = _roomDirty.style.right = 0;
            _roomDirty.style.top = _roomDirty.style.bottom = 0;
            _roomDirty.style.unityBackgroundScaleMode = ScaleMode.ScaleAndCrop;
            _room.Add(_roomDirty);

            for (int q = 0; q < 4; q++)
            {
                var window = new VisualElement();
                window.style.position = Position.Absolute;
                window.style.width = Length.Percent(50);
                window.style.height = Length.Percent(50);
                window.style.left = Length.Percent((q % 2) * 50);
                window.style.top = Length.Percent((q / 2) * 50);
                window.style.overflow = Overflow.Hidden;
                window.style.display = DisplayStyle.None;

                var inner = new VisualElement();
                inner.style.position = Position.Absolute;
                inner.style.width = Length.Percent(200);
                inner.style.height = Length.Percent(200);
                inner.style.left = Length.Percent(-(q % 2) * 100);
                inner.style.top = Length.Percent(-(q / 2) * 100);
                inner.style.unityBackgroundScaleMode = ScaleMode.ScaleAndCrop;
                window.Add(inner);

                _room.Add(window);
                _roomClean[q] = window;
            }

            // The props have to stay readable over a photograph. A cream veil
            // at the page colour keeps the room legible as a place without it
            // competing with the tiles the player is actually reading.
            var veil = new VisualElement();
            veil.style.position = Position.Absolute;
            veil.style.left = veil.style.right = veil.style.top = veil.style.bottom = 0;
            veil.style.backgroundColor = new Color(0.957f, 0.918f, 0.847f, 0.62f);
            veil.pickingMode = PickingMode.Ignore;
            _room.Add(veil);

            // On the panel root, not on game-root, and pulled out past the
            // safe-area padding: a room photograph that stops short of the
            // notch and the home indicator reads as a picture pasted on a page
            // rather than as the place the player is standing in. `SafeArea`
            // pads the panel root, so the room cancels that padding with
            // negative insets and covers the glass edge to edge.
            //
            // ScaleAndCrop, not ScaleToFit: the room fills the screen and loses
            // its edges rather than sitting letterboxed in the middle.
            panelRoot.Insert(0, _room);
            _panelRoot = panelRoot;
            panelRoot.RegisterCallback<GeometryChangedEvent>(_ => FillScreen());
        }

        /// <summary>
        /// Pull the room out past the safe-area padding, so it reaches the
        /// glass on every edge instead of stopping at the notch.
        /// </summary>
        private void FillScreen()
        {
            if (_room == null || _panelRoot == null) return;

            // Computed from Screen.safeArea, not read back from the root's
            // padding, and that is the whole point. `SafeArea` applies its
            // padding from Update, retrying until layout is ready; the room is
            // positioned during the first Render, when the padding is still
            // zero, and nothing recomputed it afterwards. Two cream bands at
            // the notch and the home indicator, twice, before this was traced
            // rather than guessed at.
            //
            // Same arithmetic as SafeArea: screen pixels are not panel units,
            // and the panel's width against the screen's gives the factor.
            var width = _panelRoot.resolvedStyle.width;
            if (float.IsNaN(width) || width <= 0f || Screen.width <= 0) return;
            var scale = width / Screen.width;
            var area = Screen.safeArea;

            _room.style.left = -area.xMin * scale;
            _room.style.right = -(Screen.width - area.xMax) * scale;
            _room.style.top = -(Screen.height - area.yMax) * scale;
            _room.style.bottom = -area.yMin * scale;
        }

        /// <summary>
        /// Point the room layers at the current level's room and reveal one
        /// quadrant per pile already cleared. Cheap to call often: it does
        /// nothing unless the room or the pile changed.
        /// </summary>
        private void RenderRoom()
        {
            if (_room == null || _level == null || _plan == null) return;

            var key = $"{_level.RoomId}/{_level.PileIndex}";
            if (_roomShown == key) return;
            _roomShown = key;

            var no = RoomPlan.RoomNumber(_level.RoomId)
                             .ToString("00", CultureInfo.InvariantCulture);
            var dirty = SpriteNamed($"Art/room_{no}_dirty");
            var clean = SpriteNamed($"Art/room_{no}_clean");
            if (dirty == null || clean == null)
            {
                _room.style.display = DisplayStyle.None;
                if (_gameRoot != null)
                    _gameRoot.style.backgroundColor = (Color)new Color32(0xF4, 0xEA, 0xD8, 0xFF);
                Debug.Log($"[Board] room {no}: no art, board stays plain");
                return;
            }

            _room.style.display = DisplayStyle.Flex;
            // Insets applied here as well as from the geometry callback. The
            // callback alone left cream bands at the notch and the home
            // indicator on a real run: it fires when the panel's geometry
            // changes, and SafeArea's padding can be in place before that ever
            // happens, so nothing was ever recomputed.
            FillScreen();
            // Layout is not ready on the first frame — the same reason SafeArea
            // retries — so ask again once it is.
            _room.schedule.Execute(FillScreen).Every(100).Until(
                () => !float.IsNaN(_panelRoot.resolvedStyle.width)
                      && _panelRoot.resolvedStyle.width > 0f);
            // game-root paints itself cream; with a room behind it that cream
            // is a lid over the photograph.
            if (_gameRoot != null) _gameRoot.style.backgroundColor = Color.clear;
            _roomDirty.style.backgroundImage = new StyleBackground(dirty);

            // One quadrant per pile finished before this one. The room's last
            // pile is finished by winning it, and the win card carries the
            // whole clean room — so the board never needs to show four.
            int cleaned = Mathf.Clamp(_level.PileIndex, 0, 4);
            for (int q = 0; q < 4; q++)
            {
                var show = q < cleaned;
                _roomClean[q].style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
                if (show)
                    _roomClean[q][0].style.backgroundImage = new StyleBackground(clean);
            }
            Debug.Log($"[Board] room {no}, pile {_level.PileIndex}: {cleaned} of 4 corners clean");
        }

        private CatCardScreen _catCard;

        /// <summary>
        /// The kitten full screen, with a Share button behind her.
        ///
        /// Built on first tap rather than at startup: it is a screen most
        /// players will never open, and this board already spent a session
        /// paying 21.8 seconds at startup for work nobody had asked for.
        /// </summary>
        private void ShowCatCard()
        {
            var uid = GetComponent<UIDocument>();
            if (uid?.rootVisualElement == null) return;

            if (_catCard == null)
            {
                _catCard = new CatCardScreen();
                // Her room behind her, not paper. `share_room_NN` is the floor
                // the rooms reserve, already cut square and imported readable
                // for the shared picture — the card and the picture then show
                // the same scene, which is the point of a card you share from.
                var roomNo = _level != null
                    ? RoomPlan.RoomNumber(_level.RoomId).ToString("00", CultureInfo.InvariantCulture)
                    : "01";
                // 512, not the board's 256. The portrait in the corner is 52
                // points; here she is nearly the whole width of the screen, and
                // at 256 every stair-step of her outline was four pixels tall.
                // For the default cat this is a baked file and costs nothing;
                // for a cat built from a photograph it is one build, cached to
                // disk, paid on a screen most players never open.
                var big = CoatBuilder.TryBuildFor(CatStateTraits, _progress?.CatState ?? 1, 512);
                _catCard.Build(uid.rootVisualElement, big != null ? big : _catTexture,
                               SpriteNamed($"Art/share_room_{roomNo}"), RenderShareCard);
                // Hide(), not Destroy(): the card is expensive enough to build
                // (see the comment above on 21.8s of unwanted startup work)
                // that it is kept alive and reused on every later tap, same as
                // the board underneath it — Hide just flips display back off.
                _catCard.OnClose = () =>
                {
                    Debug.Log("[Board] cat card closed");
                    _catCard.Hide();
                };
                _catCard.OnShareTapped = () => Debug.Log("[Board] share tapped");
            }
            // Before Show, and on every open: the bowl arrives after room 4 and
            // the blanket after room 8, and this card object outlives both
            // boundaries. See CatCardScreen.SetRewards.
            var state = _progress?.CatState ?? PlayerProgress.CatStateFor(0);
            _catCard.SetRewards(state);

            Debug.Log($"[Board] cat card opened, state={state}");
            _catCard.Show();
        }

        /// <summary>
        /// The picture that leaves the phone. 1080×1080.
        ///
        /// The kitten in her room, which is what the owner asked for and what
        /// the rooms were drawn to allow: their lower third is deliberately
        /// empty floor. `Art/share_room_NN.png` is that floor — a 1080×1080
        /// square cut from the clean room's bottom and imported readable, so
        /// the whole card composes on the CPU.
        ///
        /// Readable and CPU on purpose. Compositing this on the GPU means the
        /// blit path that blanked the iOS simulator for an entire session, and
        /// twelve small readable squares is the cheaper price.
        ///
        /// The clean room, not the dirty one, whatever state the player is in:
        /// this picture leaves the phone, and nobody posts the mess.
        /// </summary>
        private byte[] RenderShareCard() => RenderShareCard(null);

        /// <param name="pose">A cat to draw instead of her current one — the
        /// ending card passes the heart pose. Null means whoever she is now.</param>
        private byte[] RenderShareCard(Texture2D pose)
        {
            const int Side = 1080;
            var card = new Texture2D(Side, Side, TextureFormat.RGBA32, mipChain: false);
            var paper = (Color32)(Color)new Color32(0xF4, 0xEA, 0xD8, 0xFF);

            var px = new Color32[Side * Side];
            for (int i = 0; i < px.Length; i++) px[i] = paper;

            // The room first, underneath everything.
            var roomNo = _level != null
                ? RoomPlan.RoomNumber(_level.RoomId).ToString("00", CultureInfo.InvariantCulture)
                : "01";
            var stage = SpriteNamed($"Art/share_room_{roomNo}");
            if (stage != null && stage.isReadable)
            {
                var sp = stage.GetPixels32();
                int sw = stage.width, sh = stage.height;
                for (int y = 0; y < Side; y++)
                    for (int x = 0; x < Side; x++)
                        px[y * Side + x] = sp[(y * sh / Side) * sw + (x * sw / Side)];
            }
            else if (stage != null)
            {
                Debug.LogWarning($"[Board] share_room_{roomNo} is not readable — " +
                                 "the card falls back to paper");
            }

            var who = pose != null && pose.isReadable ? pose : _catTexture;
            if (who != null && who.isReadable)
            {
                var cat = who.GetPixels32();
                int cw = who.width, ch = who.height;
                // Two thirds of the card, centred, sitting a little low so the
                // game's name has room above her.
                // Large, and low: she is lying on the floor the room left for
                // her, not floating in the middle of the frame.
                int target = (int)(Side * 0.72f);
                int ox = (Side - target) / 2, oy = Side - target - Side / 12;
                for (int y = 0; y < target; y++)
                    for (int x = 0; x < target; x++)
                    {
                        var c = cat[(y * ch / target) * cw + (x * cw / target)];
                        if (c.a == 0) continue;
                        int di = (oy + y) * Side + ox + x;
                        var b = px[di];
                        float a = c.a / 255f;
                        px[di] = new Color32(
                            (byte)(c.r * a + b.r * (1 - a)),
                            (byte)(c.g * a + b.g * (1 - a)),
                            (byte)(c.b * a + b.b * (1 - a)), 255);
                    }
            }

            card.SetPixels32(px);
            card.Apply(updateMipmaps: false);
            var bytes = card.EncodeToPNG();
            Destroy(card);
            return bytes;
        }

        private void BuildCatPortrait(VisualElement gameRoot)
        {
            if (_catPortrait != null) return; // OnEnable can re-run; do not double-insert

            // 60-shell-build/01, 2026-08-28. She used to be an absolutely
            // positioned 56-unit badge in the corner, next to a header
            // sentence that took the rest of the width. She is now the
            // right-hand end of the header row itself, at 104.
            //
            // The seat exists because Paint() sets background-color: clear on
            // whatever it paints — so a background on the portrait itself is
            // wiped the instant her art loads, which is why the cream disc
            // .game__cat used to declare has never appeared on a screen. The
            // seat is a parent Paint never touches.
            //
            // The seat is also where 60-shell-build/15's tap handler goes: it
            // is 104x104, the whole of it hers, and the disc already reads as
            // a pressable object. Nothing is registered here yet.
            _catSeat = new VisualElement { name = "cat-seat" };
            _catSeat.AddToClassList("game__cat-seat");

            _catPortrait = new VisualElement { name = "cat-portrait" };
            _catPortrait.AddToClassList("game__cat");
            _catSeat.Add(_catPortrait);

            // Tapping her opens the card. The owner asked for exactly this: the
            // kitten is the point of the game and was a 52-point icon in a
            // corner, so she is now 104 points and she answers a tap.
            _catSeat.pickingMode = PickingMode.Position;
            _catSeat.RegisterCallback<PointerUpEvent>(_ => ShowCatCard());

            // Appended to the header, after the flexible spacer, so she sits
            // at its right-hand end. The fallback keeps an older skeleton (or
            // a test that builds one by hand) from losing her entirely.
            if (_header != null) _header.Add(_catSeat);
            else gameRoot.Insert(gameRoot.IndexOf(_overlay), _catSeat);
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

            // 256, not the shipped 1024: the portrait is about 52 points, and
            // building at full size cost 21.8 seconds of the board's opening on
            // the iOS simulator — the whole of it. Cached, so coming back to a
            // room does not pay again. See CoatBuilder.Downscale.
            var _tc = System.Diagnostics.Stopwatch.StartNew();
            var built = CoatBuilder.TryBuildFor(CatStateTraits, state, 256);
            Debug.Log($"[Perf] coat {_tc.ElapsedMilliseconds}ms");
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
                // A buried tile is a legitimate thing to try to tap. It answers
                // with a flinch rather than with nothing — see Refuse.
                tile.RegisterCallback<ClickEvent>(_ => Refuse(tile, entry.Item.Id));
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
            {
                tile.RegisterCallback<ClickEvent>(
                    _ => Take(entry.Item.Id, tile, entry.Item.Kind.Id));
            }
            else
            {
                tile.AddToClassList("game__tile--dim");
                // 01-presentation-input: a locked or covered tile used to
                // register no callback at all, so the "[Board] tap refused"
                // line in Take was only reachable once the board was already
                // over — a locked tile answered a tap with total silence, and
                // "the game said no" and "the game has frozen" look the same
                // from the far side of the screen.
                tile.RegisterCallback<ClickEvent>(_ => Refuse(tile, entry.Item.Id));
            }

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

        private void Take(int itemId, VisualElement source = null, string kindId = null)
        {
            var triplesBefore = _board.TriplesCompleted;

            // Everything the animation needs is read HERE, before the model
            // moves and before Render() clears the pile and the shelf. After
            // Render there is no tapped tile left to fly from and no slot left
            // to fly to — both were destroyed and rebuilt.
            //   - the destination is the leftmost free slot, because that is
            //     what Shelf.TryPlace fills (Array.IndexOf(_slots, null));
            //   - the shelf elements on screen right now are the only ones with
            //     a resolved layout, so worldBound is real only at this moment.
            int destSlot = FirstFreeSlot();
            var occupiedBefore = SlotOccupancy();
            var from = source != null ? source.worldBound : default;
            var to = SlotWorldBound(destSlot);

            if (_board.IsOver || !_board.TakeItem(itemId))
            {
                // A tap that changes nothing is worth a line: it is either a
                // locked tile behaving correctly or a bug, and from outside the
                // app those look the same.
                Debug.Log($"[Board] tap {itemId} refused " +
                          $"(over={_board.IsOver})");
                Flinch(source);
                Shell.Feedback.Refused();
                return;
            }
            Debug.Log($"[Board] took {itemId}, shelf={_board.Shelf.Occupied}, " +
                      $"triples={_board.TriplesCompleted}, " +
                      $"available={_board.GetAvailable().Count}");

            // Feedback before the redraw: the tap should answer the finger, not
            // wait for a frame of layout. A match speaks louder than a
            // placement, which is the only difference the player needs to hear.
            bool matched = _board.TriplesCompleted > triplesBefore;
            if (matched)
                Shell.Feedback.Match();
            else
                Shell.Feedback.Place();

            // The picture answers the finger in the same breath as the sound,
            // and for the same reason. Decoration only: the model has already
            // moved, Render() below draws the finished truth, and the copies
            // this spawns fly over the top of it. Nothing here is awaited, so a
            // player tapping faster than 150ms gets a second flyer beside the
            // first rather than a queue to sit through.
            AnimateTake(from, to, kindId, destSlot, occupiedBefore, matched);

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

        // =====================================================================
        // 60-shell-build/01: motion. The OUTCOME asks for placement and match
        // to be animated; until 2026-08-28 nothing in this view moved.
        //
        // Why USS transitions over inline styles, and not
        // VisualElement.experimental.animation: the `experimental` namespace is
        // named that for a reason and its overloads have shifted between Unity
        // versions, and nothing here can be compiled or run on this machine
        // before the owner builds it. transition-property/-duration have been
        // stable public USS since 2021.2. Everything below writes either a
        // float into style.left/top (StyleLength takes a plain float — no
        // struct constructor to get wrong) or toggles a class; the transform
        // work (scale, rotate) is done entirely in USS, so no Scale/Translate/
        // Rotate constructor appears in this file's animation path at all.
        //
        // Why copies rather than the real elements: Render() calls
        // _pileArea.Clear() and _shelfArea.Clear() on EVERY move. A transition
        // started on the tapped tile or on its destination slot dies with the
        // element in the same frame it began. So the tile that flies is a
        // throwaway on _fxLayer, which Render() never touches, and it is
        // spawned from geometry captured before Render runs.
        // =====================================================================

        private void BuildFxLayer(VisualElement gameRoot)
        {
            if (_fxLayer != null) return; // OnEnable can re-run; do not double-insert
            _fxLayer = new VisualElement { name = "fx-layer" };
            _fxLayer.AddToClassList("game__fx-layer");
            // Never eats a tap: a flyer passing over the pile must not swallow
            // the next one while the player is tapping quickly.
            _fxLayer.pickingMode = PickingMode.Ignore;
            // Behind the overlay, so a win or lose card is never flown across.
            int at = _overlay != null ? gameRoot.IndexOf(_overlay) : gameRoot.childCount;
            gameRoot.Insert(at < 0 ? gameRoot.childCount : at, _fxLayer);
        }

        /// <summary>Where Shelf.TryPlace will put the next item: the leftmost
        /// free slot. The shelf does not compact on a match (D16), so this is a
        /// gap in the middle as often as it is the end of the row.</summary>
        private int FirstFreeSlot()
        {
            var slots = _board?.Shelf.Slots;
            if (slots == null) return -1;
            for (int i = 0; i < slots.Count; i++)
                if (slots[i] == null) return i;
            return -1;
        }

        private bool[] SlotOccupancy()
        {
            var slots = _board?.Shelf.Slots;
            if (slots == null) return Array.Empty<bool>();
            var occupied = new bool[slots.Count];
            for (int i = 0; i < slots.Count; i++) occupied[i] = slots[i] != null;
            return occupied;
        }

        /// <summary>The on-screen rectangle of shelf slot <paramref name="index"/>.
        /// Only meaningful before Render() rebuilds the shelf.</summary>
        private Rect SlotWorldBound(int index)
        {
            if (_shelfArea == null || index < 0 || index >= _shelfArea.childCount)
                return default;
            return _shelfArea.ElementAt(index).worldBound;
        }

        private void AnimateTake(Rect from, Rect to, string kindId, int destSlot,
                                 bool[] occupiedBefore, bool matched)
        {
            if (_fxLayer == null) return;
            var origin = _fxLayer.worldBound;
            // Before the first layout pass worldBound is NaN. No animation is
            // better than one that flies to a nonsense coordinate.
            if (float.IsNaN(origin.x) || float.IsNaN(origin.y)) return;

            if (from.width > 0f && to.width > 0f)
                FlyToShelf(from, to, kindId, origin);

            if (matched)
                PopMatchedSlots(occupiedBefore, destSlot, kindId, origin);
        }

        /// <summary>The tapped tile's copy travels from the pile to its slot and
        /// shrinks from tile size to slot size on the way, so it arrives as a
        /// shelf item rather than landing oversized.</summary>
        private void FlyToShelf(Rect from, Rect to, string kindId, Rect origin)
        {
            var flyer = new VisualElement { name = "fx-fly" };
            flyer.AddToClassList("game__fly");
            flyer.pickingMode = PickingMode.Ignore;
            PaintKind(flyer, kindId);

            flyer.style.left = from.center.x - origin.x - TileHalf;
            flyer.style.top = from.center.y - origin.y - TileHalf;
            _fxLayer.Add(flyer);

            // The end position is set one frame later on purpose. Two writes to
            // the same property inside one frame collapse into a single
            // resolved style, and the transition then has nothing to
            // interpolate from — the copy would simply appear at the shelf
            // instead of travelling there. ExecuteLater(0) is "next panel
            // update", which is the next frame.
            _fxLayer.schedule.Execute(() =>
            {
                flyer.style.left = to.center.x - origin.x - SlotHalf;
                flyer.style.top = to.center.y - origin.y - SlotHalf;
                flyer.AddToClassList("game__fly--landed");
            }).ExecuteLater(0);

            // Cleaned up on a timer rather than on TransitionEndEvent: if the
            // transition never runs for any reason, the timer still fires, and
            // a copy stuck over the board would be far worse than a missing
            // animation. The scheduler is the layer's, not the flyer's, so
            // removal does not depend on the element it is removing.
            _fxLayer.schedule.Execute(() => flyer.RemoveFromHierarchy()).ExecuteLater(FlyMs + 40);
        }

        /// <summary>A match: the three slots that just emptied expand and fade
        /// where they stand. A different verb from a placement on purpose — that
        /// one travels, this one bursts, and the two are never confused at a
        /// glance. The slots are found by diffing occupancy across the move,
        /// which is exact because Shelf.TryMatch empties in place (D16).</summary>
        private void PopMatchedSlots(bool[] occupiedBefore, int destSlot,
                                     string kindId, Rect origin)
        {
            var slots = _board.Shelf.Slots;
            for (int i = 0; i < slots.Count && i < occupiedBefore.Length; i++)
            {
                // Held something a moment ago (or is the slot this move just
                // filled) and holds nothing now.
                bool held = occupiedBefore[i] || i == destSlot;
                if (!held || slots[i] != null) continue;

                var bound = SlotWorldBound(i);
                if (bound.width <= 0f) continue;

                var pop = new VisualElement { name = "fx-pop" };
                pop.AddToClassList("game__pop");
                pop.pickingMode = PickingMode.Ignore;
                if (!PaintKind(pop, kindId))
                    pop.AddToClassList("game__pop-flash");

                pop.style.left = bound.center.x - origin.x - SlotHalf;
                pop.style.top = bound.center.y - origin.y - SlotHalf;
                _fxLayer.Add(pop);

                // Held back until the flight lands, so the two read as cause
                // and effect instead of as one blur. 150 + 170 = 320ms of
                // decoration over a board that was already up to date at 0ms.
                _fxLayer.schedule.Execute(() => pop.AddToClassList("game__pop--out"))
                        .ExecuteLater(FlyMs);
                _fxLayer.schedule.Execute(() => pop.RemoveFromHierarchy())
                        .ExecuteLater(FlyMs + PopMs + 40);
            }
        }

        /// <summary>Paints a copy with a kind's art, falling back to the same
        /// coloured square MakeTile uses when a kind has no art file. Returns
        /// whether real art was found.</summary>
        private bool PaintKind(VisualElement element, string kindId)
        {
            if (kindId == null) return false;
            var art = SpriteFor(kindId);
            if (art != null)
            {
                Paint(element, art);
                return true;
            }
            element.style.backgroundColor = HueFor(kindId);
            return false;
        }

        /// <summary>A refused tap. The board did not change and Render() is not
        /// called, which makes this the one place where the real tile survives
        /// long enough to animate — so it flinches in place: away over 90ms,
        /// back over the 110ms the class removal transitions through.</summary>
        private void Refuse(VisualElement tile, int itemId)
        {
            Debug.Log($"[Board] tap {itemId} refused (over={_board?.IsOver})");
            Flinch(tile);
        }

        private void Flinch(VisualElement tile)
        {
            if (tile == null) return;
            // Already flinching: leave it alone rather than restarting from the
            // shrunk state, which would look like a stutter under fast tapping.
            if (tile.ClassListContains("game__tile--refused")) return;
            tile.AddToClassList("game__tile--refused");
            tile.schedule.Execute(() => tile.RemoveFromClassList("game__tile--refused"))
                .ExecuteLater(90);
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
                    // The save is KEPT, with the progress in it.
                    //
                    // It used to be cleared here, and that quietly threw away
                    // the whole game: progress is not stored anywhere else, so
                    // a player who cleared all twelve rooms, saw the ending and
                    // closed the app came back to `done=[], open=1` — a dirty
                    // house, a state-1 kitten, no bowl and no blanket. Finishing
                    // was indistinguishable from never having played. Found by
                    // a full playthrough on 2026-08-29, on both platforms.
                    //
                    // `GameSave` has always been able to write `roomsdone`; no
                    // caller ever passed a progress to it. The board in the file
                    // is the finished one, which `SaveResume` will refuse — and
                    // that is right, there is nothing to resume into. What
                    // survives is the house.
                    Shell.SaveFile.Write(GameSave.Write(_board, _progress));
                    Debug.Log($"[Board] house complete, rooms done=" +
                              $"[{string.Join(",", _progress.RoomsDone)}] " +
                              $"cursor={_progress.CurrentRoom}/{_progress.CurrentPile} " +
                              $"pileIndex={_level.PileIndex}");

                    // 26-room12-reveal: this branch used to call
                    // ShowEndingCard() straight away, so the twelfth room's
                    // own fourth-corner/clean-room reveal — which every other
                    // room's last pile gets on the ordinary card a few lines
                    // below — never reached the screen. lastPileOfRoom is
                    // always true here (the house's last pile is trivially
                    // its room's last pile too), so show that same
                    // win.room_clean card and transformation first, and only
                    // hand off to the ending card from its "next" button.
                    ShowCard(
                        Shell.Copy.Of("win.room_clean.title"),
                        Shell.Copy.Of("win.room_clean.body"),
                        Shell.Copy.Of("win.next"), () => { HideCard(); ShowEndingCard(); },
                        null, null);
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
            // Same rule, same reason, for the ending card's kitten and its two
            // buttons: hidden by default, and ShowEndingCard turns them on.
            HideEndingExtras();

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
        // 60-shell-build/11: the ending card, and the two things it now offers.
        //
        // The owner played to the end and asked "человек доиграл — и что?".
        // What he met was a card with no button on it, a cleared save, and no
        // way to show anybody. This section is the answer: the kitten who was
        // drawn for this screen, one button that hands the card to the phone's
        // share sheet, and one unfilled heart that opens the store's review
        // page. Neither sells anything, so neither is the "call to action" the
        // task's scope rules out — see Copy.cs at house.complete.share.
        //
        // Everything is built lazily, on the one card that uses it, and hidden
        // by default from ShowCard: the win and lose cards must not grow a
        // kitten and a heart because the house happened to end once.
        // =====================================================================

        private void ShowEndingCard()
        {
            ShowCard(Shell.Copy.Of("house.complete.title"),
                     Shell.Copy.Of("house.complete.body"),
                     null, null, null, null);

            BuildEndingExtras();

            if (_endingKitten != null) _endingKitten.style.display = DisplayStyle.Flex;
            if (_endingActions != null) _endingActions.style.display = DisplayStyle.Flex;

            ShowTheWayOff();
        }

        /// <summary>
        /// Put the plaque that leads back to the house ON TOP of the ending
        /// card, so the last screen of the game is not a room with the door
        /// painted on.
        ///
        /// **The complaint, from a full playthrough on 2026-08-29.** After the
        /// twelfth room the card appears and there is nothing to do. It carries
        /// no primary button by design — `ShowCard(..., null, null, ...)` — the
        /// back arrow in the corner does not answer a tap, and there is no
        /// close. The game's last impression is being trapped.
        ///
        /// **Why the arrow was inert, and why that reason has expired.** The
        /// plaque is `HouseMapView.AddReturnToMap`'s, inserted BEFORE the
        /// overlay in `game-root` on purpose: an element in front of the card's
        /// scrim is dimmed by it and cannot be tapped through it, which is
        /// wanted while a card is up. That doc names the danger exactly —
        /// `Finish` used to CLEAR the save when the house was finished, so
        /// leaving to the map from this card would have put a player who
        /// cleared twelve rooms back at room 1 with no way to argue. Since
        /// 2026-08-29 this branch does the opposite: it WRITES the board and
        /// the progress (`Shell.SaveFile.Write(GameSave.Write(_board,
        /// _progress))`, a few lines above), so the map redraws a house with
        /// twelve ticks on it. The exit is now safe on this one card, and only
        /// on this one — the lose card still clears the save and its plaque
        /// stays where it is.
        ///
        /// **Moved, not rebuilt.** Later siblings paint later and are picked
        /// first, so re-adding the same element at the end of the same parent
        /// is the whole fix: no second exit to keep in step with the first, no
        /// navigation logic duplicated out of `HouseMapView`, and the button a
        /// player has been pressing all game is the button that works here.
        /// Nothing about the card itself changes — it says what it said, offers
        /// what it offered, and still does not propose starting over.
        ///
        /// A board reached through `board.txt` has no plaque to lift, because
        /// nothing put one there: that flag is documented in `GameBoot` as
        /// having no way back to the map on purpose. Said out loud in the log
        /// rather than passed over, because "the ending card had no exit" is
        /// exactly the report this method exists to answer.
        /// </summary>
        private void ShowTheWayOff()
        {
            var host = _overlay?.parent;
            var plaque = host?.Q("to-map");
            if (plaque == null || plaque.parent != host)
            {
                Debug.LogWarning("[Board] ending card: no to-map plaque to lift — " +
                                 "this board was not entered from the house map");
                return;
            }

            host.Remove(plaque);
            host.Add(plaque);
            Debug.Log("[Board] ending card: the way back to the house is above it");
        }

        private void HideEndingExtras()
        {
            if (_endingKitten != null) _endingKitten.style.display = DisplayStyle.None;
            if (_endingActions != null) _endingActions.style.display = DisplayStyle.None;
        }

        private void BuildEndingExtras()
        {
            if (_endingActions != null) return; // shown once, but be safe
            var card = _overlayBody?.parent;
            if (card == null) return;

            // The kitten, above the words. cat_4_short_base is a fourth POSE —
            // fat, happy, holding a heart — drawn for this screen and for no
            // other. It is NOT a fourth cat state: PlayerProgress.CatStateFor
            // still returns 1..3 and nothing here asks it anything. Loaded by
            // name, and left greyscale: the coat shader is keyed to the three
            // states and this pose is not one of them.
            var art = SpriteNamed("Art/cat_4_short_base");
            if (art != null)
            {
                _endingKitten = new VisualElement { name = "ending-kitten" };
                _endingKitten.style.width = 148;
                _endingKitten.style.height = 148;
                _endingKitten.style.marginBottom = 10;
                _endingKitten.pickingMode = PickingMode.Ignore;
                Paint(_endingKitten, art);
                card.Insert(card.IndexOf(_overlayBody), _endingKitten);
            }
            else
            {
                // Missing art stays missing rather than leaving a 148px hole,
                // the same rule the prop collage above holds to.
                Debug.LogWarning("[Board] ending card: cat_4_short_base not found");
            }

            _endingActions = new VisualElement { name = "ending-actions" };
            _endingActions.style.flexDirection = FlexDirection.Row;
            _endingActions.style.alignItems = Align.Center;
            _endingActions.style.justifyContent = Justify.Center;
            // Wraps, because the card no longer stretches to hold it. "Show
            // someone" is twelve characters in English and twenty-two in
            // Indonesian, and something has to give: before the card was capped
            // (DebugGame.uss, 2026-08-29) what gave was the card, which grew to
            // nine pixels off the edge of a 1080px screen. The heart dropping
            // under a long button is a worse arrangement than the English one
            // and a better one than a card that fills the screen.
            _endingActions.style.flexWrap = Wrap.Wrap;
            _endingActions.style.maxWidth = Length.Percent(100);

            // "Show someone". Hidden, not disabled, while nothing has been
            // wired to compose the picture — see RenderEndingCard.
            //
            // Buttons.Share rather than Buttons.Primary: it is the same filled
            // tan button with the share mark drawn in front of the label, which
            // is what the kitten's card will carry too. The one override is the
            // gap to the heart — Buttons zeroes every margin on purpose, so
            // spacing is the caller's, and Buttons.Gap is the caller's number.
            if (RenderEndingCard != null)
            {
                var share = Buttons.Share(Shell.Copy.Of("house.complete.share"),
                                          TapEndingShare);
                share.style.marginRight = Buttons.Gap;
                _endingActions.Add(share);
            }
            else
                Debug.LogWarning("[Board] ending card: no RenderEndingCard, share button hidden");

            // The heart. Hidden while the game has no store page, because a
            // heart that opens a page that does not exist is worse than no
            // heart — Shell/Review.cs carries the whole argument and the two
            // store citations behind it.
            if (Shell.Review.Available)
                _endingActions.Add(HeartButton(TapEndingLike));
            else
                Debug.Log("[Board] ending card: no store page yet, heart hidden");

            card.Insert(card.IndexOf(_overlayBody) + 1, _endingActions);
        }

        private void TapEndingShare()
        {
            byte[] png;
            try
            {
                png = RenderEndingCard();
            }
            catch (Exception e)
            {
                // A render that throws must not take the ending down with it.
                // e.Message is diagnostic only — an OS or .NET string, not copy.
                Debug.LogWarning($"[Board] ending card render_failed: {e.Message}");
                return;
            }

            Shell.Share.Image(png, Shell.Copy.Of("house.complete.caption",
                                                 Shell.Copy.Of("card.game_name")));
        }

        private void TapEndingLike() => Shell.Review.Open();

        /// <summary>
        /// The Like heart: an outline, not a filled shape, and drawn rather
        /// than typed.
        ///
        /// Not the character ♡ (U+2661). Nothing in DebugGame.uss sets a font,
        /// so this text renders in Unity's default face, and a glyph that face
        /// may not carry would ship as a blank box on the last screen of the
        /// game. A Painter2D path has no font to be missing from, scales to
        /// whatever box it is given, and is the same stroke on both platforms.
        /// It is the same reasoning, and the same answer, as Buttons.ShareGlyph
        /// next to it — draw the mark, do not import it.
        ///
        /// Not a Buttons.Primary or a Buttons.Secondary: both are filled or
        /// bezelled, and a filled button around an *unfilled* heart argues with
        /// itself. What it does borrow is the number that matters —
        /// Buttons.MinTarget, Apple's 44pt floor — so the hit region is the
        /// same as its neighbour's even though nothing is drawn at its edges.
        /// </summary>
        private static VisualElement HeartButton(Action onClick)
        {
            var button = new VisualElement { name = "ending-like" };
            button.style.width = Buttons.MinTarget;
            button.style.height = Buttons.MinTarget;
            button.style.flexShrink = 0;
            button.style.alignItems = Align.Center;
            button.style.justifyContent = Justify.Center;
            button.AddManipulator(new Clickable(onClick));

            // The press feedback Buttons.Press gives its own buttons, by hand
            // because that method is private and this is not a Button. Same
            // opacity, and the same reason for Leave and Cancel: a finger that
            // slides off never sends PointerUp here, and the heart would be
            // left dimmed.
            button.RegisterCallback<PointerDownEvent>(_ => button.style.opacity = 0.72f);
            button.RegisterCallback<PointerUpEvent>(_ => button.style.opacity = 1f);
            button.RegisterCallback<PointerLeaveEvent>(_ => button.style.opacity = 1f);
            button.RegisterCallback<PointerCancelEvent>(_ => button.style.opacity = 1f);

            var heart = new VisualElement { name = "ending-heart" };
            heart.style.width = 26;
            heart.style.height = 24;
            heart.pickingMode = PickingMode.Ignore; // the taps belong to the 44x44
            heart.generateVisualContent += PaintHeart;
            button.Add(heart);

            return button;
        }

        /// <summary>
        /// One closed heart, stroked and not filled, fitted to whatever box the
        /// element ended up with. The path is written in a 100x100 space and
        /// scaled, so the numbers stay readable and the shape survives a
        /// different size later.
        /// </summary>
        private static void PaintHeart(MeshGenerationContext ctx)
        {
            var rect = ctx.visualElement.contentRect;
            if (rect.width <= 0f || rect.height <= 0f) return;

            float sx = rect.width / 100f;
            float sy = rect.height / 100f;
            Vector2 P(float x, float y) => new Vector2(x * sx, y * sy);

            var painter = ctx.painter2D;
            painter.lineWidth = Mathf.Max(2f, rect.height * 0.10f);
            painter.lineCap = LineCap.Round;
            painter.strokeColor = Buttons.Ink;

            painter.BeginPath();
            painter.MoveTo(P(50, 92));
            // down the left lobe and up over its shoulder to the dimple
            painter.BezierCurveTo(P(20, 68), P(6, 52), P(6, 34));
            painter.BezierCurveTo(P(6, 17), P(19, 8), P(31, 8));
            painter.BezierCurveTo(P(41, 8), P(47, 15), P(50, 22));
            // and back down the right, mirrored
            painter.BezierCurveTo(P(53, 15), P(59, 8), P(69, 8));
            painter.BezierCurveTo(P(81, 8), P(94, 17), P(94, 34));
            painter.BezierCurveTo(P(94, 52), P(80, 68), P(50, 92));
            painter.ClosePath();
            painter.Stroke();
        }

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
