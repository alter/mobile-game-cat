# Why this tests a pattern, not a feature

Games of this kind that run for hundreds of rounds do not scale by enlarging
the board. They introduce a new complication every thirty to fifty rounds -
blockers, reordering, a different way items arrive - and it is the change, not
the size, that holds the player. Twelve levels of "the same thing, more of it"
is the shape that bores, and that was the shape measured in 08.

One complication, introduced once, is enough for the MVP: it proves the rhythm
works and makes at least one room memorable. The full ladder - in ascending
cost: hidden kinds (09, already MVP), locked items (this task, MVP), a
temporarily blocked shelf slot, paired items taken only consecutively, a kind
requiring four matches instead of three, external supply (items arrive mid-run
rather than lying in the pile from the start), and full reordering after every
triple - is post-MVP design and belongs in cat-shelter-mvp.md section 14, not
in this milestone.

**The delivery rule that must survive into implementation:** one complication
is introduced in its own room, explained wordlessly by the level's own
construction, and only then combined with earlier ones. The room where
something new appears is the room that gets remembered - which also treats the
sameness of the twelve rooms found in 08.

Source: cat-shelter-tasks.md lines 577-586; cat-shelter-mvp.md section 14
("Лестница помех").
