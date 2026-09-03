using System;
using System.Collections.Generic;
using CatShelter.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace CatShelter.View
{
    /// <summary>
    /// Task 60-shell-build/28: the first lesson — three taps, no words about
    /// rules.
    ///
    /// **Why it exists.** The owner handed the build to living people on
    /// 2026-09-03 and watched them play. It was not obvious to them that the
    /// game is about collecting three of a kind. That is not a guess about a
    /// hypothetical player; it is what happened. So the game no longer explains
    /// the rule, it walks one real move: tap this, watch it land on the shelf,
    /// tap two more, watch the three vanish.
    ///
    /// **Why a class of its own rather than methods on DebugGameView.** That
    /// file is already 1800 lines and owns five separate features. This one has
    /// a state machine, a timer, a scrim and three phrases, and every one of
    /// them is temporary — it runs once in the life of an install and then
    /// never again. Kept apart, the whole of it can be read (and deleted) in
    /// one place; the board only has to call four methods.
    ///
    /// **Why no holes are cut in the dark.** UI Toolkit has no masking, and
    /// geometry that chases three moving tiles would have to be recomputed on
    /// every layout pass. What is done instead is the cheap and sturdy
    /// equivalent: one dark sheet over the room photograph, a dim class on
    /// every tile except the three, and a bright ring on the one being asked
    /// for. Nothing is measured, nothing is positioned, and it survives a
    /// relayout because it is all classes.
    ///
    /// **Why the taps are taken away rather than merely ignored.** SCOPE, in
    /// the owner's words: "no taps past it — we are waiting for the player to
    /// tap exactly where we tell them, everything else is ignored". Not a
    /// flinch, not a sound. So every tile but the target gets
    /// <see cref="PickingMode.Ignore"/>, and so do the cat and the way back to
    /// the map — a refusal animation would be the game answering, and the point
    /// is that there is nothing to answer.
    /// </summary>
    internal sealed class FirstLesson
    {
        // How long the board holds still after the first take so the player can
        // see where the thing went. Long enough to read four words, short
        // enough that nobody thinks the game has stopped. The flight itself is
        // DebugGameView.FlyMs (150) of this.
        private const int ShelfPauseMs = 1600;

        // After the third take: the flight (150) plus the match pop (170) plus
        // time to read the last phrase, then the dark lifts.
        private const int ClosingMs = 2000;

        // The ring on the target breathes rather than sitting still — a static
        // outline in a dark screen reads as decoration, a moving one reads as
        // an instruction. USS has no keyframes, so the pulse is a class
        // toggled on a timer; see Apply.
        private const int PulseMs = 620;

        private const string Dimmed = "game__tile--lesson-dimmed";
        private const string Cue = "game__tile--lesson-cue";
        private const string CueOn = "game__tile--lesson-cue-on";
        private const string ShelfCue = "game__shelf--lesson-cue";

        /// <summary>The dim for the two controls that are not tiles — the
        /// kitten and the way back to the map. They sit in the header, in front
        /// of the room and so in front of the scrim, and a bright button that
        /// answers nothing is worse than a dim one.</summary>
        private const string OffDimmed = "game__lesson-dimmed";

        private VisualElement _gameRoot;
        private VisualElement _room;
        private VisualElement _pileArea;
        private VisualElement _shelfArea;
        private VisualElement _catSeat;
        private VisualElement _scrim;
        private VisualElement _word;
        private Label _wordText;

        private Action _onPassed;
        private Action _onEnded;

        // What was switched off outside the pile, and what it was set to before.
        // Restored one for one by End rather than by setting everything back to
        // Position: some of these elements were deliberately unpickable already
        // (the chevron inside the way-back plaque is), and a lesson has no
        // business handing the board back in a different state from the one it
        // borrowed it in.
        private readonly List<VisualElement> _mutedElements = new List<VisualElement>();
        private readonly List<PickingMode> _mutedModes = new List<PickingMode>();
        private readonly HashSet<VisualElement> _mutedRoots = new HashSet<VisualElement>();

        /// <summary>How many of the three have been taken: also the index of
        /// the one being asked for next.</summary>
        private int _taken;

        /// <summary>The shelf stop and the closing beat. While it is set,
        /// NOTHING on the board answers a tap — the board is talking.</summary>
        private bool _paused;

        private bool _pulseOn;

        /// <summary>Bumped by <see cref="End"/> so a timer that was already in
        /// flight cannot reach back into a lesson that is over.</summary>
        private int _generation;

        public bool Active { get; private set; }

        /// <summary>
        /// Is the lesson due? Only on the first level, and only for a player
        /// who has not been through it. Static because the board has to answer
        /// this before it builds one.
        /// </summary>
        public static bool WantedOn(Level level, bool lessonSeen) =>
            level != null && !lessonSeen &&
            level.Number == FirstLessonPlan.LevelNumber;

        /// <summary>The item the player is being asked for, or -1 when the
        /// board is holding still and no tap is wanted at all.</summary>
        private int Target =>
            _paused || _taken >= FirstLessonPlan.ItemIds.Length
                ? -1
                : FirstLessonPlan.ItemIds[_taken];

        /// <summary>
        /// Take the board over.
        /// </summary>
        /// <param name="onPassed">Called on the THIRD successful take, not on
        /// the third tap and not when the dark lifts — SCOPE is explicit about
        /// which of those counts. The board persists the flag from here.</param>
        /// <param name="onEnded">Called once everything is put back, so the
        /// board can redraw itself out of the lesson's decoration.</param>
        public void Begin(VisualElement gameRoot, VisualElement room,
                          VisualElement pileArea, VisualElement shelfArea,
                          VisualElement catSeat,
                          Action onPassed, Action onEnded)
        {
            if (Active) return;
            _gameRoot = gameRoot;
            _room = room;
            _pileArea = pileArea;
            _shelfArea = shelfArea;
            _catSeat = catSeat;
            _onPassed = onPassed;
            _onEnded = onEnded;
            _taken = 0;
            _paused = false;
            Active = true;

            BuildScrim();
            BuildWord();
            Say("lesson.tap");
            Apply();

            // Again on the next panel update, because the way back to the map
            // does not exist yet. HouseMapView adds the board component and
            // THEN adds its plaque, both in the same frame, so a lookup made
            // during OnEnable finds nothing to switch off. Apply re-resolves it
            // every time it runs, and this is the run that catches it.
            _gameRoot.schedule.Execute(Apply).ExecuteLater(0);

            // The breathing ring. On the root's scheduler rather than a tile's:
            // Render() destroys and rebuilds every tile on every move, and a
            // timer hung off one of them would die with it.
            int born = _generation;
            _gameRoot.schedule.Execute(() =>
            {
                if (!Active || born != _generation) return;
                _pulseOn = !_pulseOn;
                Apply();
            }).Every(PulseMs);

            Debug.Log("[Lesson] begins: asking for " +
                      string.Join(",", FirstLessonPlan.ItemIds));
        }

        /// <summary>Re-decorate after the board has rebuilt its tiles.
        /// DebugGameView.Render() clears the pile on every move, so everything
        /// this class puts on a tile has to be put back.</summary>
        public void AfterRender()
        {
            if (Active) Apply();
        }

        /// <summary>
        /// One of the three has actually left the pile. Called by the board
        /// after <c>Board.TakeItem</c> succeeded and before it redraws, so the
        /// redraw already carries the next step.
        /// </summary>
        public void Taken(int itemId)
        {
            if (!Active || _paused) return;
            if (itemId != Target) return; // not ours; nothing to advance

            _taken++;
            Debug.Log($"[Lesson] took {itemId}, {_taken} of " +
                      $"{FirstLessonPlan.ItemIds.Length}");

            if (_taken == 1)
            {
                // The stop on the shelf. SCOPE asks for it by name: the player
                // has to SEE where the thing went, and at 150ms of flight
                // followed instantly by another instruction, nobody does.
                Hold("lesson.shelf", ShelfPauseMs, () => Say("lesson.tap"));
                return;
            }

            if (_taken >= FirstLessonPlan.ItemIds.Length)
            {
                // Passed, here and nowhere else. Not on the third TAP (a tap
                // that the board refused taught nothing) and not when the dark
                // lifts (an app killed during the closing beat would ask the
                // player to sit through it all again).
                _onPassed?.Invoke();
                Debug.Log("[Lesson] passed on the third take");
                Hold("lesson.match", ClosingMs, End);
            }
        }

        /// <summary>Freeze the board, say something, and do <paramref name="then"/>
        /// when the time is up.</summary>
        private void Hold(string key, int ms, Action then)
        {
            _paused = true;
            Say(key);
            if (_shelfArea != null && key == "lesson.shelf")
                _shelfArea.AddToClassList(ShelfCue);

            int born = _generation;
            _gameRoot.schedule.Execute(() =>
            {
                if (!Active || born != _generation) return;
                _paused = false;
                _shelfArea?.RemoveFromClassList(ShelfCue);
                then?.Invoke();
                if (Active) Apply();
            }).ExecuteLater(ms);
        }

        /// <summary>
        /// Put the lesson's state onto the board: who is dark, who is lit, and
        /// who may be tapped. Idempotent and cheap — a pile is at most sixty
        /// tiles and nothing here measures or allocates — because it runs on
        /// every redraw and on every pulse.
        /// </summary>
        private void Apply()
        {
            if (!Active || _pileArea == null) return;

            int target = Target;
            bool targetOnScreen = false;

            foreach (var tile in _pileArea.Children())
            {
                // Set by DebugGameView.MakeTile. A tile with no id is not one
                // of ours and is treated as scenery: dark and deaf.
                int id = tile.userData is int value ? value : -1;
                bool chosen = System.Array.IndexOf(FirstLessonPlan.ItemIds, id) >= 0;
                bool isTarget = id == target;
                if (isTarget) targetOnScreen = true;

                tile.EnableInClassList(Dimmed, !chosen);
                tile.EnableInClassList(Cue, isTarget);
                tile.EnableInClassList(CueOn, isTarget && _pulseOn);

                // The whole subtree, not just the tile. A tile can carry a lock
                // badge or a fallback Label, and a child left pickable is picked
                // — the event then BUBBLES to the handler on the tile, which is
                // the one thing this is here to prevent. Not recorded for
                // restoring: Render() throws every one of these away and builds
                // new ones the moment the lesson lets go.
                Deafen(tile);
                if (isTarget) tile.pickingMode = PickingMode.Position;
            }

            // The cat and the way back to the map are the two other things on
            // this screen that answer a tap. Re-resolved every pass rather than
            // captured once: the plaque is added by HouseMapView a moment after
            // the board is built, so there is no single instant at which both
            // are known to exist.
            Mute(_catSeat);
            Mute(_gameRoot?.Q("to-map"));

            if (target >= 0 && !targetOnScreen)
            {
                // The pile no longer holds the item the lesson is pointing at.
                // Unreachable while the level file and FirstLessonPlan agree,
                // which Tests/Core/FirstLessonPlanTests.cs is there to keep
                // true — but if the solver ever regenerates the first pile and
                // that guard is skipped, the alternative to giving up here is a
                // board on which every tap is ignored forever. Stop, say so,
                // and hand the player an ordinary game.
                Debug.LogWarning($"[Lesson] item {target} is not in the pile — " +
                                 "giving up and handing the board back");
                End();
            }
        }

        /// <summary>
        /// Make an element and everything under it deaf to a finger.
        ///
        /// The whole subtree, and this is the correction that cost a screenshot
        /// run: <see cref="PickingMode.Ignore"/> on a parent does NOT stop its
        /// children being picked, and the event a child receives then bubbles up
        /// to the handler registered on the parent. On 2026-09-03 the kitten's
        /// seat was set to Ignore, her portrait — a child of it, at the default
        /// Position — was picked instead, the tap bubbled to the seat, and the
        /// cat card opened in the middle of the lesson. "Ignore the parent" is
        /// not a way to switch a control off in UI Toolkit.
        /// </summary>
        private static void Deafen(VisualElement root)
        {
            if (root == null) return;
            root.pickingMode = PickingMode.Ignore;
            for (int i = 0; i < root.childCount; i++)
                Deafen(root[i]);
        }

        /// <summary>Deafen a control that outlives the redraw, remembering what
        /// each element was set to so <see cref="End"/> can put it back exactly.
        /// Idempotent: Apply calls this on every pulse.</summary>
        private void Mute(VisualElement root)
        {
            if (root == null || !_mutedRoots.Add(root)) return;
            Record(root);
            Deafen(root);
            root.AddToClassList(OffDimmed);
        }

        private void Record(VisualElement element)
        {
            _mutedElements.Add(element);
            _mutedModes.Add(element.pickingMode);
            for (int i = 0; i < element.childCount; i++)
                Record(element[i]);
        }

        private void Unmute()
        {
            for (int i = 0; i < _mutedElements.Count; i++)
                _mutedElements[i].pickingMode = _mutedModes[i];
            foreach (var root in _mutedRoots)
                root.RemoveFromClassList(OffDimmed);
            _mutedElements.Clear();
            _mutedModes.Clear();
            _mutedRoots.Clear();
        }

        private void Say(string key)
        {
            if (_wordText != null) _wordText.text = Shell.Copy.Of(key);
            Debug.Log($"[Lesson] says {key}");
        }

        /// <summary>
        /// The dark over the room.
        ///
        /// A child of the room layer, and that is the trick that saves all the
        /// arithmetic: the room has already been pulled out past the safe-area
        /// padding to reach the glass on every edge
        /// (DebugGameView.FillScreen), so a child of it filling its parent
        /// reaches the same edges for free and keeps doing so when the insets
        /// are recomputed.
        ///
        /// When a room has no art the room layer is hidden and this goes with
        /// it; the lesson then reads off the dimmed tiles alone, which is a
        /// weaker picture but never a broken one. Room 01 ships both frames, so
        /// on the level this actually runs on there is always a photograph to
        /// darken.
        /// </summary>
        private void BuildScrim()
        {
            if (_scrim != null || _room == null) return;
            _scrim = new VisualElement { name = "lesson-scrim" };
            _scrim.AddToClassList("game__lesson-scrim");
            _scrim.pickingMode = PickingMode.Ignore;
            _room.Add(_scrim);
        }

        /// <summary>
        /// The phrase, low on the screen.
        ///
        /// Low because the three props are high: ids 1, 5 and 8 are the first,
        /// fifth and eighth of a six-wide pile, so all three sit in the top two
        /// rows, and a caption under them cannot cover the thing it is pointing
        /// at. Inserted before the overlay for the reason every other element
        /// in this view is — a win or lose card must cover it, not share the
        /// screen with it.
        /// </summary>
        private void BuildWord()
        {
            if (_word != null || _gameRoot == null) return;
            _word = new VisualElement { name = "lesson-word" };
            _word.AddToClassList("game__lesson-word");
            _word.pickingMode = PickingMode.Ignore;

            _wordText = new Label();
            _wordText.AddToClassList("game__lesson-word-text");
            _wordText.pickingMode = PickingMode.Ignore;
            _word.Add(_wordText);

            var overlay = _gameRoot.Q("overlay");
            int at = overlay != null ? _gameRoot.IndexOf(overlay) : -1;
            if (at >= 0) _gameRoot.Insert(at, _word);
            else _gameRoot.Add(_word);
        }

        /// <summary>
        /// Give the board back. Everything this class added is removed and
        /// everything it switched off is switched on, then the board is asked
        /// to redraw itself — which is what actually clears the classes and the
        /// picking modes off the tiles, since Render() builds new ones.
        /// </summary>
        private void End()
        {
            if (!Active) return;
            Active = false;
            _generation++;   // any timer still in flight now belongs to nobody

            _scrim?.RemoveFromHierarchy();
            _word?.RemoveFromHierarchy();
            _scrim = null;
            _word = null;
            _wordText = null;

            _shelfArea?.RemoveFromClassList(ShelfCue);
            Unmute();

            Debug.Log("[Lesson] over, the board is the player's again");
            _onEnded?.Invoke();
        }
    }
}
